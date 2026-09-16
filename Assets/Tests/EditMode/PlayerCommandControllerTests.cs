using ArknightsFrontline.Arena;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class PlayerCommandControllerTests
    {
        [Test]
        public void ArmedRightClickIsCancelledByTheMoveClickPathBeforeRaycasting()
        {
            GameObject player = new GameObject("Player");
            UnitMotor motor = player.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());
            PlayerCommandController controller = player.AddComponent<PlayerCommandController>();

            controller.ArmAttackMove();
            controller.HandleMoveClick();

            Assert.That(controller.IsAttackMoveArmed, Is.False);
            Assert.That(controller.CurrentCommand.HasValue, Is.False);
            Object.DestroyImmediate(player);
        }

        [Test]
        public void MoveToAttackStopsMotorAndStoresTarget()
        {
            PlayerCommandController controller = CreateController(out UnitMotor motor, out GameObject player);
            GameObject target = new GameObject("Target");
            controller.Issue(UnitCommand.Move(new Vector3(10f, 0f, 0f)));

            controller.Issue(UnitCommand.Attack(target));

            Assert.That(motor.IsMoving, Is.False);
            Assert.That(controller.CurrentTarget, Is.EqualTo(target));
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(player);
        }

        [Test]
        public void AttackToMoveClearsTargetAndStartsMotor()
        {
            PlayerCommandController controller = CreateController(out UnitMotor motor, out GameObject player);
            GameObject target = new GameObject("Target");
            controller.Issue(UnitCommand.Attack(target));

            controller.Issue(UnitCommand.Move(new Vector3(10f, 0f, 0f)));

            Assert.That(controller.CurrentTarget, Is.Null);
            Assert.That(motor.IsMoving, Is.True);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(player);
        }

        [Test]
        public void MoveToAttackNearestInRangeStopsMotorAndClearsTarget()
        {
            PlayerCommandController controller = CreateController(out UnitMotor motor, out GameObject player);
            controller.Issue(UnitCommand.Move(new Vector3(10f, 0f, 0f)));

            controller.Issue(UnitCommand.AttackNearestInRange());

            Assert.That(controller.CurrentTarget, Is.Null);
            Assert.That(motor.IsMoving, Is.False);
            Assert.That(controller.CurrentCommand.HasValue, Is.True);
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.AttackNearestInRange));
            Object.DestroyImmediate(player);
        }

        [Test]
        public void AttackToStopClearsTargetAndStopsMotor()
        {
            PlayerCommandController controller = CreateController(out UnitMotor motor, out GameObject player);
            GameObject target = new GameObject("Target");
            controller.Issue(UnitCommand.Move(new Vector3(10f, 0f, 0f)));
            controller.Issue(UnitCommand.Attack(target));

            controller.Issue(UnitCommand.Stop());

            Assert.That(controller.CurrentTarget, Is.Null);
            Assert.That(motor.IsMoving, Is.False);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(player);
        }

        [Test]
        public void DeadPlayerRejectsNewCommandsWithoutReplacingCurrentCommand()
        {
            PlayerCommandController controller = CreateController(out UnitMotor motor, out GameObject player);
            CombatUnit unit = player.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Blue, Altitude.Ground, 10f, 1f, 0f, 1f, 1f, true, false);
            controller.Issue(UnitCommand.Stop());
            unit.TakePhysicalDamage(100f);

            controller.Issue(UnitCommand.Move(new Vector3(10f, 0f, 0f)));

            Assert.That(motor.IsMoving, Is.False);
            Assert.That(controller.CurrentCommand.HasValue, Is.True);
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.Stop));
            Object.DestroyImmediate(player);
        }

        [Test]
        public void CancelCurrentCommandClearsCommandTargetAndMovement()
        {
            PlayerCommandController controller = CreateController(out UnitMotor motor, out GameObject player);
            GameObject target = new GameObject("Target");
            controller.Issue(UnitCommand.Attack(target));
            int revisionAfterIssue = controller.CommandRevision;
            motor.SetDestination(new Vector3(10f, 0f, 0f));

            controller.CancelCurrentCommand();

            Assert.That(controller.CurrentCommand, Is.Null);
            Assert.That(controller.CurrentTarget, Is.Null);
            Assert.That(motor.IsMoving, Is.False);
            Assert.That(controller.CommandRevision, Is.GreaterThan(revisionAfterIssue));
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(player);
        }

        [Test]
        public void IssuingSameCommandAgainAdvancesCommandRevision()
        {
            PlayerCommandController controller = CreateController(out _, out GameObject player);
            GameObject target = new GameObject("Target");
            controller.Issue(UnitCommand.Attack(target));
            int firstRevision = controller.CommandRevision;

            controller.Issue(UnitCommand.Attack(target));

            Assert.That(controller.CommandRevision, Is.GreaterThan(firstRevision));
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(player);
        }

        private static PlayerCommandController CreateController(out UnitMotor motor, out GameObject player)
        {
            player = new GameObject("Player");
            motor = player.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());
            PlayerCommandController controller = player.AddComponent<PlayerCommandController>();
            typeof(PlayerCommandController)
                .GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(controller, null);
            return controller;
        }
    }
}
