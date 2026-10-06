using UnityEngine;

namespace ArknightsFrontline.Combat
{
    [DisallowMultipleComponent]
    public sealed class ProjectileSpawnPoint : MonoBehaviour
    {
        [SerializeField] private Transform muzzle;
        public Transform Muzzle => muzzle;
        public void Configure(Transform target) => muzzle = target;
        public Vector3 ResolvePosition()
        {
            if (!muzzle) return transform.position;
            Vector3 point = muzzle.position;
            return float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z) ? point : transform.position;
        }
    }
}
