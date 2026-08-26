using System;
using ArknightsFrontline.Commands;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class UnitCommandTests
    {
        [Test]
        public void AttackNearestInRangeCommandHasNoTargetOrDestination()
        {
            UnitCommand command = UnitCommand.AttackNearestInRange();

            Assert.That(command.Kind, Is.EqualTo(UnitCommandKind.AttackNearestInRange));
            Assert.That(command.TargetObject, Is.Null);
            Assert.That(command.Destination, Is.EqualTo(default(Vector3)));
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
