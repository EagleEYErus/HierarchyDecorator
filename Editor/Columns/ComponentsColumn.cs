using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// An optional, resizable "Components" column.
    ///
    /// The inline icon strip at the end of the Name column is the default because that is where the icons
    /// stay readable; this column exists for people who want a fixed, sortable-width slot instead. It is
    /// hidden by default and enabled from the Hierarchy header's right-click menu.
    ///
    /// It shares <see cref="DecorationCache"/> with the inline strip, so turning it on costs no extra
    /// component scanning.
    /// </summary>
    internal static class ComponentsColumn
    {
        private const string ColumnId = PackageInfo.Name + ".components";

        [HierarchyViewColumnDescriptor(ColumnId)]
        private static void CreateColumn(HierarchyViewColumnDescriptor descriptor)
        {
            descriptor.Title = "Components";
            descriptor.Tooltip = "Component icons for each GameObject (Hierarchy Decorator).";

            // Positive priority places the column to the right of the Name column.
            descriptor.DefaultPriority = 1;
            descriptor.DefaultWidth = 120;

            // Opt-in: the inline strip already covers the common case, and an unexpected extra column would
            // be an unwelcome surprise on install.
            descriptor.DefaultVisibility = false;
        }

        [HierarchyViewCellDescriptor(ColumnId, typeof(HierarchyGameObjectHandler))]
        private static void CreateCell(HierarchyViewCellDescriptor descriptor)
        {
            // Build the row of icon elements once and reuse it; the alternative wipes the cell's children on
            // every unbind.
            descriptor.ClearCellContent = false;
            descriptor.BindCell = BindCell;
            descriptor.UnbindCell = UnbindCell;
        }

        private static void BindCell(HierarchyViewCell cell)
        {
            // Cells are hidden by default unless marked non-default, hovered, or on a selected row.
            cell.IsDefaultValue = false;

            VisualElement strip = GetStrip(cell);

            if (!HierarchyDecoratorUserSettings.instance.Enabled || DecoratorHost.Suspended)
            {
                HideAll(strip, 0);
                return;
            }

            if (!HierarchyNodes.TryGetGameObject(cell, out GameObject gameObject))
            {
                HideAll(strip, 0);
                return;
            }

            HierarchyDecoratorSettings settings = HierarchyDecoratorSettings.instance;
            EntityId id = gameObject.GetEntityId();

            RowData data = DecorationCache.GetOrCreate(id);
            DecorationCache.Ensure(data, gameObject, settings, CacheFacet.Components);

            int count = data.IconCount;
            EnsureIcons(strip, count);

            for (int i = 0; i < count; i++)
            {
                VisualElement icon = strip[i];
                ComponentIconEntry entry = data.Icons[i];

                icon.style.display = DisplayStyle.Flex;
                icon.style.backgroundImage = entry.Icon != null ? new StyleBackground(entry.Icon) : StyleKeyword.Null;
                icon.tooltip = settings.ComponentIcons.showTooltips ? entry.DisplayName : null;

                int enabled = settings.ComponentIcons.fadeDisabledComponents && entry.Component != null
                    ? EditorUtility.GetObjectEnabled(entry.Component)
                    : 1;

                icon.style.opacity = enabled == 0 ? 0.4f : 1f;
            }

            HideAll(strip, count);
        }

        private static void UnbindCell(HierarchyViewCell cell)
        {
            // The cell keeps its children (ClearCellContent is false), so stale icons are hidden instead.
            VisualElement strip = RowElements.Find<VisualElement>(cell, "hd-column-icons");
            HideAll(strip, 0);
        }

        private static VisualElement GetStrip(HierarchyViewCell cell)
        {
            VisualElement strip = RowElements.Find<VisualElement>(cell, "hd-column-icons");

            if (strip != null)
            {
                return strip;
            }

            strip = IconElements.CreateStrip("hd-column-icons");
            cell.Add(strip);
            return strip;
        }

        private static void EnsureIcons(VisualElement strip, int count)
        {
            for (int i = strip.childCount; i < count; i++)
            {
                strip.Add(IconElements.CreateIcon("hd-column-icon-" + i, interactive: false));
            }
        }

        private static void HideAll(VisualElement strip, int from)
        {
            if (strip == null)
            {
                return;
            }

            for (int i = from; i < strip.childCount; i++)
            {
                strip[i].style.display = DisplayStyle.None;
            }
        }
    }
}
