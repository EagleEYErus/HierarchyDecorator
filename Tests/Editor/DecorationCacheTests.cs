using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HierarchyDecorator.Tests
{
    /// <summary>
    /// The cache is the whole performance story, so its filtering and invalidation are tested directly rather
    /// than through the render path.
    /// </summary>
    public sealed class DecorationCacheTests
    {
        private readonly List<GameObject> m_Created = new List<GameObject>();

        private GameObject New(string name)
        {
            GameObject gameObject = new GameObject(name);
            m_Created.Add(gameObject);
            return gameObject;
        }

        [SetUp]
        public void SetUp()
        {
            DecorationCache.Clear();
            NameMatcher.ClearRegexCache();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < m_Created.Count; i++)
            {
                if (m_Created[i] != null)
                {
                    Object.DestroyImmediate(m_Created[i]);
                }
            }

            m_Created.Clear();
            DecorationCache.Clear();
        }

        private static void ResetRules(HierarchyDecoratorSettings settings)
        {
            DefaultSettings.PopulateHeaderRules(settings.HeaderRules);
            settings.ComponentRules.Clear();
            settings.MarkChangedWithoutSave();
        }

        [Test]
        public void NameFacet_ResolvesTheHeaderRuleAndStripsThePrefix()
        {
            using SettingsScope scope = new SettingsScope();
            ResetRules(scope.Settings);

            GameObject gameObject = New("= player rig");
            RowData data = DecorationCache.GetOrCreate(gameObject.GetEntityId());

            DecorationCache.Ensure(data, gameObject, scope.Settings, CacheFacet.Name);

            Assert.IsTrue(data.HasHeader);
            Assert.AreEqual("PLAYER RIG", data.HeaderLabel, "The default '=' rule uppercases.");
            Assert.AreEqual(HeaderKind.Header, scope.Settings.HeaderRules[data.HeaderRuleIndex].kind);
        }

        [Test]
        public void NameFacet_SeparatorRuleWinsOverSubheader()
        {
            using SettingsScope scope = new SettingsScope();
            ResetRules(scope.Settings);

            GameObject gameObject = New("--- gameplay");
            RowData data = DecorationCache.GetOrCreate(gameObject.GetEntityId());

            DecorationCache.Ensure(data, gameObject, scope.Settings, CacheFacet.Name);

            Assert.IsTrue(data.HasHeader);
            Assert.AreEqual(HeaderKind.Separator, scope.Settings.HeaderRules[data.HeaderRuleIndex].kind);
        }

        [Test]
        public void ComponentFacet_HidesTransformByDefault()
        {
            using SettingsScope scope = new SettingsScope();
            ResetRules(scope.Settings);

            scope.Settings.ComponentIcons.enabled = true;
            scope.Settings.ComponentIcons.mode = ComponentIconMode.All;
            scope.Settings.ComponentIcons.hideTransform = true;
            scope.Settings.ComponentIcons.maxIconsPerRow = 0;
            scope.Settings.MarkChangedWithoutSave();

            GameObject gameObject = New("Thing");
            gameObject.AddComponent<AudioSource>();

            RowData data = DecorationCache.GetOrCreate(gameObject.GetEntityId());
            DecorationCache.Ensure(data, gameObject, scope.Settings, CacheFacet.Components);

            Assert.AreEqual(1, data.IconCount, "Transform must not take an icon slot.");
            Assert.AreEqual(typeof(AudioSource), data.Icons[0].Component.GetType());
        }

        [Test]
        public void ComponentFacet_RespectsTheIconLimitAndReportsOverflow()
        {
            using SettingsScope scope = new SettingsScope();
            ResetRules(scope.Settings);

            scope.Settings.ComponentIcons.enabled = true;
            scope.Settings.ComponentIcons.mode = ComponentIconMode.All;
            scope.Settings.ComponentIcons.hideTransform = true;
            scope.Settings.ComponentIcons.maxIconsPerRow = 2;
            scope.Settings.MarkChangedWithoutSave();

            GameObject gameObject = New("Busy");
            gameObject.AddComponent<AudioSource>();
            gameObject.AddComponent<BoxCollider>();
            gameObject.AddComponent<Rigidbody>();
            gameObject.AddComponent<Light>();

            RowData data = DecorationCache.GetOrCreate(gameObject.GetEntityId());
            DecorationCache.Ensure(data, gameObject, scope.Settings, CacheFacet.Components);

            Assert.AreEqual(2, data.IconCount);
            Assert.AreEqual(2, data.OverflowCount);
        }

        [Test]
        public void ComponentFacet_SelectedModeShowsOnlyExplicitRules()
        {
            using SettingsScope scope = new SettingsScope();
            ResetRules(scope.Settings);

            scope.Settings.ComponentIcons.enabled = true;
            scope.Settings.ComponentIcons.mode = ComponentIconMode.Selected;
            scope.Settings.ComponentIcons.maxIconsPerRow = 0;
            scope.Settings.ComponentRules.Add(ComponentCatalog.CreateRule(typeof(Light), ComponentDisplay.Show));
            scope.Settings.MarkChangedWithoutSave();

            GameObject gameObject = New("Lamp");
            gameObject.AddComponent<AudioSource>();
            gameObject.AddComponent<Light>();

            RowData data = DecorationCache.GetOrCreate(gameObject.GetEntityId());
            DecorationCache.Ensure(data, gameObject, scope.Settings, CacheFacet.Components);

            Assert.AreEqual(1, data.IconCount);
            Assert.AreEqual(typeof(Light), data.Icons[0].Component.GetType());
        }

        [Test]
        public void ComponentFacet_HideRuleWinsOverShowAll()
        {
            using SettingsScope scope = new SettingsScope();
            ResetRules(scope.Settings);

            scope.Settings.ComponentIcons.enabled = true;
            scope.Settings.ComponentIcons.mode = ComponentIconMode.All;
            scope.Settings.ComponentIcons.maxIconsPerRow = 0;
            scope.Settings.ComponentRules.Add(ComponentCatalog.CreateRule(typeof(AudioSource), ComponentDisplay.Hide));
            scope.Settings.MarkChangedWithoutSave();

            GameObject gameObject = New("Quiet");
            gameObject.AddComponent<AudioSource>();
            gameObject.AddComponent<Light>();

            RowData data = DecorationCache.GetOrCreate(gameObject.GetEntityId());
            DecorationCache.Ensure(data, gameObject, scope.Settings, CacheFacet.Components);

            Assert.AreEqual(1, data.IconCount);
            Assert.AreEqual(typeof(Light), data.Icons[0].Component.GetType());
        }

        [Test]
        public void ComponentFacet_StacksDuplicatesIntoOneEntry()
        {
            using SettingsScope scope = new SettingsScope();
            ResetRules(scope.Settings);

            scope.Settings.ComponentIcons.enabled = true;
            scope.Settings.ComponentIcons.mode = ComponentIconMode.All;
            scope.Settings.ComponentIcons.stackDuplicates = true;
            scope.Settings.ComponentIcons.maxIconsPerRow = 0;
            scope.Settings.MarkChangedWithoutSave();

            GameObject gameObject = New("Noisy");
            gameObject.AddComponent<AudioSource>();
            gameObject.AddComponent<AudioSource>();
            gameObject.AddComponent<AudioSource>();

            RowData data = DecorationCache.GetOrCreate(gameObject.GetEntityId());
            DecorationCache.Ensure(data, gameObject, scope.Settings, CacheFacet.Components);

            Assert.AreEqual(1, data.IconCount);
            Assert.AreEqual(3, data.Icons[0].StackCount);
        }

        [Test]
        public void Ensure_IsIdempotentUntilInvalidated()
        {
            using SettingsScope scope = new SettingsScope();
            ResetRules(scope.Settings);

            scope.Settings.ComponentIcons.enabled = true;
            scope.Settings.ComponentIcons.mode = ComponentIconMode.All;
            scope.Settings.ComponentIcons.maxIconsPerRow = 0;
            scope.Settings.MarkChangedWithoutSave();

            GameObject gameObject = New("Thing");
            gameObject.AddComponent<AudioSource>();

            RowData data = DecorationCache.GetOrCreate(gameObject.GetEntityId());
            DecorationCache.Ensure(data, gameObject, scope.Settings, CacheFacet.Components);
            Assert.AreEqual(1, data.IconCount);

            // A component added without an invalidation must not appear: this is what makes scrolling free.
            gameObject.AddComponent<Light>();
            DecorationCache.Ensure(data, gameObject, scope.Settings, CacheFacet.Components);
            Assert.AreEqual(1, data.IconCount);

            DecorationCache.Invalidate(gameObject.GetEntityId(), CacheFacet.Components);
            DecorationCache.Ensure(data, gameObject, scope.Settings, CacheFacet.Components);
            Assert.AreEqual(2, data.IconCount);
        }

        [Test]
        public void ChangingSettingsInvalidatesEveryFacet()
        {
            using SettingsScope scope = new SettingsScope();
            ResetRules(scope.Settings);

            GameObject gameObject = New("= player");
            RowData data = DecorationCache.GetOrCreate(gameObject.GetEntityId());

            DecorationCache.Ensure(data, gameObject, scope.Settings, CacheFacet.Name);
            Assert.IsTrue(data.HasHeader);

            scope.Settings.HeaderRules.Clear();
            scope.Settings.MarkChangedWithoutSave();

            DecorationCache.Ensure(data, gameObject, scope.Settings, CacheFacet.Name);
            Assert.IsFalse(data.HasHeader, "A settings revision bump must force a re-derive.");
        }

        [Test]
        public void EvictRemovesTheEntry()
        {
            using SettingsScope scope = new SettingsScope();
            ResetRules(scope.Settings);

            GameObject gameObject = New("Thing");
            EntityId id = gameObject.GetEntityId();

            DecorationCache.GetOrCreate(id);
            Assert.AreEqual(1, DecorationCache.Count);

            DecorationCache.Evict(id);
            Assert.AreEqual(0, DecorationCache.Count);
        }

        [Test]
        public void NullInputsAreIgnored()
        {
            using SettingsScope scope = new SettingsScope();

            RowData data = DecorationCache.GetOrCreate(default);

            Assert.DoesNotThrow(() => DecorationCache.Ensure(null, null, scope.Settings, CacheFacet.All));
            Assert.DoesNotThrow(() => DecorationCache.Ensure(data, null, scope.Settings, CacheFacet.All));
            GameObject temp = New("Temp");
            Assert.DoesNotThrow(() => DecorationCache.Ensure(data, temp, null, CacheFacet.All));
        }
    }
}
