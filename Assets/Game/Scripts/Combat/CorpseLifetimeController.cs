using System;
using System.Collections.Generic;
using ArknightsFrontline.Common;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class CorpseLifetimeController : MonoBehaviour
    {
        public const float MinionLifetimeSeconds = 5f;

        private static readonly Dictionary<string, CorpseLifetimeController> OperatorCorpses =
            new Dictionary<string, CorpseLifetimeController>();

        private float elapsedLifetime;

        public UnitKind UnitKind { get; private set; }

        public string OwnerKey { get; private set; }

        public void Configure(UnitKind unitKind, string ownerKey)
        {
            if (unitKind == ArknightsFrontline.Common.UnitKind.Operator && string.IsNullOrEmpty(ownerKey))
            {
                throw new ArgumentException("Operator corpses require an owner key.", nameof(ownerKey));
            }

            UnregisterOperatorCorpse();
            UnitKind = unitKind;
            OwnerKey = ownerKey;
            elapsedLifetime = 0f;

            if (unitKind != ArknightsFrontline.Common.UnitKind.Operator)
            {
                return;
            }

            if (OperatorCorpses.TryGetValue(ownerKey, out CorpseLifetimeController existing) &&
                !ReferenceEquals(existing, this) && existing != null)
            {
                DestroyUnityObject(existing.gameObject);
            }

            OperatorCorpses[ownerKey] = this;
        }

        public void Tick(float deltaTime)
        {
            if (UnitKind != ArknightsFrontline.Common.UnitKind.Minion)
            {
                return;
            }

            elapsedLifetime += Mathf.Max(0f, deltaTime);
            if (elapsedLifetime >= MinionLifetimeSeconds)
            {
                DestroyUnityObject(gameObject);
            }
        }

        public static void ClearOperatorCorpse(string ownerKey)
        {
            if (string.IsNullOrEmpty(ownerKey) || !OperatorCorpses.TryGetValue(ownerKey, out CorpseLifetimeController corpse))
            {
                return;
            }

            OperatorCorpses.Remove(ownerKey);
            if (corpse != null)
            {
                DestroyUnityObject(corpse.gameObject);
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            UnregisterOperatorCorpse();
        }

        private void UnregisterOperatorCorpse()
        {
            if (UnitKind != ArknightsFrontline.Common.UnitKind.Operator || string.IsNullOrEmpty(OwnerKey))
            {
                return;
            }

            if (OperatorCorpses.TryGetValue(OwnerKey, out CorpseLifetimeController registered) &&
                ReferenceEquals(registered, this))
            {
                OperatorCorpses.Remove(OwnerKey);
            }
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
    }
}
