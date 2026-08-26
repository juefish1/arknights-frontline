using System;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class BasicAttackController : MonoBehaviour
    {
        private CombatUnit owner;
        private CombatUnit target;
        private float elapsedSinceAttack;
        private bool hasRequestedFirstAttack;

        public CombatUnit CurrentTarget => target;

        public event Action<CombatUnit, CombatUnit> AttackRequested;

        private void Awake()
        {
            AttackRequested += SpawnProjectile;
            CombatUnit combatUnit = GetComponent<CombatUnit>();
            if (combatUnit != null)
            {
                Configure(combatUnit);
            }
        }

        private void OnDestroy()
        {
            AttackRequested -= SpawnProjectile;
            if (owner != null)
            {
                owner.Died -= OnOwnerDied;
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Configure(CombatUnit combatOwner)
        {
            if (combatOwner == null)
            {
                throw new ArgumentNullException(nameof(combatOwner));
            }

            if (owner != combatOwner)
            {
                if (owner != null)
                {
                    owner.Died -= OnOwnerDied;
                }

                owner = combatOwner;
                owner.Died += OnOwnerDied;
            }

            if (owner.IsDead)
            {
                ClearTarget();
            }
        }

        public void SetTarget(CombatUnit combatTarget)
        {
            if (target == combatTarget)
            {
                return;
            }

            target = combatTarget;
            elapsedSinceAttack = 0f;
            hasRequestedFirstAttack = false;
        }

        public void ClearTarget()
        {
            target = null;
            elapsedSinceAttack = 0f;
            hasRequestedFirstAttack = false;
        }

        public void Tick(float deltaTime)
        {
            if (!HasLegalTargetInRange())
            {
                ClearTarget();
                return;
            }

            if (!hasRequestedFirstAttack)
            {
                hasRequestedFirstAttack = true;
                RequestAttack();
                return;
            }

            float interval = owner.AttackInterval;
            if (interval <= 0f)
            {
                RequestAttack();
                return;
            }

            elapsedSinceAttack += Mathf.Max(0f, deltaTime);
            while (elapsedSinceAttack >= interval)
            {
                elapsedSinceAttack -= interval;
                if (!RequestAttack())
                {
                    break;
                }
            }
        }

        private bool RequestAttack()
        {
            AttackRequested?.Invoke(owner, target);
            if (HasLegalTargetInRange())
            {
                return true;
            }

            ClearTarget();
            return false;
        }

        private bool HasLegalTargetInRange()
        {
            if (!TargetRules.IsLegal(owner, target))
            {
                return false;
            }

            Vector3 ownerPosition = owner.transform.position;
            Vector3 targetPosition = target.transform.position;
            float horizontalDistance = Vector2.Distance(
                new Vector2(ownerPosition.x, ownerPosition.z),
                new Vector2(targetPosition.x, targetPosition.z));
            return horizontalDistance <= owner.AttackRange;
        }

        private void OnOwnerDied(CombatUnit _)
        {
            ClearTarget();
        }

        private void SpawnProjectile(CombatUnit attacker, CombatUnit attackTarget)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            GameObject projectileObject = new GameObject("Projectile");
            Projectile projectile = projectileObject.AddComponent<Projectile>();
            projectile.Initialize(attacker, attackTarget, attacker.AttackPower, 16f);
        }
    }
}
