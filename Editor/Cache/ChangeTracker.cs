using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator
{
    /// <summary>
    /// Turns editor object changes into targeted cache invalidation.
    ///
    /// 1.x invalidated on <c>EditorApplication.hierarchyChanged</c> with an inverted size check, so small
    /// scenes never refreshed and large ones threw everything away. 2.0 uses
    /// <see cref="ObjectChangeEvents"/>, which says precisely what changed, and maps each kind to the
    /// narrowest facet that can possibly be affected. See ARCHITECTURE.md D3.
    /// </summary>
    internal static class ChangeTracker
    {
        private static readonly HashSet<EntityId> s_Dirty = new HashSet<EntityId>();
        private static bool s_GlobalReset;

        internal static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            s_Dirty.Clear();
            s_GlobalReset = false;

            for (int i = 0; i < stream.length; i++)
            {
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.ChangeGameObjectStructure:
                    {
                        stream.GetChangeGameObjectStructureEvent(i, out ChangeGameObjectStructureEventArgs args);
                        DecorationCache.Invalidate(args.entityId, CacheFacet.Components);
                        s_Dirty.Add(args.entityId);
                        break;
                    }

                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    {
                        stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out ChangeGameObjectOrComponentPropertiesEventArgs args);
                        MarkPropertyChange(args.entityId);
                        break;
                    }

                    case ObjectChangeKind.CreateGameObjectHierarchy:
                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    case ObjectChangeKind.ChangeGameObjectParent:
                    case ObjectChangeKind.ChangeChildrenOrder:
                    case ObjectChangeKind.ChangeRootOrder:
                    case ObjectChangeKind.ChangeScene:
                    {
                        // These all affect a whole subtree, and the event only ever names its root. Rather
                        // than maintaining a parent->children index purely to walk it, drop everything:
                        // these are user-paced operations, and a cold cache costs one bind per visible row.
                        s_GlobalReset = true;
                        break;
                    }

                    case ObjectChangeKind.UpdatePrefabInstances:
                    {
                        stream.GetUpdatePrefabInstancesEvent(i, out UpdatePrefabInstancesEventArgs args);
                        NativeArray<EntityId>.ReadOnly ids = args.entityIds;

                        for (int n = 0; n < ids.Length; n++)
                        {
                            DecorationCache.Invalidate(ids[n], CacheFacet.All);
                            s_Dirty.Add(ids[n]);
                        }

                        break;
                    }

                    case ObjectChangeKind.DestroyAssetObject:
                    case ObjectChangeKind.ChangeAssetObjectProperties:
                    {
                        // A component icon can be a project asset (custom script icon). Cheapest correct
                        // answer is to re-resolve icons; the tree structure is untouched.
                        DecorationCache.InvalidateAll(CacheFacet.Components);
                        s_GlobalReset = true;
                        break;
                    }
                }
            }

            if (s_GlobalReset)
            {
                DecorationCache.Clear();
                DecoratorHost.RefreshAllLiveRows();
                return;
            }

            if (s_Dirty.Count > 0)
            {
                DecoratorHost.RefreshLiveRows(s_Dirty);
            }
        }

        /// <summary>
        /// Property changes are the highest-volume event by far (every frame of a transform drag). Only O(1)
        /// work is allowed here: never rebuild the component slice.
        /// </summary>
        private static void MarkPropertyChange(EntityId entityId)
        {
            Object target = EditorUtility.EntityIdToObject(entityId);

            switch (target)
            {
                case GameObject gameObject:
                    DecorationCache.Invalidate(entityId, CacheFacet.Name);
                    s_Dirty.Add(entityId);
                    break;

                case Component component when component != null:
                {
                    // Toggling a component's enabled checkbox reports the Component, not its GameObject.
                    GameObject owner = component.gameObject;

                    if (owner != null)
                    {
                        s_Dirty.Add(owner.GetEntityId());
                    }

                    break;
                }
            }
        }

        internal static void OnHierarchyChanged()
        {
            // Coarse safety net only: ObjectChangeEvents is the primary mechanism. Anything that reached
            // here without producing an object-change event still needs live rows re-derived, but the cache
            // itself stays warm.
            DecoratorHost.RefreshAllLiveRows();
        }

        internal static void ResetAll()
        {
            s_Dirty.Clear();
            s_GlobalReset = false;
            DecorationCache.Clear();
            ComponentCatalog.Invalidate();
            NameMatcher.ClearRegexCache();
        }
    }
}
