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

        private UnitStatModifiers modifiers;
        private bool deathNotified;

        public TeamId Team => team;

        public Altitude Altitude => altitude;

        public float MaxHealth => maxHealth;

        public float CurrentHealth => currentHealth;

        public float BaseAttackPower => attackPower;

        public float AttackPower => Modifiers == null ? attackPower : Modifiers.ApplyAttackPower(attackPower);

        public float Defense => defense;

        public float AttackRange => attackRange;

        public float BaseAttackInterval => attackInterval;

        public float AttackInterval => Modifiers == null ? attackInterval : Modifiers.ApplyAttackInterval(attackInterval);

        public bool CanAttackGround => canAttackGround;

        public bool CanAttackAir => canAttackAir;

        public bool IsDead => currentHealth <= 0f;

        public event Action<CombatUnit> Died;

        public event Action<CombatUnit, float> DamageTaken;

        private UnitStatModifiers Modifiers => modifiers == null ? modifiers = GetComponent<UnitStatModifiers>() : modifiers;

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
            deathNotified = false;
            this.attackPower = attackPower;
            this.defense = Mathf.Max(0f, defense);
            this.attackRange = Mathf.Max(0f, attackRange);
            this.attackInterval = Mathf.Max(0f, attackInterval);
            this.canAttackGround = canAttackGround;
            this.canAttackAir = canAttackAir;
        }

        public void TakePhysicalDamage(float damage)
        {
            TakePhysicalDamage(damage, null);
        }

        public void TakePhysicalDamage(float damage, CombatUnit attacker)
        {
            if (IsDead)
            {
                return;
            }

            float previousHealth = currentHealth;
            currentHealth = Mathf.Max(0f, currentHealth - Mathf.Max(0f, damage));
            float actualDamage = previousHealth - currentHealth;
            if (actualDamage <= 0f)
            {
                return;
            }

            DamageTaken?.Invoke(attacker, actualDamage);

            if (IsDead && !deathNotified)
            {
                deathNotified = true;
                Died?.Invoke(this);
            }
        }
    }
}
