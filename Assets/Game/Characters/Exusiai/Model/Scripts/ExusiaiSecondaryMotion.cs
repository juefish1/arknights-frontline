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
    private float WorldScale => Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x));

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
            Vector3.Distance(lastPosition, motionRoot.position) > profile.teleportDistance * WorldScale ||
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
                state.acceleration = Vector3.ClampMagnitude((velocity - state.lastAnchorVelocity) / step, 60 * WorldScale);
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
                        (capsule.radius + spring.radius) * WorldScale, restDirection);
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
