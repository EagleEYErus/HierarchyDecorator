using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator
{
    /// <summary>
    /// Imports a HierarchyDecorator 1.x settings asset into the 2.0 settings.
    ///
    /// The 1.x asset is read as text (see <see cref="MiniYaml"/>) and is never modified or deleted, so a
    /// migration can be repeated and nothing the user configured is destroyed.
    /// </summary>
    internal static class LegacyMigrator
    {
        /// <summary>GUID of HierarchyDecorator 1.x's Settings.cs, stable across every 0.x release.</summary>
        private const string LegacyScriptGuid = "00668fd727de9bb4081a8a202ce24c3b";

        private const string LegacyClassIdentifier = "Wooshii.HierarchyDecorator.Editor::HierarchyDecorator.Settings";

        internal sealed class Report
        {
            public string SourcePath;
            public int HeaderRules;
            public int ComponentRules;
            public int UnresolvedComponents;
            public readonly List<string> Notes = new List<string>();

            public override string ToString()
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine($"Migrated HierarchyDecorator 1.x settings from '{SourcePath}'.");
                builder.AppendLine($"  Header rules:    {HeaderRules}");
                builder.AppendLine($"  Component rules: {ComponentRules}");

                if (UnresolvedComponents > 0)
                {
                    builder.AppendLine($"  Skipped {UnresolvedComponents} component entries whose type no longer exists.");
                }

                for (int i = 0; i < Notes.Count; i++)
                {
                    builder.AppendLine("  " + Notes[i]);
                }

                builder.Append("The original asset was left untouched. See MIGRATION.md for the full mapping.");
                return builder.ToString();
            }
        }

        /// <summary>Called once per domain from the bootstrap.</summary>
        internal static void RunIfNeeded()
        {
            HierarchyDecoratorSettings settings = HierarchyDecoratorSettings.instance;

            if (settings.LegacyMigrationCompleted)
            {
                return;
            }

            bool freshInstall = !File.Exists(HierarchyDecoratorSettings.FilePath);
            List<string> candidates = FindLegacyAssets();

            if (candidates.Count == 0)
            {
                settings.LegacyMigrationCompleted = true;
                settings.Persist();
                return;
            }

            if (!freshInstall)
            {
                // The project already has 2.0 settings, so silently replacing them would be the one thing a
                // migration must never do.
                settings.LegacyMigrationCompleted = true;
                settings.Persist();

                HierarchyLog.Once(
                    "legacy-settings-found",
                    $"Found HierarchyDecorator 1.x settings at '{candidates[0]}', but this project already has " +
                    "2.0 settings so nothing was changed. Run Tools > Hierarchy Decorator > Import Settings " +
                    "From 1.x to import them.");
                return;
            }

            if (candidates.Count > 1)
            {
                HierarchyLog.Once(
                    "legacy-settings-multiple",
                    $"Found {candidates.Count} HierarchyDecorator 1.x settings assets. Importing '{candidates[0]}'. " +
                    "Use Tools > Hierarchy Decorator > Import Settings From 1.x to choose a different one.");
            }

            if (TryMigrate(candidates[0], settings, out Report report))
            {
                settings.LegacyMigrationCompleted = true;
                settings.MarkChanged();
                HierarchyLog.Info(report.ToString());
            }
        }

        internal static List<string> FindLegacyAssets()
        {
            List<string> results = new List<string>();

            string assetsRoot = Application.dataPath;

            if (!Directory.Exists(assetsRoot))
            {
                return results;
            }

            string[] files;

            try
            {
                files = Directory.GetFiles(assetsRoot, "*.asset", SearchOption.AllDirectories);
            }
            catch (IOException e)
            {
                HierarchyLog.Once("legacy-scan", "Could not scan the project for 1.x settings.", e);
                return results;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string text;

                try
                {
                    text = File.ReadAllText(files[i]);
                }
                catch (IOException)
                {
                    continue;
                }

                if (text.IndexOf(LegacyScriptGuid, StringComparison.Ordinal) < 0 &&
                    text.IndexOf(LegacyClassIdentifier, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                results.Add(ToProjectRelativePath(files[i]));
            }

            // The EditorPrefs pointer 1.x kept is only a hint, but when it resolves it names the asset the
            // user was actually using, so it goes first.
            string hintGuid = EditorPrefs.GetString(Application.productName + "_HD_GUID", string.Empty);

            if (!string.IsNullOrEmpty(hintGuid))
            {
                string hintPath = AssetDatabase.GUIDToAssetPath(hintGuid);
                int index = results.IndexOf(hintPath);

                if (index > 0)
                {
                    results.RemoveAt(index);
                    results.Insert(0, hintPath);
                }
            }

            return results;
        }

        internal static bool TryMigrate(string assetPath, HierarchyDecoratorSettings settings, out Report report)
        {
            report = new Report { SourcePath = assetPath };

            string absolute = ToAbsolutePath(assetPath);

            if (!File.Exists(absolute))
            {
                HierarchyLog.Once("legacy-missing:" + assetPath, $"Cannot read '{assetPath}'.");
                return false;
            }

            YamlNode root;

            try
            {
                root = MiniYaml.Parse(File.ReadAllText(absolute));
            }
            catch (Exception e)
            {
                HierarchyLog.Once("legacy-parse:" + assetPath, $"Could not parse '{assetPath}'.", e);
                return false;
            }

            YamlNode data = root["MonoBehaviour"];

            if (data.IsEmpty)
            {
                data = root;
            }

            MigrateStyles(data["styleData"], settings, report);
            MigrateRows(data["styleData"], settings);
            MigrateBreadcrumbs(data["globalData"], settings);
            MigrateComponents(data["components"], settings, report);

            NameMatcher.ClearRegexCache();
            return true;
        }

        private static void MigrateStyles(YamlNode styleData, HierarchyDecoratorSettings settings, Report report)
        {
            IReadOnlyList<YamlNode> styles = styleData["styles"].Items;

            if (styles.Count == 0)
            {
                report.Notes.Add("No header styles found in the 1.x asset; the 2.0 defaults were kept.");
                return;
            }

            bool showIconsOnStyledRows = styleData["displayIcons"].AsBool(true);

            List<HeaderRule> rules = settings.HeaderRules;
            rules.Clear();

            for (int i = 0; i < styles.Count; i++)
            {
                YamlNode style = styles[i];

                HeaderRule rule = new HeaderRule
                {
                    name = style["name"].AsString("Header " + i),
                    pattern = style["prefix"].AsString("="),
                    useRegex = style["isRegex"].AsBool(),
                    requireSpaceAfterPrefix = !style["noSpaceAfterPrefix"].AsBool(),
                    fontSize = Mathf.Clamp(style["fontSize"].AsInt(11), 6, 24),
                    alignment = ToAlignment(style["fontAlignment"].AsInt(4)),
                    bold = IsBold(style["fontStyle"].AsInt(1)),
                    textCase = ToTextCase(style["textFormatting"].AsInt(0)),
                    showComponentIcons = showIconsOnStyledRows,
                    showTreeLines = false,
                    enabled = true
                };

                IReadOnlyList<YamlNode> modes = style["modes"].Items;

                Color lightText = new Color(0.176f, 0.176f, 0.176f);
                Color lightBackground = new Color(0.667f, 0.667f, 0.667f);
                Color darkText = Color.white;
                Color darkBackground = new Color(0.176f, 0.176f, 0.176f);

                if (modes.Count > 0)
                {
                    lightText = modes[0]["fontColour"].AsColor(lightText);
                    lightBackground = modes[0]["backgroundColour"].AsColor(lightBackground);
                }

                if (modes.Count > 1)
                {
                    darkText = modes[1]["fontColour"].AsColor(darkText);
                    darkBackground = modes[1]["backgroundColour"].AsColor(darkBackground);
                }

                rule.textColor = new ThemeColor(lightText, darkText);
                rule.backgroundColor = new ThemeColor(lightBackground, darkBackground);

                rules.Add(rule);
                report.HeaderRules++;
            }

            // 1.x had no separator concept, so the 2.0 default is appended rather than lost. It is placed
            // first because "---" would otherwise fall through to a "-" sub-header rule.
            List<HeaderRule> defaults = new List<HeaderRule>();
            DefaultSettings.PopulateHeaderRules(defaults);

            HeaderRule separator = defaults.Find(r => r.kind == HeaderKind.Separator);

            if (separator != null && !rules.Exists(r => r.kind == HeaderKind.Separator))
            {
                rules.Insert(0, separator);
                report.HeaderRules++;
                report.Notes.Add("Added the 2.0 '---' separator rule, which has no 1.x equivalent.");
            }

            report.Notes.Add("Header rule order was preserved: the first matching rule still wins.");
        }

        private static void MigrateRows(YamlNode styleData, HierarchyDecoratorSettings settings)
        {
            if (styleData.IsEmpty)
            {
                return;
            }

            RowSettings rows = settings.Rows;

            // Unity 6.6 draws alternating rows natively, so this only carries over when the user had
            // customised the colours away from 1.x's defaults.
            rows.overrideAlternatingColors = styleData["twoToneBackground"].AsBool(false);

            YamlNode light = styleData["lightMode"];
            YamlNode dark = styleData["darkMode"];

            rows.evenColor = new ThemeColor(
                light["colorOne"].AsColor(rows.evenColor.light),
                dark["colorOne"].AsColor(rows.evenColor.dark));

            rows.oddColor = new ThemeColor(
                light["colorTwo"].AsColor(rows.oddColor.light),
                dark["colorTwo"].AsColor(rows.oddColor.dark));
        }

        private static void MigrateBreadcrumbs(YamlNode globalData, HierarchyDecoratorSettings settings)
        {
            if (globalData.IsEmpty)
            {
                return;
            }

            TreeLineSettings lines = settings.TreeLines;

            lines.enabled = globalData["showBreadcrumbs"].AsBool(true);

            YamlNode instance = globalData["instanceBreadcrumbs"];
            YamlNode fullDepth = globalData["fullDepthBreadcrumbs"];

            lines.showConnector = instance["displayHorizontal"].AsBool(true);
            lines.fullDepth = fullDepth["show"].AsBool(true);
            lines.style = ToLineStyle(instance["style"].AsInt(0));

            Color color = instance["color"].AsColor(new Color(0.5f, 0.5f, 0.5f, 1f));

            lines.opacity = Mathf.Clamp(color.a, 0.05f, 1f);
            color.a = 1f;
            lines.color = new ThemeColor(color, color);
        }

        private static void MigrateComponents(YamlNode components, HierarchyDecoratorSettings settings, Report report)
        {
            if (components.IsEmpty)
            {
                return;
            }

            ComponentIconSettings icons = settings.ComponentIcons;

            icons.enabled = components["enableIcons"].AsBool(true);
            icons.stackDuplicates = components["stackDuplicateIcons"].AsBool(false);
            icons.clickAction = components["clickToToggleComponent"].AsBool(true)
                ? ComponentClickAction.ToggleEnabled
                : ComponentClickAction.Select;

            settings.Indicators.showMissingScripts = components["showMissingScriptWarning"].AsBool(true);

            // DisplayMode is a bitmask: 1 = all built-in components, 2 = all custom scripts.
            int showAll = components["showAll"].AsInt(3);

            icons.mode = (showAll & 1) != 0 ? ComponentIconMode.All : ComponentIconMode.Selected;
            icons.includeCustomScripts = (showAll & 2) != 0;

            List<ComponentRule> rules = settings.ComponentRules;
            rules.Clear();

            CollectGroupRules(components["unityGroups"], rules, report, builtIn: true);
            CollectGroupRules(components["customGroups"], rules, report, builtIn: false);
            CollectComponentRules(components["allCustomComponents"]["components"], rules, report, builtIn: false);

            report.ComponentRules = rules.Count;

            if (report.ComponentRules > 0)
            {
                report.Notes.Add("Only components you had explicitly shown or excluded were imported; 1.x's " +
                                 "generated group structure is rebuilt by 2.0 and was not carried over.");
            }
        }

        private static void CollectGroupRules(YamlNode groups, List<ComponentRule> rules, Report report, bool builtIn)
        {
            IReadOnlyList<YamlNode> items = groups.Items;

            for (int i = 0; i < items.Count; i++)
            {
                CollectComponentRules(items[i]["components"], rules, report, builtIn);
            }
        }

        private static void CollectComponentRules(YamlNode components, List<ComponentRule> rules, Report report, bool builtIn)
        {
            IReadOnlyList<YamlNode> items = components.Items;

            for (int i = 0; i < items.Count; i++)
            {
                YamlNode component = items[i];

                bool shown = component["shown"].AsBool();
                bool excluded = component["excluded"].AsBool();

                if (!shown && !excluded)
                {
                    continue;
                }

                ComponentDisplay display = excluded ? ComponentDisplay.Hide : ComponentDisplay.Show;

                string scriptGuid = component["script"]["guid"].AsString(string.Empty);

                if (!string.IsNullOrEmpty(scriptGuid))
                {
                    rules.Add(new ComponentRule(null, null, scriptGuid, display));
                    continue;
                }

                // Built-ins were keyed by assembly-qualified name, which is not stable across Unity versions;
                // resolve it now and store the namespace-qualified name instead.
                string assemblyQualifiedName = component["name"].AsString(string.Empty);

                if (string.IsNullOrEmpty(assemblyQualifiedName))
                {
                    report.UnresolvedComponents++;
                    continue;
                }

                Type type = Type.GetType(assemblyQualifiedName, throwOnError: false);

                if (type != null)
                {
                    rules.Add(ComponentCatalog.CreateRule(type, display));
                    continue;
                }

                string typeName = assemblyQualifiedName.Split(',')[0].Trim();

                if (string.IsNullOrEmpty(typeName))
                {
                    report.UnresolvedComponents++;
                    continue;
                }

                rules.Add(new ComponentRule(typeName, null, null, display));
            }
        }

        private static HeaderAlignment ToAlignment(int textAnchor)
        {
            // UnityEngine.TextAnchor: rows of three, left/center/right.
            switch (textAnchor % 3)
            {
                case 1:
                    return HeaderAlignment.Center;

                case 2:
                    return HeaderAlignment.Right;

                default:
                    return HeaderAlignment.Left;
            }
        }

        private static bool IsBold(int fontStyle)
        {
            // UnityEngine.FontStyle: Normal, Bold, Italic, BoldAndItalic.
            return fontStyle == 1 || fontStyle == 3;
        }

        private static HeaderTextCase ToTextCase(int textFormatting)
        {
            switch (textFormatting)
            {
                case 0:
                    return HeaderTextCase.Upper;

                case 1:
                    return HeaderTextCase.Lower;

                default:
                    return HeaderTextCase.AsTyped;
            }
        }

        private static LineStyle ToLineStyle(int breadcrumbStyle)
        {
            switch (breadcrumbStyle)
            {
                case 1:
                    return LineStyle.Dashed;

                case 2:
                    return LineStyle.Dotted;

                default:
                    return LineStyle.Solid;
            }
        }

        private static string ToProjectRelativePath(string absolutePath)
        {
            string normalised = absolutePath.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');

            return normalised.StartsWith(dataPath, StringComparison.Ordinal)
                ? "Assets" + normalised.Substring(dataPath.Length)
                : normalised;
        }

        private static string ToAbsolutePath(string projectRelativePath)
        {
            if (Path.IsPathRooted(projectRelativePath))
            {
                return projectRelativePath;
            }

            string dataPath = Application.dataPath.Replace('\\', '/');
            return dataPath.Substring(0, dataPath.Length - "Assets".Length) + projectRelativePath;
        }
    }
}
