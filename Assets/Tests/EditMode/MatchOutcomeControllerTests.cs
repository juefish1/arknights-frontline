using System.Collections.Generic;
using System.Reflection;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public void BothTowerDeathsResolveDrawBeforeTheSingleFinalNotification()
        {
            MatchFixture match = CreateMatch();
            int resolvedCount = 0;
            MatchOutcome observedOutcome = MatchOutcome.None;
            bool callbackObservedResolvedMatch = false;
            match.OutcomeController.MatchResolved += () =>
            {
                resolvedCount++;
                observedOutcome = match.OutcomeController.Outcome;
                callbackObservedResolvedMatch = match.OutcomeController.IsMatchOver;
            };

            match.BlueTower.TakePhysicalDamage(match.BlueTower.MaxHealth);
            match.RedTower.TakePhysicalDamage(match.RedTower.MaxHealth);
            match.OutcomeController.Tick();
            match.OutcomeController.Tick();

            Assert.That(match.OutcomeController.Outcome, Is.EqualTo(MatchOutcome.Draw));
            Assert.That(resolvedCount, Is.EqualTo(1));
            Assert.That(callbackObservedResolvedMatch, Is.True);
            Assert.That(observedOutcome, Is.EqualTo(MatchOutcome.Draw));
        }

        [Test]
        public void MatchTimeContinuesPastFifteenMinutesWithoutForcingSettlement()
        {
            MatchFixture match = CreateMatch();
            int resolvedCount = 0;
            match.OutcomeController.MatchResolved += () => resolvedCount++;

            match.OutcomeController.Tick(901f);

            Assert.That(match.OutcomeController.ElapsedSeconds, Is.EqualTo(901f));
            Assert.That(match.OutcomeController.IsMatchOver, Is.False);
            Assert.That(match.OutcomeController.Outcome, Is.EqualTo(MatchOutcome.None));
            Assert.That(resolvedCount, Is.Zero);
        }

        [Test]
        public void MatchTimeIgnoresPausedAndNegativeTimeDeltas()
        {
            MatchFixture match = CreateMatch();

            match.OutcomeController.Tick(2.5f);
            match.OutcomeController.Tick(0f);
            match.OutcomeController.Tick(-4f);
            match.OutcomeController.Tick();

            Assert.That(match.OutcomeController.ElapsedSeconds, Is.EqualTo(2.5f));
            Assert.That(match.OutcomeController.IsMatchOver, Is.False);
        }

        [Test]
        public void FinalSettlementFreezesTimeAndNotifiesOnceAfterOutcomeAndProjectileCancellation()
        {
            MatchFixture match = CreateMatch();
            CombatUnit attacker = CreateUnit("ProjectileAttacker", TeamId.Blue, Vector3.zero, 8f);
            CombatUnit target = CreateUnit("ProjectileTarget", TeamId.Red, Vector3.right, 8f);
            Projectile projectile = CreateGameObject("InFlightProjectile").AddComponent<Projectile>();
            projectile.Initialize(attacker, target, 1f, 1f);
            int resolvedCount = 0;
            MatchOutcome callbackOutcome = MatchOutcome.None;
            bool callbackSawResolved = false;
            bool callbackSawCancelledProjectile = false;
            match.OutcomeController.MatchResolved += () =>
            {
                resolvedCount++;
                callbackOutcome = match.OutcomeController.Outcome;
                callbackSawResolved = match.OutcomeController.IsMatchOver;
                callbackSawCancelledProjectile = projectile.IsFinished;
            };
            match.OutcomeController.Tick(41.25f);

            match.RedTower.TakePhysicalDamage(match.RedTower.MaxHealth);
            match.OutcomeController.Tick(20f);
            match.OutcomeController.Tick(100f);

            Assert.That(match.OutcomeController.ElapsedSeconds, Is.EqualTo(41.25f));
            Assert.That(resolvedCount, Is.EqualTo(1));
            Assert.That(callbackSawResolved, Is.True);
            Assert.That(callbackOutcome, Is.EqualTo(MatchOutcome.BlueVictory));
            Assert.That(callbackSawCancelledProjectile, Is.True);
        }

        [Test]
        public void SettlementStopsPlayerCommandsAndEveryActiveUnitMotorButKeepsEventSystemEnabled()
        {
            MatchFixture match = CreateMatch();
            GameObject player = CreateGameObject("PlayerOperator");
            UnitMotor playerMotor = player.AddComponent<UnitMotor>();
            playerMotor.Configure(5f, ArenaLayout.CreateDefault());
            PlayerCommandController commands = player.AddComponent<PlayerCommandController>();
            InvokePrivate(commands, "Awake");
            var gameplayActions = commands.InputActions.FindActionMap("Gameplay");
            gameplayActions.Enable();
            commands.Issue(UnitCommand.Move(Vector3.right * 10f));
            commands.ArmAttackMove();

            GameObject minion = CreateGameObject("LaneMinion");
            UnitMotor minionMotor = minion.AddComponent<UnitMotor>();
            minionMotor.Configure(5f, ArenaLayout.CreateDefault());
            minionMotor.SetDestination(Vector3.right * 10f);
            EventSystem eventSystem = CreateGameObject("ResultEventSystem").AddComponent<EventSystem>();
            Assert.That(gameplayActions.enabled, Is.True);

            match.RedTower.TakePhysicalDamage(match.RedTower.MaxHealth);

            Assert.That(commands.enabled, Is.False);
            Assert.That(commands.CurrentCommand, Is.Null);
            Assert.That(commands.CurrentTarget, Is.Null);
            Assert.That(commands.IsAttackMoveArmed, Is.False);
            Assert.That(gameplayActions.enabled, Is.False);
            Assert.That(playerMotor.enabled, Is.False);
            Assert.That(playerMotor.IsMoving, Is.False);
            Assert.That(minionMotor.enabled, Is.False);
            Assert.That(minionMotor.IsMoving, Is.False);
            Assert.That(eventSystem.enabled, Is.True);
            Assert.That(commands.IsStoppedForMatch, Is.True);

            commands.Issue(UnitCommand.Move(Vector3.left * 10f));

            Assert.That(commands.CurrentCommand, Is.Null);
            Assert.That(playerMotor.IsMoving, Is.False);
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
        public void TowerDeathStopsRedeployCountdownBeforeItCanSpawnAnotherOperator()
        {
            MatchFixture match = CreateMatch();
            OperatorRosterController roster = CreateGameObject("PlayerRoster")
                .AddComponent<OperatorRosterController>();
            GameObject template = CreateOperatorTemplate("PlayerTemplate");
            int playerSpawnCount = 0;
            roster.PlayerOperatorSpawned += (_, __) => playerSpawnCount++;
            OperatorRosterSlot slot = roster.RegisterSlot(
                "match-freeze-player",
                TeamId.Blue,
                OperatorType.Exusiai,
                template,
                Vector3.zero,
                true);
            roster.StartMatch();
            Assert.That(playerSpawnCount, Is.EqualTo(1));
            Assert.That(roster.NotifySuccessfulRetreat(slot.CurrentOperator), Is.True);
            Assert.That(slot.RedeployRemaining, Is.EqualTo(5.6f).Within(0.0001f));

            match.RedTower.TakePhysicalDamage(match.RedTower.MaxHealth);
            roster.Tick(100f);

            Assert.That(match.OutcomeController.IsEnding, Is.True);
            Assert.That(slot.IsStopped, Is.True, "Settlement must stop every roster slot synchronously.");
            Assert.That(slot.RedeployRemaining, Is.Zero, "The pending countdown must stop at settlement.");
            Assert.That(slot.CurrentOperator, Is.Null, "A settlement frame must not deploy a replacement.");
            Assert.That(playerSpawnCount, Is.EqualTo(1), "No spawn event may occur after tower death.");
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

        [Test]
        public void SettlementStopsExusiaiSkillsSequencesDashAndTimedModifiers()
        {
            MatchFixture match = CreateMatch();
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit exusiai = CreateUnit("Player_Exusiai", TeamId.Blue, new Vector3(-10f, 0f, 0f), 6f);
            exusiai.Configure(TeamId.Blue, Altitude.Ground, 1000f, 50f, 2f, 6f, 0.5f, true, false);
            CombatUnit target = CreateUnit("TimedModifierTarget", TeamId.Red, new Vector3(-8f, 0f, 0f), 0f);
            UnitMotor targetMotor = target.gameObject.AddComponent<UnitMotor>();
            targetMotor.Configure(5f, layout);
            UnitStatModifiers targetModifiers = target.gameObject.AddComponent<UnitStatModifiers>();
            TimedStatModifierController effects = target.gameObject.AddComponent<TimedStatModifierController>();
            effects.ApplyMovementSlow("Test.TimedMovementSlow", 0.70f, 2f);
            CombatUnit chargeTarget = CreateUnit(
                "ChargeLandingTarget", TeamId.Red, exusiai.transform.position + Vector3.right * 4f, 6f);
            UnitMotor chargeTargetMotor = chargeTarget.gameObject.AddComponent<UnitMotor>();
            chargeTargetMotor.Configure(5f, layout);
            UnitMotor motor = exusiai.gameObject.AddComponent<UnitMotor>();
            motor.Configure(5f, layout);
            PlayerCommandController commands = exusiai.gameObject.AddComponent<PlayerCommandController>();
            InvokePrivate(commands, "Awake");
            UnitStatModifiers modifiers = exusiai.gameObject.AddComponent<UnitStatModifiers>();
            AttackSequenceExecutor sequence = exusiai.gameObject.AddComponent<AttackSequenceExecutor>();
            sequence.Configure(exusiai);
            BasicAttackController attacks = exusiai.gameObject.AddComponent<BasicAttackController>();
            attacks.Configure(exusiai, sequence);
            SkillDashController dash = exusiai.gameObject.AddComponent<SkillDashController>();
            dash.Configure(motor, layout, 0);
            ExusiaiSkillController skills = exusiai.gameObject.AddComponent<ExusiaiSkillController>();
            skills.Configure(exusiai, commands, attacks, sequence, modifiers, dash);

            skills.Tick(10f);
            Assert.That(skills.TryActivateOverload(), Is.True);
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            Assert.That(skills.TryConfirmCharge(chargeTarget.transform.position, null), Is.True);
            Assert.That(dash.IsDashing, Is.True);
            Assert.That(sequence.IsRunning, Is.False,
                "E must not start its volley before the dash reaches its confirmed landing point.");
            Assert.That(skills.Snapshot.ChargePhase, Is.EqualTo(ExusiaiChargePhase.Cooldown));
            Assert.That(exusiai.AttackPower, Is.EqualTo(55f).Within(0.001f));
            Assert.That(motor.MovementSpeed, Is.EqualTo(5f * 1.08f).Within(0.001f));
            Assert.That(targetMotor.MovementSpeed, Is.EqualTo(5f * 0.70f).Within(0.001f));
            Assert.That(chargeTargetMotor.MovementSpeed, Is.EqualTo(5f).Within(0.001f),
                "E must not slow its landing target before arrival.");
            float landingTargetHealth = chargeTarget.CurrentHealth;

            match.RedTower.TakePhysicalDamage(match.RedTower.MaxHealth);
            match.OutcomeController.Tick();

            Assert.That(skills.IsOverloadActive, Is.False);
            Assert.That(skills.TryActivateOverload(), Is.False);
            Assert.That(skills.Snapshot.ChargePhase, Is.EqualTo(ExusiaiChargePhase.Cooldown));
            Assert.That(modifiers.ApplyAttackPower(50f), Is.EqualTo(50f).Within(0.001f));
            Assert.That(modifiers.ApplyAttackInterval(0.5f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(motor.MovementSpeed, Is.EqualTo(5f).Within(0.001f));
            Assert.That(modifiers.ApplyMovementSpeed(5f), Is.EqualTo(5f).Within(0.001f));
            Assert.That(targetMotor.MovementSpeed, Is.EqualTo(5f).Within(0.001f));
            Assert.That(targetModifiers.ApplyMovementSpeed(5f), Is.EqualTo(5f).Within(0.001f));
            Assert.That(chargeTargetMotor.MovementSpeed, Is.EqualTo(5f).Within(0.001f));
            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(dash.IsDashing, Is.False);
            Assert.That(match.OutcomeController.IsMatchOver, Is.True);
            dash.Tick(1f);
            sequence.Tick(1f);
            Assert.That(sequence.IsRunning, Is.False,
                "A dash canceled by settlement must not launch its pending E volley afterward.");
            Assert.That(chargeTarget.CurrentHealth, Is.EqualTo(landingTargetHealth),
                "Settlement before arrival must not apply deferred E damage.");
            Assert.That(chargeTargetMotor.MovementSpeed, Is.EqualTo(5f).Within(0.001f),
                "Settlement before arrival must not apply the E movement slow later.");
            effects.ApplyMovementSlow("Test.TimedMovementSlow", 0.70f, 2f);
            effects.Tick(100f);
            Assert.That(targetMotor.MovementSpeed, Is.EqualTo(5f).Within(0.001f));
            Assert.That(targetModifiers.ApplyMovementSpeed(5f), Is.EqualTo(5f).Within(0.001f));
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

        private GameObject CreateOperatorTemplate(string name)
        {
            GameObject template = CreateGameObject(name);
            template.SetActive(false);
            CombatUnit unit = template.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Blue, Altitude.Ground, 1000f, 50f, 2f, 6f, 0.5f, true, true);
            template.AddComponent<OperatorIdentity>();
            Material corpseMaterial = CreateMaterial(Color.blue);
            template.AddComponent<MeshRenderer>().sharedMaterial = corpseMaterial;
            template.AddComponent<DeathCorpsePresenter>().Configure(unit, corpseMaterial, 8);
            return template;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            return gameObject;
        }

        private static void InvokePrivate(MonoBehaviour behaviour, string methodName)
        {
            MethodInfo method = behaviour.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(behaviour, null);
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
