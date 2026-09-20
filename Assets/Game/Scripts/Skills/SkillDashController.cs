using System;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Movement;
using UnityEngine;

namespace ArknightsFrontline.Skills
{
    public sealed class SkillDashController : MonoBehaviour
    {
        private const float DashSpeed = 14f;
        private const float MaximumDistance = 7f;
        private const float ObstacleClearance = 0.25f;
        private const float DestinationTolerance = 0.001f;

        private UnitMotor motor;
        private ArenaLayout layout;
        private int obstacleMask;

        public bool IsDashing { get; private set; }

        public Vector3 Destination { get; private set; }

        public event Action DashCompleted;

        public void Configure(UnitMotor unitMotor, ArenaLayout arenaLayout, int obstacles)
        {
            motor = unitMotor;
            layout = arenaLayout;
            obstacleMask = obstacles;
            IsDashing = false;
            Destination = motor == null
                ? Vector3.zero
                : new Vector3(motor.transform.position.x, 0f, motor.transform.position.z);
        }

        public bool TryStart(Vector3 clickedPoint)
        {
            if (motor == null
                || !DashPathResolver.TryResolve(
                    motor.transform.position,
                    clickedPoint,
                    MaximumDistance,
                    layout,
                    obstacleMask,
                    ObstacleClearance,
                    out Vector3 endpoint))
            {
                return false;
            }

            motor.Stop();
            Destination = endpoint;
            IsDashing = true;
            return true;
        }

        public bool Preview(Vector3 clickedPoint, out Vector3 endpoint)
        {
            if (motor == null)
            {
                endpoint = Vector3.zero;
                return false;
            }

            return DashPathResolver.TryResolve(
                motor.transform.position,
                clickedPoint,
                MaximumDistance,
                layout,
                obstacleMask,
                ObstacleClearance,
                out endpoint);
        }

        public void Tick(float deltaTime)
        {
            if (!IsDashing)
            {
                return;
            }

            if (motor == null)
            {
                IsDashing = false;
                return;
            }

            Vector3 currentPosition = motor.transform.position;
            Vector3 targetPosition = new Vector3(Destination.x, currentPosition.y, Destination.z);
            motor.transform.position = Vector3.MoveTowards(
                currentPosition,
                targetPosition,
                DashSpeed * Mathf.Max(0f, deltaTime));

            if (HorizontalDistance(motor.transform.position, Destination) > DestinationTolerance)
            {
                return;
            }

            IsDashing = false;
            DashCompleted?.Invoke();
        }

        public void Cancel()
        {
            IsDashing = false;
            if (motor != null)
            {
                motor.Stop();
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            return Vector2.Distance(
                new Vector2(first.x, first.z),
                new Vector2(second.x, second.z));
        }
    }
}
