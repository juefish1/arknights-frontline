using ArknightsFrontline.Arena;
using ArknightsFrontline.Camera;
using ArknightsFrontline.Input;
using ArknightsFrontline.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

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
        private InputControl consumedCancelControl;
        private int consumedCancelFrame = -1;
        private bool hasPendingMoveClick;
        private bool pendingMoveClickWasArmed;
        private InputControl pendingMoveControl;

        public GameObject CurrentTarget => currentTarget;

        public UnitCommand? CurrentCommand { get; private set; }

        public bool IsAttackMoveArmed => attackMoveState.IsArmed;

        public InputActionAsset InputActions => input.Asset;

        private void Awake()
        {
            motor = GetComponent<UnitMotor>();
            input = new GameInputActions();
            InputBindingStore.Load(input.Asset);
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
            CurrentCommand = command;
            switch (command.Kind)
            {
                case UnitCommandKind.Move:
                    currentTarget = null;
                    motor.SetDestination(command.Destination);
                    break;
                case UnitCommandKind.Attack:
                    motor.Stop();
                    currentTarget = command.TargetObject;
                    break;
                case UnitCommandKind.AttackNearestInRange:
                    currentTarget = null;
                    motor.Stop();
                    break;
                case UnitCommandKind.Stop:
                    currentTarget = null;
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
            QueueMoveClick(context.control);
        }

        private void OnAttackMove(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            ArmAttackMove();
        }

        private void OnConfirm(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!attackMoveState.IsArmed)
            {
                return;
            }

            attackMoveState.Confirm();
            if (TryRaycast(out RaycastHit hit)
                && hit.collider.gameObject.layer == LayerMask.NameToLayer("Targetable"))
            {
                Issue(UnitCommand.Attack(hit.collider.gameObject));
                return;
            }

            Issue(UnitCommand.AttackNearestInRange());
        }

        private void OnStop(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            Issue(UnitCommand.Stop());
        }

        private void OnCancel(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!attackMoveState.IsArmed)
            {
                return;
            }

            attackMoveState.Cancel();
            consumedCancelControl = context.control;
            consumedCancelFrame = Time.frameCount;
            if (hasPendingMoveClick && pendingMoveControl == context.control)
            {
                hasPendingMoveClick = false;
            }
        }

        private void Update()
        {
            if (!hasPendingMoveClick)
            {
                return;
            }

            bool wasArmed = pendingMoveClickWasArmed;
            hasPendingMoveClick = false;
            pendingMoveControl = null;
            if (wasArmed)
            {
                attackMoveState.Cancel();
                return;
            }

            HandleMoveClick();
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

            Ray ray = mainCamera.ScreenPointToRay(input.PointerPosition.ReadValue<Vector2>());
            return Physics.Raycast(ray, out hit, Mathf.Infinity, Physics.DefaultRaycastLayers);
        }

        private void QueueMoveClick(InputControl control)
        {
            if (consumedCancelFrame == Time.frameCount && consumedCancelControl == control)
            {
                return;
            }

            hasPendingMoveClick = true;
            pendingMoveClickWasArmed = attackMoveState.IsArmed;
            pendingMoveControl = control;
        }
    }
}
