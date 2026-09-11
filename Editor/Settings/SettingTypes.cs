using System;
using UnityEngine;

namespace HierarchyDecorator
{
    public enum HeaderKind
    {
        Header = 0,
        Separator = 1
    }

    public enum HeaderAlignment
    {
        Left = 0,
        Center = 1,
        Right = 2
    }

    public enum HeaderTextCase
    {
        AsTyped = 0,
        Upper = 1,
        Lower = 2
    }

    public enum LineStyle
    {
        Solid = 0,
        Dashed = 1,
        Dotted = 2
    }

    public enum ComponentIconMode
    {
        /// <summary>Every component on the object is shown unless a rule hides it.</summary>
        All = 0,

        /// <summary>Only components a rule explicitly shows are drawn.</summary>
        Selected = 1
    }

    public enum ComponentDisplay
    {
        Default = 0,
        Show = 1,
        Hide = 2
    }

    public enum ComponentIconOrder
    {
        /// <summary>Component order on the GameObject.</summary>
        Natural = 0,
        Alphabetical = 1
    }

    public enum ComponentClickAction
    {
        None = 0,
        Select = 1,
        ToggleEnabled = 2
    }

    /// <summary>
    /// A colour with a separate value for the light and dark editor skin.
    /// The editor has no USS theme selector, so every themed value is authored as a pair.
    /// </summary>
    [Serializable]
    public struct ThemeColor : IEquatable<ThemeColor>
    {
        public Color light;
        public Color dark;

        public ThemeColor(Color light, Color dark)
        {
            this.light = light;
            this.dark = dark;
        }

        public static ThemeColor Uniform(Color color) => new ThemeColor(color, color);

        public readonly Color Resolve(bool isDarkSkin) => isDarkSkin ? dark : light;

        public readonly bool Equals(ThemeColor other) => light == other.light && dark == other.dark;
        public override readonly bool Equals(object obj) => obj is ThemeColor other && Equals(other);
        public override readonly int GetHashCode() => (light, dark).GetHashCode();
    }

    /// <summary>
    /// A name-pattern rule that turns matching rows into a header or a separator.
    /// The matching contract is intentionally identical to HierarchyDecorator 1.x so that
    /// existing scenes keep rendering after an upgrade.
    /// </summary>
    [Serializable]
    public class HeaderRule
    {
        public string name = "New Header";
        public bool enabled = true;

        public HeaderKind kind = HeaderKind.Header;

        [Tooltip("Literal prefix, or - when 'Use Regex' is on - a regular expression that is implicitly anchored with ^.")]
        public string pattern = "=";

        public bool useRegex;

        [Tooltip("When on, the character after the prefix must be a single space. This is the 1.x default and keeps '=' from matching '==='.")]
        public bool requireSpaceAfterPrefix = true;

        [Tooltip("Keep the matched prefix in the drawn label instead of stripping it.")]
        public bool keepPrefixInLabel;

        public HeaderAlignment alignment = HeaderAlignment.Center;
        public HeaderTextCase textCase = HeaderTextCase.Upper;
        public bool bold = true;
        [Range(6, 24)] public int fontSize = 11;
        [Range(0, 8)] public int letterSpacing;

        public ThemeColor textColor = new ThemeColor(new Color(0.176f, 0.176f, 0.176f), Color.white);
        public ThemeColor backgroundColor = new ThemeColor(new Color(0.667f, 0.667f, 0.667f), new Color(0.176f, 0.176f, 0.176f));

        [Tooltip("Draw a line across the row. Always on for Separator rules.")]
        public bool showLine;

        public LineStyle lineStyle = LineStyle.Solid;

        [Range(1, 4)] public int lineThickness = 1;

        [Tooltip("Draw component icons on rows matched by this rule.")]
        public bool showComponentIcons;

        [Tooltip("Draw tree guide lines on rows matched by this rule.")]
        public bool showTreeLines;

        public HeaderRule() { }

        public HeaderRule(string name, string pattern, ThemeColor text, ThemeColor background)
        {
            this.name = name;
            this.pattern = pattern;
            textColor = text;
            backgroundColor = background;
        }

        public HeaderRule Clone() => (HeaderRule)MemberwiseClone();
    }

    /// <summary>
    /// Per-component display rule.
    /// Identity is stored twice on purpose: built-in Unity components have no MonoScript, and user scripts
    /// survive renames only through their MonoScript GUID. See ARCHITECTURE.md D7.
    /// </summary>
    [Serializable]
    public class ComponentRule
    {
        /// <summary>Namespace-qualified type name, e.g. "UnityEngine.AudioSource".</summary>
        public string typeName;

        /// <summary>Simple assembly name, e.g. "UnityEngine.AudioModule".</summary>
        public string assemblyName;

        /// <summary>MonoScript GUID for user scripts. Empty for built-in components.</summary>
        public string monoScriptGuid;

        public ComponentDisplay display = ComponentDisplay.Default;

        public ComponentRule() { }

        public ComponentRule(string typeName, string assemblyName, string monoScriptGuid, ComponentDisplay display)
        {
            this.typeName = typeName;
            this.assemblyName = assemblyName;
            this.monoScriptGuid = monoScriptGuid;
            this.display = display;
        }

        public ComponentRule Clone() => (ComponentRule)MemberwiseClone();
    }

    [Serializable]
    public class TreeLineSettings
    {
        public bool enabled = true;

        [Tooltip("Draw a guide line for every ancestor level, not just the immediate parent.")]
        public bool fullDepth = true;

        [Tooltip("Draw the short horizontal connector from the guide line to the row.")]
        public bool showConnector = true;

        public LineStyle style = LineStyle.Solid;

        [Range(0.05f, 1f)] public float opacity = 0.35f;

        public ThemeColor color = new ThemeColor(new Color(0.1f, 0.1f, 0.1f), new Color(0.85f, 0.85f, 0.85f));

        public TreeLineSettings Clone() => (TreeLineSettings)MemberwiseClone();
    }

    [Serializable]
    public class ComponentIconSettings
    {
        public bool enabled = true;

        public ComponentIconMode mode = ComponentIconMode.All;

        [Tooltip("Include user MonoBehaviours when the mode is 'All'.")]
        public bool includeCustomScripts = true;

        [Tooltip("Never draw an icon for these always-present components.")]
        public bool hideTransform = true;

        [Range(0, 16)]
        [Tooltip("0 means unlimited.")]
        public int maxIconsPerRow = 8;

        public bool showOverflowIndicator = true;

        public bool showTooltips = true;

        [Tooltip("Collapse repeated components of the same type into a single icon with a count.")]
        public bool stackDuplicates;

        [Tooltip("Draw the icon dimmed when the component is disabled.")]
        public bool fadeDisabledComponents = true;

        public ComponentIconOrder order = ComponentIconOrder.Natural;

        // Toggling is the action people asked for, and it is the only one that does something a plain click
        // on the row does not: Select puts the same GameObject in the Inspector that clicking the row
        // already would, so with it the icons look inert.
        public ComponentClickAction clickAction = ComponentClickAction.ToggleEnabled;

        public ComponentIconSettings Clone()
        {
            return (ComponentIconSettings)MemberwiseClone();
        }
    }

    [Serializable]
    public class IndicatorSettings
    {
        [Tooltip("Show a warning badge on GameObjects with a missing MonoBehaviour script.")]
        public bool showMissingScripts = true;

        public IndicatorSettings Clone() => (IndicatorSettings)MemberwiseClone();
    }

    [Serializable]
    public class RowSettings
    {
        [Tooltip("Unity 6.6 draws alternating rows itself. Enable this only to override the colours it uses.")]
        public bool overrideAlternatingColors;

        public ThemeColor evenColor = new ThemeColor(new Color(0.8f, 0.8f, 0.8f), new Color(0.235f, 0.235f, 0.235f));
        public ThemeColor oddColor = new ThemeColor(new Color(0.765f, 0.765f, 0.765f), new Color(0.212f, 0.212f, 0.212f));

        public RowSettings Clone() => (RowSettings)MemberwiseClone();
    }
}
