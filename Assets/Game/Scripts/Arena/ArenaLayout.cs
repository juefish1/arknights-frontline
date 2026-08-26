using UnityEngine;

namespace ArknightsFrontline.Arena
{
    public readonly struct ArenaLayout
    {
        public Vector3 BlueTower { get; }
        public Vector3 RedTower { get; }
        public Vector3 BlueDeployment { get; }
        public Vector3 RedDeployment { get; }
        public float LaneHalfLength { get; }
        public float LaneHalfWidth { get; }

        private ArenaLayout(
            Vector3 blueTower,
            Vector3 redTower,
            Vector3 blueDeployment,
            Vector3 redDeployment,
            float laneHalfLength,
            float laneHalfWidth)
        {
            BlueTower = blueTower;
            RedTower = redTower;
            BlueDeployment = blueDeployment;
            RedDeployment = redDeployment;
            LaneHalfLength = laneHalfLength;
            LaneHalfWidth = laneHalfWidth;
        }

        public static ArenaLayout CreateDefault()
        {
            return new ArenaLayout(
                new Vector3(-38f, 0f, 0f),
                new Vector3(38f, 0f, 0f),
                new Vector3(-46f, 0f, 0f),
                new Vector3(46f, 0f, 0f),
                50f,
                12f);
        }

        public Vector3 Clamp(Vector3 point)
        {
            return new Vector3(
                Mathf.Clamp(point.x, -LaneHalfLength, LaneHalfLength),
                0f,
                Mathf.Clamp(point.z, -LaneHalfWidth, LaneHalfWidth));
        }
    }
}
