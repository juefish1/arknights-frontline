using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public readonly struct PhysicalDamagePayload
    {
        public PhysicalDamagePayload(
            float attackPower,
            float damageMultiplier,
            float missingHealthRatio,
            float movementSlowMultiplier,
            float slowDuration)
        {
            AttackPower = Mathf.Max(0f, attackPower);
            DamageMultiplier = Mathf.Max(0f, damageMultiplier);
            MissingHealthRatio = Mathf.Max(0f, missingHealthRatio);
            MovementSlowMultiplier = Mathf.Clamp01(movementSlowMultiplier);
            SlowDuration = Mathf.Max(0f, slowDuration);
        }

        public float AttackPower { get; }

        public float DamageMultiplier { get; }

        public float MissingHealthRatio { get; }

        public float MovementSlowMultiplier { get; }

        public float SlowDuration { get; }
    }
}
