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

        private ExusiaiSkillController controller;
        private GameObject hudRoot;
        private GameObject ownedCanvas;

        public string WLabel { get; private set; } = "W  0/3";
        public string ELabel { get; private set; } = "E  READY";
        public string RLabel { get; private set; } = "R  0.0";
        public int SlotCount => hudRoot == null ? 0 : slots.Length;
        public bool IsVisible => hudRoot != null && hudRoot.activeSelf;

        private void OnEnable()
        {
            if (controller != null) Refresh();
        }

        private void OnDisable()
        {
            if (hudRoot != null) hudRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (hudRoot != null) Destroy(hudRoot);
            if (ownedCanvas != null) Destroy(ownedCanvas);
        }

        public void Configure(ExusiaiSkillController skillController)
        {
            controller = skillController;
            if (controller == null)
            {
                if (hudRoot != null) hudRoot.SetActive(false);
                return;
            }

            EnsureHud();
            Refresh();
        }

        public void Refresh()
        {
            if (controller == null || !isActiveAndEnabled)
            {
                if (hudRoot != null) hudRoot.SetActive(false);
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
            hudRoot.SetActive(true);
        }

        private static string FormatCharge(ExusiaiSkillSnapshot snapshot)
        {
            switch (snapshot.ChargePhase)
            {
                case ExusiaiChargePhase.Ready: return "E  READY";
                case ExusiaiChargePhase.Targeting: return "E  SELECT TARGET";
                case ExusiaiChargePhase.DashWindow: return "E  MOVE!";
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
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                ownedCanvas = new GameObject("SkillHudCanvas", typeof(Canvas));
                canvas = ownedCanvas.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            hudRoot = new GameObject("SkillHud", typeof(RectTransform));
            RectTransform root = hudRoot.GetComponent<RectTransform>();
            root.SetParent(canvas.transform, false);
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 24f);
            root.sizeDelta = new Vector2(SlotWidth * 3f + SlotSpacing * 2f, SlotHeight);
            for (int index = 0; index < slots.Length; index++) CreateSlot(index, root);
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

        private void SetSlot(int index, string text, bool ready, bool cooldown)
        {
            labels[index].text = text;
            outlines[index].enabled = ready;
            overlays[index].enabled = cooldown;
        }
    }
}
