using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class ExusiaiSkillControllerTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private GameObject ownerObject;
        private CombatUnit owner;
        private UnitMotor motor;
        private PlayerCommandController commands;
        private UnitStatModifiers modifiers;
        private AttackSequenceExecutor executor;
        private BasicAttackController attacks;
        private SkillDashController dash;
        private ExusiaiSkillController controller;
        private CombatUnit target;

        [SetUp]
        public void SetUp()
        {
            CreateFixture(
                "Exusiai",
                out ownerObject,
                out owner,
                out motor,
                out commands,
                out modifiers,
                out executor,
                out attacks,
                out dash,
                out controller);
            target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in gameObjects)
            {
                if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
            }

            gameObjects.Clear();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DeploymentStartsWAtZeroEReadyAndRAtTenSeconds()
        {
            controller.ResetForDeployment();

            Assert.That(controller.Snapshot.SweepProgress, Is.Zero);
            Assert.That(controller.Snapshot.IsSweepReady, Is.False);
            Assert.That(controller.Snapshot.IsChargeReady, Is.True);
            Assert.That(controller.Snapshot.ChargePhase, Is.EqualTo(ExusiaiChargePhase.Ready));
            Assert.That(controller.Snapshot.ChargeCooldown, Is.Zero);
            Assert.That(controller.Snapshot.IsOverloadActive, Is.False);
            Assert.That(controller.Snapshot.OverloadDuration, Is.Zero);
            Assert.That(controller.Snapshot.OverloadCooldown, Is.EqualTo(10f));
            Assert.That(controller.BlocksNormalCommands, Is.False);
        }

        [Test]
        public void SnapshotIsAnImmutableValueWithReadOnlyProperties()
        {
            Assert.That(typeof(ExusiaiSkillSnapshot).IsValueType, Is.True);
            foreach (PropertyInfo property in typeof(ExusiaiSkillSnapshot).GetProperties())
            {
                Assert.That(property.CanWrite, Is.False, property.Name);
            }
        }

        [Test]
        public void FourthBasicAttackConsumesReadyWAndCreatesThreeShotPlan()
        {
            CompleteBasicAttack();
            CompleteBasicAttack();
            CompleteBasicAttack();

            AttackSequencePlan plan = StartBasicAttackAndCapturePlan(false);

            AssertPlan(plan, AttackSequenceKind.Sweep, 3, 50f, 1.45f);
            Assert.That(plan.LastShotMissingHealthRatio, Is.EqualTo(0.08f));
            Assert.That(controller.Snapshot.SweepProgress, Is.Zero);
        }

        [Test]
        public void InterruptedBasicSequenceDoesNotAddWProgress()
        {
            controller.Tick(10f);
            Assert.That(controller.TryActivateOverload(), Is.True);

            AttackSequencePlan plan = StartBasicAttackAndCapturePlan(false);

            Assert.That(plan.Kind, Is.EqualTo(AttackSequenceKind.Overload));
            Assert.That(controller.Snapshot.SweepProgress, Is.Zero);
        }

        [Test]
        public void ConsumedWIsNotRefundedWhenItsSequenceIsInterrupted()
        {
            ReadySweep();

            AttackSequencePlan plan = StartBasicAttackAndCapturePlan(false);

            Assert.That(plan.Kind, Is.EqualTo(AttackSequenceKind.Sweep));
            Assert.That(controller.Snapshot.SweepProgress, Is.Zero);
            Assert.That(controller.Snapshot.IsSweepReady, Is.False);
        }

        [Test]
        public void CompletedWAttackBeginsNextCycleAtOneOfThree()
        {
            ReadySweep();

            AttackSequencePlan plan = StartBasicAttackAndCapturePlan(true);

            Assert.That(plan.Kind, Is.EqualTo(AttackSequenceKind.Sweep));
            Assert.That(controller.Snapshot.SweepProgress, Is.EqualTo(1));
        }

        [Test]
        public void ChargePlanNeverChangesWProgress()
        {
            CompleteBasicAttack();
            Assert.That(controller.BeginChargeTargeting(), Is.True);
            Assert.That(controller.TryConfirmCharge(target.transform.position, target), Is.True);

            executor.Tick(1f);

            Assert.That(controller.Snapshot.SweepProgress, Is.EqualTo(1));
        }

        [Test]
        public void OverloadHonorsInitialWaitDurationCooldownAndApprovedModifiers()
        {
            controller.Tick(9.999f);
            Assert.That(controller.TryActivateOverload(), Is.False);
            controller.Tick(0.001f);

            Assert.That(controller.TryActivateOverload(), Is.True);
            Assert.That(controller.Snapshot.OverloadDuration, Is.EqualTo(10f));
            Assert.That(controller.Snapshot.OverloadCooldown, Is.EqualTo(30f));
            Assert.That(owner.AttackPower, Is.EqualTo(55f).Within(0.001f));
            Assert.That(motor.MovementSpeed, Is.EqualTo(5.4f).Within(0.001f));
            Assert.That(owner.AttackInterval, Is.EqualTo(0.28f).Within(0.001f));

            controller.Tick(9.999f);
            Assert.That(controller.IsOverloadActive, Is.True);
            controller.Tick(0.001f);

            Assert.That(controller.IsOverloadActive, Is.False);
            Assert.That(controller.Snapshot.OverloadCooldown, Is.EqualTo(20f).Within(0.001f));
            Assert.That(owner.AttackPower, Is.EqualTo(50f).Within(0.001f));
            Assert.That(motor.MovementSpeed, Is.EqualTo(5f).Within(0.001f));
            Assert.That(owner.AttackInterval, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(controller.TryActivateOverload(), Is.False);

            controller.Tick(20f);
            Assert.That(controller.TryActivateOverload(), Is.True);
        }

        [Test]
        public void OverloadAndOverloadSweepPlansUseSnapshotPanelAttackAndApprovedMultipliers()
        {
            controller.Tick(10f);
            Assert.That(controller.TryActivateOverload(), Is.True);
            AttackSequencePlan normalR = StartBasicAttackAndCapturePlan(false);

            AssertPlan(normalR, AttackSequenceKind.Overload, 5, 55f, 1f);

            controller.ResetForDeployment();
            ReadySweep();
            controller.Tick(10f);
            Assert.That(controller.TryActivateOverload(), Is.True);
            AttackSequencePlan rSweep = StartBasicAttackAndCapturePlan(false);

            AssertPlan(rSweep, AttackSequenceKind.OverloadSweep, 5, 55f, 1.45f);
            Assert.That(rSweep.LastShotMissingHealthRatio, Is.EqualTo(0.08f));
        }

        [Test]
        public void ActivatingOverloadAfterChargeStartsDoesNotRewriteExistingPlan()
        {
            controller.Tick(10f);
            Assert.That(controller.BeginChargeTargeting(), Is.True);
            Assert.That(controller.TryConfirmCharge(target.transform.position, target), Is.True);
            Assert.That(controller.TryActivateOverload(), Is.True);

            AttackSequencePlan plan = CancelRunningAndCapturePlan();

            AssertPlan(plan, AttackSequenceKind.Charge, 4, 50f, 1.25f);
        }

        [Test]
        public void ActivatingOverloadBeforeChargeConfirmationCreatesFiveShotPlanAtFiftyFive()
        {
            controller.Tick(10f);
            Assert.That(controller.BeginChargeTargeting(), Is.True);
            Assert.That(controller.TryActivateOverload(), Is.True);
            Assert.That(controller.TryConfirmCharge(target.transform.position, target), Is.True);

            AttackSequencePlan plan = CancelRunningAndCapturePlan();

            AssertPlan(plan, AttackSequenceKind.Charge, 5, 55f, 1.25f);
        }

        [Test]
        public void BeginChargeTargetingRequiresReadyLivingOwnerAndBlocksNormalCommands()
        {
            Assert.That(controller.BeginChargeTargeting(), Is.True);
            Assert.That(controller.IsSelectingChargeTarget, Is.True);
            Assert.That(controller.BlocksNormalCommands, Is.True);
            Assert.That(controller.BeginChargeTargeting(), Is.False);
            Assert.That(controller.CancelChargeTargeting(), Is.True);
            Assert.That(controller.BlocksNormalCommands, Is.False);

            owner.TakePhysicalDamage(owner.MaxHealth);
            Assert.That(controller.BeginChargeTargeting(), Is.False);
        }

        [Test]
        public void OutOfRangeChargePointDoesNotConsumeCooldownOrLeaveTargeting()
        {
            Assert.That(controller.BeginChargeTargeting(), Is.True);

            Assert.That(controller.TryConfirmCharge(new Vector3(6.001f, 0f, 0f), target), Is.False);

            Assert.That(controller.IsSelectingChargeTarget, Is.True);
            Assert.That(controller.Snapshot.ChargeCooldown, Is.Zero);
            Assert.That(controller.Snapshot.ChargePhase, Is.EqualTo(ExusiaiChargePhase.Targeting));
        }

        [Test]
        public void DirectLegalTargetWinsOverPointNearestTarget()
        {
            CombatUnit pointNearest = CreateUnit("Point Nearest", TeamId.Red, new Vector3(5f, 0f, 0f));
            CombatUnit firstShotTarget = null;
            executor.ShotRequested += (shotTarget, _) => firstShotTarget ??= shotTarget;
            Assert.That(controller.BeginChargeTargeting(), Is.True);

            Assert.That(controller.TryConfirmCharge(pointNearest.transform.position, target), Is.True);

            Assert.That(firstShotTarget, Is.SameAs(target));
            Assert.That(controller.SelectedChargeTarget, Is.SameAs(target));
        }

        [Test]
        public void GroundPointSelectsLegalOwnerRangeTargetNearestThatPoint()
        {
            CombatUnit pointNearest = CreateUnit("Point Nearest", TeamId.Red, new Vector3(5f, 0f, 0f));
            CombatUnit firstShotTarget = null;
            executor.ShotRequested += (shotTarget, _) => firstShotTarget ??= shotTarget;
            Assert.That(controller.BeginChargeTargeting(), Is.True);

            Assert.That(controller.TryConfirmCharge(new Vector3(5.5f, 0f, 0f), null), Is.True);

            Assert.That(firstShotTarget, Is.SameAs(pointNearest));
            Assert.That(controller.SelectedChargeTarget, Is.SameAs(pointNearest));
        }

        [Test]
        public void NoTargetStillConsumesChargeAndOpensWindowWithoutSequence()
        {
            target.Configure(TeamId.Blue, Altitude.Ground, 1000f, 50f, 0f, 6f, 0.5f, true, false);
            Assert.That(controller.BeginChargeTargeting(), Is.True);

            Assert.That(controller.TryConfirmCharge(Vector3.right, null), Is.True);

            Assert.That(controller.SelectedChargeTarget, Is.Null);
            Assert.That(executor.IsRunning, Is.False);
            Assert.That(controller.IsDashWindowOpen, Is.True);
            Assert.That(controller.BlocksNormalCommands, Is.True);
            Assert.That(controller.Snapshot.ChargeCooldown, Is.EqualTo(20f));
            Assert.That(controller.Snapshot.ChargePhase, Is.EqualTo(ExusiaiChargePhase.DashWindow));
        }

        [Test]
        public void TargetingAndCancelPreserveCommandButSuccessfulConfirmationClearsIt()
        {
            commands.Issue(UnitCommand.Move(new Vector3(4f, 0f, 0f)));
            int originalRevision = commands.CommandRevision;
            Assert.That(controller.BeginChargeTargeting(), Is.True);
            Assert.That(commands.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.Move));
            Assert.That(commands.CommandRevision, Is.EqualTo(originalRevision));

            Assert.That(controller.CancelChargeTargeting(), Is.True);
            Assert.That(commands.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.Move));
            Assert.That(commands.CommandRevision, Is.EqualTo(originalRevision));

            Assert.That(controller.BeginChargeTargeting(), Is.True);
            Assert.That(controller.TryConfirmCharge(target.transform.position, target), Is.True);
            Assert.That(commands.CurrentCommand, Is.Null);
            Assert.That(commands.CommandRevision, Is.GreaterThan(originalRevision));
        }

        [Test]
        public void ConfirmationCancelsRunningNormalOwnedSequenceBeforeStartingCharge()
        {
            controller.Tick(10f);
            Assert.That(controller.TryActivateOverload(), Is.True);
            attacks.SetTarget(target);
            attacks.Tick(0f);
            Assert.That(attacks.IsOwnedSequenceRunning, Is.True);
            Assert.That(controller.BeginChargeTargeting(), Is.True);

            Assert.That(controller.TryConfirmCharge(target.transform.position, target), Is.True);

            Assert.That(attacks.IsOwnedSequenceRunning, Is.False);
            AttackSequencePlan charge = CancelRunningAndCapturePlan();
            Assert.That(charge.Kind, Is.EqualTo(AttackSequenceKind.Charge));
        }

        [Test]
        public void DashWindowAcceptsPointZeroTwoFourNineButRejectsPointTwoFive()
        {
            OpenDashWindow();
            controller.Tick(0.249f);
            Assert.That(controller.TryConsumeDashMove(new Vector3(4f, 0f, 0f)), Is.True);
            Assert.That(dash.IsDashing, Is.True);

            controller.ResetForDeployment();
            OpenDashWindow();
            controller.Tick(0.25f);
            Assert.That(controller.TryConsumeDashMove(new Vector3(4f, 0f, 0f)), Is.False);
            Assert.That(dash.IsDashing, Is.False);
            Assert.That(controller.IsDashWindowOpen, Is.False);
        }

        [Test]
        public void FirstWindowClickClosesWindowAndLaterClicksAreNotAccepted()
        {
            OpenDashWindow();

            Assert.That(controller.TryConsumeDashMove(new Vector3(4f, 0f, 0f)), Is.True);
            Vector3 destination = dash.Destination;
            Assert.That(controller.IsDashWindowOpen, Is.False);
            Assert.That(controller.TryConsumeDashMove(new Vector3(7f, 0f, 0f)), Is.False);
            Assert.That(dash.Destination, Is.EqualTo(destination));
        }

        [Test]
        public void InvalidFirstWindowClickReturnsFalseButStillConsumesWindow()
        {
            OpenDashWindow();

            Assert.That(controller.TryConsumeDashMove(owner.transform.position), Is.False);

            Assert.That(controller.IsDashWindowOpen, Is.False);
            Assert.That(dash.IsDashing, Is.False);
            Assert.That(controller.TryConsumeDashMove(new Vector3(4f, 0f, 0f)), Is.False);
        }

        [Test]
        public void WindowExpiresWithoutClickAndDoesNotMoveOwner()
        {
            OpenDashWindow();
            Vector3 initialPosition = owner.transform.position;

            controller.Tick(0.25f);
            dash.Tick(1f);

            Assert.That(controller.IsDashWindowOpen, Is.False);
            Assert.That(dash.IsDashing, Is.False);
            Assert.That(owner.transform.position, Is.EqualTo(initialPosition));
        }

        [Test]
        public void CancelOnlySucceedsBeforeChargeConfirmation()
        {
            Assert.That(controller.CancelChargeTargeting(), Is.False);
            Assert.That(controller.BeginChargeTargeting(), Is.True);
            Assert.That(controller.CancelChargeTargeting(), Is.True);
            Assert.That(controller.Snapshot.IsChargeReady, Is.True);

            Assert.That(controller.BeginChargeTargeting(), Is.True);
            Assert.That(controller.TryConfirmCharge(target.transform.position, target), Is.True);
            Assert.That(controller.CancelChargeTargeting(), Is.False);
            Assert.That(controller.Snapshot.ChargeCooldown, Is.EqualTo(20f));
        }

        [Test]
        public void OverloadCanActivateDuringTargetingWindowAndDash()
        {
            controller.Tick(10f);
            Assert.That(controller.BeginChargeTargeting(), Is.True);
            Assert.That(controller.TryActivateOverload(), Is.True);

            controller.ResetForDeployment();
            controller.Tick(10f);
            OpenDashWindow();
            Assert.That(controller.TryActivateOverload(), Is.True);

            controller.ResetForDeployment();
            controller.Tick(10f);
            OpenDashWindow();
            Assert.That(controller.TryConsumeDashMove(new Vector3(4f, 0f, 0f)), Is.True);
            Assert.That(controller.TryActivateOverload(), Is.True);
        }

        [Test]
        public void DeathCancelsSequenceWindowDashModifiersAndTimers()
        {
            controller.Tick(10f);
            Assert.That(controller.TryActivateOverload(), Is.True);
            OpenDashWindow();
            Assert.That(controller.TryConsumeDashMove(new Vector3(4f, 0f, 0f)), Is.True);
            Assert.That(executor.IsRunning, Is.True);

            owner.TakePhysicalDamage(owner.MaxHealth);

            AssertTerminatedState();
            Assert.That(controller.BeginChargeTargeting(), Is.False);
            Assert.That(controller.TryActivateOverload(), Is.False);
        }

        [Test]
        public void StopForMatchTerminatesStateAndPreventsFurtherTicksAndInput()
        {
            controller.Tick(10f);
            Assert.That(controller.TryActivateOverload(), Is.True);
            OpenDashWindow();

            controller.StopForMatch();
            controller.Tick(100f);

            AssertTerminatedState();
            Assert.That(controller.BeginChargeTargeting(), Is.False);
            Assert.That(controller.TryConfirmCharge(target.transform.position, target), Is.False);
            Assert.That(controller.TryConsumeDashMove(new Vector3(4f, 0f, 0f)), Is.False);
            Assert.That(controller.CancelChargeTargeting(), Is.False);
            Assert.That(controller.TryActivateOverload(), Is.False);
        }

        [Test]
        public void ResetForDeploymentClearsCommandsSequenceDashAndModifiersThenRestoresInitialState()
        {
            controller.Tick(10f);
            Assert.That(controller.TryActivateOverload(), Is.True);
            OpenDashWindow();
            Assert.That(controller.TryConsumeDashMove(new Vector3(4f, 0f, 0f)), Is.True);
            commands.Issue(UnitCommand.Move(Vector3.right * 4f));

            controller.ResetForDeployment();

            Assert.That(commands.CurrentCommand, Is.Null);
            Assert.That(executor.IsRunning, Is.False);
            Assert.That(dash.IsDashing, Is.False);
            Assert.That(owner.AttackPower, Is.EqualTo(50f));
            Assert.That(controller.Snapshot.SweepProgress, Is.Zero);
            Assert.That(controller.Snapshot.IsChargeReady, Is.True);
            Assert.That(controller.Snapshot.OverloadCooldown, Is.EqualTo(10f));
        }

        [Test]
        public void ConfigureRejectsEveryMissingDependency()
        {
            Assert.That(() => controller.Configure(null, commands, attacks, executor, modifiers, dash),
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("combatOwner"));
            Assert.That(() => controller.Configure(owner, null, attacks, executor, modifiers, dash),
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("commandController"));
            Assert.That(() => controller.Configure(owner, commands, null, executor, modifiers, dash),
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("basicAttackController"));
            Assert.That(() => controller.Configure(owner, commands, attacks, null, modifiers, dash),
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("sequenceExecutor"));
            Assert.That(() => controller.Configure(owner, commands, attacks, executor, null, dash),
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("statModifiers"));
            Assert.That(() => controller.Configure(owner, commands, attacks, executor, modifiers, null),
                Throws.ArgumentNullException.With.Property("ParamName").EqualTo("dashController"));
        }

        [Test]
        public void ReconfigureUnsubscribesOldOwnerExecutorAndPlanProvider()
        {
            CreateFixture(
                "Replacement",
                out _,
                out CombatUnit replacementOwner,
                out _,
                out PlayerCommandController replacementCommands,
                out UnitStatModifiers replacementModifiers,
                out AttackSequenceExecutor replacementExecutor,
                out BasicAttackController replacementAttacks,
                out SkillDashController replacementDash,
                out _);
            controller.Configure(replacementOwner, replacementCommands, replacementAttacks,
                replacementExecutor, replacementModifiers, replacementDash);
            executor.TryStart(new AttackSequencePlan(
                AttackSequenceKind.Basic, 1, 0.05f, 50f, 1f, 0f, 1f, 0f, true, false), target);

            Assert.That(controller.Snapshot.SweepProgress, Is.Zero);

            attacks.SetTarget(target);
            AttackSequencePlan oldPlan = null;
            executor.SequenceFinished += (plan, _) => oldPlan = plan;
            attacks.Tick(0f);
            Assert.That(oldPlan.Kind, Is.EqualTo(AttackSequenceKind.Basic));

            controller.Tick(10f);
            Assert.That(controller.TryActivateOverload(), Is.True);
            owner.TakePhysicalDamage(owner.MaxHealth);
            Assert.That(controller.IsOverloadActive, Is.True);
        }

        [Test]
        public void RepeatedConfigureDoesNotDuplicateSequenceFinishedSubscription()
        {
            controller.Configure(owner, commands, attacks, executor, modifiers, dash);
            controller.Configure(owner, commands, attacks, executor, modifiers, dash);

            CompleteBasicAttack();

            Assert.That(controller.Snapshot.SweepProgress, Is.EqualTo(1));
        }

        [TestCase(typeof(CombatUnit))]
        [TestCase(typeof(PlayerCommandController))]
        [TestCase(typeof(BasicAttackController))]
        [TestCase(typeof(AttackSequenceExecutor))]
        [TestCase(typeof(UnitStatModifiers))]
        [TestCase(typeof(SkillDashController))]
        public void MissingSiblingLogsOnceNamesObjectAndComponentAndDisablesController(Type missingType)
        {
            GameObject incomplete = new GameObject("Incomplete Exusiai");
            gameObjects.Add(incomplete);
            incomplete.AddComponent<UnitMotor>();
            Type[] requiredTypes =
            {
                typeof(CombatUnit),
                typeof(PlayerCommandController),
                typeof(BasicAttackController),
                typeof(AttackSequenceExecutor),
                typeof(UnitStatModifiers),
                typeof(SkillDashController)
            };
            foreach (Type requiredType in requiredTypes)
            {
                if (requiredType != missingType) incomplete.AddComponent(requiredType);
            }

            ExusiaiSkillController incompleteController = incomplete.AddComponent<ExusiaiSkillController>();
            LogAssert.Expect(LogType.Exception, new Regex($"Incomplete Exusiai.*{missingType.Name}"));

            InvokePrivate(incompleteController, "Awake");
            incompleteController.Tick(1f);
            incompleteController.Tick(1f);

            Assert.That(incompleteController.enabled, Is.False);
        }

        [Test]
        public void DestroyUnsubscribesAndClearsOldBasicPlanProvider()
        {
            UnityEngine.Object.DestroyImmediate(controller);
            attacks.SetTarget(target);
            AttackSequencePlan plan = null;
            executor.SequenceFinished += (finished, _) => plan = finished;

            attacks.Tick(0f);

            Assert.That(plan.Kind, Is.EqualTo(AttackSequenceKind.Basic));
            Assert.That(plan.ShotCount, Is.EqualTo(1));
        }

        private void CreateFixture(
            string name,
            out GameObject fixtureObject,
            out CombatUnit fixtureOwner,
            out UnitMotor fixtureMotor,
            out PlayerCommandController fixtureCommands,
            out UnitStatModifiers fixtureModifiers,
            out AttackSequenceExecutor fixtureExecutor,
            out BasicAttackController fixtureAttacks,
            out SkillDashController fixtureDash,
            out ExusiaiSkillController fixtureController)
        {
            fixtureObject = new GameObject(name);
            gameObjects.Add(fixtureObject);
            fixtureMotor = fixtureObject.AddComponent<UnitMotor>();
            fixtureMotor.Configure(5f, ArenaLayout.CreateDefault());
            fixtureCommands = fixtureObject.AddComponent<PlayerCommandController>();
            InvokePrivate(fixtureCommands, "Awake");
            fixtureModifiers = fixtureObject.AddComponent<UnitStatModifiers>();
            fixtureOwner = fixtureObject.AddComponent<CombatUnit>();
            fixtureOwner.Configure(TeamId.Blue, Altitude.Ground, 1000f, 50f, 0f, 6f, 0.5f, true, false);
            fixtureExecutor = fixtureObject.AddComponent<AttackSequenceExecutor>();
            fixtureExecutor.Configure(fixtureOwner);
            fixtureAttacks = fixtureObject.AddComponent<BasicAttackController>();
            fixtureAttacks.Configure(fixtureOwner, fixtureExecutor);
            fixtureDash = fixtureObject.AddComponent<SkillDashController>();
            fixtureDash.Configure(fixtureMotor, ArenaLayout.CreateDefault(), 0);
            fixtureController = fixtureObject.AddComponent<ExusiaiSkillController>();
            fixtureController.Configure(fixtureOwner, fixtureCommands, fixtureAttacks,
                fixtureExecutor, fixtureModifiers, fixtureDash);
        }

        private CombatUnit CreateUnit(string name, TeamId team, Vector3 position)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            gameObject.transform.position = position;
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, 1000f, 50f, 0f, 6f, 0.5f, true, false);
            return unit;
        }

        private void ReadySweep()
        {
            CompleteBasicAttack();
            CompleteBasicAttack();
            CompleteBasicAttack();
            Assert.That(controller.Snapshot.IsSweepReady, Is.True);
        }

        private void CompleteBasicAttack()
        {
            StartBasicAttackAndCapturePlan(true);
        }

        private AttackSequencePlan StartBasicAttackAndCapturePlan(bool complete)
        {
            AttackSequencePlan result = null;
            void Capture(AttackSequencePlan plan, bool _) => result = plan;
            executor.SequenceFinished += Capture;
            if (attacks.CurrentTarget == null) attacks.SetTarget(target);
            attacks.Tick(1f);
            if (executor.IsRunning)
            {
                if (complete) executor.Tick(1f);
                else executor.Cancel();
            }

            executor.SequenceFinished -= Capture;
            Assert.That(result, Is.Not.Null);
            return result;
        }

        private AttackSequencePlan CancelRunningAndCapturePlan()
        {
            AttackSequencePlan result = null;
            void Capture(AttackSequencePlan plan, bool _) => result = plan;
            executor.SequenceFinished += Capture;
            executor.Cancel();
            executor.SequenceFinished -= Capture;
            Assert.That(result, Is.Not.Null);
            return result;
        }

        private void OpenDashWindow()
        {
            Assert.That(controller.BeginChargeTargeting(), Is.True);
            Assert.That(controller.TryConfirmCharge(target.transform.position, target), Is.True);
            Assert.That(controller.IsDashWindowOpen, Is.True);
        }

        private void AssertTerminatedState()
        {
            Assert.That(executor.IsRunning, Is.False);
            Assert.That(controller.IsDashWindowOpen, Is.False);
            Assert.That(controller.IsSelectingChargeTarget, Is.False);
            Assert.That(dash.IsDashing, Is.False);
            Assert.That(controller.IsOverloadActive, Is.False);
            Assert.That(owner.AttackPower, Is.EqualTo(50f).Within(0.001f));
            Assert.That(owner.AttackInterval, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(controller.Snapshot.ChargeCooldown, Is.Zero);
            Assert.That(controller.Snapshot.OverloadCooldown, Is.Zero);
            Assert.That(controller.Snapshot.OverloadDuration, Is.Zero);
            Assert.That(controller.Snapshot.ChargePhase, Is.EqualTo(ExusiaiChargePhase.Inactive));
            Assert.That(controller.BlocksNormalCommands, Is.False);
        }

        private static void AssertPlan(
            AttackSequencePlan plan,
            AttackSequenceKind kind,
            int shots,
            float attackPower,
            float multiplier)
        {
            Assert.That(plan.Kind, Is.EqualTo(kind));
            Assert.That(plan.ShotCount, Is.EqualTo(shots));
            Assert.That(plan.ShotInterval, Is.EqualTo(0.05f));
            Assert.That(plan.AttackPower, Is.EqualTo(attackPower).Within(0.001f));
            Assert.That(plan.DamageMultiplier, Is.EqualTo(multiplier).Within(0.001f));
        }

        private static void InvokePrivate(MonoBehaviour behaviour, string method)
        {
            behaviour.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(behaviour, null);
        }
    }
}
