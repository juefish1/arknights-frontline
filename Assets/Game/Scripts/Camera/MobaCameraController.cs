using System;
using ArknightsFrontline.Arena;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsFrontline.Camera
{
    public sealed class MobaCameraController : MonoBehaviour
    {
        private const float EdgeThresholdPixels = 20f;
        private const float MovementSpeed = 18f;
        private const float DragWorldUnitsPerPixel = 0.03f;
        public static readonly Vector3 DefaultOffset = new Vector3(-14f, 28f, -14f);
        public const float DefaultFieldOfView = 45f;

        private readonly ArenaLayout layout = ArenaLayout.CreateDefault();
        [SerializeField] private Vector3 cameraOffset = new Vector3(-14f, 28f, -14f);

        [SerializeField] private Transform centeringTarget;
        private Vector3 focusPosition;

        public Transform CenteringTarget => centeringTarget;

        private void Awake()
        {
            focusPosition = layout.Clamp(transform.position - cameraOffset);
            ApplyFocus();
        }

        private void Start()
        {
            if (centeringTarget != null)
            {
                CenterOn(centeringTarget);
            }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 pointerPosition = mouse.position.ReadValue();
            Vector3 edgeMovement = CalculateEdgePanVelocity(
                pointerPosition,
                new Vector2(Screen.width, Screen.height),
                transform.eulerAngles.y);
            focusPosition = layout.Clamp(focusPosition + edgeMovement * Time.deltaTime);

            if (mouse.middleButton.isPressed)
            {
                Vector2 dragDelta = mouse.delta.ReadValue();
                focusPosition = layout.Clamp(
                    focusPosition - ScreenToGround(dragDelta, transform.eulerAngles.y) * DragWorldUnitsPerPixel);
            }

            ApplyFocus();
        }

        public void CenterOn(Transform target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            focusPosition = layout.Clamp(target.position);
            ApplyFocus();
        }

        public void SetCenteringTarget(Transform target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            centeringTarget = target;
        }

        public void ConfigureOffset(Vector3 offset)
        {
            cameraOffset = offset;
            focusPosition = layout.Clamp(transform.position - cameraOffset);
            ApplyFocus();
        }

        public static Vector3 CalculateEdgePanVelocity(Vector2 pointerPosition, Vector2 screenSize, float cameraYaw = 45f)
        {
            Vector3 movement = Vector3.zero;
            if (pointerPosition.x <= EdgeThresholdPixels)
            {
                movement.x -= MovementSpeed;
            }
            else if (pointerPosition.x >= screenSize.x - EdgeThresholdPixels)
            {
                movement.x += MovementSpeed;
            }

            if (pointerPosition.y <= EdgeThresholdPixels)
            {
                movement.z -= MovementSpeed;
            }
            else if (pointerPosition.y >= screenSize.y - EdgeThresholdPixels)
            {
                movement.z += MovementSpeed;
            }

            return movement == Vector3.zero
                ? Vector3.zero
                : ScreenToGround(new Vector2(movement.x, movement.z), cameraYaw).normalized * MovementSpeed;
        }

        private static Vector3 ScreenToGround(Vector2 movement, float cameraYaw)
        {
            return Quaternion.Euler(0f, cameraYaw, 0f) * new Vector3(movement.x, 0f, movement.y);
        }

        private void ApplyFocus()
        {
            transform.position = focusPosition + cameraOffset;
            transform.rotation = Quaternion.LookRotation(-cameraOffset, Vector3.up);
        }
    }
}
