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
        private PhysicalDamagePayload payload;
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
            Initialize(
                combatAttacker,
                combatTarget,
                new PhysicalDamagePayload(projectileDamage, 1f, 0f, 1f, 0f),
                projectileSpeed);
        }

        public void Initialize(
            CombatUnit combatAttacker,
            CombatUnit combatTarget,
            PhysicalDamagePayload projectilePayload,
            float projectileSpeed)
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
            payload = projectilePayload;
            speed = Mathf.Max(MinimumSpeed, projectileSpeed);
            var spawnPoint = attacker.GetComponent<ProjectileSpawnPoint>();
            transform.position = spawnPoint ? spawnPoint.ResolvePosition() : attacker.transform.position;
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
                float missingHealth = Mathf.Max(0f, target.MaxHealth - target.CurrentHealth);
                float rawDamage = payload.AttackPower * payload.DamageMultiplier
                    + missingHealth * payload.MissingHealthRatio;
                float resolvedDamage = Mathf.Round(Mathf.Max(1f, rawDamage - target.Defense) * 100f) / 100f;
                target.TakePhysicalDamage(resolvedDamage, attacker);

                if (!target.IsDead && payload.SlowDuration > 0f && payload.MovementSlowMultiplier < 1f)
                {
                    TimedStatModifierController effects = target.GetComponent<TimedStatModifierController>();
                    if (effects == null)
                    {
                        effects = target.gameObject.AddComponent<TimedStatModifierController>();
                    }

                    effects.ApplyMovementSlow("Exusiai.E.Slow", payload.MovementSlowMultiplier, payload.SlowDuration);
                }
            }

            Finish();
        }

        public void Cancel()
        {
            IsFinished = true;
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
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
            Cancel();
        }
    }
}
