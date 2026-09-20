using System.Collections.Generic;
using ArknightsFrontline.Combat;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class TimedStatModifierControllerTests
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
        public void ApplyMovementSlowAddsModifiersRefreshesInsteadOfStackingAndExpires()
        {
            TimedStatModifierController controller = CreateController(out UnitStatModifiers modifiers);

            controller.ApplyMovementSlow("Exusiai.E.Slow", 0.70f, 2f);
            Assert.That(modifiers.ApplyMovementSpeed(5f), Is.EqualTo(3.5f).Within(0.001f));

            controller.Tick(1.5f);
            controller.ApplyMovementSlow("Exusiai.E.Slow", 0.70f, 2f);
            Assert.That(modifiers.ApplyMovementSpeed(5f), Is.EqualTo(3.5f).Within(0.001f));

            controller.Tick(2f);
            Assert.That(modifiers.ApplyMovementSpeed(5f), Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void StopForMatchRemovesOwnedSourcesAndPreventsFutureTicksFromRemovingReusedSource()
        {
            TimedStatModifierController controller = CreateController(out UnitStatModifiers modifiers);
            controller.ApplyMovementSlow("Exusiai.E.Slow", 0.70f, 2f);
            modifiers.SetMovementSpeedMultiplier("Exusiai.R", 0.90f);

            controller.StopForMatch();
            Assert.That(modifiers.ApplyMovementSpeed(5f), Is.EqualTo(4.5f).Within(0.001f));

            modifiers.SetMovementSpeedMultiplier("Exusiai.E.Slow", 0.60f);
            controller.Tick(10f);
            Assert.That(modifiers.ApplyMovementSpeed(5f), Is.EqualTo(2.7f).Within(0.001f));
        }

        private TimedStatModifierController CreateController(out UnitStatModifiers modifiers)
        {
            GameObject gameObject = new GameObject("TimedStatModifierController");
            gameObjects.Add(gameObject);
            TimedStatModifierController controller = gameObject.AddComponent<TimedStatModifierController>();
            typeof(TimedStatModifierController)
                .GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(controller, null);
            modifiers = gameObject.GetComponent<UnitStatModifiers>();
            return controller;
        }
    }
}
