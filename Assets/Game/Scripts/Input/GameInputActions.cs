using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsFrontline.Input
{
    public sealed class GameInputActions : IDisposable
    {
        public GameInputActions()
        {
            Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            Gameplay = new InputActionMap("Gameplay");
            Asset.AddActionMap(Gameplay);

            MoveClick = AddAction("MoveClick", InputActionType.Button, "<Mouse>/rightButton", "ffeb75c4-54fd-456f-a397-0b2f3b0a3f01");
            AttackMove = AddAction("AttackMove", InputActionType.Button, "<Keyboard>/q", "ffeb75c4-54fd-456f-a397-0b2f3b0a3f02");
            Confirm = AddAction("Confirm", InputActionType.Button, "<Mouse>/leftButton", "ffeb75c4-54fd-456f-a397-0b2f3b0a3f03");
            Stop = AddAction("Stop", InputActionType.Button, "<Keyboard>/s", "ffeb75c4-54fd-456f-a397-0b2f3b0a3f04");
            Skill1 = AddAction("Skill1", InputActionType.Button, "<Keyboard>/w", "ffeb75c4-54fd-456f-a397-0b2f3b0a3f05");
            Skill2 = AddAction("Skill2", InputActionType.Button, "<Keyboard>/e", "ffeb75c4-54fd-456f-a397-0b2f3b0a3f06");
            Skill3 = AddAction("Skill3", InputActionType.Button, "<Keyboard>/r", "ffeb75c4-54fd-456f-a397-0b2f3b0a3f07");
            Retreat = AddAction("Retreat", InputActionType.Button, "<Keyboard>/b", "ffeb75c4-54fd-456f-a397-0b2f3b0a3f08");
            CenterCamera = AddAction("CenterCamera", InputActionType.Button, "<Keyboard>/space", "ffeb75c4-54fd-456f-a397-0b2f3b0a3f09");
            Cancel = AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape", "ffeb75c4-54fd-456f-a397-0b2f3b0a3f10");
            Cancel.AddBinding(new InputBinding
            {
                path = "<Mouse>/rightButton",
                id = new Guid("ffeb75c4-54fd-456f-a397-0b2f3b0a3f11")
            });
            PointerPosition = AddAction("PointerPosition", InputActionType.Value, "<Pointer>/position", "ffeb75c4-54fd-456f-a397-0b2f3b0a3f12");
        }

        public InputActionAsset Asset { get; }

        public InputActionMap Gameplay { get; }

        public InputAction MoveClick { get; }

        public InputAction AttackMove { get; }

        public InputAction Confirm { get; }

        public InputAction Stop { get; }

        public InputAction Skill1 { get; }

        public InputAction Skill2 { get; }

        public InputAction Skill3 { get; }

        public InputAction Retreat { get; }

        public InputAction CenterCamera { get; }

        public InputAction Cancel { get; }

        public InputAction PointerPosition { get; }

        public void Dispose()
        {
            Gameplay.Dispose();
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(Asset);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(Asset);
            }
        }

        private InputAction AddAction(string name, InputActionType type, string bindingPath, string bindingId)
        {
            InputAction action = Gameplay.AddAction(name, type);
            action.AddBinding(new InputBinding
            {
                path = bindingPath,
                id = new Guid(bindingId)
            });

            return action;
        }
    }
}
