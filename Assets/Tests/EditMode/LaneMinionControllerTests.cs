using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class LaneMinionControllerTests
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
        public void MinionMovesTowardEnemyTowerWhenNoTargetExists()
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit minion = CreateUnit("BlueMinion", TeamId.Blue, Altitude.Ground, Vector3.zero, 5f, true, false);
            UnitMotor motor = minion.gameObject.AddComponent<UnitMotor>();
            motor.Configure(3f, layout);
            BasicAttackController attack = minion.gameObject.AddComponent<BasicAttackController>();
            CombatUnit redTower = CreateUnit("RedTower", TeamId.Red, Altitude.Ground, layout.RedTower, 8f, true, true);
            LaneMinionController controller = minion.gameObject.AddComponent<LaneMinionController>();
            controller.Configure(minion, motor, attack, redTower, redTower.transform.position);

            controller.Tick(0f);
            motor.Tick(1f);

            Assert.That(motor.IsMoving, Is.True);
            Assert.That(minion.transform.position.x, Is.GreaterThan(0f));
        }

        [Test]
        public void MinionStopsAndTargetsEnemyUnitInRange()
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit minion = CreateUnit("BlueMinion", TeamId.Blue, Altitude.Ground, Vector3.zero, 5f, true, false);
            UnitMotor motor = minion.gameObject.AddComponent<UnitMotor>();
            motor.Configure(3f, layout);
            BasicAttackController attack = minion.gameObject.AddComponent<BasicAttackController>();
            CombatUnit enemy = CreateUnit("RedMinion", TeamId.Red, Altitude.Ground, new Vector3(2f, 0f, 0f), 5f, true, false);
            CombatUnit redTower = CreateUnit("RedTower", TeamId.Red, Altitude.Ground, layout.RedTower, 8f, true, true);
            LaneMinionController controller = minion.gameObject.AddComponent<LaneMinionController>();
            controller.Configure(minion, motor, attack, redTower, redTower.transform.position);

            controller.Tick(0f);

            Assert.That(motor.IsMoving, Is.False);
            Assert.That(attack.CurrentTarget, Is.EqualTo(enemy));
        }

        [Test]
        public void MinionResumesAdvancingAfterEnemyUnitDies()
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit minion = CreateUnit("BlueMinion", TeamId.Blue, Altitude.Ground, Vector3.zero, 5f, true, false);
            UnitMotor motor = minion.gameObject.AddComponent<UnitMotor>();
            motor.Configure(3f, layout);
            BasicAttackController attack = minion.gameObject.AddComponent<BasicAttackController>();
            CombatUnit enemy = CreateUnit("RedMinion", TeamId.Red, Altitude.Ground, new Vector3(2f, 0f, 0f), 5f, true, false);
            CombatUnit redTower = CreateUnit("RedTower", TeamId.Red, Altitude.Ground, layout.RedTower, 8f, true, true);
            LaneMinionController controller = minion.gameObject.AddComponent<LaneMinionController>();
            controller.Configure(minion, motor, attack, redTower, redTower.transform.position);
            controller.Tick(0f);
            enemy.TakePhysicalDamage(100f);

            controller.Tick(0f);

            Assert.That(motor.IsMoving, Is.True);
            Assert.That(attack.CurrentTarget, Is.Null);
        }

        [Test]
        public void MinionTargetsEnemyUnitBeforeEnemyTowerWhenBothAreInRange()
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit minion = CreateUnit("BlueMinion", TeamId.Blue, Altitude.Ground, Vector3.zero, 5f, true, false);
            UnitMotor motor = minion.gameObject.AddComponent<UnitMotor>();
            motor.Configure(3f, layout);
            BasicAttackController attack = minion.gameObject.AddComponent<BasicAttackController>();
            CombatUnit enemy = CreateUnit("RedMinion", TeamId.Red, Altitude.Ground, new Vector3(3f, 0f, 0f), 5f, true, false);
            CombatUnit redTower = CreateUnit("RedTower", TeamId.Red, Altitude.Ground, new Vector3(2f, 0f, 0f), 8f, true, true);
            redTower.gameObject.AddComponent<TowerCombatController>();
            LaneMinionController controller = minion.gameObject.AddComponent<LaneMinionController>();
            controller.Configure(minion, motor, attack, redTower, redTower.transform.position);

            controller.Tick(0f);

            Assert.That(attack.CurrentTarget, Is.EqualTo(enemy));
        }

        [Test]
        public void MinionTargetsEnemyTowerWhenNoEnemyUnitIsInRange()
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit minion = CreateUnit("BlueMinion", TeamId.Blue, Altitude.Ground, Vector3.zero, 5f, true, false);
            UnitMotor motor = minion.gameObject.AddComponent<UnitMotor>();
            motor.Configure(3f, layout);
            BasicAttackController attack = minion.gameObject.AddComponent<BasicAttackController>();
            CombatUnit redTower = CreateUnit("RedTower", TeamId.Red, Altitude.Ground, new Vector3(4f, 0f, 0f), 8f, true, true);
            redTower.gameObject.AddComponent<TowerCombatController>();
            LaneMinionController controller = minion.gameObject.AddComponent<LaneMinionController>();
            controller.Configure(minion, motor, attack, redTower, redTower.transform.position);

            controller.Tick(0f);

            Assert.That(motor.IsMoving, Is.False);
            Assert.That(attack.CurrentTarget, Is.EqualTo(redTower));
        }

        [Test]
        public void GroundMinionIgnoresEnemyAirUnit()
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit minion = CreateUnit("BlueMinion", TeamId.Blue, Altitude.Ground, Vector3.zero, 5f, true, false);
            UnitMotor motor = minion.gameObject.AddComponent<UnitMotor>();
            motor.Configure(3f, layout);
            BasicAttackController attack = minion.gameObject.AddComponent<BasicAttackController>();
            CreateUnit("RedAirMinion", TeamId.Red, Altitude.Air, new Vector3(2f, 0f, 0f), 5f, true, true);
            CombatUnit redTower = CreateUnit("RedTower", TeamId.Red, Altitude.Ground, layout.RedTower, 8f, true, true);
            LaneMinionController controller = minion.gameObject.AddComponent<LaneMinionController>();
            controller.Configure(minion, motor, attack, redTower, redTower.transform.position);

            controller.Tick(0f);

            Assert.That(motor.IsMoving, Is.True);
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
