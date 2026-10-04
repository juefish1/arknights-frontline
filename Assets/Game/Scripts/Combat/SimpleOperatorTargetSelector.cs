using System;
using System.Collections.Generic;
using ArknightsFrontline.Arena;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public static class SimpleOperatorTargetSelector
    {
        public static CombatUnit Select(
            CombatUnit owner,
            IEnumerable<CombatUnit> candidates,
            Func<CombatUnit, bool> wasRecentlyDamagedBy)
        {
            if (owner == null || candidates == null)
            {
                return null;
            }

            CombatUnit selected = null;
            int selectedPriority = int.MaxValue;
            float selectedDistanceSquared = float.PositiveInfinity;
            EntityId selectedId = default;
            Vector2 ownerPosition = HorizontalPosition(owner.transform.position);

            foreach (CombatUnit candidate in candidates)
            {
                if (!TargetRules.IsLegal(owner, candidate))
                {
                    continue;
                }

                int priority = GetPriority(candidate, wasRecentlyDamagedBy);
                if (priority == int.MaxValue)
                {
                    continue;
                }

                float distanceSquared = (HorizontalPosition(candidate.transform.position) - ownerPosition).sqrMagnitude;
                if (distanceSquared > owner.AttackRange * owner.AttackRange)
                {
                    continue;
                }

                EntityId candidateId = candidate.GetEntityId();
                if (priority < selectedPriority
                    || priority == selectedPriority && distanceSquared < selectedDistanceSquared
                    || priority == selectedPriority && distanceSquared == selectedDistanceSquared
                    && Comparer<EntityId>.Default.Compare(candidateId, selectedId) < 0)
                {
                    selected = candidate;
                    selectedPriority = priority;
                    selectedDistanceSquared = distanceSquared;
                    selectedId = candidateId;
                }
            }

            return selected;
        }

        public static CombatUnit FindNearestFriendlyMinion(CombatUnit owner, IEnumerable<CombatUnit> candidates)
        {
            if (owner == null || candidates == null)
            {
                return null;
            }

            CombatUnit nearest = null;
            float nearestDistanceSquared = float.PositiveInfinity;
            EntityId nearestId = default;
            Vector2 ownerPosition = HorizontalPosition(owner.transform.position);

            foreach (CombatUnit candidate in candidates)
            {
                if (candidate == null || candidate.Team != owner.Team || candidate.IsDead
                    || candidate.GetComponent<LaneMinionController>() == null)
                {
                    continue;
                }

                float distanceSquared = (HorizontalPosition(candidate.transform.position) - ownerPosition).sqrMagnitude;
                EntityId candidateId = candidate.GetEntityId();
                if (distanceSquared < nearestDistanceSquared
                    || distanceSquared == nearestDistanceSquared
                    && Comparer<EntityId>.Default.Compare(candidateId, nearestId) < 0)
                {
                    nearest = candidate;
                    nearestDistanceSquared = distanceSquared;
                    nearestId = candidateId;
                }
            }

            return nearest;
        }

        private static int GetPriority(CombatUnit candidate, Func<CombatUnit, bool> wasRecentlyDamagedBy)
        {
            if (candidate.GetComponent<OperatorIdentity>() != null)
            {
                return wasRecentlyDamagedBy != null && wasRecentlyDamagedBy(candidate) ? 0 : 1;
            }

            if (candidate.GetComponent<LaneMinionController>() != null)
            {
                return 2;
            }

            if (candidate.GetComponent<TowerCombatController>() != null)
            {
                return 3;
            }

            return int.MaxValue;
        }

        private static Vector2 HorizontalPosition(Vector3 position)
        {
            return new Vector2(position.x, position.z);
        }
    }
}
