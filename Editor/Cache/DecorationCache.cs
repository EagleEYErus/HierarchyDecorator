using System;
using System.Collections.Generic;
using Unity.Profiling;
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

        private static int s_RuleTableRevision = -1;

        /// <summary>
        /// Icon and display name depend only on the component's Type, but resolving them is a native round
        /// trip each (and NicifyVariableName marshals a fresh string). Without this, filling 60 rows at five
        /// components apiece cost 600 native calls and 300 throwaway strings for a few dozen distinct types.
        /// </summary>
        private static readonly Dictionary<Type, ComponentTypeInfo> s_TypeInfo = new Dictionary<Type, ComponentTypeInfo>(64);

        private struct ComponentTypeInfo
        {
            public Texture2D Icon;
            public string DisplayName;
        }

        /// <summary>Discards the per-type icon table. Icons are assets and can be reimported or destroyed.</summary>
        public static void InvalidateTypeInfo()
        {
            s_TypeInfo.Clear();
        }

        // The two markers that matter: everything else on the bind path is style writes.
        private static readonly ProfilerMarker s_ComponentScanMarker = new ProfilerMarker("HierarchyDecorator.ScanComponents");
        private static readonly ProfilerMarker s_NameMatchMarker = new ProfilerMarker("HierarchyDecorator.MatchHeaderRule");

        public static int Count => s_Rows.Count;

        public static void Clear()
        {
            // The dictionary is the only thing holding a RowData - RowContext is a stack-local readonly
            // struct and the column keeps none - so dropping it already makes every entry unreachable.
            // Walking them to null out references first protected nothing and cost O(cache).
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
            s_Rows.Remove(id);
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
                // No eviction policy: a scene this large is outside what the cache was sized for, and a
                // silent refill loop would look like a performance mystery rather than a known limit.
                HierarchyLog.Once(
                    "cache-ceiling",
                    $"The decoration cache reached its {MaxEntries} entry ceiling and was cleared. " +
                    "Decorations still work, but rows will be recomputed more often in scenes this large.");

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

            bool isProSkin = EditorGUIUtility.isProSkin;

            if (data.SettingsRevision != settings.Revision || data.WasProSkin != isProSkin)
            {
                data.SettingsRevision = settings.Revision;
                data.WasProSkin = isProSkin;
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
            using ProfilerMarker.AutoScope scope = s_NameMatchMarker.Auto();

            data.Name = gameObject.name;

            NameMatcher.Match match = NameMatcher.FindRule(data.Name, settings.HeaderRules);

            data.HeaderRuleIndex = match.RuleIndex;
            data.HeaderLabel = match.Label;
        }

        private static void FillComponents(RowData data, GameObject gameObject, HierarchyDecoratorSettings settings)
        {
            using ProfilerMarker.AutoScope scope = s_ComponentScanMarker.Auto();

            data.IconCount = 0;
            data.OverflowCount = 0;
            data.MissingScriptCount = 0;

            ComponentIconSettings options = settings.ComponentIcons;
            EnsureRuleTable(settings);

            int total = gameObject.GetComponentCount();
            data.EnsureIconCapacity(total);

            int limit = options.maxIconsPerRow <= 0 ? int.MaxValue : options.maxIconsPerRow;

            for (int i = 0; i < total; i++)
            {
                Component component = gameObject.GetComponentAtIndex(i);

                if (component == null)
                {
                    data.MissingScriptCount++;
                    continue;
                }

                // Note: the icon slice is built whenever this facet is requested, not only when the inline
                // strip is on. The Components column is a second consumer, and gating the data on the strip's
                // own toggle left that column permanently empty.
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

                ComponentTypeInfo info = GetTypeInfo(type, component);

                data.Icons[data.IconCount] = new ComponentIconEntry
                {
                    Component = component,
                    Icon = info.Icon,
                    DisplayName = info.DisplayName,
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

        }

        private static ComponentTypeInfo GetTypeInfo(Type type, Component instance)
        {
            // The texture is a Unity object and can be destroyed by a reimport, so a cached null is re-resolved
            // rather than trusted.
            if (s_TypeInfo.TryGetValue(type, out ComponentTypeInfo info) && info.Icon != null)
            {
                return info;
            }

            info = new ComponentTypeInfo
            {
                Icon = AssetPreview.GetMiniThumbnail(instance),
                DisplayName = ObjectNames.NicifyVariableName(type.Name)
            };

            s_TypeInfo[type] = info;
            return info;
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
