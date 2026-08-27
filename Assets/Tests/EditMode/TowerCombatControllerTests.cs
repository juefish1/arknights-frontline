using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class TowerCombatControllerTests
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
        public void TowerTargetsNearestLegalEnemy()
        {
            CombatUnit tower = CreateUnit("BlueTower", TeamId.Blue, Altitude.Ground, Vector3.zero, 8f, true, false);
            BasicAttackController attack = tower.gameObject.AddComponent<BasicAttackController>();
            CombatUnit near = CreateUnit("NearRed", TeamId.Red, Altitude.Ground, new Vector3(2f, 0f, 0f), 2f, true, false);
            CreateUnit("FarRed", TeamId.Red, Altitude.Ground, new Vector3(4f, 0f, 0f), 2f, true, false);
            TowerCombatController controller = tower.gameObject.AddComponent<TowerCombatController>();
            controller.Configure(tower, attack);

            controller.Tick(0f);

            Assert.That(attack.CurrentTarget, Is.EqualTo(near));
        }

        [Test]
        public void TowerRetargetsAfterNearestEnemyDies()
        {
            CombatUnit tower = CreateUnit("BlueTower", TeamId.Blue, Altitude.Ground, Vector3.zero, 8f, true, false);
            BasicAttackController attack = tower.gameObject.AddComponent<BasicAttackController>();
            CombatUnit near = CreateUnit("NearRed", TeamId.Red, Altitude.Ground, new Vector3(2f, 0f, 0f), 2f, true, false);
            CombatUnit far = CreateUnit("FarRed", TeamId.Red, Altitude.Ground, new Vector3(4f, 0f, 0f), 2f, true, false);
            TowerCombatController controller = tower.gameObject.AddComponent<TowerCombatController>();
            controller.Configure(tower, attack);
            controller.Tick(0f);
            near.TakePhysicalDamage(100f);

            controller.Tick(0f);

            Assert.That(attack.CurrentTarget, Is.EqualTo(far));
        }

        [Test]
        public void TowerOnlyTargetsAirEnemiesWhenConfiguredToAttackAir()
        {
            CombatUnit tower = CreateUnit("BlueTower", TeamId.Blue, Altitude.Ground, Vector3.zero, 8f, true, false);
            BasicAttackController attack = tower.gameObject.AddComponent<BasicAttackController>();
            CreateUnit("RedAir", TeamId.Red, Altitude.Air, new Vector3(2f, 0f, 0f), 2f, true, true);
            TowerCombatController controller = tower.gameObject.AddComponent<TowerCombatController>();
            controller.Configure(tower, attack);

            controller.Tick(0f);

            Assert.That(attack.CurrentTarget, Is.Null);
            tower.Configure(TeamId.Blue, Altitude.Ground, 10f, 1f, 0f, 8f, 1f, true, true);
            controller.Tick(0f);

            Assert.That(attack.CurrentTarget, Is.Not.Null);
            Assert.That(attack.CurrentTarget.Altitude, Is.EqualTo(Altitude.Air));
        }

        [Test]
        public void StopForMatchClearsTargetAndPreventsRetargeting()
        {
            CombatUnit tower = CreateUnit("BlueTower", TeamId.Blue, Altitude.Ground, Vector3.zero, 8f, true, true);
            BasicAttackController attack = tower.gameObject.AddComponent<BasicAttackController>();
            CreateUnit("Red", TeamId.Red, Altitude.Ground, new Vector3(2f, 0f, 0f), 2f, true, false);
            TowerCombatController controller = tower.gameObject.AddComponent<TowerCombatController>();
            controller.Configure(tower, attack);
            controller.Tick(0f);

            controller.StopForMatch();
            controller.Tick(0f);

            Assert.That(attack.CurrentTarget, Is.Null);
        }

        private CombatUnit CreateUnit(string name, TeamId team, Altitude altitude, Vector3 position, float attackRange, bool canAttackGround, bool canAttackAir)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            gameObject.transform.position = position;
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, altitude, 10f, 1f, 0f, attackRange, 1f, canAttackGround, canAttackAir);
            return unit;
        }
    }
}
