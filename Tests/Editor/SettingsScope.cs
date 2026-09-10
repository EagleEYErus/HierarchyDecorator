using System;
using UnityEditor;

namespace HierarchyDecorator.Tests
{
    /// <summary>
    /// Snapshots the project settings singleton and restores it afterwards.
    ///
    /// The settings live in a <see cref="ScriptableSingleton{T}"/>, whose protected constructor claims the
    /// static instance - so a test must never construct its own. Tests therefore operate on the real
    /// singleton and put it back exactly as they found it, which keeps them from corrupting the settings of
    /// whatever project is running the suite.
    /// </summary>
    internal sealed class SettingsScope : IDisposable
    {
        private readonly string m_Snapshot;

        public HierarchyDecoratorSettings Settings { get; }

        public SettingsScope()
        {
            Settings = HierarchyDecoratorSettings.instance;
            m_Snapshot = EditorJsonUtility.ToJson(Settings);
        }

        public void Dispose()
        {
            EditorJsonUtility.FromJsonOverwrite(m_Snapshot, Settings);
            Settings.Persist();
            NameMatcher.ClearRegexCache();
        }
    }
}
