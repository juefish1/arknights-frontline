using System;
using ArknightsFrontline.Common;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class DeathCorpsePresenter : MonoBehaviour
    {
        private CombatUnit combatUnit;
        private Material corpseMaterial;
        private int groundLayer;
        private bool hasSpawnedCorpse;

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

            if (this.combatUnit != null)
            {
                this.combatUnit.Died -= OnUnitDied;
            }

            this.combatUnit = combatUnit;
            this.corpseMaterial = corpseMaterial;
            this.groundLayer = groundLayer;
            this.combatUnit.Died += OnUnitDied;
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

        private void OnDestroy()
        {
            if (combatUnit != null)
            {
                combatUnit.Died -= OnUnitDied;
            }
        }
    }
}
