using ArknightsFrontline.Arena;
using ArknightsFrontline.Commands;
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
            Object.DestroyImmediate(player);
        }
    }
}
