using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HierarchyDecorator.Tests
{
    /// <summary>
    /// Measures the part of Hierarchy Decorator that is actually expensive: filling the decoration cache for
    /// a row. Everything else on the bind path is style writes onto pooled elements.
    ///
    /// <para>
    /// Marked <see cref="ExplicitAttribute"/> so it never runs as part of the normal suite - it builds a
    /// 10 000 object scene and its numbers depend on the machine. Run it deliberately:
    /// </para>
    /// <code>
    /// Unity -batchmode -nographics -projectPath &lt;project&gt; -runTests -testPlatform EditMode \
    ///       -testFilter "HierarchyDecorator.Tests.PerformanceBenchmarks" -testResults results.xml -logFile -
    /// </code>
    /// <para>
    /// It reports cold fill (the scan) and warm fill (the early-out that every repaint and every scroll-back
    /// takes) separately, because the whole design rests on the second number being negligible. It does not
    /// measure UI Toolkit's own layout and repaint cost - for that, see the Profiler markers listed in
    /// PERFORMANCE.md.
    /// </para>
    /// </summary>
    [Explicit("Builds scenes of up to 10,000 GameObjects; run deliberately, not as part of the suite.")]
    public sealed class PerformanceBenchmarks
    {
        private readonly List<GameObject> m_Created = new List<GameObject>(16384);

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < m_Created.Count; i++)
            {
                if (m_Created[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_Created[i]);
                }
            }

            m_Created.Clear();
            DecorationCache.Clear();
            DecorationCache.InvalidateTypeInfo();
        }

        [Test]
        public void CacheFill_100() => Measure(100);

        [Test]
        public void CacheFill_1000() => Measure(1000);

        [Test]
        public void CacheFill_10000() => Measure(10000);

        private void Measure(int count)
        {
            using SettingsScope scope = new SettingsScope();
            HierarchyDecoratorSettings settings = scope.Settings;

            DefaultSettings.PopulateHeaderRules(settings.HeaderRules);
            settings.ComponentIcons.enabled = true;
            settings.ComponentIcons.mode = ComponentIconMode.All;
            settings.ComponentIcons.maxIconsPerRow = 8;
            settings.Indicators.showMissingScripts = true;
            settings.MarkChangedWithoutSave();

            Build(count);

            DecorationCache.Clear();
            DecorationCache.InvalidateTypeInfo();

            // Warm up the type table and JIT on a handful of rows so the cold number measures the scan and
            // not the first-ever call into AssetPreview.
            FillAll(Math.Min(count, 32), settings);
            DecorationCache.Clear();

            long before = GC.GetTotalMemory(true);

            Stopwatch cold = Stopwatch.StartNew();
            FillAll(count, settings);
            cold.Stop();

            long after = GC.GetTotalMemory(false);

            Stopwatch warm = Stopwatch.StartNew();
            FillAll(count, settings);
            warm.Stop();

            // A second pass over an unchanged cache must not re-scan: Ensure early-outs on the facet mask.
            Assert.Less(
                warm.Elapsed.TotalMilliseconds,
                Math.Max(cold.Elapsed.TotalMilliseconds * 0.25, 0.5),
                "A warm pass must be far cheaper than a cold one - if it is not, the cache is not being hit.");

            StringBuilder report = new StringBuilder();
            report.AppendLine($"[Hierarchy Decorator benchmark] {count} GameObjects");
            report.AppendLine($"  cold fill : {cold.Elapsed.TotalMilliseconds:0.00} ms total, {cold.Elapsed.TotalMilliseconds * 1000.0 / count:0.0} us/object");
            report.AppendLine($"  warm fill : {warm.Elapsed.TotalMilliseconds:0.00} ms total, {warm.Elapsed.TotalMilliseconds * 1000.0 / count:0.0} us/object");
            report.AppendLine($"  cache     : {DecorationCache.Count} entries, ~{(after - before) / 1024.0:0} KB managed");
            report.Append($"  editor    : Unity {Application.unityVersion}, {SystemInfo.processorType}");

            Debug.Log(report.ToString());
        }

        private void FillAll(int count, HierarchyDecoratorSettings settings)
        {
            for (int i = 0; i < count && i < m_Created.Count; i++)
            {
                GameObject gameObject = m_Created[i];
                RowData data = DecorationCache.GetOrCreate(gameObject.GetEntityId());
                DecorationCache.Ensure(data, gameObject, settings, CacheFacet.All);
            }
        }

        /// <summary>
        /// Shaped like a real scene rather than a flat list: mixed depth, a spread of component counts, and
        /// header-prefixed rows every fiftieth object.
        /// </summary>
        private void Build(int count)
        {
            Transform current = null;
            int depth = 0;

            for (int i = 0; i < count; i++)
            {
                bool header = i % 50 == 0;

                GameObject gameObject = new GameObject(header ? "= SECTION " + i : "Object_" + i);
                m_Created.Add(gameObject);

                if (header)
                {
                    current = null;
                    depth = 0;
                    continue;
                }

                gameObject.transform.SetParent(current);

                switch (i % 6)
                {
                    case 0:
                        gameObject.AddComponent<BoxCollider>();
                        gameObject.AddComponent<Rigidbody>();
                        break;

                    case 1:
                        gameObject.AddComponent<Light>();
                        break;

                    case 2:
                        gameObject.AddComponent<AudioSource>();
                        gameObject.AddComponent<SphereCollider>();
                        break;

                    case 3:
                        gameObject.AddComponent<MeshFilter>();
                        gameObject.AddComponent<MeshRenderer>();
                        break;

                    case 4:
                        gameObject.AddComponent<Animator>();
                        gameObject.AddComponent<CapsuleCollider>();
                        gameObject.AddComponent<AudioSource>();
                        break;
                }

                if (depth < 6 && i % 3 == 0)
                {
                    current = gameObject.transform;
                    depth++;
                }
                else if (i % 11 == 0)
                {
                    current = null;
                    depth = 0;
                }
            }
        }
    }
}
