using System;
using ArknightsFrontline.Common;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class CombatUnit : MonoBehaviour
    {
        public TeamId Team { get; private set; }

        public Altitude Altitude { get; private set; }

        public float MaxHealth { get; private set; }

        public float CurrentHealth { get; private set; }

        public float AttackPower { get; private set; }

        public float Defense { get; private set; }

        public float AttackRange { get; private set; }

        public float AttackInterval { get; private set; }

        public bool CanAttackGround { get; private set; }

        public bool CanAttackAir { get; private set; }

        public bool IsDead => CurrentHealth <= 0f;

        public event Action<CombatUnit> Died;

        public void Configure(
            TeamId team,
            Altitude altitude,
            float maxHealth,
            float attackPower,
            float defense,
            float attackRange,
            float attackInterval,
            bool canAttackGround,
            bool canAttackAir)
        {
            Team = team;
            Altitude = altitude;
            MaxHealth = Mathf.Max(1f, maxHealth);
            CurrentHealth = MaxHealth;
            AttackPower = attackPower;
            Defense = Mathf.Max(0f, defense);
            AttackRange = Mathf.Max(0f, attackRange);
            AttackInterval = Mathf.Max(0f, attackInterval);
            CanAttackGround = canAttackGround;
            CanAttackAir = canAttackAir;
        }

        public void TakePhysicalDamage(float damage)
        {
            if (IsDead)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - Mathf.Max(0f, damage));
            if (IsDead)
            {
                Died?.Invoke(this);
            }
        }
    }
}
