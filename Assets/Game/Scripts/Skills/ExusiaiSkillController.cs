using System;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Movement;
using UnityEngine;

namespace ArknightsFrontline.Skills
{
    public enum ExusiaiChargePhase
    {
        Inactive,
        Ready,
        Targeting,
        DashWindow,
        Cooldown
    }

    public readonly struct ExusiaiSkillSnapshot
    {
        public ExusiaiSkillSnapshot(
            int sweepProgress,
            bool isSweepReady,
            ExusiaiChargePhase chargePhase,
            bool isChargeReady,
            float dashWindowRemaining,
            float chargeCooldown,
            bool isOverloadActive,
            float overloadDuration,
            float overloadCooldown)
        {
            SweepProgress = sweepProgress;
            IsSweepReady = isSweepReady;
            ChargePhase = chargePhase;
            IsChargeReady = isChargeReady;
            DashWindowRemaining = dashWindowRemaining;
            ChargeCooldown = chargeCooldown;
            IsOverloadActive = isOverloadActive;
            OverloadDuration = overloadDuration;
            OverloadCooldown = overloadCooldown;
        }

        public int SweepProgress { get; }
        public bool IsSweepReady { get; }
        public ExusiaiChargePhase ChargePhase { get; }
        public bool IsChargeReady { get; }
        public float DashWindowRemaining { get; }
        public float ChargeCooldown { get; }
        public bool IsOverloadActive { get; }
        public float OverloadDuration { get; }
        public float OverloadCooldown { get; }
    }

    [DisallowMultipleComponent]
    public sealed class ExusiaiSkillController : MonoBehaviour, IPlayerSkillInputHandler
    {
        // TagManager remains user-owned; Stage 5 reserves numeric layer 10 without naming it.
        public const int ReservedObstacleLayerIndex = 10;
        private const int SweepRequiredAttacks = 3;
        private const float SweepDamageMultiplier = 1.45f;
        private const float SweepMissingHealthRatio = 0.08f;
        private const float ChargeCooldownDuration = 20f;
        private const float ChargeDamageMultiplier = 1.25f;
        private const float ChargeSlowMultiplier = 0.70f;
        private const float ChargeSlowDuration = 2f;
        private const float OverloadInitialWait = 10f;
        private const float OverloadCooldownDuration = 30f;
        private const float OverloadDuration = 10f;
        private const float OverloadAttackMultiplier = 1.10f;
        private const float OverloadMovementMultiplier = 1.08f;
        private const float OverloadAttackIntervalOffset = -0.22f;
        private const float MultiShotInterval = 0.05f;
        private const string OverloadModifierSource = "Exusiai.R";

        private readonly SkillCountdown chargeCooldown = new SkillCountdown();
        private readonly SkillCountdown overloadCooldown = new SkillCountdown();
        private readonly SkillCountdown overloadDuration = new SkillCountdown();

        private CombatUnit owner;
        private PlayerCommandController commands;
        private BasicAttackController basicAttacks;
        private AttackSequenceExecutor sequenceExecutor;
        private UnitStatModifiers modifiers;
        private SkillDashController dash;
        private AttackSequencePlan pendingChargePlan;
        private int sweepProgress;
        private bool selectingChargeTarget;
        private bool overloadActive;
        private bool configured;
        private bool stopped;
        private bool missingDependencyLogged;
        private bool hasFrozenSnapshot;
        private ExusiaiSkillSnapshot frozenSnapshot;

        public bool IsSelectingChargeTarget => selectingChargeTarget;

        public bool IsStopped => stopped;

        // Retained for existing HUD and indicator consumers until their migration is complete.
        public bool IsDashWindowOpen => false;

        public bool IsOverloadActive => overloadActive;

        public CombatUnit SelectedChargeTarget { get; private set; }

        public bool BlocksAttackMove => CanRun()
            && (selectingChargeTarget || dash.IsDashing);

        public bool BlocksNormalCommands => CanRun() && dash.IsDashing;

        public ExusiaiSkillSnapshot Snapshot => hasFrozenSnapshot
            ? frozenSnapshot
            : new ExusiaiSkillSnapshot(
                sweepProgress,
                sweepProgress >= SweepRequiredAttacks,
                GetChargePhase(),
                IsChargeReady(),
                0f,
                chargeCooldown.Remaining,
                overloadActive,
                overloadActive ? overloadDuration.Remaining : 0f,
                overloadCooldown.Remaining);

        private void Awake()
        {
            CombatUnit combatOwner = GetComponent<CombatUnit>();
            UnitMotor unitMotor = GetComponent<UnitMotor>();
            PlayerCommandController commandController = GetComponent<PlayerCommandController>();
            BasicAttackController basicAttackController = GetComponent<BasicAttackController>();
            AttackSequenceExecutor attackSequenceExecutor = GetComponent<AttackSequenceExecutor>();
            UnitStatModifiers statModifiers = GetComponent<UnitStatModifiers>();
            SkillDashController dashController = GetComponent<SkillDashController>();
            string missing = combatOwner == null ? nameof(CombatUnit)
                : unitMotor == null ? nameof(UnitMotor)
                : commandController == null ? nameof(PlayerCommandController)
                : basicAttackController == null ? nameof(BasicAttackController)
                : attackSequenceExecutor == null ? nameof(AttackSequenceExecutor)
                : statModifiers == null ? nameof(UnitStatModifiers)
                : dashController == null ? nameof(SkillDashController)
                : null;
            if (missing != null)
            {
                LogMissingDependencyOnce(missing);
                enabled = false;
                return;
            }

            if (!attackSequenceExecutor.IsConfiguredFor(combatOwner))
            {
                attackSequenceExecutor.Configure(combatOwner);
            }
            if (!basicAttackController.IsConfiguredFor(combatOwner, attackSequenceExecutor))
            {
                basicAttackController.Configure(combatOwner, attackSequenceExecutor);
            }
            if (!dashController.IsConfiguredFor(unitMotor))
            {
                dashController.Configure(
                    unitMotor, ArenaLayout.CreateDefault(),
                    1 << ReservedObstacleLayerIndex);
            }

            ConfigureCore(combatOwner, commandController, basicAttackController,
                attackSequenceExecutor, statModifiers, dashController, false);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            if (configured) StopForMatch();
            DetachSubscriptions();
            configured = false;
        }

        private void OnDestroy()
        {
            if (configured) StopForMatch();
            DetachSubscriptions();
            configured = false;
        }

        public void Configure(
            CombatUnit combatOwner,
            PlayerCommandController commandController,
            BasicAttackController basicAttackController,
            AttackSequenceExecutor sequenceExecutor,
            UnitStatModifiers statModifiers,
            SkillDashController dashController)
        {
            ConfigureCore(combatOwner, commandController, basicAttackController,
                sequenceExecutor, statModifiers, dashController, true);
        }

        private void ConfigureCore(
            CombatUnit combatOwner,
            PlayerCommandController commandController,
            BasicAttackController basicAttackController,
            AttackSequenceExecutor sequenceExecutor,
            UnitStatModifiers statModifiers,
            SkillDashController dashController,
            bool resetPipeline)
        {
            if (combatOwner == null) throw new ArgumentNullException(nameof(combatOwner));
            if (commandController == null) throw new ArgumentNullException(nameof(commandController));
            if (basicAttackController == null) throw new ArgumentNullException(nameof(basicAttackController));
            if (sequenceExecutor == null) throw new ArgumentNullException(nameof(sequenceExecutor));
            if (statModifiers == null) throw new ArgumentNullException(nameof(statModifiers));
            if (dashController == null) throw new ArgumentNullException(nameof(dashController));

            if (!resetPipeline
                && configured
                && owner == combatOwner
                && commands == commandController
                && basicAttacks == basicAttackController
                && this.sequenceExecutor == sequenceExecutor
                && modifiers == statModifiers
                && dash == dashController)
            {
                return;
            }

            if (configured && resetPipeline) ClearRuntimeState(true);
            DetachSubscriptions();

            owner = combatOwner;
            commands = commandController;
            basicAttacks = basicAttackController;
            this.sequenceExecutor = sequenceExecutor;
            modifiers = statModifiers;
            dash = dashController;
            commands.SetSkillInputHandler(this);
            CommandFeedbackPresenter feedback = GetComponent<CommandFeedbackPresenter>();
            if (feedback != null) feedback.ConfigureSkillController(this);
            owner.Died += OnOwnerDied;
            this.sequenceExecutor.SequenceFinished += OnSequenceFinished;
            basicAttacks.SetPlanProvider(CreateNextBasicAttackPlan);
            configured = true;
            stopped = false;
            enabled = true;
            if (resetPipeline)
            {
                ResetForDeployment();
            }
            else
            {
                InitializeSkillState();
            }
        }

        public void Tick(float deltaTime)
        {
            if (!CanRun()) return;

            chargeCooldown.Tick(deltaTime);
            overloadCooldown.Tick(deltaTime);

            if (!overloadActive) return;
            overloadDuration.Tick(deltaTime);
            if (overloadDuration.IsReady) EndOverload();
        }

        public bool BeginChargeTargeting()
        {
            if (!CanRun() || !IsChargeReady()) return false;
            selectingChargeTarget = true;
            SelectedChargeTarget = null;
            return true;
        }

        public bool TryConfirmCharge(Vector3 point, CombatUnit directTarget)
        {
            if (!CanRun() || !selectingChargeTarget || !dash.TryStart(point)) return false;

            selectingChargeTarget = false;
            SelectedChargeTarget = null;

            commands.CancelCurrentCommand();
            basicAttacks.ClearTarget();
            sequenceExecutor.Cancel();

            chargeCooldown.Start(ChargeCooldownDuration);
            pendingChargePlan = new AttackSequencePlan(
                AttackSequenceKind.Charge,
                overloadActive ? 5 : 4,
                MultiShotInterval,
                owner.AttackPower,
                ChargeDamageMultiplier,
                0f,
                ChargeSlowMultiplier,
                ChargeSlowDuration,
                false,
                true);
            dash.DashCompleted += OnDashCompleted;

            return true;
        }

        public bool TryConsumeDashMove(Vector3 point)
        {
            return false;
        }

        public bool CancelChargeTargeting()
        {
            if (!CanRun() || !selectingChargeTarget) return false;
            selectingChargeTarget = false;
            SelectedChargeTarget = null;
            return true;
        }

        public bool TryActivateOverload()
        {
            if (!CanRun() || overloadActive || !overloadCooldown.IsReady) return false;
            overloadActive = true;
            overloadDuration.Start(OverloadDuration);
            overloadCooldown.Start(OverloadCooldownDuration);
            modifiers.SetAttackPowerMultiplier(OverloadModifierSource, OverloadAttackMultiplier);
            modifiers.SetMovementSpeedMultiplier(OverloadModifierSource, OverloadMovementMultiplier);
            modifiers.SetAttackIntervalOffset(OverloadModifierSource, OverloadAttackIntervalOffset);
            return true;
        }

        public void HandleSkill2()
        {
            if (!CanRun())
            {
                return;
            }

            if (selectingChargeTarget)
            {
                CancelChargeTargeting();
                return;
            }

            BeginChargeTargeting();
        }

        public void HandleSkill3()
        {
            TryActivateOverload();
        }

        public bool TryHandleConfirm(Vector3 worldPoint, GameObject hitObject)
        {
            if (!CanRun())
            {
                return false;
            }

            if (dash.IsDashing)
            {
                return true;
            }

            bool wasSelectingChargeTarget = selectingChargeTarget;
            bool confirmed = TryConfirmCharge(worldPoint, null);
            return wasSelectingChargeTarget || confirmed;
        }

        public bool TryHandleMoveClick(Vector3 worldPoint)
        {
            if (!CanRun())
            {
                return false;
            }

            return dash.IsDashing;
        }

        public bool TryHandleCancel()
        {
            return CancelChargeTargeting();
        }

        public void HandleStop()
        {
            if (!CanRun())
            {
                return;
            }

            CancelChargeTargeting();
            ClearPendingChargePlan();
            SelectedChargeTarget = null;
            basicAttacks.ClearTarget();
            sequenceExecutor.Cancel();
            dash.Cancel();
        }

        public void ResetForDeployment()
        {
            if (!configured || !isActiveAndEnabled) return;
            stopped = false;
            ClearRuntimeState(true);
            InitializeSkillState();
        }

        private void InitializeSkillState()
        {
            hasFrozenSnapshot = false;
            selectingChargeTarget = false;
            ClearPendingChargePlan();
            SelectedChargeTarget = null;
            EndOverload();
            sweepProgress = 0;
            chargeCooldown.Reset(0f);
            overloadCooldown.Reset(OverloadInitialWait);
            overloadDuration.Reset(0f);
        }

        public void StopForMatch()
        {
            Stop(true);
        }

        private void Stop(bool preserveSnapshot)
        {
            if (!configured) return;
            if (preserveSnapshot && !hasFrozenSnapshot)
            {
                ExusiaiSkillSnapshot snapshot = Snapshot;
                bool hasTemporaryChargePhase = snapshot.ChargePhase == ExusiaiChargePhase.Targeting;
                if (hasTemporaryChargePhase)
                {
                    bool chargeReady = chargeCooldown.IsReady;
                    frozenSnapshot = new ExusiaiSkillSnapshot(
                        snapshot.SweepProgress,
                        snapshot.IsSweepReady,
                        chargeReady ? ExusiaiChargePhase.Ready : ExusiaiChargePhase.Cooldown,
                        chargeReady,
                        0f,
                        snapshot.ChargeCooldown,
                        snapshot.IsOverloadActive,
                        snapshot.OverloadDuration,
                        snapshot.OverloadCooldown);
                }
                else
                {
                    frozenSnapshot = snapshot;
                }
                hasFrozenSnapshot = true;
            }
            else if (!preserveSnapshot)
            {
                hasFrozenSnapshot = false;
            }
            ClearRuntimeState(true);
            stopped = true;
            sweepProgress = 0;
            chargeCooldown.Reset(0f);
            overloadCooldown.Reset(0f);
            overloadDuration.Reset(0f);
        }

        private AttackSequencePlan CreateNextBasicAttackPlan()
        {
            bool consumeSweep = sweepProgress >= SweepRequiredAttacks;
            int shots = overloadActive ? 5 : consumeSweep ? 3 : 1;
            float multiplier = consumeSweep ? SweepDamageMultiplier : 1f;
            AttackSequenceKind kind = consumeSweep
                ? (overloadActive ? AttackSequenceKind.OverloadSweep : AttackSequenceKind.Sweep)
                : (overloadActive ? AttackSequenceKind.Overload : AttackSequenceKind.Basic);
            if (consumeSweep) sweepProgress = 0;
            return new AttackSequencePlan(
                kind,
                shots,
                MultiShotInterval,
                owner.AttackPower,
                multiplier,
                consumeSweep ? SweepMissingHealthRatio : 0f,
                1f,
                0f,
                true,
                false);
        }

        private void OnSequenceFinished(AttackSequencePlan plan, bool completed)
        {
            if (!configured || stopped) return;
            if (plan.Kind == AttackSequenceKind.Charge) SelectedChargeTarget = null;
            if (!completed || !plan.CountsAsBasicAttack) return;
            sweepProgress = Mathf.Min(SweepRequiredAttacks, sweepProgress + 1);
        }

        private void OnOwnerDied(CombatUnit _)
        {
            Stop(false);
        }

        private void OnDashCompleted()
        {
            dash.DashCompleted -= OnDashCompleted;
            AttackSequencePlan plan = pendingChargePlan;
            pendingChargePlan = null;
            if (plan == null || !CanRun()) return;

            CombatUnit selected = TargetSelector.FindNearestInRange(owner);
            if (selected == null)
            {
                SelectedChargeTarget = null;
                return;
            }

            SelectedChargeTarget = selected;
            if (!sequenceExecutor.TryStart(plan, selected))
            {
                SelectedChargeTarget = null;
            }
        }

        private bool CanRun()
        {
            return configured && isActiveAndEnabled && !stopped && owner != null && !owner.IsDead;
        }

        private bool IsChargeReady()
        {
            return CanRun() && chargeCooldown.IsReady && !selectingChargeTarget;
        }

        private ExusiaiChargePhase GetChargePhase()
        {
            if (!configured || !isActiveAndEnabled || stopped || owner == null || owner.IsDead)
            {
                return ExusiaiChargePhase.Inactive;
            }
            if (selectingChargeTarget) return ExusiaiChargePhase.Targeting;
            return chargeCooldown.IsReady ? ExusiaiChargePhase.Ready : ExusiaiChargePhase.Cooldown;
        }

        private void ClearRuntimeState(bool clearCommand)
        {
            selectingChargeTarget = false;
            ClearPendingChargePlan();
            SelectedChargeTarget = null;
            if (clearCommand) commands?.CancelCurrentCommand();
            basicAttacks?.ClearTarget();
            sequenceExecutor?.Cancel();
            dash?.Cancel();
            EndOverload();
        }

        private void ClearPendingChargePlan()
        {
            if (dash != null) dash.DashCompleted -= OnDashCompleted;
            pendingChargePlan = null;
        }

        private void EndOverload()
        {
            overloadActive = false;
            overloadDuration.Reset(0f);
            modifiers?.RemoveSource(OverloadModifierSource);
        }

        private void DetachSubscriptions()
        {
            if (owner != null) owner.Died -= OnOwnerDied;
            if (sequenceExecutor != null) sequenceExecutor.SequenceFinished -= OnSequenceFinished;
            if (dash != null) dash.DashCompleted -= OnDashCompleted;
            if (basicAttacks != null) basicAttacks.SetPlanProvider(null);
        }

        private void LogMissingDependencyOnce(string componentName)
        {
            if (missingDependencyLogged) return;
            missingDependencyLogged = true;
            Debug.LogException(new MissingReferenceException(
                $"{gameObject.name} cannot configure {nameof(ExusiaiSkillController)}: missing {componentName}."), this);
        }
    }
}
