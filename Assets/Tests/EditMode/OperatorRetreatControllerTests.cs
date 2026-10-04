using System.Reflection;
using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class OperatorRetreatControllerTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private readonly List<Material> materials = new List<Material>();

        [TearDown]
        public void TearDown()
        {
            for (int index = gameObjects.Count - 1; index >= 0; index--)
            {
                if (gameObjects[index] != null)
                {
                    Object.DestroyImmediate(gameObjects[index]);
                }
            }

            foreach (Material material in materials)
            {
                if (material != null) Object.DestroyImmediate(material);
            }

            gameObjects.Clear();
            materials.Clear();
        }

        [Test]
        public void BeginCancelsMovementAttackSequenceAndPendingChargeAndBlocksCommands()
        {
            PlayerFixture fixture = CreatePlayerFixture();
            CombatUnit enemy = CreateUnit("retreat-enemy", TeamId.Red);
            fixture.Attacks.SetPlanProvider(() => new AttackSequencePlan(
                AttackSequenceKind.Basic, 5, 0.05f, 20f, 1f, 0f, 1f, 0f, true, false));
            fixture.Attacks.SetTarget(enemy);
            fixture.Attacks.Tick(0f);
            Assert.That(fixture.Executor.IsRunning, Is.True);
            Assert.That(fixture.Skills.BeginChargeTargeting(), Is.True);
            fixture.Commands.Issue(UnitCommand.Move(Vector3.right * 5f));

            Assert.That(fixture.Retreat.TryBegin(), Is.True);

            Assert.That(fixture.Retreat.IsGuiding, Is.True);
            Assert.That(fixture.Retreat.RemainingSeconds, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(fixture.Motor.IsMoving, Is.False);
            Assert.That(fixture.Commands.CurrentCommand, Is.Null);
            Assert.That(fixture.Commands.CurrentTarget, Is.Null);
            Assert.That(fixture.Attacks.CurrentTarget, Is.Null);
            Assert.That(fixture.Executor.IsRunning, Is.False);
            Assert.That(fixture.Skills.IsSelectingChargeTarget, Is.False);

            fixture.Commands.Issue(UnitCommand.Move(Vector3.left * 5f));
            fixture.Commands.Issue(UnitCommand.Attack(enemy.gameObject));
            fixture.Retreat.Tick(0.5f);

            Assert.That(fixture.Commands.CurrentCommand, Is.Null);
            Assert.That(fixture.Motor.IsMoving, Is.False);
            Assert.That(fixture.Attacks.CurrentTarget, Is.Null);
            Assert.That(fixture.Retreat.RemainingSeconds, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CannotBeginAgainWhileGuidingOrDuringDash()
        {
            PlayerFixture fixture = CreatePlayerFixture();

            Assert.That(fixture.Retreat.TryBegin(), Is.True);
            float remaining = fixture.Retreat.RemainingSeconds;
            Assert.That(fixture.Retreat.TryBegin(), Is.False);
            Assert.That(fixture.Retreat.RemainingSeconds, Is.EqualTo(remaining));

            CombatUnit enemyOperator = CreateUnit("retreat-dash-interrupt", TeamId.Red);
            enemyOperator.gameObject.AddComponent<OperatorIdentity>().Configure(
                "retreat-dash-interrupt", TeamId.Red, OperatorType.Exusiai);
            fixture.Unit.TakePhysicalDamage(1f, enemyOperator);
            Assert.That(fixture.Dash.TryStart(Vector3.right * 5f), Is.True);
            Assert.That(fixture.Retreat.TryBegin(), Is.False);
            Assert.That(fixture.Retreat.IsGuiding, Is.False);
        }

        [Test]
        public void ExistingOverloadAndCooldownContinueCountingDuringGuidanceWithoutAttacking()
        {
            PlayerFixture fixture = CreatePlayerFixture();
            fixture.Skills.Tick(10f);
            Assert.That(fixture.Skills.TryActivateOverload(), Is.True);
            CombatUnit enemy = CreateUnit("retreat-r-target", TeamId.Red);
            fixture.Attacks.SetTarget(enemy);

            Assert.That(fixture.Retreat.TryBegin(), Is.True);
            fixture.Skills.Tick(3f);

            Assert.That(fixture.Skills.Snapshot.IsOverloadActive, Is.True);
            Assert.That(fixture.Skills.Snapshot.OverloadDuration, Is.EqualTo(7f).Within(0.0001f));
            Assert.That(fixture.Skills.Snapshot.OverloadCooldown, Is.EqualTo(27f).Within(0.0001f));
            Assert.That(fixture.Attacks.CurrentTarget, Is.Null);
            Assert.That(fixture.Executor.IsRunning, Is.False);
        }

        [TestCase(TeamId.Red, true, false, 10f, false)]
        [TestCase(TeamId.Blue, true, false, 10f, true)]
        [TestCase(TeamId.Red, false, false, 10f, true)]
        [TestCase(TeamId.Red, false, true, 10f, false)]
        [TestCase(TeamId.Red, true, true, 0f, true)]
        [TestCase(TeamId.Red, false, false, 0f, true)]
        [TestCase(TeamId.Red, false, false, 10f, true)]
        public void OnlyPositiveDamageFromAnEnemyOperatorOrTowerInterrupts(
            TeamId attackerTeam, bool isOperator, bool isTower, float damage, bool shouldRemainGuiding)
        {
            PlayerFixture fixture = CreatePlayerFixture();
            GameObject attackerObject = CreateGameObject("retreat-damage-source");
            CombatUnit attacker = attackerObject.AddComponent<CombatUnit>();
            attacker.Configure(attackerTeam, Altitude.Ground, 100f, 0f, 0f, 1f, 1f, true, false);
            if (isOperator)
            {
                attackerObject.AddComponent<OperatorIdentity>().Configure(
                    "retreat-damage-source", attackerTeam, OperatorType.Exusiai);
            }
            if (isTower) attackerObject.AddComponent<TowerCombatController>();

            Assert.That(fixture.Retreat.TryBegin(), Is.True);
            fixture.Unit.TakePhysicalDamage(damage, attacker);

            Assert.That(fixture.Retreat.IsGuiding, Is.EqualTo(shouldRemainGuiding));
            if (!shouldRemainGuiding)
            {
                Assert.That(fixture.Retreat.RemainingSeconds, Is.Zero);
            }
        }

        [Test]
        public void FatalEnemyDamageTakesDeathPathInsteadOfSuccessfulRetreat()
        {
            PlayerFixture fixture = CreatePlayerFixture();
            CombatUnit enemy = CreateUnit("retreat-lethal-enemy", TeamId.Red);
            int completed = 0;
            fixture.Retreat.GuidanceCompleted += _ => completed++;
            Assert.That(fixture.Retreat.TryBegin(), Is.True);

            fixture.Unit.TakePhysicalDamage(fixture.Unit.CurrentHealth, enemy);
            fixture.Retreat.Tick(2f);

            Assert.That(fixture.Unit.IsDead, Is.True);
            Assert.That(fixture.Retreat.IsGuiding, Is.False);
            Assert.That(completed, Is.Zero);
        }

        [Test]
        public void MatchSettlementImmediatelyCancelsGuidanceWithoutDeparture()
        {
            PlayerFixture fixture = CreatePlayerFixture();
            GameObject matchObject = CreateGameObject("retreat-match-outcome");
            MatchOutcomeController match = matchObject.AddComponent<MatchOutcomeController>();
            CombatUnit blueTower = CreateUnit("retreat-blue-tower", TeamId.Blue);
            CombatUnit redTower = CreateUnit("retreat-red-tower", TeamId.Red);
            MinionWaveSpawner spawner = CreateGameObject("retreat-spawner").AddComponent<MinionWaveSpawner>();
            match.Configure(blueTower, redTower, spawner);
            fixture.Retreat.Configure(match);
            int completed = 0;
            int interrupted = 0;
            fixture.Retreat.GuidanceCompleted += _ => completed++;
            fixture.Retreat.GuidanceInterrupted += _ => interrupted++;
            Assert.That(fixture.Retreat.TryBegin(), Is.True);

            redTower.TakePhysicalDamage(redTower.MaxHealth);
            Assert.That(fixture.Retreat.IsGuiding, Is.False);
            fixture.Retreat.Tick(2f);

            Assert.That(match.IsEnding, Is.True);
            Assert.That(fixture.Retreat.IsGuiding, Is.False);
            Assert.That(fixture.Retreat.RemainingSeconds, Is.Zero);
            Assert.That(completed, Is.Zero);
            Assert.That(interrupted, Is.EqualTo(1));
        }

        [Test]
        public void DamageWithoutAnAttackerDoesNotInterrupt()
        {
            PlayerFixture fixture = CreatePlayerFixture();
            Assert.That(fixture.Retreat.TryBegin(), Is.True);

            fixture.Unit.TakePhysicalDamage(10f);

            Assert.That(fixture.Retreat.IsGuiding, Is.True);
        }

        [Test]
        public void GuidanceCompletesOnceAfterOneAndAHalfSeconds()
        {
            PlayerFixture fixture = CreatePlayerFixture();
            int completed = 0;
            int interrupted = 0;
            fixture.Retreat.GuidanceCompleted += _ => completed++;
            fixture.Retreat.GuidanceInterrupted += _ => interrupted++;
            Assert.That(fixture.Retreat.TryBegin(), Is.True);

            fixture.Retreat.Tick(1.49f);
            Assert.That(fixture.Retreat.IsGuiding, Is.True);
            Assert.That(completed, Is.Zero);
            fixture.Retreat.Tick(0.01f);
            Assert.That(fixture.Retreat.IsGuiding, Is.False);
            fixture.Retreat.Tick(0.5f);

            Assert.That(fixture.Retreat.IsGuiding, Is.False);
            Assert.That(fixture.Retreat.RemainingSeconds, Is.Zero);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(interrupted, Is.Zero);
            Assert.That(fixture.Unit.IsDead, Is.False);
            Assert.That(Object.FindObjectsByType<DeathCorpsePresenter>(FindObjectsSortMode.None), Is.Empty);
        }

        [Test]
        public void SuccessfulCompletionNotifiesRosterAndUsesRetreatRedeployDelayWithoutCorpse()
        {
            OperatorRosterController roster = CreateGameObject("retreat-roster")
                .AddComponent<OperatorRosterController>();
            GameObject template = CreateOperatorTemplate();
            OperatorRosterSlot slot = roster.RegisterSlot(
                "retreat-roster-slot", TeamId.Blue, OperatorType.Exusiai, template, Vector3.zero, true);
            roster.StartMatch();
            CombatUnit live = slot.CurrentOperator;
            gameObjects.Add(live.gameObject);
            OperatorRetreatController retreat = live.GetComponent<OperatorRetreatController>();
            Assert.That(retreat, Is.Not.Null);
            retreat.Configure();

            Assert.That(retreat.TryBegin(), Is.True);
            retreat.Tick(1.5f);

            Assert.That(slot.CurrentOperator, Is.Null);
            Assert.That(slot.DepartureCount, Is.EqualTo(1));
            Assert.That(slot.RedeployRemaining, Is.EqualTo(5.6f).Within(0.0001f));
            Assert.That(Object.FindObjectsByType<DeathCorpsePresenter>(FindObjectsSortMode.None), Is.Empty);
        }

        private PlayerFixture CreatePlayerFixture()
        {
            GameObject player = CreateGameObject("retreat-player");
            UnitMotor motor = player.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());
            CombatUnit unit = player.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Blue, Altitude.Ground, 100f, 20f, 0f, 6f, 0.5f, true, true);
            UnitStatModifiers modifiers = player.AddComponent<UnitStatModifiers>();
            PlayerCommandController commands = player.AddComponent<PlayerCommandController>();
            InvokePrivate(commands, "Awake");
            AttackSequenceExecutor executor = player.AddComponent<AttackSequenceExecutor>();
            executor.Configure(unit);
            BasicAttackController attacks = player.AddComponent<BasicAttackController>();
            attacks.Configure(unit, executor);
            SkillDashController dash = player.AddComponent<SkillDashController>();
            dash.Configure(motor, ArenaLayout.CreateDefault(), 0);
            ExusiaiSkillController skills = player.AddComponent<ExusiaiSkillController>();
            skills.Configure(unit, commands, attacks, executor, modifiers, dash);
            OperatorRetreatController retreat = player.GetComponent<OperatorRetreatController>();
            if (retreat == null) retreat = player.AddComponent<OperatorRetreatController>();
            retreat.Configure();
            return new PlayerFixture(unit, motor, commands, executor, attacks, dash, skills, retreat);
        }

        private CombatUnit CreateUnit(string name, TeamId team)
        {
            GameObject gameObject = CreateGameObject(name);
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, 100f, 20f, 0f, 100f, 1f, true, true);
            return unit;
        }

        private GameObject CreateOperatorTemplate()
        {
            GameObject template = CreateGameObject("retreat-roster-template");
            CombatUnit unit = template.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Blue, Altitude.Ground, 100f, 20f, 0f, 6f, 0.5f, true, true);
            template.AddComponent<OperatorIdentity>();
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            materials.Add(material);
            template.AddComponent<MeshRenderer>().sharedMaterial = material;
            template.AddComponent<DeathCorpsePresenter>().Configure(unit, material, -1);
            template.AddComponent<UnitMotor>();
            template.AddComponent<OperatorRetreatController>();
            template.SetActive(false);
            return template;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            return gameObject;
        }

        private static void InvokePrivate(object instance, string methodName)
        {
            instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(instance, null);
        }

        private sealed class PlayerFixture
        {
            public PlayerFixture(
                CombatUnit unit,
                UnitMotor motor,
                PlayerCommandController commands,
                AttackSequenceExecutor executor,
                BasicAttackController attacks,
                SkillDashController dash,
                ExusiaiSkillController skills,
                OperatorRetreatController retreat)
            {
                Unit = unit;
                Motor = motor;
                Commands = commands;
                Executor = executor;
                Attacks = attacks;
                Dash = dash;
                Skills = skills;
                Retreat = retreat;
            }

            public CombatUnit Unit { get; }
            public UnitMotor Motor { get; }
            public PlayerCommandController Commands { get; }
            public AttackSequenceExecutor Executor { get; }
            public BasicAttackController Attacks { get; }
            public SkillDashController Dash { get; }
            public ExusiaiSkillController Skills { get; }
            public OperatorRetreatController Retreat { get; }
        }
    }
}
