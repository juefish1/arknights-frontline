using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Camera;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
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
            int groundLayer = EnsureLayer("Ground");
            int targetableLayer = EnsureLayer("Targetable");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ArenaLayout layout = ArenaLayout.CreateDefault();

            GameObject arenaRoot = new GameObject("ArenaBootstrap");
            ArenaBootstrap arena = arenaRoot.AddComponent<ArenaBootstrap>();

            CreateLane(arenaRoot.transform, laneMaterial, groundLayer);
            Transform blueTower = CreateTower(
                arenaRoot.transform,
                "BlueTower",
                layout.BlueTower,
                blueMaterial,
                targetableLayer,
                TeamId.Blue);
            Transform redTower = CreateTower(
                arenaRoot.transform,
                "RedTower",
                layout.RedTower,
                redMaterial,
                targetableLayer,
                TeamId.Red);
            arena.AssignTowers(blueTower, redTower);

            CombatUnit blueTowerUnit = blueTower.GetComponent<CombatUnit>();
            CombatUnit redTowerUnit = redTower.GetComponent<CombatUnit>();
            MinionWaveSpawner waveSpawner = arenaRoot.AddComponent<MinionWaveSpawner>();
            MatchOutcomeController outcome = arenaRoot.AddComponent<MatchOutcomeController>();
            outcome.Configure(blueTowerUnit, redTowerUnit, waveSpawner);
            waveSpawner.Configure(
                arenaRoot.transform,
                layout,
                blueTowerUnit,
                redTowerUnit,
                blueMaterial,
                redMaterial,
                targetableLayer,
                groundLayer);

            CreateDeploymentMarker(arenaRoot.transform, "BlueDeployment", layout.BlueDeployment, blueMaterial);
            CreateDeploymentMarker(arenaRoot.transform, "RedDeployment", layout.RedDeployment, redMaterial);
            GameObject player = CreatePlayer(arenaRoot.transform, layout.BlueDeployment, blueMaterial, groundLayer);
            CreateTrainingDummy(arenaRoot.transform, redMaterial, targetableLayer);
            CreateDirectionalLight();
            MobaCameraController cameraController = CreateMainCamera();
            cameraController.SetCenteringTarget(player.transform);
            CreateUiRoots();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildScene();
            AssetDatabase.SaveAssets();
        }

        private static void CreateLane(Transform parent, Material material, int groundLayer)
        {
            GameObject lane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            lane.name = "Lane";
            lane.transform.SetParent(parent, false);
            lane.transform.localScale = new Vector3(10f, 1f, 2.4f);
            lane.layer = groundLayer;
            lane.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Transform CreateTower(
            Transform parent,
            string towerName,
            Vector3 position,
            Material material,
            int targetableLayer,
            TeamId team)
        {
            GameObject tower = new GameObject(towerName);
            tower.transform.SetParent(parent, false);
            tower.transform.position = position;
            tower.layer = targetableLayer;

            BoxCollider collider = tower.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 3f, 0f);
            collider.size = new Vector3(3f, 6f, 3f);

            CombatUnit combatUnit = tower.AddComponent<CombatUnit>();
            combatUnit.Configure(team, Altitude.Ground, 6000f, 150f, 40f, 9f, 1f, true, true);
            BasicAttackController attack = tower.AddComponent<BasicAttackController>();
            attack.Configure(combatUnit);
            TowerCombatController controller = tower.AddComponent<TowerCombatController>();
            controller.Configure(combatUnit, attack);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = towerName + "Visual";
            visual.transform.SetParent(tower.transform, false);
            visual.transform.localPosition = new Vector3(0f, 3f, 0f);
            visual.transform.localScale = new Vector3(3f, 6f, 3f);
            visual.layer = targetableLayer;
            visual.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

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

        private static GameObject CreatePlayer(Transform parent, Vector3 deployment, Material material, int groundLayer)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player_Exusiai";
            player.transform.SetParent(parent, false);
            player.transform.position = deployment + Vector3.up;
            player.GetComponent<Renderer>().sharedMaterial = material;

            UnitMotor motor = player.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());
            PlayerCommandController commands = player.AddComponent<PlayerCommandController>();
            CombatUnit combatUnit = player.AddComponent<CombatUnit>();
            combatUnit.Configure(TeamId.Blue, Altitude.Ground, 100f, 12f, 2f, 6f, 0.5f, true, true);
            player.AddComponent<HealthBarPresenter>();
            DeathCorpsePresenter presenter = player.AddComponent<DeathCorpsePresenter>();
            presenter.Configure(combatUnit, material, groundLayer);
            player.AddComponent<CommandFeedbackPresenter>();
            BasicAttackController attack = player.AddComponent<BasicAttackController>();
            CombatCommandResolver resolver = player.AddComponent<CombatCommandResolver>();
            resolver.Configure(combatUnit, motor, commands, attack);
            return player;
        }

        private static void CreateTrainingDummy(Transform parent, Material material, int targetableLayer)
        {
            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dummy.name = "TrainingDummy_Red";
            dummy.transform.SetParent(parent, false);
            dummy.transform.position = new Vector3(20f, 1f, 0f);
            dummy.layer = targetableLayer;
            dummy.GetComponent<Renderer>().sharedMaterial = material;
            CombatUnit combatUnit = dummy.AddComponent<CombatUnit>();
            combatUnit.Configure(TeamId.Red, Altitude.Ground, 1000f, 0f, 2f, 0f, 0f, false, false);
        }

        private static void CreateDirectionalLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static MobaCameraController CreateMainCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Vector3 initialPosition = new Vector3(0f, 42f, -34f);
            cameraObject.transform.SetPositionAndRotation(
                initialPosition,
                Quaternion.Euler(55f, 0f, 0f));
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 55f;
            MobaCameraController controller = cameraObject.AddComponent<MobaCameraController>();
            controller.ConfigureOffset(initialPosition);
            return controller;
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

        private static int EnsureLayer(string layerName)
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int index = 8; index < layers.arraySize; index++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                if (layer.stringValue == layerName)
                {
                    return index;
                }
            }

            for (int index = 8; index < layers.arraySize; index++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return index;
                }
            }

            throw new UnityException("No user layers are available for " + layerName + ".");
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
