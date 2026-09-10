using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// Loads our stylesheets into <c>HierarchyView.StyleContainer</c>.
    ///
    /// Two non-obvious rules, both verified against Unity's own implementation:
    /// * <c>StyleContainer</c> is destroyed and recreated by <c>HierarchyView.Reset()</c>, which runs on every
    ///   source-hierarchy change (scene load, prefab stage, handler registration). The reference must never
    ///   be cached - the sheets are re-added from every <c>BindView</c>.
    /// * Editor USS has no theme selector. Unity ships a <c>_dark</c>/<c>_light</c> pair holding only a
    ///   <c>:root</c> variable block and picks one with <c>isProSkin</c>; we do the same.
    /// </summary>
    internal static class StyleInjector
    {
        internal static void OnBindView(HierarchyWindow window, HierarchyView view)
        {
            VisualElement container = view?.StyleContainer;

            if (container == null)
            {
                return;
            }

            // Scope marker: the built-in .hierarchy-item__* rules live in the editor-wide stylesheets and
            // HierarchyView is reused by other windows, so every selector we ship is namespaced under this.
            if (!container.ClassListContains(PackageInfo.RootClass))
            {
                container.AddToClassList(PackageInfo.RootClass);
            }

            // Theme variables first, rules second - insertion order is application order.
            AddSheet(container, EditorGUIUtility.isProSkin ? PackageInfo.StyleSheetDark : PackageInfo.StyleSheetLight);
            AddSheet(container, PackageInfo.StyleSheetBase);

            // A view is (re)bound whenever its source hierarchy is replaced - including a prefab-stage
            // reload, whose own PrefabStage.prefabStageReloaded event is internal and cannot be subscribed
            // to. Every EntityId in the cache may now belong to a different object, so it is dropped. The
            // cost is one bind per visible row.
            DecorationCache.Clear();
        }

        internal static void OnUnbindView(HierarchyWindow window, HierarchyView view)
        {
            DecoratorHost.OnUnbindView(view);
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
    }
}
