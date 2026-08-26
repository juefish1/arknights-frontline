using ArknightsFrontline.Camera;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class MobaCameraControllerTests
    {
        [Test]
        public void EdgePanVelocityAtScreenCornerIsCappedAtMovementSpeed()
        {
            Vector3 velocity = MobaCameraController.CalculateEdgePanVelocity(
                new Vector2(0f, 0f),
                new Vector2(1920f, 1080f));

            Assert.That(velocity.magnitude, Is.EqualTo(18f).Within(0.0001f));
        }
    }
}
