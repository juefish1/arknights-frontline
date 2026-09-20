using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public enum AttackSequenceKind
    {
        Basic,
        Sweep,
        Charge,
        Overload,
        OverloadSweep
    }

    public sealed class AttackSequencePlan
    {
        public AttackSequencePlan(
            AttackSequenceKind kind,
            int shotCount,
            float shotInterval,
            float attackPower,
            float damageMultiplier,
            float lastShotMissingHealthRatio,
            float movementSlowMultiplier,
            float slowDuration,
            bool countsAsBasicAttack,
            bool ignoreRangeAfterStart)
        {
            Kind = kind;
            ShotCount = Mathf.Max(1, shotCount);
            ShotInterval = Mathf.Max(0f, shotInterval);
            AttackPower = attackPower;
            DamageMultiplier = damageMultiplier;
            LastShotMissingHealthRatio = lastShotMissingHealthRatio;
            MovementSlowMultiplier = movementSlowMultiplier;
            SlowDuration = slowDuration;
            CountsAsBasicAttack = countsAsBasicAttack;
            IgnoreRangeAfterStart = ignoreRangeAfterStart;
        }

        public AttackSequenceKind Kind { get; }
        public int ShotCount { get; }
        public float ShotInterval { get; }
        public float AttackPower { get; }
        public float DamageMultiplier { get; }
        public float LastShotMissingHealthRatio { get; }
        public float MovementSlowMultiplier { get; }
        public float SlowDuration { get; }
        public bool CountsAsBasicAttack { get; }
        public bool IgnoreRangeAfterStart { get; }
    }
}
