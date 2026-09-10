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
    /// <c>margin-left: -16px</c>, so it paints over the GameObject icon and costs no horizontal space at all.
    /// Unity uses the same slot for the "added as a prefab override" indicator and styles it through a USS
    /// class on the item - an inline background would silently erase it - so the badge falls back to its own
    /// element next to the name whenever that class is present or the icon is hidden.
    /// </para>
    /// </summary>
    internal sealed class IndicatorDecorator : IRowDecorator
    {
        private const string BadgeName = "hd-missing-script";

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
                                 && context.Item.Icon != null
                                 && context.Item.Icon.resolvedStyle.display != DisplayStyle.None;

            if (overlayIsFree)
            {
                RowElements.HideIfPresent(host, BadgeName);

                overlay.style.display = DisplayStyle.Flex;
                overlay.style.backgroundImage = new StyleBackground(GetWarningIcon());
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
        /// Hands the slot back to Unity. Only the inline background we wrote is cleared - the display keyword
        /// is left alone because HeaderDecorator also drives it.
        /// </summary>
        private static void ClearOverlay(VisualElement overlay)
        {
            if (overlay == null || overlay.style.backgroundImage.keyword == StyleKeyword.Null)
            {
                return;
            }

            overlay.style.backgroundImage = StyleKeyword.Null;
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
