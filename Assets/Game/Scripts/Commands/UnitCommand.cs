using System;
using UnityEngine;

namespace ArknightsFrontline.Commands
{
    public enum UnitCommandKind
    {
        Move,
        Attack,
        AttackMove,
        Stop
    }

    public readonly struct UnitCommand
    {
        private UnitCommand(UnitCommandKind kind, Vector3 destination, GameObject targetObject)
        {
            Kind = kind;
            Destination = destination;
            TargetObject = targetObject;
        }

        public UnitCommandKind Kind { get; }

        public Vector3 Destination { get; }

        public GameObject TargetObject { get; }

        public static UnitCommand Move(Vector3 destination)
        {
            return new UnitCommand(UnitCommandKind.Move, destination, null);
        }

        public static UnitCommand Attack(GameObject targetObject)
        {
            if (targetObject == null)
            {
                throw new ArgumentNullException(nameof(targetObject));
            }

            return new UnitCommand(UnitCommandKind.Attack, default, targetObject);
        }

        public static UnitCommand AttackMove(Vector3 destination)
        {
            return new UnitCommand(UnitCommandKind.AttackMove, destination, null);
        }

        public static UnitCommand Stop()
        {
            return new UnitCommand(UnitCommandKind.Stop, default, null);
        }
    }
}
