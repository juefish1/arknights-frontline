using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class CommandLayerRegistrationTests
    {
        [Test]
        public void CommandRaycastLayersAreRegisteredAtExpectedSlots()
        {
            Assert.That(LayerMask.NameToLayer("Ground"), Is.EqualTo(8));
            Assert.That(LayerMask.NameToLayer("Targetable"), Is.EqualTo(9));
            Assert.That(LayerMask.GetMask("Ground", "Targetable"), Is.EqualTo((1 << 8) | (1 << 9)));
        }
    }
}
