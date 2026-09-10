using NUnit.Framework;
using UnityEngine;

namespace HierarchyDecorator.Tests
{
    public sealed class MiniYamlTests
    {
        [Test]
        public void ParsesNestedMappings()
        {
            YamlNode root = MiniYaml.Parse(
                "MonoBehaviour:\n" +
                "  globalData:\n" +
                "    showBreadcrumbs: 1\n" +
                "    instanceBreadcrumbs:\n" +
                "      show: 0\n" +
                "      style: 2\n");

            Assert.IsTrue(root["MonoBehaviour"]["globalData"]["showBreadcrumbs"].AsBool());
            Assert.IsFalse(root["MonoBehaviour"]["globalData"]["instanceBreadcrumbs"]["show"].AsBool(true));
            Assert.AreEqual(2, root["MonoBehaviour"]["globalData"]["instanceBreadcrumbs"]["style"].AsInt());
        }

        [Test]
        public void ParsesBlockSequencesWrittenAtTheKeyIndent()
        {
            // This is how Unity writes lists: the dash shares the key's indentation.
            YamlNode root = MiniYaml.Parse(
                "styleData:\n" +
                "  styles:\n" +
                "  - prefix: =\n" +
                "    fontSize: 11\n" +
                "  - prefix: +\n" +
                "    fontSize: 10\n");

            var styles = root["styleData"]["styles"].Items;

            Assert.AreEqual(2, styles.Count);
            Assert.AreEqual("=", styles[0]["prefix"].AsString());
            Assert.AreEqual(11, styles[0]["fontSize"].AsInt());
            Assert.AreEqual("+", styles[1]["prefix"].AsString());
            Assert.AreEqual(10, styles[1]["fontSize"].AsInt());
        }

        [Test]
        public void ParsesFlowMapsAsColors()
        {
            YamlNode root = MiniYaml.Parse("color: {r: 0.5, g: 0.25, b: 1, a: 0.8}\n");
            Color color = root["color"].AsColor(Color.black);

            Assert.AreEqual(0.5f, color.r, 0.0001f);
            Assert.AreEqual(0.25f, color.g, 0.0001f);
            Assert.AreEqual(1f, color.b, 0.0001f);
            Assert.AreEqual(0.8f, color.a, 0.0001f);
        }

        [Test]
        public void UnquotesScalars()
        {
            // Unity writes a header prefix of "-" as '-'; carrying the quotes through produced a rule that
            // could never match. This is a regression test for that.
            YamlNode root = MiniYaml.Parse("a: '-'\nb: \"x\"\nc: 'it''s'\nd: plain\n");

            Assert.AreEqual("-", root["a"].AsString());
            Assert.AreEqual("x", root["b"].AsString());
            Assert.AreEqual("it's", root["c"].AsString());
            Assert.AreEqual("plain", root["d"].AsString());
        }

        [Test]
        public void SkipsDocumentMarkersAndDirectives()
        {
            YamlNode root = MiniYaml.Parse(
                "%YAML 1.1\n" +
                "%TAG !u! tag:unity3d.com,2011:\n" +
                "--- !u!114 &11400000\n" +
                "MonoBehaviour:\n" +
                "  m_Name: Settings\n");

            Assert.AreEqual("Settings", root["MonoBehaviour"]["m_Name"].AsString());
        }

        [Test]
        public void MissingKeysReturnEmptyNodesInsteadOfThrowing()
        {
            YamlNode root = MiniYaml.Parse("a: 1\n");

            Assert.IsTrue(root["nope"].IsEmpty);
            Assert.IsTrue(root["nope"]["deeper"].IsEmpty);
            Assert.AreEqual(0, root["nope"].Items.Count);
            Assert.AreEqual(7, root["nope"].AsInt(7));
            Assert.AreEqual("fallback", root["nope"].AsString("fallback"));
        }

        [Test]
        public void EmptyInputIsHandled()
        {
            Assert.IsTrue(MiniYaml.Parse(null).IsEmpty);
            Assert.IsTrue(MiniYaml.Parse(string.Empty).IsEmpty);
        }
    }
}
