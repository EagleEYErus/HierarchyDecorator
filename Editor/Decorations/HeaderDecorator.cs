using UnityEngine;
using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// Headers and separators - rows whose name matches a <see cref="HeaderRule"/> become scene section
    /// markers.
    ///
    /// Unlike 1.x this never repaints the row. The row's own background colour is tinted, the existing name
    /// Label is restyled, and a separator line is a background image on the shared row container. Unity keeps
    /// drawing the foldout, selection, hover, prefab override bar and prefab text colours, which is what fixes
    /// the "two tone background hides the override bar" and "SubScene caret hidden" classes of bug outright.
    ///
    /// It runs after <see cref="RowTintDecorator"/> on purpose: that decorator always writes the row
    /// background (a colour or a reset), so a header only has to overwrite it and never has to clean up.
    /// </summary>
    internal sealed class HeaderDecorator : IRowDecorator
    {
        public string Id => "headers";

        public void Apply(in RowContext context)
        {
            Label label = context.Item.Name;

            if (label == null)
            {
                return;
            }

            HeaderRule rule = context.Active && context.IsGameObject ? context.Header : null;

            if (rule == null)
            {
                Reset(context);
                return;
            }

            bool isSeparator = rule.kind == HeaderKind.Separator;
            Color textColor = rule.textColor.Resolve(context.IsDarkSkin);

            VisualElement row = context.Item.RowContainer;

            if (row != null)
            {
                bool hasLabel = !string.IsNullOrEmpty(context.Data.HeaderLabel);

                if (isSeparator)
                {
                    ApplySeparatorLine(row, rule, context.IsDarkSkin, hasLabel);
                }
                else
                {
                    ClearSeparatorLine(row);
                    row.style.backgroundColor = rule.backgroundColor.Resolve(context.IsDarkSkin);

                    if (rule.showLine)
                    {
                        ApplySeparatorLine(row, rule, context.IsDarkSkin, hasLabel);
                    }
                }
            }

            // Text
            label.text = string.IsNullOrEmpty(context.Data.HeaderLabel) ? string.Empty : context.Data.HeaderLabel;
            label.style.color = textColor;
            label.style.fontSize = rule.fontSize;
            label.style.unityFontStyleAndWeight = rule.bold ? FontStyle.Bold : FontStyle.Normal;
            label.style.letterSpacing = rule.letterSpacing;
            label.style.unityTextAlign = ToTextAnchor(rule.alignment);

            // Stretch the name so a centred or right-aligned header spans the row rather than just its own
            // text box. The right container normally takes the slack, so it is neutralised here.
            VisualElement nameElement = label.parent;
            VisualElement leftContainer = nameElement?.parent;

            SetStretch(label, true);
            SetStretch(nameElement, true);
            SetStretch(leftContainer, true);

            if (context.Item.RightCustomContainer != null)
            {
                // The right container normally absorbs the slack, which would leave a centred header
                // centred over half the row. Giving the slack to the label instead centres it over the whole
                // row, and the icon strip still pins itself to the right because it keeps its content size.
                context.Item.RightCustomContainer.style.flexGrow = 0f;
            }

            // A centred marker reads better without the GameObject icon; a left-aligned one keeps it so the
            // row still looks like the object it is.
            bool hideIcon = !isSeparator && rule.alignment != HeaderAlignment.Left;
            SetIconVisible(context.Item, !hideIcon);
        }

        private static void Reset(in RowContext context)
        {
            Label label = context.Item.Name;

            if (label == null)
            {
                return;
            }

            if (context.IsGameObject && context.GameObject != null && label.text != context.GameObject.name)
            {
                // A recycled row can still be showing a stripped header label.
                label.text = context.GameObject.name;
            }

            label.style.color = StyleKeyword.Null;
            label.style.fontSize = StyleKeyword.Null;
            label.style.unityFontStyleAndWeight = StyleKeyword.Null;
            label.style.letterSpacing = StyleKeyword.Null;
            label.style.unityTextAlign = StyleKeyword.Null;

            VisualElement nameElement = label.parent;
            VisualElement leftContainer = nameElement?.parent;

            SetStretch(label, false);
            SetStretch(nameElement, false);
            SetStretch(leftContainer, false);

            if (context.Item.RightCustomContainer != null)
            {
                context.Item.RightCustomContainer.style.flexGrow = StyleKeyword.Null;
            }

            SetIconVisible(context.Item, true);

            VisualElement row = context.Item.RowContainer;

            if (row != null)
            {
                ClearSeparatorLine(row);
            }
        }

        private static void SetStretch(VisualElement element, bool stretch)
        {
            if (element == null)
            {
                return;
            }

            element.style.flexGrow = stretch ? 1f : StyleKeyword.Null;
        }

        private static void SetIconVisible(Unity.Hierarchy.HierarchyViewItem item, bool visible)
        {
            if (item.Icon != null)
            {
                item.Icon.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (item.OverlayIcon != null && !visible)
            {
                item.OverlayIcon.style.display = DisplayStyle.None;
            }
            else if (item.OverlayIcon != null)
            {
                item.OverlayIcon.style.display = StyleKeyword.Null;
            }
        }

        /// <summary>
        /// The line is a repeating background image on the row container rather than a child element, so no
        /// element is ever inserted into a container Unity owns and the line spans the full row width.
        ///
        /// It sits at the row's vertical centre when there is no label - the classic divider - and at the
        /// bottom edge when there is one, because a centred line would run through the text and read as a
        /// strikethrough.
        /// </summary>
        private static void ApplySeparatorLine(VisualElement row, HeaderRule rule, bool isDarkSkin, bool hasLabel)
        {
            Texture2D texture = LineTextures.GetStrip(rule.lineStyle, vertical: false);

            row.style.backgroundColor = rule.kind == HeaderKind.Separator
                ? new Color(0f, 0f, 0f, 0f)
                : rule.backgroundColor.Resolve(isDarkSkin);

            row.style.backgroundImage = new StyleBackground(texture);
            row.style.unityBackgroundImageTintColor = rule.textColor.Resolve(isDarkSkin);
            row.style.backgroundRepeat = new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.Repeat, Repeat.NoRepeat));
            row.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(texture.width, rule.lineThickness));
            row.style.backgroundPositionY = new StyleBackgroundPosition(
                new BackgroundPosition(hasLabel ? BackgroundPositionKeyword.Bottom : BackgroundPositionKeyword.Center));
        }

        private static void ClearSeparatorLine(VisualElement row)
        {
            row.style.backgroundImage = StyleKeyword.Null;
            row.style.unityBackgroundImageTintColor = StyleKeyword.Null;
            row.style.backgroundRepeat = StyleKeyword.Null;
            row.style.backgroundSize = StyleKeyword.Null;
            row.style.backgroundPositionY = StyleKeyword.Null;
        }

        private static TextAnchor ToTextAnchor(HeaderAlignment alignment)
        {
            switch (alignment)
            {
                case HeaderAlignment.Center:
                    return TextAnchor.MiddleCenter;

                case HeaderAlignment.Right:
                    return TextAnchor.MiddleRight;

                default:
                    return TextAnchor.MiddleLeft;
            }
        }
    }
}
