using System;
using ArknightsFrontline.Commands;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class UnitCommandTests
    {
        [Test]
        public void AttackMoveCommandCarriesDestination()
        {
            UnitCommand command = UnitCommand.AttackMove(new Vector3(4f, 0f, 2f));

            Assert.That(command.Kind, Is.EqualTo(UnitCommandKind.AttackMove));
            Assert.That(command.Destination, Is.EqualTo(new Vector3(4f, 0f, 2f)));
        }

        [Test]
        public void AttackCommandRejectsNullTarget()
        {
            Assert.That(
                () => UnitCommand.Attack(null),
                Throws.TypeOf<ArgumentNullException>());
        }
    }
}
