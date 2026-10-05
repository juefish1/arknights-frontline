using System;
using UnityEngine;

// Humanoid drives one leg chain. Preserve the original duplicate weighted chain
// and toe control transforms by following their corresponding mapped bones.
[DefaultExecutionOrder(1000)]
public sealed class ExusiaiHumanoidBoneSync : MonoBehaviour
{
    [Serializable]
    public struct Link
    {
        public Transform source;
        public Transform target;
        public Vector3 restPositionInSource;
        public Quaternion restRotationInSource;
    }

    [SerializeField] private Link[] links = Array.Empty<Link>();

    public void Configure(Link[] configuredLinks) => links = configuredLinks;

    public void Synchronize()
    {
        foreach (var link in links)
        {
            if (!link.source || !link.target) continue;
            link.target.SetPositionAndRotation(
                link.source.TransformPoint(link.restPositionInSource),
                link.source.rotation * link.restRotationInSource);
        }
    }

    private void LateUpdate() => Synchronize();
}
