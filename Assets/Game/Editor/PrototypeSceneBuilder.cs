using System;
using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Camera;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ArknightsFrontline.Editor
{
    public static class PrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/PrototypeArena.unity";
        private const string MaterialsPath = "Assets/Game/Materials";
        private const int ObstacleLayerIndex = ExusiaiSkillController.ReservedObstacleLayerIndex;

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
            // Reserve the next numeric layer for obstacles without rewriting the user-owned TagManager.
            int obstacleLayer = ObstacleLayerIndex;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ArenaLayout layout = ArenaLayout.CreateDefault();

            GameObject arenaRoot = new GameObject("ArenaBootstrap");
            ArenaBootstrap arena = arenaRoot.AddComponent<ArenaBootstrap>();
            OperatorRosterController roster = arenaRoot.AddComponent<OperatorRosterController>();

            CreateLane(arenaRoot.transform, laneMaterial, groundLayer);
            MobaViewSceneTools.AddGroundPresentation(arenaRoot.transform);
            Transform blueTower = CreateTower(
                arenaRoot.transform,
                "BlueTower",
                layout.BlueTower,
                blueMaterial,
                targetableLayer,
                groundLayer,
                TeamId.Blue);
            Transform redTower = CreateTower(
                arenaRoot.transform,
                "RedTower",
                layout.RedTower,
                redMaterial,
                targetableLayer,
                groundLayer,
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
            GameObject player = CreatePlayer(
                arenaRoot.transform,
                layout.BlueDeployment,
                blueMaterial,
                targetableLayer,
                groundLayer,
                obstacleLayer);
            GameObject blueEyjafjalla = CreateComputerOperatorTemplate(
                arenaRoot.transform,
                "Template_Blue_Eyjafjalla",
                TeamId.Blue,
                OperatorType.Eyjafjalla,
                layout.BlueDeployment + Vector3.up * ArenaVisualMetrics.OperatorCenterHeight + Vector3.back * 2f,
                blueMaterial,
                targetableLayer,
                groundLayer,
                950f,
                75f,
                10f,
                6f,
                1.2f,
                4.8f,
                true);
            GameObject blueSilverAsh = CreateComputerOperatorTemplate(
                arenaRoot.transform,
                "Template_Blue_SilverAsh",
                TeamId.Blue,
                OperatorType.SilverAsh,
                layout.BlueDeployment + Vector3.up * ArenaVisualMetrics.OperatorCenterHeight + Vector3.forward * 2f,
                blueMaterial,
                targetableLayer,
                groundLayer,
                1400f,
                85f,
                30f,
                2.2f,
                1.1f,
                4.8f,
                false);
            GameObject redExusiai = CreateComputerOperatorTemplate(
                arenaRoot.transform,
                "Template_Red_Exusiai",
                TeamId.Red,
                OperatorType.Exusiai,
                layout.RedDeployment + Vector3.up * ArenaVisualMetrics.OperatorCenterHeight,
                redMaterial,
                targetableLayer,
                groundLayer,
                1000f,
                50f,
                2f,
                6f,
                0.5f,
                5f,
                true);
            GameObject redEyjafjalla = CreateComputerOperatorTemplate(
                arenaRoot.transform,
                "Template_Red_Eyjafjalla",
                TeamId.Red,
                OperatorType.Eyjafjalla,
                layout.RedDeployment + Vector3.up * ArenaVisualMetrics.OperatorCenterHeight + Vector3.back * 2f,
                redMaterial,
                targetableLayer,
                groundLayer,
                950f,
                75f,
                10f,
                6f,
                1.2f,
                4.8f,
                true);
            GameObject redSilverAsh = CreateComputerOperatorTemplate(
                arenaRoot.transform,
                "Template_Red_SilverAsh",
                TeamId.Red,
                OperatorType.SilverAsh,
                layout.RedDeployment + Vector3.up * ArenaVisualMetrics.OperatorCenterHeight + Vector3.forward * 2f,
                redMaterial,
                targetableLayer,
                groundLayer,
                1400f,
                85f,
                30f,
                2.2f,
                1.1f,
                4.8f,
                false);
            CreateDirectionalLight();
            MobaCameraController cameraController = CreateMainCamera();
            cameraController.SetCenteringTarget(player.transform);
            cameraController.CenterOn(player.transform);
            Canvas canvas = CreateUiRoots();
            GameObject skillHudObject = new GameObject("SkillHud", typeof(RectTransform));
            skillHudObject.transform.SetParent(canvas.transform, false);
            SkillHudPresenter skillHud = skillHudObject.AddComponent<SkillHudPresenter>();
            GameObject playerDeploymentObject = new GameObject("PlayerDeployment");
            playerDeploymentObject.transform.SetParent(arenaRoot.transform, false);
            PlayerDeploymentPresenter playerDeployment = playerDeploymentObject.AddComponent<PlayerDeploymentPresenter>();
            ArenaRosterBootstrap rosterBootstrap = arenaRoot.AddComponent<ArenaRosterBootstrap>();
            rosterBootstrap.Configure(
                roster,
                blueTowerUnit,
                redTowerUnit,
                outcome,
                playerDeployment,
                skillHud,
                cameraController,
                new[]
                {
                    new ArenaOperatorSlotConfiguration(
                        "Player_Exusiai", TeamId.Blue, OperatorType.Exusiai, player,
                        layout.BlueDeployment + Vector3.up * ArenaVisualMetrics.OperatorCenterHeight, true),
                    new ArenaOperatorSlotConfiguration(
                        "Blue_Eyjafjalla", TeamId.Blue, OperatorType.Eyjafjalla, blueEyjafjalla,
                        layout.BlueDeployment + Vector3.up * ArenaVisualMetrics.OperatorCenterHeight + Vector3.back * 2f, false),
                    new ArenaOperatorSlotConfiguration(
                        "Blue_SilverAsh", TeamId.Blue, OperatorType.SilverAsh, blueSilverAsh,
                        layout.BlueDeployment + Vector3.up * ArenaVisualMetrics.OperatorCenterHeight + Vector3.forward * 2f, false),
                    new ArenaOperatorSlotConfiguration(
                        "Red_Exusiai", TeamId.Red, OperatorType.Exusiai, redExusiai,
                        layout.RedDeployment + Vector3.up * ArenaVisualMetrics.OperatorCenterHeight, false),
                    new ArenaOperatorSlotConfiguration(
                        "Red_Eyjafjalla", TeamId.Red, OperatorType.Eyjafjalla, redEyjafjalla,
                        layout.RedDeployment + Vector3.up * ArenaVisualMetrics.OperatorCenterHeight + Vector3.back * 2f, false),
                    new ArenaOperatorSlotConfiguration(
                        "Red_SilverAsh", TeamId.Red, OperatorType.SilverAsh, redSilverAsh,
                        layout.RedDeployment + Vector3.up * ArenaVisualMetrics.OperatorCenterHeight + Vector3.forward * 2f, false)
                });

            AddMatchPresentation(arenaRoot, canvas, roster, outcome, blueTowerUnit, redTowerUnit);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildScene();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Arknights Frontline/Upgrade Stage 7 Presentation")]
        public static void UpgradeStage7Presentation()
        {
            Scene scene = EditorSceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            if (AddMatchPresentation(scene)
                && !EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Could not save the upgraded Prototype Arena scene.");
            }
        }

        private static bool AddMatchPresentation(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException("The Prototype Arena scene is not loaded.");
            }

            GameObject arenaRoot = null;
            Canvas canvas = null;
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == "ArenaBootstrap")
                {
                    arenaRoot = rootObject;
                }

                Canvas candidateCanvas = rootObject.GetComponent<Canvas>();
                if (candidateCanvas != null)
                {
                    if (canvas != null)
                    {
                        throw new InvalidOperationException("The Prototype Arena scene has multiple root canvases.");
                    }

                    canvas = candidateCanvas;
                }
            }

            if (arenaRoot == null || canvas == null)
            {
                throw new InvalidOperationException(
                    "The Prototype Arena scene requires its existing ArenaBootstrap and Canvas objects.");
            }

            ArenaBootstrap arena = arenaRoot.GetComponent<ArenaBootstrap>();
            OperatorRosterController roster = arenaRoot.GetComponent<OperatorRosterController>();
            ArenaRosterBootstrap rosterBootstrap = arenaRoot.GetComponent<ArenaRosterBootstrap>();
            MatchOutcomeController match = arenaRoot.GetComponent<MatchOutcomeController>();
            if (arena == null || roster == null || rosterBootstrap == null || match == null
                || rosterBootstrap.Roster != roster || arena.BlueTower == null || arena.RedTower == null)
            {
                throw new InvalidOperationException(
                    "The existing ArenaBootstrap must reference its roster and both towers, and include its match bootstraps.");
            }

            CombatUnit blueTower = arena.BlueTower.GetComponent<CombatUnit>();
            CombatUnit redTower = arena.RedTower.GetComponent<CombatUnit>();
            if (blueTower == null || redTower == null)
            {
                throw new InvalidOperationException("Both arena towers require CombatUnit components.");
            }

            return AddMatchPresentation(arenaRoot, canvas, roster, match, blueTower, redTower);
        }

        private static bool AddMatchPresentation(
            GameObject arenaRoot,
            Canvas canvas,
            OperatorRosterController roster,
            MatchOutcomeController match,
            CombatUnit blueTower,
            CombatUnit redTower)
        {
            bool changed = false;

            MatchStatisticsController statistics = arenaRoot.GetComponent<MatchStatisticsController>();
            if (statistics == null)
            {
                statistics = arenaRoot.AddComponent<MatchStatisticsController>();
                changed = true;
            }

            TeamExitVoteController votes = arenaRoot.GetComponent<TeamExitVoteController>();
            if (votes == null)
            {
                votes = arenaRoot.AddComponent<TeamExitVoteController>();
                changed = true;
            }

            Transform matchHudTransform = canvas.transform.Find("MatchHud");
            GameObject matchHudObject;
            if (matchHudTransform == null)
            {
                matchHudObject = new GameObject("MatchHud", typeof(RectTransform));
                matchHudTransform = matchHudObject.transform;
                matchHudTransform.SetParent(canvas.transform, false);
                changed = true;
            }
            else
            {
                matchHudObject = matchHudTransform.gameObject;
                if (matchHudObject.GetComponent<RectTransform>() == null)
                {
                    throw new InvalidOperationException("The existing MatchHud object must use a RectTransform.");
                }
            }

            MatchHudPresenter hud = matchHudObject.GetComponent<MatchHudPresenter>();
            if (hud == null)
            {
                hud = matchHudObject.AddComponent<MatchHudPresenter>();
                changed = true;
            }

            MatchPresentationBootstrap presentation =
                arenaRoot.GetComponent<MatchPresentationBootstrap>();
            if (presentation == null)
            {
                presentation = arenaRoot.AddComponent<MatchPresentationBootstrap>();
                changed = true;
            }

            if (NeedsPresentationConfiguration(
                    presentation,
                    roster,
                    match,
                    statistics,
                    votes,
                    hud,
                    blueTower,
                    redTower,
                    "Player_Exusiai"))
            {
                presentation.Configure(
                    roster,
                    match,
                    statistics,
                    votes,
                    hud,
                    blueTower,
                    redTower,
                    "Player_Exusiai");
                EditorUtility.SetDirty(presentation);
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(arenaRoot.scene);
            }

            return changed;
        }

        private static bool NeedsPresentationConfiguration(
            MatchPresentationBootstrap presentation,
            OperatorRosterController roster,
            MatchOutcomeController match,
            MatchStatisticsController statistics,
            TeamExitVoteController votes,
            MatchHudPresenter hud,
            CombatUnit blueTower,
            CombatUnit redTower,
            string playerStableKey)
        {
            SerializedObject serialized = new SerializedObject(presentation);
            return !ReferenceEquals(serialized.FindProperty("roster").objectReferenceValue, roster)
                || !ReferenceEquals(serialized.FindProperty("match").objectReferenceValue, match)
                || !ReferenceEquals(serialized.FindProperty("statistics").objectReferenceValue, statistics)
                || !ReferenceEquals(serialized.FindProperty("votes").objectReferenceValue, votes)
                || !ReferenceEquals(serialized.FindProperty("hud").objectReferenceValue, hud)
                || !ReferenceEquals(serialized.FindProperty("blueTower").objectReferenceValue, blueTower)
                || !ReferenceEquals(serialized.FindProperty("redTower").objectReferenceValue, redTower)
                || serialized.FindProperty("playerStableKey").stringValue != playerStableKey;
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
            int groundLayer,
            TeamId team)
        {
            GameObject tower = new GameObject(towerName);
            tower.transform.SetParent(parent, false);
            tower.transform.position = position;
            tower.layer = targetableLayer;

            BoxCollider collider = tower.AddComponent<BoxCollider>();
            collider.center = ArenaVisualMetrics.TowerCenter;
            collider.size = ArenaVisualMetrics.TowerSize;

            CombatUnit combatUnit = tower.AddComponent<CombatUnit>();
            combatUnit.Configure(team, Altitude.Ground, 500f, 20f, 40f, 9f, 1f, true, true);
            tower.AddComponent<HealthBarPresenter>();
            DeathCorpsePresenter corpsePresenter = tower.AddComponent<DeathCorpsePresenter>();
            corpsePresenter.Configure(
                combatUnit,
                material,
                groundLayer,
                new Vector3(0.3f, 1f, 0.3f),
                UnitKind.Tower);
            BasicAttackController attack = tower.AddComponent<BasicAttackController>();
            attack.Configure(combatUnit);
            TowerCombatController controller = tower.AddComponent<TowerCombatController>();
            controller.Configure(combatUnit, attack);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = towerName + "Visual";
            visual.transform.SetParent(tower.transform, false);
            visual.transform.localPosition = ArenaVisualMetrics.TowerCenter;
            visual.transform.localScale = ArenaVisualMetrics.TowerSize;
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

        private static GameObject CreatePlayer(
            Transform parent,
            Vector3 deployment,
            Material material,
            int targetableLayer,
            int groundLayer,
            int obstacleLayer)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player_Exusiai";
            player.transform.SetParent(parent, false);
            player.transform.localScale = ArenaVisualMetrics.OperatorScale;
            player.transform.position = deployment + Vector3.up * ArenaVisualMetrics.OperatorCenterHeight;
            player.layer = targetableLayer;
            player.GetComponent<Renderer>().sharedMaterial = material;

            UnitMotor motor = player.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());
            PlayerCommandController commands = player.AddComponent<PlayerCommandController>();
            CombatUnit combatUnit = player.AddComponent<CombatUnit>();
            combatUnit.Configure(TeamId.Blue, Altitude.Ground, 1000f, 50f, 2f, 6f, 0.5f, true, true);
            player.AddComponent<HealthBarPresenter>();
            DeathCorpsePresenter presenter = player.AddComponent<DeathCorpsePresenter>();
            presenter.Configure(combatUnit, material, groundLayer, UnitKind.Operator);
            player.AddComponent<CommandFeedbackPresenter>();
            BasicAttackController attack = player.AddComponent<BasicAttackController>();
            CombatCommandResolver resolver = player.AddComponent<CombatCommandResolver>();
            resolver.Configure(combatUnit, motor, commands, attack);
            UnitStatModifiers modifiers = player.AddComponent<UnitStatModifiers>();
            AttackSequenceExecutor sequence = player.AddComponent<AttackSequenceExecutor>();
            sequence.Configure(combatUnit);
            SkillDashController dash = player.AddComponent<SkillDashController>();
            dash.Configure(motor, ArenaLayout.CreateDefault(), 1 << obstacleLayer);
            attack.Configure(combatUnit, sequence);
            OperatorIdentity identity = player.AddComponent<OperatorIdentity>();
            identity.Configure("Player_Exusiai", TeamId.Blue, OperatorType.Exusiai);
            player.AddComponent<OperatorRetreatController>();
            player.AddComponent<ExusiaiSkillController>();
            player.AddComponent<ExusiaiSkillIndicator>();
            player.SetActive(false);
            ExusiaiArenaSceneTools.AttachVisual(player);
            return player;
        }

        private static GameObject CreateComputerOperatorTemplate(
            Transform parent,
            string templateName,
            TeamId team,
            OperatorType operatorType,
            Vector3 position,
            Material material,
            int targetableLayer,
            int groundLayer,
            float health,
            float attackPower,
            float defense,
            float attackRange,
            float attackInterval,
            float movementSpeed,
            bool canAttackAir)
        {
            GameObject template = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            template.name = templateName;
            template.transform.SetParent(parent, false);
            template.transform.localScale = ArenaVisualMetrics.OperatorScale;
            template.transform.position = position;
            template.layer = targetableLayer;
            template.GetComponent<Renderer>().sharedMaterial = material;

            UnitMotor motor = template.AddComponent<UnitMotor>();
            motor.Configure(movementSpeed, ArenaLayout.CreateDefault());
            CombatUnit combatUnit = template.AddComponent<CombatUnit>();
            combatUnit.Configure(
                team,
                Altitude.Ground,
                health,
                attackPower,
                defense,
                attackRange,
                attackInterval,
                true,
                canAttackAir);
            template.AddComponent<HealthBarPresenter>();
            DeathCorpsePresenter presenter = template.AddComponent<DeathCorpsePresenter>();
            presenter.Configure(combatUnit, material, groundLayer, UnitKind.Operator);
            BasicAttackController attack = template.AddComponent<BasicAttackController>();
            attack.Configure(combatUnit);
            OperatorIdentity identity = template.AddComponent<OperatorIdentity>();
            identity.Configure(templateName, team, operatorType);
            template.AddComponent<OperatorRetreatController>();
            template.AddComponent<SimpleOperatorAiController>();
            template.SetActive(false);
            return template;
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
            Vector3 initialPosition = MobaCameraController.DefaultOffset;
            cameraObject.transform.SetPositionAndRotation(
                initialPosition,
                Quaternion.LookRotation(-initialPosition, Vector3.up));
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.orthographic = false;
            camera.fieldOfView = MobaCameraController.DefaultFieldOfView;
            MobaCameraController controller = cameraObject.AddComponent<MobaCameraController>();
            controller.ConfigureOffset(initialPosition);
            return controller;
        }

        private static Canvas CreateUiRoots()
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();

            GameObject canvasObject = new GameObject("Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static Material GetOrCreateMaterial(string fileName, Color color)
        {
            string path = MaterialsPath + "/" + fileName;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader)
            {
                name = fileName.Substring(0, fileName.Length - 4)
            };
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureBuildScene()
        {
            EditorBuildSettingsScene[] configuredScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < configuredScenes.Length; i++)
            {
                if (configuredScenes[i].path == ScenePath && configuredScenes[i].enabled)
                {
                    return;
                }
            }

            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(configuredScenes);
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
