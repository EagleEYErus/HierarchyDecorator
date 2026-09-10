using System.Collections.Generic;
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// Loads our stylesheets into <c>HierarchyView.StyleContainer</c>.
    ///
    /// Three non-obvious rules, all verified against Unity's own implementation:
    /// * <c>StyleContainer</c> is destroyed and recreated by <c>HierarchyView.Reset()</c>, which runs on every
    ///   source-hierarchy change (scene load, prefab stage, handler registration). The reference must never
    ///   be cached - the sheets are re-added from every <c>BindView</c>.
    /// * Editor USS has no theme selector. Unity ships a <c>_dark</c>/<c>_light</c> pair holding only a
    ///   <c>:root</c> variable block and picks one with <c>isProSkin</c>; we do the same.
    /// * That choice is made once, at bind time. Switching the Editor theme does not necessarily rebuild the
    ///   Hierarchy window, so the bound views are tracked and re-styled in place when the skin changes -
    ///   otherwise the dark variables would stay applied on a light editor until the window was reopened.
    /// </summary>
    internal static class StyleInjector
    {
        private static readonly List<HierarchyView> s_Views = new List<HierarchyView>(2);

        private static bool s_LastProSkin;
        private static bool s_SkinKnown;

        internal static void OnBindView(HierarchyWindow window, HierarchyView view)
        {
            VisualElement container = view?.StyleContainer;

            if (container == null)
            {
                return;
            }

            if (!s_Views.Contains(view))
            {
                s_Views.Add(view);
            }

            // Scope marker: the built-in .hierarchy-item__* rules live in the editor-wide stylesheets and
            // HierarchyView is reused by other windows, so every selector we ship is namespaced under this.
            if (!container.ClassListContains(PackageInfo.RootClass))
            {
                container.AddToClassList(PackageInfo.RootClass);
            }

            s_LastProSkin = EditorGUIUtility.isProSkin;
            s_SkinKnown = true;

            ApplySheets(container, s_LastProSkin);

            // A view is (re)bound whenever its source hierarchy is replaced - including a prefab-stage
            // reload, whose own PrefabStage.prefabStageReloaded event is internal and cannot be subscribed
            // to. Every EntityId in the cache may now belong to a different object, so it is dropped. The
            // cost is one bind per visible row.
            DecorationCache.Clear();
        }

        internal static void OnUnbindView(HierarchyWindow window, HierarchyView view)
        {
            s_Views.Remove(view);
            DecoratorHost.OnUnbindView(view);
        }

        /// <summary>
        /// Swaps the theme stylesheet on every bound view when the editor skin changes. Called from the row
        /// bind path, where the current skin is already being read - the check is one bool compare.
        /// </summary>
        internal static void EnsureSkin(bool isProSkin)
        {
            if (s_SkinKnown && isProSkin == s_LastProSkin)
            {
                return;
            }

            s_LastProSkin = isProSkin;
            s_SkinKnown = true;

            for (int i = s_Views.Count - 1; i >= 0; i--)
            {
                VisualElement container = s_Views[i]?.StyleContainer;

                if (container == null)
                {
                    s_Views.RemoveAt(i);
                    continue;
                }

                ApplySheets(container, isProSkin);
            }

            // Component icons are resolved per skin as well, and the row data caches them.
            DecorationCache.Clear();
        }

        internal static void Reset()
        {
            s_Views.Clear();
            s_SkinKnown = false;
        }

        private static void ApplySheets(VisualElement container, bool isProSkin)
        {
            RemoveSheet(container, isProSkin ? PackageInfo.StyleSheetLight : PackageInfo.StyleSheetDark);

            // Theme variables first, rules second - insertion order is application order.
            AddSheet(container, isProSkin ? PackageInfo.StyleSheetDark : PackageInfo.StyleSheetLight);
            AddSheet(container, PackageInfo.StyleSheetBase);
        }

        private static void AddSheet(VisualElement element, string assetPath)
        {
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(assetPath);

            if (sheet == null)
            {
                // Unity's own LoadStyleSheet warns and then adds null anyway; do not copy that.
                HierarchyLog.Once("uss:" + assetPath, $"Could not load stylesheet '{assetPath}'. Decorations will fall back to inline styles.");
                return;
            }

            if (!element.styleSheets.Contains(sheet))
            {
                element.styleSheets.Add(sheet);
            }
        }

        private static void RemoveSheet(VisualElement element, string assetPath)
        {
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(assetPath);

            if (sheet != null && element.styleSheets.Contains(sheet))
            {
                element.styleSheets.Remove(sheet);
            }
        }
    }
}
