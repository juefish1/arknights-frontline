using ArknightsFrontline.Arena;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class DashPathResolverTests
    {
        private GameObject obstacle;

        [TearDown]
        public void TearDown()
        {
            if (obstacle != null)
            {
                Object.DestroyImmediate(obstacle);
                Physics.SyncTransforms();
            }
        }

        [Test]
        public void ClickBeyondSevenIsClampedAlongClickDirection()
        {
            bool valid = DashPathResolver.TryResolve(
                Vector3.zero,
                new Vector3(20f, 0f, 0f),
                7f,
                ArenaLayout.CreateDefault(),
                0,
                0.25f,
                out Vector3 endpoint);

            Assert.That(valid, Is.True);
            Assert.That(endpoint, Is.EqualTo(new Vector3(7f, 0f, 0f)));
        }

        [Test]
        public void ClickWithinMaximumDistanceKeepsRequestedFourMeterEndpoint()
        {
            bool valid = DashPathResolver.TryResolve(
                Vector3.zero,
                new Vector3(0f, 8f, 4f),
                7f,
                ArenaLayout.CreateDefault(),
                0,
                0.25f,
                out Vector3 endpoint);

            Assert.That(valid, Is.True);
            Assert.That(endpoint, Is.EqualTo(new Vector3(0f, 0f, 4f)));
        }

        [Test]
        public void DiagonalBoundaryShorteningKeepsTheOriginalDirection()
        {
            Vector3 start = new Vector3(49f, 0f, 10f);

            bool valid = DashPathResolver.TryResolve(
                start,
                new Vector3(60f, 0f, 21f),
                7f,
                ArenaLayout.CreateDefault(),
                0,
                0.25f,
                out Vector3 endpoint);

            Assert.That(valid, Is.True);
            Assert.That(endpoint.x, Is.EqualTo(50f).Within(0.001f));
            Assert.That(endpoint.z, Is.EqualTo(11f).Within(0.001f));
            Vector3 travel = endpoint - start;
            Assert.That(travel.x, Is.EqualTo(travel.z).Within(0.001f));
        }

        [Test]
        public void ObstacleShortensEndpointByClearanceFromRayHit()
        {
            const int obstacleLayer = 10;
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "Obstacle";
            obstacle.layer = obstacleLayer;
            obstacle.transform.position = new Vector3(3f, 0.5f, 0f);
            Physics.SyncTransforms();

            bool valid = DashPathResolver.TryResolve(
                Vector3.zero,
                new Vector3(7f, 0f, 0f),
                7f,
                ArenaLayout.CreateDefault(),
                1 << obstacleLayer,
                0.25f,
                out Vector3 endpoint);

            Assert.That(valid, Is.True);
            Assert.That(endpoint, Is.EqualTo(new Vector3(2.25f, 0f, 0f)));
        }

        [Test]
        public void ObstacleRayStartsHalfAMeterAboveTheOriginalStartHeight()
        {
            const int obstacleLayer = 10;
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.layer = obstacleLayer;
            obstacle.transform.position = new Vector3(3f, 2.5f, 0f);
            Physics.SyncTransforms();

            bool valid = DashPathResolver.TryResolve(
                new Vector3(0f, 2f, 0f),
                new Vector3(7f, 2f, 0f),
                7f,
                ArenaLayout.CreateDefault(),
                1 << obstacleLayer,
                0.25f,
                out Vector3 endpoint);

            Assert.That(valid, Is.True);
            Assert.That(endpoint, Is.EqualTo(new Vector3(2.25f, 0f, 0f)));
        }

        [Test]
        public void ZeroObstacleMaskSkipsPhysicsEvenWhenColliderBlocksPath()
        {
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = new Vector3(3f, 0.5f, 0f);
            Physics.SyncTransforms();

            bool valid = DashPathResolver.TryResolve(
                Vector3.zero,
                new Vector3(7f, 0f, 0f),
                7f,
                ArenaLayout.CreateDefault(),
                0,
                0.25f,
                out Vector3 endpoint);

            Assert.That(valid, Is.True);
            Assert.That(endpoint, Is.EqualTo(new Vector3(7f, 0f, 0f)));
        }

        [Test]
        public void ClickShorterThanMinimumEffectiveTravelIsRejected()
        {
            bool valid = DashPathResolver.TryResolve(
                Vector3.zero,
                new Vector3(0.049f, 3f, 0f),
                7f,
                ArenaLayout.CreateDefault(),
                0,
                0.25f,
                out Vector3 endpoint);

            Assert.That(valid, Is.False);
            Assert.That(endpoint, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ObstacleLeavingLessThanMinimumEffectiveTravelIsRejected()
        {
            const int obstacleLayer = 10;
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.layer = obstacleLayer;
            obstacle.transform.position = new Vector3(0.7f, 0.5f, 0f);
            Physics.SyncTransforms();

            bool valid = DashPathResolver.TryResolve(
                Vector3.zero,
                new Vector3(7f, 0f, 0f),
                7f,
                ArenaLayout.CreateDefault(),
                1 << obstacleLayer,
                0.25f,
                out Vector3 endpoint);

            Assert.That(valid, Is.False);
            Assert.That(endpoint, Is.EqualTo(Vector3.zero));
        }
    }
}
