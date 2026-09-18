using System.Collections;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class ArenaSceneSmokeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene prototypeArena = SceneManager.GetSceneByName("PrototypeArena");
            if (!prototypeArena.isLoaded)
            {
                yield break;
            }

            Scene cleanupScene = SceneManager.CreateScene("ArenaSceneSmokeCleanup");
            SceneManager.SetActiveScene(cleanupScene);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(prototypeArena);
            while (!unload.isDone)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator PrototypeArenaContainsRequiredRoots()
        {
            SceneManager.LoadScene("PrototypeArena");
            yield return null;
            ArenaBootstrap arena = Object.FindFirstObjectByType<ArenaBootstrap>();
            Assert.That(arena, Is.Not.Null);
            Assert.That(arena.BlueTower, Is.Not.Null);
            Assert.That(arena.RedTower, Is.Not.Null);
            AssertTowerIsCombatReady(arena.BlueTower);
            AssertTowerIsCombatReady(arena.RedTower);

            MatchOutcomeController outcome = Object.FindFirstObjectByType<MatchOutcomeController>();
            Assert.That(outcome, Is.Not.Null);
            Assert.That(outcome.Outcome, Is.EqualTo(MatchOutcome.None));
            Assert.That(Object.FindFirstObjectByType<MinionWaveSpawner>(), Is.Not.Null);
            GameObject player = GameObject.Find("Player_Exusiai");
            Assert.That(player, Is.Not.Null);
            CombatUnit playerUnit = player.GetComponent<CombatUnit>();
            Assert.That(playerUnit, Is.Not.Null);
            Assert.That(playerUnit.MaxHealth, Is.EqualTo(1000f));
            Assert.That(playerUnit.AttackPower, Is.EqualTo(50f));
            Assert.That(player.transform.localScale, Is.EqualTo(new Vector3(1.6f, 2f, 1.6f)));
            Assert.That(player.transform.position.y, Is.EqualTo(2f));
            Assert.That(player.GetComponent<HealthBarPresenter>(), Is.Not.Null);
            GameObject enemyOperator = GameObject.Find("TrainingDummy_Red");
            Assert.That(enemyOperator, Is.Not.Null);
            Assert.That(enemyOperator.transform.localScale, Is.EqualTo(player.transform.localScale));
            Assert.That(enemyOperator.transform.position.y, Is.EqualTo(player.transform.position.y));
            Assert.That(enemyOperator.GetComponent<CapsuleCollider>(), Is.Not.Null);
            Assert.That(enemyOperator.GetComponent<HealthBarPresenter>(), Is.Not.Null);
            Assert.That(enemyOperator.GetComponent<DeathCorpsePresenter>(), Is.Not.Null);
            Assert.That(enemyOperator.transform.Find("HealthBar"), Is.Not.Null);
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            Assert.That(mainCamera, Is.Not.Null);
            Assert.That(mainCamera.transform.position, Is.EqualTo(new Vector3(0f, 42f, -34f)));
        }

        [UnityTest]
        public IEnumerator SavedTowerControllerRebindsAndTargetsNearbyEnemy()
        {
            SceneManager.LoadScene("PrototypeArena");
            yield return null;

            ArenaBootstrap arena = Object.FindFirstObjectByType<ArenaBootstrap>();
            CombatUnit blueTower = arena.BlueTower.GetComponent<CombatUnit>();
            BasicAttackController blueTowerAttack = arena.BlueTower.GetComponent<BasicAttackController>();
            GameObject enemyObject = new GameObject("TowerRebindEnemy");
            enemyObject.transform.position = blueTower.transform.position + Vector3.right;
            CombatUnit enemy = enemyObject.AddComponent<CombatUnit>();
            enemy.Configure(TeamId.Red, Altitude.Ground, 1000f, 0f, 0f, 0f, 0f, false, false);

            yield return null;

            Assert.That(blueTowerAttack.CurrentTarget, Is.EqualTo(enemy));
        }

        [UnityTest]
        public IEnumerator SavedOutcomeControllerResolvesDestroyedTower()
        {
            SceneManager.LoadScene("PrototypeArena");
            yield return null;

            ArenaBootstrap arena = Object.FindFirstObjectByType<ArenaBootstrap>();
            MatchOutcomeController outcome = Object.FindFirstObjectByType<MatchOutcomeController>();
            GameObject redTowerObject = arena.RedTower.gameObject;
            CombatUnit redTower = redTowerObject.GetComponent<CombatUnit>();
            Renderer redTowerRenderer = redTowerObject.transform.Find("RedTowerVisual").GetComponent<Renderer>();
            Material redMaterial = redTowerRenderer.sharedMaterial;
            Transform healthBar = redTowerObject.transform.Find("HealthBar");
            Assert.That(healthBar, Is.Not.Null);

            redTower.TakePhysicalDamage(redTower.MaxHealth);
            yield return null;

            Assert.That(outcome.IsMatchOver, Is.True);
            Assert.That(outcome.Outcome, Is.EqualTo(MatchOutcome.BlueVictory));
            Assert.That(redTowerObject == null, Is.True);
            Assert.That(healthBar == null, Is.True);
            GameObject corpse = GameObject.Find("RedTower_Corpse");
            Assert.That(corpse, Is.Not.Null);
            Assert.That(corpse.transform.localScale, Is.EqualTo(new Vector3(0.3f, 1f, 0.3f)));
            Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.SameAs(redMaterial));
            Assert.That(corpse.GetComponent<CombatUnit>(), Is.Null);
            Assert.That(corpse.GetComponent<HealthBarPresenter>(), Is.Null);
            Assert.That(corpse.GetComponent<BasicAttackController>(), Is.Null);
            Assert.That(corpse.GetComponent<TowerCombatController>(), Is.Null);
            Assert.That(corpse.layer, Is.EqualTo(LayerMask.NameToLayer("Default")));
            Assert.That(corpse.GetComponent<Collider>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator SavedPlayerDeathCreatesGroundedCorpseWithoutRuntimeConfigure()
        {
            SceneManager.LoadScene("PrototypeArena");
            yield return null;

            GameObject player = GameObject.Find("Player_Exusiai");
            CombatUnit playerUnit = player.GetComponent<CombatUnit>();
            Renderer playerRenderer = player.GetComponent<Renderer>();
            Vector3 playerPosition = player.transform.position;
            Material playerMaterial = playerRenderer.sharedMaterial;
            Assert.That(player.transform.Find("HealthBar"), Is.Not.Null);

            playerUnit.TakePhysicalDamage(playerUnit.MaxHealth);

            Assert.That(playerRenderer.enabled, Is.False);
            yield return null;

            Assert.That(player == null, Is.True);
            GameObject corpse = GameObject.Find("Player_Exusiai_Corpse");
            Assert.That(corpse, Is.Not.Null);
            Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.SameAs(playerMaterial));
            Assert.That(corpse.transform.position.x, Is.EqualTo(playerPosition.x));
            Assert.That(corpse.transform.position.y, Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(corpse.transform.position.z, Is.EqualTo(playerPosition.z));
            Assert.That(corpse.transform.Find("HealthBar"), Is.Null);
            Collider collider = corpse.GetComponent<Collider>();
            Assert.That(collider == null || !collider.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator SavedEnemyOperatorDeathCreatesGroundedCorpse()
        {
            SceneManager.LoadScene("PrototypeArena");
            yield return null;

            GameObject enemyOperator = GameObject.Find("TrainingDummy_Red");
            CombatUnit unit = enemyOperator.GetComponent<CombatUnit>();
            Material material = enemyOperator.GetComponent<Renderer>().sharedMaterial;
            Vector3 position = enemyOperator.transform.position;

            unit.TakePhysicalDamage(unit.MaxHealth);
            yield return null;

            Assert.That(enemyOperator == null, Is.True);
            GameObject corpse = GameObject.Find("TrainingDummy_Red_Corpse");
            Assert.That(corpse, Is.Not.Null);
            Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
            Assert.That(corpse.transform.position, Is.EqualTo(new Vector3(position.x, 0.01f, position.z)));
            Assert.That(corpse.GetComponent<CombatUnit>(), Is.Null);
            Assert.That(corpse.transform.Find("HealthBar"), Is.Null);
            Collider collider = corpse.GetComponent<Collider>();
            Assert.That(collider == null || !collider.enabled, Is.True);
        }

        private static void AssertTowerIsCombatReady(Transform tower)
        {
            Assert.That(tower.GetComponent<CombatUnit>(), Is.Not.Null, $"{tower.name} needs a CombatUnit.");
            Assert.That(tower.GetComponent<BasicAttackController>(), Is.Not.Null,
                $"{tower.name} needs a BasicAttackController.");
            Assert.That(tower.GetComponent<TowerCombatController>(), Is.Not.Null,
                $"{tower.name} needs a TowerCombatController.");
            Assert.That(tower.GetComponent<DeathCorpsePresenter>(), Is.Not.Null,
                $"{tower.name} needs a DeathCorpsePresenter.");
            Assert.That(tower.GetComponent<BoxCollider>(), Is.Not.Null,
                $"{tower.name} needs a root BoxCollider.");
            CombatUnit combatUnit = tower.GetComponent<CombatUnit>();
            Assert.That(combatUnit.MaxHealth, Is.EqualTo(500f),
                $"{tower.name} should have 500 max health.");
            Assert.That(combatUnit.AttackPower, Is.EqualTo(20f),
                $"{tower.name} should have 20 attack power.");
            HealthBarPresenter healthBar = tower.GetComponent<HealthBarPresenter>();
            Assert.That(healthBar, Is.Not.Null,
                $"{tower.name} needs a HealthBarPresenter.");
            Assert.That(healthBar.IsVisible, Is.True,
                $"{tower.name} health bar should be visible.");
            Assert.That(tower.Find("HealthBar"), Is.Not.Null,
                $"{tower.name} should contain a HealthBar child.");
        }
    }
}
