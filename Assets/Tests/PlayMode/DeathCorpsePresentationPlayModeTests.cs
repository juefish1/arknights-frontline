using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class DeathCorpsePresentationPlayModeTests
    {
        private const int GroundLayer = 8;

        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private readonly List<Material> materials = new List<Material>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject gameObject in gameObjects)
            {
                if (gameObject != null)
                {
                    Object.Destroy(gameObject);
                }
            }

            foreach (Material material in materials)
            {
                Object.Destroy(material);
            }

            gameObjects.Clear();
            materials.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConfiguredPlayerAndMinionsCreateCorpsesWhileTowerAndTrainingTargetDoNot()
        {
            CreateGround();
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit blueTower = CreateUnit("BlueTower", TeamId.Blue, Altitude.Ground, layout.BlueTower, 1000f);
            CombatUnit redTower = CreateUnit("RedTower", TeamId.Red, Altitude.Ground, layout.RedTower, 1000f);
            Material blueMaterial = CreateMaterial(Color.blue);
            Material redMaterial = CreateMaterial(Color.red);
            GameObject minionParent = CreateGameObject("MinionParent");
            MinionWaveSpawner spawner = CreateGameObject("MinionWaveSpawner").AddComponent<MinionWaveSpawner>();
            spawner.Configure(
                minionParent.transform,
                layout,
                blueTower,
                redTower,
                blueMaterial,
                redMaterial,
                9,
                GroundLayer);
            spawner.SpawnWaveNow();

            CombatUnit blueGroundMinion = FindMinion(TeamId.Blue, Altitude.Ground);
            CombatUnit redAirMinion = FindMinion(TeamId.Red, Altitude.Air);
            string blueGroundMinionName = blueGroundMinion.name;
            string redAirMinionName = redAirMinion.name;
            blueGroundMinion.TakePhysicalDamage(blueGroundMinion.MaxHealth);

            yield return null;

            Assert.That(
                Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None),
                Has.None.Matches<CombatUnit>(unit => unit.name == blueGroundMinionName));
            GameObject groundCorpse = TrackCorpse(blueGroundMinionName + "_Corpse");
            AssertCorpseInvariants(groundCorpse, blueMaterial);

            redAirMinion.TakePhysicalDamage(redAirMinion.MaxHealth);

            yield return null;

            GameObject airCorpse = TrackCorpse(redAirMinionName + "_Corpse");
            AssertCorpseInvariants(airCorpse, redMaterial);
            float airCorpseStartY = airCorpse.transform.position.y;
            yield return new WaitForSeconds(0.15f);
            Assert.That(airCorpse.transform.position.y, Is.LessThan(airCorpseStartY));
            yield return new WaitForSeconds(0.2f);
            Assert.That(airCorpse.GetComponent<CorpseFallController>().HasLanded, Is.True);
            Assert.That(
                Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None),
                Has.None.Matches<CombatUnit>(unit => unit.name == redAirMinionName));

            CombatUnit player = CreateConfiguredPlayer(blueMaterial);
            player.TakePhysicalDamage(player.MaxHealth);

            yield return null;

            TrackCorpse("Player_Exusiai_Corpse");

            CombatUnit trainingTarget = CreateUnit(
                "TrainingDummy_Red",
                TeamId.Red,
                Altitude.Ground,
                new Vector3(20f, 1f, 0f),
                1000f);
            Assert.That(blueTower.GetComponent<DeathCorpsePresenter>(), Is.Null);
            Assert.That(redTower.GetComponent<DeathCorpsePresenter>(), Is.Null);
            Assert.That(trainingTarget.GetComponent<DeathCorpsePresenter>(), Is.Null);
            int corpseCountBeforeExcludedDeaths = FindCorpses().Length;
            blueTower.TakePhysicalDamage(blueTower.MaxHealth);
            redTower.TakePhysicalDamage(redTower.MaxHealth);
            trainingTarget.TakePhysicalDamage(trainingTarget.MaxHealth);

            yield return null;

            Assert.That(FindCorpses().Length, Is.EqualTo(corpseCountBeforeExcludedDeaths));
        }

        [UnityTest]
        public IEnumerator AirCorpseFallsToZeroHeightWhenGroundRaycastMisses()
        {
            Vector3 deathPosition = new Vector3(123f, 5f, 123f);
            Material material = CreateMaterial(Color.red);
            CombatUnit airUnit = CreateUnit("FallbackAir", TeamId.Red, Altitude.Air, deathPosition, 10f);
            airUnit.gameObject.AddComponent<DeathCorpsePresenter>().Configure(airUnit, material, GroundLayer);
            Assert.That(
                Physics.Raycast(deathPosition + Vector3.up, Vector3.down, 100f, 1 << GroundLayer),
                Is.False);

            airUnit.TakePhysicalDamage(airUnit.MaxHealth);

            yield return null;

            GameObject corpse = TrackCorpse("FallbackAir_Corpse");
            CorpseFallController fall = corpse.GetComponent<CorpseFallController>();
            fall.Tick(1f);
            Assert.That(fall.HasLanded, Is.True);
            Assert.That(corpse.transform.position.x, Is.EqualTo(deathPosition.x));
            Assert.That(corpse.transform.position.y, Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(corpse.transform.position.z, Is.EqualTo(deathPosition.z));
        }

        [UnityTest]
        public IEnumerator UnitDeathDestroysItsHealthBarWithoutAddingOneToTheCorpse()
        {
            CreateGround();
            Material material = CreateMaterial(Color.green);
            CombatUnit unit = CreateUnit("HealthBarUnit", TeamId.Blue, Altitude.Ground, Vector3.zero, 100f);
            unit.gameObject.AddComponent<DeathCorpsePresenter>().Configure(unit, material, GroundLayer);
            HealthBarPresenter presenter = unit.gameObject.AddComponent<HealthBarPresenter>();
            presenter.Configure(unit);
            Transform healthBar = unit.transform.Find("HealthBar");
            Assert.That(healthBar, Is.Not.Null);

            unit.TakePhysicalDamage(unit.MaxHealth);
            yield return null;

            Assert.That(unit, Is.Null);
            Assert.That(healthBar, Is.Null);
            GameObject corpse = GameObject.Find("HealthBarUnit_Corpse");
            Assert.That(corpse, Is.Not.Null);
            Assert.That(corpse.transform.Find("HealthBar"), Is.Null);
        }

        private void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.layer = GroundLayer;
            gameObjects.Add(ground);
        }

        private CombatUnit FindMinion(TeamId team, Altitude altitude)
        {
            return Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None)
                .Single(unit => unit.Team == team && unit.Altitude == altitude && unit.name.Contains("Minion"));
        }

        private CombatUnit CreateConfiguredPlayer(Material material)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player_Exusiai";
            player.transform.position = new Vector3(0f, 1f, 0f);
            player.GetComponent<Renderer>().sharedMaterial = material;
            gameObjects.Add(player);

            CombatUnit combatUnit = player.AddComponent<CombatUnit>();
            combatUnit.Configure(TeamId.Blue, Altitude.Ground, 100f, 12f, 2f, 6f, 0.5f, true, true);
            player.AddComponent<DeathCorpsePresenter>().Configure(combatUnit, material, GroundLayer);
            return combatUnit;
        }

        private CombatUnit CreateUnit(string name, TeamId team, Altitude altitude, Vector3 position, float health)
        {
            GameObject gameObject = CreateGameObject(name);
            gameObject.transform.position = position;
            CombatUnit combatUnit = gameObject.AddComponent<CombatUnit>();
            combatUnit.Configure(team, altitude, health, 1f, 0f, 1f, 1f, true, true);
            return combatUnit;
        }

        private Material CreateMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            materials.Add(material);
            return material;
        }

        private GameObject TrackCorpse(string corpseName)
        {
            GameObject corpse = FindCorpses().Single(candidate => candidate.name == corpseName);
            gameObjects.Add(corpse);
            return corpse;
        }

        private static void AssertCorpseInvariants(GameObject corpse, Material expectedMaterial)
        {
            Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.SameAs(expectedMaterial));
            Assert.That(corpse.layer, Is.EqualTo(LayerMask.NameToLayer("Default")));
            Assert.That(corpse.GetComponent<CombatUnit>(), Is.Null);
            Collider collider = corpse.GetComponent<Collider>();
            Assert.That(collider == null || !collider.enabled, Is.True);
        }

        private static GameObject[] FindCorpses()
        {
            return Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(gameObject => gameObject.name.EndsWith("_Corpse"))
                .ToArray();
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            return gameObject;
        }
    }
}
