using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExusiaiMotionPlayCheck
{
    private static int lastFrame = -1;
    private static bool clockConfigured;
    private static float maxHandMovement, maxFootMovement;
    private static Vector3 initialHand, initialFoot;
    private static bool captured;
    private static int samples, shootingSamples, recoilRestarts;
    private static float previousRecoilTime;
    private static float gripError, maximumPhysicalAngle, movingShotFootMotion;
    private static Vector3 movingShotFirstFoot;
    private static bool sawHalfSpeed, sawPrepared, sawIdleRate, sawMovingShot;
    private static Transform[] physicalBones;
    private static Quaternion[] restPhysicalRotations;
    public static void Start()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/Characters/Exusiai/Model" + "/Scenes/Preview_Game_Humanoid.unity");
        var driver = UnityEngine.Object.FindFirstObjectByType<ExusiaiPreviewDriver>();
        var serialized = new SerializedObject(driver); serialized.FindProperty("jogSpeed").floatValue = 1.2f; serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, "Assets/Motion_PlayTest.unity");
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            // Real Play with deterministic game timesteps avoids machine-dependent
            // tiny/huge Editor frame deltas and duplicate observations of one frame.
            if (!clockConfigured)
            {
                Time.captureFramerate = 60; clockConfigured = true;
                lastFrame = Time.frameCount; return;
            }
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            ExusiaiMotionAcceptance.Require(Mathf.Abs(Time.deltaTime - 1f / 60) < 0.00001f, "Play timestep was not 60 FPS");
            var animator = UnityEngine.Object.FindFirstObjectByType<Animator>();
            var hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (!captured)
            {
                captured = true; initialHand = hand.position; initialFoot = foot.position;
                physicalBones = Array.FindAll(animator.GetComponentsInChildren<Transform>(), t => t.name.Contains("Hair_") || t.name.StartsWith("SkirtHem_"));
                restPhysicalRotations = Array.ConvertAll(physicalBones, t => t.localRotation);
            }
            if (Time.time > 3.9f && Time.time < 5.5f)
            {
                sawHalfSpeed = true;
                ExusiaiMotionAcceptance.Require(animator.GetFloat("MoveSpeed") > 0.99f && Mathf.Abs(animator.GetFloat("JogRate") - 0.5f) < 0.01f,
                    "Half speed must use a full jog pose at half rate, not halve the stride twice; blend=" + animator.GetFloat("MoveSpeed") + ", rate=" + animator.GetFloat("JogRate"));
            }
            if (Time.time > 6.93f && Time.time < 6.98f)
            {
                sawPrepared = true;
                ExusiaiMotionAcceptance.Require(animator.GetLayerWeight(1) > 0.99f, "Gun was not prepared before the first shot");
            }
            if (Time.time > 0.3f && Time.time < 2)
            {
                sawIdleRate = true;
                ExusiaiMotionAcceptance.Require(Mathf.Abs(animator.GetFloat("JogRate") - 1) < 0.01f, "Idle breathing incorrectly uses jog speed scaling");
            }
            if (Time.time > 13.8f && Time.time < 15.6f && animator.GetLayerWeight(1) > 0.99f)
            {
                if (!sawMovingShot) { movingShotFirstFoot = foot.position; sawMovingShot = true; }
                movingShotFootMotion = Mathf.Max(movingShotFootMotion, Vector3.Distance(movingShotFirstFoot, foot.position));
            }
            for (int i = 0; i < physicalBones.Length; i++)
            {
                ExusiaiMotionAcceptance.Require(float.IsFinite(physicalBones[i].position.sqrMagnitude), "Physics produced a nonfinite pose during Play");
                maximumPhysicalAngle = Mathf.Max(maximumPhysicalAngle, Quaternion.Angle(restPhysicalRotations[i], physicalBones[i].localRotation));
            }
            maxHandMovement = Mathf.Max(maxHandMovement, Vector3.Distance(initialHand, hand.position));
            maxFootMovement = Mathf.Max(maxFootMovement, Vector3.Distance(initialFoot, foot.position));
            ExusiaiMotionAcceptance.Require(float.IsFinite(hand.position.sqrMagnitude), "Play produced nonfinite pose");
            if (animator.GetLayerWeight(1) > 0.99f)
            {
                var support = Array.Find(animator.GetComponentsInChildren<Transform>(), t => t.name == "SupportGrip");
                gripError = Mathf.Max(gripError, Vector3.Distance(support.position, animator.GetBoneTransform(HumanBodyBones.RightHand).position));
                float recoilTime = animator.GetCurrentAnimatorStateInfo(2).normalizedTime;
                if (recoilTime + 0.05f < previousRecoilTime) recoilRestarts++;
                previousRecoilTime = recoilTime;
                shootingSamples++;
            }
            samples++;
            if (Time.time < 18) return;
            ExusiaiMotionAcceptance.Require(maxHandMovement > 0.15f && maxFootMovement > 0.1f, "Controller did not play the three motions");
            ExusiaiMotionAcceptance.Require(shootingSamples > 10 && gripError < 0.045f, "Actual controller grip failed: " + gripError);
            ExusiaiMotionAcceptance.Require(physicalBones.Length == 78 && maximumPhysicalAngle > 0.5f && maximumPhysicalAngle < 85,
                "Actual Play secondary response missing or flipped: " + maximumPhysicalAngle);
            ExusiaiMotionAcceptance.Require(recoilRestarts > 5, "Repeated shots did not restart recoil");
            ExusiaiMotionAcceptance.Require(sawHalfSpeed && sawPrepared && sawIdleRate, "Required speed/preparation windows were not sampled");
            ExusiaiMotionAcceptance.Require(sawMovingShot && movingShotFootMotion > 0.1f, "Shooting froze moving legs");
            string result = "passed=true\nsamples=" + samples + "\nmax_hand_m=" + maxHandMovement.ToString("R") +
                "\nmax_foot_m=" + maxFootMovement.ToString("R") + "\nshooting_grip_error_m=" + gripError.ToString("R") + "\nphysical_response_degrees=" + maximumPhysicalAngle.ToString("R") + "\nrecoil_restarts=" + recoilRestarts + "\nmoving_shot_foot_motion_m=" + movingShotFootMotion.ToString("R") + "\nhalf_speed_prepared_idle_rate_windows=passed";
            File.WriteAllText("motion-play-validation.txt", result);
            Debug.Log("EXUSIAI_MOTION_PLAY_OK\n" + result);
            Time.captureFramerate = 0; EditorApplication.update -= Tick; EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); Time.captureFramerate = 0; EditorApplication.update -= Tick; EditorApplication.Exit(1); }
    }
}
