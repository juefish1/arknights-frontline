using System.Collections.Generic;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class TimedStatModifierController : MonoBehaviour
    {
        private readonly Dictionary<string, float> remainingDurations = new Dictionary<string, float>();
        private readonly List<string> expiredSources = new List<string>();
        private readonly List<KeyValuePair<string, float>> updatedDurations = new List<KeyValuePair<string, float>>();

        private UnitStatModifiers modifiers;
        private CombatUnit owner;
        private bool isStopped;

        private void Awake()
        {
            EnsureModifiers();
            SubscribeToOwnerDeath();
        }

        private void OnDestroy()
        {
            UnsubscribeFromOwnerDeath();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void ApplyMovementSlow(string source, float multiplier, float duration)
        {
            if (isStopped)
            {
                return;
            }

            EnsureModifiers();
            float clampedDuration = Mathf.Max(0f, duration);
            if (clampedDuration <= 0f)
            {
                remainingDurations.Remove(source);
                modifiers.RemoveSource(source);
                return;
            }

            modifiers.SetMovementSpeedMultiplier(source, Mathf.Clamp01(multiplier));
            remainingDurations[source] = clampedDuration;
        }

        public void Tick(float deltaTime)
        {
            if (isStopped || remainingDurations.Count == 0)
            {
                return;
            }

            EnsureModifiers();
            float clampedDeltaTime = Mathf.Max(0f, deltaTime);
            expiredSources.Clear();
            updatedDurations.Clear();
            foreach (KeyValuePair<string, float> entry in remainingDurations)
            {
                float remaining = entry.Value - clampedDeltaTime;
                if (remaining <= 0f)
                {
                    expiredSources.Add(entry.Key);
                }
                else
                {
                    updatedDurations.Add(new KeyValuePair<string, float>(entry.Key, remaining));
                }
            }

            foreach (KeyValuePair<string, float> entry in updatedDurations)
            {
                remainingDurations[entry.Key] = entry.Value;
            }

            foreach (string source in expiredSources)
            {
                remainingDurations.Remove(source);
                modifiers.RemoveSource(source);
            }
        }

        public void StopForMatch()
        {
            if (isStopped)
            {
                return;
            }

            EnsureModifiers();
            foreach (string source in remainingDurations.Keys)
            {
                modifiers.RemoveSource(source);
            }

            remainingDurations.Clear();
            expiredSources.Clear();
            updatedDurations.Clear();
            isStopped = true;
            UnsubscribeFromOwnerDeath();
        }

        private void EnsureModifiers()
        {
            if (modifiers == null)
            {
                modifiers = GetComponent<UnitStatModifiers>();
                if (modifiers == null)
                {
                    modifiers = gameObject.AddComponent<UnitStatModifiers>();
                }
            }
        }

        private void SubscribeToOwnerDeath()
        {
            owner = GetComponent<CombatUnit>();
            if (owner == null)
            {
                return;
            }

            owner.Died -= OnOwnerDied;
            owner.Died += OnOwnerDied;
        }

        private void UnsubscribeFromOwnerDeath()
        {
            if (owner != null)
            {
                owner.Died -= OnOwnerDied;
            }
        }

        private void OnOwnerDied(CombatUnit _)
        {
            StopForMatch();
        }
    }
}
