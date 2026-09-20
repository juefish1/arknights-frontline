using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class CombatCommandResolverTests
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
        public void SpecifiedTargetOutsideRangeMovesUntilInRangeThenArmsAttack()
        {
            CreatePlayer(out GameObject player, out UnitMotor motor, out PlayerCommandController controller,
                out CombatCommandResolver resolver, out BasicAttackController attack);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(8f, 0f, 0f), 1f, false);
            controller.Issue(UnitCommand.Attack(target.gameObject));

            resolver.Tick(0f);

            Assert.That(motor.IsMoving, Is.True);
            Assert.That(resolver.CurrentTarget, Is.Null);

            player.transform.position = new Vector3(target.transform.position.x - 4f, 0f, 0f);
            resolver.Tick(0f);

            Assert.That(motor.IsMoving, Is.False);
            Assert.That(resolver.CurrentTarget, Is.EqualTo(target));
            int requestCount = 0;
            attack.AttackRequested += (_, _) => requestCount++;
            attack.Tick(0f);
            Assert.That(requestCount, Is.EqualTo(1));
        }

        [Test]
        public void NearestIntentNeverSetsDestinationWhenNoLegalTargetIsInRange()
        {
            CreatePlayer(out _, out UnitMotor motor, out PlayerCommandController controller,
                out CombatCommandResolver resolver, out _);
            CreateUnit("FarTarget", TeamId.Red, new Vector3(8f, 0f, 0f), 1f, false);
            controller.Issue(UnitCommand.AttackNearestInRange());

            resolver.Tick(0f);

            Assert.That(resolver.CurrentTarget, Is.Null);
            Assert.That(motor.IsMoving, Is.False);
            Assert.That(controller.CurrentCommand, Is.Null);
        }

        [Test]
        public void EngagedSpecifiedTargetLeavingRangeCancelsCommandAndDoesNotReacquire()
        {
            CreatePlayer(out _, out UnitMotor motor, out PlayerCommandController controller,
                out CombatCommandResolver resolver, out BasicAttackController attack);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, false);
            int requestCount = 0;
            attack.AttackRequested += (_, _) => requestCount++;
            controller.Issue(UnitCommand.Attack(target.gameObject));
            resolver.Tick(0f);
            attack.Tick(0f);
            Assert.That(requestCount, Is.EqualTo(1));

            target.transform.position = new Vector3(8f, 0f, 0f);
            resolver.Tick(0f);

            Assert.That(controller.CurrentCommand, Is.Null);
            Assert.That(controller.CurrentTarget, Is.Null);
            Assert.That(resolver.CurrentTarget, Is.Null);
            Assert.That(attack.CurrentTarget, Is.Null);
            Assert.That(motor.IsMoving, Is.False);

            target.transform.position = new Vector3(2f, 0f, 0f);
            resolver.Tick(10f);
            attack.Tick(10f);
            Assert.That(requestCount, Is.EqualTo(1));

            controller.Issue(UnitCommand.Attack(target.gameObject));
            resolver.Tick(0f);
            attack.Tick(0f);
            Assert.That(requestCount, Is.EqualTo(2));
        }

        [Test]
        public void NearestIntentDoesNotRetargetAfterEngagedTargetLeavesRange()
        {
            CreatePlayer(out _, out UnitMotor motor, out PlayerCommandController controller,
                out CombatCommandResolver resolver, out BasicAttackController attack);
            CombatUnit first = CreateUnit("First", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, false);
            controller.Issue(UnitCommand.AttackNearestInRange());
            resolver.Tick(0f);
            Assert.That(resolver.CurrentTarget, Is.EqualTo(first));

            first.transform.position = new Vector3(8f, 0f, 0f);
            CreateUnit("Replacement", TeamId.Red, new Vector3(1f, 0f, 0f), 1f, false);
            resolver.Tick(0f);

            Assert.That(controller.CurrentCommand, Is.Null);
            Assert.That(resolver.CurrentTarget, Is.Null);
            Assert.That(attack.CurrentTarget, Is.Null);
            Assert.That(motor.IsMoving, Is.False);
        }

        [Test]
        public void InvalidatingSpecifiedTargetClearsCombatTarget()
        {
            CreatePlayer(out _, out _, out PlayerCommandController controller,
                out CombatCommandResolver resolver, out BasicAttackController attack);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, false);
            controller.Issue(UnitCommand.Attack(target.gameObject));
            resolver.Tick(0f);

            target.TakePhysicalDamage(100f);
            resolver.Tick(0f);
            attack.Tick(0f);

            Assert.That(resolver.CurrentTarget, Is.Null);
            Assert.That(CountAttackRequests(attack), Is.EqualTo(0));
        }

        [TestCase(UnitCommandKind.Move)]
        [TestCase(UnitCommandKind.Stop)]
        public void MoveAndStopClearActiveAttackState(UnitCommandKind kind)
        {
            CreatePlayer(out _, out UnitMotor motor, out PlayerCommandController controller,
                out CombatCommandResolver resolver, out BasicAttackController attack);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, false);
            controller.Issue(UnitCommand.Attack(target.gameObject));
            resolver.Tick(0f);

            controller.Issue(kind == UnitCommandKind.Move
                ? UnitCommand.Move(new Vector3(10f, 0f, 5f))
                : UnitCommand.Stop());
            resolver.Tick(0f);
            Vector3 positionBeforeMotorTick = motor.transform.position;
            motor.Tick(0.1f);
            int requestCount = CountAttackRequests(attack);

            Assert.That(resolver.CurrentTarget, Is.Null);
            Assert.That(requestCount, Is.EqualTo(0));
            Assert.That(motor.IsMoving, Is.EqualTo(kind == UnitCommandKind.Move));
            if (kind == UnitCommandKind.Move)
            {
                Assert.That(motor.transform.position.x, Is.GreaterThan(positionBeforeMotorTick.x));
                Assert.That(motor.transform.position.z, Is.GreaterThan(positionBeforeMotorTick.z));
            }
        }

        [Test]
        public void NearestIntentSelectsClosestLegalTargetCurrentlyInRange()
        {
            CreatePlayer(out _, out UnitMotor motor, out PlayerCommandController controller,
                out CombatCommandResolver resolver, out _);
            CombatUnit farther = CreateUnit("Farther", TeamId.Red, new Vector3(4f, 0f, 0f), 1f, false);
            CombatUnit nearest = CreateUnit("Nearest", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, false);
            CreateUnit("Friendly", TeamId.Blue, new Vector3(1f, 0f, 0f), 1f, false);
            controller.Issue(UnitCommand.AttackNearestInRange());

            resolver.Tick(0f);

            Assert.That(resolver.CurrentTarget, Is.EqualTo(nearest));
            Assert.That(resolver.CurrentTarget, Is.Not.EqualTo(farther));
            Assert.That(motor.IsMoving, Is.False);
        }

        [Test]
        public void OwnerDeathImmediatelyStopsMotorAndClearsResolverAndAttackTargets()
        {
            CreatePlayer(out GameObject player, out UnitMotor motor, out PlayerCommandController controller,
                out CombatCommandResolver resolver, out BasicAttackController attack);
            CombatUnit owner = player.GetComponent<CombatUnit>();
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, false);
            controller.Issue(UnitCommand.Attack(target.gameObject));
            resolver.Tick(0f);
            motor.SetDestination(new Vector3(10f, 0f, 0f));

            owner.TakePhysicalDamage(100f);

            Assert.That(motor.IsMoving, Is.False);
            Assert.That(resolver.CurrentTarget, Is.Null);
            Assert.That(attack.CurrentTarget, Is.Null);
        }

        [Test]
        public void PursuitRefreshesDestinationWhenTargetMoves()
        {
            CreatePlayer(out GameObject player, out UnitMotor motor, out PlayerCommandController controller,
                out CombatCommandResolver resolver, out _);
            CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(8f, 0f, 0f), 1f, false);
            controller.Issue(UnitCommand.Attack(target.gameObject));
            resolver.Tick(0f);
            target.transform.position = new Vector3(8f, 0f, 8f);

            resolver.Tick(0f);
            motor.Tick(0.1f);

            Assert.That(player.transform.position.x, Is.GreaterThan(0f));
            Assert.That(player.transform.position.z, Is.GreaterThan(0f));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void MultishotDeathRetargetsWithoutCancellingCommandInEitherTickOrder(bool nearest, bool resolverFirst)
        {
            CreateSequencePlayer(out _, out PlayerCommandController commands, out CombatCommandResolver resolver,
                out BasicAttackController attack, out AttackSequenceExecutor executor);
            CombatUnit original = CreateUnit("Original", TeamId.Red, Vector3.right, 1f, false);
            CombatUnit replacement = CreateUnit("Replacement", TeamId.Red, Vector3.right * 2f, 1f, false);
            List<CombatUnit> shots = new List<CombatUnit>();
            executor.ShotRequested += (target, _) => shots.Add(target);
            commands.Issue(nearest ? UnitCommand.AttackNearestInRange() : UnitCommand.Attack(original.gameObject));
            resolver.Tick(0f);
            attack.Tick(0f);
            original.TakePhysicalDamage(100f);

            if (resolverFirst) resolver.Tick(0.05f);
            attack.Tick(0.05f);
            executor.Tick(0.05f);
            resolver.Tick(0f);

            Assert.That(commands.CurrentCommand.HasValue, Is.True);
            Assert.That(executor.IsRunning, Is.True);
            Assert.That(resolver.CurrentTarget, Is.SameAs(replacement));
            executor.Tick(0.05f);
            resolver.Tick(0f);
            Assert.That(shots, Is.EqualTo(new[] { original, replacement, replacement }));
            Assert.That(commands.CurrentCommand.HasValue, Is.True);
            attack.Tick(0.45f);
            Assert.That(shots.Count, Is.EqualTo(4));
            Assert.That(shots[3], Is.SameAs(replacement));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void FailedSequenceCannotReviveOldCommandAfterTargetReentersRange(bool nearest, bool resolverFirst)
        {
            CreateSequencePlayer(out _, out PlayerCommandController commands, out CombatCommandResolver resolver,
                out BasicAttackController attack, out AttackSequenceExecutor executor);
            CombatUnit target = CreateUnit("Target", TeamId.Red, Vector3.right, 1f, false);
            int shots = 0;
            executor.ShotRequested += (_, _) => shots++;
            commands.Issue(nearest ? UnitCommand.AttackNearestInRange() : UnitCommand.Attack(target.gameObject));
            resolver.Tick(0f);
            attack.Tick(0f);
            target.transform.position = Vector3.right * 10f;
            if (resolverFirst) resolver.Tick(0f);
            executor.Tick(0.05f);
            target.transform.position = Vector3.right;
            // A basic update before the resolver must not erase the pending interruption.
            attack.Tick(0.05f);
            resolver.Tick(0f);
            attack.Tick(1f);

            Assert.That(commands.CurrentCommand, Is.Null);
            Assert.That(resolver.CurrentTarget, Is.Null);
            Assert.That(attack.CurrentTarget, Is.Null);
            Assert.That(shots, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExplicitSequenceCancelInvalidatesItsCommandOnlyOnce(bool nearest)
        {
            CreateSequencePlayer(out _, out PlayerCommandController commands, out CombatCommandResolver resolver,
                out BasicAttackController attack, out AttackSequenceExecutor executor);
            CombatUnit target = CreateUnit("Target", TeamId.Red, Vector3.right, 1f, false);
            UnitCommand command = nearest ? UnitCommand.AttackNearestInRange() : UnitCommand.Attack(target.gameObject);
            commands.Issue(command);
            resolver.Tick(0f);
            attack.Tick(0f);
            executor.Cancel();
            resolver.Tick(0f);
            Assert.That(commands.CurrentCommand, Is.Null);

            commands.Issue(command);
            resolver.Tick(0f);
            attack.Tick(0f);
            resolver.Tick(0f);
            Assert.That(commands.CurrentCommand.HasValue, Is.True);
            Assert.That(executor.IsRunning, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NoReplacementFailureCancelsCommandInEitherTickOrder(bool resolverFirst)
        {
            CreateSequencePlayer(out _, out PlayerCommandController commands, out CombatCommandResolver resolver,
                out BasicAttackController attack, out AttackSequenceExecutor executor);
            CombatUnit target = CreateUnit("Target", TeamId.Red, Vector3.right, 1f, false);
            commands.Issue(UnitCommand.AttackNearestInRange());
            resolver.Tick(0f);
            attack.Tick(0f);
            target.TakePhysicalDamage(100f);
            if (resolverFirst) resolver.Tick(0f);
            executor.Tick(0.05f);
            resolver.Tick(0f);
            Assert.That(commands.CurrentCommand, Is.Null);
            Assert.That(executor.IsRunning, Is.False);
        }

        [TestCase(UnitCommandKind.Move)]
        [TestCase(UnitCommandKind.Stop)]
        [TestCase(UnitCommandKind.Attack)]
        [TestCase(UnitCommandKind.AttackNearestInRange)]
        public void NewCommandRevisionCancelsOwnedSequenceAndDiscardsPreviousInterruption(UnitCommandKind kind)
        {
            CreateSequencePlayer(out _, out PlayerCommandController commands, out CombatCommandResolver resolver,
                out BasicAttackController attack, out AttackSequenceExecutor executor);
            CombatUnit target = CreateUnit("Target", TeamId.Red, Vector3.right, 1f, false);
            commands.Issue(UnitCommand.Attack(target.gameObject));
            resolver.Tick(0f);
            attack.Tick(0f);
            UnitCommand next = kind == UnitCommandKind.Move ? UnitCommand.Move(Vector3.right * 3f)
                : kind == UnitCommandKind.Stop ? UnitCommand.Stop()
                : kind == UnitCommandKind.Attack ? UnitCommand.Attack(target.gameObject)
                : UnitCommand.AttackNearestInRange();
            commands.Issue(next);
            resolver.Tick(0f);
            Assert.That(executor.IsRunning, Is.False);
            Assert.That(commands.CurrentCommand.Value.Kind, Is.EqualTo(kind));

            commands.Issue(UnitCommand.Attack(target.gameObject));
            resolver.Tick(0f);
            attack.Tick(0f);
            executor.Cancel();
            // A newer attack revision supersedes the failed one before its signal is consumed.
            commands.Issue(UnitCommand.Attack(target.gameObject));
            resolver.Tick(0f);
            attack.Tick(0f);
            resolver.Tick(0f);
            Assert.That(commands.CurrentCommand.HasValue, Is.True);
            Assert.That(executor.IsRunning, Is.True);
        }

        [Test]
        public void ResolverClearingOrdinaryCommandDoesNotCancelIndependentCharge()
        {
            CreateSequencePlayer(out _, out PlayerCommandController commands, out CombatCommandResolver resolver,
                out _, out AttackSequenceExecutor executor);
            CombatUnit target = CreateUnit("Target", TeamId.Red, Vector3.right, 1f, false);
            executor.TryStart(new AttackSequencePlan(AttackSequenceKind.Charge, 4, 0.05f,
                1f, 1.25f, 0f, 0.7f, 2f, false, true), target);
            commands.Issue(UnitCommand.Stop());
            resolver.Tick(0f);
            Assert.That(executor.IsRunning, Is.True);
        }

        private void CreateSequencePlayer(out GameObject player, out PlayerCommandController commands,
            out CombatCommandResolver resolver, out BasicAttackController attack, out AttackSequenceExecutor executor)
        {
            CreatePlayer(out player, out _, out commands, out resolver, out attack);
            executor = player.AddComponent<AttackSequenceExecutor>();
            executor.Configure(player.GetComponent<CombatUnit>());
            attack.Configure(player.GetComponent<CombatUnit>(), executor);
            attack.SetPlanProvider(() => new AttackSequencePlan(AttackSequenceKind.Basic, 3, 0.05f,
                1f, 1f, 0f, 1f, 0f, true, false));
        }

        private void CreatePlayer(
            out GameObject player,
            out UnitMotor motor,
            out PlayerCommandController controller,
            out CombatCommandResolver resolver,
            out BasicAttackController attack)
        {
            player = new GameObject("Player");
            gameObjects.Add(player);
            motor = player.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());
            controller = player.AddComponent<PlayerCommandController>();
            typeof(PlayerCommandController)
                .GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(controller, null);
            CombatUnit unit = player.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Blue, Altitude.Ground, 10f, 1f, 0f, 5f, 0.5f, true, false);
            resolver = player.AddComponent<CombatCommandResolver>();
            attack = player.AddComponent<BasicAttackController>();
            attack.Configure(unit);
            resolver.Configure(unit, motor, controller, attack);
        }

        private CombatUnit CreateUnit(string name, TeamId team, Vector3 position, float attackRange, bool canAttackGround)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            gameObject.transform.position = position;
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, 10f, 1f, 0f, attackRange, 0.5f, canAttackGround, false);
            return unit;
        }

        private static int CountAttackRequests(BasicAttackController attack)
        {
            int requestCount = 0;
            attack.AttackRequested += (_, _) => requestCount++;
            attack.Tick(0f);
            return requestCount;
        }
    }
}
