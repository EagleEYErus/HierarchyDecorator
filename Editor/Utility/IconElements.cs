using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// Builds the icon elements used by the inline strip, the Components column and the indicator badge.
    ///
    /// Geometry is set inline rather than left to USS on purpose. A stylesheet can fail to load (a renamed
    /// embedded package, source dropped into Assets/, a broken import) and the failure mode would be
    /// zero-sized invisible icons - a silent, confusing break. The stylesheet still owns everything
    /// cosmetic; this only guarantees the layout.
    /// </summary>
    internal static class IconElements
    {
        public const float IconSize = 16f;
        public const float BadgeSize = 14f;

        public static VisualElement CreateStrip(string name)
        {
            VisualElement strip = new VisualElement
            {
                name = name,
                pickingMode = PickingMode.Ignore
            };

            strip.AddToClassList("hd-component-icons");
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.alignItems = Align.Center;
            strip.style.flexShrink = 0f;
            return strip;
        }

        public static VisualElement CreateIcon(string name, bool interactive)
        {
            VisualElement icon = new VisualElement
            {
                name = name,
                pickingMode = interactive ? PickingMode.Position : PickingMode.Ignore
            };

            icon.AddToClassList("hd-component-icon");
            ApplySize(icon, IconSize);
            return icon;
        }

        public static VisualElement CreateBadge(string name)
        {
            VisualElement badge = new VisualElement
            {
                name = name,
                pickingMode = PickingMode.Position
            };

            badge.AddToClassList("hd-indicator");
            ApplySize(badge, BadgeSize);
            return badge;
        }

        private static void ApplySize(VisualElement element, float size)
        {
            element.style.width = size;
            element.style.height = size;
            element.style.flexGrow = 0f;
            element.style.flexShrink = 0f;
            element.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            element.style.backgroundRepeat = new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));
        }
    }
}
