using UnityEngine;

[CreateAssetMenu(menuName = "Characters/Exusiai/Motion Settings")]
public sealed class ExusiaiMotionSettings : ScriptableObject
{
    [Min(0.1f)] public float jogSpeed = 2.4f;
    [Min(0.05f)] public float raiseSeconds = 0.22f;
    [Min(0.05f)] public float lowerSeconds = 0.3f;
    [Min(0.1f)] public float aimHoldSeconds = 0.55f;
    [Min(0.1f)] public float turnSpeed = 540f;
}
