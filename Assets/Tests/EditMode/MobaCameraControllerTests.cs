using ArknightsFrontline.Camera;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class MobaCameraControllerTests
    {
        [Test]
        public void RightEdgePanFollowsDiagonalCameraScreenRight()
        {
            Vector3 velocity = MobaCameraController.CalculateEdgePanVelocity(
                new Vector2(1920f, 540f), new Vector2(1920f, 1080f));

            Assert.That(velocity.x, Is.GreaterThan(0f));
            Assert.That(velocity.z, Is.LessThan(0f));
            Assert.That(velocity.y, Is.Zero);
        }

        [TestCase(960f, 1080f, 1f, 1f)]
        [TestCase(960f, 0f, -1f, -1f)]
        [TestCase(0f, 540f, -1f, 1f)]
        public void EdgePanFollowsScreenAxes(float x, float y, float signX, float signZ)
        {
            Vector3 velocity = MobaCameraController.CalculateEdgePanVelocity(
                new Vector2(x, y), new Vector2(1920f, 1080f));
            Assert.That(velocity.x * signX, Is.GreaterThan(0f));
            Assert.That(velocity.z * signZ, Is.GreaterThan(0f));
            Assert.That(velocity.magnitude, Is.EqualTo(18f).Within(0.0001f));
        }

        [Test]
        public void ScreenCenterDoesNotPan()
        {
            Assert.That(MobaCameraController.CalculateEdgePanVelocity(
                new Vector2(960f, 540f), new Vector2(1920f, 1080f)), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void CenterOnPlacesTargetGroundPositionAtViewportCenter()
        {
            GameObject cameraObject = new GameObject("CenteringCamera");
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            MobaCameraController controller = cameraObject.AddComponent<MobaCameraController>();
            GameObject target = new GameObject("Target");
            try
            {
                controller.ConfigureOffset(new Vector3(-14f, 28f, -14f));
                target.transform.position = new Vector3(10f, 2f, 5f);
                controller.CenterOn(target.transform);
                Vector3 viewport = camera.WorldToViewportPoint(new Vector3(10f, 0f, 5f));
                Assert.That(viewport.z, Is.GreaterThan(0f));
                Assert.That(viewport.x, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(viewport.y, Is.EqualTo(0.5f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

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
