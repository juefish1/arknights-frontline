using System;
using ArknightsFrontline.Common;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class DeathCorpsePresenter : MonoBehaviour
    {
        [SerializeField] private CombatUnit combatUnit;
        [SerializeField] private Material corpseMaterial;
        [SerializeField] private int groundLayer = -1;
        private bool hasSpawnedCorpse;

        private void Awake()
        {
            RestoreConfiguration();
        }

        private void OnEnable()
        {
            RestoreConfiguration();
            SubscribeToDeath();
        }

        private void OnDisable()
        {
            UnsubscribeFromDeath();
        }

        public void Configure(CombatUnit combatUnit, Material corpseMaterial, int groundLayer)
        {
            if (combatUnit == null)
            {
                throw new ArgumentNullException(nameof(combatUnit));
            }

            if (corpseMaterial == null)
            {
                throw new ArgumentNullException(nameof(corpseMaterial));
            }

            UnsubscribeFromDeath();
            this.combatUnit = combatUnit;
            this.corpseMaterial = corpseMaterial;
            this.groundLayer = groundLayer;
            SubscribeToDeath();
        }

        private void OnUnitDied(CombatUnit _)
        {
            if (hasSpawnedCorpse)
            {
                return;
            }

            hasSpawnedCorpse = true;
            GameObject corpse = CreateCorpse();
            if (combatUnit.Altitude == Altitude.Air)
            {
                corpse.AddComponent<CorpseFallController>().Configure(ResolveLandingPosition(), 0.3f);
            }
            else
            {
                corpse.transform.position = ResolveLandingPosition();
            }

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
            }

            Destroy(gameObject);
        }

        private GameObject CreateCorpse()
        {
            GameObject corpse = GameObject.CreatePrimitive(PrimitiveType.Plane);
            corpse.name = gameObject.name + "_Corpse";
            corpse.layer = 0;
            corpse.transform.position = transform.position;
            corpse.transform.localScale = new Vector3(0.15f, 1f, 0.15f);
            corpse.GetComponent<Renderer>().sharedMaterial = corpseMaterial;

            Collider collider = corpse.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            return corpse;
        }

        private Vector3 ResolveLandingPosition()
        {
            Vector3 landingPosition = transform.position;
            int groundLayerMask = 1 << groundLayer;
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 100f, groundLayerMask))
            {
                landingPosition.y = hit.point.y;
            }
            else
            {
                landingPosition.y = 0f;
            }

            landingPosition.y += 0.01f;
            return landingPosition;
        }

        private void RestoreConfiguration()
        {
            if (combatUnit == null)
            {
                combatUnit = GetComponent<CombatUnit>();
            }

            if (corpseMaterial == null)
            {
                Renderer renderer = GetComponent<Renderer>();
                if (renderer != null)
                {
                    corpseMaterial = renderer.sharedMaterial;
                }
            }

            if (groundLayer < 0)
            {
                int configuredGroundLayer = LayerMask.NameToLayer("Ground");
                groundLayer = configuredGroundLayer >= 0 ? configuredGroundLayer : 0;
            }
        }

        private void SubscribeToDeath()
        {
            if (!isActiveAndEnabled || combatUnit == null || corpseMaterial == null)
            {
                return;
            }

            combatUnit.Died -= OnUnitDied;
            combatUnit.Died += OnUnitDied;
        }

        private void UnsubscribeFromDeath()
        {
            if (combatUnit != null)
            {
                combatUnit.Died -= OnUnitDied;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromDeath();
        }
    }
}
