using System;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Movement;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class CombatCommandResolver : MonoBehaviour
    {
        private CombatUnit owner;
        private UnitMotor motor;
        private PlayerCommandController commandSource;
        private BasicAttackController attackController;

        public CombatUnit CurrentTarget { get; private set; }

        private void Awake()
        {
            CombatUnit combatOwner = GetComponent<CombatUnit>();
            UnitMotor unitMotor = GetComponent<UnitMotor>();
            PlayerCommandController playerCommandController = GetComponent<PlayerCommandController>();
            BasicAttackController basicAttackController = GetComponent<BasicAttackController>();
            if (combatOwner != null
                && unitMotor != null
                && playerCommandController != null
                && basicAttackController != null)
            {
                Configure(combatOwner, unitMotor, playerCommandController, basicAttackController);
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

        public void Configure(
            CombatUnit combatOwner,
            UnitMotor unitMotor,
            PlayerCommandController playerCommandSource,
            BasicAttackController basicAttackController)
        {
            if (combatOwner == null)
            {
                throw new ArgumentNullException(nameof(combatOwner));
            }

            if (unitMotor == null)
            {
                throw new ArgumentNullException(nameof(unitMotor));
            }

            if (playerCommandSource == null)
            {
                throw new ArgumentNullException(nameof(playerCommandSource));
            }

            if (basicAttackController == null)
            {
                throw new ArgumentNullException(nameof(basicAttackController));
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
            commandSource = playerCommandSource;
            attackController = basicAttackController;
            if (owner.IsDead)
            {
                motor.Stop();
                ClearCombatTarget();
            }
        }

        public void Tick(float deltaTime)
        {
            if (owner == null || motor == null || commandSource == null || attackController == null)
            {
                return;
            }

            if (owner.IsDead)
            {
                motor.Stop();
                ClearCombatTarget();
                return;
            }

            UnitCommand? command = commandSource.CurrentCommand;
            if (!command.HasValue)
            {
                ClearCombatTarget();
                return;
            }

            switch (command.Value.Kind)
            {
                case UnitCommandKind.Attack:
                    ResolveSpecifiedTarget(command.Value.TargetObject);
                    break;
                case UnitCommandKind.AttackNearestInRange:
                    ResolveNearestTarget();
                    break;
                case UnitCommandKind.Move:
                case UnitCommandKind.Stop:
                    ClearCombatTarget();
                    break;
            }
        }

        private void ResolveSpecifiedTarget(GameObject targetObject)
        {
            CombatUnit target = targetObject == null ? null : targetObject.GetComponent<CombatUnit>();
            if (!TargetRules.IsLegal(owner, target))
            {
                ClearCombatTarget();
                return;
            }

            if (!IsInAttackRange(target))
            {
                ClearCombatTarget();
                motor.SetDestination(target.transform.position);
                return;
            }

            motor.Stop();
            CurrentTarget = target;
            attackController.SetTarget(target);
        }

        private void ResolveNearestTarget()
        {
            CombatUnit target = TargetSelector.FindNearestInRange(owner);
            if (target == null)
            {
                ClearCombatTarget();
                return;
            }

            motor.Stop();
            CurrentTarget = target;
            attackController.SetTarget(target);
        }

        private void ClearCombatTarget()
        {
            CurrentTarget = null;
            attackController?.ClearTarget();
        }

        private bool IsInAttackRange(CombatUnit target)
        {
            Vector3 ownerPosition = owner.transform.position;
            Vector3 targetPosition = target.transform.position;
            float horizontalDistance = Vector2.Distance(
                new Vector2(ownerPosition.x, ownerPosition.z),
                new Vector2(targetPosition.x, targetPosition.z));
            return horizontalDistance <= owner.AttackRange;
        }

        private void OnOwnerDied(CombatUnit _)
        {
            motor?.Stop();
            ClearCombatTarget();
        }
    }
}
