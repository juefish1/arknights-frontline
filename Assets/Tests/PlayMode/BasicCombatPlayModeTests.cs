using System.Collections;
using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    [DefaultExecutionOrder(-1000)]
    public sealed class EarlyProjectileTickDriver : MonoBehaviour
    {
        private Projectile projectile;
        private bool hasTicked;

        public void Configure(Projectile projectileToTick)
        {
            projectile = projectileToTick;
        }

        private void Update()
        {
            if (hasTicked)
            {
                return;
            }

            hasTicked = true;
            projectile.Tick(1f);
        }
    }

    [DefaultExecutionOrder(1000)]
    public sealed class LateProjectileTickDriver : MonoBehaviour
    {
        private Projectile projectile;
        private bool hasTicked;

        public void Configure(Projectile projectileToTick)
        {
            projectile = projectileToTick;
        }

        private void Update()
        {
            if (hasTicked)
            {
                return;
            }

            hasTicked = true;
            projectile.Tick(1f);
        }
    }

    [DefaultExecutionOrder(-1000)]
    public sealed class EarlyWaveBoundaryTickDriver : MonoBehaviour
    {
        private MinionWaveSpawner spawner;
        private bool hasTicked;

        public void Configure(MinionWaveSpawner waveSpawner)
        {
            spawner = waveSpawner;
        }

        private void Update()
        {
            if (hasTicked)
            {
                return;
            }

            hasTicked = true;
            spawner.Tick(24.9f);
        }
    }

    public sealed class BasicCombatPlayModeTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject gameObject in gameObjects)
            {
                Object.Destroy(gameObject);
            }

            gameObjects.Clear();
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AttackCommandPursuesThenSpawnsProjectilesAndKillsTarget()
        {
            GameObject playerObject = new GameObject("Player");
            gameObjects.Add(playerObject);
            UnitMotor motor = playerObject.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());
            PlayerCommandController commands = playerObject.AddComponent<PlayerCommandController>();
            CombatUnit player = playerObject.AddComponent<CombatUnit>();
            player.Configure(TeamId.Blue, Altitude.Ground, 100f, 12f, 2f, 6f, 0.5f, true, true);
            BasicAttackController attack = playerObject.AddComponent<BasicAttackController>();
            attack.Configure(player);
            CombatCommandResolver resolver = playerObject.AddComponent<CombatCommandResolver>();
            resolver.Configure(player, motor, commands, attack);

            GameObject targetObject = new GameObject("TrainingDummy_Red");
            gameObjects.Add(targetObject);
            targetObject.transform.position = new Vector3(12f, 0f, 0f);
            CombatUnit target = targetObject.AddComponent<CombatUnit>();
            target.Configure(TeamId.Red, Altitude.Ground, 40f, 0f, 2f, 0f, 0f, false, false);

            commands.Issue(UnitCommand.Attack(targetObject));
            bool movedBeforeAttack = false;
            bool stoppedInRange = false;
            bool spawnedVisibleProjectile = false;

            for (int step = 0; step < 500 && !target.IsDead; step++)
            {
                resolver.Tick(0.05f);
                movedBeforeAttack |= motor.IsMoving;
                motor.Tick(0.05f);
                resolver.Tick(0.05f);
                if (resolver.CurrentTarget == target)
                {
                    stoppedInRange |= !motor.IsMoving;
                }

                attack.Tick(0.05f);
                Projectile[] projectiles = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
                foreach (Projectile projectile in projectiles)
                {
                    spawnedVisibleProjectile |= projectile.GetComponent<Renderer>() != null;
                    projectile.Tick(0.05f);
                }
            }

            Assert.That(movedBeforeAttack, Is.True);
            Assert.That(stoppedInRange, Is.True);
            Assert.That(spawnedVisibleProjectile, Is.True);
            Assert.That(target.IsDead, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SameFrameLethalProjectilesResolveDrawAcrossOutcomeUpdateOrder()
        {
            MatchFixture match = CreateMatch();
            CombatUnit blueAttacker = CreateUnit(
                "BlueProjectileAttacker",
                TeamId.Blue,
                match.RedTower.transform.position,
                100f,
                20f);
            CombatUnit redAttacker = CreateUnit(
                "RedProjectileAttacker",
                TeamId.Red,
                match.BlueTower.transform.position,
                100f,
                20f);
            Projectile redTowerProjectile = CreateLethalProjectile(blueAttacker, match.RedTower);
            Projectile blueTowerProjectile = CreateLethalProjectile(redAttacker, match.BlueTower);

            EarlyProjectileTickDriver earlyDriver = CreateGameObject("EarlyProjectileTickDriver")
                .AddComponent<EarlyProjectileTickDriver>();
            earlyDriver.Configure(redTowerProjectile);
            LateProjectileTickDriver lateDriver = CreateGameObject("LateProjectileTickDriver")
                .AddComponent<LateProjectileTickDriver>();
            lateDriver.Configure(blueTowerProjectile);

            yield return null;
            yield return null;

            Assert.That(match.BlueTower.IsDead, Is.True);
            Assert.That(match.RedTower.IsDead, Is.True);
            Assert.That(match.OutcomeController.IsMatchOver, Is.True);
            Assert.That(match.OutcomeController.Outcome, Is.EqualTo(MatchOutcome.Draw));
        }

        [UnityTest]
        public IEnumerator TowerDeathAtWaveBoundaryPreventsBoundaryWaveRegardlessOfUpdateOrder()
        {
            MatchFixture match = CreateMatch();

            yield return null;
            yield return null;

            Assert.That(match.Spawner.SpawnedWaveCount, Is.EqualTo(1));
            EarlyWaveBoundaryTickDriver boundaryDriver = CreateGameObject("EarlyWaveBoundaryTickDriver")
                .AddComponent<EarlyWaveBoundaryTickDriver>();
            boundaryDriver.Configure(match.Spawner);
            CombatUnit blueAttacker = CreateUnit(
                "BlueBoundaryAttacker",
                TeamId.Blue,
                match.RedTower.transform.position,
                100f,
                20f);
            Projectile lethalProjectile = CreateLethalProjectile(blueAttacker, match.RedTower);
            LateProjectileTickDriver lateDriver = CreateGameObject("LateBoundaryProjectileTickDriver")
                .AddComponent<LateProjectileTickDriver>();
            lateDriver.Configure(lethalProjectile);
            Time.timeScale = 100f;

            yield return null;
            yield return null;
            Time.timeScale = 1f;

            Assert.That(match.RedTower.IsDead, Is.True);
            Assert.That(match.OutcomeController.Outcome, Is.EqualTo(MatchOutcome.BlueVictory));
            Assert.That(match.Spawner.SpawnedWaveCount, Is.EqualTo(1));
        }

        private MatchFixture CreateMatch()
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit blueTower = CreateUnit("BlueTower", TeamId.Blue, layout.BlueTower, 10f, 0f);
            CombatUnit redTower = CreateUnit("RedTower", TeamId.Red, layout.RedTower, 10f, 0f);
            GameObject minionParent = CreateGameObject("MinionParent");
            MinionWaveSpawner spawner = CreateGameObject("MinionWaveSpawner").AddComponent<MinionWaveSpawner>();
            spawner.Configure(minionParent.transform, layout, blueTower, redTower, null, null, 0);
            MatchOutcomeController outcomeController = CreateGameObject("MatchOutcomeController")
                .AddComponent<MatchOutcomeController>();
            outcomeController.Configure(blueTower, redTower, spawner);
            return new MatchFixture(blueTower, redTower, spawner, outcomeController);
        }

        private CombatUnit CreateUnit(string name, TeamId team, Vector3 position, float health, float attackPower)
        {
            GameObject gameObject = CreateGameObject(name);
            gameObject.transform.position = position;
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, health, attackPower, 0f, 10f, 1f, true, true);
            return unit;
        }

        private Projectile CreateLethalProjectile(CombatUnit attacker, CombatUnit target)
        {
            Projectile projectile = CreateGameObject("LethalProjectile").AddComponent<Projectile>();
            projectile.Initialize(attacker, target, target.MaxHealth, 16f);
            projectile.enabled = false;
            return projectile;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            return gameObject;
        }

        private sealed class MatchFixture
        {
            public MatchFixture(
                CombatUnit blueTower,
                CombatUnit redTower,
                MinionWaveSpawner spawner,
                MatchOutcomeController outcomeController)
            {
                BlueTower = blueTower;
                RedTower = redTower;
                Spawner = spawner;
                OutcomeController = outcomeController;
            }

            public CombatUnit BlueTower { get; }

            public CombatUnit RedTower { get; }

            public MinionWaveSpawner Spawner { get; }

            public MatchOutcomeController OutcomeController { get; }
        }
    }
}
