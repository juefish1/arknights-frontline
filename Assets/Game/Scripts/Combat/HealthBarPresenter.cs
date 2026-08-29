using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArknightsFrontline.Combat
{
    public sealed class HealthBarPresenter : MonoBehaviour
    {
        private const float Width = 80f;
        private const float Height = 12f;
        private const float WorldScale = 0.01f;
        private const float HeadOffset = 0.35f;

        private CombatUnit combatUnit;
        private Transform barTransform;
        private Image fillImage;

        public float FillAmount => fillImage == null ? 0f : fillImage.fillAmount;

        public bool IsVisible => barTransform != null && barTransform.gameObject.activeSelf;

        private void Awake()
        {
            CombatUnit unit = GetComponent<CombatUnit>();
            if (unit != null)
            {
                Configure(unit);
            }
        }

        private void Update()
        {
            Tick();
        }

        public void Configure(CombatUnit unit)
        {
            combatUnit = unit ?? throw new ArgumentNullException(nameof(unit));
            EnsureVisuals();
        }

        public void Tick()
        {
            if (combatUnit == null)
            {
                return;
            }

            EnsureVisuals();
            fillImage.fillAmount = Mathf.Clamp01(combatUnit.CurrentHealth / combatUnit.MaxHealth);
            PositionAboveUnit();
            FaceMainCamera();
        }

        private void EnsureVisuals()
        {
            if (barTransform != null)
            {
                return;
            }

            GameObject bar = new GameObject("HealthBar", typeof(RectTransform), typeof(Canvas));
            barTransform = bar.transform;
            barTransform.SetParent(transform, false);
            barTransform.localScale = Vector3.one * WorldScale;

            RectTransform barRect = (RectTransform)barTransform;
            barRect.sizeDelta = new Vector2(Width, Height);

            Canvas canvas = bar.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            CreateImage("Background", barTransform, new Color(0.1f, 0.1f, 0.1f, 1f));
            fillImage = CreateImage("Fill", barTransform, Color.green);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        private void PositionAboveUnit()
        {
            Renderer renderer = combatUnit.transform.root.GetComponent<Renderer>();
            if (renderer != null)
            {
                barTransform.position = new Vector3(
                    renderer.bounds.center.x,
                    renderer.bounds.max.y + HeadOffset,
                    renderer.bounds.center.z);
                return;
            }

            barTransform.position = combatUnit.transform.position + Vector3.up * 1.5f;
        }

        private void FaceMainCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                barTransform.LookAt(mainCamera.transform);
            }
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform imageRect = (RectTransform)imageObject.transform;
            imageRect.SetParent(parent, false);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }
    }
}
