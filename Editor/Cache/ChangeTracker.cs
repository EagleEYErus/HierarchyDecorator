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
        private static bool s_ComponentsReset;

        /// <summary>
        /// Set when an object-change batch already re-decorated the visible rows. Most structural edits raise
        /// both changesPublished and hierarchyChanged, and doing the work twice in one tick is pure waste.
        /// </summary>
        private static bool s_HandledThisTick;

        internal static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            s_Dirty.Clear();
            s_GlobalReset = false;
            s_ComponentsReset = false;

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
                    case ObjectChangeKind.UpdatePrefabInstances:
                    {
                        // These all affect a whole subtree, and the event only ever names its root - for
                        // UpdatePrefabInstances, the instance roots, which says nothing about components
                        // added to nested children. Rather than maintaining a parent->children index purely
                        // to walk it, drop everything: these are user-paced operations, and a cold cache
                        // costs one bind per visible row.
                        s_GlobalReset = true;
                        break;
                    }

                    case ObjectChangeKind.DestroyAssetObject:
                    {
                        // The object is already gone, so it cannot be identified; a cached icon Texture2D may
                        // be among the casualties.
                        s_GlobalReset = true;
                        break;
                    }

                    case ObjectChangeKind.ChangeAssetObjectProperties:
                    {
                        stream.GetChangeAssetObjectPropertiesEvent(i, out ChangeAssetObjectPropertiesEventArgs args);
                        MarkAssetChange(args.entityId);
                        break;
                    }
                }
            }

            if (s_GlobalReset)
            {
                DecorationCache.Clear();
                DecoratorHost.RefreshAllLiveRows();
                s_HandledThisTick = true;
                return;
            }

            if (s_ComponentsReset)
            {
                DecorationCache.InvalidateAll(CacheFacet.Components);
                DecoratorHost.RefreshAllLiveRows();
                s_HandledThisTick = true;
                return;
            }

            if (s_Dirty.Count > 0)
            {
                DecoratorHost.RefreshLiveRows(s_Dirty);
                s_HandledThisTick = true;
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
                case GameObject:
                    // Covers renames, which are the only property change that alters a header match.
                    DecorationCache.Invalidate(entityId, CacheFacet.Name);
                    s_Dirty.Add(entityId);
                    break;

                case Component component when component != null:
                {
                    // The only component property a row reflects is the enabled checkbox, and only when the
                    // icons are on and configured to fade. Without this gate a transform gizmo drag - which
                    // reports the Transform once per frame - would re-decorate every visible row at 60 Hz for
                    // no visual change at all.
                    ComponentIconSettings icons = HierarchyDecoratorSettings.instance.ComponentIcons;

                    if (!icons.enabled || !icons.fadeDisabledComponents || !HasEnabledState(component))
                    {
                        break;
                    }

                    GameObject owner = component.gameObject;

                    if (owner != null)
                    {
                        s_Dirty.Add(owner.GetEntityId());
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Whether a component can be disabled at all. Mirrors what EditorUtility.GetObjectEnabled answers
        /// with -1, without the native call: Transform, and most non-Behaviour components, have no checkbox.
        /// </summary>
        private static bool HasEnabledState(Component component)
        {
            return component is Behaviour or Renderer or Collider or Cloth or LODGroup;
        }

        /// <summary>
        /// Asset edits are frequent - one per frame while a slider is dragged in the Inspector - and almost
        /// none of them can change a hierarchy row. Only a script (its custom icon) or a texture (an icon
        /// asset itself) can, so everything else is ignored instead of dropping the whole cache.
        /// </summary>
        private static void MarkAssetChange(EntityId entityId)
        {
            Object target = EditorUtility.EntityIdToObject(entityId);

            if (target is MonoScript or Texture)
            {
                s_ComponentsReset = true;
            }
        }

        internal static void OnHierarchyChanged()
        {
            // Coarse safety net: ObjectChangeEvents is the primary mechanism, but a change that produced no
            // undoable event still has to be picked up - and re-decorating alone would just re-read the same
            // cached values, so the facets are dropped first.
            if (s_HandledThisTick)
            {
                s_HandledThisTick = false;
                return;
            }

            DecorationCache.InvalidateAll(CacheFacet.All);
            DecoratorHost.RefreshAllLiveRows();
        }

        internal static void ResetAll()
        {
            s_Dirty.Clear();
            s_GlobalReset = false;
            s_ComponentsReset = false;
            s_HandledThisTick = false;
            DecorationCache.Clear();
            ComponentCatalog.Invalidate();
            NameMatcher.ClearRegexCache();
        }
    }
}
