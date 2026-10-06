using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Camera;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class MobaViewSceneTests
    {
        private Scene scene;
        private Scene previous;
        private bool opened;
        private GameObject[] roots;

        [SetUp]
        public void SetUp()
        {
            previous = SceneManager.GetActiveScene();
            scene = SceneManager.GetSceneByPath("Assets/Game/Scenes/PrototypeArena.unity");
            opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/PrototypeArena.unity", OpenSceneMode.Additive);
            roots = scene.GetRootGameObjects();
        }

        [TearDown]
        public void TearDown()
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }

        [Test]
        public void OperatorsStayGroundedAtDeploymentAndTowersHaveDistinctSilhouettes()
        {
            var arena = roots.Single(root => root.name == "ArenaBootstrap");
            var slots = new SerializedObject(arena.GetComponent<ArenaRosterBootstrap>()).FindProperty("slots");
            float towerHeight = arena.transform.Find("BlueTower/BlueTowerVisual").GetComponent<Renderer>().bounds.size.y;
            for (int i = 0; i < slots.arraySize; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i);
                var template = (GameObject)slot.FindPropertyRelative("template").objectReferenceValue;
                Bounds bounds = VisibleBodyBounds(template);
                Vector3 deployment = slot.FindPropertyRelative("deploymentPosition").vector3Value;
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.02f), template.name + " floats or sinks");
                Assert.That(bounds.min.y + deployment.y - template.transform.position.y, Is.EqualTo(0f).Within(0.02f), "redeployment floats or sinks");
                var capsule = template.GetComponent<CapsuleCollider>();
                Assert.That(template.transform.position.y - capsule.height * template.transform.lossyScale.y / 2, Is.EqualTo(0).Within(0.01f));
                Assert.That(towerHeight / bounds.size.y, Is.InRange(2f, 2.4f), "tower must read larger than an operator");
            }
        }

        private static Bounds VisibleBodyBounds(GameObject template)
        {
            if (template.GetComponent<Renderer>().enabled) return template.GetComponent<Renderer>().bounds;
            Bounds bounds = default; bool first = true;
            foreach (var skin in template.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.enabled))
            {
                var mesh = new Mesh();
                try
                {
                    skin.BakeMesh(mesh, true);
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        Vector3 point = skin.transform.TransformPoint(vertex);
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                        else bounds.Encapsulate(point);
                    }
                }
                finally { Object.DestroyImmediate(mesh); }
            }
            Assert.That(first, Is.False, "Hidden player capsule must have visible body geometry");
            return bounds;
        }

        [TestCase(16f / 9f)]
        [TestCase(4f / 3f)]
        public void BackgroundCoversCameraCornersAtAllPanLimits(float aspect)
        {
            var arena = roots.Single(root => root.name == "ArenaBootstrap");
            var background = arena.transform.Find("ArenaSurround");
            Assert.That(background, Is.Not.Null, "camera should not expose a floating rectangular floor");
            Bounds bounds = background.GetComponent<Renderer>().bounds;
            Assert.That(background.GetComponent<Collider>(), Is.Null, "decorative terrain must not accept movement clicks");
            var camera = roots.SelectMany(root => root.GetComponentsInChildren<UnityEngine.Camera>()).Single();
            Vector3 originalPosition = camera.transform.position;
            float originalAspect = camera.aspect;
            var offset = new SerializedObject(camera.GetComponent<MobaCameraController>()).FindProperty("cameraOffset").vector3Value;
            try
            {
                camera.aspect = aspect;
                foreach (float x in new[] { -50f, 0f, 50f })
                foreach (float z in new[] { -12f, 0f, 12f })
                {
                    camera.transform.position = new Vector3(x, 0f, z) + offset;
                    foreach (float u in new[] { 0f, 1f })
                    foreach (float v in new[] { 0f, 1f })
                    {
                        Ray ray = camera.ViewportPointToRay(new Vector3(u, v));
                        var plane = new Plane(Vector3.up, new Vector3(0f, bounds.center.y, 0f));
                        Assert.That(plane.Raycast(ray, out float distance), Is.True);
                        Vector3 hit = ray.GetPoint(distance);
                        Assert.That(hit.x, Is.InRange(bounds.min.x, bounds.max.x));
                        Assert.That(hit.z, Is.InRange(bounds.min.z, bounds.max.z));
                    }
                }
            }
            finally
            {
                camera.transform.position = originalPosition;
                camera.aspect = originalAspect;
            }
        }
    }
}
