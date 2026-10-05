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
