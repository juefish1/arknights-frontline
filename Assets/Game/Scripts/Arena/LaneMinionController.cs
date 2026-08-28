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
        private bool isConfigured;

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
            isConfigured = true;

            if (owner.IsDead)
            {
                StopForMatch();
            }
        }

        public void Tick(float deltaTime)
        {
            if (!isConfigured)
            {
                return;
            }

            if (stopped || owner.IsDead)
            {
                StopForMatch();
                return;
            }

            CombatUnit currentTarget = attack.CurrentTarget;
            if (IsLegalAndInRange(currentTarget))
            {
                motor.Stop();
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
            return IsLegalAndInRange(enemyTower) ? enemyTower : null;
        }

        private bool IsLegalAndInRange(CombatUnit target)
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
            StopForMatch();
        }
    }
}
