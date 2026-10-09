using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using UnityEngine;

namespace ArknightsFrontline.Skills
{
    public sealed class NiuLaiSkillController : MonoBehaviour, IPlayerSkillInputHandler
    {
        private CombatUnit owner;
        private UnitMotor motor;
        private BasicAttackController attacks;
        private PlayerCommandController commands;
        private OperatorRetreatController retreat;
        private MatchOutcomeController match;
        private Transform visual;
        private Vector3 visualRest, flightStart, flightEnd;
        private float flightTime, empowered;
        private int targeting;
        public float WCooldown { get; private set; }
        public float ECooldown { get; private set; }
        public float RCooldown { get; private set; } = 15;
        public bool IsEmpowered => empowered > 0;
        public bool IsFlying { get; private set; }
        public bool BlocksAttackMove => IsFlying || targeting != 0;
        public bool BlocksNormalCommands => IsFlying;
        private bool CanAct => owner && !owner.IsDead && (!match || !match.IsEnding) && (!retreat || !retreat.IsGuiding);

        private void Awake()
        {
            owner = GetComponent<CombatUnit>();
            motor = GetComponent<UnitMotor>();
            attacks = GetComponent<BasicAttackController>();
            commands = GetComponent<PlayerCommandController>();
            retreat = GetComponent<OperatorRetreatController>();
            match = FindFirstObjectByType<MatchOutcomeController>();
            visual = transform.Find("NiuLaiVisual");
            if (visual) visualRest = visual.localPosition;
            commands.SetSkillInputHandler(this);
            attacks.UsesExternalDamage = true;
            // Keep timing/target resolution in BasicAttackController, but resolve melee immediately.
            attacks.AttackRequested += OnMeleeAttack;
        }

        private void OnMeleeAttack(CombatUnit attacker, CombatUnit target)
        {
            if (!CanAct || IsFlying || !TargetRules.IsLegal(owner, target)) return;
            float multiplier = IsEmpowered ? 2.5f : 1f;
            empowered = 0;
            target.TakePhysicalDamage(Mathf.Max(1, owner.AttackPower * multiplier - target.Defense), owner);
        }

        public void ActivateEmpower()
        {
            if (!CanAct || IsFlying || WCooldown > 0) return;
            empowered = 5;
            WCooldown = 6;
        }
        public void HandleSkill2() { if (CanAct && !IsFlying && ECooldown <= 0) targeting = targeting == 2 ? 0 : 2; }
        public void HandleSkill3() { if (CanAct && !IsFlying && RCooldown <= 0) targeting = targeting == 3 ? 0 : 3; }
        public bool TryHandleMoveClick(Vector3 point) => IsFlying;
        public bool TryHandleCancel() { bool was = targeting != 0; targeting = 0; return was; }
        public void HandleStop() { targeting = 0; }

        public bool TryHandleConfirm(Vector3 point, GameObject hitObject)
        {
            if (IsFlying) return true;
            if (targeting == 0) return false;
            if (!CanAct) { targeting = 0; return true; }
            Vector3 start = transform.position;
            point.y = start.y;
            Vector3 delta = point - start;
            float range = targeting == 2 ? 5.5f : 7f;
            point = start + Vector3.ClampMagnitude(delta, range);
            Vector3 clamped = ArenaLayout.CreateDefault().Clamp(point);
            point.x = clamped.x; point.z = clamped.z;
            if (!IsLegalLanding(point, owner)) return true;
            if (targeting == 2)
            {
                if (Vector3.Distance(start, point) < 0.1f) return true;
                commands.CancelCurrentCommand(); attacks.ClearTarget(); motor.Stop();
                flightStart = start; flightEnd = point; flightTime = 0;
                IsFlying = true; motor.enabled = false; attacks.enabled = false;
                ECooldown = 14;
            }
            else
            {
                RCooldown = 36;
                var effect = new GameObject("MamaImpact").AddComponent<NiuLaiMamaImpact>();
                effect.Initialize(owner, new Vector3(point.x, 0, point.z), owner.AttackPower, visual);
            }
            targeting = 0;
            return true;
        }

        public static bool IsLegalLanding(Vector3 point, CombatUnit self)
        {
            foreach (Collider collider in Physics.OverlapCapsule(point + Vector3.down * 0.3f, point + Vector3.up * 0.3f, 0.3f))
            {
                if (collider.isTrigger || self && collider.transform.IsChildOf(self.transform)) continue;
                if (collider.gameObject.layer == ExusiaiSkillController.ReservedObstacleLayerIndex || collider.GetComponentInParent<CombatUnit>()) return false;
            }
            return true;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (!owner || owner.IsDead || match && match.IsEnding)
            {
                CancelFlight(); empowered = 0; targeting = 0; return;
            }
            if (retreat && retreat.IsGuiding) targeting = 0;
            dt = Mathf.Max(0, dt);
            WCooldown = Mathf.Max(0, WCooldown - dt); ECooldown = Mathf.Max(0, ECooldown - dt);
            RCooldown = Mathf.Max(0, RCooldown - dt); empowered = Mathf.Max(0, empowered - dt);
            if (!IsFlying) return;
            flightTime += dt;
            float t = Mathf.Clamp01(flightTime / 0.65f);
            transform.position = Vector3.Lerp(flightStart, flightEnd, t);
            if (visual) visual.localPosition = visualRest + Vector3.up * (2f * Mathf.Sin(t * Mathf.PI) / transform.lossyScale.y);
            if (t < 1) return;
            for (int step = 0; step <= 55; step++)
            {
                Vector3 candidate = Vector3.Lerp(flightEnd, flightStart, step / 55f);
                if (IsLegalLanding(candidate, owner)) { transform.position = candidate; break; }
            }
            CancelFlight();
        }

        private void CancelFlight()
        {
            if (!IsFlying) return;
            IsFlying = false;
            if (visual) visual.localPosition = visualRest;
            if (owner && !owner.IsDead && (!match || !match.IsEnding)) { motor.enabled = true; attacks.enabled = !retreat || !retreat.IsGuiding; }
        }
        private void OnDisable() { CancelFlight(); empowered = 0; targeting = 0; }
        private void OnDestroy() { if (attacks) attacks.AttackRequested -= OnMeleeAttack; }
    }
}
