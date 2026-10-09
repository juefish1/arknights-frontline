using UnityEngine;
using ArknightsFrontline.Skills;

namespace ArknightsFrontline.Combat
{
    public sealed class NiuLaiPresentation : MonoBehaviour
    {
        private Transform visual;
        private Vector3 previous;
        private CombatUnit owner;
        private BasicAttackController attacks;
        private void Awake() { visual = transform.Find("NiuLaiVisual"); previous = transform.position; owner = GetComponent<CombatUnit>(); attacks = GetComponent<BasicAttackController>(); }
        private void LateUpdate()
        {
            Vector3 direction = transform.position - previous; previous = transform.position;
            if (!visual || owner.IsDead) return;
            if (attacks.CurrentTarget) direction = attacks.CurrentTarget.transform.position - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.00001f) visual.rotation = Quaternion.RotateTowards(visual.rotation, Quaternion.LookRotation(direction), 720 * Time.deltaTime);
        }
    }
}
