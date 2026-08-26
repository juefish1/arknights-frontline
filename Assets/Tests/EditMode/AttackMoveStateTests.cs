using ArknightsFrontline.Commands;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class AttackMoveStateTests
    {
        [Test]
        public void ConfirmStoresDestinationAndDisarmsAttackMove()
        {
            AttackMoveState state = new AttackMoveState();
            Vector3 destination = new Vector3(3f, 0f, 1f);

            state.Arm();
            state.Confirm(destination);

            Assert.That(state.IsArmed, Is.False);
            Assert.That(state.LastDestination, Is.EqualTo(destination));
        }

        [Test]
        public void CancelDisarmsAttackMoveWithoutChangingLastDestination()
        {
            AttackMoveState state = new AttackMoveState();
            state.Arm();
            state.Confirm(new Vector3(3f, 0f, 1f));
            state.Arm();

            state.Cancel();

            Assert.That(state.IsArmed, Is.False);
            Assert.That(state.LastDestination, Is.EqualTo(new Vector3(3f, 0f, 1f)));
        }
    }
}
