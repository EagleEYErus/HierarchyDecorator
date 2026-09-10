using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HierarchyDecorator
{
    /// <summary>
    /// The single owner of every event subscription in the package.
    ///
    /// The Unity 6.6 hierarchy events are <b>static</b> and are cleared on every domain reload, so the rule
    /// is: one attribute drives subscription, <see cref="Install"/> is idempotent, and
    /// <see cref="Uninstall"/> is symmetric and runs before the domain goes away. That is what keeps a
    /// reload from producing duplicate callbacks.
    /// </summary>
    [InitializeOnLoad]
    internal static class HierarchyBootstrap
    {
        private static bool s_Installed;

        static HierarchyBootstrap()
        {
            // Subscribing has to happen here, not on delayCall: HierarchyWindow.BindView is raised from the
            // window's CreateGUI, which can run before the first delayCall of a domain. Missing it means the
            // stylesheets are never added to that view - the row callbacks would still fire, so the failure
            // shows up only as unstyled decorations.
            Install();

            // Work that needs other editor subsystems (settings files, TypeCache, open windows) is what gets
            // deferred instead.
            EditorApplication.delayCall += OnEditorReady;
        }

        internal static bool IsInstalled => s_Installed;

        internal static void Install()
        {
            if (s_Installed)
            {
                return;
            }

            s_Installed = true;

            HierarchyWindow.BindView += StyleInjector.OnBindView;
            HierarchyWindow.UnbindView += StyleInjector.OnUnbindView;
            HierarchyWindow.BindViewItem += DecoratorHost.OnBindViewItem;
            HierarchyWindow.UnbindViewItem += DecoratorHost.OnUnbindViewItem;
            HierarchyWindow.PopulateContextMenu += HierarchyContextMenu.OnPopulateContextMenu;
            HierarchyWindow.GetTooltip += HierarchyTooltips.OnGetTooltip;

            ObjectChangeEvents.changesPublished += ChangeTracker.OnChangesPublished;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Undo.undoRedoEvent += OnUndoRedo;

            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            PrefabStage.prefabStageOpened += OnPrefabStageChanged;
            PrefabStage.prefabStageClosing += OnPrefabStageChanged;

            AssemblyReloadEvents.beforeAssemblyReload += Uninstall;
        }

        private static void OnEditorReady()
        {
            LegacyMigrator.RunIfNeeded();
            LegacyHierarchyNotice.CheckOnce();
            BindOpenWindows();
        }

        /// <summary>
        /// Applies the view-level setup to Hierarchy windows that were already bound before this domain's
        /// subscription happened. BindView is a one-shot per view, so without this a window that survived the
        /// reload would keep running unstyled until the next scene or prefab-stage change.
        /// </summary>
        private static void BindOpenWindows()
        {
            HierarchyWindow[] windows = Resources.FindObjectsOfTypeAll<HierarchyWindow>();

            for (int i = 0; i < windows.Length; i++)
            {
                HierarchyWindow window = windows[i];

                if (window != null && window.View != null)
                {
                    StyleInjector.OnBindView(window, window.View);
                }
            }
        }

        internal static void Uninstall()
        {
            if (!s_Installed)
            {
                return;
            }

            s_Installed = false;

            HierarchyWindow.BindView -= StyleInjector.OnBindView;
            HierarchyWindow.UnbindView -= StyleInjector.OnUnbindView;
            HierarchyWindow.BindViewItem -= DecoratorHost.OnBindViewItem;
            HierarchyWindow.UnbindViewItem -= DecoratorHost.OnUnbindViewItem;
            HierarchyWindow.PopulateContextMenu -= HierarchyContextMenu.OnPopulateContextMenu;
            HierarchyWindow.GetTooltip -= HierarchyTooltips.OnGetTooltip;

            ObjectChangeEvents.changesPublished -= ChangeTracker.OnChangesPublished;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Undo.undoRedoEvent -= OnUndoRedo;

            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            PrefabStage.prefabStageOpened -= OnPrefabStageChanged;
            PrefabStage.prefabStageClosing -= OnPrefabStageChanged;

            AssemblyReloadEvents.beforeAssemblyReload -= Uninstall;

            DecoratorHost.Reset();
            ChangeTracker.ResetAll();
            LineTextures.Release();
        }

        /// <summary>
        /// Runs on every play-mode entry, including when Fast Enter Play Mode skipped the domain reload.
        /// It only resets state; subscription stays the responsibility of <see cref="Install"/>.
        /// </summary>
        [InitializeOnEnterPlayMode]
        private static void OnEnterPlayMode(EnterPlayModeOptions options)
        {
            ChangeTracker.ResetAll();
            Install();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            // EntityIds are reassigned across the play-mode boundary in both directions, so every cached key
            // is meaningless afterwards.
            if (change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.ExitingPlayMode)
            {
                ChangeTracker.ResetAll();
                DecoratorHost.Reset();
            }
        }

        private static void OnHierarchyChanged()
        {
            ChangeTracker.OnHierarchyChanged();
        }

        private static void OnUndoRedo(in UndoRedoInfo info)
        {
            // Settings live in a ScriptableSingleton, which is only written by an explicit Save(). An undo
            // changes memory, so the file has to be re-written or the next domain reload would resurrect the
            // undone value.
            HierarchyDecoratorSettings.instance.MarkChangedWithoutSave();
            HierarchyDecoratorSettings.instance.Persist();
            NameMatcher.ClearRegexCache();
            ChangeTracker.ResetAll();
            DecoratorHost.RefreshAllLiveRows();
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            ChangeTracker.ResetAll();
        }

        private static void OnSceneClosed(Scene scene)
        {
            ChangeTracker.ResetAll();
        }

        private static void OnPrefabStageChanged(PrefabStage stage)
        {
            ChangeTracker.ResetAll();
            DecoratorHost.Reset();
        }
    }

    /// <summary>
    /// One-time notice when the project is configured to use the legacy IMGUI Hierarchy window, where the
    /// Unity 6.6 extension API is never invoked and the plugin therefore renders nothing.
    /// </summary>
    internal static class LegacyHierarchyNotice
    {
        private const string LegacyWindowTypeName = "UnityEditor.SceneHierarchyWindow";

        internal static void CheckOnce()
        {
            if (!HierarchyDecoratorUserSettings.instance.ShowLegacyHierarchyHint)
            {
                return;
            }

            if (!IsUsingLegacyWindow())
            {
                return;
            }

            HierarchyLog.Once(
                "legacy-hierarchy",
                "This project is using the legacy Hierarchy window, which does not support the Unity 6.6 " +
                "hierarchy extension API, so no decorations will be drawn. Turn off " +
                "Project Settings > Editor > Hierarchy > Use Legacy Hierarchy to enable them. " +
                "You can silence this message in Preferences > Hierarchy Decorator.");
        }

        internal static bool IsUsingLegacyWindow()
        {
            bool legacyOpen = false;
            bool modernOpen = false;

            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();

            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow window = windows[i];

                if (window == null)
                {
                    continue;
                }

                if (window is HierarchyWindow)
                {
                    modernOpen = true;
                }
                else if (window.GetType().FullName == LegacyWindowTypeName)
                {
                    legacyOpen = true;
                }
            }

            return legacyOpen && !modernOpen;
        }
    }
}
