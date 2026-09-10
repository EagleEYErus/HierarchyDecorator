using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator
{
    internal static class HierarchyDecoratorMenu
    {
        private const string DisableAllPath = PackageInfo.ToolsMenuPath + "Disable All Decorations";
        private const string SettingsPath = PackageInfo.ToolsMenuPath + "Settings...";
        private const string RecoverPath = PackageInfo.ToolsMenuPath + "Re-enable Failed Decorations";
        private const string ImportPath = PackageInfo.ToolsMenuPath + "Import Settings From 1.x";
        private const string ResetPath = PackageInfo.ToolsMenuPath + "Reset Settings To Defaults";

        [MenuItem(SettingsPath, priority = 0)]
        private static void OpenSettings()
        {
            SettingsService.OpenProjectSettings(PackageInfo.SettingsMenuPath);
        }

        /// <summary>
        /// The escape hatch. If a decoration ever makes the Hierarchy unusable, this turns everything off
        /// without uninstalling the package or editing settings.
        /// </summary>
        [MenuItem(DisableAllPath, priority = 20)]
        private static void ToggleDisableAll()
        {
            DecoratorHost.Suspend(!DecoratorHost.Suspended);
        }

        [MenuItem(DisableAllPath, validate = true)]
        private static bool ValidateDisableAll()
        {
            Menu.SetChecked(DisableAllPath, DecoratorHost.Suspended);
            return true;
        }

        [MenuItem(RecoverPath, priority = 21)]
        private static void RecoverFailedDecorators()
        {
            DecoratorHost.ClearFailures();
        }

        [MenuItem(RecoverPath, validate = true)]
        private static bool ValidateRecoverFailedDecorators()
        {
            return DecoratorHost.FailedDecorators.Count > 0;
        }

        [MenuItem(ImportPath, priority = 40)]
        private static void ImportLegacySettings()
        {
            List<string> candidates = LegacyMigrator.FindLegacyAssets();

            if (candidates.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    PackageInfo.DisplayName,
                    "No HierarchyDecorator 1.x settings asset was found in this project.",
                    "OK");
                return;
            }

            string path = candidates[0];

            if (candidates.Count > 1)
            {
                path = EditorUtility.OpenFilePanel("Select a HierarchyDecorator 1.x settings asset", "Assets", "asset");

                if (string.IsNullOrEmpty(path))
                {
                    return;
                }
            }

            bool proceed = EditorUtility.DisplayDialog(
                PackageInfo.DisplayName,
                $"Import settings from:\n{path}\n\n" +
                "This replaces the current header rules and component rules. The 1.x asset itself is not modified.",
                "Import",
                "Cancel");

            if (!proceed)
            {
                return;
            }

            HierarchyDecoratorSettings settings = HierarchyDecoratorSettings.instance;

            if (LegacyMigrator.TryMigrate(path, settings, out LegacyMigrator.Report report))
            {
                settings.LegacyMigrationCompleted = true;
                settings.MarkChanged();
                DecoratorHost.RefreshAllLiveRows();
                HierarchyLog.Info(report.ToString());
                EditorUtility.DisplayDialog(PackageInfo.DisplayName, report.ToString(), "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    PackageInfo.DisplayName,
                    "The settings asset could not be read. See the Console for details.",
                    "OK");
            }
        }

        [MenuItem(ResetPath, priority = 41)]
        private static void ResetSettings()
        {
            bool proceed = EditorUtility.DisplayDialog(
                PackageInfo.DisplayName,
                "Reset all Hierarchy Decorator project settings to their defaults?\n\nThis cannot be undone.",
                "Reset",
                "Cancel");

            if (!proceed)
            {
                return;
            }

            HierarchyDecoratorSettings settings = HierarchyDecoratorSettings.instance;

            DefaultSettings.PopulateHeaderRules(settings.HeaderRules);
            settings.ComponentRules.Clear();
            settings.CustomPresets.Clear();

            BuiltInPresets.Find(BuiltInPresets.CleanName)?.ApplyTo(settings);

            settings.MarkChanged();
            NameMatcher.ClearRegexCache();
            ChangeTracker.ResetAll();
            DecoratorHost.RefreshAllLiveRows();
        }
    }
}
