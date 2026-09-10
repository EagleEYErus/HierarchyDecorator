using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// Problem indicators. Deliberately narrow: a missing MonoBehaviour script is the one defect that is both
    /// common and free to detect, because the component scan that feeds the icon strip already produces it.
    /// HierarchyDecorator is not a static analyser (see ARCHITECTURE.md, scope control).
    /// </summary>
    internal sealed class IndicatorDecorator : IRowDecorator
    {
        private const string BadgeName = "hd-missing-script";

        private static Texture2D s_WarningIcon;

        public string Id => "indicators";

        public void Apply(in RowContext context)
        {
            VisualElement host = context.Item.LeftCustomContainer;

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
                return;
            }

            VisualElement badge = RowElements.Find<VisualElement>(host, BadgeName) ?? CreateBadge(host);

            badge.style.display = DisplayStyle.Flex;
            badge.style.backgroundImage = new StyleBackground(GetWarningIcon());

            int count = context.Data.MissingScriptCount;
            badge.tooltip = count == 1
                ? "Missing MonoBehaviour script"
                : $"{count} missing MonoBehaviour scripts";
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
