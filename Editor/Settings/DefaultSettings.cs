using System.Collections.Generic;
using UnityEngine;

namespace HierarchyDecorator
{
    /// <summary>
    /// The out-of-the-box ruleset. The three header rules reproduce HierarchyDecorator 1.x's shipped
    /// defaults exactly - same prefixes, same sizes, same colours - so an upgraded project looks unchanged.
    /// </summary>
    internal static class DefaultSettings
    {
        public static void PopulateHeaderRules(List<HeaderRule> rules)
        {
            rules.Clear();

            // Evaluated in order, first match wins. The separator is first so that "---" never falls
            // through to the "-" sub-header rule.
            rules.Add(new HeaderRule
            {
                name = "Separator",
                kind = HeaderKind.Separator,
                pattern = "---",
                requireSpaceAfterPrefix = false,
                lineStyle = LineStyle.Solid,
                lineThickness = 1,
                textColor = new ThemeColor(new Color(0.35f, 0.35f, 0.35f), new Color(0.62f, 0.62f, 0.62f)),
                backgroundColor = new ThemeColor(new Color(0.35f, 0.35f, 0.35f, 0.5f), new Color(0.62f, 0.62f, 0.62f, 0.4f)),
                alignment = HeaderAlignment.Center,
                textCase = HeaderTextCase.Upper,
                fontSize = 10,
                showComponentIcons = false,
                showTreeLines = false
            });

            rules.Add(new HeaderRule
            {
                name = "Header (Centered)",
                pattern = "=",
                alignment = HeaderAlignment.Center,
                textCase = HeaderTextCase.Upper,
                bold = true,
                fontSize = 11,
                letterSpacing = 1,
                textColor = new ThemeColor(new Color(0.1764706f, 0.1764706f, 0.1764706f), Color.white),
                backgroundColor = new ThemeColor(new Color(0.6666667f, 0.6666667f, 0.6666667f), new Color(0.1764706f, 0.1764706f, 0.1764706f)),
                lineStyle = LineStyle.Solid,
                showComponentIcons = false,
                showTreeLines = false
            });

            rules.Add(new HeaderRule
            {
                name = "Subheader",
                pattern = "-",
                alignment = HeaderAlignment.Left,
                textCase = HeaderTextCase.Upper,
                bold = true,
                fontSize = 10,
                textColor = new ThemeColor(new Color(0.245283f, 0.245283f, 0.245283f), new Color(0.8584906f, 0.8584906f, 0.8584906f)),
                backgroundColor = new ThemeColor(new Color(0.7960785f, 0.7960785f, 0.7960785f), new Color(0.2352941f, 0.2352941f, 0.2352941f)),
                lineStyle = LineStyle.Solid,
                showComponentIcons = false,
                showTreeLines = false
            });

            rules.Add(new HeaderRule
            {
                name = "Mini Header (Centered)",
                pattern = "+",
                alignment = HeaderAlignment.Center,
                textCase = HeaderTextCase.Upper,
                bold = true,
                fontSize = 10,
                textColor = new ThemeColor(Color.white, Color.white),
                backgroundColor = new ThemeColor(new Color(0.38568f, 0.6335747f, 0.764151f), new Color(0.2671325f, 0.4473481f, 0.6509434f)),
                lineStyle = LineStyle.Solid,
                showComponentIcons = false,
                showTreeLines = false
            });
        }

        /// <summary>
        /// Components that carry no information when shown on every single row.
        /// Used to seed the rule list the first time the icon settings are opened.
        /// </summary>
        public static readonly string[] NoisyBuiltInComponents =
        {
            "UnityEngine.Transform",
            "UnityEngine.RectTransform",
            "UnityEngine.CanvasRenderer"
        };
    }
}
