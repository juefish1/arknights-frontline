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
