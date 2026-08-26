using ArknightsFrontline.Arena;
using UnityEngine;

namespace ArknightsFrontline.Movement
{
    public sealed class UnitMotor : MonoBehaviour
    {
        private const float DestinationTolerance = 0.001f;

        private ArenaLayout layout;
        private Vector3 destination;
        [SerializeField] private float movementSpeed = 5f;

        public bool IsMoving { get; private set; }

        private void Awake()
        {
            layout = ArenaLayout.CreateDefault();
        }

        public void Configure(float speed, ArenaLayout arenaLayout)
        {
            movementSpeed = speed;
            layout = arenaLayout;
            destination = layout.Clamp(transform.position);
            IsMoving = false;
        }

        public void SetDestination(Vector3 point)
        {
            destination = layout.Clamp(point);
            IsMoving = HorizontalDistance(transform.position, destination) > DestinationTolerance;
        }

        public void Stop()
        {
            IsMoving = false;
        }

        public void Tick(float deltaTime)
        {
            if (!IsMoving)
            {
                return;
            }

            Vector3 currentPosition = transform.position;
            Vector3 targetPosition = new Vector3(destination.x, currentPosition.y, destination.z);
            transform.position = Vector3.MoveTowards(currentPosition, targetPosition, movementSpeed * deltaTime);
            IsMoving = HorizontalDistance(transform.position, destination) > DestinationTolerance;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private static float HorizontalDistance(Vector3 position, Vector3 target)
        {
            return Vector2.Distance(
                new Vector2(position.x, position.z),
                new Vector2(target.x, target.z));
        }
    }
}
