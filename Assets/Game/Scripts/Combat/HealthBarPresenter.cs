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
        private Sprite fillSprite;
        private Texture2D fillTexture;

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

        private void OnDestroy()
        {
            if (fillSprite != null)
            {
                DestroyRuntimeObject(fillSprite);
            }

            if (fillTexture != null)
            {
                DestroyRuntimeObject(fillTexture);
            }
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
            fillImage.sprite = CreateWhiteSprite();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        private void PositionAboveUnit()
        {
            Renderer owningRenderer = combatUnit.GetComponent<Renderer>();
            bool hasPhysicalBounds = IsPhysicalRenderer(owningRenderer);
            Bounds bounds = hasPhysicalBounds ? owningRenderer.bounds : default;

            if (!hasPhysicalBounds)
            {
                Renderer[] renderers = combatUnit.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    if (!IsPhysicalRenderer(renderer))
                    {
                        continue;
                    }

                    if (!hasPhysicalBounds)
                    {
                        bounds = renderer.bounds;
                        hasPhysicalBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            if (hasPhysicalBounds)
            {
                barTransform.position = new Vector3(
                    bounds.center.x,
                    bounds.max.y + HeadOffset,
                    bounds.center.z);
                return;
            }

            barTransform.position = combatUnit.transform.position + Vector3.up * 1.5f;
        }

        private static bool IsPhysicalRenderer(Renderer renderer)
        {
            return renderer != null
                && !(renderer is LineRenderer)
                && !(renderer is TrailRenderer);
        }

        private void FaceMainCamera()
        {
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
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

        private Sprite CreateWhiteSprite()
        {
            fillTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            fillTexture.SetPixel(0, 0, Color.white);
            fillTexture.Apply();
            fillSprite = Sprite.Create(fillTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            return fillSprite;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object runtimeObject)
        {
            if (Application.isPlaying)
            {
                Destroy(runtimeObject);
                return;
            }

            DestroyImmediate(runtimeObject);
        }
    }
}
