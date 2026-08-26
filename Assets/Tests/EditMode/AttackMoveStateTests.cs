using ArknightsFrontline.Commands;
using NUnit.Framework;
namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class AttackMoveStateTests
    {
        [Test]
        public void ConfirmDisarmsAttackMove()
        {
            AttackMoveState state = new AttackMoveState();

            state.Arm();
            state.Confirm();

            Assert.That(state.IsArmed, Is.False);
        }

        [Test]
        public void CancelDisarmsAttackMove()
        {
            AttackMoveState state = new AttackMoveState();
            state.Arm();

            state.Cancel();

            Assert.That(state.IsArmed, Is.False);
        }
    }
}
