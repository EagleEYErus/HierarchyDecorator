using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// Problem indicators. Deliberately narrow: a missing MonoBehaviour script is the one defect that is both
    /// common and free to detect, because the component scan that feeds the icon strip already produces it.
    /// HierarchyDecorator is not a static analyser (see ARCHITECTURE.md, scope control).
    ///
    /// <para>
    /// The badge is drawn on <c>HierarchyViewItem.OverlayIcon</c> when that slot is free: it has
    /// <c>margin-left: -16px</c>, so it overlaps the GameObject icon and costs no horizontal space at all.
    /// It is drawn small and in the bottom-right corner of that slot so the object icon underneath stays
    /// readable.
    /// Unity uses the same slot for the "added as a prefab override" indicator and styles it through a USS
    /// class on the item - an inline background would silently erase it - so the badge falls back to its own
    /// element next to the name whenever that class is present or the icon is hidden.
    /// </para>
    /// </summary>
    internal sealed class IndicatorDecorator : IRowDecorator
    {
        private const string BadgeName = "hd-missing-script";

        /// <summary>Size of the mark drawn in the corner of the object icon.</summary>
        private const float BadgeSize = 11f;

        /// <summary>Unity's class for "this GameObject was added as a prefab override", which owns OverlayIcon.</summary>
        private const string PrefabOverlayClass = "hierarchy-item__prefab-overlay-node";

        private static Texture2D s_WarningIcon;

        public string Id => "indicators";

        public void Apply(in RowContext context)
        {
            VisualElement host = context.Item.LeftCustomContainer;
            VisualElement overlay = context.Item.OverlayIcon;

            if (host == null)
            {
                return;
            }

            bool active = context.Active
                          && context.IsGameObject
                          && context.Settings.Indicators.showMissingScripts
                          && context.Data != null
                          && context.Data.MissingScriptCount > 0;

            if (!active)
            {
                RowElements.HideIfPresent(host, BadgeName);
                ClearOverlay(overlay);
                return;
            }

            int count = context.Data.MissingScriptCount;
            string tooltip = count == 1
                ? "Missing MonoBehaviour script"
                : $"{count} missing MonoBehaviour scripts";

            bool overlayIsFree = overlay != null
                                 && !context.Item.ClassListContains(PrefabOverlayClass)
                                 && IsIconVisible(context.Item.Icon);

            if (overlayIsFree)
            {
                RowElements.HideIfPresent(host, BadgeName);

                overlay.style.display = DisplayStyle.Flex;
                overlay.style.backgroundImage = new StyleBackground(GetWarningIcon());

                // A quarter-size mark in the bottom-right corner, the way Unity draws its own overlays: at
                // the slot's full 16px the warning triangle covers the object icon completely, so the row
                // stops saying what kind of object it is exactly when that matters most.
                overlay.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BadgeSize, BadgeSize));
                overlay.style.backgroundRepeat = new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));
                overlay.style.backgroundPositionX = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Right));
                overlay.style.backgroundPositionY = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Bottom));

                overlay.tooltip = tooltip;
                return;
            }

            ClearOverlay(overlay);

            VisualElement badge = RowElements.Find<VisualElement>(host, BadgeName) ?? CreateBadge(host);

            badge.style.display = DisplayStyle.Flex;
            badge.style.backgroundImage = new StyleBackground(GetWarningIcon());
            badge.tooltip = tooltip;
        }

        /// <summary>
        /// Is the object icon actually on screen?
        ///
        /// This must read the inline style, not <c>resolvedStyle</c>. HeaderDecorator hides the icon a few
        /// lines earlier in the same dispatch, and a resolved style is only recomputed at the next layout
        /// pass - so the resolved value here still describes the previous frame. Trusting it put the badge
        /// on an element that had just been hidden, and a header row with a broken script showed nothing.
        /// </summary>
        private static bool IsIconVisible(VisualElement icon)
        {
            if (icon == null)
            {
                return false;
            }

            StyleEnum<DisplayStyle> display = icon.style.display;

            // StyleKeyword.Null means "no inline value, the stylesheet decides" - which for the object icon
            // is visible. Anything else carries a real inline value, and only that value can hide it.
            return display.keyword == StyleKeyword.Null || display.value != DisplayStyle.None;
        }

        /// <summary>
        /// Hands the slot back to Unity. Only the inline properties we wrote are cleared - the display
        /// keyword is left alone because HeaderDecorator also drives it.
        /// </summary>
        private static void ClearOverlay(VisualElement overlay)
        {
            if (overlay == null || overlay.style.backgroundImage.keyword == StyleKeyword.Null)
            {
                return;
            }

            overlay.style.backgroundImage = StyleKeyword.Null;
            overlay.style.backgroundSize = StyleKeyword.Null;
            overlay.style.backgroundRepeat = StyleKeyword.Null;
            overlay.style.backgroundPositionX = StyleKeyword.Null;
            overlay.style.backgroundPositionY = StyleKeyword.Null;
            overlay.tooltip = null;
        }

        private static VisualElement CreateBadge(VisualElement host)
        {
            VisualElement badge = IconElements.CreateBadge(BadgeName);
            host.Add(badge);
            return badge;
        }

        private static Texture2D GetWarningIcon()
        {
            if (s_WarningIcon != null)
            {
                return s_WarningIcon;
            }

            // IconContent resolves the skin variant itself; never hand-write the "d_" prefix.
            GUIContent content = EditorGUIUtility.IconContent("console.warnicon.sml");
            s_WarningIcon = content?.image as Texture2D;

            if (s_WarningIcon == null)
            {
                s_WarningIcon = EditorGUIUtility.IconContent("console.warnicon")?.image as Texture2D;
            }

            return s_WarningIcon;
        }
    }
}
