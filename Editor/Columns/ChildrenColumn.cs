using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// An optional "Children" column showing how many descendants a row has.
    ///
    /// It exists because nothing native answers "how big is this subtree", which is the first question when
    /// auditing an unfamiliar scene, and because it is genuinely free:
    /// <c>HierarchyViewModel.GetChildrenCountRecursive</c> is a single native call that answers about the
    /// source tree, so it is correct for collapsed subtrees and needs no cache entry, no component scan and
    /// no managed traversal.
    ///
    /// Rows with no children are left as the default value, which is what keeps the column quiet: Unity hides
    /// a default cell unless the row is hovered or selected.
    /// </summary>
    internal static class ChildrenColumn
    {
        private const string ColumnId = PackageInfo.Name + ".children";
        private const string LabelName = "hd-children-count";

        [HierarchyViewColumnDescriptor(ColumnId)]
        private static void CreateColumn(HierarchyViewColumnDescriptor descriptor)
        {
            descriptor.Title = "Children";
            descriptor.Tooltip = "Number of descendants, including collapsed ones (Hierarchy Decorator).";
            descriptor.DefaultPriority = 2;
            descriptor.DefaultWidth = 64;
            descriptor.DefaultVisibility = false;
        }

        [HierarchyViewCellDescriptor(ColumnId, typeof(HierarchyGameObjectHandler))]
        private static void CreateCell(HierarchyViewCellDescriptor descriptor)
        {
            descriptor.ClearCellContent = false;
            descriptor.BindCell = BindCell;
        }

        private static void BindCell(HierarchyViewCell cell)
        {
            Label label = RowElements.Find<Label>(cell, LabelName);

            if (label == null)
            {
                label = new Label
                {
                    name = LabelName,
                    pickingMode = PickingMode.Ignore
                };

                label.AddToClassList("hd-children-count");
                cell.Add(label);
            }

            HierarchyNode node = cell.Node;

            if (node == HierarchyNode.Null || !HierarchyDecoratorUserSettings.instance.Enabled || DecoratorHost.Suspended)
            {
                label.text = string.Empty;
                cell.IsDefaultValue = true;
                return;
            }

            int count = cell.View.ViewModel.GetChildrenCountRecursive(node);

            label.text = count > 0 ? count.ToString() : string.Empty;

            // A leaf is the default value, so Unity keeps its cell hidden until the row is hovered.
            cell.IsDefaultValue = count == 0;
        }
    }
}
