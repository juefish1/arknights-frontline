using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class Stage7SceneWiringTests
    {
        private const string ScenePath = "Assets/Game/Scenes/PrototypeArena.unity";

        [Test]
        public void SavedPrototypeSceneHasStage7PresentationReferences()
        {
            Scene previouslyActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.GetSceneByPath(ScenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;

            try
            {
                if (openedForTest)
                {
                    scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                }

                GameObject[] rootObjects = scene.GetRootGameObjects();
                GameObject arenaRoot = rootObjects.Single(root => root.name == "ArenaBootstrap");
                Canvas[] canvases = rootObjects
                    .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                    .ToArray();
                Assert.That(canvases, Has.Length.EqualTo(1));
                Canvas canvas = canvases[0];

                OperatorRosterController roster = arenaRoot.GetComponent<OperatorRosterController>();
                MatchOutcomeController match = arenaRoot.GetComponent<MatchOutcomeController>();
                MatchStatisticsController statistics = arenaRoot.GetComponent<MatchStatisticsController>();
                TeamExitVoteController votes = arenaRoot.GetComponent<TeamExitVoteController>();
                MatchPresentationBootstrap presentation = arenaRoot.GetComponent<MatchPresentationBootstrap>();
                Assert.That(roster, Is.Not.Null);
                Assert.That(match, Is.Not.Null);
                Assert.That(statistics, Is.Not.Null);
                Assert.That(votes, Is.Not.Null);
                Assert.That(presentation, Is.Not.Null);

                Transform matchHudTransform = canvas.transform.Find("MatchHud");
                Assert.That(matchHudTransform, Is.Not.Null);
                Assert.That(matchHudTransform.parent, Is.SameAs(canvas.transform));
                Assert.That(matchHudTransform.GetComponent<RectTransform>(), Is.Not.Null);
                MatchHudPresenter hud = matchHudTransform.GetComponent<MatchHudPresenter>();
                Assert.That(hud, Is.Not.Null);

                ArenaBootstrap arena = arenaRoot.GetComponent<ArenaBootstrap>();
                Assert.That(arena, Is.Not.Null);
                CombatUnit blueTower = arena.BlueTower.GetComponent<CombatUnit>();
                CombatUnit redTower = arena.RedTower.GetComponent<CombatUnit>();

                SerializedObject serializedPresentation = new SerializedObject(presentation);
                serializedPresentation.UpdateIfRequiredOrScript();
                AssertReference(serializedPresentation, "roster", roster);
                AssertReference(serializedPresentation, "match", match);
                AssertReference(serializedPresentation, "statistics", statistics);
                AssertReference(serializedPresentation, "votes", votes);
                AssertReference(serializedPresentation, "hud", hud);
                AssertReference(serializedPresentation, "blueTower", blueTower);
                AssertReference(serializedPresentation, "redTower", redTower);
                Assert.That(
                    serializedPresentation.FindProperty("playerStableKey").stringValue,
                    Is.EqualTo("Player_Exusiai"));
            }
            finally
            {
                if (openedForTest && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }

                if (previouslyActiveScene.IsValid() && previouslyActiveScene.isLoaded)
                {
                    EditorSceneManager.SetActiveScene(previouslyActiveScene);
                }
            }
        }

        private static void AssertReference(
            SerializedObject serializedObject,
            string propertyName,
            Object expectedObject)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Missing serialized reference field '{propertyName}'.");
            Assert.That(property.objectReferenceValue, Is.SameAs(expectedObject), propertyName);
        }
    }
}
