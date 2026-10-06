using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class ExusiaiArenaSavedSceneTests
    {
        private const string Source = "Assets/Game/Scenes/PrototypeArena.unity";
        private const string Temporary = "Assets/Tests/ExusiaiIntegrationTemporary.unity";
        private Scene scene, previous;
        private SerializedProperty slots;
        private GameObject player;
        [SetUp] public void Setup()
        {
            previous = SceneManager.GetActiveScene();
            Assert.That(AssetDatabase.CopyAsset(Source, Temporary), Is.True);
            scene = EditorSceneManager.OpenScene(Temporary, OpenSceneMode.Additive);
            var roster = scene.GetRootGameObjects().Single(r => r.name == "ArenaBootstrap").GetComponent<ArenaRosterBootstrap>();
            slots = new SerializedObject(roster).FindProperty("slots");
            player = Templates().Single(t => t.GetComponent<ArknightsFrontline.Commands.PlayerCommandController>());
        }
        [TearDown] public void Cleanup()
        {
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            AssetDatabase.DeleteAsset(Temporary);
        }
        private GameObject[] Templates() => Enumerable.Range(0, slots.arraySize).Select(i =>
            (GameObject)slots.GetArrayElementAtIndex(i).FindPropertyRelative("template").objectReferenceValue).ToArray();
        private void Upgrade()
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ArknightsFrontline.Editor.ExusiaiArenaSceneTools")).First(t => t != null);
            var method = type.GetMethod("UpgradeScene"); Assert.That(method, Is.Not.Null, "Incremental saved-scene upgrade is missing");
            try { method.Invoke(null, new object[] { scene }); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        [Test] public void SavedSceneIntegratesOnlyPlayerExusiai()
        {
            Assert.That(player.GetComponent<ExusiaiCombatPresentation>(), Is.Not.Null);
            Assert.That(player.GetComponentsInChildren<ExusiaiPresentation>(true).Length, Is.EqualTo(1));
            Assert.That(player.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(player.GetComponent<CapsuleCollider>().enabled, Is.True);
            var bridge = player.GetComponent<ExusiaiCombatPresentation>();
            Assert.That(bridge.Presentation.transform.IsChildOf(player.transform), Is.True);
            Assert.That(bridge.VisualRoot.IsChildOf(player.transform), Is.True);
            Assert.That(player.GetComponent<ProjectileSpawnPoint>().Muzzle.IsChildOf(player.transform), Is.True);
            Assert.That(player.GetComponentInChildren<ExusiaiPreviewDriver>(true), Is.Null);
            foreach (var ai in Templates().Where(t => t != player))
            {
                Assert.That(ai.GetComponentInChildren<ExusiaiPresentation>(true), Is.Null, ai.name);
                Assert.That(ai.GetComponent<ExusiaiCombatPresentation>(), Is.Null);
                Assert.That(ai.GetComponent<ProjectileSpawnPoint>(), Is.Null);
                Assert.That(ai.GetComponent<Renderer>().enabled, Is.True);
            }
        }
        [Test] public void UpgradeTwicePreservesExistingSceneConfiguration()
        {
            var snapshots = new Dictionary<Component, string>();
            var ids = new Dictionary<GameObject, string>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                ids.Add(node.gameObject, GlobalObjectId.GetGlobalObjectIdSlow(node.gameObject).ToString());
                foreach (var component in node.GetComponents<Component>())
                {
                    if (!component || node.gameObject == player && (component is Transform || component is Renderer || component is ExusiaiCombatPresentation || component is ProjectileSpawnPoint)) continue;
                    snapshots.Add(component, EditorJsonUtility.ToJson(component));
                }
            }
            Vector3 position = player.transform.position, scale = player.transform.localScale;
            Quaternion rotation = player.transform.rotation;
            Material material = player.GetComponent<Renderer>().sharedMaterial;
            Upgrade(); Upgrade();
            foreach (var snapshot in snapshots) Assert.That(EditorJsonUtility.ToJson(snapshot.Key), Is.EqualTo(snapshot.Value), snapshot.Key.name + "/" + snapshot.Key.GetType().Name);
            foreach (var identity in ids) Assert.That(GlobalObjectId.GetGlobalObjectIdSlow(identity.Key).ToString(), Is.EqualTo(identity.Value));
            Assert.That(player.transform.position, Is.EqualTo(position)); Assert.That(player.transform.localScale, Is.EqualTo(scale));
            Assert.That(player.transform.rotation, Is.EqualTo(rotation)); Assert.That(player.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
            Assert.That(player.GetComponentsInChildren<ExusiaiPresentation>(true).Length, Is.EqualTo(1));
            Assert.That(player.GetComponents<ExusiaiCombatPresentation>().Length, Is.EqualTo(1));
            Assert.That(player.GetComponents<ProjectileSpawnPoint>().Length, Is.EqualTo(1));
        }
        [TestCase(false), TestCase(true)] public void UpgradeRejectsMissingOrMultiplePlayerExusiaiSlots(bool multiple)
        {
            int before = Templates().Sum(t => t.GetComponentsInChildren<ExusiaiPresentation>(true).Length);
            if (multiple)
            {
                var ai = Enumerable.Range(0, slots.arraySize).Select(i => slots.GetArrayElementAtIndex(i))
                    .First(s => !s.FindPropertyRelative("isPlayerControlled").boolValue && s.FindPropertyRelative("operatorType").enumValueIndex == (int)OperatorType.Exusiai);
                ai.FindPropertyRelative("isPlayerControlled").boolValue = true;
            }
            else
                foreach (int i in Enumerable.Range(0, slots.arraySize)) slots.GetArrayElementAtIndex(i).FindPropertyRelative("isPlayerControlled").boolValue = false;
            slots.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<InvalidOperationException>(Upgrade);
            Assert.That(Templates().Sum(t => t.GetComponentsInChildren<ExusiaiPresentation>(true).Length), Is.EqualTo(before));
        }
    }
}
