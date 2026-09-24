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
        private const int SweepRequiredAttacks = 3;
        private const float SweepDamageMultiplier = 1.45f;
        private const float SweepMissingHealthRatio = 0.08f;
        private const float ChargeCooldownDuration = 20f;
        private const float ChargeDashWindowDuration = 0.25f;
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
        private readonly SkillCountdown dashWindow = new SkillCountdown();
        private readonly SkillCountdown overloadCooldown = new SkillCountdown();
        private readonly SkillCountdown overloadDuration = new SkillCountdown();

        private CombatUnit owner;
        private PlayerCommandController commands;
        private BasicAttackController basicAttacks;
        private AttackSequenceExecutor sequenceExecutor;
        private UnitStatModifiers modifiers;
        private SkillDashController dash;
        private int sweepProgress;
        private bool selectingChargeTarget;
        private bool dashWindowOpen;
        private bool overloadActive;
        private bool configured;
        private bool stopped;
        private bool missingDependencyLogged;

        public bool IsSelectingChargeTarget => selectingChargeTarget;

        public bool IsDashWindowOpen => configured && !stopped && dashWindowOpen && dashWindow.Remaining > 0f;

        public bool IsOverloadActive => overloadActive;

        public CombatUnit SelectedChargeTarget { get; private set; }

        public bool BlocksAttackMove => CanRun()
            && (selectingChargeTarget || IsDashWindowOpen || dash.IsDashing);

        public bool BlocksNormalCommands => CanRun() && dash.IsDashing;

        public ExusiaiSkillSnapshot Snapshot => new ExusiaiSkillSnapshot(
            sweepProgress,
            sweepProgress >= SweepRequiredAttacks,
            GetChargePhase(),
            IsChargeReady(),
            dashWindow.Remaining,
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
                    unitMotor, ArenaLayout.CreateDefault(), LayerMask.GetMask("Obstacle"));
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
            if (dashWindowOpen)
            {
                dashWindow.Tick(deltaTime);
                if (dashWindow.IsReady) dashWindowOpen = false;
            }

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
            if (!CanRun() || !selectingChargeTarget || !IsPointInAttackRange(point)) return false;

            CombatUnit selected = IsLegalTargetInAttackRange(directTarget)
                ? directTarget
                : TargetSelector.FindNearestInRangeFromPoint(owner, point);
            selectingChargeTarget = false;
            SelectedChargeTarget = selected;

            commands.CancelCurrentCommand();
            basicAttacks.ClearTarget();
            sequenceExecutor.Cancel();

            chargeCooldown.Start(ChargeCooldownDuration);
            dashWindow.Start(ChargeDashWindowDuration);
            dashWindowOpen = true;

            if (selected != null)
            {
                AttackSequencePlan plan = new AttackSequencePlan(
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
                sequenceExecutor.TryStart(plan, selected);
            }

            return true;
        }

        public bool TryConsumeDashMove(Vector3 point)
        {
            if (!CanRun() || !IsDashWindowOpen) return false;
            dashWindowOpen = false;
            dashWindow.Reset(0f);
            return dash.TryStart(point);
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

            if (IsDashWindowOpen || dash.IsDashing)
            {
                return true;
            }

            bool wasSelectingChargeTarget = selectingChargeTarget;

            CombatUnit directTarget = null;
            if (hitObject != null
                && hitObject.layer == LayerMask.NameToLayer("Targetable")
                && hitObject.TryGetComponent(out CombatUnit hitUnit)
                && TargetRules.IsLegal(owner, hitUnit))
            {
                directTarget = hitUnit;
            }

            bool confirmed = TryConfirmCharge(worldPoint, directTarget);
            return wasSelectingChargeTarget || confirmed;
        }

        public bool TryHandleMoveClick(Vector3 worldPoint)
        {
            if (!CanRun())
            {
                return false;
            }

            if (IsDashWindowOpen)
            {
                TryConsumeDashMove(worldPoint);
                return true;
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
            dashWindowOpen = false;
            dashWindow.Reset(0f);
            SelectedChargeTarget = null;
            basicAttacks.ClearTarget();
            sequenceExecutor.Cancel();
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
            selectingChargeTarget = false;
            dashWindowOpen = false;
            SelectedChargeTarget = null;
            dashWindow.Reset(0f);
            EndOverload();
            sweepProgress = 0;
            chargeCooldown.Reset(0f);
            overloadCooldown.Reset(OverloadInitialWait);
            overloadDuration.Reset(0f);
        }

        public void StopForMatch()
        {
            if (!configured) return;
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
            StopForMatch();
        }

        private bool CanRun()
        {
            return configured && isActiveAndEnabled && !stopped && owner != null && !owner.IsDead;
        }

        private bool IsChargeReady()
        {
            return CanRun() && chargeCooldown.IsReady && !selectingChargeTarget && !IsDashWindowOpen;
        }

        private ExusiaiChargePhase GetChargePhase()
        {
            if (!configured || !isActiveAndEnabled || stopped || owner == null || owner.IsDead)
            {
                return ExusiaiChargePhase.Inactive;
            }
            if (selectingChargeTarget) return ExusiaiChargePhase.Targeting;
            if (IsDashWindowOpen) return ExusiaiChargePhase.DashWindow;
            return chargeCooldown.IsReady ? ExusiaiChargePhase.Ready : ExusiaiChargePhase.Cooldown;
        }

        private bool IsPointInAttackRange(Vector3 point)
        {
            Vector3 offset = point - owner.transform.position;
            return new Vector2(offset.x, offset.z).magnitude <= owner.AttackRange;
        }

        private bool IsLegalTargetInAttackRange(CombatUnit candidate)
        {
            if (!TargetRules.IsLegal(owner, candidate)) return false;
            Vector3 offset = candidate.transform.position - owner.transform.position;
            return new Vector2(offset.x, offset.z).magnitude <= owner.AttackRange;
        }

        private void ClearRuntimeState(bool clearCommand)
        {
            selectingChargeTarget = false;
            dashWindowOpen = false;
            SelectedChargeTarget = null;
            dashWindow.Reset(0f);
            if (clearCommand) commands?.CancelCurrentCommand();
            basicAttacks?.ClearTarget();
            sequenceExecutor?.Cancel();
            dash?.Cancel();
            EndOverload();
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
