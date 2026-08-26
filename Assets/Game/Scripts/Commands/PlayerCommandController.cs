using ArknightsFrontline.Arena;
using ArknightsFrontline.Camera;
using ArknightsFrontline.Combat;
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
        private bool isAttackMoveHeld;

        public GameObject CurrentTarget => currentTarget;

        public UnitCommand? CurrentCommand { get; private set; }

        public bool IsAttackMoveArmed => attackMoveState.IsArmed;

        public bool IsAttackMoveHeld => isAttackMoveHeld;

        public InputActionAsset InputActions => input.Asset;

        private void Awake()
        {
            motor = GetComponent<UnitMotor>();
            input = new GameInputActions();
            InputBindingStore.Load(input.Asset);
            input.MoveClick.performed += OnMoveClick;
            input.AttackMove.performed += OnAttackMove;
            input.AttackMove.canceled += OnAttackMoveCanceled;
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
            isAttackMoveHeld = false;
            attackMoveState.Cancel();
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
            input.AttackMove.canceled -= OnAttackMoveCanceled;
            input.Confirm.performed -= OnConfirm;
            input.Stop.performed -= OnStop;
            input.Cancel.performed -= OnCancel;
            input.CenterCamera.performed -= OnCenterCamera;
            input.Dispose();
        }

        public void Issue(UnitCommand command)
        {
            CombatUnit combatUnit = GetComponent<CombatUnit>();
            if (combatUnit != null && combatUnit.IsDead)
            {
                return;
            }

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
                CancelAttackMove();
                return;
            }

            if (!TryGetPointerHit(out RaycastHit hit))
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
            isAttackMoveHeld = true;
            ArmAttackMove();
        }

        private void OnAttackMoveCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            CancelAttackMove();
        }

        private void OnConfirm(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!IsAttackMoveArmed || !IsAttackMoveHeld)
            {
                return;
            }

            attackMoveState.Confirm();
            if (TryGetPointerHit(out RaycastHit hit)
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

            CancelAttackMove();
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
                CancelAttackMove();
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

        public bool TryGetPointerHit(out RaycastHit hit)
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

        private void CancelAttackMove()
        {
            isAttackMoveHeld = false;
            attackMoveState.Cancel();
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
