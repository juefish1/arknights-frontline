using ArknightsFrontline.Arena;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    [DefaultExecutionOrder(50), DisallowMultipleComponent]
    public sealed class ExusiaiCombatPresentation : MonoBehaviour
    {
        [SerializeField] private ExusiaiPresentation presentation;
        [SerializeField] private Transform visualRoot;
        // Deployment clones are not parented to the arena. Preserve this external reference.
        [SerializeField] private MatchOutcomeController match;
        private CombatUnit owner;
        private UnitMotor motor;
        private BasicAttackController attacks;
        private AttackSequenceExecutor sequence;
        private SkillDashController dash;
        private OperatorRetreatController retreat;
        private Vector3 previousPosition;
        private CombatUnit shotTarget;
        private float shotHold;
        private bool subscribed, stopped;
        public ExusiaiPresentation Presentation => presentation;
        public Transform VisualRoot => visualRoot;

        public void Configure(ExusiaiPresentation target, Transform root)
        {
            Unsubscribe();
            presentation = target;
            visualRoot = root;
            Resolve();
            ResetForDeployment();
            if (isActiveAndEnabled) Subscribe();
        }

        private void Resolve()
        {
            owner = GetComponent<CombatUnit>();
            motor = GetComponent<UnitMotor>();
            attacks = GetComponent<BasicAttackController>();
            sequence = GetComponent<AttackSequenceExecutor>();
            dash = GetComponent<SkillDashController>();
            retreat = GetComponent<OperatorRetreatController>();
            if (!match) match = GetComponentInParent<MatchOutcomeController>();
        }

        private void OnEnable()
        {
            Resolve();
            if (!owner || !motor || !sequence || !GetComponent<PlayerCommandController>() ||
                !presentation || !visualRoot || !presentation.transform.IsChildOf(transform) || !visualRoot.IsChildOf(transform))
            {
                Debug.LogError("Exusiai combat presentation requires a player attack sequence and model references.", this);
                enabled = false;
                return;
            }
            ResetForDeployment();
            Subscribe();
        }

        private void Subscribe()
        {
            if (subscribed || !sequence || !owner) return;
            sequence.ShotRequested += OnShot;
            owner.Died += OnDeath;
            if (match) match.MatchEnding += StopPresentation;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (sequence) sequence.ShotRequested -= OnShot;
            if (owner) owner.Died -= OnDeath;
            if (match) match.MatchEnding -= StopPresentation;
            subscribed = false;
        }

        public void ResetForDeployment()
        {
            previousPosition = transform.position;
            shotTarget = null;
            shotHold = 0;
            stopped = owner && owner.IsDead || match && match.IsEnding;
            if (presentation) presentation.ResetPresentation();
        }

        public void Tick(float deltaTime)
        {
            Vector3 displacement = transform.position - previousPosition;
            previousPosition = transform.position;
            displacement.y = 0;
            if (!presentation || !visualRoot) return;
            if (stopped || owner.IsDead || match && match.IsEnding) { presentation.SetMoveSpeed(0); return; }
            if (!float.IsFinite(deltaTime) || deltaTime <= 0)
            {
                presentation.SetMoveSpeed(0);
                return;
            }
            if ((!dash || !dash.IsDashing) && displacement.magnitude > Mathf.Max(0.7f, 2 * motor.MovementSpeed * deltaTime))
            {
                ResetForDeployment();
                return;
            }
            float scale = Mathf.Abs(visualRoot.lossyScale.x);
            presentation.SetMoveSpeed(displacement.magnitude / deltaTime / Mathf.Max(0.001f, scale));
            shotHold = Mathf.Max(0, shotHold - deltaTime);
            bool guiding = retreat && retreat.IsGuiding;
            CombatUnit target = shotHold > 0 && TargetRules.IsLegal(owner, shotTarget) ? shotTarget : attacks ? attacks.CurrentTarget : null;
            bool aiming = !guiding && TargetRules.IsLegal(owner, target) &&
                (shotHold > 0 || HorizontalDistance(target) <= owner.AttackRange);
            presentation.SetAiming(aiming);
            if (aiming) presentation.SetAimDirection(target.transform.position - transform.position);
            else if (displacement.sqrMagnitude > 0.000001f) presentation.SetAimDirection(displacement);
        }

        private float HorizontalDistance(CombatUnit target)
        {
            Vector3 offset = target.transform.position - transform.position;
            return new Vector2(offset.x, offset.z).magnitude;
        }

        private void OnShot(CombatUnit target, PhysicalDamagePayload payload)
        {
            if (!isActiveAndEnabled || stopped || owner.IsDead || match && match.IsEnding || retreat && retreat.IsGuiding || !target) return;
            shotTarget = target;
            shotHold = 0.55f;
            presentation.SetAimDirection(target.transform.position - transform.position);
            presentation.PlayShot();
        }

        private void OnDeath(CombatUnit _) => StopPresentation();
        private void StopPresentation()
        {
            stopped = true;
            shotTarget = null;
            shotHold = 0;
            if (presentation) presentation.ResetPresentation();
        }
        private void Update() => Tick(Time.deltaTime);
        private void OnDisable() { Unsubscribe(); StopPresentation(); }
        private void OnDestroy() => Unsubscribe();
    }
}
