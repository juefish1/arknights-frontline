using ArknightsFrontline.Arena;
using ArknightsFrontline.Camera;
using ArknightsFrontline.Input;
using ArknightsFrontline.Movement;
using UnityEngine;

namespace ArknightsFrontline.Commands
{
    [RequireComponent(typeof(UnitMotor))]
    public sealed class PlayerCommandController : MonoBehaviour
    {
        private readonly AttackMoveState attackMoveState = new AttackMoveState();
        private readonly ArenaLayout layout = ArenaLayout.CreateDefault();

        private GameInputActions input;
        private UnitMotor motor;
        private GameObject currentTarget;

        public GameObject CurrentTarget => currentTarget;

        public bool IsAttackMoveArmed => attackMoveState.IsArmed;

        private void Awake()
        {
            motor = GetComponent<UnitMotor>();
            input = new GameInputActions();
            input.MoveClick.performed += OnMoveClick;
            input.AttackMove.performed += OnAttackMove;
            input.Confirm.performed += OnConfirm;
            input.Stop.performed += OnStop;
            input.Cancel.performed += OnCancel;
            input.CenterCamera.performed += OnCenterCamera;
        }

        private void OnEnable()
        {
            input?.Gameplay.Enable();
        }

        private void OnDisable()
        {
            input?.Gameplay.Disable();
        }

        private void OnDestroy()
        {
            if (input == null)
            {
                return;
            }

            input.MoveClick.performed -= OnMoveClick;
            input.AttackMove.performed -= OnAttackMove;
            input.Confirm.performed -= OnConfirm;
            input.Stop.performed -= OnStop;
            input.Cancel.performed -= OnCancel;
            input.CenterCamera.performed -= OnCenterCamera;
            input.Dispose();
        }

        public void Issue(UnitCommand command)
        {
            switch (command.Kind)
            {
                case UnitCommandKind.Move:
                case UnitCommandKind.AttackMove:
                    motor.SetDestination(command.Destination);
                    break;
                case UnitCommandKind.Attack:
                    currentTarget = command.TargetObject;
                    break;
                case UnitCommandKind.Stop:
                    motor.Stop();
                    break;
            }
        }

        public void ArmAttackMove()
        {
            attackMoveState.Arm();
        }

        public void HandleMoveClick()
        {
            if (attackMoveState.IsArmed)
            {
                attackMoveState.Cancel();
                return;
            }

            if (!TryRaycast(out RaycastHit hit))
            {
                return;
            }

            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Targetable"))
            {
                Issue(UnitCommand.Attack(hit.collider.gameObject));
                return;
            }

            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                Issue(UnitCommand.Move(layout.Clamp(hit.point)));
            }
        }

        private void OnMoveClick(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            HandleMoveClick();
        }

        private void OnAttackMove(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            ArmAttackMove();
        }

        private void OnConfirm(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!attackMoveState.IsArmed || !TryRaycast(out RaycastHit hit))
            {
                return;
            }

            if (hit.collider.gameObject.layer != LayerMask.NameToLayer("Ground"))
            {
                return;
            }

            Vector3 destination = layout.Clamp(hit.point);
            attackMoveState.Confirm(destination);
            Issue(UnitCommand.AttackMove(destination));
        }

        private void OnStop(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            Issue(UnitCommand.Stop());
        }

        private void OnCancel(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (context.control.device is UnityEngine.InputSystem.Keyboard)
            {
                attackMoveState.Cancel();
            }
        }

        private void OnCenterCamera(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            MobaCameraController cameraController = mainCamera.GetComponent<MobaCameraController>();
            if (cameraController != null)
            {
                cameraController.CenterOn(transform);
            }
        }

        private bool TryRaycast(out RaycastHit hit)
        {
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            if (mainCamera == null)
            {
                hit = default;
                return false;
            }

            int raycastLayers = LayerMask.GetMask("Ground", "Targetable");
            Ray ray = mainCamera.ScreenPointToRay(input.PointerPosition.ReadValue<Vector2>());
            return Physics.Raycast(ray, out hit, Mathf.Infinity, raycastLayers);
        }
    }
}
