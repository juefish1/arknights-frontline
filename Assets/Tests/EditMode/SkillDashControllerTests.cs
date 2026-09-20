using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class SkillDashControllerTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private GameObject owner;
        private UnitMotor motor;
        private SkillDashController dash;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Dash Owner");
            gameObjects.Add(owner);
            motor = owner.AddComponent<UnitMotor>();
            motor.Configure(1f, ArenaLayout.CreateDefault());
            dash = owner.AddComponent<SkillDashController>();
            dash.Configure(motor, ArenaLayout.CreateDefault(), 0);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in gameObjects)
            {
                Object.DestroyImmediate(gameObject);
            }

            gameObjects.Clear();
        }

        [Test]
        public void DashUsesFixedSpeedIndependentOfOrdinaryMotorAndStatModifiers()
        {
            UnitStatModifiers modifiers = owner.AddComponent<UnitStatModifiers>();
            modifiers.SetMovementSpeedMultiplier("Exusiai.E.Slow", 0.25f);

            Assert.That(dash.TryStart(new Vector3(7f, 0f, 0f)), Is.True);
            Assert.That(motor.IsMoving, Is.False);

            dash.Tick(0.25f);

            Assert.That(owner.transform.position, Is.EqualTo(new Vector3(3.5f, 0f, 0f)));
            Assert.That(motor.MovementSpeed, Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        public void InvalidStartDoesNotTakeOwnershipOrStopExistingMotorMovement()
        {
            motor.SetDestination(new Vector3(4f, 0f, 0f));

            bool started = dash.TryStart(new Vector3(0.049f, 0f, 0f));

            Assert.That(started, Is.False);
            Assert.That(dash.IsDashing, Is.False);
            Assert.That(motor.IsMoving, Is.True);
        }

        [Test]
        public void InvalidRestartPreservesAnActiveDashAndItsDestination()
        {
            Assert.That(dash.TryStart(new Vector3(7f, 0f, 0f)), Is.True);
            Vector3 destination = dash.Destination;

            bool restarted = dash.TryStart(new Vector3(0.01f, 0f, 0f));

            Assert.That(restarted, Is.False);
            Assert.That(dash.IsDashing, Is.True);
            Assert.That(dash.Destination, Is.EqualTo(destination));
        }

        [Test]
        public void PreviewUsesActualResolverWithoutChangingMotorOrDashState()
        {
            motor.SetDestination(new Vector3(4f, 0f, 0f));

            bool valid = dash.Preview(new Vector3(20f, 0f, 0f), out Vector3 endpoint);

            Assert.That(valid, Is.True);
            Assert.That(endpoint, Is.EqualTo(new Vector3(7f, 0f, 0f)));
            Assert.That(dash.IsDashing, Is.False);
            Assert.That(dash.Destination, Is.EqualTo(Vector3.zero));
            Assert.That(motor.IsMoving, Is.True);

            Assert.That(dash.TryStart(new Vector3(20f, 0f, 0f)), Is.True);
            Assert.That(dash.Destination, Is.EqualTo(endpoint));
        }

        [Test]
        public void NegativeDeltaDoesNotMoveBackwardAndDashStaysInXZPlane()
        {
            owner.transform.position = new Vector3(0f, 2f, 0f);
            Assert.That(dash.TryStart(new Vector3(7f, 20f, 0f)), Is.True);

            dash.Tick(-1f);

            Assert.That(owner.transform.position, Is.EqualTo(new Vector3(0f, 2f, 0f)));
            dash.Tick(0.25f);
            Assert.That(owner.transform.position, Is.EqualTo(new Vector3(3.5f, 2f, 0f)));
        }

        [Test]
        public void CompletionRaisesExactlyOneEventAndLeavesOrdinaryMotorStopped()
        {
            int completions = 0;
            dash.DashCompleted += () => completions++;
            Assert.That(dash.TryStart(new Vector3(4f, 0f, 0f)), Is.True);

            dash.Tick(1f);
            dash.Tick(1f);

            Assert.That(owner.transform.position, Is.EqualTo(new Vector3(4f, 0f, 0f)));
            Assert.That(dash.IsDashing, Is.False);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(motor.IsMoving, Is.False);
        }

        [Test]
        public void LosingMotorDuringDashClearsStateWithoutCompletingAndRemainsIdempotent()
        {
            int completions = 0;
            dash.DashCompleted += () => completions++;
            Assert.That(dash.TryStart(new Vector3(7f, 0f, 0f)), Is.True);
            Object.DestroyImmediate(motor);

            dash.Tick(0.25f);

            Assert.That(dash.IsDashing, Is.False);
            Assert.That(completions, Is.Zero);

            dash.Tick(0.25f);
            dash.Cancel();
            dash.Cancel();

            Assert.That(dash.IsDashing, Is.False);
            Assert.That(completions, Is.Zero);
        }

        [Test]
        public void CancelIsIdempotentStopsDashAndLeavesOrdinaryMotorStopped()
        {
            int completions = 0;
            dash.DashCompleted += () => completions++;
            Assert.That(dash.TryStart(new Vector3(7f, 0f, 0f)), Is.True);
            dash.Tick(0.1f);
            Vector3 cancelledPosition = owner.transform.position;

            dash.Cancel();
            dash.Cancel();
            dash.Tick(1f);

            Assert.That(owner.transform.position, Is.EqualTo(cancelledPosition));
            Assert.That(dash.IsDashing, Is.False);
            Assert.That(completions, Is.Zero);
            Assert.That(motor.IsMoving, Is.False);
        }
    }
}
