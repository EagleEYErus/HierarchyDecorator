using System.Collections.Generic;
using NUnit.Framework;

namespace HierarchyDecorator.Tests
{
    /// <summary>
    /// The matching contract is load-bearing for backwards compatibility: an upgraded project keeps rendering
    /// only if these rules behave exactly as HierarchyDecorator 1.x did.
    /// </summary>
    public sealed class NameMatcherTests
    {
        private static HeaderRule Literal(string pattern, bool requireSpace = true)
        {
            return new HeaderRule
            {
                name = pattern,
                pattern = pattern,
                requireSpaceAfterPrefix = requireSpace,
                textCase = HeaderTextCase.AsTyped
            };
        }

        [SetUp]
        public void SetUp()
        {
            NameMatcher.ClearRegexCache();
        }

        [Test]
        public void LiteralPrefix_RequiresASingleSpace()
        {
            List<HeaderRule> rules = new List<HeaderRule> { Literal("=") };

            Assert.IsTrue(NameMatcher.FindRule("= PLAYER", rules).IsValid);
            Assert.IsFalse(NameMatcher.FindRule("=PLAYER", rules).IsValid, "No space after the prefix must not match.");
            Assert.IsFalse(NameMatcher.FindRule("=", rules).IsValid, "A bare prefix has nothing to show.");
            Assert.IsFalse(NameMatcher.FindRule("== PLAYER", rules).IsValid, "The character after the prefix must be a space.");
        }

        [Test]
        public void LiteralPrefix_WithoutSpaceRequirement_MatchesAnyStart()
        {
            List<HeaderRule> rules = new List<HeaderRule> { Literal("---", requireSpace: false) };

            Assert.IsTrue(NameMatcher.FindRule("---", rules).IsValid);
            Assert.IsTrue(NameMatcher.FindRule("--------", rules).IsValid);
            Assert.IsTrue(NameMatcher.FindRule("--- gameplay", rules).IsValid);
            Assert.IsFalse(NameMatcher.FindRule("-- gameplay", rules).IsValid);
        }

        [Test]
        public void LiteralPrefix_StripsPrefixAndTrims()
        {
            HeaderRule rule = Literal("=");

            Assert.IsTrue(NameMatcher.TryMatch("=   Player Rig  ", rule, out string label));
            Assert.AreEqual("Player Rig", label);
        }

        [Test]
        public void KeepPrefixInLabel_LeavesTheNameIntact()
        {
            HeaderRule rule = Literal("=");
            rule.keepPrefixInLabel = true;

            Assert.IsTrue(NameMatcher.TryMatch("= PLAYER", rule, out string label));
            Assert.AreEqual("= PLAYER", label);
        }

        [Test]
        public void TextCase_IsAppliedToTheLabel()
        {
            HeaderRule upper = Literal("=");
            upper.textCase = HeaderTextCase.Upper;
            Assert.IsTrue(NameMatcher.TryMatch("= player", upper, out string upperLabel));
            Assert.AreEqual("PLAYER", upperLabel);

            HeaderRule lower = Literal("=");
            lower.textCase = HeaderTextCase.Lower;
            Assert.IsTrue(NameMatcher.TryMatch("= PLAYER", lower, out string lowerLabel));
            Assert.AreEqual("player", lowerLabel);
        }

        [Test]
        public void FirstMatchingRuleWins_NoSpecificityOrdering()
        {
            List<HeaderRule> rules = new List<HeaderRule>
            {
                Literal("-"),
                Literal("--")
            };

            // "-" is listed first but cannot match "-- x", because the character after the prefix is not a
            // space - so the later, longer rule gets it.
            NameMatcher.Match strict = NameMatcher.FindRule("-- x", rules);
            Assert.IsTrue(strict.IsValid);
            Assert.AreEqual(1, strict.RuleIndex);

            // Drop the space requirement and the first rule shadows the longer one: list order decides, there
            // is no longest-prefix preference.
            rules[0].requireSpaceAfterPrefix = false;
            NameMatcher.Match loose = NameMatcher.FindRule("-- x", rules);

            Assert.IsTrue(loose.IsValid);
            Assert.AreEqual(0, loose.RuleIndex, "List order decides, not the longest prefix.");
        }

        [Test]
        public void DisabledRules_AreSkipped()
        {
            HeaderRule rule = Literal("=");
            rule.enabled = false;

            Assert.IsFalse(NameMatcher.FindRule("= PLAYER", new List<HeaderRule> { rule }).IsValid);
        }

        [Test]
        public void Regex_IsAnchoredAndUsesCaptureGroups()
        {
            HeaderRule rule = Literal("#");
            rule.useRegex = true;
            rule.pattern = @"#\s*(.+)";

            Assert.IsTrue(NameMatcher.TryMatch("# Enemies", rule, out string label));
            Assert.AreEqual("Enemies", label);

            Assert.IsFalse(NameMatcher.TryMatch("Enemies # tail", rule, out _), "The pattern is implicitly anchored with ^.");
        }

        [Test]
        public void Regex_WithoutCaptureGroup_StillMatches()
        {
            // 1.x silently ignored group-less patterns, which read as a bug rather than a feature.
            HeaderRule rule = Literal(">>");
            rule.useRegex = true;
            rule.pattern = ">>";

            Assert.IsTrue(NameMatcher.TryMatch(">>Section", rule, out string label));
            Assert.AreEqual("Section", label);
        }

        [Test]
        public void Regex_InvalidPattern_DoesNotThrow()
        {
            HeaderRule rule = Literal("(");
            rule.useRegex = true;
            rule.pattern = "([";

            Assert.DoesNotThrow(() => NameMatcher.TryMatch("anything", rule, out _));
            Assert.IsNotNull(NameMatcher.ValidateRegex("(["));
            Assert.IsNull(NameMatcher.ValidateRegex(@"\d+"));
        }

        [Test]
        public void EmptyInputs_AreHandled()
        {
            Assert.IsFalse(NameMatcher.FindRule(null, new List<HeaderRule> { Literal("=") }).IsValid);
            Assert.IsFalse(NameMatcher.FindRule("= A", null).IsValid);
            Assert.IsFalse(NameMatcher.TryMatch("= A", null, out _));
        }
    }
}
