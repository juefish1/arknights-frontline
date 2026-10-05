using UnityEngine;

[CreateAssetMenu(menuName = "Characters/Exusiai/Secondary Motion")]
public sealed class ExusiaiSecondaryMotionProfile : ScriptableObject
{
    [System.Serializable] public struct Spring
    {
        public float stiffness, damping, inertia, radius, maxAngle;
        public Spring(float stiffness, float damping, float inertia, float radius, float maxAngle)
        { this.stiffness = stiffness; this.damping = damping; this.inertia = inertia; this.radius = radius; this.maxAngle = maxAngle; }
    }
    public Spring hair = new Spring(110, 14, 0.55f, 0.008f, 10);
    public Spring skirt = new Spring(150, 18, 0.45f, 0.009f, 32);
    public float fixedStep = 1f / 120;
    public int maxSubsteps = 12;
    public float teleportDistance = 0.7f, teleportAngle = 90;
}
