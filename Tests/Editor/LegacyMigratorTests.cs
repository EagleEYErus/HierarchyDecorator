using System.IO;
using NUnit.Framework;

namespace HierarchyDecorator.Tests
{
    /// <summary>
    /// Migration is read-only against a text file, which makes it fully testable without a 1.x install.
    /// The fixture below is shaped exactly like a real HierarchyDecorator 1.x settings asset, including the
    /// quoted "-" prefix that Unity writes.
    /// </summary>
    public sealed class LegacyMigratorTests
    {
        private const string LegacyAsset = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_Script: {fileID: 11500000, guid: 00668fd727de9bb4081a8a202ce24c3b, type: 3}
  m_Name: Settings
  m_EditorClassIdentifier: Wooshii.HierarchyDecorator.Editor::HierarchyDecorator.Settings
  globalData:
    showActiveToggles: 1
    showBreadcrumbs: 1
    instanceBreadcrumbs:
      show: 1
      color: {r: 0.4, g: 0.4, b: 0.4, a: 0.5}
      style: 1
      displayHorizontal: 0
    fullDepthBreadcrumbs:
      show: 0
      color: {r: 0.5, g: 0.5, b: 0.5, a: 1}
      style: 0
      displayHorizontal: 0
  styleData:
    displayTags: 1
    displayLayers: 1
    displayIcons: 0
    styles:
    - prefix: =
      noSpaceAfterPrefix: 0
      isRegex: 0
      name: Header (Centered)
      fontSize: 13
      fontAlignment: 4
      fontStyle: 1
      textFormatting: 0
      modes:
      - fontColour: {r: 0.1, g: 0.1, b: 0.1, a: 1}
        backgroundColour: {r: 0.6, g: 0.6, b: 0.6, a: 1}
      - fontColour: {r: 1, g: 1, b: 1, a: 1}
        backgroundColour: {r: 0.2, g: 0.2, b: 0.2, a: 1}
    - prefix: '-'
      noSpaceAfterPrefix: 1
      isRegex: 0
      name: Subheader
      fontSize: 10
      fontAlignment: 3
      fontStyle: 0
      textFormatting: 2
      modes:
      - fontColour: {r: 0.2, g: 0.2, b: 0.2, a: 1}
        backgroundColour: {r: 0.8, g: 0.8, b: 0.8, a: 1}
      - fontColour: {r: 0.9, g: 0.9, b: 0.9, a: 1}
        backgroundColour: {r: 0.25, g: 0.25, b: 0.25, a: 1}
    twoToneBackground: 1
    lightMode:
      colorOne: {r: 0.81, g: 0.81, b: 0.81, a: 1}
      colorTwo: {r: 0.77, g: 0.77, b: 0.77, a: 1}
    darkMode:
      colorOne: {r: 0.24, g: 0.24, b: 0.24, a: 1}
      colorTwo: {r: 0.21, g: 0.21, b: 0.21, a: 1}
  components:
    enableIcons: 1
    clickToToggleComponent: 0
    showMissingScriptWarning: 0
    showAll: 1
    stackDuplicateIcons: 1
    unityGroups:
    - name: Audio
      components:
      - displayName: AudioSource
        name: UnityEngine.AudioSource, UnityEngine.AudioModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
        shown: 1
        excluded: 0
        isBuiltIn: 1
        script: {fileID: 0}
      - displayName: AudioListener
        name: UnityEngine.AudioListener, UnityEngine.AudioModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
        shown: 0
        excluded: 1
        isBuiltIn: 1
        script: {fileID: 0}
      - displayName: AudioReverbZone
        name: UnityEngine.AudioReverbZone, UnityEngine.AudioModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
        shown: 0
        excluded: 0
        isBuiltIn: 1
        script: {fileID: 0}
    customGroups:
    - name: Gameplay
      components:
      - displayName: PlayerController
        name: Game.PlayerController, Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
        shown: 1
        excluded: 0
        isBuiltIn: 0
        script: {fileID: 11500000, guid: abcdef0123456789abcdef0123456789, type: 3}
    allCustomComponents:
      name: All
      components: []
";

        private string m_Path;

        [SetUp]
        public void SetUp()
        {
            m_Path = Path.Combine(Path.GetTempPath(), "hd2-legacy-fixture.asset");
            File.WriteAllText(m_Path, LegacyAsset);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(m_Path))
            {
                File.Delete(m_Path);
            }
        }

        [Test]
        public void MigratesHeaderStyles()
        {
            using SettingsScope scope = new SettingsScope();

            Assert.IsTrue(LegacyMigrator.TryMigrate(m_Path, scope.Settings, out LegacyMigrator.Report report));

            // Two 1.x styles, plus the 2.0 separator rule that has no 1.x equivalent.
            Assert.AreEqual(3, scope.Settings.HeaderRules.Count);
            Assert.AreEqual(3, report.HeaderRules);

            HeaderRule separator = scope.Settings.HeaderRules[0];
            Assert.AreEqual(HeaderKind.Separator, separator.kind, "The separator must be first so '---' never falls through to '-'.");

            HeaderRule header = scope.Settings.HeaderRules[1];
            Assert.AreEqual("Header (Centered)", header.name);
            Assert.AreEqual("=", header.pattern);
            Assert.IsTrue(header.requireSpaceAfterPrefix);
            Assert.AreEqual(HeaderAlignment.Center, header.alignment);
            Assert.AreEqual(HeaderTextCase.Upper, header.textCase);
            Assert.IsTrue(header.bold);
            Assert.AreEqual(13, header.fontSize);
            Assert.AreEqual(1f, header.textColor.dark.r, 0.001f);
            Assert.AreEqual(0.6f, header.backgroundColor.light.r, 0.001f);
        }

        [Test]
        public void MigratesQuotedPrefixAndInvertedSpaceFlag()
        {
            using SettingsScope scope = new SettingsScope();

            Assert.IsTrue(LegacyMigrator.TryMigrate(m_Path, scope.Settings, out _));

            HeaderRule subheader = scope.Settings.HeaderRules[2];

            Assert.AreEqual("-", subheader.pattern, "Unity quotes a bare '-'; the quotes must not survive.");
            Assert.IsFalse(subheader.requireSpaceAfterPrefix, "noSpaceAfterPrefix: 1 inverts to requireSpaceAfterPrefix: false.");
            Assert.AreEqual(HeaderAlignment.Left, subheader.alignment);
            Assert.AreEqual(HeaderTextCase.AsTyped, subheader.textCase);
            Assert.IsFalse(subheader.bold);
        }

        [Test]
        public void MigratesDisplayIconsOntoEachHeaderRule()
        {
            using SettingsScope scope = new SettingsScope();

            Assert.IsTrue(LegacyMigrator.TryMigrate(m_Path, scope.Settings, out _));

            // The fixture has displayIcons: 0, a global 1.x flag that only ever applied to styled rows.
            Assert.IsFalse(scope.Settings.HeaderRules[1].showComponentIcons);
            Assert.IsFalse(scope.Settings.HeaderRules[2].showComponentIcons);
        }

        [Test]
        public void MigratesRowColorsAndBreadcrumbs()
        {
            using SettingsScope scope = new SettingsScope();

            Assert.IsTrue(LegacyMigrator.TryMigrate(m_Path, scope.Settings, out _));

            RowSettings rows = scope.Settings.Rows;
            Assert.IsTrue(rows.overrideAlternatingColors);
            Assert.AreEqual(0.81f, rows.evenColor.light.r, 0.001f);
            Assert.AreEqual(0.21f, rows.oddColor.dark.r, 0.001f);

            TreeLineSettings lines = scope.Settings.TreeLines;
            Assert.IsTrue(lines.enabled);
            Assert.IsFalse(lines.showConnector, "displayHorizontal: 0 maps to no connector.");
            Assert.IsFalse(lines.fullDepth, "fullDepthBreadcrumbs.show: 0 maps to fullDepth: false.");
            Assert.AreEqual(LineStyle.Dashed, lines.style);
            Assert.AreEqual(0.5f, lines.opacity, 0.001f, "The 1.x colour alpha becomes the opacity.");
            Assert.AreEqual(1f, lines.color.dark.a, 0.001f, "The stored colour is opaque; opacity carries the alpha.");
        }

        [Test]
        public void MigratesComponentGlobals()
        {
            using SettingsScope scope = new SettingsScope();

            Assert.IsTrue(LegacyMigrator.TryMigrate(m_Path, scope.Settings, out _));

            ComponentIconSettings icons = scope.Settings.ComponentIcons;

            Assert.IsTrue(icons.enabled);
            Assert.IsTrue(icons.stackDuplicates);
            Assert.AreEqual(ComponentClickAction.Select, icons.clickAction, "clickToToggleComponent: 0 must not become ToggleEnabled.");
            Assert.IsFalse(scope.Settings.Indicators.showMissingScripts);

            // showAll is a bitmask: 1 = built-ins, 2 = custom scripts. The fixture has 1.
            Assert.AreEqual(ComponentIconMode.All, icons.mode);
            Assert.IsFalse(icons.includeCustomScripts);
        }

        [Test]
        public void MigratesOnlyExplicitComponentFlags()
        {
            using SettingsScope scope = new SettingsScope();

            Assert.IsTrue(LegacyMigrator.TryMigrate(m_Path, scope.Settings, out LegacyMigrator.Report report));

            // AudioReverbZone has neither shown nor excluded set and must not produce a rule.
            Assert.AreEqual(3, scope.Settings.ComponentRules.Count);
            Assert.AreEqual(3, report.ComponentRules);

            ComponentRule shown = scope.Settings.ComponentRules.Find(r => r.typeName == "UnityEngine.AudioSource");
            Assert.IsNotNull(shown, "A built-in flagged shown must survive, re-keyed from its assembly-qualified name.");
            Assert.AreEqual(ComponentDisplay.Show, shown.display);

            ComponentRule excluded = scope.Settings.ComponentRules.Find(r => r.typeName == "UnityEngine.AudioListener");
            Assert.IsNotNull(excluded);
            Assert.AreEqual(ComponentDisplay.Hide, excluded.display);

            ComponentRule custom = scope.Settings.ComponentRules.Find(r => r.monoScriptGuid == "abcdef0123456789abcdef0123456789");
            Assert.IsNotNull(custom, "A custom script must be keyed by its MonoScript GUID, not its type name.");
            Assert.AreEqual(ComponentDisplay.Show, custom.display);
        }

        [Test]
        public void MissingFileFailsGracefully()
        {
            using SettingsScope scope = new SettingsScope();

            Assert.IsFalse(LegacyMigrator.TryMigrate(Path.Combine(Path.GetTempPath(), "hd2-does-not-exist.asset"), scope.Settings, out _));
        }
    }
}
