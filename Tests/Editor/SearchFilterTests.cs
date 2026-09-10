using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HierarchyDecorator.Tests
{
    /// <summary>
    /// The search filters run for every GameObject in the scene when their token is used, so their contract
    /// is worth pinning down: cheap, name-only, and never throwing on a null or undecorated object.
    /// </summary>
    public sealed class SearchFilterTests
    {
        private readonly List<GameObject> m_Created = new List<GameObject>();

        private GameObject New(string name)
        {
            GameObject gameObject = new GameObject(name);
            m_Created.Add(gameObject);
            return gameObject;
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
        }

        [Test]
        public void DecorationKind_ClassifiesRows()
        {
            using SettingsScope scope = new SettingsScope();
            DefaultSettings.PopulateHeaderRules(scope.Settings.HeaderRules);
            NameMatcher.ClearRegexCache();

            Assert.AreEqual("header", HierarchyDecoratorSearchFilters.DecorationKind(New("= PLAYER")));
            Assert.AreEqual("separator", HierarchyDecoratorSearchFilters.DecorationKind(New("--- gameplay")));
            Assert.AreEqual("none", HierarchyDecoratorSearchFilters.DecorationKind(New("Player")));
        }

        [Test]
        public void DecorationRule_ReturnsTheMatchedRuleName()
        {
            using SettingsScope scope = new SettingsScope();
            DefaultSettings.PopulateHeaderRules(scope.Settings.HeaderRules);
            NameMatcher.ClearRegexCache();

            Assert.AreEqual("Subheader", HierarchyDecoratorSearchFilters.DecorationRule(New("- Audio")));
            Assert.AreEqual(string.Empty, HierarchyDecoratorSearchFilters.DecorationRule(New("Audio")));
        }

        [Test]
        public void NullGameObjectIsSafe()
        {
            Assert.AreEqual("none", HierarchyDecoratorSearchFilters.DecorationKind(null));
            Assert.AreEqual(string.Empty, HierarchyDecoratorSearchFilters.DecorationRule(null));
        }
    }
}
