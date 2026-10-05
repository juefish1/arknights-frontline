# Exusiai Equipped Vector Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline in the current conversation. The user has explicitly approved attachment and cleanup. No extra execution-choice or Git integration prompt is needed.

**Goal:** Replace the block prop in formal animations with the approved standalone Vector and remove obsolete prop assets/generation.

**Architecture:** A nested weapon prefab under LeftWrist/WeaponMount shares the independent mesh/material. The authoring bake consumes its support point and computes both aim wrist targets in weapon coordinates. Existing acceptance, Play and physics gates exercise the rebuilt character. Selective delivery preserves unrelated assets and GUIDs.

**Tech Stack:** Unity 6000.3.25f1, URP 17.3, C# Editor bake and integration gates, Python selective delivery.

## Global Constraints

- Existing model branch; no commits, merge, subagents or main Unity batch execution.
- Isolated project /private/tmp/exusiai-weapon-check contains the current character and approved weapon.
- Shared independent weapon files, character FBXs/materials/textures, runtime physics and Generic/Humanoid diagnostic resources remain unchanged.
- Left hand holds the gun in Idle/Jog; right supports only Aim/Recoil and moving shots.
- Preserve existing resource GUIDs. Four clips and formal character/preview may change.

## Task 1: Attachment integration gate

Files: ArtSource/Exusiai/Animation/Tools/ExusiaiEquippedWeaponAcceptance.cs and Model/Tests/EditMode copy. Existing ExusiaiMotionAcceptance.Run and ExusiaiMotionPlayCheck.Start remain motion/grip gates.

- [x] Capture weapon-integration-baseline.json; stage the current character and existing authoring tools in isolation.
- [x] Add a gate that instantiates the actual formal prefab and requires its left-hand WeaponMount to contain the approved weapon asset's mesh/material, exactly one weapon renderer, unit weapon scale, and correctly placed PrimaryGrip/SupportGrip/Muzzle; reject primitive prop descendants. For PrimaryGrip wrist distance require <6 cm, character support Z offset in weapon space approximately 0.11 m, muzzle approximately 0.3681 m. Check no obsolete prop material asset and no equipped scene overrides of the weapon.
- [x] Run Unity -batchmode -nographics -projectPath /private/tmp/exusiai-weapon-check -executeMethod ExusiaiEquippedWeaponAcceptance.Run -logFile /private/tmp/exusiai-equipped-red.log. Expect exit 1 because the actual weapon mesh is absent. Gate source is saved with the plan before implementing the bake changes.

## Task 2: Read weapon reference points and bake equipped motion

Files: ArtSource/Exusiai/Animation/Tools/ExusiaiMotionBake.cs; key-poses.json; independent batch outputs in Model/Animations, formal Prefabs and Scenes.

- [x] Load the approved prefab and derive SupportGrip in its root space. Remove gripSeparation; use that local point plus the character-specific supportGripAdjustment instead.
- [x] Convert idle world gunPalmOffset to gun space using inverse idle pitch. Keep its left wrist offset identical to previous idle, derive aim right target from left + gunWorldRotation * (gunPalmOffsetInGun + supportGripLocal), where supportGripLocal includes the instance adjustment, including recoil pitch.
- [x] Replace CreateWeapon with prefab instantiation under WeaponMount, localPosition=gunInHand*gunPalmOffsetInGun, localRotation=gunInHand; override the nested SupportGrip to its character-adjusted location. No primitive creation or dedicated prop material.
- [x] Rebuild controller subassets while preserving the controller asset/GUID rather than deleting its file. Reuse existing asset objects for clips/Avatar/config.
- [x] Run ExusiaiMotionBake.Build then ExusiaiSecondaryBake.Build. Delete the obsolete material via AssetDatabase in isolation after all prefab references have been replaced.
- [x] Run ExusiaiEquippedWeaponAcceptance.Run, ExusiaiMotionAcceptance.Run, ExusiaiMotionPlayCheck.Start, ExusiaiSecondaryAcceptance.Run; expect all exit 0 and their success markers. Inspect any failed wrist/pose gate before delivery. Save actual gate outputs.

## Task 3: Visual review, cleanup and delivery

Files: animation Tools/ExusiaiMotionDelivery.py, README and previews; weapon README/validation and Exusiai README; old embedded bake/delivery code in the formal animation plan.

- [x] Render ExusiaiMotionRender.Start; inspect all 12 poses, focusing on fingers/grip, body clearance and muzzle. Run ExusiaiMotionSequenceCapture.Start and encode its 270 PNGs into the existing 18-second video; inspect selected moving/shooting frames.
- [x] Update selective delivery to require fresh equipped/motion/Play/physics markers and compare the current baseline with an explicit permitted-change list. Copy tested assets plus metas; remove obsolete material and its meta from main only after successful tests.
- [x] Refresh animation delivery report; remove old generator/material entries from current delivery code and the embedded original plan. Update docs to say approved Vector is equipped. Refresh the independent weapon character baseline after this authorized integration so its verifier stays useful; preserve its tested weapon manifest unchanged.
- [x] Verify GUID references, unchanged standalone weapon and base rig assets, stable GUIDs for changed existing files, and delivered/tested hashes. Run the standalone weapon delivery verifier again. Keep model branch and provide new preview and opening instructions.

## Source implementation and results

### Attachment gate

```csharp
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Actual delivered prefab and scene, with no primitive or mocked weapon.
public static class ExusiaiEquippedWeaponAcceptance
{
    private const string Model = "Assets/Game/Characters/Exusiai/Model";
    private const string Weapon = "Assets/Game/Weapons/ExusiaiVector";
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    private static void Check(GameObject actor)
    {
        var animator = actor.GetComponentInChildren<Animator>();
        var mount = actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == "WeaponMount");
        Require(mount.parent == animator.GetBoneTransform(HumanBodyBones.LeftHand), "Primary hand changed");
        var expected = AssetDatabase.LoadAssetAtPath<GameObject>(Weapon + "/Models/Exusiai_Vector.fbx")
            .GetComponentInChildren<MeshFilter>().sharedMesh;
        var renderers = mount.GetComponentsInChildren<MeshRenderer>(true);
        Require(renderers.Length == 1 && renderers[0].GetComponent<MeshFilter>().sharedMesh == expected,
            "Approved Vector mesh is not equipped");
        Require(renderers[0].sharedMaterial == AssetDatabase.LoadAssetAtPath<Material>(Weapon + "/Materials/Exusiai_Vector.mat"),
            "Equipped weapon material is not the shared approved material");
        var weapon = mount.Find("Exusiai_Vector");
        Require(weapon && weapon.localScale == Vector3.one, "Weapon instance missing or rescaled");
        var original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(weapon.gameObject);
        Require(original && AssetDatabase.GetAssetPath(original) == Weapon + "/Prefabs/Exusiai_Vector.prefab",
            "Weapon lost its nested prefab connection");
        var primary = weapon.Find("PrimaryGrip");
        var support = weapon.Find("SupportGrip");
        var muzzle = weapon.Find("Muzzle");
        Require(primary && support && muzzle, "Weapon grip/muzzle points missing");
        Require(Vector3.Distance(primary.position, mount.parent.position) < 0.06f, "Primary wrist separated from grip");
        Require(Mathf.Abs(support.localPosition.z - 0.11f) < 0.001f && Mathf.Abs(muzzle.localPosition.z - 0.3681f) < 0.001f,
            "Weapon markers still describe the old prop");
        Require(!mount.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("TestWeapon_")),
            "Obsolete primitive weapon survived");
        Require(mount.GetComponentsInChildren<Collider>(true).Length == 0, "Unexpected gameplay collider");
    }

    public static void Run()
    {
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Model + "/Prefabs/Exusiai_Game_Humanoid.prefab");
            Require(prefab, "Formal prefab missing");
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Check(actor);
            UnityEngine.Object.DestroyImmediate(actor);
            Require(!AssetDatabase.LoadAssetAtPath<Material>(Model + "/Animations/Exusiai_TestWeapon.mat"),
                "Obsolete prop material still exists");
            EditorSceneManager.OpenScene(Model + "/Scenes/Preview_Game_Humanoid.unity");
            actor = UnityEngine.Object.FindFirstObjectByType<ExusiaiPresentation>().gameObject;
            Check(actor);
            string report = "{\"passed\":true,\"shared_vector_mesh\":true,\"shared_vector_material\":true," +
                "\"nested_weapon_prefab\":true,\"left_primary_hand\":true,\"obsolete_prop_removed\":true,\"scene_verified\":true}";
            File.WriteAllText("equipped-weapon-acceptance.json", report);
            Debug.Log("EXUSIAI_EQUIPPED_WEAPON_OK " + report);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
```

### Complete updated authoring bake

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

### Complete updated motion gate

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

### Complete selective delivery

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

### Verified results

All eight final Unity batch processes exited 0 (bake, physical attachment, equipped gate, motion gate, real Play, physics gate, twelve pose renders, 270 sequence frames). Real Play: 1080 samples, support wrist error 5.61 mm, 46 recoil restarts and moving shots with 0.394 m foot motion. Static aim stock/torso sampled clearance 14.85 mm. Delivered 26 verified files, preserved 159 pre-existing files, removed the obsolete material and meta, and preserved existing GUIDs. Standalone Vector files remain byte-identical. Updated twelve PNGs and 18-second H.264 video inspected in representative stages.

Rebake exposed missing serialized additive reference settings; native cache calls did not persist a valid reference. The final bake persists clip settings directly and the fresh-process gate verifies the Aim reference. No private reflection/native SetAdditiveReferencePose call remains.
