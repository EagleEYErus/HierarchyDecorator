using System;
using UnityEngine;

namespace HierarchyDecorator
{
    [Flags]
    internal enum CacheFacet
    {
        None = 0,

        /// <summary>Header rule match and the label derived from the object name.</summary>
        Name = 1 << 0,

        /// <summary>Component icon slice and missing-script count.</summary>
        Components = 1 << 1,

        All = Name | Components
    }

    internal struct ComponentIconEntry
    {
        public Component Component;
        public Texture2D Icon;
        public string DisplayName;
        public int StackCount;
    }

    /// <summary>
    /// Everything expensive that a single hierarchy row needs, computed once and reused until an
    /// <see cref="UnityEditor.ObjectChangeEvents"/> event says otherwise.
    /// </summary>
    internal sealed class RowData
    {
        public CacheFacet Valid;
        public int SettingsRevision = -1;

        // --- Name facet
        public string Name;
        public int HeaderRuleIndex = -1;
        public string HeaderLabel;

        // --- Components facet
        public ComponentIconEntry[] Icons = Array.Empty<ComponentIconEntry>();
        public int IconCount;
        public int OverflowCount;
        public int MissingScriptCount;

        public bool HasHeader => HeaderRuleIndex >= 0;

        public void InvalidateAll()
        {
            Valid = CacheFacet.None;
        }

        public void Invalidate(CacheFacet facets)
        {
            Valid &= ~facets;
        }

        public void EnsureIconCapacity(int capacity)
        {
            if (Icons.Length >= capacity)
            {
                return;
            }

            int size = Mathf.NextPowerOfTwo(Mathf.Max(capacity, 4));
            Array.Resize(ref Icons, size);
        }

        /// <summary>Drops Unity object references so a cached row cannot keep destroyed wrappers alive.</summary>
        public void ReleaseReferences()
        {
            for (int i = 0; i < IconCount; i++)
            {
                Icons[i].Component = null;
                Icons[i].Icon = null;
            }

            IconCount = 0;
            OverflowCount = 0;
            MissingScriptCount = 0;
            Valid &= ~CacheFacet.Components;
        }
    }
}
