using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Camera;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArknightsFrontline.Editor
{
    public static class PrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/PrototypeArena.unity";
        private const string MaterialsPath = "Assets/Game/Materials";

        [MenuItem("Arknights Frontline/Build Prototype Arena")]
        public static void Build()
        {
            EnsureFolder("Assets/Game", "Scenes");
            EnsureFolder("Assets/Game", "Materials");

            Material blueMaterial = GetOrCreateMaterial("BlueArena.mat", new Color(0.1f, 0.35f, 0.9f));
            Material redMaterial = GetOrCreateMaterial("RedArena.mat", new Color(0.9f, 0.15f, 0.15f));
            Material laneMaterial = GetOrCreateMaterial("Lane.mat", new Color(0.25f, 0.25f, 0.25f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ArenaLayout layout = ArenaLayout.CreateDefault();

            GameObject arenaRoot = new GameObject("ArenaBootstrap");
            ArenaBootstrap arena = arenaRoot.AddComponent<ArenaBootstrap>();

            CreateLane(arenaRoot.transform, laneMaterial);
            Transform blueTower = CreateTower(arenaRoot.transform, "BlueTower", layout.BlueTower, blueMaterial);
            Transform redTower = CreateTower(arenaRoot.transform, "RedTower", layout.RedTower, redMaterial);
            arena.AssignTowers(blueTower, redTower);
            CreateDeploymentMarker(arenaRoot.transform, "BlueDeployment", layout.BlueDeployment, blueMaterial);
            CreateDeploymentMarker(arenaRoot.transform, "RedDeployment", layout.RedDeployment, redMaterial);
            CreateDirectionalLight();
            CreateMainCamera();
            CreateUiRoots();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildScene();
            AssetDatabase.SaveAssets();
        }

        private static void CreateLane(Transform parent, Material material)
        {
            GameObject lane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            lane.name = "Lane";
            lane.transform.SetParent(parent, false);
            lane.transform.localScale = new Vector3(10f, 1f, 2.4f);
            lane.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Transform CreateTower(Transform parent, string towerName, Vector3 position, Material material)
        {
            GameObject tower = new GameObject(towerName);
            tower.transform.SetParent(parent, false);
            tower.transform.position = position;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = towerName + "Visual";
            visual.transform.SetParent(tower.transform, false);
            visual.transform.localPosition = new Vector3(0f, 3f, 0f);
            visual.transform.localScale = new Vector3(3f, 6f, 3f);
            visual.GetComponent<Renderer>().sharedMaterial = material;

            return tower.transform;
        }

        private static void CreateDeploymentMarker(Transform parent, string markerName, Vector3 position, Material material)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = markerName;
            marker.transform.SetParent(parent, false);
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(2f, 0.05f, 2f);
            marker.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateDirectionalLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateMainCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 35f, -28f),
                Quaternion.Euler(50f, 0f, 0f));
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.orthographic = false;
            cameraObject.AddComponent<MobaCameraController>();
        }

        private static void CreateUiRoots()
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();

            GameObject canvasObject = new GameObject("Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        private static Material GetOrCreateMaterial(string fileName, Color color)
        {
            string path = MaterialsPath + "/" + fileName;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader)
                {
                    name = fileName.Substring(0, fileName.Length - 4)
                };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureBuildScene()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(scene => scene.path == ScenePath);
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string path = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
