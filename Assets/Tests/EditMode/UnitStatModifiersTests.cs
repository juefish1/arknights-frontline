using ArknightsFrontline.Combat;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class UnitStatModifiersTests
    {
        private readonly System.Collections.Generic.List<GameObject> gameObjects = new System.Collections.Generic.List<GameObject>();

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
        public void RAndSlowUseSeparateApprovedOperations()
        {
            UnitStatModifiers modifiers = CreateModifiers();
            modifiers.SetAttackPowerMultiplier("Exusiai.R", 1.10f);
            modifiers.SetMovementSpeedMultiplier("Exusiai.R", 1.08f);
            modifiers.SetMovementSpeedMultiplier("Exusiai.E.Slow", 0.70f);
            modifiers.SetAttackIntervalOffset("Exusiai.R", -0.22f);

            Assert.That(modifiers.ApplyAttackPower(50f), Is.EqualTo(55f).Within(0.001f));
            Assert.That(modifiers.ApplyMovementSpeed(5f), Is.EqualTo(3.78f).Within(0.001f));
            Assert.That(modifiers.ApplyAttackInterval(0.5f), Is.EqualTo(0.28f).Within(0.001f));
        }

        [Test]
        public void ReusingSourceReplacesInsteadOfStacking()
        {
            UnitStatModifiers modifiers = CreateModifiers();
            modifiers.SetMovementSpeedMultiplier("Exusiai.E.Slow", 0.70f);
            modifiers.SetMovementSpeedMultiplier("Exusiai.E.Slow", 0.70f);
            Assert.That(modifiers.ApplyMovementSpeed(5f), Is.EqualTo(3.5f).Within(0.001f));
        }

        [Test]
        public void RemovingSourceRemovesItsModifiersFromEveryStat()
        {
            UnitStatModifiers modifiers = CreateModifiers();
            modifiers.SetAttackPowerMultiplier("Exusiai.R", 1.10f);
            modifiers.SetMovementSpeedMultiplier("Exusiai.R", 1.08f);
            modifiers.SetAttackIntervalOffset("Exusiai.R", -0.22f);

            modifiers.RemoveSource("Exusiai.R");

            Assert.That(modifiers.ApplyAttackPower(50f), Is.EqualTo(50f));
            Assert.That(modifiers.ApplyMovementSpeed(5f), Is.EqualTo(5f));
            Assert.That(modifiers.ApplyAttackInterval(0.5f), Is.EqualTo(0.5f));
        }

        [Test]
        public void ClearRemovesModifiersFromEveryStat()
        {
            UnitStatModifiers modifiers = CreateModifiers();
            modifiers.SetAttackPowerMultiplier("Exusiai.R", 1.10f);
            modifiers.SetMovementSpeedMultiplier("Exusiai.E.Slow", 0.70f);
            modifiers.SetAttackIntervalOffset("Exusiai.R", -0.22f);

            modifiers.Clear();

            Assert.That(modifiers.ApplyAttackPower(50f), Is.EqualTo(50f));
            Assert.That(modifiers.ApplyMovementSpeed(5f), Is.EqualTo(5f));
            Assert.That(modifiers.ApplyAttackInterval(0.5f), Is.EqualTo(0.5f));
        }

        [Test]
        public void AttackIntervalDoesNotDropBelowItsMinimum()
        {
            UnitStatModifiers modifiers = CreateModifiers();
            modifiers.SetAttackIntervalOffset("Exusiai.R", -1f);

            Assert.That(modifiers.ApplyAttackInterval(0.5f), Is.EqualTo(0.05f));
        }

        private UnitStatModifiers CreateModifiers()
        {
            GameObject gameObject = new GameObject("Modifiers");
            gameObjects.Add(gameObject);
            return gameObject.AddComponent<UnitStatModifiers>();
        }
    }
}
