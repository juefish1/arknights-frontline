using UnityEngine;

namespace ArknightsFrontline.Skills
{
    public sealed class SkillCountdown
    {
        public float Remaining { get; private set; }

        public bool IsReady => Remaining <= 0f;

        public void Reset(float delay)
        {
            Remaining = Mathf.Max(0f, delay);
        }

        public void Start(float duration)
        {
            Remaining = Mathf.Max(0f, duration);
        }

        public void Tick(float deltaTime)
        {
            Remaining = Mathf.Max(0f, Remaining - Mathf.Max(0f, deltaTime));
            if (Remaining <= 0.0001f)
            {
                Remaining = 0f;
            }
        }
    }
}
