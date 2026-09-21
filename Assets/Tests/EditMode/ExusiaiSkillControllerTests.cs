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
            Assert.That(controller.BlocksAttackMove, Is.False);
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
            AssertChargePlan(plan);
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
            AssertChargePlan(plan);
        }

        [Test]
        public void BeginChargeTargetingRequiresReadyLivingOwnerAndBlocksAttackMoveOnly()
        {
            Assert.That(controller.BeginChargeTargeting(), Is.True);
            Assert.That(controller.IsSelectingChargeTarget, Is.True);
            Assert.That(controller.BlocksAttackMove, Is.True);
            Assert.That(controller.BlocksNormalCommands, Is.False);
            Assert.That(controller.BeginChargeTargeting(), Is.False);
            Assert.That(controller.CancelChargeTargeting(), Is.True);
            Assert.That(controller.BlocksAttackMove, Is.False);
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
        public void CompletedChargeSequenceClearsSelectedChargeTarget()
        {
            Assert.That(controller.BeginChargeTargeting(), Is.True);
            Assert.That(controller.TryConfirmCharge(target.transform.position, target), Is.True);
            Assert.That(controller.SelectedChargeTarget, Is.SameAs(target));

            executor.Tick(1f);

            Assert.That(controller.SelectedChargeTarget, Is.Null);
        }

        [Test]
        public void InterruptedChargeSequenceClearsSelectedChargeTarget()
        {
            Assert.That(controller.BeginChargeTargeting(), Is.True);
            Assert.That(controller.TryConfirmCharge(target.transform.position, target), Is.True);
            Assert.That(controller.SelectedChargeTarget, Is.SameAs(target));

            executor.Cancel();

            Assert.That(controller.SelectedChargeTarget, Is.Null);
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
            Assert.That(controller.BlocksAttackMove, Is.True);
            Assert.That(controller.BlocksNormalCommands, Is.False);
            Assert.That(controller.Snapshot.ChargeCooldown, Is.EqualTo(20f));
            Assert.That(controller.Snapshot.ChargePhase, Is.EqualTo(ExusiaiChargePhase.DashWindow));
        }

        [Test]
        public void CommandBlockingMatchesTargetingWindowDashAndIdlePhases()
        {
            Assert.That(controller.BlocksAttackMove, Is.False);
            Assert.That(controller.BlocksNormalCommands, Is.False);

            Assert.That(controller.BeginChargeTargeting(), Is.True);
            Assert.That(controller.BlocksAttackMove, Is.True);
            Assert.That(controller.BlocksNormalCommands, Is.False);

            Assert.That(controller.TryConfirmCharge(target.transform.position, target), Is.True);
            Assert.That(controller.BlocksAttackMove, Is.True);
            Assert.That(controller.BlocksNormalCommands, Is.False);

            Assert.That(controller.TryConsumeDashMove(Vector3.right * 4f), Is.True);
            Assert.That(controller.BlocksAttackMove, Is.True);
            Assert.That(controller.BlocksNormalCommands, Is.True);

            dash.Tick(1f);
            Assert.That(controller.BlocksAttackMove, Is.False);
            Assert.That(controller.BlocksNormalCommands, Is.False);
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
        public void DisableClosesStateDetachesOwnershipAndRequiresExplicitConfigureToRestore()
        {
            controller.Tick(10f);
            Assert.That(controller.TryActivateOverload(), Is.True);
            OpenDashWindow();
            Assert.That(controller.TryConsumeDashMove(Vector3.right * 4f), Is.True);
            Assert.That(executor.IsRunning, Is.True);
            Assert.That(dash.IsDashing, Is.True);

            controller.enabled = false;
            InvokePrivate(controller, "OnDisable");

            Assert.That(executor.IsRunning, Is.False);
            Assert.That(dash.IsDashing, Is.False);
            Assert.That(owner.AttackPower, Is.EqualTo(50f));
            Assert.That(ReadPrivateField<Func<AttackSequencePlan>>(attacks, "planProvider"), Is.Null);

            controller.enabled = true;
            controller.ResetForDeployment();
            controller.Tick(100f);
            Assert.That(controller.BeginChargeTargeting(), Is.False);
            Assert.That(controller.TryActivateOverload(), Is.False);
            Assert.That(controller.Snapshot.ChargePhase, Is.EqualTo(ExusiaiChargePhase.Inactive));

            Assert.That(executor.TryStart(new AttackSequencePlan(
                AttackSequenceKind.Basic, 1, 0.05f, 50f, 1f, 0f, 1f, 0f, true, false), target), Is.True);
            Assert.That(controller.Snapshot.SweepProgress, Is.Zero);

            controller.Configure(owner, commands, attacks, executor, modifiers, dash);
            Assert.That(controller.Snapshot.IsChargeReady, Is.True);
            Assert.That(ReadPrivateField<Func<AttackSequencePlan>>(attacks, "planProvider"), Is.Not.Null);
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

        [Test]
        public void AwakeConfiguresUninitializedSiblingPipelineInDependencyOrder()
        {
            GameObject automatic = new GameObject("Automatic Exusiai");
            gameObjects.Add(automatic);
            UnitMotor automaticMotor = automatic.AddComponent<UnitMotor>();
            automaticMotor.Configure(5f, ArenaLayout.CreateDefault());
            PlayerCommandController automaticCommands = automatic.AddComponent<PlayerCommandController>();
            InvokePrivate(automaticCommands, "Awake");
            automatic.AddComponent<UnitStatModifiers>();
            CombatUnit automaticOwner = automatic.AddComponent<CombatUnit>();
            automaticOwner.Configure(
                TeamId.Blue, Altitude.Ground, 1000f, 50f, 0f, 6f, 0.5f, true, false);
            AttackSequenceExecutor automaticExecutor = automatic.AddComponent<AttackSequenceExecutor>();
            BasicAttackController automaticAttacks = automatic.AddComponent<BasicAttackController>();
            SkillDashController automaticDash = automatic.AddComponent<SkillDashController>();

            ExusiaiSkillController automaticController = automatic.AddComponent<ExusiaiSkillController>();
            InvokePrivate(automaticController, "Awake");
            CombatUnit automaticTarget = CreateUnit("Automatic Target", TeamId.Red, Vector3.right * 2f);

            automaticAttacks.SetTarget(automaticTarget);
            automaticAttacks.Tick(0f);
            Assert.That(automaticController.Snapshot.SweepProgress, Is.EqualTo(1));

            Assert.That(automaticController.BeginChargeTargeting(), Is.True);
            Assert.That(automaticController.TryConfirmCharge(
                automaticTarget.transform.position, automaticTarget), Is.True);
            Assert.That(automaticExecutor.IsRunning, Is.True);
            Assert.That(automaticController.TryConsumeDashMove(Vector3.right * 4f), Is.True);
            Assert.That(automaticDash.IsDashing, Is.True);
        }

        [Test]
        public void AwakePreservesCorrectlyConfiguredRunningSiblingPipeline()
        {
            GameObject automatic = new GameObject("Running Automatic Exusiai");
            gameObjects.Add(automatic);
            UnitMotor automaticMotor = automatic.AddComponent<UnitMotor>();
            automaticMotor.Configure(5f, ArenaLayout.CreateDefault());
            PlayerCommandController automaticCommands = automatic.AddComponent<PlayerCommandController>();
            InvokePrivate(automaticCommands, "Awake");
            automatic.AddComponent<UnitStatModifiers>();
            CombatUnit automaticOwner = automatic.AddComponent<CombatUnit>();
            automaticOwner.Configure(
                TeamId.Blue, Altitude.Ground, 1000f, 50f, 0f, 6f, 0.5f, true, false);
            AttackSequenceExecutor automaticExecutor = automatic.AddComponent<AttackSequenceExecutor>();
            automaticExecutor.Configure(automaticOwner);
            BasicAttackController automaticAttacks = automatic.AddComponent<BasicAttackController>();
            automaticAttacks.Configure(automaticOwner, automaticExecutor);
            SkillDashController automaticDash = automatic.AddComponent<SkillDashController>();
            automaticDash.Configure(automaticMotor, ArenaLayout.CreateDefault(), 0);
            CombatUnit automaticTarget = CreateUnit("Running Automatic Target", TeamId.Red, Vector3.right * 2f);
            automaticAttacks.SetPlanProvider(() => new AttackSequencePlan(
                AttackSequenceKind.Basic, 5, 0.05f, 50f, 1f, 0f, 1f, 0f, true, false));
            automaticAttacks.SetTarget(automaticTarget);
            automaticAttacks.Tick(0f);
            Assert.That(automaticDash.TryStart(Vector3.right * 4f), Is.True);

            ExusiaiSkillController automaticController = automatic.AddComponent<ExusiaiSkillController>();
            InvokePrivate(automaticController, "Awake");

            Assert.That(automaticExecutor.IsRunning, Is.True);
            Assert.That(automaticAttacks.CurrentTarget, Is.SameAs(automaticTarget));
            Assert.That(automaticDash.IsDashing, Is.True);
            automaticExecutor.Tick(1f);
            Assert.That(automaticController.Snapshot.SweepProgress, Is.EqualTo(1));
            Assert.That(automaticController.BeginChargeTargeting(), Is.True);
        }

        [Test]
        public void AwakePreservesCorrectlyConfiguredIdlePipelineAndSubscribesOnce()
        {
            GameObject automatic = new GameObject("Idle Automatic Exusiai");
            gameObjects.Add(automatic);
            automatic.transform.position = Vector3.left * 4f;
            UnitMotor automaticMotor = automatic.AddComponent<UnitMotor>();
            automaticMotor.Configure(5f, ArenaLayout.CreateDefault());
            PlayerCommandController automaticCommands = automatic.AddComponent<PlayerCommandController>();
            InvokePrivate(automaticCommands, "Awake");
            automatic.AddComponent<UnitStatModifiers>();
            CombatUnit automaticOwner = automatic.AddComponent<CombatUnit>();
            automaticOwner.Configure(
                TeamId.Blue, Altitude.Ground, 1000f, 50f, 0f, 6f, 0.5f, true, false);
            AttackSequenceExecutor automaticExecutor = automatic.AddComponent<AttackSequenceExecutor>();
            automaticExecutor.Configure(automaticOwner);
            BasicAttackController automaticAttacks = automatic.AddComponent<BasicAttackController>();
            automaticAttacks.Configure(automaticOwner, automaticExecutor);
            SkillDashController automaticDash = automatic.AddComponent<SkillDashController>();
            const int customObstacleLayer = 10;
            automaticDash.Configure(automaticMotor, default, 1 << customObstacleLayer);
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObjects.Add(obstacle);
            obstacle.layer = customObstacleLayer;
            obstacle.transform.position = new Vector3(-2f, 0.5f, 0f);
            Physics.SyncTransforms();
            CombatUnit automaticTarget = CreateUnit("Idle Automatic Target", TeamId.Red, Vector3.right * 2f);
            automaticAttacks.SetTarget(automaticTarget);
            Assert.That(automaticDash.Preview(Vector3.right * 4f, out Vector3 beforeEndpoint), Is.True);
            Assert.That(beforeEndpoint.x, Is.EqualTo(-2.75f).Within(0.001f));

            ExusiaiSkillController automaticController = automatic.AddComponent<ExusiaiSkillController>();
            InvokePrivate(automaticController, "Awake");

            Assert.That(automaticExecutor.IsRunning, Is.False);
            Assert.That(automaticAttacks.CurrentTarget, Is.SameAs(automaticTarget));
            Assert.That(automaticDash.IsDashing, Is.False);
            Assert.That(automaticDash.Preview(Vector3.right * 4f, out Vector3 afterEndpoint), Is.True);
            Assert.That(afterEndpoint, Is.EqualTo(beforeEndpoint));
            automaticAttacks.Tick(0f);
            Assert.That(automaticController.Snapshot.SweepProgress, Is.EqualTo(1));
        }

        [TestCase(typeof(UnitMotor))]
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
            Type[] requiredTypes =
            {
                typeof(UnitMotor),
                typeof(CombatUnit),
                typeof(PlayerCommandController),
                typeof(BasicAttackController),
                typeof(AttackSequenceExecutor),
                typeof(UnitStatModifiers),
                typeof(SkillDashController)
            };
            foreach (Type requiredType in requiredTypes)
            {
                if (requiredType == missingType) continue;
                if (missingType == typeof(UnitMotor) && requiredType == typeof(PlayerCommandController)) continue;
                incomplete.AddComponent(requiredType);
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
            Assert.That(controller.BlocksAttackMove, Is.False);
            Assert.That(controller.BlocksNormalCommands, Is.False);
        }

        private static void AssertChargePlan(AttackSequencePlan plan)
        {
            Assert.That(plan.LastShotMissingHealthRatio, Is.Zero);
            Assert.That(plan.MovementSlowMultiplier, Is.EqualTo(0.70f).Within(0.001f));
            Assert.That(plan.SlowDuration, Is.EqualTo(2f).Within(0.001f));
            Assert.That(plan.CountsAsBasicAttack, Is.False);
            Assert.That(plan.IgnoreRangeAfterStart, Is.True);
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

        private static T ReadPrivateField<T>(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(instance);
        }

        private static void InvokePrivate(MonoBehaviour behaviour, string method)
        {
            behaviour.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(behaviour, null);
        }
    }
}
