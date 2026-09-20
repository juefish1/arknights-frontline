using System.Collections.Generic;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class AttackSequenceExecutorTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private CombatUnit owner;
        private CombatUnit target;
        private AttackSequenceExecutor executor;
        private readonly List<CombatUnit> targets = new List<CombatUnit>();
        private readonly List<PhysicalDamagePayload> payloads = new List<PhysicalDamagePayload>();
        private readonly List<bool> completions = new List<bool>();

        [SetUp]
        public void SetUp()
        {
            owner = CreateUnit("Owner", TeamId.Blue, Vector3.zero);
            target = CreateUnit("Target", TeamId.Red, Vector3.right * 2f);
            executor = owner.gameObject.AddComponent<AttackSequenceExecutor>();
            executor.Configure(owner);
            executor.ShotRequested += (unit, payload) => { targets.Add(unit); payloads.Add(payload); };
            executor.SequenceFinished += (_, completed) => completions.Add(completed);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject item in objects) Object.DestroyImmediate(item);
            objects.Clear();
            targets.Clear();
            payloads.Clear();
            completions.Clear();
        }

        [Test]
        public void FiveShotPlanFiresImmediatelyThenEveryPointZeroFiveSeconds()
        {
            Assert.That(executor.TryStart(Plan(5), target), Is.True);
            Assert.That(payloads.Count, Is.EqualTo(1));
            executor.Tick(0.049f);
            Assert.That(payloads.Count, Is.EqualTo(1));
            executor.Tick(0.001f);
            Assert.That(payloads.Count, Is.EqualTo(2));
            executor.Tick(0.15f);
            Assert.That(payloads.Count, Is.EqualTo(5));
            Assert.That(executor.IsRunning, Is.False);
            Assert.That(completions, Is.EqualTo(new[] { true }));
            executor.Tick(100f);
            executor.Cancel();
            Assert.That(completions.Count, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None), Is.Empty);
        }

        [Test]
        public void LargeTickCatchesUpAllShotsUsingPlanInterval()
        {
            executor.TryStart(Plan(5, interval: 0.2f), target);
            executor.Tick(0.19f);
            Assert.That(payloads.Count, Is.EqualTo(1));
            executor.Tick(10f);
            Assert.That(payloads.Count, Is.EqualTo(5));
            Assert.That(completions, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void SmallTicksDoNotAccumulateMissingShots()
        {
            executor.TryStart(Plan(5), target);
            for (int i = 0; i < 200; i++) executor.Tick(0.001f);
            Assert.That(payloads.Count, Is.EqualTo(5));
            Assert.That(executor.IsRunning, Is.False);
        }

        [Test]
        public void NegativeTimeDoesNotAdvanceSequence()
        {
            executor.TryStart(Plan(2), target);
            executor.Tick(-1f);
            executor.Tick(0.049f);
            Assert.That(payloads.Count, Is.EqualTo(1));
            executor.Tick(0.001f);
            Assert.That(payloads.Count, Is.EqualTo(2));
        }

        [Test]
        public void PlanClampsShotCountAndNegativeInterval()
        {
            AttackSequencePlan plan = Plan(0, interval: -1f);
            Assert.That(plan.ShotCount, Is.EqualTo(1));
            Assert.That(plan.ShotInterval, Is.Zero);
            executor.TryStart(plan, target);
            Assert.That(payloads.Count, Is.EqualTo(1));
            Assert.That(completions, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void ZeroIntervalGeneratesAllShotsWithoutHanging()
        {
            executor.TryStart(Plan(5, interval: 0f), target);
            executor.Tick(0f);
            Assert.That(payloads.Count, Is.EqualTo(5));
            Assert.That(executor.IsRunning, Is.False);
        }

        [Test]
        public void RunningSequenceRejectsAnotherStart()
        {
            executor.TryStart(Plan(5), target);
            Assert.That(executor.TryStart(Plan(1), target), Is.False);
            Assert.That(payloads.Count, Is.EqualTo(1));
        }

        [Test]
        public void StartRejectsNullPlanMissingOwnerAndIllegalOrOutOfRangeTarget()
        {
            Assert.That(executor.TryStart(null, target), Is.False);
            Assert.That(executor.TryStart(Plan(1), null), Is.False);
            Assert.That(executor.TryStart(Plan(1), owner), Is.False);
            target.transform.position = Vector3.right * 7f;
            Assert.That(executor.TryStart(Plan(1, ignoreRange: true), target), Is.False);
            GameObject empty = new GameObject("Unconfigured");
            objects.Add(empty);
            Assert.That(empty.AddComponent<AttackSequenceExecutor>().TryStart(Plan(1), target), Is.False);
            Assert.That(payloads, Is.Empty);
            Assert.That(completions, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DeadOrIllegalTargetRetargetsNearestLegalEnemy(bool makeIllegal)
        {
            CombatUnit nearest = CreateUnit("Nearest", TeamId.Red, Vector3.right);
            CreateUnit("Farther", TeamId.Red, Vector3.right * 4f);
            CreateUnit("Ally", TeamId.Blue, Vector3.right * 0.5f);
            executor.TryStart(Plan(3), target);
            if (makeIllegal) target.Configure(TeamId.Blue, Altitude.Ground, 100f, 50f, 0f, 6f, 0.5f, true, false);
            else target.TakePhysicalDamage(100f);
            executor.Tick(0.1f);
            Assert.That(targets, Is.EqualTo(new[] { target, nearest, nearest }));
            Assert.That(completions, Is.EqualTo(new[] { true }));
        }

        [TestCase(AttackSequenceKind.Basic)]
        [TestCase(AttackSequenceKind.Sweep)]
        [TestCase(AttackSequenceKind.Overload)]
        [TestCase(AttackSequenceKind.OverloadSweep)]
        public void LivingTargetLeavingRangeInterruptsWithoutRetargeting(AttackSequenceKind kind)
        {
            CreateUnit("Replacement", TeamId.Red, Vector3.right);
            AttackSequencePlan plan = Plan(5, kind);
            AttackSequencePlan finished = null;
            executor.SequenceFinished += (value, _) => finished = value;
            executor.TryStart(plan, target);
            target.transform.position = Vector3.right * 7f;
            executor.Tick(0.2f);
            target.transform.position = Vector3.right;
            executor.Tick(1f);
            Assert.That(payloads.Count, Is.EqualTo(1));
            Assert.That(completions, Is.EqualTo(new[] { false }));
            Assert.That(finished, Is.SameAs(plan));
        }

        [Test]
        public void ChargeContinuesWhenOwnerMovesOutOfRange()
        {
            executor.TryStart(Plan(4, AttackSequenceKind.Charge, true), target);
            owner.transform.position = Vector3.right * 20f;
            executor.Tick(0.15f);
            Assert.That(targets, Is.EqualTo(new[] { target, target, target, target }));
            Assert.That(completions, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void ChargeRetargetsFromOwnersCurrentPositionAfterDeath()
        {
            CreateUnit("OldNearby", TeamId.Red, Vector3.right);
            CombatUnit newNearby = CreateUnit("NewNearby", TeamId.Red, Vector3.right * 21f);
            executor.TryStart(Plan(4, AttackSequenceKind.Charge, true), target);
            owner.transform.position = Vector3.right * 20f;
            target.TakePhysicalDamage(100f);
            executor.Tick(0.15f);
            Assert.That(targets, Is.EqualTo(new[] { target, newNearby, newNearby, newNearby }));
        }

        [Test]
        public void DeadTargetWithoutReplacementInterruptsOnce()
        {
            executor.TryStart(Plan(5), target);
            target.TakePhysicalDamage(100f);
            executor.Tick(10f);
            executor.Tick(10f);
            Assert.That(payloads.Count, Is.EqualTo(1));
            Assert.That(completions, Is.EqualTo(new[] { false }));
        }

        [Test]
        public void EachShotUsesSnapshotAndOnlyLastCarriesMissingHealthRatio()
        {
            executor.TryStart(Plan(3, AttackSequenceKind.Sweep), target);
            owner.gameObject.AddComponent<UnitStatModifiers>().SetAttackPowerMultiplier("R", 2f);
            executor.Tick(0.1f);
            Assert.That(payloads.ConvertAll(p => p.MissingHealthRatio), Is.EqualTo(new[] { 0f, 0f, 0.08f }));
            foreach (PhysicalDamagePayload payload in payloads)
            {
                Assert.That(payload.AttackPower, Is.EqualTo(50f));
                Assert.That(payload.DamageMultiplier, Is.EqualTo(1.45f));
                Assert.That(payload.MovementSlowMultiplier, Is.EqualTo(0.7f));
                Assert.That(payload.SlowDuration, Is.EqualTo(2f));
            }
        }

        [Test]
        public void CancelClearsStateAndNotifiesExactlyOnce()
        {
            executor.TryStart(Plan(5), target);
            executor.Cancel();
            executor.Cancel();
            executor.Tick(10f);
            Assert.That(executor.IsRunning, Is.False);
            Assert.That(completions, Is.EqualTo(new[] { false }));
            Assert.That(payloads.Count, Is.EqualTo(1));
            Assert.That(executor.TryStart(Plan(1), target), Is.True);
            Assert.That(completions, Is.EqualTo(new[] { false, true }));
        }

        [Test]
        public void CallbackCancellationAndNestedTickDoNotGenerateExtraShots()
        {
            executor.ShotRequested += (_, _) => { executor.Tick(10f); executor.Cancel(); };
            executor.TryStart(Plan(5), target);
            Assert.That(payloads.Count, Is.EqualTo(1));
            Assert.That(completions, Is.EqualTo(new[] { false }));
        }

        [Test]
        public void CompletionCallbackCannotReenterOrDoubleFinishSequence()
        {
            executor.SequenceFinished += (_, _) =>
            {
                Assert.That(executor.IsRunning, Is.False);
                executor.Cancel();
                executor.Tick(1f);
                Assert.That(executor.TryStart(Plan(1), target), Is.False);
            };
            executor.TryStart(Plan(1), target);
            Assert.That(completions, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void OwnerDeathInterruptsImmediately()
        {
            executor.TryStart(Plan(5), target);
            owner.TakePhysicalDamage(100f);
            Assert.That(executor.IsRunning, Is.False);
            Assert.That(completions, Is.EqualTo(new[] { false }));
        }

        [Test]
        public void DisableLifecycleCallbackInterruptsImmediately()
        {
            executor.TryStart(Plan(5), target);
            // Ordinary MonoBehaviours do not dispatch runtime lifecycle events in EditMode.
            typeof(AttackSequenceExecutor).GetMethod("OnDisable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(executor, null);
            Assert.That(executor.IsRunning, Is.False);
            Assert.That(completions, Is.EqualTo(new[] { false }));
        }

        private static AttackSequencePlan Plan(int shots, AttackSequenceKind kind = AttackSequenceKind.Basic,
            bool ignoreRange = false, float interval = 0.05f)
        {
            return new AttackSequencePlan(kind, shots, interval, 50f, 1.45f, 0.08f, 0.7f, 2f,
                kind != AttackSequenceKind.Charge, ignoreRange);
        }

        private CombatUnit CreateUnit(string name, TeamId team, Vector3 position)
        {
            GameObject item = new GameObject(name);
            objects.Add(item);
            item.transform.position = position;
            CombatUnit unit = item.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, 100f, 50f, 0f, 6f, 0.5f, true, false);
            return unit;
        }
    }
}
