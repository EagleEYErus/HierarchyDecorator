using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace HierarchyDecorator
{
    /// <summary>
    /// Matches GameObject names against <see cref="HeaderRule"/>s and derives the label to draw.
    ///
    /// The literal-prefix contract is deliberately identical to HierarchyDecorator 1.x
    /// (<c>name.StartsWith(prefix)</c>, and unless the rule opts out, exactly one space after the prefix)
    /// so that existing scenes keep rendering after an upgrade. Two intentional differences:
    ///
    /// * comparison is <see cref="StringComparison.Ordinal"/> instead of culture-sensitive - a header prefix
    ///   is a literal marker, not prose, and Ordinal is both correct and allocation-free;
    /// * a regex rule with no capture group now matches (1.x silently ignored it, which read as a bug).
    /// </summary>
    public static class NameMatcher
    {
        public readonly struct Match
        {
            public readonly int RuleIndex;
            public readonly string Label;

            public Match(int ruleIndex, string label)
            {
                RuleIndex = ruleIndex;
                Label = label;
            }

            public bool IsValid => RuleIndex >= 0;

            public static readonly Match None = new Match(-1, null);
        }

        private static readonly Dictionary<string, Regex> s_RegexCache = new Dictionary<string, Regex>(StringComparer.Ordinal);
        private static readonly StringBuilder s_Builder = new StringBuilder(64);

        /// <summary>Discards compiled patterns. Call when the rule list changes.</summary>
        public static void ClearRegexCache()
        {
            s_RegexCache.Clear();
        }

        /// <summary>
        /// Finds the first enabled rule matching <paramref name="name"/>. There is no specificity ordering:
        /// list order wins, exactly as in 1.x.
        /// </summary>
        public static Match FindRule(string name, IReadOnlyList<HeaderRule> rules)
        {
            if (string.IsNullOrEmpty(name) || rules == null)
            {
                return Match.None;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                HeaderRule rule = rules[i];

                if (rule == null || !rule.enabled || string.IsNullOrEmpty(rule.pattern))
                {
                    continue;
                }

                if (TryMatch(name, rule, out string label))
                {
                    return new Match(i, label);
                }
            }

            return Match.None;
        }

        public static bool TryMatch(string name, HeaderRule rule, out string label)
        {
            label = null;

            if (string.IsNullOrEmpty(name) || rule == null || string.IsNullOrEmpty(rule.pattern))
            {
                return false;
            }

            return rule.useRegex
                ? TryMatchRegex(name, rule, out label)
                : TryMatchLiteral(name, rule, out label);
        }

        /// <summary>
        /// Removes the prefix a rule matches, preserving the original case. Returns false when the rule does
        /// not match.
        ///
        /// This is the single stripping implementation. 1.x had two that disagreed - the drawn label used
        /// <c>Substring(len).Trim()</c> while the width measurement used <c>Substring(len + 1)</c> with no
        /// trim - so a header could be measured at one width and drawn at another.
        /// </summary>
        public static bool TryStripPrefix(string name, HeaderRule rule, out string stripped)
        {
            stripped = null;

            if (string.IsNullOrEmpty(name) || rule == null || string.IsNullOrEmpty(rule.pattern))
            {
                return false;
            }

            return rule.useRegex
                ? TryStripRegex(name, rule, out stripped)
                : TryStripLiteral(name, rule, out stripped);
        }

        private static bool TryMatchLiteral(string name, HeaderRule rule, out string label)
        {
            label = null;

            if (!TryStripLiteral(name, rule, out string stripped))
            {
                return false;
            }

            label = ApplyCase(rule.keepPrefixInLabel ? name : stripped, rule.textCase);
            return true;
        }

        private static bool TryStripLiteral(string name, HeaderRule rule, out string stripped)
        {
            stripped = null;

            string prefix = rule.pattern;

            if (!name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            if (rule.requireSpaceAfterPrefix)
            {
                // A bare prefix with nothing after it is not a header - there would be nothing to show.
                if (name.Length == prefix.Length || name[prefix.Length] != ' ')
                {
                    return false;
                }
            }

            stripped = name.Substring(prefix.Length).Trim();
            return true;
        }

        private static bool TryMatchRegex(string name, HeaderRule rule, out string label)
        {
            label = null;

            if (!TryStripRegex(name, rule, out string stripped))
            {
                return false;
            }

            label = ApplyCase(rule.keepPrefixInLabel ? name : stripped, rule.textCase);
            return true;
        }

        private static bool TryStripRegex(string name, HeaderRule rule, out string stripped)
        {
            stripped = null;

            Regex regex = GetRegex(rule.pattern);

            if (regex == null)
            {
                return false;
            }

            System.Text.RegularExpressions.Match match;

            try
            {
                match = regex.Match(name);
            }
            catch (RegexMatchTimeoutException)
            {
                // Poison the entry: a catastrophically backtracking pattern would otherwise burn the 50 ms
                // timeout again for every row, every re-derive, forever. Same policy as the decorator
                // failure isolation in ARCHITECTURE.md D8.
                s_RegexCache[rule.pattern] = null;

                HierarchyLog.Once(
                    "regex-timeout:" + rule.pattern,
                    $"The header regex '{rule.pattern}' timed out and has been disabled for this session. " +
                    "Simplify the pattern - it is backtracking catastrophically.");

                return false;
            }

            if (!match.Success)
            {
                return false;
            }

            if (match.Groups.Count > 1)
            {
                s_Builder.Clear();

                for (int i = 1; i < match.Groups.Count; i++)
                {
                    s_Builder.Append(match.Groups[i].Value);
                }

                stripped = s_Builder.ToString().Trim();
            }
            else
            {
                stripped = name.Substring(match.Length).Trim();
            }

            return true;
        }

        /// <summary>
        /// Compiles and caches a rule pattern. The pattern is implicitly anchored with '^', matching 1.x.
        /// An invalid pattern is cached as null so it is only reported once.
        /// </summary>
        private static Regex GetRegex(string pattern)
        {
            if (s_RegexCache.TryGetValue(pattern, out Regex cached))
            {
                return cached;
            }

            Regex regex = null;

            try
            {
                regex = new Regex(
                    "^" + pattern,
                    RegexOptions.Compiled | RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(50));
            }
            catch (ArgumentException)
            {
                // Deliberately silent. This runs while the user is still typing the pattern in the settings
                // window, so every intermediate keystroke would log. The settings page reports invalid
                // patterns through NameMatcher.ValidateRegex instead.
            }

            s_RegexCache[pattern] = regex;
            return regex;
        }

        /// <summary>Validates a pattern for the settings UI. Returns null when the pattern is usable.</summary>
        public static string ValidateRegex(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return "Pattern is empty.";
            }

            try
            {
                _ = new Regex("^" + pattern, RegexOptions.CultureInvariant);
                return null;
            }
            catch (ArgumentException e)
            {
                return e.Message;
            }
        }

        private static string ApplyCase(string text, HeaderTextCase textCase)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            switch (textCase)
            {
                case HeaderTextCase.Upper:
                    return text.ToUpperInvariant();

                case HeaderTextCase.Lower:
                    return text.ToLowerInvariant();

                default:
                    return text;
            }
        }
    }
}
