using System;
using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    [DisallowMultipleComponent]
    public sealed class SimpleOperatorAiController : MonoBehaviour
    {
        private const float RecentDamageWindowSeconds = 2f;
        private const float SafeRetrySeconds = 1.5f;

        private readonly Dictionary<EntityId, float> recentOperatorDamageAge = new Dictionary<EntityId, float>();
        private readonly List<EntityId> expiredDamageSources = new List<EntityId>();
        private readonly List<CombatUnit> sceneUnits = new List<CombatUnit>();

        private CombatUnit owner;
        private OperatorRosterSlot slot;
        private CombatUnit friendlyTower;
        private ArenaLayout layout;
        private UnitMotor motor;
        private BasicAttackController attacks;
        private OperatorRetreatController retreat;
        private MatchOutcomeController match;
        private Func<float> timeProvider;
        private float manualTime;
        private float safeRetryNotBefore;
        private bool safeRetryArmed;
        private bool guidanceAttemptInProgress;
        private bool retreatInterruptionPendingDamageCheck;
        private bool isConfigured;
        private bool isStopped;

        public CombatUnit CurrentTarget => attacks == null ? null : attacks.CurrentTarget;

        private void OnDestroy()
        {
            Unsubscribe();
            if (attacks != null)
            {
                attacks.ClearTarget();
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Configure(
            OperatorIdentity operatorIdentity,
            OperatorRosterSlot rosterSlot,
            CombatUnit friendlyTower,
            ArenaLayout arenaLayout,
            MatchOutcomeController matchController = null,
            Func<float> timeProvider = null)
        {
            Configure(
                operatorIdentity,
                rosterSlot,
                friendlyTower,
                GetComponent<OperatorRetreatController>(),
                arenaLayout,
                matchController,
                timeProvider);
        }

        public void Configure(
            OperatorIdentity operatorIdentity,
            OperatorRosterSlot rosterSlot,
            CombatUnit friendlyTower,
            OperatorRetreatController retreatController,
            ArenaLayout arenaLayout,
            MatchOutcomeController matchController = null,
            Func<float> timeProvider = null)
        {
            if (operatorIdentity == null)
            {
                throw new ArgumentNullException(nameof(operatorIdentity));
            }

            if (friendlyTower == null)
            {
                throw new ArgumentNullException(nameof(friendlyTower));
            }

            CombatUnit resolvedOwner = GetComponent<CombatUnit>();
            UnitMotor resolvedMotor = GetComponent<UnitMotor>();
            BasicAttackController resolvedAttacks = GetComponent<BasicAttackController>();
            if (resolvedOwner == null || resolvedMotor == null || resolvedAttacks == null || retreatController == null)
            {
                throw new InvalidOperationException(
                    "A computer operator requires CombatUnit, UnitMotor, BasicAttackController and OperatorRetreatController.");
            }

            if (operatorIdentity.gameObject != gameObject || operatorIdentity.Team != resolvedOwner.Team
                || friendlyTower.Team != resolvedOwner.Team || retreatController.gameObject != gameObject)
            {
                throw new ArgumentException("The computer identity and retreat tower must match its CombatUnit team.");
            }

            if (rosterSlot != null && (rosterSlot.Team != resolvedOwner.Team
                || rosterSlot.OperatorType != operatorIdentity.OperatorType
                || rosterSlot.CurrentOperator != resolvedOwner))
            {
                throw new ArgumentException("The computer roster slot must describe the live operator being configured.");
            }

            Unsubscribe();
            attacks?.ClearTarget();
            motor?.Stop();
            owner = resolvedOwner;
            slot = rosterSlot;
            this.friendlyTower = friendlyTower;
            layout = arenaLayout;
            motor = resolvedMotor;
            attacks = resolvedAttacks;
            retreat = retreatController;
            this.timeProvider = timeProvider;
            match = matchController != null
                ? matchController
                : UnityEngine.Object.FindAnyObjectByType<MatchOutcomeController>();
            safeRetryArmed = false;
            guidanceAttemptInProgress = false;
            retreatInterruptionPendingDamageCheck = false;
            isStopped = false;
            isConfigured = true;
            manualTime = 0f;
            safeRetryNotBefore = 0f;
            recentOperatorDamageAge.Clear();
            attacks.Configure(owner);
            retreat.Configure(match);
            owner.DamageTaken += OnOwnerDamageTaken;
            owner.Died += OnOwnerDied;
            if (match != null)
            {
                match.MatchEnding += OnMatchEnding;
            }

            retreat.GuidanceStarted += OnGuidanceStarted;
            retreat.GuidanceInterrupted += OnGuidanceInterrupted;
            retreat.GuidanceCompleted += OnGuidanceCompleted;
        }

        public void Tick(float deltaTime)
        {
            if (!isConfigured || isStopped)
            {
                return;
            }

            if (owner == null || owner.IsDead || slot != null
                && (slot.IsStopped || slot.CurrentOperator != owner)
                || match != null && match.IsEnding)
            {
                StopForMatch();
                return;
            }

            float elapsed = Mathf.Max(0f, deltaTime);
            if (timeProvider == null && !Application.isPlaying)
            {
                manualTime += elapsed;
            }

            float now = GetCurrentTime();
            ExpireRecentDamage(now);

            if (retreat != null && retreat.IsGuiding)
            {
                guidanceAttemptInProgress = true;
                motor.Stop();
                attacks.ClearTarget();
                return;
            }

            if (owner.CurrentHealth / owner.MaxHealth < 0.25f
                && (!safeRetryArmed || now >= safeRetryNotBefore)
                && retreat.TryBegin())
            {
                guidanceAttemptInProgress = true;
                motor.Stop();
                attacks.ClearTarget();
                return;
            }

            List<CombatUnit> units = GetActiveUnits();
            CombatUnit target = SimpleOperatorTargetSelector.Select(
                owner,
                units,
                candidate => WasRecentlyDamagedBy(candidate, now));
            if (target != null)
            {
                motor.Stop();
                attacks.SetTarget(target);
                return;
            }

            attacks.ClearTarget();
            FollowLaneOrReturnToTower(units);
        }

        public void StopForMatch()
        {
            if (isStopped)
            {
                return;
            }

            isStopped = true;
            motor?.Stop();
            attacks?.ClearTarget();
        }

        private void FollowLaneOrReturnToTower(List<CombatUnit> units)
        {
            float direction = owner.Team == TeamId.Blue ? 1f : -1f;
            CombatUnit nearestMinion = SimpleOperatorTargetSelector.FindNearestFriendlyMinion(owner, units);
            Vector3 destination;
            if (nearestMinion != null)
            {
                destination = nearestMinion.transform.position - new Vector3(direction * 2f, 0f, 0f);
            }
            else
            {
                destination = friendlyTower.transform.position + new Vector3(direction * 4f, 0f, 0f);
                destination.z = owner.transform.position.z;
            }

            motor.SetDestination(layout.Clamp(destination));
        }

        private List<CombatUnit> GetActiveUnits()
        {
            sceneUnits.Clear();
            sceneUnits.AddRange(UnityEngine.Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None));
            return sceneUnits;
        }

        private bool WasRecentlyDamagedBy(CombatUnit candidate, float now)
        {
            if (candidate == null || !recentOperatorDamageAge.TryGetValue(candidate.GetEntityId(), out float lastHitAt))
            {
                return false;
            }

            return now - lastHitAt <= RecentDamageWindowSeconds;
        }

        private void ExpireRecentDamage(float now)
        {
            if (recentOperatorDamageAge.Count == 0)
            {
                return;
            }

            expiredDamageSources.Clear();
            foreach (KeyValuePair<EntityId, float> damageSource in recentOperatorDamageAge)
            {
                float age = now - damageSource.Value;
                if (age > RecentDamageWindowSeconds)
                {
                    expiredDamageSources.Add(damageSource.Key);
                }
            }

            for (int index = 0; index < expiredDamageSources.Count; index++)
            {
                recentOperatorDamageAge.Remove(expiredDamageSources[index]);
            }
        }

        private float GetCurrentTime()
        {
            if (timeProvider != null)
            {
                return timeProvider();
            }

            return Application.isPlaying ? Time.time : manualTime;
        }

        private void OnOwnerDamageTaken(CombatUnit attacker, float actualDamage)
        {
            if (actualDamage <= 0f || owner == null || attacker == null || attacker.Team == owner.Team)
            {
                retreatInterruptionPendingDamageCheck = false;
                return;
            }

            bool isEnemyOperator = attacker.GetComponent<OperatorIdentity>() != null;
            bool isEnemyTower = attacker.GetComponent<TowerCombatController>() != null;
            if (!isEnemyOperator && !isEnemyTower)
            {
                retreatInterruptionPendingDamageCheck = false;
                return;
            }

            bool guidanceWasInterrupted = guidanceAttemptInProgress || retreatInterruptionPendingDamageCheck;
            if (safeRetryArmed || guidanceWasInterrupted)
            {
                safeRetryArmed = true;
                if (!owner.IsDead && (match == null || !match.IsEnding))
                {
                    safeRetryNotBefore = GetCurrentTime() + SafeRetrySeconds;
                }
            }

            if (isEnemyOperator)
            {
                recentOperatorDamageAge[attacker.GetEntityId()] = GetCurrentTime();
            }

            retreatInterruptionPendingDamageCheck = false;
        }

        private void OnGuidanceStarted(CombatUnit _)
        {
            guidanceAttemptInProgress = true;
        }

        private void OnGuidanceInterrupted(CombatUnit _)
        {
            if (guidanceAttemptInProgress && owner != null && !owner.IsDead
                && (match == null || !match.IsEnding))
            {
                retreatInterruptionPendingDamageCheck = true;
            }

            guidanceAttemptInProgress = false;
        }

        private void OnGuidanceCompleted(CombatUnit _)
        {
            guidanceAttemptInProgress = false;
            retreatInterruptionPendingDamageCheck = false;
        }

        private void OnOwnerDied(CombatUnit _)
        {
            retreatInterruptionPendingDamageCheck = false;
            guidanceAttemptInProgress = false;
            StopForMatch();
        }

        private void OnMatchEnding()
        {
            retreatInterruptionPendingDamageCheck = false;
            guidanceAttemptInProgress = false;
            StopForMatch();
        }

        private void Unsubscribe()
        {
            if (owner != null)
            {
                owner.DamageTaken -= OnOwnerDamageTaken;
                owner.Died -= OnOwnerDied;
            }

            if (match != null)
            {
                match.MatchEnding -= OnMatchEnding;
            }

            if (retreat != null)
            {
                retreat.GuidanceStarted -= OnGuidanceStarted;
                retreat.GuidanceInterrupted -= OnGuidanceInterrupted;
                retreat.GuidanceCompleted -= OnGuidanceCompleted;
            }
        }
    }
}
