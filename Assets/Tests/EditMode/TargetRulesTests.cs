using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class TargetRulesTests
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
        public void LegalTargetRequiresEnemyAliveAndSupportedAltitude()
        {
            CombatUnit attacker = CreateUnit("Blue", TeamId.Blue, Altitude.Ground, true, false);
            CombatUnit redGround = CreateUnit("RedGround", TeamId.Red, Altitude.Ground, false, false);
            CombatUnit redAir = CreateUnit("RedAir", TeamId.Red, Altitude.Air, false, false);

            Assert.That(TargetRules.IsLegal(attacker, redGround), Is.True);
            Assert.That(TargetRules.IsLegal(attacker, redAir), Is.False);
        }

        [Test]
        public void LegalTargetRejectsSelfAndSameTeam()
        {
            CombatUnit attacker = CreateUnit("Blue", TeamId.Blue, Altitude.Ground, true, true);
            CombatUnit ally = CreateUnit("Ally", TeamId.Blue, Altitude.Ground, false, false);

            Assert.That(TargetRules.IsLegal(attacker, attacker), Is.False);
            Assert.That(TargetRules.IsLegal(attacker, ally), Is.False);
        }

        [Test]
        public void LegalTargetRejectsDeadAttackerAndDeadCandidate()
        {
            CombatUnit attacker = CreateUnit("Blue", TeamId.Blue, Altitude.Ground, true, true);
            CombatUnit candidate = CreateUnit("Red", TeamId.Red, Altitude.Ground, false, false);

            candidate.TakePhysicalDamage(10f);
            Assert.That(TargetRules.IsLegal(attacker, candidate), Is.False);

            CombatUnit livingCandidate = CreateUnit("RedLiving", TeamId.Red, Altitude.Ground, false, false);
            attacker.TakePhysicalDamage(10f);
            Assert.That(TargetRules.IsLegal(attacker, livingCandidate), Is.False);
        }

        [Test]
        public void LegalTargetRejectsNullUnits()
        {
            CombatUnit attacker = CreateUnit("Blue", TeamId.Blue, Altitude.Ground, true, true);

            Assert.That(TargetRules.IsLegal(null, attacker), Is.False);
            Assert.That(TargetRules.IsLegal(attacker, null), Is.False);
        }

        private CombatUnit CreateUnit(string name, TeamId team, Altitude altitude, bool canAttackGround, bool canAttackAir)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, altitude, 10f, 1f, 0f, 1f, 1f, canAttackGround, canAttackAir);
            return unit;
        }
    }
}
