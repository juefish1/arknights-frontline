using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace ArknightsFrontline.Skills
{
    [DisallowMultipleComponent]
    public sealed class SkillHudPresenter : MonoBehaviour
    {
        private const float SlotWidth = 120f;
        private const float SlotHeight = 64f;
        private const float SlotSpacing = 8f;
        private readonly GameObject[] slots = new GameObject[3];
        private readonly Text[] labels = new Text[3];
        private readonly Image[] overlays = new Image[3];
        private readonly Outline[] outlines = new Outline[3];

        [SerializeField] private ExusiaiSkillController controller;
        private GameObject hudRoot;
        private GameObject ownedCanvas;
        private GameObject statusObject;
        private Text statusLabel;
        private bool ownsHudRoot;

        public string WLabel { get; private set; } = "W  0/3";
        public string ELabel { get; private set; } = "E  READY";
        public string RLabel { get; private set; } = "R  0.0";
        public string StatusText { get; private set; } = string.Empty;
        public ExusiaiSkillController BoundController => controller;
        public bool HasVisibleSkillControls => isActiveAndEnabled && controller != null
            && slots[0] != null && slots[0].activeSelf;
        public int SlotCount => hudRoot == null ? 0 : slots.Length;
        public bool IsVisible => isActiveAndEnabled && hudRoot != null
            && (ownsHudRoot ? hudRoot.activeSelf : HasVisibleSkillControls || statusObject != null && statusObject.activeSelf);

        private void Awake()
        {
            if (controller != null) Configure(controller);
        }

        private void OnEnable()
        {
            if (controller != null) Refresh();
            else UpdateVisibility();
        }

        private void Update()
        {
            if (controller != null) Refresh();
        }

        private void OnDisable()
        {
            UpdateVisibility();
        }

        private void OnDestroy()
        {
            if (ownsHudRoot && hudRoot != null) Destroy(hudRoot);
            if (ownedCanvas != null) Destroy(ownedCanvas);
        }

        public void Configure(ExusiaiSkillController skillController)
        {
            BindOperator(skillController);
        }

        public void ConfigureControllerReference(ExusiaiSkillController skillController)
        {
            BindOperator(skillController);
        }

        public void BindOperator(ExusiaiSkillController skillController)
        {
            controller = skillController;
            if (controller != null || !string.IsNullOrEmpty(StatusText))
            {
                EnsureHud();
            }

            Refresh();
        }

        public void SetDeploymentStatus(string statusText)
        {
            StatusText = statusText ?? string.Empty;
            if (!string.IsNullOrEmpty(StatusText) || hudRoot != null)
            {
                EnsureHud();
            }

            if (statusLabel != null)
            {
                statusLabel.text = StatusText;
            }

            UpdateVisibility();
        }

        public void Refresh()
        {
            if (!isActiveAndEnabled)
            {
                UpdateVisibility();
                return;
            }

            if (controller == null)
            {
                UpdateVisibility();
                return;
            }

            EnsureHud();
            ExusiaiSkillSnapshot snapshot = controller.Snapshot;
            WLabel = snapshot.IsSweepReady ? "W  READY" : $"W  {snapshot.SweepProgress}/3";
            ELabel = FormatCharge(snapshot);
            RLabel = snapshot.IsOverloadActive
                ? $"R  ACTIVE {FormatSeconds(snapshot.OverloadDuration)}"
                : $"R  {FormatSeconds(snapshot.OverloadCooldown)}";
            SetSlot(0, WLabel, snapshot.IsSweepReady, !snapshot.IsSweepReady);
            SetSlot(1, ELabel, snapshot.ChargePhase == ExusiaiChargePhase.Ready, snapshot.ChargePhase == ExusiaiChargePhase.Cooldown);
            SetSlot(2, RLabel, snapshot.IsOverloadActive || (!snapshot.IsOverloadActive && snapshot.OverloadCooldown <= 0f), !snapshot.IsOverloadActive && snapshot.OverloadCooldown > 0f);
            UpdateVisibility();
        }

        private static string FormatCharge(ExusiaiSkillSnapshot snapshot)
        {
            switch (snapshot.ChargePhase)
            {
                case ExusiaiChargePhase.Ready: return "E  READY";
                case ExusiaiChargePhase.Targeting: return "E  SELECT DEST";
                default: return $"E  {FormatSeconds(snapshot.ChargeCooldown)}";
            }
        }

        private static string FormatSeconds(float seconds)
        {
            return Mathf.Max(0f, seconds).ToString("0.0", CultureInfo.InvariantCulture);
        }

        private void EnsureHud()
        {
            if (hudRoot != null) return;
            RectTransform suppliedRoot = transform as RectTransform;
            if (suppliedRoot != null)
            {
                hudRoot = gameObject;
                ownsHudRoot = false;
                ConfigureRoot(suppliedRoot);
                for (int index = 0; index < slots.Length; index++) CreateSlot(index, suppliedRoot);
                CreateStatus(suppliedRoot);
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                ownedCanvas = new GameObject("SkillHudCanvas", typeof(Canvas));
                canvas = ownedCanvas.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            hudRoot = new GameObject("SkillHud", typeof(RectTransform));
            ownsHudRoot = true;
            RectTransform root = hudRoot.GetComponent<RectTransform>();
            root.SetParent(canvas.transform, false);
            ConfigureRoot(root);
            for (int index = 0; index < slots.Length; index++) CreateSlot(index, root);
            CreateStatus(root);
        }

        private static void ConfigureRoot(RectTransform root)
        {
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 24f);
            root.sizeDelta = new Vector2(SlotWidth * 3f + SlotSpacing * 2f, SlotHeight + 30f);
        }

        private void CreateSlot(int index, RectTransform root)
        {
            GameObject slot = new GameObject(index == 0 ? "W" : index == 1 ? "E" : "R", typeof(RectTransform), typeof(Image), typeof(Outline));
            slots[index] = slot;
            RectTransform rect = slot.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(index * (SlotWidth + SlotSpacing), 0f);
            rect.sizeDelta = new Vector2(SlotWidth, SlotHeight);
            Image background = slot.GetComponent<Image>();
            background.color = new Color(0.07f, 0.11f, 0.18f, 0.92f);
            background.raycastTarget = false;
            outlines[index] = slot.GetComponent<Outline>();
            outlines[index].effectColor = new Color(0.15f, 0.55f, 1f, 1f);
            outlines[index].effectDistance = new Vector2(2f, -2f);

            GameObject overlayObject = new GameObject("Cooldown", typeof(RectTransform), typeof(Image));
            overlayObject.transform.SetParent(slot.transform, false);
            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlays[index] = overlayObject.GetComponent<Image>();
            overlays[index].color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
            overlays[index].raycastTarget = false;

            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(slot.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            labels[index] = textObject.GetComponent<Text>();
            labels[index].font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labels[index].fontSize = 18;
            labels[index].alignment = TextAnchor.MiddleCenter;
            labels[index].color = Color.white;
            labels[index].raycastTarget = false;
        }

        private void CreateStatus(RectTransform root)
        {
            statusObject = new GameObject("DeploymentStatus", typeof(RectTransform));
            statusObject.transform.SetParent(root, false);
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(statusObject.transform, false);
            RectTransform containerRect = statusObject.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0f);
            containerRect.anchorMax = new Vector2(0.5f, 0f);
            containerRect.pivot = new Vector2(0.5f, 0f);
            containerRect.anchoredPosition = new Vector2(0f, SlotHeight + 3f);
            containerRect.sizeDelta = new Vector2(SlotWidth * 3f + SlotSpacing * 2f, 24f);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            statusLabel = labelObject.GetComponent<Text>();
            statusLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusLabel.fontSize = 18;
            statusLabel.alignment = TextAnchor.MiddleCenter;
            statusLabel.color = Color.white;
            statusLabel.raycastTarget = false;
            statusLabel.text = StatusText;
        }

        private void SetSlot(int index, string text, bool ready, bool cooldown)
        {
            labels[index].text = text;
            outlines[index].enabled = ready;
            overlays[index].enabled = cooldown;
        }

        private void UpdateVisibility()
        {
            if (hudRoot == null)
            {
                return;
            }

            bool showControls = isActiveAndEnabled && controller != null;
            bool showStatus = isActiveAndEnabled && !string.IsNullOrEmpty(StatusText);
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index] != null)
                {
                    slots[index].SetActive(showControls);
                }
            }

            if (statusObject != null)
            {
                statusObject.SetActive(showStatus);
            }

            if (ownsHudRoot)
            {
                hudRoot.SetActive(showControls || showStatus);
            }
        }
    }
}
