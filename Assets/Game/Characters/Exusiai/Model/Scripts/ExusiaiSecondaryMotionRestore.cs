using UnityEngine;

// Animator must evaluate after restoring last frame's secondary override.
[DefaultExecutionOrder(-2000)]
public sealed class ExusiaiSecondaryMotionRestore : MonoBehaviour
{
    [SerializeField] private ExusiaiSecondaryMotion motion;
    public void Configure(ExusiaiSecondaryMotion target) => motion = target;
    private void Update() { if (motion && motion.enabled) motion.RestorePose(); }
}
