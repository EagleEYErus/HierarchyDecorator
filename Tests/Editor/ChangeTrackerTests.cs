using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator.Tests
{
    /// <summary>
    /// The ObjectChangeKind mapping is where "the cache is always correct" and "editing is not slow" meet, and
    /// every one of its rules is invisible from the outside. <see cref="ObjectChangeEventStream.Builder"/>
    /// lets the stream be synthesised, so the mapping can be tested without a live scene or a real edit.
    /// </summary>
    public sealed class ChangeTrackerTests
    {
        private sealed class DummyAsset : ScriptableObject
        {
        }

        private readonly List<Object> m_Created = new List<Object>();

        private GameObject NewGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            m_Created.Add(gameObject);
            return gameObject;
        }

        [SetUp]
        public void SetUp()
        {
            // Also resets the "already handled this frame" stamp, so tests that share a frame stay isolated.
            ChangeTracker.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < m_Created.Count; i++)
            {
                if (m_Created[i] != null)
                {
                    Object.DestroyImmediate(m_Created[i]);
                }
            }

            m_Created.Clear();
            ChangeTracker.ResetAll();
        }

        private static void Publish(ObjectChangeEventStream.Builder builder)
        {
            ObjectChangeEventStream stream = builder.ToStream(Allocator.Temp);

            try
            {
                ChangeTracker.OnChangesPublished(ref stream);
            }
            finally
            {
                stream.Dispose();
            }
        }

        private RowData WarmRow(GameObject gameObject, HierarchyDecoratorSettings settings)
        {
            RowData data = DecorationCache.GetOrCreate(gameObject.GetEntityId());
            DecorationCache.Ensure(data, gameObject, settings, CacheFacet.All);

            Assert.AreEqual(CacheFacet.All, data.Valid & CacheFacet.All, "The row should start fully cached.");
            return data;
        }

        [Test]
        public void ChangeGameObjectStructure_InvalidatesOnlyTheComponentFacet()
        {
            using SettingsScope scope = new SettingsScope();

            GameObject gameObject = NewGameObject("Thing");
            gameObject.AddComponent<AudioSource>();

            RowData data = WarmRow(gameObject, scope.Settings);

            using ObjectChangeEventStream.Builder builder = new ObjectChangeEventStream.Builder(Allocator.Temp);
            ChangeGameObjectStructureEventArgs args = new ChangeGameObjectStructureEventArgs(gameObject.GetEntityId(), gameObject.scene);
            builder.PushChangeGameObjectStructureEvent(ref args);
            Publish(builder);

            Assert.AreEqual(CacheFacet.None, data.Valid & CacheFacet.Components, "Adding a component must drop the icon slice.");
            Assert.AreEqual(CacheFacet.Name, data.Valid & CacheFacet.Name, "It must not drop the header match.");
        }

        [Test]
        public void ChangeAssetObjectProperties_IgnoresAssetsThatCannotAffectARow()
        {
            using SettingsScope scope = new SettingsScope();

            GameObject gameObject = NewGameObject("Thing");
            RowData data = WarmRow(gameObject, scope.Settings);

            DummyAsset asset = ScriptableObject.CreateInstance<DummyAsset>();
            m_Created.Add(asset);

            // This event fires once per frame while any Inspector slider is dragged. Dropping the cache for
            // it meant a material tweak re-scanned every visible row at 60 Hz.
            using ObjectChangeEventStream.Builder builder = new ObjectChangeEventStream.Builder(Allocator.Temp);
            ChangeAssetObjectPropertiesEventArgs args = new ChangeAssetObjectPropertiesEventArgs(default, asset.GetEntityId(), gameObject.scene);
            builder.PushChangeAssetObjectPropertiesEvent(ref args);
            Publish(builder);

            Assert.AreEqual(1, DecorationCache.Count, "An unrelated asset edit must not clear the cache.");
            Assert.AreEqual(CacheFacet.All, data.Valid & CacheFacet.All, "…nor invalidate a row.");
        }

        [Test]
        public void UpdatePrefabInstances_DropsEverything()
        {
            using SettingsScope scope = new SettingsScope();

            GameObject gameObject = NewGameObject("Thing");
            WarmRow(gameObject, scope.Settings);

            // The event names only the instance roots, so a component added to a nested prefab child is not
            // in the list. Anything narrower than a full drop would leave that child stale.
            NativeArray<EntityId> ids = new NativeArray<EntityId>(1, Allocator.Temp);
            ids[0] = gameObject.GetEntityId();

            try
            {
                using ObjectChangeEventStream.Builder builder = new ObjectChangeEventStream.Builder(Allocator.Temp);
                UpdatePrefabInstancesEventArgs args = new UpdatePrefabInstancesEventArgs(gameObject.scene, ids.AsReadOnly());
                builder.PushUpdatePrefabInstancesEvent(ref args);
                Publish(builder);
            }
            finally
            {
                ids.Dispose();
            }

            Assert.AreEqual(0, DecorationCache.Count);
        }

        [Test]
        public void DestroyGameObjectHierarchy_DropsEverything()
        {
            using SettingsScope scope = new SettingsScope();

            GameObject gameObject = NewGameObject("Thing");
            WarmRow(gameObject, scope.Settings);

            using ObjectChangeEventStream.Builder builder = new ObjectChangeEventStream.Builder(Allocator.Temp);
            DestroyGameObjectHierarchyEventArgs args = new DestroyGameObjectHierarchyEventArgs(
                gameObject.GetEntityId(), default, gameObject.scene);
            builder.PushDestroyGameObjectHierarchyEvent(ref args);
            Publish(builder);

            Assert.AreEqual(0, DecorationCache.Count);
        }

        [Test]
        public void HierarchyChangedSafetyNet_InvalidatesRatherThanJustRedecorating()
        {
            using SettingsScope scope = new SettingsScope();

            GameObject gameObject = NewGameObject("Thing");
            RowData data = WarmRow(gameObject, scope.Settings);

            // A change that produced no undoable event still has to be picked up; re-decorating alone would
            // just re-read the same cached values.
            ChangeTracker.OnHierarchyChanged();

            Assert.AreEqual(CacheFacet.None, data.Valid & CacheFacet.All);
        }
    }
}
