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
            Assert.That(player.transform.localScale, Is.EqualTo(new Vector3(1.6f, 2f, 1.6f)));
            Assert.That(player.transform.position.y, Is.EqualTo(2f));
            Assert.That(player.GetComponent<HealthBarPresenter>(), Is.Not.Null);
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
            CombatUnit redTower = arena.RedTower.GetComponent<CombatUnit>();

            redTower.TakePhysicalDamage(redTower.MaxHealth);
            yield return null;

            Assert.That(outcome.IsMatchOver, Is.True);
            Assert.That(outcome.Outcome, Is.EqualTo(MatchOutcome.BlueVictory));
        }

        private static void AssertTowerIsCombatReady(Transform tower)
        {
            Assert.That(tower.GetComponent<CombatUnit>(), Is.Not.Null, $"{tower.name} needs a CombatUnit.");
            Assert.That(tower.GetComponent<BasicAttackController>(), Is.Not.Null,
                $"{tower.name} needs a BasicAttackController.");
            Assert.That(tower.GetComponent<TowerCombatController>(), Is.Not.Null,
                $"{tower.name} needs a TowerCombatController.");
            Assert.That(tower.GetComponent<BoxCollider>(), Is.Not.Null,
                $"{tower.name} needs a root BoxCollider.");
        }
    }
}
