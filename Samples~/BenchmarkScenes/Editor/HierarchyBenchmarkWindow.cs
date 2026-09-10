using System;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace HierarchyDecorator.Samples
{
    /// <summary>
    /// Generates the scenes PERFORMANCE.md's methodology is written against, and times scene construction.
    ///
    /// It deliberately does NOT report a "decoration cost" number of its own: the only honest way to measure
    /// the bind path is the Unity Profiler with the HierarchyDecorator.* markers, because that is what
    /// accounts for UI Toolkit's own layout and repaint work. This window's job is to give you a reproducible
    /// scene to point the Profiler at.
    /// </summary>
    public sealed class HierarchyBenchmarkWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Hierarchy Decorator/Benchmark Scene Generator";

        private int m_ObjectCount = 1000;
        private int m_ChildrenPerNode = 5;
        private bool m_AddComponents = true;
        private bool m_AddHeaders = true;
        private bool m_AddMissingScripts;

        private Label m_Result;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            HierarchyBenchmarkWindow window = GetWindow<HierarchyBenchmarkWindow>();
            window.titleContent = new GUIContent("HD Benchmark");
            window.minSize = new Vector2(340f, 300f);
            window.Show();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;

            Label heading = new Label("Hierarchy Decorator - Benchmark Scenes");
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginBottom = 6f;
            root.Add(heading);

            Label hint = new Label(
                "Generates a scene of the requested size in a new, unsaved scene. " +
                "Measure with the Unity Profiler (Editor mode) and the HierarchyDecorator.* markers - " +
                "see PERFORMANCE.md for the full procedure.");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.opacity = 0.75f;
            hint.style.marginBottom = 10f;
            root.Add(hint);

            IntegerField count = new IntegerField("GameObjects") { value = m_ObjectCount };
            count.RegisterValueChangedCallback(e => m_ObjectCount = Mathf.Clamp(e.newValue, 1, 200000));
            root.Add(count);

            SliderInt children = new SliderInt("Children per node", 1, 20) { value = m_ChildrenPerNode };
            children.RegisterValueChangedCallback(e => m_ChildrenPerNode = e.newValue);
            root.Add(children);

            Toggle components = new Toggle("Add components") { value = m_AddComponents };
            components.RegisterValueChangedCallback(e => m_AddComponents = e.newValue);
            root.Add(components);

            Toggle headers = new Toggle("Add header rows") { value = m_AddHeaders };
            headers.RegisterValueChangedCallback(e => m_AddHeaders = e.newValue);
            root.Add(headers);

            Toggle missing = new Toggle("Add missing scripts") { value = m_AddMissingScripts };
            missing.tooltip = "Adds GameObjects that reference a script asset that does not exist, to exercise the missing-script indicator.";
            missing.RegisterValueChangedCallback(e => m_AddMissingScripts = e.newValue);
            root.Add(missing);

            VisualElement buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 10f } };
            buttons.Add(new Button(() => Generate(100)) { text = "100" });
            buttons.Add(new Button(() => Generate(1000)) { text = "1 000" });
            buttons.Add(new Button(() => Generate(10000)) { text = "10 000" });
            buttons.Add(new Button(() => Generate(m_ObjectCount)) { text = "Custom" });
            root.Add(buttons);

            m_Result = new Label { style = { whiteSpace = WhiteSpace.Normal, marginTop = 10f } };
            root.Add(m_Result);
        }

        private void Generate(int count)
        {
            if (!EditorUtility.DisplayDialog(
                    "Hierarchy Decorator",
                    $"Create a new scene with {count} GameObjects?\n\nThe current scene will be closed. Unsaved changes are lost.",
                    "Create",
                    "Cancel"))
            {
                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            int created = Build(count);

            stopwatch.Stop();
            EditorSceneManager.MarkSceneDirty(scene);

            StringBuilder report = new StringBuilder();
            report.AppendLine($"Created {created} GameObjects in {stopwatch.ElapsedMilliseconds} ms.");
            report.AppendLine("Scene construction time only - it is not a measure of decoration cost.");
            report.Append("Open the Profiler in Editor mode, record while scrolling the Hierarchy, and read the HierarchyDecorator.* markers.");

            string text = report.ToString();

            if (m_Result != null)
            {
                m_Result.text = text;
            }

            Debug.Log("[Hierarchy Decorator] " + text);
        }

        private int Build(int count)
        {
            int created = 0;
            int index = 0;

            Transform current = null;
            int depthRemaining = 0;

            while (created < count)
            {
                if (m_AddHeaders && index % 50 == 0)
                {
                    GameObject header = new GameObject(index % 100 == 0 ? "= SECTION " + index : "--- block " + index);
                    header.transform.SetParent(null);
                    current = null;
                    depthRemaining = 0;
                    created++;
                    index++;
                    continue;
                }

                GameObject gameObject = new GameObject("Object_" + index);
                gameObject.transform.SetParent(current);

                if (m_AddComponents)
                {
                    AddComponents(gameObject, index);
                }

                if (m_AddMissingScripts && index % 97 == 0)
                {
                    // A MonoBehaviour whose script asset is absent cannot be created directly; the closest
                    // reproducible stand-in is a GameObject the user can break by deleting a script.
                    gameObject.name += " (add a script and delete it to test missing-script detection)";
                }

                created++;
                index++;

                if (depthRemaining > 0)
                {
                    depthRemaining--;
                }
                else
                {
                    current = index % m_ChildrenPerNode == 0 ? null : gameObject.transform;
                    depthRemaining = m_ChildrenPerNode;
                }
            }

            return created;
        }

        private static void AddComponents(GameObject gameObject, int index)
        {
            switch (index % 6)
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

                default:
                    break;
            }
        }
    }
}
