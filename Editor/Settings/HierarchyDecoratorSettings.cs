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

        private const double SaveDebounceSeconds = 0.5;

        [SerializeField] private int m_SchemaVersion = CurrentSchemaVersion;

        [SerializeField] private List<HeaderRule> m_HeaderRules = new List<HeaderRule>();
        [SerializeField] private List<ComponentRule> m_ComponentRules = new List<ComponentRule>();
        [SerializeField] private ComponentIconSettings m_ComponentIcons = new ComponentIconSettings();
        [SerializeField] private TreeLineSettings m_TreeLines = new TreeLineSettings();
        [SerializeField] private IndicatorSettings m_Indicators = new IndicatorSettings();
        [SerializeField] private RowSettings m_Rows = new RowSettings();
        [SerializeField] private List<Preset> m_CustomPresets = new List<Preset>();

        [SerializeField] private bool m_LegacyMigrationCompleted;

        /// <summary>
        /// Whether the shipped defaults have ever been written. Deliberately not inferred from an empty rule
        /// list: a team that does not use the "= " convention must be able to delete every rule and have it
        /// stay deleted.
        /// </summary>
        [SerializeField] private bool m_DefaultsSeeded;

        /// <summary>Bumped whenever anything a decorator reads changes. Cheap change detection for caches.</summary>
        [NonSerialized] private int m_Revision;

        /// <summary>Set by the migration chain; the actual disk write happens on the main thread.</summary>
        [NonSerialized] private bool m_PendingSave;

        /// <summary>Set when the file on disk was written by a newer version of the package.</summary>
        [NonSerialized] private bool m_UnknownSchema;

        [NonSerialized] private bool m_SaveScheduled;
        [NonSerialized] private double m_SaveDeadline;

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
            if (m_UnknownSchema)
            {
                HierarchyLog.Once(
                    "schema-newer",
                    $"{HierarchyDecoratorSettings.FilePath} was written by a newer version of Hierarchy " +
                    $"Decorator (schema {m_SchemaVersion}, this build understands {CurrentSchemaVersion}). " +
                    "It is being read as best it can and will not be rewritten; update the package to edit it.");
                return;
            }

            if (!m_DefaultsSeeded && m_SchemaVersion == CurrentSchemaVersion)
            {
                DefaultSettings.PopulateHeaderRules(m_HeaderRules);

                // Also apply the preset the user settings claim is active, so a fresh install actually looks
                // like what the Preset dropdown says it is.
                BuiltInPresets.Find(BuiltInPresets.CleanName)?.ApplyTo(this);

                m_DefaultsSeeded = true;
                m_PendingSave = true;
            }

            if (m_PendingSave)
            {
                m_PendingSave = false;
                EditorApplication.delayCall += Persist;
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

        /// <summary>
        /// Signal a change and debounce the disk write. Used by the settings UI: a two-second slider drag
        /// changes the value once per frame, and a delayCall would fire on the very next tick, so the file
        /// was being rewritten in full a hundred times for one gesture.
        /// </summary>
        public void MarkChangedDeferred()
        {
            unchecked { m_Revision++; }

            m_SaveDeadline = EditorApplication.timeSinceStartup + SaveDebounceSeconds;

            if (m_SaveScheduled)
            {
                return;
            }

            m_SaveScheduled = true;
            EditorApplication.update += TickDeferredSave;
        }

        private void TickDeferredSave()
        {
            if (EditorApplication.timeSinceStartup < m_SaveDeadline)
            {
                return;
            }

            FlushPendingSave();
        }

        /// <summary>Writes a debounced change immediately. Called before a domain reload and on quit.</summary>
        public void FlushPendingSave()
        {
            if (!m_SaveScheduled)
            {
                return;
            }

            m_SaveScheduled = false;
            EditorApplication.update -= TickDeferredSave;
            Persist();
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

            if (m_SchemaVersion > CurrentSchemaVersion)
            {
                // Written by a newer package. Unity's deserializer has already dropped whatever fields this
                // build does not know; stamping the version down as well would tell the newer package that
                // the file is old and let it "migrate" the damage in. Leave it alone and report it.
                m_UnknownSchema = true;
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
        internal const int CurrentSchemaVersion = 1;

        [SerializeField] private int m_SchemaVersion = CurrentSchemaVersion;
        [SerializeField] private bool m_Enabled = true;
        [SerializeField] private string m_ActivePreset = BuiltInPresets.CleanName;
        [SerializeField] private bool m_ShowLegacyHierarchyHint = true;

        [NonSerialized] private int m_Revision;

        public int Revision => m_Revision;

        public int SchemaVersion => m_SchemaVersion;

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
