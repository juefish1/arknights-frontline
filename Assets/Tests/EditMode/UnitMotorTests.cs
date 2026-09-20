using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Movement;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class UnitMotorTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();

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
        public void SiblingModifiersChangeMovementSpeedAndTickDistanceThenRestoreBaseSpeed()
        {
            GameObject gameObject = new GameObject("Motor");
            gameObjects.Add(gameObject);
            UnitMotor motor = gameObject.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());

            Assert.That(motor.BaseMovementSpeed, Is.EqualTo(5f));
            Assert.That(motor.MovementSpeed, Is.EqualTo(5f));

            UnitStatModifiers modifiers = gameObject.AddComponent<UnitStatModifiers>();
            modifiers.SetMovementSpeedMultiplier("Exusiai.E.Slow", 0.70f);

            Assert.That(motor.MovementSpeed, Is.EqualTo(3.5f).Within(0.001f));
            motor.SetDestination(new Vector3(10f, 0f, 0f));
            motor.Tick(1f);
            Assert.That(gameObject.transform.position.x, Is.EqualTo(3.5f).Within(0.001f));

            modifiers.RemoveSource("Exusiai.E.Slow");
            Assert.That(motor.MovementSpeed, Is.EqualTo(5f));

            modifiers.SetMovementSpeedMultiplier("Exusiai.E.Slow", 0.70f);
            modifiers.Clear();
            Assert.That(motor.MovementSpeed, Is.EqualTo(5f));
        }
    }
}
