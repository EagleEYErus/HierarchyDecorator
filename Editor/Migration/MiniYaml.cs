using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HierarchyDecorator
{
    /// <summary>
    /// A deliberately small reader for the subset of YAML that Unity writes into <c>.asset</c> files:
    /// indentation-based block mappings, block sequences and inline flow maps such as
    /// <c>{r: 1, g: 0, b: 0, a: 1}</c>.
    ///
    /// Migration reads the 1.x settings file as text rather than deserialising it, because the 1.x classes no
    /// longer exist in a 2.0 project - and even when they do, 1.x wipes some flags in <c>OnEnable</c> on a
    /// Unity version mismatch, so the file on disk is the more faithful source.
    /// </summary>
    internal sealed class YamlNode
    {
        public string Scalar;
        public Dictionary<string, YamlNode> Map;
        public List<YamlNode> Seq;

        public static readonly YamlNode Empty = new YamlNode();

        public bool IsEmpty => Scalar == null && Map == null && Seq == null;

        public YamlNode this[string key]
        {
            get
            {
                if (Map != null && Map.TryGetValue(key, out YamlNode node))
                {
                    return node;
                }

                return Empty;
            }
        }

        public IReadOnlyList<YamlNode> Items => (IReadOnlyList<YamlNode>)Seq ?? Array.Empty<YamlNode>();

        public bool Has(string key) => Map != null && Map.ContainsKey(key);

        public string AsString(string fallback = null)
        {
            return string.IsNullOrEmpty(Scalar) ? fallback : Scalar;
        }

        public int AsInt(int fallback = 0)
        {
            return int.TryParse(Scalar, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
        }

        public float AsFloat(float fallback = 0f)
        {
            return float.TryParse(Scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
        }

        public bool AsBool(bool fallback = false)
        {
            if (string.IsNullOrEmpty(Scalar))
            {
                return fallback;
            }

            if (Scalar == "1" || string.Equals(Scalar, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (Scalar == "0" || string.Equals(Scalar, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return fallback;
        }

        public Color AsColor(Color fallback)
        {
            if (Map == null)
            {
                return fallback;
            }

            return new Color(
                this["r"].AsFloat(fallback.r),
                this["g"].AsFloat(fallback.g),
                this["b"].AsFloat(fallback.b),
                this["a"].AsFloat(fallback.a));
        }
    }

    internal static class MiniYaml
    {
        private struct Line
        {
            public int Indent;
            public string Text;
        }

        public static YamlNode Parse(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return YamlNode.Empty;
            }

            List<Line> lines = Tokenize(content);
            int index = 0;

            if (lines.Count == 0)
            {
                return YamlNode.Empty;
            }

            return ParseBlock(lines, ref index, lines[0].Indent);
        }

        private static List<Line> Tokenize(string content)
        {
            string[] raw = content.Split('\n');
            List<Line> lines = new List<Line>(raw.Length);

            for (int i = 0; i < raw.Length; i++)
            {
                string line = raw[i].TrimEnd('\r');

                if (line.Length == 0)
                {
                    continue;
                }

                string trimmed = line.TrimStart(' ');

                if (trimmed.Length == 0 || trimmed[0] == '#' || trimmed[0] == '%')
                {
                    continue;
                }

                // Document markers and Unity's tag lines carry no data we need.
                if (trimmed.StartsWith("---", StringComparison.Ordinal))
                {
                    continue;
                }

                lines.Add(new Line
                {
                    Indent = line.Length - trimmed.Length,
                    Text = trimmed
                });
            }

            return lines;
        }

        private static YamlNode ParseBlock(List<Line> lines, ref int index, int indent)
        {
            if (index >= lines.Count || lines[index].Indent < indent)
            {
                return YamlNode.Empty;
            }

            return lines[index].Text.StartsWith("- ", StringComparison.Ordinal) || lines[index].Text == "-"
                ? ParseSequence(lines, ref index, indent)
                : ParseMapping(lines, ref index, indent);
        }

        private static YamlNode ParseSequence(List<Line> lines, ref int index, int indent)
        {
            YamlNode node = new YamlNode { Seq = new List<YamlNode>() };

            while (index < lines.Count && lines[index].Indent == indent &&
                   (lines[index].Text.StartsWith("- ", StringComparison.Ordinal) || lines[index].Text == "-"))
            {
                string rest = lines[index].Text.Length > 1 ? lines[index].Text.Substring(2) : string.Empty;

                if (rest.Length == 0)
                {
                    index++;
                    node.Seq.Add(YamlNode.Empty);
                    continue;
                }

                // An item's first line shares the dash's indent; rewriting it as a normal line two columns in
                // lets the block parser handle "- key: value" followed by deeper keys uniformly.
                lines[index] = new Line { Indent = indent + 2, Text = rest };
                node.Seq.Add(ParseBlock(lines, ref index, indent + 2));
            }

            return node;
        }

        private static YamlNode ParseMapping(List<Line> lines, ref int index, int indent)
        {
            YamlNode node = new YamlNode { Map = new Dictionary<string, YamlNode>(StringComparer.Ordinal) };

            while (index < lines.Count && lines[index].Indent == indent &&
                   !lines[index].Text.StartsWith("- ", StringComparison.Ordinal))
            {
                string text = lines[index].Text;
                int colon = text.IndexOf(':');

                if (colon < 0)
                {
                    index++;
                    continue;
                }

                string key = text.Substring(0, colon).Trim();
                string value = text.Substring(colon + 1).Trim();
                index++;

                if (value.Length > 0)
                {
                    node.Map[key] = ParseInline(value);
                    continue;
                }

                if (index < lines.Count && lines[index].Indent > indent)
                {
                    node.Map[key] = ParseBlock(lines, ref index, lines[index].Indent);
                }
                else if (index < lines.Count && lines[index].Indent == indent &&
                         (lines[index].Text.StartsWith("- ", StringComparison.Ordinal) || lines[index].Text == "-"))
                {
                    // Unity writes block sequences at the same indent as their key.
                    node.Map[key] = ParseSequence(lines, ref index, indent);
                }
                else
                {
                    node.Map[key] = YamlNode.Empty;
                }
            }

            return node;
        }

        private static YamlNode ParseInline(string value)
        {
            if (value.Length < 2 || value[0] != '{' || value[value.Length - 1] != '}')
            {
                return new YamlNode { Scalar = value };
            }

            YamlNode node = new YamlNode { Map = new Dictionary<string, YamlNode>(StringComparer.Ordinal) };
            string body = value.Substring(1, value.Length - 2);

            int depth = 0;
            int start = 0;

            for (int i = 0; i <= body.Length; i++)
            {
                if (i < body.Length)
                {
                    char c = body[i];

                    if (c == '{')
                    {
                        depth++;
                        continue;
                    }

                    if (c == '}')
                    {
                        depth--;
                        continue;
                    }

                    if (c != ',' || depth != 0)
                    {
                        continue;
                    }
                }

                string pair = body.Substring(start, i - start).Trim();
                start = i + 1;

                if (pair.Length == 0)
                {
                    continue;
                }

                int colon = pair.IndexOf(':');

                if (colon < 0)
                {
                    continue;
                }

                node.Map[pair.Substring(0, colon).Trim()] = ParseInline(pair.Substring(colon + 1).Trim());
            }

            return node;
        }
    }
}
