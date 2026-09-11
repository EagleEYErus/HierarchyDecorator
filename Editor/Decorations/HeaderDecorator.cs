using Unity.Hierarchy;
using UnityEngine;
using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// Headers and separators - rows whose name matches a <see cref="HeaderRule"/> become scene section
    /// markers.
    ///
    /// Unlike 1.x this never repaints the row. The row's own background colour is tinted, a label of our own
    /// carries the stripped text, and a separator line is a background image on the shared row container.
    /// Unity keeps drawing the foldout, the prefab override bar and the prefab text colours, which is what
    /// fixes the "two tone background hides the override bar" and "SubScene caret hidden" classes of bug
    /// outright.
    ///
    /// <para>
    /// The fill is an inline style, and an inline style outranks every stylesheet - including Unity's
    /// selection rule. A selected row is therefore left unfilled so the highlight shows; hover cannot be
    /// read from C# at all and stays overridden, which is recorded in ARCHITECTURE.md as a limitation.
    /// </para>
    ///
    /// <para>
    /// The header text is deliberately NOT written into Unity's own name Label. Unity's inline rename reads
    /// that Label to seed the edit field (<c>HierarchyViewItemName.BeginRename</c> does
    /// <c>TextField.value = Label.text</c>), so overwriting it meant renaming "= PLAYER" committed "PLAYER" -
    /// silently destroying the prefix, the casing, and therefore the header itself. Unity's Label is hidden
    /// instead, and left holding the true name.
    /// </para>
    ///
    /// It runs after <see cref="RowTintDecorator"/> on purpose: that decorator always writes the row
    /// background (a colour or a reset), so a header only has to overwrite it and never has to clean up.
    /// </summary>
    internal sealed class HeaderDecorator : IRowDecorator
    {
        /// <summary>Unity's HierarchyViewItem indent step; see TreeLineDecorator for where this comes from.</summary>
        private const float IndentWidth = 14f;

        private const string LabelName = "hd-header-label";
        private const string RenameHookKey = "hd-rename-hook";

        public string Id => "headers";

        public void Apply(in RowContext context)
        {
            Label unityLabel = context.Item.Name;
            VisualElement host = context.Item.LeftCustomContainer;

            if (unityLabel == null || host == null)
            {
                return;
            }

            HeaderRule rule = context.Active && context.IsGameObject ? context.Header : null;

            if (rule == null)
            {
                Reset(context, unityLabel, host);
                return;
            }

            bool isSeparator = rule.kind == HeaderKind.Separator;
            bool hasLabel = !string.IsNullOrEmpty(context.Data.HeaderLabel);

            VisualElement row = context.Item.RowContainer;

            if (row != null)
            {
                if (isSeparator)
                {
                    ApplySeparatorLine(row, rule, context.IsDarkSkin, hasLabel, context.IsSelected);
                }
                else
                {
                    ClearSeparatorLine(row);
                    SetRowBackground(row, rule.backgroundColor.Resolve(context.IsDarkSkin), context.IsSelected);

                    if (rule.showLine)
                    {
                        ApplySeparatorLine(row, rule, context.IsDarkSkin, hasLabel, context.IsSelected);
                    }
                }
            }

            Label header = RowElements.Find<Label>(host, LabelName) ?? CreateHeaderLabel(host, context.Item);

            header.style.display = DisplayStyle.Flex;
            header.text = hasLabel ? context.Data.HeaderLabel : string.Empty;
            header.style.color = rule.textColor.Resolve(context.IsDarkSkin);
            header.style.fontSize = rule.fontSize;
            header.style.unityFontStyleAndWeight = rule.bold ? FontStyle.Bold : FontStyle.Normal;
            header.style.letterSpacing = rule.letterSpacing;
            header.style.unityTextAlign = ToTextAnchor(rule.alignment);
            header.userData = true;

            // Unity indents a row by translating LeftContainer, which does not change its laid-out box - so a
            // label centred inside that box lands half an indent to the right of the row's real centre, and
            // the error grows with depth. Translating the label back by the same amount cancels it exactly.
            // Left-aligned rules keep the indent: for those, sitting under the parent is the point.
            if (rule.alignment == HeaderAlignment.Left || context.IsFiltering)
            {
                header.style.translate = StyleKeyword.Null;
            }
            else
            {
                float indent = IndentWidth * Mathf.Max(0, HierarchyNodes.GetViewDepth(context.View, context.Node));
                header.style.translate = new StyleTranslate(new Translate(-indent, 0f, 0f));
            }

            // Unity's label keeps the real name; it is only hidden so the row shows ours instead.
            unityLabel.style.display = DisplayStyle.None;

            // Stretch so a centred or right-aligned header spans the row rather than just its own text box.
            // The right container normally takes the slack, so it is neutralised here; it keeps its content
            // size, which is what still pins the icon strip to the right.
            VisualElement nameElement = unityLabel.parent;
            VisualElement leftContainer = nameElement?.parent;

            SetStretch(header, true);
            SetStretch(host, true);
            SetStretch(leftContainer, true);

            if (context.Item.RightCustomContainer != null)
            {
                context.Item.RightCustomContainer.style.flexGrow = 0f;
            }

            // A centred marker reads better without the GameObject icon; a left-aligned one keeps it so the
            // row still looks like the object it is.
            bool hideIcon = !isSeparator && rule.alignment != HeaderAlignment.Left;
            SetIconVisible(context.Item, !hideIcon);
        }

        private static void Reset(in RowContext context, Label unityLabel, VisualElement host)
        {
            unityLabel.style.display = StyleKeyword.Null;

            Label header = RowElements.Find<Label>(host, LabelName);

            if (header != null)
            {
                header.style.display = DisplayStyle.None;
                header.style.translate = StyleKeyword.Null;
                header.userData = false;
                SetStretch(header, false);
            }

            VisualElement nameElement = unityLabel.parent;
            VisualElement leftContainer = nameElement?.parent;

            SetStretch(host, false);
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

        private static void SetIconVisible(HierarchyViewItem item, bool visible)
        {
            if (item.Icon != null)
            {
                item.Icon.style.display = visible ? StyleKeyword.Null : DisplayStyle.None;
            }

            if (item.OverlayIcon != null)
            {
                item.OverlayIcon.style.display = visible ? StyleKeyword.Null : DisplayStyle.None;
            }
        }

        private static Label CreateHeaderLabel(VisualElement host, HierarchyViewItem item)
        {
            Label header = new Label
            {
                name = LabelName,
                pickingMode = PickingMode.Ignore
            };

            header.AddToClassList("hd-header-label");
            host.Insert(0, header);

            HookRename(item, header);
            return header;
        }

        /// <summary>
        /// Swaps our label out while Unity's inline rename field is up, and back afterwards. Registered once
        /// per pooled row; the callbacks read the current state from the label's userData, so they stay
        /// correct as the row is rebound to other objects.
        /// </summary>
        private static void HookRename(HierarchyViewItem item, Label header)
        {
            VisualElement nameElement = item.Name?.parent;

            if (nameElement == null || (nameElement.userData as string) == RenameHookKey)
            {
                return;
            }

            TextField field = FindTextField(nameElement);

            if (field == null)
            {
                return;
            }

            nameElement.userData = RenameHookKey;

            field.RegisterCallback<FocusInEvent>(_ => header.style.display = DisplayStyle.None);

            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                bool active = header.userData is bool flag && flag;

                header.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;

                if (item.Name != null)
                {
                    item.Name.style.display = active ? DisplayStyle.None : StyleKeyword.Null;
                }
            });
        }

        private static TextField FindTextField(VisualElement parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent[i] is TextField field)
                {
                    return field;
                }
            }

            return null;
        }

        /// <summary>
        /// The line is a repeating background image on the row container rather than a child element, so no
        /// element is ever inserted into a container Unity owns and the line spans the full row width.
        ///
        /// It sits at the row's vertical centre when there is no label - the classic divider - and at the
        /// bottom edge when there is one, because a centred line would run through the text and read as a
        /// strikethrough.
        /// </summary>
        private static void ApplySeparatorLine(VisualElement row, HeaderRule rule, bool isDarkSkin, bool hasLabel, bool isSelected)
        {
            Texture2D texture = LineTextures.GetStrip(rule.lineStyle, vertical: false);

            // Transparent is still an inline value, and an inline value still beats the selection rule -
            // a separator row would lose its highlight just as a filled header would.
            SetRowBackground(
                row,
                rule.kind == HeaderKind.Separator ? new Color(0f, 0f, 0f, 0f) : rule.backgroundColor.Resolve(isDarkSkin),
                isSelected);

            row.style.backgroundImage = new StyleBackground(texture);
            row.style.unityBackgroundImageTintColor = rule.textColor.Resolve(isDarkSkin);
            row.style.backgroundRepeat = new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.Repeat, Repeat.NoRepeat));
            row.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(texture.width, rule.lineThickness));
            row.style.backgroundPositionY = new StyleBackgroundPosition(
                new BackgroundPosition(hasLabel ? BackgroundPositionKeyword.Bottom : BackgroundPositionKeyword.Center));
        }

        /// <summary>
        /// Writes the row fill, unless the row is selected - see <see cref="RowContext.IsSelected"/>. Every
        /// background write in this file goes through here so the rule cannot be forgotten in one branch.
        /// </summary>
        private static void SetRowBackground(VisualElement row, Color color, bool isSelected)
        {
            row.style.backgroundColor = isSelected ? (StyleColor)StyleKeyword.Null : new StyleColor(color);
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
