using System.Collections.Generic;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class BasicAttackControllerTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in gameObjects)
            {
                Object.DestroyImmediate(gameObject);
            }

            gameObjects.Clear();
        }

        [Test]
        public void AttackTimerRaisesOneRequestPerIntervalForInRangeLegalTarget()
        {
            CombatUnit player = CreateUnit("Player", TeamId.Blue, Vector3.zero, 5f, 0.5f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, 1f, false);
            BasicAttackController attack = player.gameObject.AddComponent<BasicAttackController>();
            attack.Configure(player);
            int requestCount = 0;
            attack.AttackRequested += (_, _) => requestCount++;

            attack.SetTarget(target);
            attack.Tick(0f);
            attack.Tick(0.49f);
            attack.Tick(0.01f);

            Assert.That(requestCount, Is.EqualTo(2));
        }

        [Test]
        public void LongTickEmitsAtMostOneRequestAndRestartsFullInterval()
        {
            CombatUnit player = CreateUnit("Player", TeamId.Blue, Vector3.zero, 5f, 0.5f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, 1f, false);
            BasicAttackController attack = player.gameObject.AddComponent<BasicAttackController>();
            attack.Configure(player);
            int requestCount = 0;
            attack.AttackRequested += (_, _) => requestCount++;

            attack.SetTarget(target);
            attack.Tick(0f);
            attack.Tick(1.2f);

            Assert.That(requestCount, Is.EqualTo(2));

            attack.Tick(0.49f);
            Assert.That(requestCount, Is.EqualTo(2));
            attack.Tick(0.01f);
            Assert.That(requestCount, Is.EqualTo(3));
        }

        [Test]
        public void CatchUpStopsWhenAnAttackCallbackInvalidatesTarget()
        {
            CombatUnit player = CreateUnit("Player", TeamId.Blue, Vector3.zero, 5f, 0.5f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, 1f, false);
            BasicAttackController attack = player.gameObject.AddComponent<BasicAttackController>();
            attack.Configure(player);
            int requestCount = 0;
            attack.AttackRequested += (_, _) => requestCount++;
            attack.SetTarget(target);
            attack.Tick(0f);
            attack.AttackRequested += (_, requestedTarget) => requestedTarget.TakePhysicalDamage(100f);

            attack.Tick(1.2f);

            Assert.That(requestCount, Is.EqualTo(2));
            Assert.That(attack.CurrentTarget, Is.Null);
        }

        [Test]
        public void InitialRequestClearsTargetWhenCallbackInvalidatesIt()
        {
            CombatUnit owner = CreateUnit("Player", TeamId.Blue, Vector3.zero, 5f, 0.5f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, 1f, false);
            BasicAttackController attack = owner.gameObject.AddComponent<BasicAttackController>();
            attack.Configure(owner);
            attack.SetTarget(target);
            attack.AttackRequested += (_, requestedTarget) => requestedTarget.TakePhysicalDamage(100f);

            attack.Tick(0f);

            Assert.That(attack.CurrentTarget, Is.Null);
        }

        [Test]
        public void ZeroIntervalRequestClearsTargetWhenCallbackInvalidatesIt()
        {
            CombatUnit owner = CreateUnit("Player", TeamId.Blue, Vector3.zero, 5f, 0f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, 1f, false);
            BasicAttackController attack = owner.gameObject.AddComponent<BasicAttackController>();
            attack.Configure(owner);
            attack.SetTarget(target);
            attack.Tick(0f);
            attack.AttackRequested += (_, requestedTarget) => requestedTarget.TakePhysicalDamage(100f);

            attack.Tick(0f);

            Assert.That(attack.CurrentTarget, Is.Null);
        }

        [Test]
        public void InvalidTargetStopsFutureAttackRequests()
        {
            CombatUnit player = CreateUnit("Player", TeamId.Blue, Vector3.zero, 5f, 0.5f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, 1f, false);
            BasicAttackController attack = player.gameObject.AddComponent<BasicAttackController>();
            attack.Configure(player);
            int requestCount = 0;
            attack.AttackRequested += (_, _) => requestCount++;
            attack.SetTarget(target);
            attack.Tick(0f);

            target.TakePhysicalDamage(100f);
            attack.Tick(1f);

            Assert.That(requestCount, Is.EqualTo(1));
        }

        [Test]
        public void OwnerDeathClearsAttackStateImmediately()
        {
            CombatUnit player = CreateUnit("Player", TeamId.Blue, Vector3.zero, 5f, 0.5f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, 1f, false);
            BasicAttackController attack = player.gameObject.AddComponent<BasicAttackController>();
            attack.Configure(player);
            int requestCount = 0;
            attack.AttackRequested += (_, _) => requestCount++;
            attack.SetTarget(target);

            player.TakePhysicalDamage(100f);

            Assert.That(attack.CurrentTarget, Is.Null);
            attack.Tick(0f);

            Assert.That(requestCount, Is.EqualTo(0));
        }

        [Test]
        public void ZeroIntervalEmitsAtMostOneRequestPerTick()
        {
            CombatUnit player = CreateUnit("Player", TeamId.Blue, Vector3.zero, 5f, 0f, true);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, 1f, false);
            BasicAttackController attack = player.gameObject.AddComponent<BasicAttackController>();
            attack.Configure(player);
            int requestCount = 0;
            attack.AttackRequested += (_, _) => requestCount++;
            attack.SetTarget(target);

            attack.Tick(2f);

            Assert.That(requestCount, Is.EqualTo(1));
        }

        [Test]
        public void ConfiguredSequencePreventsOverlapAndMeasuresIntervalFromStart()
        {
            CreateSequenceAttack(out CombatUnit owner, out CombatUnit target, out BasicAttackController attack,
                out AttackSequenceExecutor executor);
            attack.SetPlanProvider(() => CreatePlan(5));
            int starts = 0;
            attack.AttackRequested += (_, _) => starts++;
            attack.Tick(0f);
            attack.Tick(0.2f);
            Assert.That(starts, Is.EqualTo(1));
            executor.Tick(0.2f);
            attack.Tick(0.299f);
            Assert.That(starts, Is.EqualTo(1));
            attack.Tick(0.001f);
            Assert.That(starts, Is.EqualTo(2));
        }

        [Test]
        public void SequenceLongerThanIntervalCanRestartOnlyAfterItCompletes()
        {
            CreateSequenceAttack(out CombatUnit owner, out CombatUnit target, out BasicAttackController attack,
                out AttackSequenceExecutor executor);
            attack.SetPlanProvider(() => CreatePlan(5));
            int starts = 0;
            attack.AttackRequested += (_, _) => starts++;
            attack.Tick(0f);
            attack.Tick(2f);
            Assert.That(starts, Is.EqualTo(1));
            executor.Tick(0.2f);
            attack.Tick(0f);
            Assert.That(starts, Is.EqualTo(2));
        }

        [Test]
        public void OneShotProviderUsesCurrentEffectiveIntervalAndInvokesStartEvent()
        {
            CreateSequenceAttack(out CombatUnit owner, out CombatUnit target, out BasicAttackController attack,
                out AttackSequenceExecutor executor);
            int provided = 0;
            int starts = 0;
            attack.SetPlanProvider(() => { provided++; return CreatePlan(1); });
            attack.AttackRequested += (_, _) => starts++;
            attack.Tick(0f);
            Assert.That(executor.IsRunning, Is.False);
            attack.Tick(0.25f);
            owner.gameObject.AddComponent<UnitStatModifiers>().SetAttackIntervalOffset("R", -0.22f);
            attack.Tick(0.029f);
            Assert.That(starts, Is.EqualTo(1));
            attack.Tick(0.001f);
            Assert.That(starts, Is.EqualTo(2));
            Assert.That(provided, Is.EqualTo(2));
        }

        [Test]
        public void DefaultSequencePlanUsesEffectiveOwnerAttackPower()
        {
            CreateSequenceAttack(out CombatUnit owner, out CombatUnit target, out BasicAttackController attack,
                out AttackSequenceExecutor executor);
            owner.gameObject.AddComponent<UnitStatModifiers>().SetAttackPowerMultiplier("R", 1.1f);
            PhysicalDamagePayload observed = default;
            AttackSequencePlan finished = null;
            executor.ShotRequested += (_, payload) => observed = payload;
            executor.SequenceFinished += (plan, _) => finished = plan;
            attack.Tick(0f);
            Assert.That(observed.AttackPower, Is.EqualTo(1.1f));
            Assert.That(observed.DamageMultiplier, Is.EqualTo(1f));
            Assert.That(observed.MissingHealthRatio, Is.Zero);
            Assert.That(finished.Kind, Is.EqualTo(AttackSequenceKind.Basic));
            Assert.That(finished.CountsAsBasicAttack, Is.True);
            Assert.That(finished.ShotCount, Is.EqualTo(1));
        }

        [Test]
        public void InterruptedSequenceClearsTargetAndDoesNotResumeOnReentry()
        {
            CreateSequenceAttack(out CombatUnit owner, out CombatUnit target, out BasicAttackController attack,
                out AttackSequenceExecutor executor);
            attack.SetPlanProvider(() => CreatePlan(5));
            int starts = 0;
            attack.AttackRequested += (_, _) => starts++;
            attack.Tick(0f);
            target.transform.position = Vector3.right * 20f;
            executor.Tick(0.05f);
            Assert.That(attack.CurrentTarget, Is.Null);
            target.transform.position = Vector3.right;
            attack.Tick(10f);
            Assert.That(starts, Is.EqualTo(1));
        }

        [Test]
        public void DeadTargetBetweenShotsDoesNotCancelRetargeting()
        {
            CreateSequenceAttack(out CombatUnit owner, out CombatUnit target, out BasicAttackController attack,
                out AttackSequenceExecutor executor);
            CombatUnit replacement = CreateUnit("Replacement", TeamId.Red, Vector3.right, 1f, 1f, false);
            attack.SetPlanProvider(() => CreatePlan(3));
            List<CombatUnit> shots = new List<CombatUnit>();
            executor.ShotRequested += (shotTarget, _) => shots.Add(shotTarget);
            attack.Tick(0f);
            target.TakePhysicalDamage(100f);
            attack.Tick(0.05f);
            executor.Tick(0.1f);
            Assert.That(shots, Is.EqualTo(new[] { target, replacement, replacement }));
            Assert.That(attack.CurrentTarget, Is.SameAs(replacement));
        }

        [Test]
        public void ClearTargetCancelsOwnedSequenceButNotIndependentSkillSequence()
        {
            CreateSequenceAttack(out CombatUnit owner, out CombatUnit target, out BasicAttackController attack,
                out AttackSequenceExecutor executor);
            attack.SetPlanProvider(() => CreatePlan(3));
            attack.Tick(0f);
            attack.ClearTarget();
            Assert.That(executor.IsRunning, Is.False);
            executor.TryStart(new AttackSequencePlan(AttackSequenceKind.Charge, 4, 0.05f,
                1f, 1.25f, 0f, 0.7f, 2f, false, true), target);
            attack.ClearTarget();
            Assert.That(executor.IsRunning, Is.True);
        }

        private void CreateSequenceAttack(out CombatUnit owner, out CombatUnit target,
            out BasicAttackController attack, out AttackSequenceExecutor executor)
        {
            owner = CreateUnit("Player", TeamId.Blue, Vector3.zero, 5f, 0.5f, true);
            target = CreateUnit("Target", TeamId.Red, Vector3.right * 2f, 1f, 1f, false);
            executor = owner.gameObject.AddComponent<AttackSequenceExecutor>();
            executor.Configure(owner);
            attack = owner.gameObject.AddComponent<BasicAttackController>();
            attack.Configure(owner, executor);
            attack.SetTarget(target);
        }

        private static AttackSequencePlan CreatePlan(int shots)
        {
            return new AttackSequencePlan(AttackSequenceKind.Basic, shots, 0.05f, 1f, 1f, 0f, 1f, 0f, true, false);
        }

        private CombatUnit CreateUnit(string name, TeamId team, Vector3 position, float attackRange, float attackInterval, bool canAttackGround)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            gameObject.transform.position = position;
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, 10f, 1f, 0f, attackRange, attackInterval, canAttackGround, false);
            return unit;
        }
    }
}
