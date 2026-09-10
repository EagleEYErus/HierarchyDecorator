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

        public const string PackageRoot = "Packages/" + Name;
        public const string UiRoot = PackageRoot + "/Editor/UI";

        public const string StyleSheetBase = UiRoot + "/HierarchyDecorator.uss";
        public const string StyleSheetDark = UiRoot + "/HierarchyDecorator_dark.uss";
        public const string StyleSheetLight = UiRoot + "/HierarchyDecorator_light.uss";
        public const string SettingsStyleSheet = UiRoot + "/HierarchyDecoratorSettings.uss";

        /// <summary>
        /// Marker class added to <c>HierarchyView.StyleContainer</c>. Every selector we ship is scoped under
        /// it, because the built-in <c>.hierarchy-item__*</c> rules live in the editor-wide stylesheets and
        /// <c>HierarchyView</c> is reused by windows other than the Hierarchy.
        /// </summary>
        public const string RootClass = "hd-root";

        public const string LogPrefix = "[Hierarchy Decorator] ";
    }
}
