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
        private int observedCommandRevision = -1;
        private bool hasEngagedCurrentCommand;

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
                hasEngagedCurrentCommand = false;
                return;
            }

            UnitCommand? command = commandSource.CurrentCommand;
            if (!command.HasValue)
            {
                ClearCombatTarget();
                hasEngagedCurrentCommand = false;
                return;
            }

            if (observedCommandRevision != commandSource.CommandRevision)
            {
                observedCommandRevision = commandSource.CommandRevision;
                hasEngagedCurrentCommand = false;
                ClearCombatTarget();
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
                    hasEngagedCurrentCommand = false;
                    break;
            }
        }

        private void ResolveSpecifiedTarget(GameObject targetObject)
        {
            CombatUnit target = targetObject == null ? null : targetObject.GetComponent<CombatUnit>();
            if (!TargetRules.IsLegal(owner, target))
            {
                CancelCurrentCommand();
                return;
            }

            if (!IsInAttackRange(target))
            {
                if (hasEngagedCurrentCommand)
                {
                    CancelCurrentCommand();
                    return;
                }

                ClearCombatTarget();
                motor.SetDestination(target.transform.position);
                return;
            }

            EngageTarget(target);
        }

        private void ResolveNearestTarget()
        {
            if (hasEngagedCurrentCommand)
            {
                if (!TargetRules.IsLegal(owner, CurrentTarget) || !IsInAttackRange(CurrentTarget))
                {
                    CancelCurrentCommand();
                    return;
                }

                EngageTarget(CurrentTarget);
                return;
            }

            CombatUnit target = TargetSelector.FindNearestInRange(owner);
            if (target == null)
            {
                CancelCurrentCommand();
                return;
            }

            EngageTarget(target);
        }

        private void EngageTarget(CombatUnit target)
        {
            motor.Stop();
            CurrentTarget = target;
            attackController.SetTarget(target);
            hasEngagedCurrentCommand = true;
        }

        private void CancelCurrentCommand()
        {
            motor.Stop();
            ClearCombatTarget();
            commandSource.CancelCurrentCommand();
            observedCommandRevision = commandSource.CommandRevision;
            hasEngagedCurrentCommand = false;
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
            hasEngagedCurrentCommand = false;
        }
    }
}
