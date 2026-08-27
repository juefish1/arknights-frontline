using System;
using ArknightsFrontline.Combat;
using UnityEngine;

namespace ArknightsFrontline.Arena
{
    public sealed class TowerCombatController : MonoBehaviour
    {
        private CombatUnit owner;
        private BasicAttackController attack;
        private bool stopped;

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

        public void Configure(CombatUnit combatOwner, BasicAttackController attackController)
        {
            if (combatOwner == null)
            {
                throw new ArgumentNullException(nameof(combatOwner));
            }

            if (attackController == null)
            {
                throw new ArgumentNullException(nameof(attackController));
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

            attack = attackController;
            stopped = false;

            if (owner.IsDead)
            {
                StopForMatch();
            }
        }

        public void Tick(float deltaTime)
        {
            if (stopped || owner.IsDead)
            {
                StopForMatch();
                return;
            }

            CombatUnit target = TargetSelector.FindNearestInRange(owner);
            if (target != null)
            {
                attack.SetTarget(target);
                return;
            }

            attack.ClearTarget();
        }

        public void StopForMatch()
        {
            stopped = true;
            if (attack != null)
            {
                attack.ClearTarget();
            }
        }

        private void OnOwnerDied(CombatUnit _)
        {
            StopForMatch();
        }
    }
}
