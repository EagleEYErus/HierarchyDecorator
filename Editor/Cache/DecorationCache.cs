using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator
{
    /// <summary>
    /// Per-GameObject decoration data, keyed by <see cref="EntityId"/>.
    ///
    /// Filled on <c>BindViewItem</c> - which fires when a row scrolls into view or its data changes, not on
    /// every repaint - and invalidated per facet from <see cref="ChangeTracker"/>.
    /// </summary>
    internal static class DecorationCache
    {
        /// <summary>Hard ceiling. Reaching it means a scene far larger than the cache was designed for.</summary>
        private const int MaxEntries = 32768;

        private static readonly Dictionary<EntityId, RowData> s_Rows = new Dictionary<EntityId, RowData>(1024);
        private static readonly Dictionary<Type, ComponentDisplay> s_RuleTable = new Dictionary<Type, ComponentDisplay>(64);
        private static readonly List<Type> s_StackedTypes = new List<Type>(16);

        private static int s_RuleTableRevision = -1;

        public static int Count => s_Rows.Count;

        public static void Clear()
        {
            foreach (KeyValuePair<EntityId, RowData> pair in s_Rows)
            {
                pair.Value.ReleaseReferences();
            }

            s_Rows.Clear();
        }

        public static void Invalidate(EntityId id, CacheFacet facets)
        {
            if (s_Rows.TryGetValue(id, out RowData data))
            {
                data.Invalidate(facets);
            }
        }

        public static void Evict(EntityId id)
        {
            if (s_Rows.Remove(id, out RowData data))
            {
                data.ReleaseReferences();
            }
        }

        public static void InvalidateAll(CacheFacet facets)
        {
            foreach (KeyValuePair<EntityId, RowData> pair in s_Rows)
            {
                pair.Value.Invalidate(facets);
            }
        }

        public static RowData GetOrCreate(EntityId id)
        {
            if (s_Rows.TryGetValue(id, out RowData data))
            {
                return data;
            }

            if (s_Rows.Count >= MaxEntries)
            {
                Clear();
            }

            data = new RowData();
            s_Rows.Add(id, data);
            return data;
        }

        /// <summary>
        /// Brings <paramref name="data"/> up to date for the requested facets. Everything expensive lives
        /// behind this call.
        /// </summary>
        public static void Ensure(RowData data, GameObject gameObject, HierarchyDecoratorSettings settings, CacheFacet facets)
        {
            if (data == null || gameObject == null || settings == null)
            {
                return;
            }

            if (data.SettingsRevision != settings.Revision)
            {
                data.SettingsRevision = settings.Revision;
                data.InvalidateAll();
            }

            CacheFacet missing = facets & ~data.Valid;

            if (missing == CacheFacet.None)
            {
                return;
            }

            if ((missing & CacheFacet.Name) != 0)
            {
                FillName(data, gameObject, settings);
                data.Valid |= CacheFacet.Name;
            }

            if ((missing & CacheFacet.Components) != 0)
            {
                FillComponents(data, gameObject, settings);
                data.Valid |= CacheFacet.Components;
            }
        }

        private static void FillName(RowData data, GameObject gameObject, HierarchyDecoratorSettings settings)
        {
            data.Name = gameObject.name;

            NameMatcher.Match match = NameMatcher.FindRule(data.Name, settings.HeaderRules);

            data.HeaderRuleIndex = match.RuleIndex;
            data.HeaderLabel = match.Label;
        }

        private static void FillComponents(RowData data, GameObject gameObject, HierarchyDecoratorSettings settings)
        {
            data.IconCount = 0;
            data.OverflowCount = 0;
            data.MissingScriptCount = 0;

            ComponentIconSettings options = settings.ComponentIcons;
            EnsureRuleTable(settings);

            int total = gameObject.GetComponentCount();
            data.EnsureIconCapacity(total);

            int limit = options.maxIconsPerRow <= 0 ? int.MaxValue : options.maxIconsPerRow;

            if (options.stackDuplicates)
            {
                s_StackedTypes.Clear();
            }

            for (int i = 0; i < total; i++)
            {
                Component component = gameObject.GetComponentAtIndex(i);

                if (component == null)
                {
                    data.MissingScriptCount++;
                    continue;
                }

                if (!options.enabled)
                {
                    continue;
                }

                Type type = component.GetType();

                if (!ShouldShow(type, options))
                {
                    continue;
                }

                if (options.stackDuplicates)
                {
                    int existing = IndexOfType(data, type);

                    if (existing >= 0)
                    {
                        data.Icons[existing].StackCount++;
                        continue;
                    }
                }

                if (data.IconCount >= limit)
                {
                    data.OverflowCount++;
                    continue;
                }

                data.EnsureIconCapacity(data.IconCount + 1);

                data.Icons[data.IconCount] = new ComponentIconEntry
                {
                    Component = component,
                    Icon = AssetPreview.GetMiniThumbnail(component),
                    DisplayName = ObjectNames.NicifyVariableName(type.Name),
                    StackCount = 1
                };

                data.IconCount++;
            }

            // The null-entry scan above is a free pre-filter; this is the authoritative count and it runs
            // once per cache fill, not per repaint.
            if (data.MissingScriptCount == 0 && settings.Indicators.showMissingScripts)
            {
                data.MissingScriptCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            }

            if (options.order == ComponentIconOrder.Alphabetical && data.IconCount > 1)
            {
                Array.Sort(data.Icons, 0, data.IconCount, IconNameComparer.Instance);
            }

            // Clear the tail so stale Component/Texture references are not retained.
            for (int i = data.IconCount; i < data.Icons.Length; i++)
            {
                data.Icons[i].Component = null;
                data.Icons[i].Icon = null;
            }
        }

        private static int IndexOfType(RowData data, Type type)
        {
            for (int i = 0; i < data.IconCount; i++)
            {
                Component component = data.Icons[i].Component;

                if (component != null && component.GetType() == type)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool ShouldShow(Type type, ComponentIconSettings options)
        {
            if (s_RuleTable.TryGetValue(type, out ComponentDisplay display))
            {
                switch (display)
                {
                    case ComponentDisplay.Show:
                        return true;

                    case ComponentDisplay.Hide:
                        return false;
                }
            }

            if (options.mode == ComponentIconMode.Selected)
            {
                return false;
            }

            if (options.hideTransform && (type == typeof(Transform) || type == typeof(RectTransform)))
            {
                return false;
            }

            if (!options.includeCustomScripts && ComponentCatalog.IsUserScript(type))
            {
                return false;
            }

            return true;
        }

        private static void EnsureRuleTable(HierarchyDecoratorSettings settings)
        {
            if (s_RuleTableRevision == settings.Revision)
            {
                return;
            }

            s_RuleTableRevision = settings.Revision;
            s_RuleTable.Clear();

            List<ComponentRule> rules = settings.ComponentRules;

            for (int i = 0; i < rules.Count; i++)
            {
                ComponentRule rule = rules[i];

                if (rule == null || rule.display == ComponentDisplay.Default)
                {
                    continue;
                }

                Type type = ComponentCatalog.Resolve(rule);

                if (type == null)
                {
                    continue;
                }

                s_RuleTable[type] = rule.display;
            }
        }

        private sealed class IconNameComparer : IComparer<ComponentIconEntry>
        {
            public static readonly IconNameComparer Instance = new IconNameComparer();

            public int Compare(ComponentIconEntry x, ComponentIconEntry y)
            {
                return string.CompareOrdinal(x.DisplayName, y.DisplayName);
            }
        }
    }
}
