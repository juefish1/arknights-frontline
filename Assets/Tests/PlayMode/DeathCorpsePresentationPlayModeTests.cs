using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class DeathCorpsePresentationPlayModeTests
    {
        private const int GroundLayer = 8;
        private const int SerializedGroundLayer = 10;

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
        public IEnumerator ConfiguredPlayerAndMinionsCreateCorpsesWhileTowersDoNot()
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
            spawner.enabled = false;
            spawner.SpawnWaveNow();

            CombatUnit blueGroundMinion = FindMinion(minionParent.transform, TeamId.Blue, Altitude.Ground);
            CombatUnit redAirMinion = FindMinion(minionParent.transform, TeamId.Red, Altitude.Air);
            string blueGroundMinionName = blueGroundMinion.name;
            string redAirMinionName = redAirMinion.name;
            blueGroundMinion.TakePhysicalDamage(blueGroundMinion.MaxHealth);

            yield return null;

            Assert.That(blueGroundMinion == null, Is.True);
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
            Assert.That(redAirMinion == null, Is.True);

            CombatUnit player = CreateConfiguredPlayer(blueMaterial);
            player.TakePhysicalDamage(player.MaxHealth);

            yield return null;

            TrackCorpse("Player_Exusiai_Corpse");

            Assert.That(blueTower.GetComponent<DeathCorpsePresenter>(), Is.Null);
            Assert.That(redTower.GetComponent<DeathCorpsePresenter>(), Is.Null);
            int corpseCountBeforeExcludedDeaths = FindCorpses().Length;
            blueTower.TakePhysicalDamage(blueTower.MaxHealth);
            redTower.TakePhysicalDamage(redTower.MaxHealth);

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

            Assert.That(unit == null, Is.True);
            Assert.That(healthBar == null, Is.True);
            GameObject corpse = TrackCorpse("HealthBarUnit_Corpse");
            Assert.That(corpse.transform.Find("HealthBar"), Is.Null);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator DeserializedInactivePresenterKeepsConfiguredMaterialAndSubscribesWhenEnabled()
        {
            CreateGround();
            CreateGround(SerializedGroundLayer, new Vector3(4f, 3f, 0f));
            Material configuredMaterial = CreateMaterial(Color.blue);
            CombatUnit source = CreateUnit("SerializedSource", TeamId.Blue, Altitude.Ground, new Vector3(4f, 1f, 0f), 100f);
            DeathCorpsePresenter configuredPresenter = source.gameObject.AddComponent<DeathCorpsePresenter>();
            configuredPresenter.Configure(source, configuredMaterial, SerializedGroundLayer);

            GameObject rehydratedObject = CreateGameObject("RehydratedPresenter");
            rehydratedObject.transform.position = new Vector3(4f, 5f, 0f);
            rehydratedObject.SetActive(false);
            CombatUnit rehydratedUnit = rehydratedObject.AddComponent<CombatUnit>();
            rehydratedUnit.Configure(TeamId.Red, Altitude.Air, 100f, 0f, 0f, 0f, 0f, false, false);
            DeathCorpsePresenter rehydratedPresenter = rehydratedObject.AddComponent<DeathCorpsePresenter>();
            EditorUtility.CopySerialized(configuredPresenter, rehydratedPresenter);
            rehydratedObject.SetActive(true);

            Physics.SyncTransforms();
            source.TakePhysicalDamage(source.MaxHealth);
            yield return null;

            TrackCorpse("SerializedSource_Corpse");
            GameObject corpse = TrackCorpse("RehydratedPresenter_Corpse");
            Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.SameAs(configuredMaterial));
            Assert.That(corpse.transform.position, Is.EqualTo(new Vector3(4f, 3.01f, 0f)));
            Assert.That(corpse.GetComponent<CorpseFallController>(), Is.Null);
        }
#endif

        private void CreateGround()
        {
            CreateGround(GroundLayer, Vector3.zero);
        }

        private void CreateGround(int layer, Vector3 position)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.layer = layer;
            ground.transform.position = position;
            gameObjects.Add(ground);
        }

        private CombatUnit FindMinion(Transform minionParent, TeamId team, Altitude altitude)
        {
            return Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None)
                .First(unit => unit.transform.parent == minionParent
                    && unit.Team == team
                    && unit.Altitude == altitude
                    && unit.name.Contains("Minion"));
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
            GameObject corpse = FindCorpses().SingleOrDefault(candidate => candidate.name == corpseName);
            Assert.That(corpse, Is.Not.Null, $"Expected a corpse named {corpseName}.");
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
