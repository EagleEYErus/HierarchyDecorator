using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEngine;

namespace HierarchyDecorator
{
    /// <summary>
    /// Safe conversions between the Unity 6.6 hierarchy model and GameObjects.
    ///
    /// Not every row is a GameObject: scenes, sub-scenes, entity worlds and UI Toolkit elements are all
    /// separate node types, and calling the wrong handler returns null silently rather than throwing.
    /// Every decorator therefore goes through these helpers instead of casting.
    /// </summary>
    internal static class HierarchyNodes
    {
        public static bool IsGameObject(HierarchyViewItem item)
        {
            return item != null && item.Handler is HierarchyGameObjectHandler;
        }

        public static bool TryGetGameObject(HierarchyViewItem item, out GameObject gameObject)
        {
            gameObject = null;

            if (item?.Handler is not HierarchyGameObjectHandler handler)
            {
                return false;
            }

            gameObject = handler.GetGameObject(item.Node);
            return gameObject != null;
        }

        public static bool TryGetGameObject(HierarchyViewCell cell, out GameObject gameObject)
        {
            gameObject = null;

            if (cell?.Handler is not HierarchyGameObjectHandler handler)
            {
                return false;
            }

            HierarchyNode node = cell.Node;

            if (node == HierarchyNode.Null)
            {
                return false;
            }

            gameObject = handler.GetGameObject(node);
            return gameObject != null;
        }

        public static bool TryGetEntityId(HierarchyView view, HierarchyNode node, out EntityId entityId)
        {
            entityId = default;

            if (view == null || node == HierarchyNode.Null)
            {
                return false;
            }

            entityId = view.Source.GetEntityIdFromNode(node);
            return true;
        }

        /// <summary>
        /// Depth of a node as the view lays it out. Scene/root children are depth 0; the hierarchy Root
        /// itself is -1.
        /// </summary>
        public static int GetDepth(HierarchyViewModel viewModel, HierarchyNode node)
        {
            return viewModel.GetDepth(node);
        }

        /// <summary>
        /// True when no later sibling of <paramref name="node"/> is present in the visible list.
        ///
        /// The view model's topology getters answer about the *source* tree, so <c>GetNextSibling</c> can
        /// return a node that is hidden or filtered out; those have to be skipped or the tree line would be
        /// drawn past the last visible row.
        /// </summary>
        public static bool IsLastVisibleChild(HierarchyViewModel viewModel, HierarchyNode node)
        {
            HierarchyNode next = viewModel.GetNextSibling(node);

            while (next != HierarchyNode.Null && !viewModel.Contains(next))
            {
                next = viewModel.GetNextSibling(next);
            }

            return next == HierarchyNode.Null;
        }

        /// <summary>
        /// Fills <paramref name="continuations"/> for the guide-line columns of a row.
        ///
        /// A row at depth <c>D</c> has <c>D</c> indent columns. The vertical line in column <c>i</c> descends
        /// from the ancestor at depth <c>i</c> towards its children, and continues past this row exactly when
        /// the ancestor at depth <c>i + 1</c> (the next node on our own path, with the node itself as the
        /// last one) still has a visible sibling below it.
        ///
        /// Returns the depth of <paramref name="node"/>.
        /// </summary>
        public static int CollectAncestorContinuations(HierarchyViewModel viewModel, HierarchyNode node, bool[] continuations)
        {
            int depth = viewModel.GetDepth(node);

            if (depth <= 0 || continuations == null)
            {
                return depth;
            }

            int columns = Mathf.Min(depth, continuations.Length);
            HierarchyNode current = node;

            // Column D-1 belongs to the node itself; each step up moves one column left.
            for (int column = columns - 1; column >= 0; column--)
            {
                if (current == HierarchyNode.Null)
                {
                    continuations[column] = false;
                    continue;
                }

                continuations[column] = !IsLastVisibleChild(viewModel, current);
                current = viewModel.GetParent(current);
            }

            return depth;
        }

        public static bool HasVisibleChildren(HierarchyViewModel viewModel, HierarchyNode node)
        {
            return viewModel.HasVisibleChildren(node);
        }

        public static bool IsExpanded(HierarchyViewModel viewModel, HierarchyNode node)
        {
            return viewModel.HasFlags(node, HierarchyNodeFlags.Expanded);
        }

        public static int GetRowIndex(HierarchyViewModel viewModel, HierarchyNode node)
        {
            return viewModel.IndexOf(node);
        }
    }
}
