using System;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Movement;
using UnityEngine;

namespace ArknightsFrontline.Arena
{
    public sealed class LaneMinionController : MonoBehaviour
    {
        private CombatUnit owner;
        private UnitMotor motor;
        private BasicAttackController attack;
        private CombatUnit enemyTower;
        private Vector3 forwardDestination;
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

        public void Configure(
            CombatUnit combatOwner,
            UnitMotor unitMotor,
            BasicAttackController attackController,
            CombatUnit opposingTower,
            Vector3 destination)
        {
            if (combatOwner == null)
            {
                throw new ArgumentNullException(nameof(combatOwner));
            }

            if (unitMotor == null)
            {
                throw new ArgumentNullException(nameof(unitMotor));
            }

            if (attackController == null)
            {
                throw new ArgumentNullException(nameof(attackController));
            }

            if (opposingTower == null)
            {
                throw new ArgumentNullException(nameof(opposingTower));
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

            motor = unitMotor;
            attack = attackController;
            enemyTower = opposingTower;
            forwardDestination = destination;
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

            CombatUnit unitTarget = TargetSelector.FindNearestInRange(
                owner,
                candidate => candidate.GetComponent<TowerCombatController>() == null);
            CombatUnit target = unitTarget ?? FindTowerIfInRange();
            if (target != null)
            {
                motor.Stop();
                attack.SetTarget(target);
                return;
            }

            attack.ClearTarget();
            motor.SetDestination(forwardDestination);
        }

        public void StopForMatch()
        {
            stopped = true;
            if (motor != null)
            {
                motor.Stop();
            }

            if (attack != null)
            {
                attack.ClearTarget();
            }
        }

        private CombatUnit FindTowerIfInRange()
        {
            if (!TargetRules.IsLegal(owner, enemyTower))
            {
                return null;
            }

            Vector3 ownerPosition = owner.transform.position;
            Vector3 towerPosition = enemyTower.transform.position;
            float horizontalDistance = Vector2.Distance(
                new Vector2(ownerPosition.x, ownerPosition.z),
                new Vector2(towerPosition.x, towerPosition.z));
            return horizontalDistance <= owner.AttackRange ? enemyTower : null;
        }

        private void OnOwnerDied(CombatUnit _)
        {
            StopForMatch();
        }
    }
}
