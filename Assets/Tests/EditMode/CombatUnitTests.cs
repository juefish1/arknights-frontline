using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class CombatUnitTests
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
        public void ConfigureClampsCombatValuesAndRestoresHealthToMaximum()
        {
            CombatUnit unit = CreateUnit("Target", TeamId.Red, Altitude.Ground, false, false, -4f, -2f, -3f, -5f, -6f);

            Assert.That(unit.MaxHealth, Is.EqualTo(1f));
            Assert.That(unit.CurrentHealth, Is.EqualTo(1f));
            Assert.That(unit.Defense, Is.EqualTo(0f));
            Assert.That(unit.AttackRange, Is.EqualTo(0f));
            Assert.That(unit.AttackInterval, Is.EqualTo(0f));
        }

        [Test]
        public void DamageConsumesResolvedNonNegativeDamageAndDeathFiresOnlyOnce()
        {
            CombatUnit unit = CreateUnit("Target", TeamId.Red, Altitude.Ground, false, false, 10f, 2f);
            int deathCount = 0;
            unit.Died += _ => deathCount++;

            unit.TakePhysicalDamage(-2f);
            unit.TakePhysicalDamage(1f);

            Assert.That(unit.CurrentHealth, Is.EqualTo(9f));

            unit.TakePhysicalDamage(100f);
            unit.TakePhysicalDamage(100f);

            Assert.That(unit.CurrentHealth, Is.EqualTo(0f));
            Assert.That(unit.IsDead, Is.True);
            Assert.That(deathCount, Is.EqualTo(1));
        }

        [Test]
        public void StatModifiersPreserveBaseAttackValuesAndApplyEffectiveValues()
        {
            CombatUnit unit = CreateUnit("Exusiai", TeamId.Blue, Altitude.Ground, true, true, 10f, 0f, 50f, 1f, 0.5f);
            UnitStatModifiers modifiers = unit.gameObject.AddComponent<UnitStatModifiers>();
            modifiers.SetAttackPowerMultiplier("Exusiai.R", 1.10f);
            modifiers.SetAttackIntervalOffset("Exusiai.R", -0.22f);

            Assert.That(unit.BaseAttackPower, Is.EqualTo(50f));
            Assert.That(unit.AttackPower, Is.EqualTo(55f));
            Assert.That(unit.BaseAttackInterval, Is.EqualTo(0.5f));
            Assert.That(unit.AttackInterval, Is.EqualTo(0.28f).Within(0.001f));
        }

        private CombatUnit CreateUnit(
            string name,
            TeamId team,
            Altitude altitude,
            bool canAttackGround,
            bool canAttackAir,
            float maxHealth = 10f,
            float defense = 0f,
            float attackPower = 1f,
            float attackRange = 1f,
            float attackInterval = 1f)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, altitude, maxHealth, attackPower, defense, attackRange, attackInterval, canAttackGround, canAttackAir);
            return unit;
        }
    }
}
