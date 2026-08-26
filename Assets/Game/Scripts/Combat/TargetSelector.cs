using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public static class TargetSelector
    {
        public static CombatUnit FindNearestInRange(CombatUnit attacker)
        {
            if (attacker == null)
            {
                return null;
            }

            CombatUnit nearest = null;
            float nearestDistance = float.PositiveInfinity;
            Vector3 attackerPosition = attacker.transform.position;
            Vector2 attackerHorizontalPosition = new Vector2(attackerPosition.x, attackerPosition.z);

            foreach (CombatUnit candidate in Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.InstanceID))
            {
                if (!TargetRules.IsLegal(attacker, candidate))
                {
                    continue;
                }

                Vector3 candidatePosition = candidate.transform.position;
                float distance = Vector2.Distance(
                    attackerHorizontalPosition,
                    new Vector2(candidatePosition.x, candidatePosition.z));
                if (distance <= attacker.AttackRange && distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }
    }
}
