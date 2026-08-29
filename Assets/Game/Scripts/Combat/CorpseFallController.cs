using System;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class CorpseFallController : MonoBehaviour
    {
        private Vector3 startPosition;
        private Vector3 landingPosition;
        private float duration;
        private float elapsed;

        public bool HasLanded { get; private set; }

        public void Configure(Vector3 landingPosition, float duration)
        {
            if (duration <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            startPosition = transform.position;
            this.landingPosition = landingPosition;
            this.duration = duration;
            elapsed = 0f;
            HasLanded = false;
            enabled = true;
        }

        public void Tick(float deltaTime)
        {
            elapsed += Mathf.Max(0f, deltaTime);
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPosition, landingPosition, progress);
            if (progress >= 1f)
            {
                transform.position = landingPosition;
                HasLanded = true;
                enabled = false;
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
