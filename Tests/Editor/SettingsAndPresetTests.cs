using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator.Tests
{
    public sealed class SettingsAndPresetTests
    {
        [Test]
        public void SettingsFilesLiveOutsideAssets()
        {
            StringAssert.StartsWith("ProjectSettings/", HierarchyDecoratorSettings.FilePath);
            StringAssert.StartsWith("UserSettings/", HierarchyDecoratorUserSettings.FilePath);
        }

        [Test]
        public void DefaultHeaderRulesAreShipped()
        {
            HeaderRule[] rules;

            using (SettingsScope scope = new SettingsScope())
            {
                System.Collections.Generic.List<HeaderRule> list = new System.Collections.Generic.List<HeaderRule>();
                DefaultSettings.PopulateHeaderRules(list);
                rules = list.ToArray();
            }

            Assert.AreEqual(4, rules.Length);
            Assert.AreEqual("---", rules[0].pattern);
            Assert.AreEqual(HeaderKind.Separator, rules[0].kind);
            Assert.AreEqual("=", rules[1].pattern);
            Assert.AreEqual("-", rules[2].pattern);
            Assert.AreEqual("+", rules[3].pattern);

            // The separator must not require a space, or "--------" would not match.
            Assert.IsFalse(rules[0].requireSpaceAfterPrefix);
            Assert.IsTrue(rules[1].requireSpaceAfterPrefix);
        }

        [Test]
        public void SettingsSurviveAJsonRoundTrip()
        {
            using SettingsScope scope = new SettingsScope();

            scope.Settings.HeaderRules.Clear();
            scope.Settings.HeaderRules.Add(new HeaderRule
            {
                name = "Round Trip",
                pattern = "@@",
                useRegex = true,
                alignment = HeaderAlignment.Right,
                textCase = HeaderTextCase.Lower,
                fontSize = 17,
                textColor = new ThemeColor(Color.red, Color.green),
                backgroundColor = new ThemeColor(Color.blue, Color.yellow)
            });

            scope.Settings.TreeLines.style = LineStyle.Dotted;
            scope.Settings.TreeLines.opacity = 0.66f;
            scope.Settings.ComponentIcons.maxIconsPerRow = 3;

            string json = EditorJsonUtility.ToJson(scope.Settings);

            scope.Settings.HeaderRules.Clear();
            scope.Settings.TreeLines.style = LineStyle.Solid;
            scope.Settings.ComponentIcons.maxIconsPerRow = 99;

            EditorJsonUtility.FromJsonOverwrite(json, scope.Settings);

            Assert.AreEqual(1, scope.Settings.HeaderRules.Count);

            HeaderRule rule = scope.Settings.HeaderRules[0];
            Assert.AreEqual("@@", rule.pattern);
            Assert.IsTrue(rule.useRegex);
            Assert.AreEqual(HeaderAlignment.Right, rule.alignment);
            Assert.AreEqual(HeaderTextCase.Lower, rule.textCase);
            Assert.AreEqual(17, rule.fontSize);
            Assert.AreEqual(Color.red, rule.textColor.light);
            Assert.AreEqual(Color.yellow, rule.backgroundColor.dark);

            Assert.AreEqual(LineStyle.Dotted, scope.Settings.TreeLines.style);
            Assert.AreEqual(0.66f, scope.Settings.TreeLines.opacity, 0.0001f);
            Assert.AreEqual(3, scope.Settings.ComponentIcons.maxIconsPerRow);
        }

        [Test]
        public void SchemaVersionIsCurrent()
        {
            Assert.AreEqual(HierarchyDecoratorSettings.CurrentSchemaVersion, HierarchyDecoratorSettings.instance.SchemaVersion);
        }

        [Test]
        public void MarkChangedBumpsTheRevision()
        {
            using SettingsScope scope = new SettingsScope();

            int before = scope.Settings.Revision;
            scope.Settings.MarkChangedWithoutSave();

            Assert.AreNotEqual(before, scope.Settings.Revision);
        }

        [Test]
        public void EveryBuiltInPresetExistsAndIsFindable()
        {
            string[] names =
            {
                BuiltInPresets.MinimalName,
                BuiltInPresets.CleanName,
                BuiltInPresets.DeveloperName,
                BuiltInPresets.DesignerName,
                BuiltInPresets.DebugName
            };

            Assert.AreEqual(names.Length, BuiltInPresets.All.Count);

            for (int i = 0; i < names.Length; i++)
            {
                Assert.IsNotNull(BuiltInPresets.Find(names[i]), names[i] + " must exist.");
                Assert.IsTrue(BuiltInPresets.IsBuiltIn(names[i]));
            }

            Assert.IsNull(BuiltInPresets.Find("Nope"));
            Assert.IsFalse(BuiltInPresets.IsBuiltIn("Nope"));
        }

        [Test]
        public void PresetApplyThenCaptureRoundTrips()
        {
            using SettingsScope scope = new SettingsScope();
            DefaultSettings.PopulateHeaderRules(scope.Settings.HeaderRules);

            Preset developer = BuiltInPresets.Find(BuiltInPresets.DeveloperName);
            developer.ApplyTo(scope.Settings);

            Preset captured = Preset.CaptureFrom(scope.Settings, "Captured");

            Assert.AreEqual(developer.treeLines, captured.treeLines);
            Assert.AreEqual(developer.treeLinesFullDepth, captured.treeLinesFullDepth);
            Assert.AreEqual(developer.treeLineStyle, captured.treeLineStyle);
            Assert.AreEqual(developer.treeLineOpacity, captured.treeLineOpacity, 0.0001f);
            Assert.AreEqual(developer.componentIcons, captured.componentIcons);
            Assert.AreEqual(developer.componentIconMode, captured.componentIconMode);
            Assert.AreEqual(developer.maxIconsPerRow, captured.maxIconsPerRow);
            Assert.AreEqual(developer.clickAction, captured.clickAction);
            Assert.AreEqual(developer.missingScriptIndicator, captured.missingScriptIndicator);
            Assert.AreEqual(developer.overrideRowColors, captured.overrideRowColors);
        }

        [Test]
        public void MinimalPresetTurnsComponentIconsOff()
        {
            using SettingsScope scope = new SettingsScope();
            DefaultSettings.PopulateHeaderRules(scope.Settings.HeaderRules);

            BuiltInPresets.Find(BuiltInPresets.MinimalName).ApplyTo(scope.Settings);

            Assert.IsFalse(scope.Settings.ComponentIcons.enabled);
            Assert.IsTrue(scope.Settings.TreeLines.enabled);
        }

        [Test]
        public void PresetsDoNotTouchTheHeaderOrComponentRuleContent()
        {
            using SettingsScope scope = new SettingsScope();

            DefaultSettings.PopulateHeaderRules(scope.Settings.HeaderRules);
            scope.Settings.ComponentRules.Clear();
            scope.Settings.ComponentRules.Add(ComponentCatalog.CreateRule(typeof(Light), ComponentDisplay.Show));

            int headerCount = scope.Settings.HeaderRules.Count;

            // A rule the user turned off must stay off: a preset applies a style, it does not restore content.
            scope.Settings.HeaderRules[1].enabled = false;

            BuiltInPresets.Find(BuiltInPresets.DebugName).ApplyTo(scope.Settings);

            Assert.AreEqual(headerCount, scope.Settings.HeaderRules.Count, "A preset must not add or remove header rules.");
            Assert.IsFalse(scope.Settings.HeaderRules[1].enabled, "A preset must not re-enable a rule the user disabled.");
            Assert.AreEqual(1, scope.Settings.ComponentRules.Count, "A preset must not touch component rules.");
        }

        [Test]
        public void PresetApplyToNullIsSafe()
        {
            Assert.DoesNotThrow(() => BuiltInPresets.Find(BuiltInPresets.CleanName).ApplyTo(null));
        }
    }

    public sealed class ComponentCatalogTests
    {
        [Test]
        public void CatalogContainsBuiltInComponents()
        {
            Assert.Greater(ComponentCatalog.AllComponentTypes.Count, 50);
            Assert.Contains(typeof(AudioSource), (System.Collections.ICollection)ComponentCatalog.AllComponentTypes);
        }

        [Test]
        public void BuiltInRuleRoundTrips()
        {
            ComponentRule rule = ComponentCatalog.CreateRule(typeof(AudioSource), ComponentDisplay.Show);

            Assert.IsNotNull(rule);
            Assert.AreEqual("UnityEngine.AudioSource", rule.typeName);
            Assert.IsFalse(string.IsNullOrEmpty(rule.assemblyName));
            Assert.IsTrue(string.IsNullOrEmpty(rule.monoScriptGuid), "Built-in components have no MonoScript.");
            Assert.AreEqual(typeof(AudioSource), ComponentCatalog.Resolve(rule));
        }

        [Test]
        public void UnknownTypeNameResolvesToNullInsteadOfThrowing()
        {
            ComponentRule rule = new ComponentRule("Nope.NotAType", "NotAnAssembly", null, ComponentDisplay.Show);

            Assert.IsNull(ComponentCatalog.Resolve(rule));
        }

        [Test]
        public void NullInputsAreSafe()
        {
            Assert.IsNull(ComponentCatalog.CreateRule(null, ComponentDisplay.Show));
            Assert.IsNull(ComponentCatalog.Resolve(null));
            Assert.AreEqual(string.Empty, ComponentCatalog.FindMonoScriptGuid(null));
        }

        [Test]
        public void UserScriptDetection()
        {
            Assert.IsFalse(ComponentCatalog.IsUserScript(typeof(Transform)));
            Assert.IsTrue(ComponentCatalog.IsUserScript(typeof(TestBehaviour)));
        }

        private sealed class TestBehaviour : MonoBehaviour
        {
        }
    }
}
