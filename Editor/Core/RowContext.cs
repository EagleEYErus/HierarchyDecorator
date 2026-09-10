using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEngine;

namespace HierarchyDecorator
{
    /// <summary>
    /// Everything a decorator is allowed to see about one hierarchy row. Passed by <c>in</c> so no allocation
    /// happens on the bind path.
    /// </summary>
    internal readonly struct RowContext
    {
        public readonly HierarchyWindow Window;
        public readonly HierarchyView View;
        public readonly HierarchyViewItem Item;
        public readonly HierarchyNode Node;
        public readonly GameObject GameObject;
        public readonly EntityId Id;
        public readonly RowData Data;
        public readonly HierarchyDecoratorSettings Settings;
        public readonly bool IsDarkSkin;

        /// <summary>False for scene, sub-scene, entity and third-party rows.</summary>
        public readonly bool IsGameObject;

        /// <summary>True while a search filter is active: the list is flat and the indent is forced to zero.</summary>
        public readonly bool IsFiltering;

        /// <summary>
        /// Master switch. When false every decorator must hide its own elements - the row may have been
        /// decorated before the user turned the plugin off, and recycled rows keep what was put on them.
        /// </summary>
        public readonly bool Active;

        public RowContext(
            HierarchyWindow window,
            HierarchyView view,
            HierarchyViewItem item,
            HierarchyNode node,
            GameObject gameObject,
            EntityId id,
            RowData data,
            HierarchyDecoratorSettings settings,
            bool isDarkSkin,
            bool isFiltering,
            bool active)
        {
            Active = active;
            Window = window;
            View = view;
            Item = item;
            Node = node;
            GameObject = gameObject;
            Id = id;
            Data = data;
            Settings = settings;
            IsDarkSkin = isDarkSkin;
            IsGameObject = gameObject != null;
            IsFiltering = isFiltering;
        }

        /// <summary>The header rule matched by this row's name, or null.</summary>
        public HeaderRule Header
        {
            get
            {
                if (Data == null || !Data.HasHeader)
                {
                    return null;
                }

                int index = Data.HeaderRuleIndex;
                return index >= 0 && index < Settings.HeaderRules.Count ? Settings.HeaderRules[index] : null;
            }
        }
    }

    /// <summary>
    /// A single visual concern applied to a row.
    ///
    /// Contract, imposed by Unity's row recycling:
    /// * <see cref="Apply"/> is called for every bound row and must fully re-derive its visuals, including
    ///   hiding them when the feature is off or the row is not applicable. It must never blindly
    ///   <c>Add()</c> children - rows are pooled and children survive rebinding.
    /// * There is no matching "undo" call. Unity explicitly recommends against reverting styles on unbind.
    /// </summary>
    internal interface IRowDecorator
    {
        /// <summary>Stable id used for error isolation and the emergency disable list.</summary>
        string Id { get; }

        void Apply(in RowContext context);
    }
}
