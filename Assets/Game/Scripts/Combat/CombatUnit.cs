using System;
using ArknightsFrontline.Common;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class CombatUnit : MonoBehaviour
    {
        [SerializeField] private TeamId team;
        [SerializeField] private Altitude altitude;
        [SerializeField] private float maxHealth;
        [SerializeField] private float currentHealth;
        [SerializeField] private float attackPower;
        [SerializeField] private float defense;
        [SerializeField] private float attackRange;
        [SerializeField] private float attackInterval;
        [SerializeField] private bool canAttackGround;
        [SerializeField] private bool canAttackAir;

        public TeamId Team => team;

        public Altitude Altitude => altitude;

        public float MaxHealth => maxHealth;

        public float CurrentHealth => currentHealth;

        public float AttackPower => attackPower;

        public float Defense => defense;

        public float AttackRange => attackRange;

        public float AttackInterval => attackInterval;

        public bool CanAttackGround => canAttackGround;

        public bool CanAttackAir => canAttackAir;

        public bool IsDead => currentHealth <= 0f;

        public event Action<CombatUnit> Died;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

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
            this.team = team;
            this.altitude = altitude;
            this.maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = this.maxHealth;
            this.attackPower = attackPower;
            this.defense = Mathf.Max(0f, defense);
            this.attackRange = Mathf.Max(0f, attackRange);
            this.attackInterval = Mathf.Max(0f, attackInterval);
            this.canAttackGround = canAttackGround;
            this.canAttackAir = canAttackAir;
        }

        public void TakePhysicalDamage(float damage)
        {
            if (IsDead)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - Mathf.Max(0f, damage));
            if (IsDead)
            {
                Died?.Invoke(this);
            }
        }
    }
}
