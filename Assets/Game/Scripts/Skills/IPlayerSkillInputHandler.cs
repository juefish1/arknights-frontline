using UnityEngine;

namespace ArknightsFrontline.Skills
{
    public interface IPlayerSkillInputHandler
    {
        bool BlocksAttackMove { get; }

        bool BlocksNormalCommands { get; }

        void HandleSkill2();

        void HandleSkill3();

        bool TryHandleConfirm(Vector3 worldPoint, GameObject hitObject);

        bool TryHandleMoveClick(Vector3 worldPoint);

        bool TryHandleCancel();

        void HandleStop();
    }
}
