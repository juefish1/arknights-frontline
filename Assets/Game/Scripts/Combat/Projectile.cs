using System;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class Projectile : MonoBehaviour
    {
        private const float MinimumSpeed = 0.01f;

        private static Material sharedYellowMaterial;

        private CombatUnit attacker;
        private CombatUnit target;
        private float damage;
        private float speed;

        public bool IsFinished { get; private set; }

        private void Awake()
        {
            EnsureVisibleRenderer();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Initialize(CombatUnit combatAttacker, CombatUnit combatTarget, float projectileDamage, float projectileSpeed)
        {
            if (combatAttacker == null)
            {
                throw new ArgumentNullException(nameof(combatAttacker));
            }

            if (combatTarget == null)
            {
                throw new ArgumentNullException(nameof(combatTarget));
            }

            attacker = combatAttacker;
            target = combatTarget;
            damage = Mathf.Max(0f, projectileDamage);
            speed = Mathf.Max(MinimumSpeed, projectileSpeed);
            transform.position = attacker.transform.position;
            IsFinished = false;
        }

        public void Tick(float deltaTime)
        {
            if (IsFinished)
            {
                return;
            }

            if (!TargetRules.IsLegal(attacker, target))
            {
                Finish();
                return;
            }

            Vector3 currentPosition = transform.position;
            Vector3 targetPosition = target.transform.position;
            targetPosition.y = currentPosition.y;
            transform.position = Vector3.MoveTowards(
                currentPosition,
                targetPosition,
                speed * Mathf.Max(0f, deltaTime));

            if (transform.position != targetPosition)
            {
                return;
            }

            if (TargetRules.IsLegal(attacker, target))
            {
                target.TakePhysicalDamage(Mathf.Max(1f, damage - target.Defense));
            }

            Finish();
        }

        private void EnsureVisibleRenderer()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            meshFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            meshRenderer.sharedMaterial = GetSharedYellowMaterial();

            transform.localScale = Vector3.one * 0.25f;
        }

        private static Material GetSharedYellowMaterial()
        {
            if (sharedYellowMaterial != null)
            {
                return sharedYellowMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                throw new InvalidOperationException("No shader is available for projectile rendering.");
            }

            sharedYellowMaterial = new Material(shader)
            {
                name = "ProjectileYellow",
                hideFlags = HideFlags.DontSave
            };
            sharedYellowMaterial.color = Color.yellow;
            return sharedYellowMaterial;
        }

        private void Finish()
        {
            IsFinished = true;
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
        }
    }
}
