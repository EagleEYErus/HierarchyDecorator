namespace HierarchyDecorator
{
    /// <summary>
    /// Every package-identity string lives here so that renaming the package is a one-file change.
    /// </summary>
    public static class PackageInfo
    {
        public const string Name = "com.wooshii.hierarchydecorator";
        public const string DisplayName = "Hierarchy Decorator";
        public const string Version = "2.0.0";

        public const string ProjectSettingsPath = "ProjectSettings/Packages/" + Name + "/Settings.asset";
        public const string UserSettingsPath = "UserSettings/Packages/" + Name + "/UserSettings.asset";

        public const string SettingsMenuPath = "Project/Hierarchy Decorator";
        public const string PreferencesMenuPath = "Preferences/Hierarchy Decorator";
        public const string ToolsMenuPath = "Tools/Hierarchy Decorator/";

        public const string DefaultPackageRoot = "Packages/" + Name;

        public static string StyleSheetBase => PackagePaths.Ui("HierarchyDecorator.uss");
        public static string StyleSheetDark => PackagePaths.Ui("HierarchyDecorator_dark.uss");
        public static string StyleSheetLight => PackagePaths.Ui("HierarchyDecorator_light.uss");
        public static string SettingsStyleSheet => PackagePaths.Ui("HierarchyDecoratorSettings.uss");

        /// <summary>
        /// Marker class added to <c>HierarchyView.StyleContainer</c>. Every selector we ship is scoped under
        /// it, because the built-in <c>.hierarchy-item__*</c> rules live in the editor-wide stylesheets and
        /// <c>HierarchyView</c> is reused by windows other than the Hierarchy.
        /// </summary>
        public const string RootClass = "hd-root";

        public const string LogPrefix = "[Hierarchy Decorator] ";
    }
}
