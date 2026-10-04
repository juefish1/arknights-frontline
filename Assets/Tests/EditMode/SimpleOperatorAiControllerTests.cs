using System;
using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class SimpleOperatorAiControllerTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in objects)
            {
                if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
            }

            objects.Clear();
        }

        [Test]
        public void RecentOperatorDamageOutranksCloserEnemyOperator()
        {
            AiFixture ai = CreateAi(OperatorType.Exusiai, TeamId.Blue, Vector3.zero, 8f);
            CombatUnit recentAttacker = CreateUnit("recent-operator", TeamId.Red, new Vector3(6f, 0f, 0f), 1f);
            recentAttacker.gameObject.AddComponent<OperatorIdentity>().Configure("recent", TeamId.Red, OperatorType.Exusiai);
            CombatUnit closerOperator = CreateUnit("closer-operator", TeamId.Red, new Vector3(2f, 0f, 0f), 1f);
            closerOperator.gameObject.AddComponent<OperatorIdentity>().Configure("closer", TeamId.Red, OperatorType.SilverAsh);

            ai.Owner.TakePhysicalDamage(1f, recentAttacker);
            ai.Controller.Tick(0.1f);

            Assert.That(ai.Attacks.CurrentTarget, Is.EqualTo(recentAttacker));
        }

        [Test]
        public void SelectionUsesOperatorThenMinionThenTowerPriority()
        {
            AiFixture ai = CreateAi(OperatorType.Exusiai, TeamId.Blue, Vector3.zero, 8f);
            CombatUnit enemyTower = CreateUnit("enemy-tower", TeamId.Red, new Vector3(5f, 0f, 0f), 1f);
            enemyTower.gameObject.AddComponent<TowerCombatController>();
            CombatUnit enemyMinion = CreateMinion("enemy-minion", TeamId.Red, new Vector3(4f, 0f, 0f), enemyTower);
            CombatUnit enemyOperator = CreateUnit("enemy-operator", TeamId.Red, new Vector3(7f, 0f, 0f), 1f);
            enemyOperator.gameObject.AddComponent<OperatorIdentity>().Configure("enemy-op", TeamId.Red, OperatorType.Eyjafjalla);

            ai.Controller.Tick(0.1f);
            Assert.That(ai.Attacks.CurrentTarget, Is.EqualTo(enemyOperator));

            enemyOperator.TakePhysicalDamage(1f);
            ai.Controller.Tick(0.1f);
            Assert.That(ai.Attacks.CurrentTarget, Is.EqualTo(enemyMinion));

            enemyMinion.TakePhysicalDamage(enemyMinion.CurrentHealth);
            ai.Controller.Tick(0.1f);
            Assert.That(ai.Attacks.CurrentTarget, Is.EqualTo(enemyTower));
        }

        [Test]
        public void EqualDistanceTargetsUseLowestUnityEntityId()
        {
            AiFixture ai = CreateAi(OperatorType.Exusiai, TeamId.Blue, Vector3.zero, 5f);
            CombatUnit first = CreateUnit("first", TeamId.Red, new Vector3(-3f, 0f, 0f), 1f);
            first.gameObject.AddComponent<OperatorIdentity>().Configure("first", TeamId.Red, OperatorType.Exusiai);
            CombatUnit second = CreateUnit("second", TeamId.Red, new Vector3(3f, 0f, 0f), 1f);
            second.gameObject.AddComponent<OperatorIdentity>().Configure("second", TeamId.Red, OperatorType.SilverAsh);
            CombatUnit expected = Comparer<EntityId>.Default.Compare(first.GetEntityId(), second.GetEntityId()) < 0 ? first : second;

            ai.Controller.Tick(0f);

            Assert.That(ai.Attacks.CurrentTarget, Is.EqualTo(expected));
        }

        [Test]
        public void SilverAshCannotTargetAirWhileExusiaiAndEyjafjallaCan()
        {
            CombatUnit airTarget = CreateUnit("air-target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, Altitude.Air);
            airTarget.gameObject.AddComponent<OperatorIdentity>().Configure(
                "air-target", TeamId.Red, OperatorType.Exusiai);

            AiFixture silverAsh = CreateAi(OperatorType.SilverAsh, TeamId.Blue, Vector3.zero, 5f, false);
            silverAsh.Controller.Tick(0f);
            Assert.That(silverAsh.Attacks.CurrentTarget, Is.Null);

            AiFixture exusiai = CreateAi(OperatorType.Exusiai, TeamId.Blue, new Vector3(20f, 0f, 0f), 5f, true);
            airTarget.transform.position = new Vector3(22f, 0f, 0f);
            exusiai.Controller.Tick(0f);
            Assert.That(exusiai.Attacks.CurrentTarget, Is.EqualTo(airTarget));

            AiFixture eyjafjalla = CreateAi(OperatorType.Eyjafjalla, TeamId.Blue, new Vector3(-20f, 0f, 0f), 5f, true);
            airTarget.transform.position = new Vector3(-18f, 0f, 0f);
            eyjafjalla.Controller.Tick(0f);
            Assert.That(eyjafjalla.Attacks.CurrentTarget, Is.EqualTo(airTarget));
        }

        [Test]
        public void OutOfRangeAndInvalidTargetsAreNotAttackedAndCurrentTargetIsCleared()
        {
            AiFixture ai = CreateAi(OperatorType.Exusiai, TeamId.Blue, Vector3.zero, 5f);
            CombatUnit enemy = CreateUnit("enemy", TeamId.Red, new Vector3(4f, 0f, 0f), 1f);
            enemy.gameObject.AddComponent<OperatorIdentity>().Configure("enemy", TeamId.Red, OperatorType.Exusiai);

            ai.Controller.Tick(0f);
            Assert.That(ai.Attacks.CurrentTarget, Is.EqualTo(enemy));

            enemy.transform.position = new Vector3(5.1f, 0f, 0f);
            ai.Controller.Tick(0f);

            Assert.That(ai.Attacks.CurrentTarget, Is.Null);
            Assert.That(ai.Motor.IsMoving, Is.True);
        }

        [Test]
        public void FollowsNearestFriendlyMinionTwoMetersBehindLaneDirection()
        {
            AiFixture ai = CreateAi(OperatorType.Exusiai, TeamId.Blue, Vector3.zero, 5f);
            CombatUnit enemyTower = CreateUnit("enemy-tower", TeamId.Red, new Vector3(40f, 0f, 0f), 1f);
            enemyTower.gameObject.AddComponent<TowerCombatController>();
            CreateMinion("near-minion", TeamId.Blue, new Vector3(4f, 0f, 0f), enemyTower);
            CreateMinion("far-minion", TeamId.Blue, new Vector3(7f, 0f, 0f), enemyTower);

            ai.Controller.Tick(0f);
            ai.Motor.Tick(1f);

            Assert.That(ai.Owner.transform.position.x, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void FollowsChosenFriendlyMinionsLaneOffsetWhenItsZDiffersFromOwner()
        {
            AiFixture ai = CreateAi(OperatorType.Exusiai, TeamId.Blue, Vector3.zero, 5f);
            CombatUnit enemyTower = CreateUnit("enemy-tower", TeamId.Red, new Vector3(40f, 0f, 0f), 1f);
            enemyTower.gameObject.AddComponent<TowerCombatController>();
            CreateMinion("offset-minion", TeamId.Blue, new Vector3(4f, 0f, 5f), enemyTower);

            ai.Controller.Tick(0f);
            ai.Motor.Tick(2f);

            Assert.That(ai.Owner.transform.position.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(ai.Owner.transform.position.z, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void WithNoFriendlyMinionsMovesFourMetersAheadOfFriendlyTowerAndClampsDestination()
        {
            AiFixture ai = CreateAi(OperatorType.Exusiai, TeamId.Blue, new Vector3(-30f, 0f, 15f), 5f);

            ai.Controller.Tick(0f);
            ai.Motor.Tick(10f);

            Assert.That(ai.Owner.transform.position.x, Is.EqualTo(-34f).Within(0.001f));
            Assert.That(ai.Owner.transform.position.z, Is.EqualTo(12f).Within(0.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LowHealthRetreatsAndWaitsForOneAndAHalfSecondsFromTheInterruption(bool attackerIsTower)
        {
            float now = 0f;
            AiFixture ai = CreateAi(OperatorType.Exusiai, TeamId.Blue, Vector3.zero, 8f, timeProvider: () => now);
            CombatUnit attacker = CreateUnit(
                attackerIsTower ? "enemy-tower" : "enemy-operator",
                TeamId.Red,
                new Vector3(3f, 0f, 0f),
                100f);
            if (attackerIsTower)
            {
                attacker.gameObject.AddComponent<TowerCombatController>();
            }
            else
            {
                attacker.gameObject.AddComponent<OperatorIdentity>().Configure(
                    "attacker", TeamId.Red, OperatorType.Exusiai);
            }

            ai.Owner.TakePhysicalDamage(76f);

            ai.Controller.Tick(0f);
            Assert.That(ai.Retreat.IsGuiding, Is.True);

            now = 1f;
            ai.Owner.TakePhysicalDamage(1f, attacker);
            Assert.That(ai.Retreat.IsGuiding, Is.False);

            now = 2.49f;
            ai.Controller.Tick(2.49f);
            Assert.That(ai.Retreat.IsGuiding, Is.False);
            Assert.That(ai.Attacks.CurrentTarget, Is.EqualTo(attacker), "The computer should keep attacking during the safety wait.");

            now = 2.5f;
            ai.Controller.Tick(0f);
            Assert.That(ai.Retreat.IsGuiding, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ASecondEnemyOperatorOrTowerHitRestartsTheSafeRetryWindow(bool secondAttackerIsTower)
        {
            float now = 0f;
            AiFixture ai = CreateAi(OperatorType.Exusiai, TeamId.Blue, Vector3.zero, 8f, timeProvider: () => now);
            CombatUnit firstAttacker = CreateUnit("first-enemy", TeamId.Red, new Vector3(3f, 0f, 0f), 100f);
            firstAttacker.gameObject.AddComponent<OperatorIdentity>().Configure(
                "first-attacker", TeamId.Red, OperatorType.Exusiai);
            CombatUnit secondAttacker = CreateUnit(
                secondAttackerIsTower ? "second-tower" : "second-operator",
                TeamId.Red,
                new Vector3(4f, 0f, 0f),
                100f);
            if (secondAttackerIsTower)
            {
                secondAttacker.gameObject.AddComponent<TowerCombatController>();
            }
            else
            {
                secondAttacker.gameObject.AddComponent<OperatorIdentity>().Configure(
                    "second-attacker", TeamId.Red, OperatorType.SilverAsh);
            }

            ai.Owner.TakePhysicalDamage(76f);
            ai.Controller.Tick(0f);
            ai.Owner.TakePhysicalDamage(1f, firstAttacker);
            Assert.That(ai.Retreat.IsGuiding, Is.False);

            now = 1f;
            ai.Owner.TakePhysicalDamage(1f, secondAttacker);
            now = 2.49f;
            ai.Controller.Tick(2.49f);
            Assert.That(ai.Retreat.IsGuiding, Is.False,
                "The newer enemy hit must restart, not inherit, the full quiet interval.");

            now = 2.5f;
            ai.Controller.Tick(0f);
            Assert.That(ai.Retreat.IsGuiding, Is.True);
        }

        [Test]
        public void RecentOperatorThreatUsesDamageTimestampInsteadOfWholeTickDelta()
        {
            float now = 0f;
            AiFixture ai = CreateAi(OperatorType.Exusiai, TeamId.Blue, Vector3.zero, 8f, timeProvider: () => now);
            CombatUnit recentAttacker = CreateUnit("recent", TeamId.Red, new Vector3(6f, 0f, 0f), 100f);
            recentAttacker.gameObject.AddComponent<OperatorIdentity>().Configure("recent", TeamId.Red, OperatorType.Exusiai);
            CombatUnit closerOperator = CreateUnit("closer", TeamId.Red, new Vector3(2f, 0f, 0f), 100f);
            closerOperator.gameObject.AddComponent<OperatorIdentity>().Configure("closer", TeamId.Red, OperatorType.SilverAsh);

            now = 1f;
            ai.Owner.TakePhysicalDamage(1f, recentAttacker);
            now = 2.49f;
            ai.Controller.Tick(2.49f);
            Assert.That(ai.Attacks.CurrentTarget, Is.EqualTo(recentAttacker),
                "A frame delta includes time before the hit; the threat is only 1.49 seconds old.");

            now = 3f;
            ai.Controller.Tick(0f);
            Assert.That(ai.Attacks.CurrentTarget, Is.EqualTo(recentAttacker), "The inclusive two-second boundary is still recent.");

            now = 3.001f;
            ai.Controller.Tick(0f);
            Assert.That(ai.Attacks.CurrentTarget, Is.EqualTo(closerOperator));
        }

        [Test]
        public void OwnerDeathStopsAiAndClearsItsTarget()
        {
            AiFixture ai = CreateAi(OperatorType.Exusiai, TeamId.Blue, Vector3.zero, 8f);
            CombatUnit enemy = CreateUnit("enemy", TeamId.Red, new Vector3(3f, 0f, 0f), 100f);
            enemy.gameObject.AddComponent<OperatorIdentity>().Configure("enemy", TeamId.Red, OperatorType.Exusiai);
            ai.Controller.Tick(0f);
            Assert.That(ai.Controller.CurrentTarget, Is.EqualTo(enemy));

            ai.Owner.TakePhysicalDamage(ai.Owner.CurrentHealth);
            ai.Controller.Tick(1f);

            Assert.That(ai.Owner.IsDead, Is.True);
            Assert.That(ai.Controller.CurrentTarget, Is.Null);
        }

        [Test]
        public void MatchEndingStopsAiAndClearsItsTarget()
        {
            AiFixture ai = CreateAi(OperatorType.Exusiai, TeamId.Blue, Vector3.zero, 8f);
            CombatUnit enemyTower = CreateUnit("red-tower", TeamId.Red, new Vector3(30f, 0f, 0f), 1f);
            enemyTower.gameObject.AddComponent<TowerCombatController>();
            MinionWaveSpawner spawner = CreateObject("spawner").AddComponent<MinionWaveSpawner>();
            MatchOutcomeController match = CreateObject("match").AddComponent<MatchOutcomeController>();
            match.Configure(ai.FriendlyTower, enemyTower, spawner);
            ai.Controller.Configure(ai.Identity, null, ai.FriendlyTower, ai.Retreat, ArenaLayout.CreateDefault(), match);

            CombatUnit enemy = CreateUnit("enemy", TeamId.Red, new Vector3(3f, 0f, 0f), 100f);
            enemy.gameObject.AddComponent<OperatorIdentity>().Configure("enemy", TeamId.Red, OperatorType.Exusiai);
            ai.Controller.Tick(0f);
            Assert.That(ai.Controller.CurrentTarget, Is.EqualTo(enemy));

            enemyTower.TakePhysicalDamage(enemyTower.CurrentHealth);
            ai.Controller.Tick(0f);

            Assert.That(match.IsEnding, Is.True);
            Assert.That(ai.Controller.CurrentTarget, Is.Null);
        }

        [Test]
        public void StoppedRosterSlotStopsItsAi()
        {
            CombatUnit friendlyTower = CreateUnit("blue-tower", TeamId.Blue, new Vector3(-50f, 0f, 0f), 1000f);
            OperatorRosterController roster = CreateObject("roster").AddComponent<OperatorRosterController>();
            GameObject template = CreateObject("template");
            template.SetActive(false);
            CombatUnit templateUnit = template.AddComponent<CombatUnit>();
            templateUnit.Configure(TeamId.Blue, Altitude.Ground, 100f, 1f, 0f, 8f, 1f, true, true);
            template.AddComponent<OperatorIdentity>().Configure("slot", TeamId.Blue, OperatorType.Exusiai);
            template.AddComponent<UnitMotor>().Configure(5f, ArenaLayout.CreateDefault());
            template.AddComponent<BasicAttackController>().Configure(templateUnit);
            template.AddComponent<OperatorRetreatController>();
            template.AddComponent<SimpleOperatorAiController>();
            template.AddComponent<DeathCorpsePresenter>();
            OperatorRosterSlot slot = roster.RegisterSlot(
                "slot", TeamId.Blue, OperatorType.Exusiai, template, Vector3.zero);
            roster.OperatorSpawned += (spawnedSlot, live) =>
            {
                objects.Add(live.gameObject);
                live.GetComponent<SimpleOperatorAiController>().Configure(
                    live.GetComponent<OperatorIdentity>(), spawnedSlot, friendlyTower,
                    live.GetComponent<OperatorRetreatController>(), ArenaLayout.CreateDefault());
            };
            roster.StartMatch();
            CombatUnit owner = slot.CurrentOperator;
            SimpleOperatorAiController controller = owner.GetComponent<SimpleOperatorAiController>();
            CombatUnit enemy = CreateUnit("enemy", TeamId.Red, new Vector3(3f, 0f, 0f), 100f);
            enemy.gameObject.AddComponent<OperatorIdentity>().Configure("enemy", TeamId.Red, OperatorType.Exusiai);
            controller.Tick(0f);
            Assert.That(controller.CurrentTarget, Is.EqualTo(enemy));

            roster.StopForMatch();
            controller.Tick(0f);

            Assert.That(slot.IsStopped, Is.True);
            Assert.That(controller.CurrentTarget, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FirstLowHealthDecisionRetreatsImmediatelyWhenEnemyThreatDamageCrossesThreshold(bool sourceIsTower)
        {
            AiFixture ai = CreateAi(OperatorType.Exusiai, TeamId.Blue, Vector3.zero, 8f);
            CombatUnit attacker = CreateUnit(
                sourceIsTower ? "enemy-tower" : "enemy-operator",
                TeamId.Red,
                new Vector3(3f, 0f, 0f),
                100f);
            if (sourceIsTower)
            {
                attacker.gameObject.AddComponent<TowerCombatController>();
            }
            else
            {
                attacker.gameObject.AddComponent<OperatorIdentity>().Configure(
                    "enemy-operator", TeamId.Red, OperatorType.Exusiai);
            }

            ai.Owner.TakePhysicalDamage(76f, attacker);
            ai.Controller.Tick(0f);

            Assert.That(ai.Owner.CurrentHealth / ai.Owner.MaxHealth, Is.LessThan(0.25f));
            Assert.That(ai.Retreat.IsGuiding, Is.True);
        }

        private AiFixture CreateAi(
            OperatorType operatorType,
            TeamId team,
            Vector3 position,
            float attackRange,
            bool canAttackAir = true,
            Func<float> timeProvider = null)
        {
            CombatUnit friendlyTower = CreateUnit("friendly-tower-" + objects.Count, team,
                team == TeamId.Blue ? ArenaLayout.CreateDefault().BlueTower : ArenaLayout.CreateDefault().RedTower, 100f);
            friendlyTower.gameObject.AddComponent<TowerCombatController>();

            GameObject gameObject = CreateObject("ai-" + operatorType + "-" + objects.Count);
            gameObject.transform.position = position;
            CombatUnit owner = gameObject.AddComponent<CombatUnit>();
            owner.Configure(team, Altitude.Ground, 100f, 20f, 0f, attackRange, 0.5f, true, canAttackAir);
            OperatorIdentity identity = gameObject.AddComponent<OperatorIdentity>();
            identity.Configure(gameObject.name, team, operatorType);
            UnitMotor motor = gameObject.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());
            BasicAttackController attacks = gameObject.AddComponent<BasicAttackController>();
            attacks.Configure(owner);
            OperatorRetreatController retreat = gameObject.AddComponent<OperatorRetreatController>();
            SimpleOperatorAiController controller = gameObject.AddComponent<SimpleOperatorAiController>();
            controller.Configure(identity, null, friendlyTower, retreat, ArenaLayout.CreateDefault(), null, timeProvider);
            return new AiFixture(owner, identity, friendlyTower, motor, attacks, retreat, controller);
        }

        private CombatUnit CreateMinion(string name, TeamId team, Vector3 position, CombatUnit enemyTower)
        {
            GameObject gameObject = CreateObject(name);
            gameObject.transform.position = position;
            CombatUnit minion = gameObject.AddComponent<CombatUnit>();
            minion.Configure(team, Altitude.Ground, 100f, 1f, 0f, 1f, 1f, true, false);
            UnitMotor motor = gameObject.AddComponent<UnitMotor>();
            motor.Configure(3f, ArenaLayout.CreateDefault());
            BasicAttackController attack = gameObject.AddComponent<BasicAttackController>();
            attack.Configure(minion);
            gameObject.AddComponent<LaneMinionController>().Configure(
                minion, motor, attack, enemyTower, enemyTower.transform.position);
            return minion;
        }

        private CombatUnit CreateUnit(string name, TeamId team, Vector3 position, float health, Altitude altitude = Altitude.Ground)
        {
            GameObject gameObject = CreateObject(name);
            gameObject.transform.position = position;
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, altitude, health, 0f, 0f, 0f, 1f, true, true);
            return unit;
        }

        private GameObject CreateObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            objects.Add(gameObject);
            return gameObject;
        }

        private sealed class AiFixture
        {
            public AiFixture(
                CombatUnit owner,
                OperatorIdentity identity,
                CombatUnit friendlyTower,
                UnitMotor motor,
                BasicAttackController attacks,
                OperatorRetreatController retreat,
                SimpleOperatorAiController controller)
            {
                Owner = owner;
                Identity = identity;
                FriendlyTower = friendlyTower;
                Motor = motor;
                Attacks = attacks;
                Retreat = retreat;
                Controller = controller;
            }

            public CombatUnit Owner { get; }
            public OperatorIdentity Identity { get; }
            public CombatUnit FriendlyTower { get; }
            public UnitMotor Motor { get; }
            public BasicAttackController Attacks { get; }
            public OperatorRetreatController Retreat { get; }
            public SimpleOperatorAiController Controller { get; }
        }
    }
}
