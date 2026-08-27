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

        [Test]
        public void ConfiguredOffsetPreservesInitialPositionWhenAwakeRuns()
        {
            GameObject cameraObject = new GameObject("ConfiguredCamera");
            cameraObject.SetActive(false);
            Vector3 requiredPosition = new Vector3(0f, 42f, -34f);
            cameraObject.transform.position = requiredPosition;
            MobaCameraController controller = cameraObject.AddComponent<MobaCameraController>();

            try
            {
                controller.ConfigureOffset(requiredPosition);
                cameraObject.SetActive(true);

                Assert.That(cameraObject.transform.position, Is.EqualTo(requiredPosition));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
