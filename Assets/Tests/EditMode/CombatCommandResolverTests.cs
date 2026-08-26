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
