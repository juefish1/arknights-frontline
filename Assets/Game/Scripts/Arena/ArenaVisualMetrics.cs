using UnityEngine;

namespace ArknightsFrontline.Arena
{
    // Unity primitive sizes: capsule height 2, sphere diameter 1, plane side 10.
    public static class ArenaVisualMetrics
    {
        public const float OperatorCenterHeight = 1.2f;
        public static readonly Vector3 OperatorScale = Vector3.one * 1.2f;
        public static readonly Vector3 GroundMinionScale = new Vector3(0.75f, 0.65f, 0.75f);
        public const float GroundMinionCenterHeight = 0.65f;
        public static readonly Vector3 AirMinionScale = Vector3.one * 0.85f;
        public const float AirMinionCenterHeight = 1.5f;
        public static readonly Vector3 TowerSize = new Vector3(3f, 5f, 3f);
        public static readonly Vector3 TowerCenter = new Vector3(0f, 2.5f, 0f);
    }
}
