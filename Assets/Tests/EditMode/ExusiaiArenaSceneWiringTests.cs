using System;
using System.Linq;
using System.Reflection;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class ExusiaiArenaSceneWiringTests
    {
        private GameObject root;

        [TearDown] public void Cleanup() { if (root) Object.DestroyImmediate(root); }

        private GameObject CreateOperator(bool player = true)
        {
            root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.SetActive(false);
            root.transform.position = Vector3.up * 1.2f;
            root.transform.localScale = Vector3.one * 1.2f;
            var unit = root.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Blue, Altitude.Ground, 1000, 50, 2, 6, 0.5f, true, true);
            root.AddComponent<OperatorIdentity>().Configure("Player_Exusiai", TeamId.Blue, OperatorType.Exusiai);
            if (player) root.AddComponent<PlayerCommandController>();
            return root;
        }

        internal static GameObject Attach(GameObject target)
        {
            Type tool = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ArknightsFrontline.Editor.ExusiaiArenaSceneTools")).FirstOrDefault(t => t != null);
            Assert.That(tool, Is.Not.Null, "Formal player model installer is missing");
            try { return (GameObject)tool.GetMethod("AttachVisual").Invoke(null, new object[] { target }); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }

        [Test] public void AttachVisualUsesDeliveredPrefabAndKeepsGameplayRoot()
        {
            CreateOperator();
            Vector3 position = root.transform.position;
            GameObject visual = Attach(root);
            Assert.That(visual.GetComponent<ExusiaiPresentation>(), Is.Not.Null);
            Assert.That(visual.GetComponentInChildren<Animator>(true).applyRootMotion, Is.False);
            Assert.That(visual.GetComponentInChildren<ExusiaiPreviewDriver>(true), Is.Null);
            Assert.That(root.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(root.GetComponent<CapsuleCollider>().enabled, Is.True);
            Assert.That(root.transform.position, Is.EqualTo(position));
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one * 1.2f));
        }

        [Test] public void AttachVisualIsIdempotent()
        {
            CreateOperator();
            GameObject visual = Attach(root);
            Assert.That(Attach(root), Is.SameAs(visual));
            Assert.That(root.GetComponentsInChildren<ExusiaiPresentation>(true).Length, Is.EqualTo(1));
        }

        [Test] public void VisibleBodyIsGroundedAtArenaScale()
        {
            CreateOperator();
            var visual = Attach(root);
            Bounds bounds = default;
            bool first = true;
            foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = new Mesh();
                try
                {
                    skin.BakeMesh(mesh, true);
                    foreach (var vertex in mesh.vertices)
                    {
                        Vector3 point = skin.transform.TransformPoint(vertex);
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                        else bounds.Encapsulate(point);
                    }
                }
                finally { Object.DestroyImmediate(mesh); }
            }
            TestContext.WriteLine("Actual idle mesh bounds: " + bounds);
            Assert.That(bounds.min.y, Is.EqualTo(0).Within(0.02f));
            Assert.That(bounds.size.y, Is.EqualTo(2.4f).Within(0.02f));
            root.SetActive(true);
            Physics.SyncTransforms();
            Assert.That(root.GetComponent<Collider>().bounds.min.y, Is.EqualTo(0).Within(0.02f));
        }

        [Test] public void AttachVisualRejectsAiOperatorWithoutMutation()
        {
            CreateOperator(false);
            Assert.Throws<ArgumentException>(() => Attach(root));
            Assert.That(root.transform.childCount, Is.Zero);
            Assert.That(root.GetComponent<Renderer>().enabled, Is.True);
        }

        [Test] public void HealthBarIgnoresHiddenCapsule()
        {
            CreateOperator();
            root.GetComponent<Renderer>().enabled = false;
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.5f, 3f, 0.5f);
            root.SetActive(true);
            var bar = root.AddComponent<HealthBarPresenter>();
            bar.Configure(root.GetComponent<CombatUnit>());
            bar.Tick();
            Assert.That(root.transform.Find("HealthBar").position.y, Is.EqualTo(body.GetComponent<Renderer>().bounds.max.y + 0.35f).Within(0.001f));
        }
    }
}
