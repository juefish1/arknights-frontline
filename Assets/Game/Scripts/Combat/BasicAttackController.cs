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
            CombatUnit combatUnit = GetComponent<CombatUnit>();
            if (combatUnit != null)
            {
                Configure(combatUnit);
            }
        }

        private void OnDestroy()
        {
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
                AttackRequested?.Invoke(owner, target);
                return;
            }

            float interval = owner.AttackInterval;
            if (interval <= 0f)
            {
                AttackRequested?.Invoke(owner, target);
                return;
            }

            elapsedSinceAttack += Mathf.Max(0f, deltaTime);
            while (elapsedSinceAttack >= interval)
            {
                elapsedSinceAttack -= interval;
                AttackRequested?.Invoke(owner, target);
                if (!HasLegalTargetInRange())
                {
                    ClearTarget();
                    break;
                }
            }
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
    }
}
