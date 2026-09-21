using ArknightsFrontline.Commands;
using ArknightsFrontline.Combat;
using UnityEngine;

namespace ArknightsFrontline.Skills
{
    public enum ExusiaiSkillIndicatorMode
    {
        None,
        ChargeTargeting,
        DashWindow
    }

    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class ExusiaiSkillIndicator : MonoBehaviour
    {
        private const int RangePoints = 65;
        private const int MarkerPoints = 33;
        private const float Height = 0.05f;
        private const float LineWidth = 0.06f;
        private const float DashRadius = 7f;
        private const float FadeDuration = 0.15f;

        private CombatUnit owner;
        private ExusiaiSkillController skills;
        private PlayerCommandController commands;
        private SkillDashController dash;
        private LineRenderer rangeRenderer;
        private LineRenderer arrowRenderer;
        private LineRenderer markerRenderer;
        private float fadeRemaining;
        private bool wasDashWindow;

        public ExusiaiSkillIndicatorMode Mode { get; private set; }
        public Vector3 DisplayedEndpoint { get; private set; }
        public int RangePointCount => rangeRenderer == null ? 0 : rangeRenderer.positionCount;
        public int MarkerPointCount => markerRenderer == null ? 0 : markerRenderer.positionCount;
        public float RangeRadius { get; private set; }
        public bool IsVisible => (rangeRenderer != null && rangeRenderer.enabled)
            || (arrowRenderer != null && arrowRenderer.enabled)
            || (markerRenderer != null && markerRenderer.enabled);

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void OnDisable()
        {
            Mode = ExusiaiSkillIndicatorMode.None;
            fadeRemaining = 0f;
            SetAllVisible(false);
        }

        private void OnDestroy()
        {
            DestroyRenderer(ref rangeRenderer);
            DestroyRenderer(ref arrowRenderer);
            DestroyRenderer(ref markerRenderer);
        }

        public void Configure(
            CombatUnit combatOwner,
            ExusiaiSkillController skillController,
            PlayerCommandController commandController,
            SkillDashController dashController)
        {
            owner = combatOwner;
            skills = skillController;
            commands = commandController;
            dash = dashController;
            EnsureRenderers();
            Refresh();
        }

        public void Refresh()
        {
            if (!isActiveAndEnabled || owner == null || skills == null || dash == null)
            {
                Mode = ExusiaiSkillIndicatorMode.None;
                SetAllVisible(false);
                return;
            }

            if (skills.IsSelectingChargeTarget)
            {
                fadeRemaining = 0f;
                Mode = ExusiaiSkillIndicatorMode.ChargeTargeting;
                wasDashWindow = false;
                ShowChargeTargeting();
                return;
            }

            if (skills.IsDashWindowOpen)
            {
                fadeRemaining = 0f;
                Mode = ExusiaiSkillIndicatorMode.DashWindow;
                wasDashWindow = true;
                ShowDashWindow();
                return;
            }

            Mode = ExusiaiSkillIndicatorMode.None;
            if (wasDashWindow && arrowRenderer != null && arrowRenderer.enabled)
            {
                fadeRemaining = FadeDuration;
            }
            wasDashWindow = false;
            if (fadeRemaining > 0f)
            {
                fadeRemaining = Mathf.Max(0f, fadeRemaining - Time.deltaTime);
                SetFade(fadeRemaining / FadeDuration);
                if (fadeRemaining > 0f) return;
            }
            SetAllVisible(false);
        }

        private void ShowChargeTargeting()
        {
            EnsureRenderers();
            Vector3 point = GetPointerPoint(out bool hasPoint);
            Vector3 center = GetOwnerPosition();
            RangeRadius = owner.AttackRange;
            if (!hasPoint)
            {
                DisplayedEndpoint = center;
                SetAllVisible(false);
                return;
            }
            bool inRange = hasPoint && HorizontalDistance(center, point) <= RangeRadius;
            Color color = inRange ? Color.blue : Color.red;
            DrawLoop(rangeRenderer, center, RangeRadius, RangePoints, color);
            rangeRenderer.enabled = true;
            arrowRenderer.enabled = false;

            CombatUnit selected = skills.SelectedChargeTarget;
            Vector3 markerCenter = selected != null ? selected.transform.position : point;
            DrawLoop(markerRenderer, markerCenter, 0.35f, MarkerPoints, color);
            markerRenderer.enabled = hasPoint || selected != null;
            DisplayedEndpoint = markerCenter;
        }

        private void ShowDashWindow()
        {
            EnsureRenderers();
            Vector3 point = GetPointerPoint(out bool hasPoint);
            Vector3 center = GetOwnerPosition();
            RangeRadius = DashRadius;
            if (!hasPoint)
            {
                DisplayedEndpoint = center;
                SetAllVisible(false);
                return;
            }
            Color color = Color.blue;
            Vector3 endpoint = center;
            bool valid = hasPoint && dash.Preview(point, out endpoint);
            if (!valid)
            {
                endpoint = center;
                color = Color.red;
            }
            DisplayedEndpoint = endpoint;
            DrawLoop(rangeRenderer, center, RangeRadius, RangePoints, color);
            rangeRenderer.enabled = true;
            DrawArrow(center, endpoint, color);
            arrowRenderer.enabled = true;
            DrawLoop(markerRenderer, endpoint, 0.35f, MarkerPoints, color);
            markerRenderer.enabled = true;
        }

        private Vector3 GetPointerPoint(out bool hasPoint)
        {
            if (commands != null && commands.TryGetCachedPointerHit(out RaycastHit hit))
            {
                hasPoint = true;
                return hit.point;
            }
            hasPoint = false;
            return GetOwnerPosition();
        }

        private void EnsureRenderers()
        {
            if (rangeRenderer == null) rangeRenderer = CreateRenderer("ExusiaiSkillRange", RangePoints, true);
            if (arrowRenderer == null) arrowRenderer = CreateRenderer("ExusiaiSkillArrow", 2, false);
            if (markerRenderer == null) markerRenderer = CreateRenderer("ExusiaiSkillMarker", MarkerPoints, true);
        }

        private LineRenderer CreateRenderer(string objectName, int points, bool loop)
        {
            GameObject rendererObject = new GameObject(objectName);
            rendererObject.layer = LayerMask.NameToLayer("Default");
            rendererObject.transform.SetParent(transform, false);
            LineRenderer renderer = rendererObject.AddComponent<LineRenderer>();
            renderer.positionCount = points;
            renderer.loop = loop;
            renderer.useWorldSpace = true;
            renderer.startWidth = LineWidth;
            renderer.endWidth = LineWidth;
            renderer.enabled = false;
            return renderer;
        }

        private static void DrawLoop(LineRenderer renderer, Vector3 center, float radius, int points, Color color)
        {
            center.y = Height;
            renderer.startColor = color;
            renderer.endColor = color;
            for (int index = 0; index < points; index++)
            {
                float angle = index * Mathf.PI * 2f / (points - 1);
                renderer.SetPosition(index, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }

        private void DrawArrow(Vector3 start, Vector3 endpoint, Color color)
        {
            start.y = Height;
            endpoint.y = Height;
            arrowRenderer.startColor = color;
            arrowRenderer.endColor = color;
            arrowRenderer.SetPosition(0, start);
            arrowRenderer.SetPosition(1, endpoint);
        }

        private void SetFade(float alpha)
        {
            if (arrowRenderer == null) return;
            Color color = arrowRenderer.startColor;
            color.a = alpha;
            arrowRenderer.startColor = color;
            arrowRenderer.endColor = color;
            rangeRenderer.enabled = false;
            markerRenderer.enabled = false;
        }

        private void SetAllVisible(bool visible)
        {
            if (rangeRenderer != null) rangeRenderer.enabled = visible;
            if (arrowRenderer != null) arrowRenderer.enabled = visible;
            if (markerRenderer != null) markerRenderer.enabled = visible;
        }

        private Vector3 GetOwnerPosition()
        {
            return owner == null ? Vector3.zero : owner.transform.position;
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            return Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z));
        }

        private static void DestroyRenderer(ref LineRenderer renderer)
        {
            if (renderer != null) Destroy(renderer.gameObject);
            renderer = null;
        }
    }
}
