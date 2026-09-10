using System;
using System.Collections.Generic;
using UnityEngine;

namespace HierarchyDecorator
{
    /// <summary>
    /// A named snapshot of the feature toggles.
    ///
    /// Presets deliberately never touch the header rules or the per-component rules - not their content and
    /// not their enabled flags. Those are content a team authors; a preset that silently re-enabled a rule
    /// someone had turned off would be destroying work, not applying a style.
    /// </summary>
    [Serializable]
    public class Preset
    {
        public string name = "New Preset";

        public bool treeLines = true;
        public bool treeLinesFullDepth = true;
        public bool treeLineConnector = true;
        public LineStyle treeLineStyle = LineStyle.Solid;
        [Range(0.05f, 1f)] public float treeLineOpacity = 0.35f;

        public bool overrideRowColors;

        public bool componentIcons = true;
        public ComponentIconMode componentIconMode = ComponentIconMode.All;
        public bool componentIconsIncludeCustomScripts = true;
        [Range(0, 16)] public int maxIconsPerRow = 8;
        public bool componentTooltips = true;
        public bool fadeDisabledComponents = true;
        public ComponentClickAction clickAction = ComponentClickAction.Select;

        public bool missingScriptIndicator = true;

        public Preset Clone() => (Preset)MemberwiseClone();

        public void ApplyTo(HierarchyDecoratorSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            TreeLineSettings lines = settings.TreeLines;
            lines.enabled = treeLines;
            lines.fullDepth = treeLinesFullDepth;
            lines.showConnector = treeLineConnector;
            lines.style = treeLineStyle;
            lines.opacity = treeLineOpacity;

            settings.Rows.overrideAlternatingColors = overrideRowColors;

            ComponentIconSettings icons = settings.ComponentIcons;
            icons.enabled = componentIcons;
            icons.mode = componentIconMode;
            icons.includeCustomScripts = componentIconsIncludeCustomScripts;
            icons.maxIconsPerRow = maxIconsPerRow;
            icons.showTooltips = componentTooltips;
            icons.fadeDisabledComponents = fadeDisabledComponents;
            icons.clickAction = clickAction;

            settings.Indicators.showMissingScripts = missingScriptIndicator;
        }

        public static Preset CaptureFrom(HierarchyDecoratorSettings settings, string presetName)
        {
            TreeLineSettings lines = settings.TreeLines;
            ComponentIconSettings icons = settings.ComponentIcons;

            return new Preset
            {
                name = presetName,
                treeLines = lines.enabled,
                treeLinesFullDepth = lines.fullDepth,
                treeLineConnector = lines.showConnector,
                treeLineStyle = lines.style,
                treeLineOpacity = lines.opacity,
                overrideRowColors = settings.Rows.overrideAlternatingColors,
                componentIcons = icons.enabled,
                componentIconMode = icons.mode,
                componentIconsIncludeCustomScripts = icons.includeCustomScripts,
                maxIconsPerRow = icons.maxIconsPerRow,
                componentTooltips = icons.showTooltips,
                fadeDisabledComponents = icons.fadeDisabledComponents,
                clickAction = icons.clickAction,
                missingScriptIndicator = settings.Indicators.showMissingScripts
            };
        }
    }

    public static class BuiltInPresets
    {
        public const string MinimalName = "Minimal";
        public const string CleanName = "Clean";
        public const string DeveloperName = "Developer";
        public const string DesignerName = "Designer";
        public const string DebugName = "Debug";

        public static IReadOnlyList<Preset> All => s_All;

        private static readonly Preset[] s_All =
        {
            new Preset
            {
                name = MinimalName,
                treeLines = true,
                treeLinesFullDepth = false,
                treeLineConnector = false,
                treeLineStyle = LineStyle.Dotted,
                treeLineOpacity = 0.18f,
                overrideRowColors = false,
                componentIcons = false,
                missingScriptIndicator = true,
                maxIconsPerRow = 0
            },
            new Preset
            {
                name = CleanName,
                treeLines = true,
                treeLinesFullDepth = true,
                treeLineConnector = true,
                treeLineStyle = LineStyle.Solid,
                treeLineOpacity = 0.3f,
                overrideRowColors = false,
                componentIcons = true,

                // Built-in components only, capped short. "Selected" would mean "only what an explicit rule
                // shows" - and with no rules stored, that is nothing at all, which is what a fresh install
                // would have looked like.
                componentIconMode = ComponentIconMode.All,
                componentIconsIncludeCustomScripts = false,
                maxIconsPerRow = 4,
                componentTooltips = true,
                clickAction = ComponentClickAction.Select,
                missingScriptIndicator = true
            },
            new Preset
            {
                name = DeveloperName,
                treeLines = true,
                treeLinesFullDepth = true,
                treeLineConnector = true,
                treeLineStyle = LineStyle.Solid,
                treeLineOpacity = 0.4f,
                overrideRowColors = false,
                componentIcons = true,
                componentIconMode = ComponentIconMode.All,
                componentIconsIncludeCustomScripts = true,
                maxIconsPerRow = 8,
                componentTooltips = true,
                fadeDisabledComponents = true,
                clickAction = ComponentClickAction.ToggleEnabled,
                missingScriptIndicator = true
            },
            new Preset
            {
                name = DesignerName,
                treeLines = true,
                treeLinesFullDepth = true,
                treeLineConnector = true,
                treeLineStyle = LineStyle.Solid,
                treeLineOpacity = 0.25f,
                overrideRowColors = false,
                componentIcons = false,
                maxIconsPerRow = 0,
                componentTooltips = false,
                missingScriptIndicator = false
            },
            new Preset
            {
                name = DebugName,
                treeLines = true,
                treeLinesFullDepth = true,
                treeLineConnector = true,
                treeLineStyle = LineStyle.Dashed,
                treeLineOpacity = 0.5f,
                overrideRowColors = true,
                componentIcons = true,
                componentIconMode = ComponentIconMode.All,
                componentIconsIncludeCustomScripts = true,
                maxIconsPerRow = 12,
                componentTooltips = true,
                fadeDisabledComponents = true,
                clickAction = ComponentClickAction.ToggleEnabled,
                missingScriptIndicator = true
            }
        };

        public static Preset Find(string name)
        {
            for (int i = 0; i < s_All.Length; i++)
            {
                if (s_All[i].name == name)
                {
                    return s_All[i];
                }
            }

            return null;
        }

        public static bool IsBuiltIn(string name) => Find(name) != null;
    }
}
