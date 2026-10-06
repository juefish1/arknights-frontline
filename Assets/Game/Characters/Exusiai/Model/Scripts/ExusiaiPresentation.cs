using System;
using UnityEngine;

// This component accepts movement/attack facts. It never produces gameplay events.
[DefaultExecutionOrder(100)]
public sealed class ExusiaiPresentation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private ExusiaiMotionSettings settings;
    private float speed, aimWeight, hold;
    private bool pendingShot, requestedAim, pendingPoseReset;
    private Quaternion targetRotation;
    public event Action ResetRequested;

    public void Configure(Animator target, ExusiaiMotionSettings profile)
    {
        animator = target;
        settings = profile;
        targetRotation = animator.transform.rotation;
    }

    public void SetMoveSpeed(float metersPerSecond)
    {
        if (float.IsNaN(metersPerSecond) || float.IsInfinity(metersPerSecond)) return;
        speed = Mathf.Max(0, metersPerSecond);
    }

    public void SetAimDirection(Vector3 worldDirection)
    {
        worldDirection.y = 0;
        if (!float.IsFinite(worldDirection.sqrMagnitude) || worldDirection.sqrMagnitude < 0.000001f) return;
        targetRotation = Quaternion.LookRotation(worldDirection);
    }

    // Set this during attack windup so real shot events do not wait for the raise.
    public void SetAiming(bool value) => requestedAim = value;

    public void PlayShot()
    {
        if (!settings) return;
        hold = settings.aimHoldSeconds;
        pendingShot = true;
    }

    public void ResetPresentation()
    {
        speed = aimWeight = hold = 0;
        pendingShot = requestedAim = false;
        if (animator)
        {
            pendingPoseReset = !animator.isActiveAndEnabled;
            if (!pendingPoseReset)
            {
                animator.Rebind();
                animator.Update(0);
            }
            targetRotation = animator.transform.rotation;
        }
        ResetRequested?.Invoke();
    }

    private void OnEnable()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator)
        {
            if (pendingPoseReset) ResetPresentation();
            else targetRotation = animator.transform.rotation;
        }
    }

    private void Update()
    {
        if (!animator || !animator.isActiveAndEnabled || !settings || Time.deltaTime <= 0) return;
        if (pendingPoseReset) ResetPresentation();
        float dt = Time.deltaTime;
        animator.SetFloat("MoveSpeed", speed > 0.01f ? 1 : 0, 0.12f, dt);
        animator.SetFloat("JogRate", speed > 0.01f ? Mathf.Clamp(speed / Mathf.Max(0.01f, settings.jogSpeed), 0.01f, 3) : 1);
        bool aiming = requestedAim || hold > 0 || pendingShot;
        aimWeight = Mathf.MoveTowards(aimWeight, aiming ? 1 : 0,
            dt / Mathf.Max(0.01f, aiming ? settings.raiseSeconds : settings.lowerSeconds));
        animator.SetLayerWeight(1, aimWeight);
        animator.SetLayerWeight(2, aimWeight);
        if (aimWeight >= 0.99f && pendingShot)
        {
            animator.Play("Recoil", 2, 0);
            pendingShot = false;
        }
        // Keep enough time for a full recoil and recovery even when fire stops immediately.
        if (!pendingShot) hold = Mathf.Max(0, hold - dt);
        animator.transform.rotation = Quaternion.RotateTowards(animator.transform.rotation,
            targetRotation, settings.turnSpeed * dt);
    }
}
