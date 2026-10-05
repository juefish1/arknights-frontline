using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class ExusiaiScaledSecondaryMotionTests
    {
        private const string Prefab = "Assets/Game/Characters/Exusiai/Model/Prefabs/Exusiai_Game_Humanoid.prefab";
        private static GameObject CreateModel(float scale)
        {
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            model.transform.localScale = Vector3.one * scale;
            var motion = model.GetComponent<ExusiaiSecondaryMotion>();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            motion.Configure((ExusiaiSecondaryMotion.Chain[])typeof(ExusiaiSecondaryMotion).GetField("chains", flags).GetValue(motion),
                (ExusiaiSecondaryMotion.Capsule[])typeof(ExusiaiSecondaryMotion).GetField("colliders", flags).GetValue(motion),
                (ExusiaiSecondaryMotionProfile)typeof(ExusiaiSecondaryMotion).GetField("profile", flags).GetValue(motion));
            return model;
        }

        [TestCase(1f), TestCase(2f)]
        public void ScaledChainCollisionUsesScaledRadii(float scale)
        {
            var fixture = new GameObject("ScaledCollisionChain");
            var profile = ScriptableObject.CreateInstance<ExusiaiSecondaryMotionProfile>();
            try
            {
                fixture.transform.localScale = Vector3.one * scale;
                var bones = new Transform[3];
                for (int i = 0; i < 3; i++)
                {
                    bones[i] = new GameObject("Segment" + i).transform;
                    bones[i].SetParent(i == 0 ? fixture.transform : bones[i - 1], false);
                    bones[i].localPosition = i == 0 ? Vector3.zero : Vector3.down * 0.1f;
                }
                var obstacle = new GameObject("LegProxy").transform;
                obstacle.SetParent(fixture.transform, false);
                obstacle.localPosition = new Vector3(-0.025f, -0.19f, 0);
                var motion = fixture.AddComponent<ExusiaiSecondaryMotion>();
                motion.Configure(new[] { new ExusiaiSecondaryMotion.Chain { bones = bones } },
                    new[] { new ExusiaiSecondaryMotion.Capsule { start = obstacle, end = obstacle, radius = 0.04f } }, profile);
                for (int frame = 0; frame < 240; frame++) { motion.RestorePose(); motion.Simulate(1f / 120); }
                Assert.That(Vector3.Distance(bones[2].position, obstacle.position), Is.GreaterThanOrEqualTo(0.047f * scale));
                Assert.That(Vector3.Distance(bones[1].position, bones[2].position), Is.EqualTo(0.1f * scale).Within(0.001f * scale));
            }
            finally { Object.DestroyImmediate(fixture); Object.DestroyImmediate(profile); }
        }

        [TestCase(30), TestCase(60), TestCase(120)]
        public void ScaledMotionPreservesActualBonesAndMatchesReference(int rate)
        {
            var reference = CreateModel(1);
            var enlarged = CreateModel(1.5f);
            try
            {
                var a = reference.GetComponent<ExusiaiSecondaryMotion>();
                var b = enlarged.GetComponent<ExusiaiSecondaryMotion>();
                var bonesA = reference.GetComponentsInChildren<Transform>().Where(t => t.name.Contains("Hair_") || t.name.StartsWith("SkirtHem_")).ToArray();
                var bonesB = enlarged.GetComponentsInChildren<Transform>().Where(t => t.name.Contains("Hair_") || t.name.StartsWith("SkirtHem_")).ToArray();
                var positions = bonesB.Select(t => t.localPosition).ToArray();
                var scales = bonesB.Select(t => t.localScale).ToArray();
                for (int frame = 1; frame <= rate * 3; frame++)
                {
                    float time = (float)frame / rate;
                    a.RestorePose(); b.RestorePose();
                    reference.transform.position = new Vector3(0.08f * Mathf.Sin(time * 4), 0, 0);
                    enlarged.transform.position = reference.transform.position * 1.5f;
                    a.Simulate(1f / rate); b.Simulate(1f / rate);
                    for (int i = 0; i < bonesB.Length; i++)
                    {
                        Assert.That(float.IsFinite(bonesB[i].position.sqrMagnitude), Is.True);
                        Assert.That(bonesB[i].localPosition, Is.EqualTo(positions[i]));
                        Assert.That(bonesB[i].localScale, Is.EqualTo(scales[i]));
                        Assert.That(Vector3.Distance(bonesA[i].position, bonesB[i].position / 1.5f), Is.LessThan(0.015f), bonesB[i].name);
                    }
                }
                var paused = bonesB.Select(t => t.rotation).ToArray();
                b.RestorePose(); b.Simulate(0);
                for (int i = 0; i < paused.Length; i++) Assert.That(Quaternion.Angle(paused[i], bonesB[i].rotation), Is.LessThan(0.15f));
                enlarged.transform.position += Vector3.right * 10;
                b.RestorePose(); b.Simulate(1f / rate);
                for (int i = 0; i < bonesB.Length; i++) Assert.That(bonesB[i].localPosition, Is.EqualTo(positions[i]));
                b.enabled = false; b.enabled = true;
                b.RestorePose(); b.Simulate(1f / rate);
                Assert.That(bonesB.All(t => float.IsFinite(t.position.sqrMagnitude)), Is.True);
            }
            finally { Object.DestroyImmediate(reference); Object.DestroyImmediate(enlarged); }
        }

        [Test] public void DeploymentInstancesDoNotShareSimulationState()
        {
            var a = CreateModel(1.5f); var b = CreateModel(1.5f);
            try
            {
                var motionA = a.GetComponent<ExusiaiSecondaryMotion>(); var motionB = b.GetComponent<ExusiaiSecondaryMotion>();
                var bonesB = b.GetComponentsInChildren<Transform>();
                for (int i = 0; i < 240; i++) { motionB.RestorePose(); motionB.Simulate(1f / 120); }
                var before = bonesB.Select(t => t.rotation).ToArray();
                a.transform.position += Vector3.right * 20; motionA.ResetSimulation();
                for (int i = 0; i < bonesB.Length; i++) Assert.That(bonesB[i].rotation, Is.EqualTo(before[i]));
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }
    }
}
