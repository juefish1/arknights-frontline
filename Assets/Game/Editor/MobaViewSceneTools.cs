using System;
using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Camera;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ArknightsFrontline.Editor
{
    public static class MobaViewSceneTools
    {
        private const string ScenePath = "Assets/Game/Scenes/PrototypeArena.unity";

        [MenuItem("Arknights Frontline/Upgrade MOBA View")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            GameObject[] roots = scene.GetRootGameObjects();
            GameObject arena = roots.Single(root => root.name == "ArenaBootstrap");
            var roster = new SerializedObject(arena.GetComponent<ArenaRosterBootstrap>());
            SerializedProperty slots = roster.FindProperty("slots");
            for (int i = 0; i < slots.arraySize; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                var template = (GameObject)slot.FindPropertyRelative("template").objectReferenceValue;
                template.transform.localScale = ArenaVisualMetrics.OperatorScale;
                Vector3 position = template.transform.position;
                position.y = ArenaVisualMetrics.OperatorCenterHeight;
                template.transform.position = position;
                SerializedProperty deployment = slot.FindPropertyRelative("deploymentPosition");
                Vector3 deploymentPosition = deployment.vector3Value;
                deploymentPosition.y = ArenaVisualMetrics.OperatorCenterHeight;
                deployment.vector3Value = deploymentPosition;
            }
            roster.ApplyModifiedPropertiesWithoutUndo();

            var arenaBootstrap = arena.GetComponent<ArenaBootstrap>();
            foreach (Transform tower in new[] { arenaBootstrap.BlueTower, arenaBootstrap.RedTower })
            {
                var collider = tower.GetComponent<BoxCollider>();
                collider.center = ArenaVisualMetrics.TowerCenter;
                collider.size = ArenaVisualMetrics.TowerSize;
                Transform visual = tower.Find(tower.name + "Visual");
                visual.localPosition = ArenaVisualMetrics.TowerCenter;
                visual.localScale = ArenaVisualMetrics.TowerSize;
            }

            AddGroundPresentation(arena.transform);
            var controller = roots.SelectMany(root => root.GetComponentsInChildren<MobaCameraController>(true)).Single();
            var camera = controller.GetComponent<UnityEngine.Camera>();
            camera.orthographic = false;
            camera.fieldOfView = MobaCameraController.DefaultFieldOfView;
            controller.ConfigureOffset(MobaCameraController.DefaultOffset);
            controller.CenterOn(controller.CenteringTarget);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save the MOBA view upgrade.");
            AssetDatabase.SaveAssets();
            Debug.Log("MOBA view upgraded: 56 degree pitch, 20 unit height, 40 degree vertical FOV.");
        }

        public static void AddGroundPresentation(Transform arena)
        {
            // Keep the existing 100 x 24 ground collider as the movement surface.
            // The road and surround are visual only, so clicks cannot escape the arena.
            AddPlane(arena, "ArenaSurround", new Vector3(16f, 1f, 10f), -0.04f,
                "ArenaSurround.mat", new Color(0.115f, 0.16f, 0.13f));
            AddPlane(arena, "RoadSurface", new Vector3(10f, 1f, 1.2f), 0.015f,
                "RoadSurface.mat", new Color(0.32f, 0.34f, 0.30f));
        }

        private static void AddPlane(Transform arena, string name, Vector3 scale, float height,
            string materialName, Color color)
        {
            Transform existing = arena.Find(name);
            GameObject plane = existing == null ? GameObject.CreatePrimitive(PrimitiveType.Plane) : existing.gameObject;
            plane.name = name;
            plane.transform.SetParent(arena, false);
            plane.transform.localPosition = Vector3.up * height;
            plane.transform.localRotation = Quaternion.identity;
            plane.transform.localScale = scale;
            plane.layer = 0;
            Collider collider = plane.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            string path = "Assets/Game/Materials/" + materialName;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = color;
                material.SetFloat("_Smoothness", 0f);
                AssetDatabase.CreateAsset(material, path);
            }
            plane.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
