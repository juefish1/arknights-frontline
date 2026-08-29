using System.Collections.Generic;
using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class MinionWaveSpawnerTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private readonly List<Material> materials = new List<Material>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in gameObjects)
            {
                Object.DestroyImmediate(gameObject);
            }

            foreach (Material material in materials)
            {
                Object.DestroyImmediate(material);
            }

            gameObjects.Clear();
            materials.Clear();
        }

        [Test]
        public void SpawnWaveNowCreatesFourMinionsPerSideWithRequiredComponents()
        {
            MinionWaveSpawner spawner = CreateSpawner();

            spawner.SpawnWaveNow();

            CombatUnit[] minions = GetSpawnedMinions();
            Assert.That(spawner.SpawnedWaveCount, Is.EqualTo(1));
            Assert.That(minions.Length, Is.EqualTo(8));
            Assert.That(minions.Count(unit => unit.Team == TeamId.Blue), Is.EqualTo(4));
            Assert.That(minions.Count(unit => unit.Team == TeamId.Red), Is.EqualTo(4));
            Assert.That(minions.Count(unit => unit.Team == TeamId.Blue && unit.Altitude == Altitude.Ground), Is.EqualTo(3));
            Assert.That(minions.Count(unit => unit.Team == TeamId.Red && unit.Altitude == Altitude.Ground), Is.EqualTo(3));
            Assert.That(minions.Count(unit => unit.Team == TeamId.Blue && unit.Altitude == Altitude.Air), Is.EqualTo(1));
            Assert.That(minions.Count(unit => unit.Team == TeamId.Red && unit.Altitude == Altitude.Air), Is.EqualTo(1));

            foreach (CombatUnit minion in minions)
            {
                Assert.That(minion.GetComponent<UnitMotor>(), Is.Not.Null);
                Assert.That(minion.GetComponent<BasicAttackController>(), Is.Not.Null);
                Assert.That(minion.GetComponent<LaneMinionController>(), Is.Not.Null);
                Assert.That(minion.GetComponent<DeathCorpsePresenter>(), Is.Not.Null);
                Assert.That(minion.GetComponent<HealthBarPresenter>(), Is.Not.Null);
            }
        }

        [Test]
        public void TickSpawnsAtTwentyFiveSecondsAndStopPreventsFutureWaves()
        {
            MinionWaveSpawner spawner = CreateSpawner();
            spawner.SpawnWaveNow();

            spawner.Tick(24.99f);
            Assert.That(spawner.SpawnedWaveCount, Is.EqualTo(1));

            spawner.Tick(0.01f);
            Assert.That(spawner.SpawnedWaveCount, Is.EqualTo(2));

            spawner.StopForMatch();
            spawner.Tick(100f);

            Assert.That(spawner.SpawnedWaveCount, Is.EqualTo(2));
        }

        [Test]
        public void TickAtSeventyFiveSecondsCatchesUpThreeAdditionalWaves()
        {
            MinionWaveSpawner spawner = CreateSpawner();
            spawner.SpawnWaveNow();

            spawner.Tick(75f);

            Assert.That(spawner.SpawnedWaveCount, Is.EqualTo(4));
        }

        [Test]
        public void SpawnedMinionsUseFixedCombatProfilesAndMovementSpeeds()
        {
            MinionWaveSpawner spawner = CreateSpawner();
            spawner.SpawnWaveNow();

            CombatUnit blueGround = GetSpawnedMinions().Single(unit => unit.name.StartsWith("BlueGroundMinion"));
            Assert.That(blueGround.MaxHealth, Is.EqualTo(400f));
            Assert.That(blueGround.AttackPower, Is.EqualTo(35f));
            Assert.That(blueGround.Defense, Is.EqualTo(10f));
            Assert.That(blueGround.AttackRange, Is.EqualTo(1.5f));
            Assert.That(blueGround.AttackInterval, Is.EqualTo(1.2f));
            Assert.That(blueGround.CanAttackGround, Is.True);
            Assert.That(blueGround.CanAttackAir, Is.False);

            UnitMotor blueGroundMotor = blueGround.GetComponent<UnitMotor>();
            LaneMinionController blueGroundController = blueGround.GetComponent<LaneMinionController>();
            float blueGroundStartX = blueGround.transform.position.x;
            blueGroundController.Tick(0f);
            blueGroundMotor.Tick(1f);
            Assert.That(blueGround.transform.position.x - blueGroundStartX, Is.EqualTo(3f).Within(0.001f));

            CombatUnit redAir = GetSpawnedMinions().Single(unit => unit.name.StartsWith("RedAirMinion"));
            Assert.That(redAir.MaxHealth, Is.EqualTo(280f));
            Assert.That(redAir.AttackPower, Is.EqualTo(28f));
            Assert.That(redAir.Defense, Is.EqualTo(5f));
            Assert.That(redAir.AttackRange, Is.EqualTo(4.5f));
            Assert.That(redAir.AttackInterval, Is.EqualTo(1f));
            Assert.That(redAir.CanAttackGround, Is.True);
            Assert.That(redAir.CanAttackAir, Is.True);

            UnitMotor redAirMotor = redAir.GetComponent<UnitMotor>();
            LaneMinionController redAirController = redAir.GetComponent<LaneMinionController>();
            float redAirStartX = redAir.transform.position.x;
            redAirController.Tick(0f);
            redAirMotor.Tick(1f);
            Assert.That(redAirStartX - redAir.transform.position.x, Is.EqualTo(3.2f).Within(0.001f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ConfigureRejectsNullTeamMaterialBeforeSpawningAnyMinions(bool nullBlueMaterial)
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            GameObject parent = CreateGameObject("MinionParent");
            CombatUnit blueTower = CreateTower("BlueTower", TeamId.Blue, layout.BlueTower);
            CombatUnit redTower = CreateTower("RedTower", TeamId.Red, layout.RedTower);
            MinionWaveSpawner spawner = CreateGameObject("Spawner").AddComponent<MinionWaveSpawner>();
            Material blueMaterial = nullBlueMaterial ? null : CreateMaterial(Color.blue);
            Material redMaterial = nullBlueMaterial ? CreateMaterial(Color.red) : null;

            System.ArgumentNullException exception = Assert.Throws<System.ArgumentNullException>(() => spawner.Configure(
                parent.transform,
                layout,
                blueTower,
                redTower,
                blueMaterial,
                redMaterial,
                9,
                8));

            Assert.That(exception.ParamName, Is.EqualTo(nullBlueMaterial ? "blueMaterial" : "redMaterial"));
            spawner.SpawnWaveNow();
            Assert.That(parent.transform.childCount, Is.Zero);
            Assert.That(spawner.SpawnedWaveCount, Is.Zero);
        }

        private MinionWaveSpawner CreateSpawner()
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            GameObject parent = CreateGameObject("MinionParent");
            CombatUnit blueTower = CreateTower("BlueTower", TeamId.Blue, layout.BlueTower);
            CombatUnit redTower = CreateTower("RedTower", TeamId.Red, layout.RedTower);
            MinionWaveSpawner spawner = CreateGameObject("Spawner").AddComponent<MinionWaveSpawner>();
            spawner.Configure(
                parent.transform,
                layout,
                blueTower,
                redTower,
                CreateMaterial(Color.blue),
                CreateMaterial(Color.red),
                9,
                8);
            return spawner;
        }

        private CombatUnit[] GetSpawnedMinions()
        {
            GameObject parent = gameObjects.Single(gameObject => gameObject.name == "MinionParent");
            return parent.GetComponentsInChildren<CombatUnit>();
        }

        private CombatUnit CreateTower(string name, TeamId team, Vector3 position)
        {
            GameObject towerObject = CreateGameObject(name);
            towerObject.transform.position = position;
            CombatUnit tower = towerObject.AddComponent<CombatUnit>();
            tower.Configure(team, Altitude.Ground, 1000f, 60f, 20f, 8f, 1f, true, true);
            return tower;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            return gameObject;
        }

        private Material CreateMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            materials.Add(material);
            return material;
        }
    }
}
