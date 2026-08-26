using UnityEngine;

namespace ArknightsFrontline.Commands
{
    public sealed class AttackMoveState
    {
        public bool IsArmed { get; private set; }

        public Vector3 LastDestination { get; private set; }

        public void Arm()
        {
            IsArmed = true;
        }

        public void Confirm(Vector3 destination)
        {
            LastDestination = destination;
            IsArmed = false;
        }

        public void Cancel()
        {
            IsArmed = false;
        }
    }
}
