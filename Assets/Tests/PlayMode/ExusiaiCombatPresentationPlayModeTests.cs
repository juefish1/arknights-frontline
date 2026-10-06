#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class ExusiaiCombatPresentationPlayModeTests
    {
        private GameObject arena, root, enemy, visual;
        private Component adapter;
        private Animator animator;
        private UnitMotor motor;
        private AttackSequenceExecutor sequence;
        private BasicAttackController attacks;
        private CombatUnit target;
        private float previousScale;
        private int previousCapture;

        [UnitySetUp] public IEnumerator Setup()
        {
            previousScale = Time.timeScale;
            previousCapture = Time.captureFramerate;
            Time.timeScale = 1;
            Time.captureFramerate = 60;
            arena = new GameObject("PresentationTestArena");
            arena.AddComponent<MatchOutcomeController>();
            root = new GameObject("PresentationTestPlayer");
            root.SetActive(false);
            root.transform.SetParent(arena.transform);
            root.transform.position = Vector3.up * 1.2f;
            var owner = root.AddComponent<CombatUnit>();
            owner.Configure(TeamId.Blue, Altitude.Ground, 1000, 10, 0, 6, 0.5f, true, true);
            motor = root.AddComponent<UnitMotor>(); motor.Configure(3.6f, ArenaLayout.CreateDefault()); motor.enabled = false;
            root.AddComponent<PlayerCommandController>().enabled = false;
            sequence = root.AddComponent<AttackSequenceExecutor>(); sequence.Configure(owner);
            attacks = root.AddComponent<BasicAttackController>(); attacks.Configure(owner, sequence); attacks.enabled = false;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Characters/Exusiai/Model/Prefabs/Exusiai_Game_Humanoid.prefab");
            visual = Object.Instantiate(prefab, root.transform);
            visual.transform.localScale = Vector3.one * 1.5f;
            animator = visual.GetComponentInChildren<Animator>(true);
            Type adapterType = typeof(CombatUnit).Assembly.GetType("ArknightsFrontline.Combat.ExusiaiCombatPresentation");
            Assert.That(adapterType, Is.Not.Null, "Combat animation adapter is missing");
            adapter = root.AddComponent(adapterType);
            Call("Configure", visual.GetComponent<ExusiaiPresentation>(), visual.transform);
            enemy = new GameObject("PresentationTestTarget"); enemy.transform.position = new Vector3(3, 1.2f, 0);
            target = enemy.AddComponent<CombatUnit>(); target.Configure(TeamId.Red, Altitude.Ground, 10000, 1, 0, 6, 1, true, true);
            root.SetActive(true);
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (arena) Object.Destroy(arena);
            if (enemy) Object.Destroy(enemy);
            foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Object.Destroy(projectile.gameObject);
            Time.timeScale = previousScale;
            Time.captureFramerate = previousCapture;
            yield return null;
        }

        private void Call(string method, params object[] arguments)
        {
            try { adapter.GetType().GetMethod(method).Invoke(adapter, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        private static AttackSequencePlan Plan(int shots, AttackSequenceKind kind = AttackSequenceKind.Basic) =>
            new AttackSequencePlan(kind, shots, 0.1f, 10, 1, 0, 1, 0, true, false);

        [UnityTest] public IEnumerator MotorDisplacementDrivesJogWithoutRootMotion()
        {
            Vector3 firstFoot = animator.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
            motor.SetDestination(root.transform.position + Vector3.right * 5);
            for (int i = 0; i < 30; i++) { motor.Tick(1f / 60); yield return null; }
            Assert.That(root.transform.position.x, Is.EqualTo(1.8f).Within(0.002f), "Animator moved the gameplay root");
            Assert.That(animator.GetFloat("MoveSpeed"), Is.GreaterThan(0.9f));
            Assert.That(animator.GetFloat("JogRate"), Is.EqualTo(1).Within(0.02f));
            Assert.That(Vector3.Distance(firstFoot, animator.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position)), Is.GreaterThan(0.01f));
            Assert.That(animator.applyRootMotion, Is.False);
        }

        [UnityTest] public IEnumerator ArrivingAndPausingDoNotProduceFalseVelocity()
        {
            motor.SetDestination(root.transform.position + Vector3.right * 0.06f);
            motor.Tick(1f / 60);
            yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.That(animator.GetFloat("MoveSpeed"), Is.LessThan(0.01f));
            Time.timeScale = 0;
            root.transform.position += Vector3.forward * 15;
            Call("Tick", 0f);
            yield return null;
            Time.timeScale = 1;
            yield return null;
            Assert.That(animator.GetFloat("JogRate"), Is.EqualTo(1).Within(0.02f));
            Assert.That(float.IsFinite(animator.GetFloat("MoveSpeed")), Is.True);
        }

        [UnityTest] public IEnumerator PlayerSequenceShotsDoNotDuplicateRoundNotifications()
        {
            int shots = 0;
            sequence.ShotRequested += (_, __) => shots++;
            attacks.SetPlanProvider(() => Plan(3));
            attacks.SetTarget(target);
            attacks.Tick(0);
            bool sawAim = false;
            for (int i = 0; i < 35; i++) { yield return null; sawAim |= animator.GetLayerWeight(2) > 0.95f; }
            Assert.That(shots, Is.EqualTo(3));
            Assert.That(target.CurrentHealth, Is.EqualTo(9970).Within(0.01f), "Duplicate visual binding changed damage");
            Assert.That(sawAim, Is.True);
            sequence.Cancel();
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(shots, Is.EqualTo(3));
        }

        [UnityTest] public IEnumerator ChargeShotUsesEmittedTargetWithoutBasicAttackTarget()
        {
            attacks.ClearTarget();
            target.transform.position = root.transform.position + Vector3.left * 3;
            Assert.That(sequence.TryStart(Plan(1, AttackSequenceKind.Charge), target), Is.True);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(animator.GetLayerWeight(2), Is.GreaterThan(0.95f));
            Assert.That(Vector3.Dot(animator.transform.forward, Vector3.left), Is.GreaterThan(0.9f));
            Assert.That(attacks.CurrentTarget, Is.Null);
        }

        [UnityTest] public IEnumerator DisableEnableAndReconfigureDoNotDuplicateFeedback()
        {
            root.SetActive(false); root.SetActive(true);
            Call("Configure", visual.GetComponent<ExusiaiPresentation>(), visual.transform);
            Call("Configure", visual.GetComponent<ExusiaiPresentation>(), visual.transform);
            sequence.TryStart(Plan(1), target);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(target.CurrentHealth, Is.EqualTo(9990).Within(0.01f));
            Assert.That(animator.GetLayerWeight(2), Is.GreaterThan(0.95f));
            root.SetActive(false);
            Assert.That(sequence.TryStart(Plan(1), target), Is.False);
        }

        [UnityTest] public IEnumerator MissingPlayerSequenceDisablesAdapter()
        {
            root.SetActive(false);
            Object.DestroyImmediate(sequence);
            LogAssert.Expect(LogType.Error, "Exusiai combat presentation requires a player attack sequence and model references.");
            root.SetActive(true);
            yield return null;
            Assert.That(((Behaviour)adapter).enabled, Is.False);
        }

        [UnityTest] public IEnumerator InactiveConfigurationAndDisableDoNotEvaluateAnimator()
        {
            var warnings = new System.Collections.Generic.List<string>();
            void Capture(string message, string trace, LogType type)
            {
                if (message.Contains("Can't call Animator.Update on inactive object")) warnings.Add(message);
            }
            Application.logMessageReceived += Capture;
            try
            {
                root.SetActive(false);
                Call("Configure", visual.GetComponent<ExusiaiPresentation>(), visual.transform);
                yield return null;
                Assert.That(warnings, Is.Empty, "Inactive lifecycle attempted to evaluate an Animator");
                root.SetActive(true);
                Assert.That(sequence.TryStart(Plan(1), target), Is.True);
                for (int i = 0; i < 20; i++) yield return null;
                Assert.That(animator.GetLayerWeight(2), Is.GreaterThan(0.95f));
                root.SetActive(false); root.SetActive(true);
                yield return null;
                Assert.That(warnings, Is.Empty);
            }
            finally { Application.logMessageReceived -= Capture; }
        }

        [UnityTest] public IEnumerator TeleportAndMatchEndClearPendingPresentation()
        {
            root.transform.position += Vector3.forward * 10;
            yield return null;
            Assert.That(animator.GetFloat("JogRate"), Is.EqualTo(1).Within(0.02f));
            root.transform.position = Vector3.up * 1.2f;
            var blue = new GameObject("BlueTestTower"); blue.transform.SetParent(arena.transform);
            var red = new GameObject("RedTestTower"); red.transform.SetParent(arena.transform);
            var blueUnit = blue.AddComponent<CombatUnit>(); blueUnit.Configure(TeamId.Blue, Altitude.Ground, 100, 1, 0, 1, 1, true, true);
            var redUnit = red.AddComponent<CombatUnit>(); redUnit.Configure(TeamId.Red, Altitude.Ground, 100, 1, 0, 1, 1, true, true);
            var spawner = arena.AddComponent<MinionWaveSpawner>(); spawner.enabled = false;
            arena.GetComponent<MatchOutcomeController>().Configure(blueUnit, redUnit, spawner);
            sequence.TryStart(Plan(1), target);
            redUnit.TakePhysicalDamage(1000);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(animator.GetLayerWeight(2), Is.LessThan(0.01f));
            Assert.That(animator.GetFloat("MoveSpeed"), Is.LessThan(0.01f));
        }
    }
}
#endif
