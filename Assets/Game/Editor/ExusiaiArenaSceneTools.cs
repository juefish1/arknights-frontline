using System;
using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace ArknightsFrontline.Editor
{
    public static class ExusiaiArenaSceneTools
    {
        public const string PrefabPath = "Assets/Game/Characters/Exusiai/Model/Prefabs/Exusiai_Game_Humanoid.prefab";
        private const string IdlePath = "Assets/Game/Characters/Exusiai/Model/Animations/Exusiai_Idle_Humanoid.anim";

        public static GameObject AttachVisual(GameObject operatorRoot)
        {
            if (!operatorRoot || !operatorRoot.GetComponent<CombatUnit>() ||
                !operatorRoot.GetComponent<PlayerCommandController>() ||
                !operatorRoot.TryGetComponent(out OperatorIdentity identity) || identity.OperatorType != OperatorType.Exusiai)
                throw new ArgumentException("Only player-controlled Exusiai roots accept the formal visual.", nameof(operatorRoot));
            Vector3 rootScale = operatorRoot.transform.lossyScale;
            if (rootScale.x <= 0 || Mathf.Abs(rootScale.x - rootScale.y) > 0.001f || Mathf.Abs(rootScale.x - rootScale.z) > 0.001f)
                throw new ArgumentException("Exusiai requires a positive uniform root scale.", nameof(operatorRoot));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
            if (!prefab || !idle) throw new MissingReferenceException("Formal Exusiai prefab or idle clip is missing.");
            Transform existing = operatorRoot.transform.Find("ExusiaiVisual");
            if (existing)
            {
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existing.gameObject) != PrefabPath)
                    throw new InvalidOperationException("ExusiaiVisual is not the delivered formal prefab.");
                return existing.gameObject;
            }

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                visual.name = "ExusiaiVisual";
                var animator = visual.GetComponentInChildren<Animator>(true);
                if (!animator || !visual.GetComponent<ExusiaiPresentation>() || visual.GetComponentInChildren<ExusiaiPreviewDriver>(true))
                    throw new InvalidOperationException("Formal prefab has invalid presentation wiring.");
                animator.applyRootMotion = false;
                animator.transform.rotation = Quaternion.LookRotation(Vector3.right);
                var graph = PlayableGraph.Create("ArenaIdleCalibration");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                Bounds bounds = default;
                bool hasBounds = false;
                try
                {
                    var output = AnimationPlayableOutput.Create(graph, "Idle", animator);
                    var clip = AnimationClipPlayable.Create(graph, idle);
                    clip.SetApplyFootIK(false);
                    output.SetSourcePlayable(clip);
                    graph.Play();
                    graph.Evaluate(0);
                    animator.GetComponent<ExusiaiHumanoidBoneSync>()?.Synchronize();
                    foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        var mesh = new Mesh();
                        try
                        {
                            // Compensate the renderer scale; TransformPoint applies it once.
                            skin.BakeMesh(mesh, true);
                            mesh.RecalculateBounds();
                            skin.updateWhenOffscreen = true;
                            foreach (Vector3 vertex in mesh.vertices)
                            {
                                Vector3 point = skin.transform.TransformPoint(vertex);
                                if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                                else bounds.Encapsulate(point);
                            }
                        }
                        finally { Object.DestroyImmediate(mesh); }
                    }
                }
                finally { graph.Destroy(); }
                if (!hasBounds || !float.IsFinite(bounds.size.y) || bounds.size.y <= 0.01f)
                    throw new InvalidOperationException("Cannot calibrate Exusiai body geometry.");
                float worldScale = 2f * ArenaVisualMetrics.OperatorCenterHeight / bounds.size.y;
                visual.transform.SetParent(operatorRoot.transform, false);
                visual.transform.localScale = Vector3.one * (worldScale / rootScale.x);
                visual.transform.localPosition = new Vector3(0, (-operatorRoot.transform.position.y - bounds.min.y * worldScale) / rootScale.y, 0);
                visual.transform.localRotation = Quaternion.identity;
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                operatorRoot.GetComponent<Renderer>().enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                return visual;
            }
            catch { Object.DestroyImmediate(visual); throw; }
        }
    }
}
