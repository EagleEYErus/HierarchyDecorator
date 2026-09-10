using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator
{
    /// <summary>
    /// Project-scoped configuration: the decoration ruleset a team shares through source control.
    /// Stored under ProjectSettings/ so that nothing is ever written into Assets/.
    /// </summary>
    [FilePath(PackageInfo.ProjectSettingsPath, FilePathAttribute.Location.ProjectFolder)]
    public sealed class HierarchyDecoratorSettings : ScriptableSingleton<HierarchyDecoratorSettings>, ISerializationCallbackReceiver
    {
        internal const int CurrentSchemaVersion = 1;

        [SerializeField] private int m_SchemaVersion = CurrentSchemaVersion;

        [SerializeField] private List<HeaderRule> m_HeaderRules = new List<HeaderRule>();
        [SerializeField] private List<ComponentRule> m_ComponentRules = new List<ComponentRule>();
        [SerializeField] private ComponentIconSettings m_ComponentIcons = new ComponentIconSettings();
        [SerializeField] private TreeLineSettings m_TreeLines = new TreeLineSettings();
        [SerializeField] private IndicatorSettings m_Indicators = new IndicatorSettings();
        [SerializeField] private RowSettings m_Rows = new RowSettings();
        [SerializeField] private List<Preset> m_CustomPresets = new List<Preset>();

        [SerializeField] private bool m_LegacyMigrationCompleted;

        /// <summary>Bumped whenever anything a decorator reads changes. Cheap change detection for caches.</summary>
        [NonSerialized] private int m_Revision;

        /// <summary>Set by the migration chain; the actual disk write happens on the main thread.</summary>
        [NonSerialized] private bool m_PendingSave;

        public List<HeaderRule> HeaderRules => m_HeaderRules;
        public List<ComponentRule> ComponentRules => m_ComponentRules;
        public ComponentIconSettings ComponentIcons => m_ComponentIcons;
        public TreeLineSettings TreeLines => m_TreeLines;
        public IndicatorSettings Indicators => m_Indicators;
        public RowSettings Rows => m_Rows;
        public List<Preset> CustomPresets => m_CustomPresets;

        public int SchemaVersion => m_SchemaVersion;
        public int Revision => m_Revision;

        public bool LegacyMigrationCompleted
        {
            get => m_LegacyMigrationCompleted;
            set => m_LegacyMigrationCompleted = value;
        }

        private void OnEnable()
        {
            if (m_HeaderRules.Count == 0 && m_SchemaVersion == CurrentSchemaVersion)
            {
                DefaultSettings.PopulateHeaderRules(m_HeaderRules);
            }

            if (m_PendingSave)
            {
                m_PendingSave = false;
                EditorApplication.delayCall += () => Persist();
            }
        }

        /// <summary>Signal that settings changed. Increments the revision and writes the file.</summary>
        public void MarkChanged()
        {
            unchecked { m_Revision++; }
            Persist();
        }

        /// <summary>Signal a change without writing to disk (e.g. while a slider is being dragged).</summary>
        public void MarkChangedWithoutSave()
        {
            unchecked { m_Revision++; }
        }

        public void Persist()
        {
            Save(true);
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            // Runs off the main thread for some object types: field shuffling only, no Unity API calls.
            if (m_SchemaVersion == CurrentSchemaVersion)
            {
                return;
            }

            // Migration chain: add a `case n:` per schema bump, falling through to the next.
            switch (m_SchemaVersion)
            {
                // case 1: MigrateV1ToV2(); goto case 2;
                default:
                    break;
            }

            m_SchemaVersion = CurrentSchemaVersion;
            m_PendingSave = true;
        }

        internal static string FilePath => GetFilePath();
    }

    /// <summary>
    /// Per-developer configuration. UserSettings/ is in Unity's standard .gitignore, so nothing here
    /// causes merge conflicts.
    /// </summary>
    [FilePath(PackageInfo.UserSettingsPath, FilePathAttribute.Location.ProjectFolder)]
    public sealed class HierarchyDecoratorUserSettings : ScriptableSingleton<HierarchyDecoratorUserSettings>
    {
        [SerializeField] private bool m_Enabled = true;
        [SerializeField] private string m_ActivePreset = BuiltInPresets.CleanName;
        [SerializeField] private bool m_ShowLegacyHierarchyHint = true;

        [NonSerialized] private int m_Revision;

        public int Revision => m_Revision;

        public bool Enabled
        {
            get => m_Enabled;
            set
            {
                if (m_Enabled == value)
                {
                    return;
                }

                m_Enabled = value;
                MarkChanged();
            }
        }

        public string ActivePreset
        {
            get => m_ActivePreset;
            set
            {
                if (m_ActivePreset == value)
                {
                    return;
                }

                m_ActivePreset = value;
                MarkChanged();
            }
        }

        public bool ShowLegacyHierarchyHint
        {
            get => m_ShowLegacyHierarchyHint;
            set
            {
                if (m_ShowLegacyHierarchyHint == value)
                {
                    return;
                }

                m_ShowLegacyHierarchyHint = value;
                MarkChanged();
            }
        }

        public void MarkChanged()
        {
            unchecked { m_Revision++; }
            Save(true);
        }

        public void Persist()
        {
            Save(true);
        }

        internal static string FilePath => GetFilePath();
    }
}
