# Exusiai Formal Animation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking; this is the executed plan with final verified sources. Execute inline in the current conversation, as requested by the user.

**Goal:** Deliver editable single-hand idle/jog, two-hand shooting, an independent presentation component and an interactive Unity preview.

**Architecture:** Author world-space hand and ankle targets on the existing Humanoid rig, solve two-joint limbs, capture HumanPose muscle curves and complete body root curves. A locomotion blend tree and masked upper-body aim/additive recoil layers keep shooting independent of the legs. All importing, Play and rendering checks run in the existing temporary Unity project before selective delivery to the model branch.

**Tech Stack:** Unity 6000.3.25f1, URP 17.3, HumanPoseHandler, AnimationClip, AnimatorController, PlayableGraph, Python 3 for selective file delivery.

## Global Constraints

- Work in the existing `model` branch; preserve the current uncommitted changes. User requested modeling in this branch; do not create another branch or worktree.
- Unity 6000.3.25f1，URP 17.3。
- 待机和普通移动均为单手持枪，射击时切换为双手。
- 沿用待机的左手握持并扣扳机，右手支撑。
- 动画原地播放，位移由游戏移动系统控制。
- 主 FBX、材质、贴图、骨骼层级与权重不变。
- ExusiaiHumanoidBoneSync 执行顺序 1000；其 .meta/GUID 保持。
- SetMoveSpeed(float metersPerSecond), SetAimDirection(Vector3 worldDirection), SetAiming(bool value), PlayShot(), ResetPresentation() are presentation inputs and produce no gameplay events.
- Physical secondary motion is a separate implementation group B; the combined delivery gate C runs after B. This plan produces a usable preview without physics.
- No new paid dependencies. Test weapon is replaceable and clearly labeled. No Git commit, staging, publishing or branch merge is needed for this checkpoint.

## File Map

Runtime scripts under `Assets/Game/Characters/Exusiai/Model/Scripts/`: `ArknightsFrontline.Model.Runtime.asmdef`, `ExusiaiMotionSettings.cs`, `ExusiaiPresentation.cs`, `ExusiaiPreviewDriver.cs`. Existing bone sync joins this independent auto-referenced assembly without changing its source or GUID.

Authoring under `ArtSource/Exusiai/Animation/Tools/`: `ExusiaiMotionBake.cs`, `ExusiaiSecondaryBake.cs`, `ExusiaiMotionRender.cs`, `ExusiaiMotionSequenceCapture.cs`; import only these four into the isolated Assets/Editor. Executable gates are delivered under `Model/Tests/EditMode/` and `Model/Tests/PlayMode/` in independent Editor assemblies (definitions below). Tools also preserves gate source backups, which must not be compiled a second time. These are batch integration checks, not NUnit Test Runner tests.

Editable source: `ArtSource/Exusiai/Animation/key-poses.json`. Generated clips/settings/controller/mask/test material under `Assets/Game/Characters/Exusiai/Model/Animations/`. Generated prefab/scene: `Prefabs/Exusiai_Game_Humanoid.prefab`, `Scenes/Preview_Game_Humanoid.unity`.

Temporary project: `/private/tmp/exusiai-playback-check`; Unity executable: `/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity`.

## Task 1: Prove the new resource gate fails before authoring

**Files:** Initially author the red gate under ArtSource/Tools; final executable copy is `Model/Tests/EditMode/ExusiaiMotionAcceptance.cs` with its Editor asmdef.
**Interfaces:** Consumes existing Humanoid prefab/Avatar. Produces `ExusiaiMotionAcceptance.Run()` and `Require(bool condition, string reason)`.

- [x] Write this acceptance gate; its hand grip, muzzle, mesh and loop checks inspect real sampled skin/bones. Missing new prefab must fail first.

```csharp
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Integration gates use the delivered assets, not a mocked Animator.
public static class ExusiaiMotionAcceptance
{
    private const string Root = "Assets/Game/Characters/Exusiai/Model";
    public static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
    public static void Run()
    {
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Exusiai_Game_Humanoid.prefab");
            Require(prefab, "Missing formal animation prefab");
            var go = UnityEngine.Object.Instantiate(prefab);
            var animator = go.GetComponentInChildren<Animator>();
            var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
            Require(animator.avatar && animator.avatar.isHuman && animator.avatar.isValid, "Invalid humanoid avatar");
            Require(skin.bones.Length == 253 && skin.sharedMesh.blendShapeCount == 56, "Lost original deformation data");
            File.WriteAllLines("human-muscle-names.txt", HumanTrait.MuscleName);
            File.WriteAllLines("human-bone-names.txt", HumanTrait.BoneName);
            Require(animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal) && animator.GetBoneTransform(HumanBodyBones.LeftThumbProximal), "Formal finger mapping missing");
            var names = go.GetComponentsInChildren<Transform>().ToDictionary(t => t.name, t => t);
            Require(names["WeaponMount"].parent == animator.GetBoneTransform(HumanBodyBones.LeftHand), "Primary hand changed");
            int blinkIndex = Enumerable.Range(0, skin.sharedMesh.blendShapeCount).First(i => skin.sharedMesh.GetBlendShapeName(i).Contains("まばたき"));
            var report = new System.Text.StringBuilder();
            foreach (string kind in new[] { "Idle", "Jog", "Aim", "Recoil" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Animations/Exusiai_" + kind + "_Humanoid.anim");
                Require(clip && clip.humanMotion, "Missing human animation: " + kind);
                if (kind == "Recoil")
                {
                    var serialized = new SerializedObject(clip);
                    Require(serialized.FindProperty("m_AnimationClipSettings.m_HasAdditiveReferencePose").boolValue,
                        "Recoil lost its additive reference pose during rebake");
                    Require(serialized.FindProperty("m_AnimationClipSettings.m_AdditiveReferencePoseClip").objectReferenceValue ==
                        AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Animations/Exusiai_Aim_Humanoid.anim"),
                        "Recoil additive reference is not the equipped aim pose");
                }
                Require(AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y")) != null,
                    "Missing body root curve");
                var graph = PlayableGraph.Create("Acceptance"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
                var playable = AnimationClipPlayable.Create(graph, clip); playable.SetApplyFootIK(false);
                output.SetSourcePlayable(playable); graph.Play();
                var mesh = new Mesh(); Vector3[] first = null; float displacement = 0, gripError = 0, minY = float.MaxValue, maximumBlink = 0;
                Vector3 firstLeft = default, firstRight = default; float plantStart = 0, plantEnd = 0;
                for (int frame = 0; frame <= 60; frame++)
                {
                    playable.SetTime(clip.length * frame / 60.0); graph.Evaluate(0);
                    animator.GetComponent<ExusiaiHumanoidBoneSync>()?.Synchronize();
                    maximumBlink = Mathf.Max(maximumBlink, skin.GetBlendShapeWeight(blinkIndex));
                    if (kind == "Jog" && frame == 10) plantStart = names["LeftAnkle"].position.z;
                    if (kind == "Jog" && frame == 15) plantEnd = names["LeftAnkle"].position.z;
                    skin.BakeMesh(mesh);
                    var vertices = mesh.vertices;
                    Require(vertices.All(v => float.IsFinite(v.sqrMagnitude)), "Nonfinite mesh in " + kind);
                    if (first == null) { first = vertices; firstLeft = names["LeftWrist"].position; firstRight = names["RightWrist"].position; }
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        displacement = Mathf.Max(displacement, Vector3.Distance(first[v], vertices[v]));
                        minY = Mathf.Min(minY, skin.transform.TransformPoint(vertices[v]).y);
                    }
                    if (frame == 0 && kind == "Aim")
                    {
                        var renderer = names["WeaponMount"].GetComponentInChildren<MeshRenderer>();
                        var gunMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                        var stock = new Vector3(names["PrimaryGrip"].position.x, names["PrimaryGrip"].position.y, renderer.bounds.min.z);
                        float torsoFront = float.NegativeInfinity;
                        var weights = skin.sharedMesh.boneWeights;
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            var w = weights[i];
                            bool torso = (w.weight0 > 0.4f && (skin.bones[w.boneIndex0].name.Contains("Chest") || skin.bones[w.boneIndex0].name == "Spine")) ||
                                (w.weight1 > 0.4f && (skin.bones[w.boneIndex1].name.Contains("Chest") || skin.bones[w.boneIndex1].name == "Spine"));
                            if (!torso) continue;
                            var point = skin.transform.TransformPoint(vertices[i]);
                            if (Mathf.Abs(point.x - stock.x) < 0.04f && Mathf.Abs(point.y - stock.y) < 0.045f)
                                torsoFront = Mathf.Max(torsoFront, point.z);
                        }
                        Require(float.IsFinite(torsoFront), "No torso surface near stock to verify clearance");
                        float clearance = stock.z - torsoFront;
                        report.AppendLine("Aim stock/torso clearance=" + clearance.ToString("R"));
                        Require(clearance > -0.005f, "Gun stock penetrates torso: " + clearance);
                    }
                    if (frame == 0 && (kind == "Idle" || kind == "Aim"))
                    {
                        var pose = new HumanPose();
                        using (var handler = new HumanPoseHandler(animator.avatar, animator.transform)) handler.GetHumanPose(ref pose);
                        bool curled = HumanTrait.MuscleName.Select((name, index) => new { name, index }).Any(m => m.name.Contains("Left") && m.name.Contains("Middle") && pose.muscles[m.index] < -0.2f);
                        var index = Array.IndexOf(HumanTrait.MuscleName, "Left Middle 1 Stretched");
                        var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "LeftHand.Middle.1 Stretched"));
                        Require(curled, "Primary fingers are not curled around the grip; muscle=" + pose.muscles[index] + "; curve=" + curve.Evaluate(0));
                    }
                    if (kind == "Idle" || kind == "Jog")
                    {
                        Require(names["Muzzle"].forward.y < -0.8f, "Single-hand gun not pointing down in " + kind);
                        Require(Vector3.Distance(names["SupportGrip"].position, names["RightWrist"].position) > 0.18f,
                            "Free hand is supporting the gun in " + kind);
                    }
                    else gripError = Mathf.Max(gripError, Vector3.Distance(names["SupportGrip"].position, names["RightWrist"].position));
                    if (frame == 60 && kind != "Recoil")
                    {
                        Require(Vector3.Distance(firstLeft, names["LeftWrist"].position) < 0.004f, "Left arm loop seam in " + kind);
                        Require(Vector3.Distance(firstRight, names["RightWrist"].position) < 0.004f, "Right arm loop seam in " + kind);
                    }
                }
                if (kind == "Idle") Require(maximumBlink > 50, "Idle never blinks");
                Require(minY > -0.035f, "Feet penetrate the floor in " + kind + ": " + minY);
                if (kind == "Aim" || kind == "Recoil") Require(gripError < 0.035f, "Support wrist misses grip: " + gripError);
                if (kind == "Jog")
                {
                    Require(displacement > 0.15f, "Jog has no visible stride");
                    float velocity = (plantEnd - plantStart) / (clip.length * 5 / 60);
                    Require(velocity < -2.1f && velocity > -2.7f, "Foot plant does not match 2.4 m/s movement: " + velocity);
                    report.AppendLine("Jog planted foot velocity=" + velocity.ToString("R"));
                }
                if (kind == "Recoil") Require(displacement > 0.008f, "Shot has no visible recoil");
                report.AppendLine(kind + ": displacement=" + displacement.ToString("R") + ", min_y=" + minY.ToString("R") + ", grip_error=" + gripError.ToString("R"));
                graph.Destroy(); UnityEngine.Object.DestroyImmediate(mesh);
            }
            UnityEngine.Object.DestroyImmediate(go);
            File.WriteAllText("motion-acceptance.txt", report.ToString());
            Debug.Log("EXUSIAI_MOTION_ACCEPTANCE_OK\n" + report);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText("motion-acceptance-error.txt", e.ToString()); EditorApplication.Exit(1); }
    }
}
```

- [x] Run the isolated gate before production implementation:

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /private/tmp/exusiai-playback-check -executeMethod ExusiaiMotionAcceptance.Run -logFile /private/tmp/exusiai-motion-red.log
```

Expected: exit 1, `Missing formal animation prefab`; any compilation error must be corrected before treating this as the red result.

## Task 2: Author curves and compose an independent runtime preview

**Files:** Create the four mapped runtime files, `ArtSource/Exusiai/Animation/Tools/ExusiaiMotionBake.cs`; bake the mapped animation assets, new prefab and scene.
**Interfaces:** Consumes existing Humanoid/8-link bone synchronization and the real gate from Task 1. Produces `ExusiaiMotionSettings`, five presentation inputs, `ExusiaiPreviewDriver.Configure(ExusiaiPresentation target, float speed)`, `ExusiaiMotionBake.BuildAssets()` / batch `Build()` and editable key-poses.json.

- [x] Create the runtime assembly in the temporary project. Its auto reference lets existing editor tools continue to refer to the bone sync class.

```json
{"name":"ArknightsFrontline.Model.Runtime","references":[],"autoReferenced":true}
```

- [x] Write `ExusiaiMotionSettings.cs` using the complete content below.

```csharp
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
```

- [x] Write `ExusiaiPresentation.cs` using the complete content below.

```csharp
using System;
using UnityEngine;

// This component accepts movement/attack facts. It never produces gameplay events.
public sealed class ExusiaiPresentation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private ExusiaiMotionSettings settings;
    private float speed, aimWeight, hold;
    private bool pendingShot, requestedAim;
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
            animator.Rebind();
            animator.Update(0);
            targetRotation = animator.transform.rotation;
        }
        ResetRequested?.Invoke();
    }

    private void OnEnable()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator) targetRotation = animator.transform.rotation;
    }

    private void Update()
    {
        if (!animator || !settings || Time.deltaTime <= 0) return;
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
```

- [x] Write `ExusiaiPreviewDriver.cs` using the complete content below.

```csharp
using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class ExusiaiPreviewDriver : MonoBehaviour
{
    public enum Mode { Sequence, Idle, Jog, Shoot, JogAndShoot }
    public Mode mode;
    [SerializeField] private ExusiaiPresentation presentation;
    [SerializeField] private float jogSpeed = 2.4f;
    private Vector3 origin;
    private float clock, nextShot;
    private Mode previousMode;

    public void Configure(ExusiaiPresentation target, float speed)
    {
        presentation = target;
        jogSpeed = speed;
    }

    private void OnEnable()
    {
        if (!presentation) presentation = GetComponent<ExusiaiPresentation>();
        origin = transform.position;
        clock = nextShot = 0;
        previousMode = mode;
    }

    private void Update()
    {
        if (!presentation || Time.deltaTime <= 0) return;
        if (mode != previousMode)
        {
            clock = nextShot = 0;
            presentation.ResetPresentation();
            transform.position = origin;
            previousMode = mode;
        }
        clock += Time.deltaTime;
        float t = clock % 18;
        if (mode == Mode.Sequence && t < Time.deltaTime && clock > 1)
        {
            transform.position = origin;
            presentation.ResetPresentation();
        }
        bool moving = mode == Mode.Jog || mode == Mode.JogAndShoot ||
            (mode == Mode.Sequence && ((t >= 3 && t < 6) || (t >= 13 && t < 16)));
        bool shooting = mode == Mode.Shoot || mode == Mode.JogAndShoot ||
            (mode == Mode.Sequence && ((t >= 7 && t < 10) || (t >= 13 && t < 16)));
        Vector3 direction = mode == Mode.Sequence && t >= 10 ? Vector3.back : Vector3.forward;
        bool prepared = mode == Mode.Shoot || mode == Mode.JogAndShoot ||
            (mode == Mode.Sequence && ((t >= 6.65f && t < 10) || (t >= 12.65f && t < 16)));
        presentation.SetAiming(prepared);
        presentation.SetMoveSpeed(moving ? jogSpeed : 0);
        presentation.SetAimDirection(direction);
        // The camera previews a treadmill: movement facts drive animation; the user can
        // translate the actor to inspect secondary motion without the actor leaving view.
        if (shooting && clock >= 0.25f && clock >= nextShot)
        {
            presentation.PlayShot();
            nextShot = clock + 0.12f;
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(16, 16, 210, 190), GUI.skin.box);
        GUILayout.Label("Exusiai animation preview");
        foreach (Mode value in System.Enum.GetValues(typeof(Mode)))
            if (GUILayout.Button(value.ToString())) mode = value;
        GUILayout.EndArea();
    }
}
```

- [x] Write `ExusiaiMotionBake.cs` using the complete content below.

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExusiaiMotionBake
{
    public const string Root = "Assets/Game/Characters/Exusiai/Model";
    public const string Output = Root + "/Animations";
    private const string BasePrefab = Root + "/Prefabs/Exusiai_Humanoid.prefab";
    private const string GamePrefab = Root + "/Prefabs/Exusiai_Game_Humanoid.prefab";
    [Serializable] public sealed class Source
    {
        public float idleDuration = 3.6f, jogDuration = 0.72f, shotDuration = 0.3f, jogSpeed = 2.4f;
        public Vector3 idleLeftHand = new Vector3(-0.28f, 0.85f, 0.07f);
        public Vector3 idleRightHand = new Vector3(0.28f, 0.86f, 0.04f);
        public Vector3 aimLeftHand = new Vector3(-0.06f, 1.30f, 0.30f);
        public Vector3 supportGripAdjustment = new Vector3(0.025f, -0.086439176f, -0.131469115f);
        public float jogLift = 0.13f;
        public float jogFreeHandRaise = 0.17f;
        public float jogHipDrop = 0.045f;
        public float jogLeanDegrees = 6;
        public float idleGunPitchDegrees = 88;
        public Vector3 gunPalmOffset = new Vector3(0, -0.018f, 0.035f);
    }
    private static Source source;
    private static GameObject actor;
    private static Animator animator;
    private static HumanPoseHandler handler;
    private static Transform[] bones;
    private static Vector3[] restPositions;
    private static Quaternion[] restRotations;
    private static Dictionary<string, Transform> named;
    private static Vector3 leftFoot, rightFoot;
    private static Quaternion leftFootRotation, rightFootRotation, leftHandRest, rightHandRest;
    private static Vector3 restLeftArmDirection, restRightArmDirection;
    private static Vector3 restHip;
    private static Quaternion gunInHand;
    private static Vector3 supportGripLocal, palmOffsetInGun;
    private const string WeaponPrefab = "Assets/Game/Weapons/ExusiaiVector/Prefabs/Exusiai_Vector.prefab";

    public static void Build()
    {
        try { BuildAssets(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void BuildAssets()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory("ArtSource/Exusiai/Animation");
        string sourcePath = "ArtSource/Exusiai/Animation/key-poses.json";
        source = File.Exists(sourcePath) ? JsonUtility.FromJson<Source>(File.ReadAllText(sourcePath)) : new Source();
        File.WriteAllText(sourcePath, JsonUtility.ToJson(source, true));
        AssetDatabase.Refresh();
        actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab));
        actor.name = "Exusiai_Game_Humanoid";
        animator = actor.GetComponentInChildren<Animator>();
        animator.runtimeAnimatorController = null;
        animator.enabled = false;
        bones = animator.GetComponentsInChildren<Transform>(true);
        named = bones.ToDictionary(t => t.name, t => t);
        restPositions = bones.Select(t => t.localPosition).ToArray();
        restRotations = bones.Select(t => t.localRotation).ToArray();
        // HumanTrait uses spaced finger names; enum-style names were silently ignored.
        var description = animator.avatar.humanDescription;
        var mappings = description.human.Where(h => !new[] { "Thumb", "Index", "Middle", "Ring", "Little" }.Any(f => h.humanName.Contains(f))).ToList();
        foreach (string side in new[] { "Left", "Right" })
            foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                for (int joint = 0; joint < 3; joint++)
                {
                    string enumName = side + finger + new[] { "Proximal", "Intermediate", "Distal" }[joint];
                    string humanName = HumanTrait.BoneName.Single(n => n.Replace(" ", "") == enumName);
                    string boneName = side + finger + (finger == "Thumb" ? joint.ToString() : "Finger" + (joint + 1));
                    mappings.Add(new HumanBone { humanName = humanName, boneName = boneName, limit = new HumanLimit { useDefaultValues = true } });
                }
        description.human = mappings.ToArray();
        var avatarClone = UnityEngine.Object.Instantiate(animator.gameObject);
        avatarClone.name = description.skeleton[0].name;
        var avatar = AvatarBuilder.BuildHumanAvatar(avatarClone, description);
        UnityEngine.Object.DestroyImmediate(avatarClone);
        if (!avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Formal finger avatar invalid");
        avatar.name = "Exusiai_Game_Avatar_Humanoid";
        animator.avatar = Put(avatar, Output + "/Exusiai_Game_Avatar_Humanoid.asset");
        handler = new HumanPoseHandler(animator.avatar, animator.transform);
        leftFoot = named["LeftAnkle"].position;
        rightFoot = named["RightAnkle"].position;
        leftFootRotation = named["LeftAnkle"].rotation;
        rightFootRotation = named["RightAnkle"].rotation;
        leftHandRest = named["LeftWrist"].rotation;
        rightHandRest = named["RightWrist"].rotation;
        restLeftArmDirection = named["LeftWrist"].position - named["LeftElbow"].position;
        restRightArmDirection = named["RightWrist"].position - named["RightElbow"].position;
        restHip = named["Hips"].position;

        var weaponTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefab);
        if (!weaponTemplate || !weaponTemplate.transform.Find("SupportGrip"))
            throw new InvalidOperationException("Approved weapon prefab or support grip missing");
        supportGripLocal = weaponTemplate.transform.Find("SupportGrip").localPosition + source.supportGripAdjustment;
        Quaternion idleGunWorld = Quaternion.Euler(source.idleGunPitchDegrees, 0, 0);
        palmOffsetInGun = Quaternion.Inverse(idleGunWorld) * source.gunPalmOffset;
        Pose("Idle", 0);
        gunInHand = Quaternion.Inverse(named["LeftWrist"].rotation) * idleGunWorld;
        AnimationClip idle = Bake("Idle", source.idleDuration, true);
        AnimationClip jog = Bake("Jog", source.jogDuration, true);
        AnimationClip aim = Bake("Aim", source.idleDuration, true);
        AnimationClip recoil = Bake("Recoil", source.shotDuration, false);
        // Persist the reference as clip settings; native SetAdditiveReferencePose only
        // accepts an initialized runtime cache and is unsuitable for this editor rebake.
        var referenceSettings = new SerializedObject(recoil);
        referenceSettings.FindProperty("m_AnimationClipSettings.m_HasAdditiveReferencePose").boolValue = true;
        referenceSettings.FindProperty("m_AnimationClipSettings.m_AdditiveReferencePoseClip").objectReferenceValue = aim;
        referenceSettings.FindProperty("m_AnimationClipSettings.m_AdditiveReferencePoseTime").floatValue = 0;
        referenceSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recoil);

        var newSettings = ScriptableObject.CreateInstance<ExusiaiMotionSettings>();
        newSettings.name = "Exusiai_MotionSettings";
        var settings = Put(newSettings, Output + "/Exusiai_MotionSettings.asset");
        settings.jogSpeed = source.jogSpeed;
        EditorUtility.SetDirty(settings);
        var mask = new AvatarMask { name = "Exusiai_UpperBody" };
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
            AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
            AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
            mask.SetHumanoidBodyPartActive(part, true);
        mask = Put(mask, Output + "/Exusiai_UpperBody.mask");
        string controllerPath = Output + "/Exusiai_Game_Humanoid.controller";
        var old = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        AnimatorController controller;
        if (old)
        {
            old.layers = Array.Empty<AnimatorControllerLayer>();
            old.parameters = Array.Empty<AnimatorControllerParameter>();
            foreach (var item in AssetDatabase.LoadAllAssetsAtPath(controllerPath))
                if (item && item != old) UnityEngine.Object.DestroyImmediate(item, true);
            old.AddLayer("Base Layer"); controller = old;
        }
        else controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("JogRate", AnimatorControllerParameterType.Float);
        var tree = new BlendTree { name = "Locomotion", blendType = BlendTreeType.Simple1D,
            blendParameter = "MoveSpeed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(idle, 0); tree.AddChild(jog, 1);
        var state = controller.layers[0].stateMachine.AddState("Locomotion");
        state.motion = tree; state.iKOnFeet = false;
        // Locomotion uses the complete jog stride; runtime speed scales its playback rate.
        var children = tree.children; children[1].timeScale = 1; tree.children = children;
        state.speedParameter = "JogRate"; state.speedParameterActive = true;
        controller.AddLayer("Aim"); controller.AddLayer("Recoil");
        var layers = controller.layers;
        layers[1].avatarMask = mask; layers[2].avatarMask = mask;
        layers[2].blendingMode = AnimatorLayerBlendingMode.Additive;
        var aimState = layers[1].stateMachine.AddState("Aim"); aimState.motion = aim; aimState.iKOnFeet = false;
        var shotState = layers[2].stateMachine.AddState("Recoil"); shotState.motion = recoil; shotState.iKOnFeet = false;
        controller.layers = layers;
        animator.runtimeAnimatorController = controller; animator.enabled = true;
        animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var presentation = actor.AddComponent<ExusiaiPresentation>(); presentation.Configure(animator, settings);
        Pose("Idle", 0);
        AttachWeapon();
        PrefabUtility.SaveAsPrefabAsset(actor, GamePrefab);
        UnityEngine.Object.DestroyImmediate(actor);
        handler.Dispose();
        AssetDatabase.SaveAssets();
        var scene = EditorSceneManager.OpenScene(Root + "/Scenes/Preview_Humanoid.unity");
        foreach (var existing in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(existing.transform.root.gameObject);
        var preview = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GamePrefab));
        preview.AddComponent<ExusiaiPreviewDriver>().Configure(preview.GetComponent<ExusiaiPresentation>(), source.jogSpeed);
        EditorSceneManager.SaveScene(scene, Root + "/Scenes/Preview_Game_Humanoid.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("EXUSIAI_MOTION_BUILD_OK");
    }

    private static T Put<T>(T asset, string path) where T : UnityEngine.Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing) { EditorUtility.CopySerialized(asset, existing); UnityEngine.Object.DestroyImmediate(asset); return existing; }
        AssetDatabase.CreateAsset(asset, path); return asset;
    }

    private static void ResetPose()
    {
        for (int i = 0; i < bones.Length; i++)
        { bones[i].localPosition = restPositions[i]; bones[i].localRotation = restRotations[i]; }
    }

    private static void Solve(string prefix, string joint, string tip, Vector3 target, Vector3 pole)
    {
        var a = named[prefix]; var b = named[joint]; var c = named[tip];
        float l1 = Vector3.Distance(a.position, b.position), l2 = Vector3.Distance(b.position, c.position);
        Vector3 delta = target - a.position;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(l1 - l2) + 0.0001f, l1 + l2 - 0.0001f);
        Vector3 direction = delta.normalized;
        float along = (l1 * l1 - l2 * l2 + distance * distance) / (2 * distance);
        Vector3 bend = Vector3.ProjectOnPlane(pole - a.position, direction).normalized;
        Vector3 mid = a.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, l1 * l1 - along * along));
        a.rotation = Quaternion.FromToRotation(b.position - a.position, mid - a.position) * a.rotation;
        b.rotation = Quaternion.FromToRotation(c.position - b.position, target - b.position) * b.rotation;
    }

    private static void Pose(string kind, float time)
    {
        ResetPose();
        bool jogging = kind == "Jog", aiming = kind == "Aim" || kind == "Recoil";
        float phase = 2 * Mathf.PI * time / (jogging ? source.jogDuration : source.idleDuration);
        float breath = Mathf.Sin(phase);
        float recoil = kind == "Recoil" ? (time < 0.045f ? Mathf.Sin(time / 0.045f * Mathf.PI / 2) :
            Mathf.Pow(Mathf.Clamp01(1 - (time - 0.045f) / (source.shotDuration - 0.045f)), 3)) : 0;
        Vector3 hips = restHip;
        hips.y += jogging ? -source.jogHipDrop + 0.009f * Mathf.Cos(phase * 2) : 0.0015f * breath;
        named["Hips"].position = hips;
        named["Spine"].rotation = Quaternion.Euler(jogging ? source.jogLeanDegrees : (aiming ? 2 + recoil * -1.5f : 0),
            jogging ? Mathf.Sin(phase) * 2 : 0, 0) * named["Spine"].rotation;
        named["Chest"].rotation = Quaternion.Euler(0.4f * breath, jogging ? -Mathf.Sin(phase) * 4 : 0, 0) * named["Chest"].rotation;
        const float stanceFraction = 0.4f;
        float stride = source.jogSpeed * source.jogDuration * stanceFraction;
        foreach (var side in new[] { "Left", "Right" })
        {
            Vector3 foot = side == "Left" ? leftFoot : rightFoot;
            if (jogging)
            {
                float p = Mathf.Repeat(time / source.jogDuration + (side == "Right" ? 0.5f : 0), 1);
                if (p < stanceFraction) foot.z += stride * (0.5f - p / stanceFraction);
                else
                {
                    float q = (p - stanceFraction) / (1 - stanceFraction);
                    foot.z += stride * (-0.5f + Mathf.SmoothStep(0, 1, q));
                    foot.y += source.jogLift * Mathf.Sin(Mathf.PI * q);
                }
            }
            Solve(side + "UpperLeg", side + "Knee", side + "Ankle", foot,
                named[side + "UpperLeg"].position + Vector3.forward * 1.5f);
            named[side + "Ankle"].rotation = side == "Left" ? leftFootRotation : rightFootRotation;
        }
        Vector3 left = source.idleLeftHand, right = source.idleRightHand;
        if (jogging)
        {
            left.z += 0.028f * Mathf.Sin(phase); left.y += 0.008f * Mathf.Cos(phase * 2);
            right.z -= 0.13f * Mathf.Sin(phase); right.y += source.jogFreeHandRaise + 0.035f * Mathf.Cos(phase);
        }
        else if (aiming)
        {
            left = source.aimLeftHand + Vector3.back * recoil * 0.022f;
            right = left + Quaternion.Euler(-recoil * 2, 0, 0) * (palmOffsetInGun + supportGripLocal);
        }
        else { left.y += breath * 0.002f; right.y += breath * 0.002f; }
        Solve("LeftUpperArm", "LeftElbow", "LeftWrist", left, new Vector3(-0.5f, 0.95f, -0.16f));
        Solve("RightUpperArm", "RightElbow", "RightWrist", right, jogging ? new Vector3(0.22f, 1.1f, -0.45f) : new Vector3(0.5f, 0.96f, -0.08f));
        named["LeftWrist"].rotation = Quaternion.FromToRotation(restLeftArmDirection,
            named["LeftWrist"].position - named["LeftElbow"].position) * leftHandRest;
        named["RightWrist"].rotation = Quaternion.FromToRotation(restRightArmDirection,
            named["RightWrist"].position - named["RightElbow"].position) * rightHandRest;
        if (aiming)
        {
            Quaternion gunWorld = Quaternion.Euler(-recoil * 2, 0, 0);
            named["LeftWrist"].rotation = gunWorld * Quaternion.Inverse(gunInHand);
            // Right palm faces down onto the fore-end; retain the anatomical local wrist frame.
            named["RightWrist"].rotation = Quaternion.Euler(0, -35, -20) * named["RightWrist"].rotation;
        }
        var pose = new HumanPose(); handler.GetHumanPose(ref pose);
        foreach (string side in new[] { "Left", "Right" })
            foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                for (int n = 1; n <= 3; n++)
                {
                    int index = Array.IndexOf(HumanTrait.MuscleName, side + " " + finger + " " + n + " Stretched");
                    if (index >= 0) pose.muscles[index] = side == "Left" || aiming ? (finger == "Index" ? 0.15f : -0.45f) : 0.25f;
                }
        handler.SetHumanPose(ref pose);
        animator.GetComponent<ExusiaiHumanoidBoneSync>()?.Synchronize();
    }

    private static AnimationClip Bake(string kind, float duration, bool loop)
    {
        int frames = Mathf.RoundToInt(duration * 60);
        var curves = Enumerable.Range(0, HumanTrait.MuscleCount + 7).Select(_ => new List<Keyframe>()).ToArray();
        for (int frame = 0; frame <= frames; frame++)
        {
            float time = duration * frame / frames;
            Pose(kind, loop && frame == frames ? 0 : time);
            var pose = new HumanPose(); handler.GetHumanPose(ref pose);
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string muscle = HumanTrait.MuscleName[i];
                if (muscle.Contains("Stretched") && new[] { "Thumb", "Index", "Middle", "Ring", "Little" }.Any(f => muscle.Contains(f)))
                    pose.muscles[i] = muscle.StartsWith("Left") || kind == "Aim" || kind == "Recoil" ? (muscle.Contains("Index") ? -0.25f : -0.65f) : 0.25f;
            }
            var values = pose.muscles.Concat(new[] { pose.bodyPosition.x, pose.bodyPosition.y, pose.bodyPosition.z,
                pose.bodyRotation.x, pose.bodyRotation.y, pose.bodyRotation.z, pose.bodyRotation.w }).ToArray();
            for (int i = 0; i < values.Length; i++) curves[i].Add(new Keyframe(time, values[i]));
        }
        var names = HumanTrait.MuscleName.Concat(new[] { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" }).ToArray();
        var clip = new AnimationClip { name = "Exusiai_" + kind + "_Humanoid", frameRate = 60 };
        for (int i = 0; i < names.Length; i++)
        {
            var keys = curves[i].ToArray();
            for (int k = 0; k < keys.Length; k++)
            {
                int prev = k == 0 ? (loop ? frames - 1 : 0) : k - 1;
                int next = k == frames ? (loop ? 1 : frames) : k + 1;
                float dt = k == 0 || k == frames ? duration / frames * (loop ? 2 : 1) : keys[next].time - keys[prev].time;
                float tangent = (keys[next].value - keys[prev].value) / dt;
                keys[k].inTangent = keys[k].outTangent = tangent;
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), CurveName(names[i])), new AnimationCurve(keys));
        }
        var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>();
        int blink = Enumerable.Range(0, skin.sharedMesh.blendShapeCount).First(i => skin.sharedMesh.GetBlendShapeName(i).Contains("まばたき"));
        var blinkCurve = kind == "Idle" || kind == "Aim" ? new AnimationCurve(new Keyframe(0, 0), new Keyframe(2.40f, 0),
            new Keyframe(2.48f, 100), new Keyframe(2.63f, 0), new Keyframe(duration, 0)) : AnimationCurve.Constant(0, duration, 0);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(skin.transform, animator.transform),
            typeof(SkinnedMeshRenderer), "blendShape." + skin.sharedMesh.GetBlendShapeName(blink)), blinkCurve);
        var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        clip = Put(clip, Output + "/" + clip.name + ".anim");
        if (!clip.humanMotion) throw new InvalidOperationException("Not a humanoid clip: " + kind);
        return clip;
    }

    private static string CurveName(string muscle)
    {
        string[] part = muscle.Split(' ');
        if (part.Length >= 3 && new[] { "Thumb", "Index", "Middle", "Ring", "Little" }.Contains(part[1]))
            return part[0] + "Hand." + part[1] + (part[2] == "Spread" ? " Spread" : "." + part[2] + " Stretched");
        return muscle;
    }

    private static void AttachWeapon()
    {
        var mount = new GameObject("WeaponMount").transform;
        mount.SetParent(named["LeftWrist"], false);
        mount.localRotation = gunInHand;
        mount.localPosition = gunInHand * palmOffsetInGun;
        var weapon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefab));
        weapon.name = "Exusiai_Vector";
        weapon.transform.SetParent(mount, false);
        weapon.transform.Find("SupportGrip").localPosition = supportGripLocal;
    }
}
```

- [x] Run the baker in the temporary project:

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /private/tmp/exusiai-playback-check -executeMethod ExusiaiMotionBake.Build -logFile /private/tmp/exusiai-motion-build.log
```

Expected: exit 0 and `EXUSIAI_MOTION_BUILD_OK`; four humanoid clips, complete RootT/RootQ curves, an in-place locomotion controller with upper-body aim/recoil mask, left-hand test weapon and preview.

- [x] Run Task 1's gate again; expect exit 0, `EXUSIAI_MOTION_ACCEPTANCE_OK`. Fix failing authoring geometry in key-poses.json or the baker and regenerate before continuing. Keep independent grip/ground thresholds unchanged.

## Task 3: Validate the actual controller and inspect all poses

**Files:** Create `ArtSource/Exusiai/Animation/Tools/ExusiaiMotionPlayCheck.cs`, `ExusiaiMotionRender.cs`; reports and PNGs under ArtSource/Exusiai/Animation after delivery.
**Interfaces:** Consumes generated scene, clips, prefab, real acceptance Require method; produces `ExusiaiMotionPlayCheck.Start()` and `ExusiaiMotionRender.Start()`.

- [x] Write `ExusiaiMotionPlayCheck.cs`.

```csharp
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
```

- [x] Write `ExusiaiMotionRender.cs`.

```csharp
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ExusiaiMotionRender
{
    private static int frames;
    public static void Start()
    {
        EditorSceneManager.OpenScene(ExusiaiMotionBake.Root + "/Scenes/Preview_Game_Humanoid.unity");
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/PreviewRenderer.asset");
        if (!data) { data = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(data, "Assets/PreviewRenderer.asset"); }
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/PreviewPipeline.asset");
        if (!pipeline) { pipeline = UniversalRenderPipelineAsset.Create(data); AssetDatabase.CreateAsset(pipeline, "Assets/PreviewPipeline.asset"); }
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (++frames < 50) return;
        EditorApplication.update -= Tick;
        try
        {
            Directory.CreateDirectory("motion-previews");
            var animator = UnityEngine.Object.FindFirstObjectByType<Animator>();
            var skin = animator.GetComponentInChildren<SkinnedMeshRenderer>();
            var cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            cam.fieldOfView = 30; cam.aspect = 900f / 1100;
            foreach (string shot in new[] { "warmup", "idle-front", "idle-side", "idle-back", "jog-front", "jog-side", "jog-back", "aim-front", "aim-side", "aim-back", "recoil-front", "recoil-side", "recoil-back" })
            {
                string kind = shot.StartsWith("jog") ? "Jog" : shot.StartsWith("aim") ? "Aim" : shot.StartsWith("recoil") ? "Recoil" : "Idle";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ExusiaiMotionBake.Output + "/Exusiai_" + kind + "_Humanoid.anim");
                var graph = PlayableGraph.Create("RenderMotion"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip); playable.SetApplyFootIK(false);
                var output = AnimationPlayableOutput.Create(graph, "Render", animator); output.SetSourcePlayable(playable); graph.Play();
                playable.SetTime(kind == "Jog" ? 0.19 : kind == "Recoil" ? 0.04 : 0); graph.Evaluate(0);
                animator.GetComponent<ExusiaiHumanoidBoneSync>()?.Synchronize();
                cam.transform.position = shot.EndsWith("side") ? new Vector3(3.9f, 1.1f, 0) :
                    shot.EndsWith("back") ? new Vector3(0, 1.1f, -4.1f) : new Vector3(0, 1.1f, 4.1f);
                cam.transform.LookAt(new Vector3(0, 0.87f, 0));
                var mesh = new Mesh(); skin.BakeMesh(mesh);
                var display = new GameObject("CapturedSkin"); display.transform.SetParent(skin.transform, false);
                display.AddComponent<MeshFilter>().sharedMesh = mesh; display.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                skin.enabled = false;
                var rt = new RenderTexture(900, 1100, 24); rt.Create();
                var prepare = typeof(RenderPipelineManager).GetMethod("TryPrepareRenderPipeline", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                prepare.Invoke(null, new object[] { GraphicsSettings.defaultRenderPipeline });
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                RenderPipeline.SubmitRenderRequest(cam, request); RenderPipeline.SubmitRenderRequest(cam, request);
                RenderTexture.active = rt;
                var texture = new Texture2D(900, 1100, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 900, 1100), 0, 0); texture.Apply();
                if (shot != "warmup") File.WriteAllBytes("motion-previews/" + shot + ".png", texture.EncodeToPNG());
                RenderTexture.active = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(display); UnityEngine.Object.DestroyImmediate(mesh); skin.enabled = true; graph.Destroy();
            }
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
```

- [x] Run real Play for 18 seconds. Expect visible hand and ankle movement, at least 10 actual aiming samples and support wrist error below 4.5 cm.

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /private/tmp/exusiai-playback-check -executeMethod ExusiaiMotionPlayCheck.Start -logFile /private/tmp/exusiai-motion-play.log
```

- [x] Render front/side/back idle, jog, aim and recoil. This run requires graphics:

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /private/tmp/exusiai-playback-check -executeMethod ExusiaiMotionRender.Start -logFile /private/tmp/exusiai-motion-render.log
```

- [x] View every generated PNG. Reject elbow inversion, knees bending backwards, foot sink, unsupported gun, wrist bends, skirt penetration or abrupt silhouette changes. Adjust source targets/poses and rerun affected checks; do not accept numeric tests as a substitute for visual inspection.
- [x] Inspect transition samples from the actual controller, including idle↔jog, idle↔shot, jog↔shot and repeated shots. Upper body must not freeze legs or accumulate recoil.

## Task 4: Selective delivery and migration notes

**Files:** Deliver only the newly generated animations, runtime files, formal prefab/scene and their metas; preserve all prior 108 asset hashes. Create ArtSource/Exusiai/Animation/README.md and validation files.
**Interfaces:** Consumes the successful isolated results. Produces reviewable files on model, without importing the temporary project's obsolete simplified rig assets.

- [x] Write `ArtSource/Exusiai/Animation/Tools/ExusiaiMotionDelivery.py` using the full source below and run it with Python 3 after final A/B/C checks pass. Its whitelist excludes obsolete RigCheck assets even inside the temporary Animations directory. It verifies both checkouts against the 108-file baseline, copies only new assets/metas, and resolves project/package GUIDs.

```python
"""Selectively deliver the approved equipped motion after isolated Unity acceptance."""
from pathlib import Path
import hashlib, json, re, shutil
project = Path(__file__).resolve().parents[4]
tested = Path('/private/tmp/exusiai-weapon-check')
root = Path('Assets/Game/Characters/Exusiai/Model')
source = project / 'ArtSource/Exusiai/Animation'
validation = source / 'Validation'
baseline = json.loads((validation / 'weapon-integration-baseline.json').read_text())
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
gates = {'ExusiaiMotionBake.Build': 'EXUSIAI_MOTION_BUILD_OK',
         'ExusiaiEquippedWeaponAcceptance.Run': 'EXUSIAI_EQUIPPED_WEAPON_OK',
         'ExusiaiMotionAcceptance.Run': 'EXUSIAI_MOTION_ACCEPTANCE_OK',
         'ExusiaiMotionPlayCheck.Start': 'EXUSIAI_MOTION_PLAY_OK',
         'ExusiaiSecondaryAcceptance.Run': 'EXUSIAI_SECONDARY_ACCEPTANCE_OK'}
for method, marker in gates.items():
    log = Path('/private/tmp/exusiai-equipped-' + method + '.log').read_text()
    assert marker in log and 'error CS' not in log and 'Assertion failed' not in log, method
animation_names = ['Exusiai_Idle_Humanoid.anim', 'Exusiai_Jog_Humanoid.anim',
    'Exusiai_Aim_Humanoid.anim', 'Exusiai_Recoil_Humanoid.anim',
    'Exusiai_Game_Avatar_Humanoid.asset', 'Exusiai_Game_Humanoid.controller',
    'Exusiai_UpperBody.mask', 'Exusiai_MotionSettings.asset', 'Exusiai_SecondaryMotion.asset']
assets = [root / 'Animations' / name for name in animation_names]
assets += [root / 'Prefabs/Exusiai_Game_Humanoid.prefab', root / 'Scenes/Preview_Game_Humanoid.unity',
    root / 'Tests/EditMode/ExusiaiMotionAcceptance.cs', root / 'Tests/EditMode/ExusiaiEquippedWeaponAcceptance.cs']
paths = assets + [Path(str(path) + '.meta') for path in assets]
obsolete = root / 'Animations/Exusiai_TestWeapon.mat'
removed = [obsolete, Path(str(obsolete) + '.meta')]
permitted = set(map(str, paths + removed))
for checkout in (project, tested):
    for name, sha in baseline.items():
        if name not in permitted:
            assert (checkout / name).is_file() and digest(checkout / name) == sha, (checkout, name)
for path in paths:
    assert (tested / path).is_file(), path
    if path.suffix == '.meta' and str(path) in baseline:
        assert digest(tested / path) == baseline[str(path)], ('Metadata changed', path)
for path in removed: assert not (tested / path).exists(), path
for name in ['ExusiaiMotionBake.cs', 'ExusiaiSecondaryBake.cs', 'ExusiaiMotionRender.cs', 'ExusiaiMotionSequenceCapture.cs']:
    assert digest(source / 'Tools' / name) == digest(tested / 'Assets/Editor' / name), name
for path in paths:
    destination = project / path; destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(tested / path, destination)
    assert digest(destination) == digest(tested / path), path
for path in removed:
    destination = project / path
    if destination.exists():
        assert digest(destination) == baseline[str(path)], ('Edited obsolete file', path)
        destination.unlink()
shutil.copy2(tested / 'ArtSource/Exusiai/Animation/key-poses.json', source / 'key-poses.json')
for name in ['equipped-weapon-acceptance.json', 'motion-acceptance.txt', 'motion-play-validation.txt', 'secondary-motion-acceptance.txt']:
    shutil.copy2(tested / name, validation / name)
for image in (tested / 'motion-previews').glob('*.png'): shutil.copy2(image, source / 'Previews' / image.name)
shutil.copy2('/private/tmp/exusiai-equipped-sequence.mp4', source / 'Previews/Exusiai_Motion_Sequence.mp4')
known = {}
for folder in [project / 'Assets', project / 'Packages', project / 'Library/PackageCache']:
    for meta in folder.rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(errors='replace'), re.M)
        if match: known.setdefault(match.group(1), str(meta.relative_to(project)))
references = {}
for path in paths:
    for guid in set(re.findall(r'guid: ([0-9a-f]{32})', (project / path).read_text(errors='replace'))):
        assert guid.startswith('0000000000000000') or guid in known, (path, guid)
        references[guid] = known.get(guid, 'Unity built-in resource')
preserved = [name for name in baseline if name not in permitted]
for name in preserved: assert digest(project / name) == baseline[name], name
report = {'passed': True, 'branch': 'model', 'preserved_file_count': len(preserved),
    'independent_weapon_unchanged': True, 'original_rig_unchanged': True,
    'removed_obsolete_asset_count': len(removed), 'delivered_file_count': len(paths),
    'delivered_sha256': {str(path): digest(project / path) for path in paths},
    'resolved_references': references, 'existing_meta_guids_preserved': True,
    'gates': list(gates), 'animation_attachment': 'equipped_user_approved'}
(validation / 'delivery-validation.json').write_text(json.dumps(report, indent=2) + '\n')
print('Delivered', len(paths), 'files; preserved', len(preserved), 'existing files; removed', len(removed), 'obsolete files.')
```

- [x] Document source durations, 2.4 m/s reference speed, left primary/right support, input signatures, editable authoring/rebuild steps, preview controls and the fact that no damage or projectiles are generated. Attach measured gate reports and inspected previews.
- [x] Check every new scene/prefab/controller/script asset GUID resolves against project .meta files. No existing FBX, Avatar, benchmark prefab/scene, original bone sync, materials or textures may differ from baseline; the new formal Avatar is independent.

## Review against the approved spec

A covers formal editable curves, one-hand idle/run, two-hand shots, same primary hand, transitions, real Animator Play, a replaceable weapon, an upper-body mask, in-place motion, migration interfaces and independent preview. Physics and shared stability/visual tests are covered by B/C and have passed in the final combined delivery.

## Executed refinements and final evidence

- Built an independent formal Avatar with 49 actual mapped bones. Original finger enum names were ignored by Unity; HumanTrait spaced humanName and native finger curve property names now drive all 30 fingers. Original Avatar/FBX remain unchanged.
- Added real blink curves and measured them; use Foot IK=false on direct clip Playables to preserve the authored body root.
- Jog uses a 0.4 stance fraction with measured planted-foot velocity -2.385 m/s at the 2.4 m/s reference. Free-arm elbow stays beside/behind the torso. Low speed uses full stride at a reduced playback rate, rather than reducing blend and rate together; idle stays at rate 1.
- Added SetAiming during attack preparation. Real Play confirms the upper layer is ready before the first actual shot. Without preparation the first feedback waits for the 0.22 s raise.
- The final Play fixture runs at 1.2 m/s and samples required timing windows; moving-fire foot travel is 0.388 m, grip error 9.09 mm, and recoil restarts 50 times. After the hair-physics correction, Play checks sample completed game frames at a verified 60 FPS timestep; the current report has 1080 samples, 8.96 mm support error, 46 recoil restarts and 0.394 m moving-shot foot travel. Earlier variable Editor-update counts are historical and not rendered frame counts.
- 12 front/side/back pose PNGs and an 18 s continuous authoring preview were inspected. The latter uses the same Animator controller and solver with actual root translation; real MonoBehaviour Play is checked separately.
- Delivery: 54 new asset/meta files; 108 prior model files unchanged; all referenced GUIDs resolve. Measured reports are under ArtSource/Exusiai/Animation/Validation. Model branch remains uncommitted for subsequent integration.

### Verification assembly definitions

`Model/Tests/EditMode/ArknightsFrontline.Model.EditModeVerification.Editor.asmdef`:

```json
{
  "name": "ArknightsFrontline.Model.EditModeVerification.Editor",
  "references": [
    "ArknightsFrontline.Model.Runtime"
  ],
  "includePlatforms": [
    "Editor"
  ],
  "autoReferenced": true
}
```

`Model/Tests/PlayMode/ArknightsFrontline.Model.PlayModeVerification.Editor.asmdef`:

```json
{
  "name": "ArknightsFrontline.Model.PlayModeVerification.Editor",
  "references": [
    "ArknightsFrontline.Model.Runtime",
    "ArknightsFrontline.Model.EditModeVerification.Editor"
  ],
  "includePlatforms": [
    "Editor"
  ],
  "autoReferenced": true
}
```

### Continuous preview capture source

`ArtSource/Exusiai/Animation/Tools/ExusiaiMotionSequenceCapture.cs` (copy only the authoring tool to isolated Assets/Editor):

```csharp
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;

// Deterministic authoring preview. Actual MonoBehaviour/Animator integration is
// checked separately by ExusiaiMotionPlayCheck in real Play mode.
public static class ExusiaiMotionSequenceCapture
{
    private static int updates;
    public static void Start()
    {
        EditorSceneManager.OpenScene(ExusiaiMotionBake.Root + "/Scenes/Preview_Game_Humanoid.unity");
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/PreviewRenderer.asset");
        if (!data) { data = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(data, "Assets/PreviewRenderer.asset"); }
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/PreviewPipeline.asset");
        if (!pipeline) { pipeline = UniversalRenderPipelineAsset.Create(data); AssetDatabase.CreateAsset(pipeline, "Assets/PreviewPipeline.asset"); }
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (++updates < 50) return;
        EditorApplication.update -= Tick;
        try
        {
            const int rate = 15, width = 640, height = 800;
            Directory.CreateDirectory("motion-sequence-frames");
            var animator = UnityEngine.Object.FindFirstObjectByType<Animator>();
            var actor = animator.transform.root;
            var physics = actor.GetComponent<ExusiaiSecondaryMotion>();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var type = physics.GetType();
            physics.Configure((ExusiaiSecondaryMotion.Chain[])type.GetField("chains", flags).GetValue(physics),
                (ExusiaiSecondaryMotion.Capsule[])type.GetField("colliders", flags).GetValue(physics),
                (ExusiaiSecondaryMotionProfile)type.GetField("profile", flags).GetValue(physics));
            var graph = PlayableGraph.Create("Sequence"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var controller = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
            var output = AnimationPlayableOutput.Create(graph, "Sequence", animator); output.SetSourcePlayable(controller); graph.Play();
            var skin = animator.GetComponentInChildren<SkinnedMeshRenderer>(); var mesh = new Mesh();
            var display = new GameObject("CapturedSkin"); display.transform.SetParent(skin.transform, false);
            display.AddComponent<MeshFilter>().sharedMesh = mesh; display.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
            skin.enabled = false;
            var cam = UnityEngine.Object.FindFirstObjectByType<Camera>(); cam.fieldOfView = 30; cam.aspect = (float)width / height;
            var rt = new RenderTexture(width, height, 24); rt.Create(); var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var prepare = typeof(RenderPipelineManager).GetMethod("TryPrepareRenderPipeline", BindingFlags.Static | BindingFlags.NonPublic);
            prepare.Invoke(null, new object[] { GraphicsSettings.defaultRenderPipeline });
            float aim = 0, blend = 0, nextShot = 0;
            for (int frame = 0; frame < 18 * rate; frame++)
            {
                float time = (float)frame / rate, dt = 1f / rate;
                bool moving = (time >= 3 && time < 6) || (time >= 13 && time < 16);
                bool shooting = (time >= 7 && time < 10) || (time >= 13 && time < 16);
                Vector3 direction = time >= 10 ? Vector3.back : Vector3.forward;
                physics.RestorePose();
                animator.transform.rotation = Quaternion.RotateTowards(animator.transform.rotation, Quaternion.LookRotation(direction), 540 * dt);
                if (moving) actor.position += direction * 2.4f * dt;
                blend = Mathf.MoveTowards(blend, moving ? 1 : 0, dt / 0.12f);
                bool prepared = (time >= 6.65f && time < 10.55f) || (time >= 12.65f && time < 16.55f);
                aim = Mathf.MoveTowards(aim, prepared ? 1 : 0, dt / (prepared ? 0.22f : 0.3f));
                controller.SetFloat("MoveSpeed", blend); controller.SetFloat("JogRate", 1);
                controller.SetLayerWeight(1, aim); controller.SetLayerWeight(2, aim);
                if (shooting && aim >= 0.99f && time >= nextShot) { controller.Play("Recoil", 2, 0); nextShot = time + 0.12f; }
                graph.Evaluate(dt); animator.GetComponent<ExusiaiHumanoidBoneSync>()?.Synchronize(); physics.Simulate(dt);
                skin.BakeMesh(mesh);
                cam.transform.position = actor.position + new Vector3(2.2f, 1.15f, 4.1f); cam.transform.LookAt(actor.position + new Vector3(0, 0.87f, 0));
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                RenderPipeline.SubmitRenderRequest(cam, request); RenderPipeline.SubmitRenderRequest(cam, request);
                RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                File.WriteAllBytes("motion-sequence-frames/frame-" + frame.ToString("D4") + ".png", texture.EncodeToPNG());
            }
            RenderTexture.active = null; graph.Destroy(); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
```

## Hair-physics correction checkpoint (2026-10-05)

A dedicated real Play regression verifies preservation of the short bangs forehead silhouette. Fixed timing is set after entering Play because Unity resets pre-transition capture timing; every sampled timestep is asserted. The independent reference comes from original prefab hair transforms, not RestorePose. It checks 3517 weighted skin vertices, with a 15 mm maximum deviation and a nonzero jogging response. Verified 30/60/120 FPS runs pass. Source:

```csharp
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Catches bangs peeling away from the forehead during the actual preview lifecycle.
// Reference is independently read from the prefab, never from solver.RestorePose().
public static class ExusiaiHairPlayCheck
{
    public static Action<SkinnedMeshRenderer, Mesh, string> Capture;
    private static readonly float[] snapshotTimes = { 3.6f, 10.25f, 10.45f };
    private static readonly string[] snapshotNames = { "jog", "turn", "recover" };
    private static Quaternion[] originalRotations;
    private static Transform[] hair;
    private static int[] vertices;
    private static SkinnedMeshRenderer skin;
    private static Mesh actualMesh, referenceMesh;
    private static int frame = -1, samples, snapshot, requestedRate;
    private static bool clockConfigured;
    private static float maximum, joggingMaximum, turnMaximum;

    public static void Start() => StartAtRate(60);
    public static void Start30() => StartAtRate(30);
    public static void Start120() => StartAtRate(120);

    private static void StartAtRate(int rate)
    {
        requestedRate = rate;
        const string model = "Assets/Game/Characters/Exusiai/Model";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(model + "/Prefabs/Exusiai_Game_Humanoid.prefab");
        originalRotations = prefab.GetComponentsInChildren<Transform>().Where(t => t.name.Contains("Hair_")).Select(t => t.localRotation).ToArray();
        EditorSceneManager.OpenScene(model + "/Scenes/Preview_Game_Humanoid.unity");
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = rate; Time.captureFramerate = rate;
        EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        // Entering Play resets capture timing; set it after the transition and only
        // sample completed game frames, not repeated Editor update callbacks.
        if (!clockConfigured)
        {
            Time.captureFramerate = requestedRate; clockConfigured = true;
            frame = Time.frameCount; return;
        }
        if (frame == Time.frameCount) return;
        frame = Time.frameCount;
        if (frame % 3 != 0) return;
        try
        {
            ExusiaiMotionAcceptance.Require(Mathf.Abs(Time.deltaTime - 1f / requestedRate) < 0.00001f, "Requested simulation timestep was reset");
            if (!skin)
            {
                var physics = UnityEngine.Object.FindFirstObjectByType<ExusiaiSecondaryMotion>();
                hair = physics.GetComponentsInChildren<Transform>().Where(t => t.name.Contains("Hair_")).ToArray();
                skin = physics.GetComponentInChildren<SkinnedMeshRenderer>();
                ExusiaiMotionAcceptance.Require(hair.Length == 6 && originalRotations.Length == 6, "Missing original hair chains");
                int[] indices = skin.bones.Select((t, i) => new { t, i }).Where(x => x.t.name.Contains("Hair_")).Select(x => x.i).ToArray();
                vertices = skin.sharedMesh.boneWeights.Select((w, i) => new { w, i }).Where(x =>
                    (indices.Contains(x.w.boneIndex0) ? x.w.weight0 : 0) + (indices.Contains(x.w.boneIndex1) ? x.w.weight1 : 0) +
                    (indices.Contains(x.w.boneIndex2) ? x.w.weight2 : 0) + (indices.Contains(x.w.boneIndex3) ? x.w.weight3 : 0) > 0.25f).Select(x => x.i).ToArray();
                ExusiaiMotionAcceptance.Require(vertices.Length > 3000, "Missing actual weighted hair surface");
                actualMesh = new Mesh(); referenceMesh = new Mesh();
            }
            var rotations = hair.Select(t => t.localRotation).ToArray();
            skin.BakeMesh(actualMesh);
            for (int i = 0; i < hair.Length; i++) hair[i].localRotation = originalRotations[i];
            skin.BakeMesh(referenceMesh);
            for (int i = 0; i < hair.Length; i++) hair[i].localRotation = rotations[i];
            var actual = actualMesh.vertices; var expected = referenceMesh.vertices;
            float difference = 0;
            foreach (int i in vertices)
                difference = Mathf.Max(difference, Vector3.Distance(skin.transform.TransformPoint(actual[i]), skin.transform.TransformPoint(expected[i])));
            maximum = Mathf.Max(maximum, difference);
            if (Time.time >= 3 && Time.time < 6) joggingMaximum = Mathf.Max(joggingMaximum, difference);
            if (Time.time >= 10 && Time.time < 11) turnMaximum = Mathf.Max(turnMaximum, difference);
            samples++;
            if (snapshot < snapshotTimes.Length && Time.time >= snapshotTimes[snapshot])
            {
                Capture?.Invoke(skin, actualMesh, snapshotNames[snapshot] + "-physics");
                Capture?.Invoke(skin, referenceMesh, snapshotNames[snapshot] + "-reference");
                snapshot++;
            }
            if (Time.time < 18) return;
            string report = "simulation_rate=" + requestedRate + "\nsamples=" + samples + "\nweighted_hair_vertices=" + vertices.Length + "\nmaximum_hair_displacement_m=" + maximum.ToString("R") +
                "\njog_hair_displacement_m=" + joggingMaximum.ToString("R") + "\nturn_hair_displacement_m=" + turnMaximum.ToString("R");
            File.WriteAllText("hair-play-validation.txt", report + "\n");
            ExusiaiMotionAcceptance.Require(samples > 100 && snapshot == 3, "Required real Play stages not sampled");
            // This short, broad bang geometry should keep its forehead silhouette, while
            // still exhibiting a measurable response under ordinary jogging/turning.
            ExusiaiMotionAcceptance.Require(maximum < 0.015f, "Hair physics peels bangs away from the forehead: " + maximum);
            ExusiaiMotionAcceptance.Require(joggingMaximum > 0.0005f, "Hair physics was disabled instead of repaired");
            Debug.Log("EXUSIAI_HAIR_PLAY_OK\n" + report);
            Time.captureFramerate = 0; EditorApplication.update -= Tick; EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); Time.captureFramerate = 0; EditorApplication.update -= Tick; EditorApplication.Exit(1); }
    }
}
```

## Approved Vector update (2026-10-05)

The formerly temporary block prop has been removed. The embedded authoring bake and delivery tool now use the approved independent Vector prefab; equipped scope/results are recorded in 2026-10-05-exusiai-equipped-vector.md.
