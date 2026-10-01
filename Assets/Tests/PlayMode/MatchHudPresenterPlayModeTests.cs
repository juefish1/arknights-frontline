using System.Collections;
using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class MatchHudPresenterPlayModeTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();

        [UnityTest]
        public IEnumerator DestroyingAnActiveHudReleasesItsOwnedDynamicFont()
        {
            GameObject canvasObject = CreateRectObject("hud-font-lifecycle-canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            MatchOutcomeController match = CreateObject("hud-font-lifecycle-match")
                .AddComponent<MatchOutcomeController>();
            MatchStatisticsController statistics = CreateObject("hud-font-lifecycle-statistics")
                .AddComponent<MatchStatisticsController>();
            TeamExitVoteController votes = CreateObject("hud-font-lifecycle-votes")
                .AddComponent<TeamExitVoteController>();
            CombatUnit blueTower = CreateObject("hud-font-lifecycle-blue-tower")
                .AddComponent<CombatUnit>();
            CombatUnit redTower = CreateObject("hud-font-lifecycle-red-tower")
                .AddComponent<CombatUnit>();
            GameObject hudObject = CreateRectObject("hud-font-lifecycle-presenter");
            hudObject.transform.SetParent(canvas.transform, false);
            MatchHudPresenter hud = hudObject.AddComponent<MatchHudPresenter>();

            hud.Configure(
                match,
                statistics,
                votes,
                blueTower,
                redTower,
                "hud-font-lifecycle-player",
                () => { });

            Font builtInFallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Font ownedFont = hud.ClockTextComponent.font;
            Assert.That(ownedFont, Is.Not.Null);
            Assert.That(ownedFont, Is.Not.SameAs(builtInFallback),
                "The lifecycle test requires the Windows OS font, not the shared built-in fallback.");

            Object.Destroy(hudObject);
            yield return null;
            yield return null;

            Assert.That(ownedFont == null, Is.True,
                "Destroying an active presenter must release its native dynamic font during Unity's real OnDestroy lifecycle.");
        }

        [UnityTearDown]
        public IEnumerator DestroyRuntimeObjects()
        {
            foreach (GameObject gameObject in gameObjects)
            {
                if (gameObject != null)
                {
                    Object.Destroy(gameObject);
                }
            }

            gameObjects.Clear();
            yield return null;
        }

        private GameObject CreateObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            return gameObject;
        }

        private GameObject CreateRectObject(string name)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObjects.Add(gameObject);
            return gameObject;
        }
    }
}
