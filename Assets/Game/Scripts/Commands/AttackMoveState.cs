namespace ArknightsFrontline.Commands
{
    public sealed class AttackMoveState
    {
        public bool IsArmed { get; private set; }

        public void Arm()
        {
            IsArmed = true;
        }

        public void Confirm()
        {
            IsArmed = false;
        }

        public void Cancel()
        {
            IsArmed = false;
        }
    }
}
