using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using NUnit.Framework;
using UnityEngine;
using MatchOutcome = ArknightsFrontline.Common.MatchOutcome;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class MatchOutcomeControllerTests
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
        public void RedTowerDeathResolvesBlueVictoryOnTick()
        {
            MatchFixture match = CreateMatch();
            match.RedTower.TakePhysicalDamage(1000f);

            match.OutcomeController.Tick();

            Assert.That(match.OutcomeController.IsMatchOver, Is.True);
            Assert.That(match.OutcomeController.Outcome, Is.EqualTo(MatchOutcome.BlueVictory));
        }

        [Test]
        public void BlueTowerDeathResolvesRedVictoryOnTick()
        {
            MatchFixture match = CreateMatch();
            match.BlueTower.TakePhysicalDamage(1000f);

            match.OutcomeController.Tick();

            Assert.That(match.OutcomeController.IsMatchOver, Is.True);
            Assert.That(match.OutcomeController.Outcome, Is.EqualTo(MatchOutcome.RedVictory));
        }

        [Test]
        public void BothTowerDeathsBeforeOneTickResolveDraw()
        {
            MatchFixture match = CreateMatch();
            match.BlueTower.TakePhysicalDamage(1000f);
            match.RedTower.TakePhysicalDamage(1000f);

            match.OutcomeController.Tick();

            Assert.That(match.OutcomeController.IsMatchOver, Is.True);
            Assert.That(match.OutcomeController.Outcome, Is.EqualTo(MatchOutcome.Draw));
        }

        [Test]
        public void ConfigureAfterSettlementPreservesResolvedOutcome()
        {
            MatchFixture match = CreateMatch();
            match.RedTower.TakePhysicalDamage(1000f);
            match.OutcomeController.Tick();

            match.OutcomeController.Configure(match.BlueTower, match.RedTower, match.Spawner);

            Assert.That(match.OutcomeController.IsMatchOver, Is.True);
            Assert.That(match.OutcomeController.Outcome, Is.EqualTo(MatchOutcome.BlueVictory));
        }

        [Test]
        public void ResolvingOutcomeStopsSpawnsRetargetingAttacksAndProjectileDamage()
        {
            MatchFixture match = CreateMatch();
            CombatUnit blueMinion = CreateUnit(
                "BlueMinion",
                TeamId.Blue,
                match.BlueTower.transform.position + Vector3.right,
                8f);
            UnitMotor motor = blueMinion.gameObject.AddComponent<UnitMotor>();
            motor.Configure(3f, ArenaLayout.CreateDefault());
            BasicAttackController minionAttack = blueMinion.gameObject.AddComponent<BasicAttackController>();
            LaneMinionController minionController = blueMinion.gameObject.AddComponent<LaneMinionController>();
            minionController.Configure(
                blueMinion,
                motor,
                minionAttack,
                match.RedTower,
                match.RedTower.transform.position);

            CombatUnit redMinion = CreateUnit(
                "RedMinion",
                TeamId.Red,
                match.BlueTower.transform.position + Vector3.right * 2f,
                2f);
            BasicAttackController towerAttack = match.BlueTower.gameObject.AddComponent<BasicAttackController>();
            TowerCombatController towerController = match.BlueTower.gameObject.AddComponent<TowerCombatController>();
            towerController.Configure(match.BlueTower, towerAttack);
            minionController.Tick(0f);
            towerController.Tick(0f);
            Assert.That(minionAttack.CurrentTarget, Is.EqualTo(redMinion));
            Assert.That(towerAttack.CurrentTarget, Is.EqualTo(redMinion));

            Projectile projectile = CreateGameObject("Projectile").AddComponent<Projectile>();
            projectile.Initialize(blueMinion, redMinion, 12f, 16f);
            float targetHealthBeforeOutcome = redMinion.CurrentHealth;
            int waveCountBeforeOutcome = match.Spawner.SpawnedWaveCount;

            match.RedTower.TakePhysicalDamage(1000f);
            match.OutcomeController.Tick();
            match.Spawner.Tick(100f);
            match.Spawner.SpawnWaveNow();
            minionController.Tick(0f);
            towerController.Tick(0f);
            projectile.Tick(10f);

            Assert.That(match.Spawner.SpawnedWaveCount, Is.EqualTo(waveCountBeforeOutcome));
            Assert.That(minionAttack.CurrentTarget, Is.Null);
            Assert.That(towerAttack.CurrentTarget, Is.Null);
            Assert.That(minionAttack.enabled, Is.False);
            Assert.That(towerAttack.enabled, Is.False);
            Assert.That(projectile.IsFinished, Is.True);
            Assert.That(redMinion.CurrentHealth, Is.EqualTo(targetHealthBeforeOutcome));
        }

        private MatchFixture CreateMatch()
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit blueTower = CreateUnit("BlueTower", TeamId.Blue, layout.BlueTower, 8f);
            CombatUnit redTower = CreateUnit("RedTower", TeamId.Red, layout.RedTower, 8f);
            GameObject minionParent = CreateGameObject("MinionParent");
            MinionWaveSpawner spawner = CreateGameObject("Spawner").AddComponent<MinionWaveSpawner>();
            spawner.Configure(
                minionParent.transform,
                layout,
                blueTower,
                redTower,
                CreateMaterial(Color.blue),
                CreateMaterial(Color.red),
                8,
                8);
            MatchOutcomeController outcomeController = CreateGameObject("MatchOutcomeController")
                .AddComponent<MatchOutcomeController>();
            outcomeController.Configure(blueTower, redTower, spawner);
            return new MatchFixture(blueTower, redTower, spawner, outcomeController);
        }

        private CombatUnit CreateUnit(string name, TeamId team, Vector3 position, float attackRange)
        {
            GameObject gameObject = CreateGameObject(name);
            gameObject.transform.position = position;
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, 100f, 12f, 0f, attackRange, 1f, true, true);
            return unit;
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
