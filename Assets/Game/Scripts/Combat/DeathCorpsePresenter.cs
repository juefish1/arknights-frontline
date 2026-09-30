using System;
using ArknightsFrontline.Common;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class DeathCorpsePresenter : MonoBehaviour
    {
        private static readonly Vector3 DefaultCorpseScale = new Vector3(0.15f, 1f, 0.15f);

        [SerializeField] private CombatUnit combatUnit;
        [SerializeField] private Material corpseMaterial;
        [SerializeField] private int groundLayer = -1;
        [SerializeField] private Vector3 corpseScale = new Vector3(0.15f, 1f, 0.15f);
        [SerializeField] private UnitKind unitKind = ArknightsFrontline.Common.UnitKind.Operator;
        private bool hasSpawnedCorpse;

        public UnitKind UnitKind => unitKind;

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
            Configure(
                combatUnit,
                corpseMaterial,
                groundLayer,
                DefaultCorpseScale,
                ArknightsFrontline.Common.UnitKind.Operator);
        }

        public void Configure(
            CombatUnit combatUnit,
            Material corpseMaterial,
            int groundLayer,
            UnitKind unitKind)
        {
            Configure(combatUnit, corpseMaterial, groundLayer, DefaultCorpseScale, unitKind);
        }

        public void Configure(
            CombatUnit combatUnit,
            Material corpseMaterial,
            int groundLayer,
            Vector3 corpseScale)
        {
            Configure(
                combatUnit,
                corpseMaterial,
                groundLayer,
                corpseScale,
                ArknightsFrontline.Common.UnitKind.Operator);
        }

        public void Configure(
            CombatUnit combatUnit,
            Material corpseMaterial,
            int groundLayer,
            Vector3 corpseScale,
            UnitKind unitKind)
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
            this.corpseScale = corpseScale;
            this.unitKind = unitKind;
            SubscribeToDeath();
        }

        public void ConfigureFromTemplate(DeathCorpsePresenter templatePresenter, CombatUnit liveCombatUnit)
        {
            if (templatePresenter == null)
            {
                throw new ArgumentNullException(nameof(templatePresenter));
            }

            if (liveCombatUnit == null)
            {
                throw new ArgumentNullException(nameof(liveCombatUnit));
            }

            UnsubscribeFromDeath();
            corpseMaterial = templatePresenter.corpseMaterial;
            groundLayer = templatePresenter.groundLayer;
            corpseScale = templatePresenter.corpseScale;
            unitKind = templatePresenter.unitKind;
            combatUnit = liveCombatUnit;
            hasSpawnedCorpse = false;
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

            DestroyUnityObject(gameObject);
        }

        private GameObject CreateCorpse()
        {
            GameObject corpse = GameObject.CreatePrimitive(PrimitiveType.Plane);
            corpse.name = gameObject.name + "_Corpse";
            corpse.layer = 0;
            corpse.AddComponent<CorpseLifetimeController>().Configure(unitKind, combatUnit.gameObject.name);
            corpse.transform.position = transform.position;
            corpse.transform.localScale = corpseScale;
            corpse.GetComponent<Renderer>().sharedMaterial = corpseMaterial;

            Collider collider = corpse.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                DestroyUnityObject(collider);
            }

            return corpse;
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
                return;
            }

            DestroyImmediate(target);
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
