using System;
using System.Collections.Generic;
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator
{
    /// <summary>
    /// Owns the decorator list, the per-row dispatch and the failure isolation.
    ///
    /// A decorator that throws is disabled for the rest of the domain and reported exactly once, so a bug in
    /// one decoration can neither break the Hierarchy window nor spam the console.
    /// </summary>
    internal static class DecoratorHost
    {
        private readonly struct BoundRow
        {
            public readonly HierarchyWindow Window;
            public readonly HierarchyView View;
            public readonly HierarchyNode Node;
            public readonly EntityId Id;

            public BoundRow(HierarchyWindow window, HierarchyView view, HierarchyNode node, EntityId id)
            {
                Window = window;
                View = view;
                Node = node;
                Id = id;
            }
        }

        private static readonly IRowDecorator[] s_Decorators =
        {
            new RowTintDecorator(),
            new TreeLineDecorator(),
            new HeaderDecorator(),
            new IndicatorDecorator(),
            new ComponentIconDecorator()
        };

        // Zero cost while the profiler is off, and the only honest way to measure the bind path.
        // See PERFORMANCE.md for the methodology these feed.
        private static readonly ProfilerMarker s_DecorateMarker = new ProfilerMarker("HierarchyDecorator.DecorateRow");

        private static readonly HashSet<string> s_Failed = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<HierarchyViewItem, BoundRow> s_Live = new Dictionary<HierarchyViewItem, BoundRow>();
        private static readonly List<HierarchyViewItem> s_Scratch = new List<HierarchyViewItem>(128);

        /// <summary>Emergency kill switch driven by Tools ▸ Hierarchy Decorator ▸ Disable All Decorations.</summary>
        private static bool s_Suspended;

        private static bool s_RefreshScheduled;

        public static bool Suspended => s_Suspended;

        public static IReadOnlyCollection<string> FailedDecorators => s_Failed;

        public static void Suspend(bool suspended)
        {
            if (s_Suspended == suspended)
            {
                return;
            }

            s_Suspended = suspended;
            RefreshAllLiveRows();
        }

        public static void ClearFailures()
        {
            s_Failed.Clear();
            RefreshAllLiveRows();
        }

        internal static void OnBindViewItem(HierarchyWindow window, HierarchyView view, HierarchyViewItem item)
        {
            if (item == null || view == null)
            {
                return;
            }

            EntityId id = default;
            HierarchyNode node = item.Node;

            if (node != HierarchyNode.Null)
            {
                id = view.Source.GetEntityIdFromNode(node);
            }

            s_Live[item] = new BoundRow(window, view, node, id);

            Decorate(window, view, item, node, id);
        }

        internal static void OnUnbindViewItem(HierarchyWindow window, HierarchyView view, HierarchyViewItem item)
        {
            if (item == null)
            {
                return;
            }

            // item.Node is already HierarchyNode.Null here. Unity explicitly recommends against reverting
            // styling on unbind because rows are recycled, so this only forgets the row.
            s_Live.Remove(item);
        }

        internal static void OnUnbindView(HierarchyView view)
        {
            if (view == null)
            {
                s_Live.Clear();
                return;
            }

            s_Scratch.Clear();

            foreach (KeyValuePair<HierarchyViewItem, BoundRow> pair in s_Live)
            {
                if (pair.Value.View == view)
                {
                    s_Scratch.Add(pair.Key);
                }
            }

            for (int i = 0; i < s_Scratch.Count; i++)
            {
                s_Live.Remove(s_Scratch[i]);
            }

            s_Scratch.Clear();
        }

        /// <summary>Re-runs decoration for the currently visible rows whose object changed.</summary>
        internal static void RefreshLiveRows(HashSet<EntityId> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return;
            }

            ComponentsColumn.RefreshBoundCells(ids);

            if (s_Live.Count == 0)
            {
                return;
            }

            s_Scratch.Clear();

            foreach (KeyValuePair<HierarchyViewItem, BoundRow> pair in s_Live)
            {
                if (ids.Contains(pair.Value.Id))
                {
                    s_Scratch.Add(pair.Key);
                }
            }

            RedecorateScratch();
        }

        /// <summary>
        /// Coalesces a full refresh into the next editor tick. Used by the settings window, where a slider
        /// drag would otherwise re-decorate every visible row once per frame.
        /// </summary>
        internal static void RequestRefreshAllLiveRows()
        {
            if (s_RefreshScheduled)
            {
                return;
            }

            s_RefreshScheduled = true;
            EditorApplication.delayCall += FlushScheduledRefresh;
        }

        private static void FlushScheduledRefresh()
        {
            s_RefreshScheduled = false;
            RefreshAllLiveRows();
        }

        internal static void RefreshAllLiveRows()
        {
            ComponentsColumn.RefreshBoundCells();

            if (s_Live.Count == 0)
            {
                return;
            }

            s_Scratch.Clear();

            foreach (KeyValuePair<HierarchyViewItem, BoundRow> pair in s_Live)
            {
                s_Scratch.Add(pair.Key);
            }

            RedecorateScratch();
        }

        private static void RedecorateScratch()
        {
            for (int i = 0; i < s_Scratch.Count; i++)
            {
                HierarchyViewItem item = s_Scratch[i];

                if (!s_Live.TryGetValue(item, out BoundRow row))
                {
                    continue;
                }

                if (item.panel == null)
                {
                    // The row left the panel without an unbind (window closed); drop it.
                    s_Live.Remove(item);
                    continue;
                }

                Decorate(row.Window, row.View, item, row.Node, row.Id);
            }

            s_Scratch.Clear();
        }

        private static void Decorate(HierarchyWindow window, HierarchyView view, HierarchyViewItem item, HierarchyNode node, EntityId id)
        {
            using ProfilerMarker.AutoScope scope = s_DecorateMarker.Auto();

            HierarchyDecoratorSettings settings = HierarchyDecoratorSettings.instance;
            bool active = !s_Suspended && HierarchyDecoratorUserSettings.instance.Enabled;

            GameObject gameObject = null;

            if (active && node != HierarchyNode.Null)
            {
                HierarchyNodes.TryGetGameObject(item, out gameObject);
            }

            RowData data = null;

            if (gameObject != null)
            {
                data = DecorationCache.GetOrCreate(id);
                DecorationCache.Ensure(data, gameObject, settings, RequiredFacets(settings));
            }

            RowContext context = new RowContext(
                window,
                view,
                item,
                node,
                gameObject,
                id,
                data,
                settings,
                EditorGUIUtility.isProSkin,
                view.Filtering,
                active);

            for (int i = 0; i < s_Decorators.Length; i++)
            {
                IRowDecorator decorator = s_Decorators[i];

                if (s_Failed.Contains(decorator.Id))
                {
                    continue;
                }

                try
                {
                    decorator.Apply(in context);
                }
                catch (Exception e)
                {
                    s_Failed.Add(decorator.Id);
                    HierarchyLog.Once(
                        "decorator:" + decorator.Id,
                        $"The '{decorator.Id}' decoration threw and has been disabled for this session. " +
                        "Use Tools > Hierarchy Decorator > Re-enable Failed Decorations after fixing the cause.",
                        e);
                }
            }
        }

        /// <summary>
        /// Which facets to fill before dispatching. This is derived from settings rather than from the
        /// decorator list on purpose: the component slice is the only expensive facet, and whether anything
        /// needs it depends on the user's configuration, not on which decorators exist.
        /// </summary>
        private static CacheFacet RequiredFacets(HierarchyDecoratorSettings settings)
        {
            CacheFacet facets = CacheFacet.Name;

            if (settings.ComponentIcons.enabled || settings.Indicators.showMissingScripts)
            {
                facets |= CacheFacet.Components;
            }

            return facets;
        }

        internal static void Reset()
        {
            s_Live.Clear();
            s_Scratch.Clear();
            ComponentsColumn.ResetBoundCells();
        }
    }
}
