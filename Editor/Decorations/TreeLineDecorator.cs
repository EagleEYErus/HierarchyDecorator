using UnityEngine;
using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// Tree guide lines - the visual signature of HierarchyDecorator.
    ///
    /// Geometry comes from Unity's own row layout, read out of the shipped implementation rather than
    /// guessed: the 4 px prefab override bar sits before the indent, and the indent itself is a
    /// <c>translate</c> of <c>14 * depth</c> applied to the left container. A guide column therefore sits at
    /// <c>4 + 14 * column + 7</c>.
    ///
    /// Lines are plain <see cref="VisualElement"/>s reused across binds and addressed by child index, so a
    /// rebind costs a handful of style writes and no lookups. Dashed and dotted styles use a repeating
    /// 1-pixel texture generated once per domain, so nothing is allocated or rasterised while scrolling.
    /// </summary>
    internal sealed class TreeLineDecorator : IRowDecorator
    {
        private const string ContainerName = "hd-tree-lines";

        /// <summary>Unity's HierarchyViewItem indent step.</summary>
        private const float IndentWidth = 14f;

        /// <summary>Width of the prefab override bar, which is laid out before the indent.</summary>
        private const float OverrideBarWidth = 4f;

        /// <summary>Centre of the indent cell, matching where Unity places the expand toggle.</summary>
        private const float ColumnCentre = 7f;

        private const int MaxDepth = 32;

        /// <summary>Child 0 of the container is always the horizontal connector; columns follow in order.</summary>
        private const int ConnectorIndex = 0;
        private const int FirstColumnIndex = 1;

        private readonly bool[] m_Continuations = new bool[MaxDepth];

        public string Id => "tree-lines";

        public CacheFacet RequiredFacets => CacheFacet.Name;

        public void Apply(in RowContext context)
        {
            // The container is added to the item itself rather than to .hierarchy-item__container: the item
            // shares the same origin, is not clipped by overflow:hidden, and absolutely positioned children
            // never disturb the row's flex layout.
            VisualElement host = context.Item;
            VisualElement container = RowElements.Find<VisualElement>(host, ContainerName);

            TreeLineSettings settings = context.Settings.TreeLines;

            // Indentation is forced to zero while a search filter is active and the list becomes flat, so
            // guide lines would point at nothing.
            bool active = context.Active
                          && context.IsGameObject
                          && settings.enabled
                          && !context.IsFiltering
                          && AllowedByHeaderRule(context);

            if (!active)
            {
                RowElements.SetVisible(container, false);
                return;
            }

            int depth = HierarchyNodes.CollectAncestorContinuations(context.View.ViewModel, context.Node, m_Continuations);

            if (depth <= 0)
            {
                RowElements.SetVisible(container, false);
                return;
            }

            int columns = Mathf.Min(depth, MaxDepth);

            container ??= CreateContainer(host);
            EnsureColumnElements(container, columns);

            Color color = settings.color.Resolve(context.IsDarkSkin);
            color.a *= settings.opacity;

            container.style.display = DisplayStyle.Flex;
            container.style.width = OverrideBarWidth + IndentWidth * columns;

            int firstColumn = settings.fullDepth ? 0 : columns - 1;
            int existingColumns = container.childCount - FirstColumnIndex;

            for (int column = 0; column < existingColumns; column++)
            {
                VisualElement line = container[FirstColumnIndex + column];

                if (column < firstColumn || column >= columns)
                {
                    line.style.display = DisplayStyle.None;
                    continue;
                }

                bool isBranchColumn = column == columns - 1;
                bool continues = m_Continuations[column];

                line.style.display = DisplayStyle.Flex;
                line.style.left = OverrideBarWidth + IndentWidth * column + ColumnCentre;
                line.style.width = 1f;
                line.style.top = 0f;

                // The branch column stops halfway when this row is the last visible child, which is what
                // turns the line into an "L" instead of a "T".
                if (isBranchColumn && !continues)
                {
                    line.style.bottom = StyleKeyword.Null;
                    line.style.height = Length.Percent(50f);
                }
                else
                {
                    line.style.height = StyleKeyword.Null;
                    line.style.bottom = 0f;
                }

                LineStyler.Apply(line, settings.style, color, vertical: true);
            }

            VisualElement connector = container[ConnectorIndex];

            if (settings.showConnector)
            {
                connector.style.display = DisplayStyle.Flex;
                connector.style.left = OverrideBarWidth + IndentWidth * (columns - 1) + ColumnCentre;
                connector.style.width = ColumnCentre;
                connector.style.top = Length.Percent(50f);
                connector.style.bottom = StyleKeyword.Null;
                connector.style.height = 1f;

                LineStyler.Apply(connector, settings.style, color, vertical: false);
            }
            else
            {
                connector.style.display = DisplayStyle.None;
            }
        }

        /// <summary>A header row is a section marker; drawing tree lines through it usually reads as noise.</summary>
        private static bool AllowedByHeaderRule(in RowContext context)
        {
            HeaderRule rule = context.Header;
            return rule == null || rule.showTreeLines;
        }

        private static VisualElement CreateContainer(VisualElement host)
        {
            VisualElement container = new VisualElement
            {
                name = ContainerName,
                pickingMode = PickingMode.Ignore
            };

            container.AddToClassList("hd-tree-lines");
            container.style.position = Position.Absolute;
            container.style.left = 0f;
            container.style.top = 0f;
            container.style.bottom = 0f;

            container.Add(CreateLine("hd-tree-connector"));

            host.Add(container);
            return container;
        }

        private static void EnsureColumnElements(VisualElement container, int columns)
        {
            int existing = container.childCount - FirstColumnIndex;

            for (int i = existing; i < columns; i++)
            {
                container.Add(CreateLine("hd-tree-line-" + i));
            }
        }

        private static VisualElement CreateLine(string name)
        {
            VisualElement line = new VisualElement
            {
                name = name,
                pickingMode = PickingMode.Ignore
            };

            line.style.position = Position.Absolute;
            return line;
        }
    }

    internal static class LineStyler
    {
        public static void Apply(VisualElement element, LineStyle style, Color color, bool vertical)
        {
            if (style == LineStyle.Solid)
            {
                element.style.backgroundImage = StyleKeyword.Null;
                element.style.unityBackgroundImageTintColor = StyleKeyword.Null;
                element.style.backgroundRepeat = StyleKeyword.Null;
                element.style.backgroundSize = StyleKeyword.Null;
                element.style.backgroundColor = color;
                return;
            }

            Texture2D texture = vertical ? LineTextures.GetVertical(style) : LineTextures.GetHorizontal(style);

            if (texture == null)
            {
                element.style.backgroundColor = color;
                return;
            }

            element.style.backgroundColor = Color.clear;
            element.style.backgroundImage = new StyleBackground(texture);
            element.style.unityBackgroundImageTintColor = color;
            element.style.backgroundRepeat = new StyleBackgroundRepeat(
                vertical
                    ? new BackgroundRepeat(Repeat.NoRepeat, Repeat.Repeat)
                    : new BackgroundRepeat(Repeat.Repeat, Repeat.NoRepeat));
            element.style.backgroundSize = new StyleBackgroundSize(
                new BackgroundSize(texture.width, texture.height));
        }
    }
}
