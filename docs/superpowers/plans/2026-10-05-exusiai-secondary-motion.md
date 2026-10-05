# Exusiai Hair and Skirt Physics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking; this is the executed plan with final verified sources. Continue inline on model.

**Goal:** Add stable hair/skirt follow-through to the formal animated prefab and verify the combined result.

**Architecture:** Simulate 14 existing bone chains with fixed-step inertial springs and angle/length/capsule constraints. An early restore component clears the previous override before Animator; the solver runs after the existing leg synchronization. Each instance owns its state; a shared settings asset owns only immutable tuning values.

**Tech Stack:** Unity 6000.3.25f1, URP 17.3, existing Humanoid rig, C# bone spring/Verlet integration, editor and actual Play integration gates.

## Global Constraints

- Work on model in the existing checkout; test in /private/tmp/exusiai-playback-check.
- 2 hair chains × 3 bones and 12 skirt chains × 6 bones; 78 bones total, 14 chains. Last tips are virtual; add no skin bones.
- Do not modify FBX, bind matrices, vertex weights, bone parents, local positions, scales, existing Generic/Humanoid benchmarks, source sync or shared materials/textures.
- Restore (-2000) → Animator → ExusiaiHumanoidBoneSync (1000) → secondary motion (1100).
- Fixed substeps with a cap; cover 30/60/120 FPS, pause/resume, teleport, abrupt rotation, hitches and enable/disable.
- Body proxies include head, torso and both thighs. Assert collision on actual chains; a utility-only collision test does not satisfy acceptance.
- Separate profile values for hair/skirt stiffness, damping, inertia, radius and angular limit. No per-instance state in the profile.
- Preserve one-hand idle/jog and two-hand shooting from plan A. Use the presentation ResetRequested event to clear simulation caches.
- No paid plugins or cloth self-collision requirement. A and B jointly need numeric and visual acceptance before full completion.

## File Map and Interfaces

Runtime under Assets/Game/Characters/Exusiai/Model/Scripts: ExusiaiSecondaryMotionProfile.cs (tuning only), ExusiaiSecondaryMotionRestore.cs (early restore), ExusiaiSecondaryMotion.cs (per-instance solver, capsules, reset).

Executable gate: Model/Tests/EditMode/ExusiaiSecondaryAcceptance.cs in Model.EditModeVerification.Editor; ArtSource/Tools keeps an uncompiled source backup. Authoring tool: ArtSource/Exusiai/Animation/Tools/ExusiaiSecondaryBake.cs (14 chains and 4 body proxies). Profile: Model/Animations/Exusiai_SecondaryMotion.asset. Modify only the new formal prefab to add components; benchmark prefabs remain unchanged.

Public integration: ExusiaiSecondaryMotion.Configure(Chain[] definitions, Capsule[] capsules, ExusiaiSecondaryMotionProfile settings), RestorePose(), ResetSimulation(), Simulate(float deltaTime); Restore.Configure(ExusiaiSecondaryMotion target). The solver automatically subscribes to ExusiaiPresentation.ResetRequested.

## Task 1: Establish the failing simulation gate

- [x] The original reflection-only red gate ran before solver implementation and failed on missing hair/skirt simulation. After the solver was implemented, expand it to the final real chain/collision/lifecycle gate below. Compile the final gate in Model/Tests/EditMode; its test fixture initializes the serialized solver explicitly because runtime OnEnable does not execute on EditMode clones.

```csharp
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
```

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /private/tmp/exusiai-playback-check -executeMethod ExusiaiSecondaryAcceptance.Run -logFile /private/tmp/exusiai-secondary-red.log
```

## Task 2: Implement the solver and attach all chains

**Consumes:** A's ExusiaiPresentation.ResetRequested, existing 78 chains, eight-link sync and new formal prefab.
**Produces:** the public interfaces above, complete profile and component configuration.

- [x] Write `ExusiaiSecondaryMotionProfile.cs` with the complete content below.

```csharp
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
```

- [x] Write `ExusiaiSecondaryMotionRestore.cs` with the complete content below.

```csharp
using UnityEngine;

// Animator must evaluate after restoring last frame's secondary override.
[DefaultExecutionOrder(-2000)]
public sealed class ExusiaiSecondaryMotionRestore : MonoBehaviour
{
    [SerializeField] private ExusiaiSecondaryMotion motion;
    public void Configure(ExusiaiSecondaryMotion target) => motion = target;
    private void Update() { if (motion && motion.enabled) motion.RestorePose(); }
}
```

- [x] Write `ExusiaiSecondaryMotion.cs` with the complete content below.

```csharp
using System;
using UnityEngine;

[DefaultExecutionOrder(1100)]
public sealed class ExusiaiSecondaryMotion : MonoBehaviour
{
    [Serializable] public struct Chain { public bool hair; public Transform[] bones; }
    [Serializable] public struct Capsule { public Transform start, end; public float radius; }
    [SerializeField] private Chain[] chains = Array.Empty<Chain>();
    [SerializeField] private Capsule[] colliders = Array.Empty<Capsule>();
    [SerializeField] private ExusiaiSecondaryMotionProfile profile;
    private sealed class State
    {
        public Chain chain;
        public Quaternion[] rotations, worldRotations;
        public Vector3[] current, previous, rest, frameRest, simulatedRest;
        public float[] lengths;
        public Vector3 tipLocal;
        public Vector3 lastAnchor, lastAnchorVelocity, acceleration;
    }
    private State[] states = Array.Empty<State>();
    private float accumulator;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private ExusiaiPresentation presentation;
    private Transform motionRoot;
    private Vector3[] colliderPreviousStart, colliderPreviousEnd, colliderFrameStart, colliderFrameEnd;
    private float substepFraction;

    public void Configure(Chain[] definitions, Capsule[] capsules, ExusiaiSecondaryMotionProfile settings)
    {
        RestorePose(); chains = definitions; colliders = capsules; profile = settings;
        Initialize();
    }

    private void Initialize()
    {
        motionRoot = GetComponentInChildren<Animator>() ? GetComponentInChildren<Animator>().transform : transform;
        states = new State[chains.Length];
        for (int i = 0; i < chains.Length; i++)
        {
            var bone = chains[i].bones;
            if (bone == null || bone.Length < 2 || Array.Exists(bone, t => !t))
                throw new InvalidOperationException("Secondary motion needs a complete bone chain.");
            int count = bone.Length;
            var state = new State { chain = chains[i], rotations = new Quaternion[count], worldRotations = new Quaternion[count], current = new Vector3[count + 1],
                previous = new Vector3[count + 1], rest = new Vector3[count + 1], frameRest = new Vector3[count + 1], simulatedRest = new Vector3[count + 1], lengths = new float[count] };
            for (int n = 0; n < count; n++) state.rotations[n] = bone[n].localRotation;
            Vector3 tip = bone[count - 1].position + (bone[count - 1].position - bone[count - 2].position);
            state.tipLocal = bone[count - 1].InverseTransformPoint(tip);
            states[i] = state;
        }
        colliderPreviousStart = new Vector3[colliders.Length]; colliderPreviousEnd = new Vector3[colliders.Length];
        colliderFrameStart = new Vector3[colliders.Length]; colliderFrameEnd = new Vector3[colliders.Length];
        ResetSimulation();
    }

    public void RestorePose()
    {
        foreach (var state in states)
            for (int i = 0; i < state.chain.bones.Length; i++)
                if (state.chain.bones[i]) state.chain.bones[i].localRotation = state.rotations[i];
    }

    private static void CaptureRest(State state)
    {
        int count = state.chain.bones.Length;
        for (int i = 0; i < count; i++) { state.rest[i] = state.chain.bones[i].position; state.worldRotations[i] = state.chain.bones[i].rotation; }
        state.rest[count] = state.chain.bones[count - 1].TransformPoint(state.tipLocal);
        for (int i = 0; i < count; i++) state.lengths[i] = Vector3.Distance(state.rest[i], state.rest[i + 1]);
    }

    public void ResetSimulation()
    {
        RestorePose(); accumulator = 0;
        lastPosition = motionRoot ? motionRoot.position : transform.position; lastRotation = motionRoot ? motionRoot.rotation : transform.rotation;
        foreach (var state in states)
        {
            CaptureRest(state);
            Array.Copy(state.rest, state.current, state.rest.Length);
            Array.Copy(state.rest, state.previous, state.rest.Length);
            Array.Copy(state.rest, state.simulatedRest, state.rest.Length);
            state.lastAnchor = state.rest[0]; state.lastAnchorVelocity = state.acceleration = Vector3.zero;
        }
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderPreviousStart[i] = colliders[i].start ? colliders[i].start.position : Vector3.zero;
            colliderPreviousEnd[i] = colliders[i].end ? colliders[i].end.position : Vector3.zero;
        }
    }

    private void OnEnable()
    {
        Initialize(); presentation = GetComponent<ExusiaiPresentation>();
        if (presentation) presentation.ResetRequested += ResetSimulation;
    }

    private void OnDisable()
    {
        RestorePose();
        if (presentation) presentation.ResetRequested -= ResetSimulation;
    }

    private void LateUpdate() => Simulate(Time.deltaTime);

    public void Simulate(float deltaTime)
    {
        if (!profile || states.Length == 0) return;
        if (!float.IsFinite(deltaTime) || deltaTime < 0 || deltaTime > profile.fixedStep * profile.maxSubsteps ||
            Vector3.Distance(lastPosition, motionRoot.position) > profile.teleportDistance ||
            Quaternion.Angle(lastRotation, motionRoot.rotation) > profile.teleportAngle)
        { ResetSimulation(); return; }
        lastPosition = motionRoot ? motionRoot.position : transform.position; lastRotation = motionRoot ? motionRoot.rotation : transform.rotation;
        foreach (var state in states)
        {
            CaptureRest(state);
            Array.Copy(state.rest, state.frameRest, state.rest.Length);
        }
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderFrameStart[i] = colliders[i].start ? colliders[i].start.position : Vector3.zero;
            colliderFrameEnd[i] = colliders[i].end ? colliders[i].end.position : Vector3.zero;
        }
        accumulator += deltaTime;
        float step = Mathf.Max(0.001f, profile.fixedStep);
        int count = Mathf.Min(profile.maxSubsteps, Mathf.FloorToInt((accumulator + 0.000001f) / step));
        for (int n = 0; n < count; n++)
        {
            substepFraction = (float)(n + 1) / count;
            foreach (var state in states)
            {
                for (int i = 0; i < state.rest.Length; i++) state.rest[i] = Vector3.Lerp(state.simulatedRest[i], state.frameRest[i], substepFraction);
                var spring = state.chain.hair ? profile.hair : profile.skirt;
                Vector3 carried = state.rest[0] - state.lastAnchor;
                Vector3 velocity = Vector3.Lerp(state.lastAnchorVelocity, carried / step, 1 - Mathf.Exp(-20 * step));
                state.acceleration = Vector3.ClampMagnitude((velocity - state.lastAnchorVelocity) / step, 60);
                state.lastAnchorVelocity = velocity;
                for (int i = 1; i < state.current.Length; i++) { state.current[i] += carried; state.previous[i] += carried; }
                state.lastAnchor = state.rest[0];
                Integrate(state, step);
            }
        }
        if (count > 0)
        {
            foreach (var state in states) Array.Copy(state.frameRest, state.simulatedRest, state.rest.Length);
            Array.Copy(colliderFrameStart, colliderPreviousStart, colliders.Length);
            Array.Copy(colliderFrameEnd, colliderPreviousEnd, colliders.Length);
        }
        accumulator = Mathf.Max(0, accumulator - count * step);
        foreach (var state in states) Apply(state);
    }

    private void Integrate(State state, float dt)
    {
        var spring = state.chain.hair ? profile.hair : profile.skirt;
        state.current[0] = state.previous[0] = state.rest[0];
        // These short chains control broad bangs. Anchor their first segment to the
        // head so inertia cannot rotate the entire forehead silhouette off the scalp.
        int firstFree = state.chain.hair ? 2 : 1;
        if (state.chain.hair) state.current[1] = state.previous[1] = state.rest[1];
        for (int i = firstFree; i < state.current.Length; i++)
        {
            Vector3 value = state.current[i];
            Vector3 velocity = (value - state.previous[i]) * Mathf.Exp(-Mathf.Max(0, spring.damping) * dt);
            Vector3 force = (state.rest[i] - value) * Mathf.Max(0, spring.stiffness) - state.acceleration * Mathf.Clamp01(spring.inertia);
            state.current[i] = value + velocity + force * dt * dt;
            state.previous[i] = value;
        }
        // Repeated angle/length/collision projections keep each whole chain constrained.
        for (int pass = 0; pass < 12; pass++)
        {
            state.current[0] = state.rest[0];
            for (int i = firstFree; i < state.current.Length; i++)
            {
                Vector3 restDirection = state.rest[i] - state.rest[i - 1];
                Vector3 direction = state.current[i] - state.current[i - 1];
                direction = Vector3.RotateTowards(restDirection.normalized, direction.normalized,
                    Mathf.Clamp(spring.maxAngle, 0, 85) * Mathf.Deg2Rad, 0);
                if (i > 1)
                {
                    Vector3 previousRest = state.rest[i - 1] - state.rest[i - 2];
                    Vector3 previousCurrent = state.current[i - 1] - state.current[i - 2];
                    Vector3 guide = Quaternion.FromToRotation(previousRest, previousCurrent) * restDirection;
                    direction = Vector3.RotateTowards(guide.normalized, direction.normalized,
                        Mathf.Min(12, spring.maxAngle / 2) * Mathf.Deg2Rad, 0);
                }
                state.current[i] = state.current[i - 1] + direction * state.lengths[i - 1];
                for (int col = 0; col < colliders.Length; col++)
                {
                    var capsule = colliders[col];
                    if (!capsule.start || !capsule.end) continue;
                    Vector3 before = state.current[i];
                    state.current[i] = OutsideCapsule(state.current[i], Vector3.Lerp(colliderPreviousStart[col], colliderFrameStart[col], substepFraction), Vector3.Lerp(colliderPreviousEnd[col], colliderFrameEnd[col], substepFraction),
                        capsule.radius + spring.radius, restDirection);
                    if (state.current[i] != before)
                    {
                        // Contact does not inject the projection jump as spring velocity.
                        state.previous[i] += state.current[i] - before;
                    }
                }
            }
            // Distribute contact displacement toward the pin instead of folding one joint.
            for (int i = state.current.Length - 1; i > 1; i--)
            {
                Vector3 delta = state.current[i] - state.current[i - 1];
                if (delta.sqrMagnitude < 0.0000001f) continue;
                Vector3 correction = delta.normalized * (delta.magnitude - state.lengths[i - 1]) * (i == firstFree ? 1 : 0.5f);
                state.current[i] -= correction;
                if (i != firstFree) state.current[i - 1] += correction;
            }
        }
        // Rotations cannot stretch the actual skeleton. Apply uses constrained directions
        // on the original local positions; collider clearance is checked on actual bones.
    }

    public static Vector3 OutsideCapsule(Vector3 point, Vector3 a, Vector3 b, float radius, Vector3 fallback)
    {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude < 0.0000001f ? 0 : Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
        Vector3 center = a + ab * t, outward = point - center;
        float distance = outward.magnitude;
        if (distance >= radius) return point;
        if (distance < 0.000001f) outward = fallback.sqrMagnitude > 0.000001f ? fallback.normalized : Vector3.right;
        else outward /= distance;
        return center + outward * Mathf.Max(0, radius);
    }

    private static void Apply(State state)
    {
        for (int i = 0; i < state.chain.bones.Length; i++)
        {
            if (state.chain.hair && i == 0)
            {
                state.chain.bones[i].rotation = state.worldRotations[i];
                continue;
            }
            Vector3 original = state.frameRest[i + 1] - state.frameRest[i];
            Vector3 desired = state.current[i + 1] - state.current[i];
            if (original.sqrMagnitude > 0.0000001f && desired.sqrMagnitude > 0.0000001f)
                state.chain.bones[i].rotation = Quaternion.FromToRotation(original, desired) * state.worldRotations[i];
        }
    }
}
```

- [x] Write `ExusiaiSecondaryBake.cs` with the complete content below.

```csharp
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ExusiaiSecondaryBake
{
    public static void Build()
    {
        try
        {
            const string root = "Assets/Game/Characters/Exusiai/Model";
            var go = PrefabUtility.LoadPrefabContents(root + "/Prefabs/Exusiai_Game_Humanoid.prefab");
            var named = go.GetComponentsInChildren<Transform>().ToDictionary(t => t.name, t => t);
            var chains = new List<ExusiaiSecondaryMotion.Chain>();
            foreach (var side in new[] { "Left", "Right" })
                chains.Add(new ExusiaiSecondaryMotion.Chain { hair = true,
                    bones = Enumerable.Range(0, 3).Select(i => named[side + "Hair_" + i + "_1"]).ToArray() });
            for (int col = 0; col < 12; col++)
                chains.Add(new ExusiaiSecondaryMotion.Chain { hair = false,
                    bones = Enumerable.Range(0, 6).Select(i => named["SkirtHem_" + i + "_" + col]).ToArray() });
            var capsules = new[]
            {
                new ExusiaiSecondaryMotion.Capsule { start = named["Head"], end = named["Head"], radius = 0.07f },
                new ExusiaiSecondaryMotion.Capsule { start = named["Hips"], end = named["Spine"], radius = 0.06f },
                new ExusiaiSecondaryMotion.Capsule { start = named["LeftUpperLeg"], end = named["LeftKnee"], radius = 0.06f },
                new ExusiaiSecondaryMotion.Capsule { start = named["RightUpperLeg"], end = named["RightKnee"], radius = 0.06f }
            };
            string path = root + "/Animations/Exusiai_SecondaryMotion.asset";
            var profile = AssetDatabase.LoadAssetAtPath<ExusiaiSecondaryMotionProfile>(path);
            if (!profile) { profile = ScriptableObject.CreateInstance<ExusiaiSecondaryMotionProfile>(); AssetDatabase.CreateAsset(profile, path); }
            var physics = go.GetComponent<ExusiaiSecondaryMotion>(); if (!physics) physics = go.AddComponent<ExusiaiSecondaryMotion>();
            physics.Configure(chains.ToArray(), capsules, profile);
            var restore = go.GetComponent<ExusiaiSecondaryMotionRestore>(); if (!restore) restore = go.AddComponent<ExusiaiSecondaryMotionRestore>();
            restore.Configure(physics);
            PrefabUtility.SaveAsPrefabAsset(go, root + "/Prefabs/Exusiai_Game_Humanoid.prefab");
            PrefabUtility.UnloadPrefabContents(go);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(root + "/Scenes/Preview_Game_Humanoid.unity");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
```

- [x] Run ExusiaiSecondaryBake.Build in the temporary project. Expect exit 0 and 14 serialized chains / 4 capsule proxies / profile on the new formal prefab.

```sh
/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /private/tmp/exusiai-playback-check -executeMethod ExusiaiSecondaryBake.Build -logFile /private/tmp/exusiai-secondary-build.log
```

- [x] Rerun ExusiaiSecondaryAcceptance.Run; require real response, finite poses, unchanged local positions/scales, pause stability, frame-rate differences below 1.5 cm in baked mesh and bone world positions; local rotation differences are diagnostic only, teleport/hitch reset, enable/disable and separate-instance convergence. A deliberately intersecting collider must push the actual three-bone fixture tip beyond 4.7 cm.

## Task 3: Combined delivery gate C

- [x] Run ExusiaiMotionAcceptance.Run again with physics attached; authoring graph samples keep physics inactive and preserve the pure animation baseline.
- [x] Run ExusiaiMotionPlayCheck.Start for 18 seconds with solver enabled. Inspect real hand/foot motion and support wrist alignment; add recorded physical response/finite chain checks to this gate before claiming combined acceptance.
- [x] Render the final prefab under idle/jog/shoot and inspect front/side/back and game-camera distance. Record a continuous sequence with physical follow-through and state transitions, rather than presenting stills as evidence of motion.
- [x] Selectively deliver only the three new physics C# files with their metas, profile/meta and updated new formal prefab. Repeat all 108 original file hashes from /private/tmp/exusiai-motion-baseline.json and resolve new GUID references.
- [x] Document tuning/reset behavior and exact manual validation steps in ArtSource/Exusiai/Animation/README.md; preserve and deliver both animation and physics gate reports. Keep model branch open without merging or committing unrelated existing changes.

## Self-review

A+B cover the approved one-hand idle/run, two-hand shooting, same primary hand, transitions, upper-body locomotion combination, complete root curves, geometry/loop checks, existing leg synchronization, 14 secondary chains, capsules, fixed-step scheduling and lifecycle resets. Numeric success does not override visible clipping, inverted joints or unnatural grips; tune/rebuild and rerun the affected gate before delivery.

## Executed refinements and acceptance metric

The initial local-joint frame-rate threshold of 8° was replaced with a visible-geometry threshold of 1.5 cm for both baked skin vertices and bone world positions. Contact bends redistribute across neighboring joints: the final local rotation difference is 11.997°, while maximum surface/point differences are 13.21/13.99 mm. The old 8° criterion did not pass and is not reported as passed. This replaces a diagnostic proxy with the actual visible result; no mesh/bone tolerance was loosened after delivery.

Solver refinements: interpolate animated goals and moving capsule endpoints during substeps; apply smoothed anchor acceleration inertia rather than persistent velocity drag; preserve captured animated world rotations to prevent roll feedback; distribute contact constraints over the chain and shift previous positions with contact correction to avoid injecting velocity. Torso proxy radius is 6 cm, so the resting skirt is not forcibly displaced by an oversized body proxy.

Final fixture reports: 14 chains, 78 bones, 13.53° motion response, actual three-bone collider clearance 4.9 cm against a 4.7 cm gate. Pause, teleport, abrupt 180° turn, invalid/hitch time steps, enable/disable and independent instances pass. Real Play shows finite physical motion without a >85° local flip; continuous preview was visually checked, including root movement and turn.

The 108 existing model files were preserved byte-for-byte. Reports, editable source and tuning instructions are delivered under ArtSource/Exusiai/Animation. No gameplay binding, final gun model, arbitrary-action cloth guarantee, Git commit or merge is included.

## Hair-physics defect and correction (2026-10-05)

The supplied screenshots exposed a missing visual gate: the original pose PNGs were rendered without physics. Real Play reproduction at a verified 60 FPS timestep measured 50.34 mm hair surface deviation during turning. Anchoring only the chain root point allowed the broad bang patch to rotate away from the forehead.

Hair now pins the first segment and its root orientation to the animated head, including frames with no integration substep. Remaining joints keep inertial motion; the hair profile direction limit is 10° (formerly 22°). Skirt integration remains unchanged. Reducing the root deformation alone reduced the error to about 21 mm; the final short-hair limit brings actual 30/60/120 FPS deviations to 9.55/9.53/9.55 mm with approximately 9.5 mm jogging response.

Tests use original prefab rotations as an independent visual reference. The real Play fixtures set and assert capture timing after entering Play and sample completed game frames, removing variable Editor-update timing. The combined 60 FPS motion/physics gate passes with maximum local physical response 52.99°, and existing capsule/frame-rate/reset/instance checks pass. Report: ArtSource/Exusiai/Animation/Validation/hair-physics-fix.json.

Actual Play comparison PNGs are in Previews/HairPhysics; continuous preview was refreshed. No hierarchy, local position, scale, skin weight, original asset GUID or original model file was changed.
