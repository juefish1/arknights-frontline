using UnityEngine;

namespace ArknightsFrontline.Arena
{
    public sealed class ArenaBootstrap : MonoBehaviour
    {
        [SerializeField] private Transform blueTower;
        [SerializeField] private Transform redTower;

        public Transform BlueTower => blueTower;
        public Transform RedTower => redTower;

        public void AssignTowers(Transform blue, Transform red)
        {
            blueTower = blue;
            redTower = red;
        }

        private void Awake()
        {
            if (blueTower == null)
            {
                throw new MissingReferenceException(
                    $"{gameObject.name}: {nameof(ArenaBootstrap)}.{nameof(BlueTower)} is not assigned.");
            }

            if (redTower == null)
            {
                throw new MissingReferenceException(
                    $"{gameObject.name}: {nameof(ArenaBootstrap)}.{nameof(RedTower)} is not assigned.");
            }
        }
    }
}
