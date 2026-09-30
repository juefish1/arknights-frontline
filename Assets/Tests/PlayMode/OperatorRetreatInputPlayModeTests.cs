using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class OperatorRetreatInputPlayModeTests : InputTestFixture
    {
        private const int GroundLayer = 8;
        private const string InputBindingsPreferenceKey = "af.input.bindings.v1";

        private readonly List<GameObject> ownedObjects = new List<GameObject>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly HashSet<EntityId> baselineProjectileIds = new HashSet<EntityId>();
        private Keyboard keyboard;
        private Mouse mouse;
        private float previousTimeScale;
        private bool capturedInputBindingsPreference;
        private bool hadInputBindingsPreference;
        private string originalInputBindingsPreference;

        public override void Setup()
        {
            base.Setup();
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            hadInputBindingsPreference = PlayerPrefs.HasKey(InputBindingsPreferenceKey);
            originalInputBindingsPreference = hadInputBindingsPreference
                ? PlayerPrefs.GetString(InputBindingsPreferenceKey, string.Empty)
                : null;
            capturedInputBindingsPreference = true;
            TestContext.Progress.WriteLine(
                $"Input-test initial PlayerPrefs HasKey({InputBindingsPreferenceKey})={hadInputBindingsPreference}");
            PlayerPrefs.DeleteKey(InputBindingsPreferenceKey);
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            baselineProjectileIds.Clear();
            foreach (Projectile projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                baselineProjectileIds.Add(projectile.gameObject.GetEntityId());
            }
        }

        public override void TearDown()
        {
        }

        [UnityTearDown]
        public IEnumerator TearDownRuntimeObjects()
        {
            try
            {
                foreach (InputDevice device in InputSystem.devices)
                {
                    InputSystem.ResetDevice(device, true);
                }

                foreach (Projectile projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                {
                    if (projectile != null && !baselineProjectileIds.Contains(projectile.gameObject.GetEntityId()))
                    {
                        Object.Destroy(projectile.gameObject);
                    }
                }

                for (int index = ownedObjects.Count - 1; index >= 0; index--)
                {
                    if (ownedObjects[index] != null)
                    {
                        Object.Destroy(ownedObjects[index]);
                    }
                }

                foreach (Material material in ownedMaterials)
                {
                    if (material != null)
                    {
                        Object.Destroy(material);
                    }
                }

                yield return null;
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                if (capturedInputBindingsPreference)
                {
                    if (hadInputBindingsPreference)
                    {
                        PlayerPrefs.SetString(InputBindingsPreferenceKey, originalInputBindingsPreference ?? string.Empty);
                    }
                    else
                    {
                        PlayerPrefs.DeleteKey(InputBindingsPreferenceKey);
                    }

                    PlayerPrefs.Save();
                }

                ownedObjects.Clear();
                ownedMaterials.Clear();
                baselineProjectileIds.Clear();
                capturedInputBindingsPreference = false;
                originalInputBindingsPreference = null;
                base.TearDown();
            }
        }

        [UnityTest]
        public IEnumerator RealBStartsGuidanceAndIgnoresRetreatCombatAndPointerInputs()
        {
            PlayerFixture fixture = CreatePlayerFixture("retreat-input-player");
            CombatUnit enemy = CreateUnit("retreat-input-enemy", TeamId.Red, Vector3.right * 3f, true, false);
            fixture.Commands.Issue(UnitCommand.Move(Vector3.right * 8f));
            fixture.Attacks.SetPlanProvider(() => new AttackSequencePlan(
                AttackSequenceKind.Basic, 5, 10f, fixture.Owner.AttackPower, 1f, 0f, 1f, 0f, true, false));
            fixture.Attacks.SetTarget(enemy);
            fixture.Attacks.Tick(0f);
            CaptureNewProjectiles();
            Assert.That(fixture.Motor.IsMoving, Is.True);
            Assert.That(fixture.Sequence.IsRunning, Is.True,
                "The operator should have an in-range active basic attack sequence before retreat input.");

            fixture.Skills.Tick(10f);
            Assert.That(fixture.Skills.Snapshot.OverloadCooldown, Is.Zero,
                "R must be ready before testing that retreat guidance blocks it.");
            Assert.That(fixture.Skills.IsOverloadActive, Is.False);

            Press(keyboard.aKey);
            yield return null;
            Assert.That(fixture.Commands.IsAttackMoveArmed, Is.True);
            Assert.That(fixture.Commands.IsAttackMoveHeld, Is.True);

            Press(keyboard.eKey);
            yield return null;
            Release(keyboard.eKey);
            yield return null;
            Assert.That(fixture.Skills.IsSelectingChargeTarget, Is.True,
                "E should be available and actively selecting before B cancels its pending target.");

            int startedCount = 0;
            fixture.Retreat.GuidanceStarted += _ => startedCount++;
            Press(keyboard.bKey);
            yield return null;

            Assert.That(fixture.Retreat.IsGuiding, Is.True);
            Assert.That(startedCount, Is.EqualTo(1));
            Assert.That(fixture.Retreat.RemainingSeconds, Is.LessThanOrEqualTo(1.5f).And.GreaterThan(0f));
            Assert.That(fixture.Motor.IsMoving, Is.False);
            Assert.That(fixture.Commands.CurrentCommand, Is.Null);
            Assert.That(fixture.Commands.CurrentTarget, Is.Null);
            Assert.That(fixture.Commands.IsAttackMoveArmed, Is.False);
            Assert.That(fixture.Commands.IsAttackMoveHeld, Is.False);
            Assert.That(fixture.Attacks.CurrentTarget, Is.Null);
            Assert.That(fixture.Sequence.IsRunning, Is.False);
            Assert.That(fixture.Skills.IsSelectingChargeTarget, Is.False);
            Assert.That(fixture.Skills.IsOverloadActive, Is.False);

            SkillInputSpy skillInputSpy = new SkillInputSpy();
            fixture.Commands.SetSkillInputHandler(skillInputSpy);
            Release(keyboard.bKey);
            Release(keyboard.aKey);
            yield return null;
            int revisionAfterRetreat = fixture.Commands.CommandRevision;
            float remainingBeforeRetry = fixture.Retreat.RemainingSeconds;

            Press(keyboard.bKey);
            Press(keyboard.eKey);
            Press(keyboard.rKey);
            // W/Skill1 currently has no PlayerCommandController consumer; verify other state stays unchanged.
            Press(keyboard.wKey);
            Press(keyboard.aKey);
            Press(keyboard.sKey);
            Press(keyboard.escapeKey);
            Press(mouse.leftButton);
            Press(mouse.rightButton);
            fixture.Commands.Issue(UnitCommand.Move(Vector3.left * 8f));
            fixture.Commands.Issue(UnitCommand.Attack(enemy.gameObject));
            yield return null;

            Assert.That(fixture.Retreat.IsGuiding, Is.True);
            Assert.That(startedCount, Is.EqualTo(1), "A second B press cannot restart the timer.");
            Assert.That(fixture.Retreat.RemainingSeconds, Is.LessThan(remainingBeforeRetry));
            Assert.That(fixture.Commands.IsAttackMoveArmed, Is.False);
            Assert.That(fixture.Commands.IsAttackMoveHeld, Is.False);
            Assert.That(fixture.Commands.CommandRevision, Is.EqualTo(revisionAfterRetreat));
            Assert.That(fixture.Commands.CurrentCommand, Is.Null);
            Assert.That(fixture.Commands.CurrentTarget, Is.Null);
            Assert.That(fixture.Motor.IsMoving, Is.False);
            Assert.That(fixture.Attacks.CurrentTarget, Is.Null);
            Assert.That(fixture.Skills.IsSelectingChargeTarget, Is.False);
            Assert.That(fixture.Skills.IsOverloadActive, Is.False,
                "R was ready but its real input must not activate while retreat guidance is active.");
            Assert.That(skillInputSpy.CancelCalls, Is.Zero,
                "Esc and right-click cancel should not be forwarded to the skill handler during guidance.");
            Assert.That(skillInputSpy.ConfirmCalls, Is.Zero,
                "Left-click confirm should not be forwarded to the skill handler during guidance.");
            Assert.That(skillInputSpy.MoveClickCalls, Is.Zero,
                "Right-click movement should not be forwarded to the skill handler during guidance.");
            Assert.That(skillInputSpy.Skill2Calls, Is.Zero);
            Assert.That(skillInputSpy.Skill3Calls, Is.Zero);
            Assert.That(skillInputSpy.StopCalls, Is.Zero);
            fixture.Commands.SetSkillInputHandler(fixture.Skills);

            Release(keyboard.bKey);
            Release(keyboard.eKey);
            Release(keyboard.rKey);
            Release(keyboard.wKey);
            Release(keyboard.aKey);
            Release(keyboard.sKey);
            Release(keyboard.escapeKey);
            Release(mouse.leftButton);
            Release(mouse.rightButton);
            yield return null;

            fixture.Retreat.Tick(1.5f);
            Assert.That(fixture.Retreat.IsGuiding, Is.False);
            Assert.That(fixture.Owner.IsDead, Is.False);
            Assert.That(FindOperatorCorpses("retreat-input-player"), Is.Empty);
        }

        [UnityTest]
        public IEnumerator PositiveEnemyOperatorDamageInterruptsGuidanceStartedByRealB()
        {
            PlayerFixture fixture = CreatePlayerFixture("retreat-damage-player");
            CombatUnit attacker = CreateUnit("retreat-enemy-operator", TeamId.Red, Vector3.right * 2f, true, false);
            yield return BeginGuidanceWithRealB(fixture);

            fixture.Owner.TakePhysicalDamage(1f, attacker);

            Assert.That(fixture.Retreat.IsGuiding, Is.False);
            Assert.That(fixture.Retreat.RemainingSeconds, Is.Zero);
            Assert.That(fixture.Owner.IsDead, Is.False);
            Assert.That(FindOperatorCorpses("retreat-damage-player"), Is.Empty);
        }

        [UnityTest]
        public IEnumerator PositiveTowerDamageInterruptsGuidanceStartedByRealB()
        {
            PlayerFixture fixture = CreatePlayerFixture("retreat-tower-damage-player");
            CombatUnit tower = CreateUnit("retreat-enemy-tower", TeamId.Red, Vector3.right * 2f, false, true);
            yield return BeginGuidanceWithRealB(fixture);

            fixture.Owner.TakePhysicalDamage(1f, tower);

            Assert.That(fixture.Retreat.IsGuiding, Is.False);
            Assert.That(fixture.Retreat.RemainingSeconds, Is.Zero);
            Assert.That(fixture.Owner.IsDead, Is.False);
            Assert.That(FindOperatorCorpses("retreat-tower-damage-player"), Is.Empty);
        }

        [UnityTest]
        public IEnumerator NoSourceAndMinionDamageDoNotInterruptGuidanceStartedByRealB()
        {
            PlayerFixture fixture = CreatePlayerFixture("retreat-minion-damage-player");
            CombatUnit minion = CreateUnit("retreat-enemy-minion", TeamId.Red, Vector3.right * 2f, false, false);
            yield return BeginGuidanceWithRealB(fixture);

            fixture.Owner.TakePhysicalDamage(1f);
            Assert.That(fixture.Retreat.IsGuiding, Is.True,
                "Damage with no source must not interrupt a valid retreat.");
            fixture.Owner.TakePhysicalDamage(1f, minion);

            Assert.That(fixture.Retreat.IsGuiding, Is.True,
                "Positive damage from a minion must not interrupt a valid retreat.");
            Assert.That(fixture.Retreat.RemainingSeconds, Is.GreaterThan(0f));
            Assert.That(fixture.Owner.IsDead, Is.False);
            Assert.That(FindOperatorCorpses("retreat-minion-damage-player"), Is.Empty);
        }

        [UnityTest]
        public IEnumerator RealBCompletesRetreatAndRedeploysSlotAfterReducedWaitWithoutCorpse()
        {
            const string stableKey = "retreat-playmode-lifecycle-exusiai";
            OperatorRosterController roster = CreateRoster(stableKey, out OperatorRosterSlot slot);
            CombatUnit oldLife = slot.CurrentOperator;
            OperatorRetreatController retreat = oldLife.GetComponent<OperatorRetreatController>();
            Assert.That(retreat, Is.Not.Null);

            bool completionObserved = false;
            bool oldLifeExistsDuringCompletion = false;
            bool oldLifeInactiveDuringCompletion = false;
            float redeployWaitAtCompletion = -1f;
            retreat.GuidanceCompleted += _ =>
            {
                completionObserved = true;
                oldLifeExistsDuringCompletion = oldLife != null;
                oldLifeInactiveDuringCompletion = oldLife != null && !oldLife.gameObject.activeInHierarchy;
                redeployWaitAtCompletion = slot.RedeployRemaining;
            };

            yield return BeginGuidanceWithRealB(retreat);
            yield return new WaitForSeconds(1.6f);

            Assert.That(completionObserved, Is.True, "Real frames should complete the 1.5-second guidance.");
            Assert.That(oldLifeExistsDuringCompletion, Is.True,
                "The old live operator must exist in the callback frame while Destroy is deferred.");
            Assert.That(oldLifeInactiveDuringCompletion, Is.True,
                "The roster should deactivate the old life immediately before deferred Destroy.");
            Assert.That(redeployWaitAtCompletion, Is.EqualTo(5.6f).Within(0.1f));
            Assert.That(slot.CurrentOperator, Is.Null);
            Assert.That(slot.DepartureCount, Is.EqualTo(1));
            Assert.That(slot.RedeployRemaining, Is.GreaterThan(5.4f));
            Assert.That(FindOperatorCorpses(stableKey), Is.Empty);

            yield return null;
            Assert.That(oldLife == null, Is.True, "The old live object should be destroyed after its callback frame.");

            yield return new WaitForSeconds(5.8f);

            CombatUnit replacement = slot.CurrentOperator;
            Assert.That(replacement, Is.Not.Null, "The slot should redeploy after its reduced 5.6-second wait.");
            Assert.That(replacement, Is.Not.SameAs(oldLife));
            Assert.That(slot.DepartureCount, Is.EqualTo(1));
            Assert.That(FindOperatorCorpses(stableKey), Is.Empty,
                "A successful retreat removes the operator without creating a death corpse.");
            Assert.That(roster.Slots, Has.Count.EqualTo(1));
        }

        private PlayerFixture CreatePlayerFixture(string name)
        {
            GameObject player = CreateGameObject(name);
            UnitMotor motor = player.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());
            CombatUnit owner = player.AddComponent<CombatUnit>();
            owner.Configure(TeamId.Blue, Altitude.Ground, 100f, 20f, 0f, 6f, 0.5f, true, true);
            UnitStatModifiers modifiers = player.AddComponent<UnitStatModifiers>();
            AttackSequenceExecutor sequence = player.AddComponent<AttackSequenceExecutor>();
            sequence.Configure(owner);
            BasicAttackController attacks = player.AddComponent<BasicAttackController>();
            attacks.Configure(owner, sequence);
            SkillDashController dash = player.AddComponent<SkillDashController>();
            dash.Configure(motor, ArenaLayout.CreateDefault(), 0);
            PlayerCommandController commands = player.AddComponent<PlayerCommandController>();
            ExusiaiSkillController skills = player.AddComponent<ExusiaiSkillController>();
            skills.Configure(owner, commands, attacks, sequence, modifiers, dash);
            return new PlayerFixture(
                owner,
                motor,
                commands,
                attacks,
                sequence,
                skills,
                dash,
                player.GetComponent<OperatorRetreatController>());
        }

        private OperatorRosterController CreateRoster(string stableKey, out OperatorRosterSlot slot)
        {
            OperatorRosterController roster = CreateGameObject("retreat-lifecycle-roster")
                .AddComponent<OperatorRosterController>();
            GameObject template = CreateOperatorTemplate();
            roster.OperatorSpawned += (_, liveOperator) => ownedObjects.Add(liveOperator.gameObject);
            slot = roster.RegisterSlot(
                stableKey,
                TeamId.Blue,
                OperatorType.Exusiai,
                template,
                Vector3.zero,
                true);
            roster.StartMatch();
            return roster;
        }

        private GameObject CreateOperatorTemplate()
        {
            GameObject template = CreateGameObject("retreat-lifecycle-template");
            template.SetActive(false);
            CombatUnit unit = template.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Blue, Altitude.Ground, 100f, 20f, 0f, 6f, 0.5f, true, true);
            template.AddComponent<OperatorIdentity>();
            UnitMotor motor = template.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());
            template.AddComponent<UnitStatModifiers>();
            AttackSequenceExecutor sequence = template.AddComponent<AttackSequenceExecutor>();
            sequence.Configure(unit);
            BasicAttackController attacks = template.AddComponent<BasicAttackController>();
            attacks.Configure(unit, sequence);
            SkillDashController dash = template.AddComponent<SkillDashController>();
            dash.Configure(motor, ArenaLayout.CreateDefault(), 0);
            template.AddComponent<OperatorRetreatController>();
            template.AddComponent<PlayerCommandController>();
            template.AddComponent<ExusiaiSkillController>();

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material corpseMaterial = new Material(shader);
            ownedMaterials.Add(corpseMaterial);
            template.AddComponent<MeshRenderer>().sharedMaterial = corpseMaterial;
            template.AddComponent<DeathCorpsePresenter>().Configure(unit, corpseMaterial, GroundLayer);
            return template;
        }

        private CombatUnit CreateUnit(string name, TeamId team, Vector3 position, bool isOperator, bool isTower)
        {
            GameObject source = CreateGameObject(name);
            source.transform.position = position;
            CombatUnit combatUnit = source.AddComponent<CombatUnit>();
            combatUnit.Configure(team, Altitude.Ground, 100f, 0f, 0f, 6f, 1f, true, true);
            if (isOperator)
            {
                source.AddComponent<OperatorIdentity>().Configure(name, team, OperatorType.Exusiai);
            }
            if (isTower)
            {
                source.AddComponent<TowerCombatController>();
            }

            return combatUnit;
        }

        private IEnumerator BeginGuidanceWithRealB(PlayerFixture fixture)
        {
            yield return BeginGuidanceWithRealB(fixture.Retreat);
        }

        private IEnumerator BeginGuidanceWithRealB(OperatorRetreatController retreat)
        {
            Press(keyboard.bKey);
            yield return null;
            Assert.That(retreat.IsGuiding, Is.True, "The guidance must begin from simulated physical B input.");
            Release(keyboard.bKey);
            yield return null;
        }

        private void CaptureNewProjectiles()
        {
            foreach (Projectile projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                if (projectile != null && !baselineProjectileIds.Contains(projectile.gameObject.GetEntityId()))
                {
                    ownedObjects.Add(projectile.gameObject);
                }
            }
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            ownedObjects.Add(gameObject);
            return gameObject;
        }

        private static GameObject[] FindOperatorCorpses(string stableKey)
        {
            return Object.FindObjectsByType<CorpseLifetimeController>(FindObjectsSortMode.None)
                .Where(corpse => corpse.UnitKind == UnitKind.Operator && corpse.OwnerKey == stableKey)
                .Select(corpse => corpse.gameObject)
                .ToArray();
        }

        private sealed class PlayerFixture
        {
            public PlayerFixture(
                CombatUnit owner,
                UnitMotor motor,
                PlayerCommandController commands,
                BasicAttackController attacks,
                AttackSequenceExecutor sequence,
                ExusiaiSkillController skills,
                SkillDashController dash,
                OperatorRetreatController retreat)
            {
                Owner = owner;
                Motor = motor;
                Commands = commands;
                Attacks = attacks;
                Sequence = sequence;
                Skills = skills;
                Dash = dash;
                Retreat = retreat;
            }

            public CombatUnit Owner { get; }
            public UnitMotor Motor { get; }
            public PlayerCommandController Commands { get; }
            public BasicAttackController Attacks { get; }
            public AttackSequenceExecutor Sequence { get; }
            public ExusiaiSkillController Skills { get; }
            public SkillDashController Dash { get; }
            public OperatorRetreatController Retreat { get; }
        }

        private sealed class SkillInputSpy : IPlayerSkillInputHandler
        {
            public int CancelCalls { get; private set; }
            public int ConfirmCalls { get; private set; }
            public int MoveClickCalls { get; private set; }
            public int Skill2Calls { get; private set; }
            public int Skill3Calls { get; private set; }
            public int StopCalls { get; private set; }
            public bool BlocksAttackMove => false;
            public bool BlocksNormalCommands => false;

            public void HandleSkill2() => Skill2Calls++;

            public void HandleSkill3() => Skill3Calls++;

            public bool TryHandleConfirm(Vector3 worldPoint, GameObject hitObject)
            {
                ConfirmCalls++;
                return true;
            }

            public bool TryHandleMoveClick(Vector3 worldPoint)
            {
                MoveClickCalls++;
                return true;
            }

            public bool TryHandleCancel()
            {
                CancelCalls++;
                return true;
            }

            public void HandleStop() => StopCalls++;
        }
    }
}
