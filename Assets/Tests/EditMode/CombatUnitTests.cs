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
        public void DamageTakenReportsAttackerAndActualHealthLossBeforeDeathNotification()
        {
            CombatUnit attacker = CreateUnit("Attacker", TeamId.Blue, Altitude.Ground, false, false);
            CombatUnit unit = CreateUnit("Target", TeamId.Red, Altitude.Ground, false, false, 10f);
            var notifications = new System.Collections.Generic.List<string>();
            var reportedAttackers = new System.Collections.Generic.List<CombatUnit>();
            var reportedDamage = new System.Collections.Generic.List<float>();
            var deadStatesWhenDamageReported = new System.Collections.Generic.List<bool>();
            int deathCount = 0;
            unit.Died += _ =>
            {
                deathCount++;
                notifications.Add("Died");
                Assert.That(unit.IsDead, Is.True);
            };
            unit.DamageTaken += (source, amount) =>
            {
                notifications.Add("DamageTaken");
                reportedAttackers.Add(source);
                reportedDamage.Add(amount);
                deadStatesWhenDamageReported.Add(unit.IsDead);
            };

            unit.TakePhysicalDamage(3f, attacker);
            unit.TakePhysicalDamage(100f, attacker);
            unit.TakePhysicalDamage(100f, attacker);

            Assert.That(reportedAttackers, Is.EqualTo(new[] { attacker, attacker }));
            Assert.That(reportedDamage, Is.EqualTo(new[] { 3f, 7f }));
            Assert.That(notifications, Is.EqualTo(new[] { "DamageTaken", "DamageTaken", "Died" }));
            Assert.That(deadStatesWhenDamageReported, Is.EqualTo(new[] { false, true }));
            Assert.That(deathCount, Is.EqualTo(1));
        }

        [Test]
        public void NonLethalDamageCallbackCanApplyLethalDamageAndDeathIsNotifiedOnce()
        {
            CombatUnit attacker = CreateUnit("Attacker", TeamId.Blue, Altitude.Ground, false, false);
            CombatUnit unit = CreateUnit("Target", TeamId.Red, Altitude.Ground, false, false, 10f);
            var reportedDamage = new System.Collections.Generic.List<float>();
            var notifications = new System.Collections.Generic.List<string>();
            bool nestedHitStarted = false;
            bool deadWhenLethalDamageWasReported = false;
            int deathCount = 0;
            unit.DamageTaken += (_, amount) =>
            {
                notifications.Add("DamageTaken");
                reportedDamage.Add(amount);
                if (!nestedHitStarted)
                {
                    nestedHitStarted = true;
                    unit.TakePhysicalDamage(100f, attacker);
                }
                else
                {
                    deadWhenLethalDamageWasReported = unit.IsDead;
                }
            };
            unit.Died += _ =>
            {
                notifications.Add("Died");
                deathCount++;
            };

            unit.TakePhysicalDamage(3f, attacker);

            Assert.That(unit.CurrentHealth, Is.EqualTo(0f));
            Assert.That(reportedDamage, Is.EqualTo(new[] { 3f, 7f }));
            Assert.That(deadWhenLethalDamageWasReported, Is.True);
            Assert.That(notifications, Is.EqualTo(new[] { "DamageTaken", "DamageTaken", "Died" }));
            Assert.That(deathCount, Is.EqualTo(1));
        }

        [Test]
        public void NonPositiveDamageDoesNotNotifyAndLegacyDamageReportsNoAttacker()
        {
            CombatUnit unit = CreateUnit("Target", TeamId.Red, Altitude.Ground, false, false, 10f);
            int damageEventCount = 0;
            CombatUnit reportedAttacker = unit;
            float reportedDamage = 0f;
            unit.DamageTaken += (attacker, damage) =>
            {
                damageEventCount++;
                reportedAttacker = attacker;
                reportedDamage = damage;
            };

            unit.TakePhysicalDamage(0f, null);
            unit.TakePhysicalDamage(-2f, null);
            unit.TakePhysicalDamage(2f);

            Assert.That(unit.CurrentHealth, Is.EqualTo(8f));
            Assert.That(damageEventCount, Is.EqualTo(1));
            Assert.That(reportedAttacker, Is.Null);
            Assert.That(reportedDamage, Is.EqualTo(2f));
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
