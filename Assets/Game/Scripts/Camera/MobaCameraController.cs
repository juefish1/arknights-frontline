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

        private readonly ArenaLayout layout = ArenaLayout.CreateDefault();
        private readonly Vector3 cameraOffset = new Vector3(0f, 35f, -28f);

        [SerializeField] private Transform centeringTarget;
        private Vector3 focusPosition;

        public Transform CenteringTarget => centeringTarget;

        private void Awake()
        {
            focusPosition = layout.Clamp(transform.position - cameraOffset);
            ApplyFocus();
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
                new Vector2(Screen.width, Screen.height));
            focusPosition = layout.Clamp(focusPosition + edgeMovement * Time.deltaTime);

            if (mouse.middleButton.isPressed)
            {
                Vector2 dragDelta = mouse.delta.ReadValue();
                focusPosition = layout.Clamp(
                    focusPosition - new Vector3(dragDelta.x, 0f, dragDelta.y) * DragWorldUnitsPerPixel);
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

        public static Vector3 CalculateEdgePanVelocity(Vector2 pointerPosition, Vector2 screenSize)
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

            return movement == Vector3.zero ? Vector3.zero : movement.normalized * MovementSpeed;
        }

        private void ApplyFocus()
        {
            transform.position = focusPosition + cameraOffset;
        }
    }
}
