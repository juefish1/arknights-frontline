using ArknightsFrontline.Arena;
using UnityEngine;

namespace ArknightsFrontline.Skills
{
    public static class DashPathResolver
    {
        private const float MinimumTravelDistance = 0.05f;
        private const float RayHeight = 0.5f;

        public static bool TryResolve(
            Vector3 start,
            Vector3 clickedPoint,
            float maxDistance,
            ArenaLayout layout,
            int obstacleMask,
            float clearance,
            out Vector3 endpoint)
        {
            Vector3 horizontalStart = new Vector3(start.x, 0f, start.z);
            endpoint = horizontalStart;

            Vector3 delta = clickedPoint - start;
            delta.y = 0f;
            float clickedDistance = delta.magnitude;
            if (clickedDistance < MinimumTravelDistance)
            {
                return false;
            }

            Vector3 direction = delta / clickedDistance;
            float travelDistance = Mathf.Min(clickedDistance, Mathf.Max(0f, maxDistance));
            Vector3 desiredEndpoint = horizontalStart + direction * travelDistance;
            Vector3 clampedEndpoint = layout.Clamp(desiredEndpoint);
            travelDistance *= BoundaryFraction(horizontalStart, desiredEndpoint, clampedEndpoint);

            if (obstacleMask != 0
                && Physics.Raycast(
                    start + Vector3.up * RayHeight,
                    direction,
                    out RaycastHit hit,
                    travelDistance,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                travelDistance = Mathf.Max(0f, hit.distance - Mathf.Max(0f, clearance));
            }

            if (travelDistance < MinimumTravelDistance)
            {
                return false;
            }

            endpoint = horizontalStart + direction * travelDistance;
            return true;
        }

        private static float BoundaryFraction(Vector3 start, Vector3 desired, Vector3 clamped)
        {
            float fraction = 1f;
            float xTravel = desired.x - start.x;
            if (!Mathf.Approximately(xTravel, 0f))
            {
                fraction = Mathf.Min(fraction, (clamped.x - start.x) / xTravel);
            }

            float zTravel = desired.z - start.z;
            if (!Mathf.Approximately(zTravel, 0f))
            {
                fraction = Mathf.Min(fraction, (clamped.z - start.z) / zTravel);
            }

            return Mathf.Clamp01(fraction);
        }
    }
}
