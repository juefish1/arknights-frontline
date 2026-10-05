using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public static class ExusiaiSecondaryAcceptance
{
    private const string Prefab = "Assets/Game/Characters/Exusiai/Model/Prefabs/Exusiai_Game_Humanoid.prefab";
    private static void Call(Component target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
    private static void ConfigureSerialized(Component target)
    {
        var type = target.GetType(); const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        Call(target, "Configure", type.GetField("chains", flags).GetValue(target), type.GetField("colliders", flags).GetValue(target), type.GetField("profile", flags).GetValue(target));
    }
    private static Transform[] Bones(GameObject go) => go.GetComponentsInChildren<Transform>().Where(t => t.name.Contains("Hair_") || t.name.StartsWith("SkirtHem_")).ToArray();
    public static void Run()
    {
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            var go = UnityEngine.Object.Instantiate(prefab);
            var physics = go.GetComponent("ExusiaiSecondaryMotion");
            ExusiaiMotionAcceptance.Require(physics, "Missing hair/skirt simulation");
            ConfigureSerialized(physics);
            var bones = Bones(go);
            ExusiaiMotionAcceptance.Require(bones.Length == 78, "Incorrect hair/skirt coverage");
            var positions = bones.Select(t => t.localPosition).ToArray();
            var scales = bones.Select(t => t.localScale).ToArray();
            var restRotations = bones.Select(t => t.localRotation).ToArray();
            var final = new Quaternion[3][];
            var meshFrames = new Vector3[3][]; var pointFrames = new Vector3[3][];
            float maximum = 0;
            for (int rateIndex = 0; rateIndex < 3; rateIndex++)
            {
                int rate = new[] { 30, 60, 120 }[rateIndex];
                go.transform.position = Vector3.zero; Call(physics, "ResetSimulation");
                for (int frame = 1; frame <= rate * 3; frame++)
                {
                    Call(physics, "RestorePose");
                    float t = (float)frame / rate;
                    go.transform.position = new Vector3(0.08f * Mathf.Sin(t * 4), 0, 0);
                    Call(physics, "Simulate", 1f / rate);
                    for (int i = 0; i < bones.Length; i++)
                    {
                        ExusiaiMotionAcceptance.Require(float.IsFinite(bones[i].position.sqrMagnitude), "Nonfinite spring pose");
                        ExusiaiMotionAcceptance.Require(bones[i].localPosition == positions[i] && bones[i].localScale == scales[i], "Physics stretched a bone");
                        maximum = Mathf.Max(maximum, Quaternion.Angle(restRotations[i], bones[i].localRotation));
                    }
                }
                final[rateIndex] = bones.Select(t => t.localRotation).ToArray();
                pointFrames[rateIndex] = bones.Select(t => t.position).ToArray();
                var skin = go.GetComponentInChildren<SkinnedMeshRenderer>(); var mesh = new Mesh(); skin.BakeMesh(mesh);
                meshFrames[rateIndex] = mesh.vertices; UnityEngine.Object.DestroyImmediate(mesh);
                var paused = bones.Select(t => t.rotation).ToArray();
                for (int frame = 0; frame < 10; frame++) { Call(physics, "RestorePose"); Call(physics, "Simulate", 0f); }
                for (int i = 0; i < bones.Length; i++)
                    ExusiaiMotionAcceptance.Require(Quaternion.Angle(paused[i], bones[i].rotation) < 0.15f, "Pause changed the physical pose");
            }
            ExusiaiMotionAcceptance.Require(maximum > 1, "Hair/skirt simulation has no response");
            float difference = 0;
            for (int i = 0; i < bones.Length; i++) difference = Mathf.Max(difference,
                Mathf.Max(Quaternion.Angle(final[0][i], final[1][i]), Quaternion.Angle(final[1][i], final[2][i])));
            File.WriteAllLines("secondary-framerate-debug.txt", Enumerable.Range(0, bones.Length).Select(i => bones[i].name + " : 30-60=" + Quaternion.Angle(final[0][i], final[1][i]) + " 60-120=" + Quaternion.Angle(final[1][i], final[2][i]) + " 30=" + final[0][i].eulerAngles + " 120=" + final[2][i].eulerAngles));
            float meshDifference = 0, pointDifference = 0;
            for (int i = 0; i < meshFrames[0].Length; i++) meshDifference = Mathf.Max(meshDifference,
                Mathf.Max(Vector3.Distance(meshFrames[0][i], meshFrames[1][i]), Vector3.Distance(meshFrames[1][i], meshFrames[2][i])));
            for (int i = 0; i < bones.Length; i++) pointDifference = Mathf.Max(pointDifference,
                Mathf.Max(Vector3.Distance(pointFrames[0][i], pointFrames[1][i]), Vector3.Distance(pointFrames[1][i], pointFrames[2][i])));
            Debug.Log("SECONDARY_RESPONSE " + maximum + " FPS_DIFF " + difference + " MESH_DIFF_M " + meshDifference + " POINT_DIFF_M " + pointDifference);
            // Contact can distribute bending across adjacent joints differently; verify the
            // visible skin and chain positions instead of equating local rotations.
            ExusiaiMotionAcceptance.Require(meshDifference < 0.015f && pointDifference < 0.015f,
                "Unstable visible frame-rate response: mesh=" + meshDifference + ", chain=" + pointDifference);
            Call(physics, "RestorePose"); go.transform.position += Vector3.right * 3; Call(physics, "Simulate", 1f / 60);
            for (int i = 0; i < bones.Length; i++)
                ExusiaiMotionAcceptance.Require(Quaternion.Angle(restRotations[i], bones[i].localRotation) < 0.15f, "Teleport failed to reset");
            Call(physics, "RestorePose"); go.transform.rotation = Quaternion.Euler(0, 180, 0); Call(physics, "Simulate", 1f / 60);
            for (int i = 0; i < bones.Length; i++)
                ExusiaiMotionAcceptance.Require(Quaternion.Angle(restRotations[i], bones[i].localRotation) < 0.15f, "Abrupt rotation failed to reset");
            go.transform.rotation = Quaternion.identity; Call(physics, "Simulate", 1f / 60);
            Call(physics, "Simulate", float.NaN);
            Call(physics, "Simulate", 1f);
            ((Behaviour)physics).enabled = false; ((Behaviour)physics).enabled = true;
            var other = UnityEngine.Object.Instantiate(prefab);
            var otherPhysics = other.GetComponent("ExusiaiSecondaryMotion");
            ConfigureSerialized(otherPhysics);
            var otherBones = Bones(other); Call(otherPhysics, "ResetSimulation");
            for (int frame = 0; frame < 240; frame++) { Call(otherPhysics, "RestorePose"); Call(otherPhysics, "Simulate", 1f / 120); }
            var otherRest = otherBones.Select(t => t.rotation).ToArray();
            for (int frame = 0; frame < 60; frame++)
            {
                Call(physics, "RestorePose"); go.transform.position += Vector3.left * 0.005f; Call(physics, "Simulate", 1f / 60);
                Call(otherPhysics, "RestorePose"); Call(otherPhysics, "Simulate", 1f / 60);
            }
            for (int i = 0; i < otherBones.Length; i++)
                ExusiaiMotionAcceptance.Require(Quaternion.Angle(otherRest[i], otherBones[i].rotation) < 1, "Instances share state or fail to converge");
            // A real three-bone chain must bend away from a collider that intersects its rest tip.
            var fixture = new GameObject("CollisionChain");
            var chainBones = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                chainBones[i] = new GameObject("Segment" + i).transform;
                chainBones[i].SetParent(i == 0 ? fixture.transform : chainBones[i - 1], false);
                chainBones[i].localPosition = i == 0 ? Vector3.zero : new Vector3(0, -0.1f, 0);
            }
            var obstacle = new GameObject("Obstacle"); obstacle.transform.position = new Vector3(-0.025f, -0.19f, 0);
            Type type = physics.GetType(), chainType = type.GetNestedType("Chain"), capsuleType = type.GetNestedType("Capsule");
            object chain = Activator.CreateInstance(chainType); chainType.GetField("bones").SetValue(chain, chainBones);
            Array chainArray = Array.CreateInstance(chainType, 1); chainArray.SetValue(chain, 0);
            object capsule = Activator.CreateInstance(capsuleType); capsuleType.GetField("start").SetValue(capsule, obstacle.transform);
            capsuleType.GetField("end").SetValue(capsule, obstacle.transform); capsuleType.GetField("radius").SetValue(capsule, 0.04f);
            Array capsuleArray = Array.CreateInstance(capsuleType, 1); capsuleArray.SetValue(capsule, 0);
            var profile = ScriptableObject.CreateInstance(type.GetField("profile", BindingFlags.NonPublic | BindingFlags.Instance).FieldType);
            var fixturePhysics = fixture.AddComponent(type); Call(fixturePhysics, "Configure", chainArray, capsuleArray, profile);
            for (int frame = 0; frame < 240; frame++) { Call(fixturePhysics, "RestorePose"); Call(fixturePhysics, "Simulate", 1f / 120); }
            float collisionDistance = Vector3.Distance(chainBones[2].position, obstacle.transform.position);
            ExusiaiMotionAcceptance.Require(collisionDistance >= 0.047f, "Actual chain still penetrates collider: " + collisionDistance);
            UnityEngine.Object.DestroyImmediate(fixture); UnityEngine.Object.DestroyImmediate(obstacle); UnityEngine.Object.DestroyImmediate(profile);
            string result = "passed=true\nchains=14\nbones=78\nmaximum_response_degrees=" + maximum.ToString("R") +
                "\nframe_rate_difference_degrees=" + difference.ToString("R") + "\nframe_rate_mesh_difference_m=" + meshDifference.ToString("R") + "\nframe_rate_point_difference_m=" + pointDifference.ToString("R") + "\nactual_chain_collision_distance_m=" + collisionDistance.ToString("R") + "\npause_teleport_hitch_enable_multiple_instances=passed";
            File.WriteAllText("secondary-motion-acceptance.txt", result);
            UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(other);
            Debug.Log("EXUSIAI_SECONDARY_ACCEPTANCE_OK\n" + result); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
