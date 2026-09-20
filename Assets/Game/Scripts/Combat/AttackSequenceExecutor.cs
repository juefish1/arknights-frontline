using System;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class AttackSequenceExecutor : MonoBehaviour
    {
        // Accommodate float delta inputs at boundaries without accumulating interval subtraction error.
        private const double TimingTolerance = 0.0000001d;

        private CombatUnit owner;
        private CombatUnit target;
        private AttackSequencePlan plan;
        private double elapsed;
        private int shotsFired;
        private bool emittingShot;
        private bool notifyingCompletion;

        public bool IsRunning => plan != null;

        public event Action<CombatUnit, PhysicalDamagePayload> ShotRequested;
        public event Action<AttackSequencePlan, bool> SequenceFinished;

        public void Configure(CombatUnit combatOwner)
        {
            if (combatOwner == null) throw new ArgumentNullException(nameof(combatOwner));
            Cancel();
            if (owner != null) owner.Died -= OnOwnerDied;
            owner = combatOwner;
            owner.Died += OnOwnerDied;
        }

        public bool TryStart(AttackSequencePlan attackPlan, CombatUnit initialTarget)
        {
            if (IsRunning || emittingShot || notifyingCompletion || !isActiveAndEnabled
                || attackPlan == null || !TargetRules.IsLegal(owner, initialTarget)
                || !IsInRange(initialTarget))
            {
                return false;
            }

            plan = attackPlan;
            target = initialTarget;
            elapsed = 0d;
            shotsFired = 0;
            EmitShot();
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning || emittingShot || notifyingCompletion) return;
            if (owner == null || owner.IsDead)
            {
                Cancel();
                return;
            }

            elapsed += Mathf.Max(0f, deltaTime);
            while (IsRunning && elapsed + TimingTolerance >= (double)plan.ShotInterval * shotsFired)
            {
                if (TargetRules.IsLegal(owner, target))
                {
                    if (!plan.IgnoreRangeAfterStart && !IsInRange(target))
                    {
                        Cancel();
                        return;
                    }
                }
                else
                {
                    target = TargetSelector.FindNearestInRange(owner);
                    if (target == null)
                    {
                        Cancel();
                        return;
                    }
                }

                EmitShot();
            }
        }

        public void Cancel()
        {
            Finish(false);
        }

        private void EmitShot()
        {
            AttackSequencePlan shotPlan = plan;
            CombatUnit shotTarget = target;
            PhysicalDamagePayload payload = new PhysicalDamagePayload(
                shotPlan.AttackPower, shotPlan.DamageMultiplier,
                shotsFired == shotPlan.ShotCount - 1 ? shotPlan.LastShotMissingHealthRatio : 0f,
                shotPlan.MovementSlowMultiplier, shotPlan.SlowDuration);
            shotsFired++;
            emittingShot = true;
            try
            {
                if (Application.isPlaying)
                {
                    GameObject projectileObject = new GameObject("Projectile");
                    Projectile projectile = projectileObject.AddComponent<Projectile>();
                    projectile.Initialize(owner, shotTarget, payload, 16f);
                }

                ShotRequested?.Invoke(shotTarget, payload);
            }
            finally
            {
                emittingShot = false;
            }

            if (IsRunning && shotsFired == shotPlan.ShotCount) Finish(true);
        }

        private void Finish(bool completed)
        {
            if (!IsRunning) return;
            AttackSequencePlan finished = plan;
            plan = null;
            target = null;
            shotsFired = 0;
            elapsed = 0d;
            notifyingCompletion = true;
            try
            {
                SequenceFinished?.Invoke(finished, completed);
            }
            finally
            {
                notifyingCompletion = false;
            }
        }

        private bool IsInRange(CombatUnit candidate)
        {
            Vector3 offset = candidate.transform.position - owner.transform.position;
            return new Vector2(offset.x, offset.z).magnitude <= owner.AttackRange;
        }

        private void Update() => Tick(Time.deltaTime);
        private void OnDisable() => Cancel();
        private void OnOwnerDied(CombatUnit _) => Cancel();

        private void OnDestroy()
        {
            Cancel();
            if (owner != null) owner.Died -= OnOwnerDied;
        }
    }
}
