using System.Collections.Generic;
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
    /// stay readable; this column exists for people who want a fixed-width slot instead. It is hidden by
    /// default and enabled from the Hierarchy header's right-click menu.
    ///
    /// It shares <see cref="DecorationCache"/> with the inline strip, so turning it on costs no extra
    /// component scanning.
    ///
    /// <para>
    /// Bound cells are tracked here for the same reason rows are tracked in <see cref="DecoratorHost"/>:
    /// nothing rebinds a cell when a component is added or a setting changes, so without this registry the
    /// column silently drifts out of sync with the inline strip and shows stale icons until the row is
    /// scrolled out of view and back.
    /// </para>
    /// </summary>
    internal static class ComponentsColumn
    {
        private const string ColumnId = PackageInfo.Name + ".components";
        private const string StripName = "hd-column-icons";
        private const string OverflowName = "hd-column-overflow";

        private const int IconsIndex = 0;
        private const int OverflowIndex = 1;

        private static readonly Dictionary<HierarchyViewCell, EntityId> s_BoundCells =
            new Dictionary<HierarchyViewCell, EntityId>();

        private static readonly List<HierarchyViewCell> s_Scratch = new List<HierarchyViewCell>(64);

        private const string HeaderTooltip = "Component icons for each GameObject (Hierarchy Decorator).";

        [HierarchyViewColumnDescriptor(ColumnId)]
        private static void CreateColumn(HierarchyViewColumnDescriptor descriptor)
        {
            descriptor.Title = "Components";

            // No Icon. A column header is not a button - the header is inert and our columns are not
            // sortable - and the Inspector icon this used to carry renders as a circled "i", so people
            // hovered and clicked it expecting an explanation and got nothing at all.
            //
            // Tooltip is set because it is the documented way to explain a column, but be aware that Unity
            // 6000.6.0f1 does not show it: measured on a live window, the string reaches no element of the
            // header, and neither MakeHeader nor BindHeader is ever invoked (MakeHeader is worse than
            // useless - assigning it makes the column vanish from the window entirely).
            descriptor.Tooltip = HeaderTooltip;

            // Positive priority places the column to the right of the Name column.
            descriptor.DefaultPriority = 1;
            descriptor.DefaultWidth = 90;

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

        internal static void RefreshBoundCells()
        {
            RefreshBoundCells(null);
        }

        internal static void RefreshBoundCells(HashSet<EntityId> ids)
        {
            if (s_BoundCells.Count == 0)
            {
                return;
            }

            s_Scratch.Clear();

            foreach (KeyValuePair<HierarchyViewCell, EntityId> pair in s_BoundCells)
            {
                if (ids == null || ids.Contains(pair.Value))
                {
                    s_Scratch.Add(pair.Key);
                }
            }

            for (int i = 0; i < s_Scratch.Count; i++)
            {
                HierarchyViewCell cell = s_Scratch[i];

                if (cell.panel == null)
                {
                    // The cell left the panel without an unbind (window closed); drop it.
                    s_BoundCells.Remove(cell);
                    continue;
                }

                Populate(cell);
            }

            s_Scratch.Clear();
        }

        internal static void ResetBoundCells()
        {
            s_BoundCells.Clear();
            s_Scratch.Clear();
        }

        private static void BindCell(HierarchyViewCell cell)
        {
            // Cells are hidden by default unless marked non-default, hovered, or on a selected row.
            cell.IsDefaultValue = false;

            Populate(cell);
        }

        private static void UnbindCell(HierarchyViewCell cell)
        {
            s_BoundCells.Remove(cell);

            // The cell keeps its children (ClearCellContent is false), so stale icons are hidden instead.
            VisualElement strip = RowElements.Find<VisualElement>(cell, StripName);

            if (strip != null)
            {
                HideFrom(strip[IconsIndex], 0);
                strip[OverflowIndex].style.display = DisplayStyle.None;
            }

            // Hand the cell back in its default state. A cell left non-default stays visible when it is
            // rebound to a row this column has no cell descriptor for - a scene row, say - and then shows
            // the previous occupant's content.
            cell.IsDefaultValue = true;
        }

        private static void Populate(HierarchyViewCell cell)
        {
            VisualElement strip = GetStrip(cell);
            VisualElement icons = strip[IconsIndex];
            Label overflow = (Label)strip[OverflowIndex];

            if (!HierarchyDecoratorUserSettings.instance.Enabled || DecoratorHost.Suspended)
            {
                HideFrom(icons, 0);
                overflow.style.display = DisplayStyle.None;
                return;
            }

            if (!HierarchyNodes.TryGetGameObject(cell, out GameObject gameObject))
            {
                s_BoundCells.Remove(cell);
                HideFrom(icons, 0);
                overflow.style.display = DisplayStyle.None;
                return;
            }

            HierarchyDecoratorSettings settings = HierarchyDecoratorSettings.instance;

            // Resolved through the view, exactly as DecoratorHost does, so the column and the inline strip
            // are guaranteed to share one cache entry rather than building two.
            EntityId id = cell.View.Source.GetEntityIdFromNode(cell.Node);
            s_BoundCells[cell] = id;

            RowData data = DecorationCache.GetOrCreate(id);

            // The Name facet is needed too: a header rule can suppress component icons, and the column has to
            // agree with the inline strip on the same row.
            DecorationCache.Ensure(data, gameObject, settings, CacheFacet.Name | CacheFacet.Components);

            HeaderRule header = data.HasHeader && data.HeaderRuleIndex < settings.HeaderRules.Count
                ? settings.HeaderRules[data.HeaderRuleIndex]
                : null;

            if (header != null && !header.showComponentIcons)
            {
                HideFrom(icons, 0);
                overflow.style.display = DisplayStyle.None;
                return;
            }

            int count = data.IconCount;
            EnsureIcons(icons, count);

            for (int i = 0; i < count; i++)
            {
                ComponentIconEntry entry = data.Icons[i];
                VisualElement icon = icons[i];

                if (entry.Component == null)
                {
                    icon.style.display = DisplayStyle.None;
                    continue;
                }

                icon.style.display = DisplayStyle.Flex;
                icon.style.backgroundImage = entry.Icon != null ? new StyleBackground(entry.Icon) : StyleKeyword.Null;
                icon.tooltip = settings.ComponentIcons.showTooltips ? entry.DisplayName : null;
                icon.userData = entry.Component;

                int enabled = settings.ComponentIcons.fadeDisabledComponents
                    ? EditorUtility.GetObjectEnabled(entry.Component)
                    : 1;

                icon.style.opacity = enabled == 0 ? 0.4f : 1f;
            }

            HideFrom(icons, count);

            int hidden = data.OverflowCount;

            IconElements.SetOverflow(overflow, settings.ComponentIcons.showOverflowIndicator ? hidden : 0);
        }

        private static VisualElement GetStrip(HierarchyViewCell cell)
        {
            VisualElement strip = RowElements.Find<VisualElement>(cell, StripName);

            if (strip != null)
            {
                return strip;
            }

            strip = IconElements.CreateStrip(StripName);
            strip.Add(IconElements.CreateStrip("hd-column-icon-list"));

            Label overflow = new Label
            {
                name = OverflowName,
                pickingMode = PickingMode.Ignore
            };

            overflow.AddToClassList("hd-icon-overflow");
            overflow.style.display = DisplayStyle.None;
            strip.Add(overflow);

            cell.Add(strip);
            return strip;
        }

        private static void EnsureIcons(VisualElement icons, int count)
        {
            for (int i = icons.childCount; i < count; i++)
            {
                VisualElement icon = IconElements.CreateIcon("hd-column-icon-" + i, interactive: true);

                // Same handler as the inline strip: a click on an icon must mean the same thing wherever the
                // icon is drawn.
                icon.RegisterCallback<ClickEvent>(ComponentIconDecorator.OnIconClicked);
                icons.Add(icon);
            }
        }

        private static void HideFrom(VisualElement icons, int from)
        {
            for (int i = from; i < icons.childCount; i++)
            {
                icons[i].style.display = DisplayStyle.None;
            }
        }
    }
}
