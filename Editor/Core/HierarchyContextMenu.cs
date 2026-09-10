using System.Collections.Generic;
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// Adds a small, self-contained submenu to the Hierarchy context menu.
    ///
    /// Every action goes through <see cref="Undo"/>, because they all rename real GameObjects - that is how
    /// headers are represented (see ARCHITECTURE.md D9).
    /// </summary>
    internal static class HierarchyContextMenu
    {
        private const string Root = "Hierarchy Decorator/";

        private static readonly List<GameObject> s_Targets = new List<GameObject>(8);

        internal static void OnPopulateContextMenu(HierarchyWindow window, HierarchyView view, HierarchyViewItem item, DropdownMenu menu)
        {
            if (menu == null)
            {
                return;
            }

            if (TryAppendSceneActions(item, menu))
            {
                return;
            }

            CollectTargets(item);

            if (s_Targets.Count == 0)
            {
                menu.AppendAction(Root + "Settings...", _ => SettingsService.OpenProjectSettings(PackageInfo.SettingsMenuPath), DropdownMenuAction.AlwaysEnabled, null);
                return;
            }

            List<HeaderRule> rules = HierarchyDecoratorSettings.instance.HeaderRules;

            for (int i = 0; i < rules.Count; i++)
            {
                HeaderRule rule = rules[i];

                if (rule == null || !rule.enabled || rule.useRegex)
                {
                    // A regex rule has no single literal prefix to apply, so it is not offered here.
                    continue;
                }

                string label = Root + "Convert To/" + SanitiseMenuLabel(rule.name);
                string prefix = rule.pattern;
                bool space = rule.requireSpaceAfterPrefix;

                menu.AppendAction(label, _ => ApplyPrefix(prefix, space), DropdownMenuAction.AlwaysEnabled, null);
            }

            menu.AppendAction(Root + "Clear Decoration", _ => ClearPrefix(), DropdownMenuAction.AlwaysEnabled, null);
            menu.AppendAction(Root + "Settings...", _ => SettingsService.OpenProjectSettings(PackageInfo.SettingsMenuPath), DropdownMenuAction.AlwaysEnabled, null);
        }

        /// <summary>
        /// Scene rows are not GameObjects, so the visual decorators skip them - but the context menu exists
        /// to add entries, and "which asset is this scene?" is a real question with no other answer in the
        /// Hierarchy. Returns true when the row was a scene and the menu is done.
        /// </summary>
        private static bool TryAppendSceneActions(HierarchyViewItem item, DropdownMenu menu)
        {
            if (item?.Handler is not HierarchySceneHandler handler || item.Node == HierarchyNode.Null)
            {
                return false;
            }

            Scene scene = handler.GetScene(item.Node);

            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                return false;
            }

            string path = scene.path;

            menu.AppendAction(Root + "Ping Scene Asset", _ => PingSceneAsset(path), DropdownMenuAction.AlwaysEnabled, null);
            menu.AppendAction(Root + "Settings...", _ => SettingsService.OpenProjectSettings(PackageInfo.SettingsMenuPath), DropdownMenuAction.AlwaysEnabled, null);
            return true;
        }

        private static void PingSceneAsset(string path)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);

            if (asset == null)
            {
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        /// <summary>
        /// Acts on the selection when the clicked row is part of it, and on the clicked row otherwise -
        /// which is how every other Hierarchy context action behaves.
        /// </summary>
        private static void CollectTargets(HierarchyViewItem item)
        {
            s_Targets.Clear();

            GameObject clicked = null;

            if (item != null && item.Node != HierarchyNode.Null)
            {
                HierarchyNodes.TryGetGameObject(item, out clicked);
            }

            GameObject[] selection = Selection.gameObjects;

            if (clicked != null && System.Array.IndexOf(selection, clicked) < 0)
            {
                s_Targets.Add(clicked);
                return;
            }

            for (int i = 0; i < selection.Length; i++)
            {
                if (selection[i] != null)
                {
                    s_Targets.Add(selection[i]);
                }
            }
        }

        private static void ApplyPrefix(string prefix, bool requireSpace)
        {
            if (s_Targets.Count == 0)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Convert To Header");
            int group = Undo.GetCurrentGroup();

            HierarchyDecoratorSettings settings = HierarchyDecoratorSettings.instance;

            for (int i = 0; i < s_Targets.Count; i++)
            {
                GameObject target = s_Targets[i];

                if (target == null)
                {
                    continue;
                }

                string bare = StripAnyPrefix(target.name, settings);
                string next = requireSpace ? prefix + " " + bare : prefix + bare;

                if (target.name == next)
                {
                    continue;
                }

                Undo.RecordObject(target, "Convert To Header");
                target.name = next;
            }

            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
        }

        private static void ClearPrefix()
        {
            if (s_Targets.Count == 0)
            {
                return;
            }

            HierarchyDecoratorSettings settings = HierarchyDecoratorSettings.instance;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Clear Decoration");
            int group = Undo.GetCurrentGroup();

            for (int i = 0; i < s_Targets.Count; i++)
            {
                GameObject target = s_Targets[i];

                if (target == null)
                {
                    continue;
                }

                string bare = StripAnyPrefix(target.name, settings);

                if (bare == target.name || string.IsNullOrEmpty(bare))
                {
                    continue;
                }

                Undo.RecordObject(target, "Clear Decoration");
                target.name = bare;
            }

            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
        }

        /// <summary>
        /// Removes whichever rule prefix currently matches, so converting between styles is lossless.
        /// Goes through the same stripping path the renderer uses, so the result always matches the label
        /// the user was looking at.
        /// </summary>
        private static string StripAnyPrefix(string name, HierarchyDecoratorSettings settings)
        {
            NameMatcher.Match match = NameMatcher.FindRule(name, settings.HeaderRules);

            if (!match.IsValid)
            {
                return name;
            }

            HeaderRule rule = settings.HeaderRules[match.RuleIndex];

            if (!NameMatcher.TryStripPrefix(name, rule, out string stripped) || string.IsNullOrEmpty(stripped))
            {
                return name;
            }

            return stripped;
        }

        private static string SanitiseMenuLabel(string label)
        {
            // '/' would create an unintended submenu level.
            return string.IsNullOrEmpty(label) ? "Header" : label.Replace('/', '⁄');
        }
    }
}
