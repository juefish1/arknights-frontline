using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class UnitStatModifiers : MonoBehaviour
    {
        private readonly Dictionary<string, float> attackPowerMultipliers = new Dictionary<string, float>();
        private readonly Dictionary<string, float> movementSpeedMultipliers = new Dictionary<string, float>();
        private readonly Dictionary<string, float> attackIntervalOffsets = new Dictionary<string, float>();

        public void SetAttackPowerMultiplier(string source, float multiplier)
        {
            ValidateSource(source);
            attackPowerMultipliers[source] = Mathf.Max(0f, multiplier);
        }

        public void SetMovementSpeedMultiplier(string source, float multiplier)
        {
            ValidateSource(source);
            movementSpeedMultipliers[source] = Mathf.Max(0f, multiplier);
        }

        public void SetAttackIntervalOffset(string source, float offset)
        {
            ValidateSource(source);
            attackIntervalOffsets[source] = offset;
        }

        public void RemoveSource(string source)
        {
            ValidateSource(source);
            attackPowerMultipliers.Remove(source);
            movementSpeedMultipliers.Remove(source);
            attackIntervalOffsets.Remove(source);
        }

        public void Clear()
        {
            attackPowerMultipliers.Clear();
            movementSpeedMultipliers.Clear();
            attackIntervalOffsets.Clear();
        }

        public float ApplyAttackPower(float baseValue)
        {
            return Mathf.Max(0f, baseValue * Product(attackPowerMultipliers));
        }

        public float ApplyMovementSpeed(float baseValue)
        {
            return Mathf.Max(0f, baseValue * Product(movementSpeedMultipliers));
        }

        public float ApplyAttackInterval(float baseValue)
        {
            return Mathf.Max(0.05f, baseValue + Sum(attackIntervalOffsets));
        }

        private static void ValidateSource(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentException("Modifier source cannot be null or empty.", nameof(source));
            }
        }

        private static float Product(Dictionary<string, float> values)
        {
            float product = 1f;
            foreach (float value in values.Values)
            {
                product *= value;
            }

            return product;
        }

        private static float Sum(Dictionary<string, float> values)
        {
            float sum = 0f;
            foreach (float value in values.Values)
            {
                sum += value;
            }

            return sum;
        }
    }
}
