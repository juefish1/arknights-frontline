using System;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public static class TargetSelector
    {
        public static CombatUnit FindNearestInRange(CombatUnit attacker)
        {
            return FindNearestInRange(attacker, null);
        }

        public static CombatUnit FindNearestInRange(CombatUnit attacker, Predicate<CombatUnit> filter)
        {
            return FindNearestInRangeFromPoint(attacker, attacker == null ? Vector3.zero : attacker.transform.position, filter);
        }

        public static CombatUnit FindNearestInRangeFromPoint(CombatUnit attacker, Vector3 point)
        {
            return FindNearestInRangeFromPoint(attacker, point, null);
        }

        private static CombatUnit FindNearestInRangeFromPoint(CombatUnit attacker, Vector3 point, Predicate<CombatUnit> filter)
        {
            if (attacker == null)
            {
                return null;
            }

            CombatUnit nearest = null;
            float nearestDistance = float.PositiveInfinity;
            Vector3 attackerPosition = attacker.transform.position;
            Vector2 attackerHorizontalPosition = new Vector2(attackerPosition.x, attackerPosition.z);
            Vector2 pointHorizontalPosition = new Vector2(point.x, point.z);

            foreach (CombatUnit candidate in
                     UnityEngine.Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.InstanceID))
            {
                if (!TargetRules.IsLegal(attacker, candidate))
                {
                    continue;
                }

                if (filter != null && !filter(candidate))
                {
                    continue;
                }

                Vector3 candidatePosition = candidate.transform.position;
                float distanceFromAttacker = Vector2.Distance(
                    attackerHorizontalPosition,
                    new Vector2(candidatePosition.x, candidatePosition.z));
                float distanceFromPoint = Vector2.Distance(
                    pointHorizontalPosition,
                    new Vector2(candidatePosition.x, candidatePosition.z));
                if (distanceFromAttacker <= attacker.AttackRange && distanceFromPoint < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distanceFromPoint;
                }
            }

            return nearest;
        }
    }
}
