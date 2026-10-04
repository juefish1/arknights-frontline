using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using MatchOutcome = ArknightsFrontline.Common.MatchOutcome;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class MatchHudPresenterTests
    {
        private const string BluePlayerKey = "hud-test-blue-exusiai";

        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Font> createdFonts = new List<Font>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in gameObjects)
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }

            foreach (GameObject corpse in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (corpse != null
                    && corpse.name.StartsWith("hud-test-", StringComparison.Ordinal)
                    && corpse.name.EndsWith("_Corpse", StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(corpse);
                }
            }

            foreach (Material material in materials)
            {
                if (material != null)
                {
                    Object.DestroyImmediate(material);
                }
            }

            foreach (Font font in createdFonts)
            {
                if (font != null)
                {
                    Object.DestroyImmediate(font);
                }
            }

            gameObjects.Clear();
            materials.Clear();
            createdFonts.Clear();
        }

        [Test]
        public void LiveHudUsesSharedCanvasAndDisplaysElapsedTimeAndBothTowerHealthValues()
        {
            MatchFixture fixture = CreateFixture("hud-test-live");
            fixture.BlueTower.TakePhysicalDamage(20f);
            fixture.RedTower.TakePhysicalDamage(25f);
            fixture.Match.Tick(901f);

            fixture.Hud.Configure(
                fixture.Match,
                fixture.Statistics,
                fixture.Votes,
                fixture.BlueTower,
                fixture.RedTower,
                BluePlayerKey);
            fixture.Hud.Refresh();

            Assert.That(fixture.Match.IsMatchOver, Is.False,
                "The HUD must not turn the 15-minute mark into a forced result.");
            Assert.That(fixture.Hud.Canvas, Is.SameAs(fixture.Canvas));
            Assert.That(fixture.SkillHud.transform.IsChildOf(fixture.Canvas.transform), Is.True);
            Assert.That(fixture.Hud.transform.IsChildOf(fixture.Canvas.transform), Is.True);
            Assert.That(fixture.Hud.ClockTextComponent, Is.Not.Null);
            Assert.That(fixture.Hud.BlueTowerTextComponent, Is.Not.Null);
            Assert.That(fixture.Hud.RedTowerTextComponent, Is.Not.Null);
            Assert.That(fixture.Hud.ClockTextComponent.transform.IsChildOf(fixture.Canvas.transform), Is.True);
            Assert.That(fixture.Hud.ClockText, Does.Contain("15:01"));
            Assert.That(fixture.Hud.BlueTowerText, Does.Contain("80"));
            Assert.That(fixture.Hud.RedTowerText, Does.Contain("75"));
            Assert.That(fixture.Hud.ClockTextComponent.text, Does.Contain("15:01"));
            Assert.That(fixture.Hud.BlueTowerTextComponent.text, Does.Contain("80"));
            Assert.That(fixture.Hud.RedTowerTextComponent.text, Does.Contain("75"));
        }

        [Test]
        public void HudRootStretchesOverSharedCanvasAndAnchorsLiveBarAtItsTopEdge()
        {
            MatchFixture fixture = CreateFixture("hud-test-layout");
            RectTransform canvasRect = (RectTransform)fixture.Canvas.transform;
            fixture.Canvas.renderMode = RenderMode.WorldSpace;
            canvasRect.position = Vector3.zero;
            canvasRect.localScale = Vector3.one;
            canvasRect.sizeDelta = new Vector2(1280f, 720f);

            fixture.Hud.Configure(
                fixture.Match,
                fixture.Statistics,
                fixture.Votes,
                fixture.BlueTower,
                fixture.RedTower,
                BluePlayerKey);
            Canvas.ForceUpdateCanvases();

            RectTransform hudRect = (RectTransform)fixture.Hud.transform;
            Assert.That(hudRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(hudRect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(hudRect.sizeDelta, Is.EqualTo(Vector2.zero));
            Assert.That(hudRect.rect.width, Is.EqualTo(canvasRect.rect.width).Within(0.01f));
            Assert.That(hudRect.rect.height, Is.EqualTo(canvasRect.rect.height).Within(0.01f));

            RectTransform liveBar = hudRect.Find("MatchLiveBar") as RectTransform;
            Assert.That(liveBar, Is.Not.Null);
            Assert.That(liveBar.rect.width, Is.EqualTo(canvasRect.rect.width).Within(0.01f));
            Canvas.ForceUpdateCanvases();
            Vector3[] canvasCorners = new Vector3[4];
            Vector3[] liveBarCorners = new Vector3[4];
            canvasRect.GetWorldCorners(canvasCorners);
            liveBar.GetWorldCorners(liveBarCorners);
            Assert.That(canvasCorners[1].y - liveBarCorners[1].y, Is.EqualTo(6f).Within(0.01f),
                "The live status bar should sit just inside the top edge of the full-screen HUD root.");
        }

        [Test]
        public void ResultCardFitsSharedCanvasAtCommonResolutionsAndResizesBackToNativeScale()
        {
            MatchFixture fixture = CreateFixture("hud-test-responsive-results");
            RectTransform canvasRect = (RectTransform)fixture.Canvas.transform;
            fixture.Canvas.renderMode = RenderMode.WorldSpace;
            canvasRect.position = Vector3.zero;
            canvasRect.localScale = Vector3.one;

            RectTransform skillHudRect = (RectTransform)fixture.SkillHud.transform;
            Transform skillHudParent = skillHudRect.parent;
            Vector2 skillHudAnchorMin = skillHudRect.anchorMin;
            Vector2 skillHudAnchorMax = skillHudRect.anchorMax;
            Vector2 skillHudSize = skillHudRect.sizeDelta;
            Vector2 skillHudPosition = skillHudRect.anchoredPosition;
            Vector3 skillHudScale = skillHudRect.localScale;
            int skillHudSiblingIndex = skillHudRect.GetSiblingIndex();

            fixture.Hud.Configure(
                fixture.Match,
                fixture.Statistics,
                fixture.Votes,
                fixture.BlueTower,
                fixture.RedTower,
                BluePlayerKey);
            fixture.RedTower.TakePhysicalDamage(fixture.RedTower.MaxHealth);
            fixture.Match.Tick();
            fixture.Hud.Refresh();

            RectTransform cardRect = fixture.Hud.ResultRowsRoot.parent as RectTransform;
            Assert.That(cardRect, Is.Not.Null);

            Vector2[] canvasSizes =
            {
                new Vector2(640f, 480f),
                new Vector2(1280f, 720f),
                new Vector2(1920f, 1080f)
            };
            List<string> layoutViolations = new List<string>();
            foreach (Vector2 canvasSize in canvasSizes)
            {
                canvasRect.sizeDelta = canvasSize;
                Canvas.ForceUpdateCanvases();
                fixture.Hud.Refresh();
                Canvas.ForceUpdateCanvases();

                Rect canvasBounds = canvasRect.rect;
                Vector3[] worldCorners = new Vector3[4];
                cardRect.GetWorldCorners(worldCorners);
                for (int cornerIndex = 0; cornerIndex < worldCorners.Length; cornerIndex++)
                {
                    Vector3 localCorner = canvasRect.InverseTransformPoint(worldCorners[cornerIndex]);
                    if (localCorner.x < canvasBounds.xMin + 32f - 0.01f
                        || localCorner.x > canvasBounds.xMax - 32f + 0.01f
                        || localCorner.y < canvasBounds.yMin + 32f - 0.01f
                        || localCorner.y > canvasBounds.yMax - 32f + 0.01f)
                    {
                        layoutViolations.Add(
                            $"Canvas {canvasSize}: card corner {cornerIndex} at {localCorner} "
                            + $"exceeds 32px inset bounds {canvasBounds}.");
                    }
                }

                if (canvasSize.x >= 1280f && !Mathf.Approximately(cardRect.localScale.x, 1f))
                {
                    layoutViolations.Add(
                        $"Canvas {canvasSize}: card should return to native scale 1 after resize, "
                        + $"but scale was {cardRect.localScale}.");
                }

                if (canvasRect.sizeDelta != canvasSize
                    || fixture.Canvas.renderMode != RenderMode.WorldSpace
                    || canvasRect.position != Vector3.zero
                    || canvasRect.localScale != Vector3.one)
                {
                    layoutViolations.Add(
                        $"Refreshing at {canvasSize} changed the shared Canvas configuration.");
                }

                if (skillHudRect.parent != skillHudParent
                    || skillHudRect.anchorMin != skillHudAnchorMin
                    || skillHudRect.anchorMax != skillHudAnchorMax
                    || skillHudRect.sizeDelta != skillHudSize
                    || skillHudRect.anchoredPosition != skillHudPosition
                    || skillHudRect.localScale != skillHudScale
                    || skillHudRect.GetSiblingIndex() != skillHudSiblingIndex)
                {
                    layoutViolations.Add(
                        $"Refreshing at {canvasSize} changed the existing Skill HUD sibling.");
                }
            }

            Assert.That(
                layoutViolations,
                Is.Empty,
                "The result card must fit the shared Canvas with a 32px inset and resize without "
                + "changing the shared Canvas or its existing Skill HUD sibling.\n"
                + string.Join("\n", layoutViolations));
        }

        [Test]
        public void ResolvedPanelRendersSixRowsFromSnapshotAfterTowerObjectIsDestroyed()
        {
            MatchFixture fixture = CreateFixture("hud-test-results");
            fixture.Hud.Configure(
                fixture.Match,
                fixture.Statistics,
                fixture.Votes,
                fixture.BlueTower,
                fixture.RedTower,
                BluePlayerKey);
            fixture.Match.Tick(83f);

            GameObject redTowerObject = fixture.RedTower.gameObject;
            Material corpseMaterial = CreateMaterial(Color.red);
            redTowerObject.AddComponent<MeshRenderer>().sharedMaterial = corpseMaterial;
            redTowerObject.AddComponent<DeathCorpsePresenter>()
                .Configure(fixture.RedTower, corpseMaterial, 0, UnitKind.Tower);

            fixture.RedTower.TakePhysicalDamage(fixture.RedTower.MaxHealth);
            Assert.That(redTowerObject == null, Is.True,
                "EditMode death presentation destroys the tower object immediately.");
            fixture.Match.Tick();

            Assert.That(fixture.Statistics.Snapshot, Is.Not.Null);
            Assert.That(fixture.Statistics.Snapshot.Outcome, Is.EqualTo(MatchOutcome.BlueVictory));
            Assert.That(fixture.Statistics.Snapshot.RedTowerCurrentHealth, Is.Zero);

            Assert.DoesNotThrow(() => fixture.Hud.Refresh(),
                "Result rendering must read frozen tower health rather than a destroyed live tower.");
            Assert.That(fixture.Hud.IsResultVisible, Is.True);
            Assert.That(fixture.Hud.ResultTitle, Does.Contain("胜利"));
            Assert.That(fixture.Hud.ResultElapsedText, Does.Contain("01:23"));
            Assert.That(fixture.Hud.ResultRowCount, Is.EqualTo(6));
            Assert.That(fixture.Hud.ResultRowsRoot, Is.Not.Null);
            Assert.That(fixture.Hud.ResultRowsRoot.childCount, Is.EqualTo(6));
            Assert.That(fixture.Hud.ExitButton, Is.Not.Null);
            Assert.That(fixture.Hud.ExitButton.GetComponentInParent<Canvas>(), Is.SameAs(fixture.Canvas));

            string renderedRows = string.Join(
                " ",
                fixture.Hud.ResultRowsRoot.GetComponentsInChildren<Text>(true)
                    .Select(label => label.text));
            Assert.That(renderedRows, Does.Contain("能天使"));
            Assert.That(renderedRows, Does.Contain("艾雅法拉"));
            Assert.That(renderedRows, Does.Contain("银灰"));
        }

        [TestCase(MatchOutcome.BlueVictory, "胜利")]
        [TestCase(MatchOutcome.RedVictory, "失败")]
        [TestCase(MatchOutcome.Draw, "平局")]
        public void ResultTitleUsesBluePlayersWinLossOrDrawPerspective(
            MatchOutcome outcome,
            string expectedTitle)
        {
            MatchFixture fixture = CreateFixture($"hud-test-title-{outcome}");
            fixture.Hud.Configure(
                fixture.Match,
                fixture.Statistics,
                fixture.Votes,
                fixture.BlueTower,
                fixture.RedTower,
                BluePlayerKey);

            if (outcome == MatchOutcome.BlueVictory)
            {
                fixture.RedTower.TakePhysicalDamage(fixture.RedTower.MaxHealth);
            }
            else if (outcome == MatchOutcome.RedVictory)
            {
                fixture.BlueTower.TakePhysicalDamage(fixture.BlueTower.MaxHealth);
            }
            else
            {
                fixture.BlueTower.TakePhysicalDamage(fixture.BlueTower.MaxHealth);
                fixture.RedTower.TakePhysicalDamage(fixture.RedTower.MaxHealth);
            }

            fixture.Match.Tick();
            fixture.Hud.Refresh();

            Assert.That(fixture.Hud.IsResultVisible, Is.True);
            Assert.That(fixture.Hud.ResultTitle, Does.Contain(expectedTitle));
        }

        [Test]
        public void RefreshingTheSameSnapshotKeepsSixRowsAndReusesExistingRowObjects()
        {
            MatchFixture fixture = CreateFixture("hud-test-stable-rows");
            fixture.Hud.Configure(
                fixture.Match,
                fixture.Statistics,
                fixture.Votes,
                fixture.BlueTower,
                fixture.RedTower,
                BluePlayerKey);
            fixture.RedTower.TakePhysicalDamage(fixture.RedTower.MaxHealth);
            fixture.Match.Tick();
            fixture.Hud.Refresh();

            Assert.That(fixture.Hud.ResultRowsRoot, Is.Not.Null);
            Assert.That(fixture.Hud.ResultRowsRoot.childCount, Is.EqualTo(6));
            Transform originalFirstRow = fixture.Hud.ResultRowsRoot.GetChild(0);

            fixture.Hud.Refresh();
            fixture.Hud.Refresh();
            fixture.Hud.Refresh();

            Assert.That(fixture.Hud.ResultRowsRoot.childCount, Is.EqualTo(6));
            Assert.That(fixture.Hud.ResultRowsRoot.GetChild(0), Is.SameAs(originalFirstRow),
                "Refreshing a frozen snapshot should update existing rows, not destroy/recreate the UI tree.");
        }

        [Test]
        public void BootstrapConfiguresStatsVotesAndInjectedExitActionThroughRealButton()
        {
            MatchFixture fixture = CreateFixture(
                "hud-test-bootstrap",
                configureStatistics: false,
                configureVotes: false);
            int exitCallCount = 0;
            MatchPresentationBootstrap bootstrap = CreateGameObject("hud-test-bootstrap-component")
                .AddComponent<MatchPresentationBootstrap>();
            bootstrap.Configure(
                fixture.Roster,
                fixture.Match,
                fixture.Statistics,
                fixture.Votes,
                fixture.Hud,
                fixture.BlueTower,
                fixture.RedTower,
                BluePlayerKey,
                () => exitCallCount++);
            InvokePrivate(bootstrap, "Start");

            Assert.That(fixture.Statistics.GetRows().Count, Is.EqualTo(6));
            Assert.That(fixture.Votes.IsConfigured, Is.True);
            Assert.That(fixture.Hud.Canvas, Is.SameAs(fixture.Canvas));

            fixture.RedTower.TakePhysicalDamage(fixture.RedTower.MaxHealth);
            fixture.Match.Tick();
            fixture.Hud.Refresh();
            Button exitButton = fixture.Hud.ExitButton;
            Assert.That(exitButton, Is.Not.Null);
            Assert.That(exitButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(exitButton.GetComponentInParent<Canvas>(), Is.SameAs(fixture.Canvas));
            Assert.That(fixture.Hud.ExitVoteText, Does.Contain("0/3"));

            exitButton.onClick.Invoke();
            fixture.Hud.Refresh();
            Assert.That(fixture.Votes.HasRequestedExit, Is.True);
            Assert.That(fixture.Votes.AgreeCount, Is.EqualTo(3),
                "The player vote and both configured computer votes should approve the exit.");
            Assert.That(fixture.Votes.IsExitApproved, Is.True);
            Assert.That(fixture.Hud.ExitVoteText, Does.Contain("3/3"));
            Assert.That(exitCallCount, Is.EqualTo(1));

            exitButton.onClick.Invoke();
            Assert.That(exitCallCount, Is.EqualTo(1), "An approved vote may invoke exit only once.");
            Assert.That(fixture.EventSystem.enabled, Is.True,
                "Match presentation must leave the UI EventSystem usable after settlement.");
        }

        [Test]
        public void ReconfiguringHudRemovesPreviousExitActionAndDoesNotDuplicateButtonListener()
        {
            MatchFixture fixture = CreateFixture("hud-test-rebind");
            int oldExitCallCount = 0;
            int currentExitCallCount = 0;
            fixture.Hud.Configure(
                fixture.Match,
                fixture.Statistics,
                fixture.Votes,
                fixture.BlueTower,
                fixture.RedTower,
                BluePlayerKey,
                () => oldExitCallCount++);
            fixture.Hud.Configure(
                fixture.Match,
                fixture.Statistics,
                fixture.Votes,
                fixture.BlueTower,
                fixture.RedTower,
                BluePlayerKey,
                () => currentExitCallCount++);

            fixture.RedTower.TakePhysicalDamage(fixture.RedTower.MaxHealth);
            fixture.Match.Tick();
            fixture.Hud.Refresh();
            Assert.That(fixture.Hud.ExitButton, Is.Not.Null);

            fixture.Hud.ExitButton.onClick.Invoke();
            fixture.Hud.ExitButton.onClick.Invoke();

            Assert.That(oldExitCallCount, Is.Zero);
            Assert.That(currentExitCallCount, Is.EqualTo(1));
            Assert.That(fixture.Votes.AgreeCount, Is.EqualTo(3));
        }

        [Test]
        public void OnDestroyReleasesThePresentersOwnedWindowsFont()
        {
            MatchFixture fixture = CreateFixture("hud-test-font-lifecycle");
            fixture.Hud.Configure(
                fixture.Match,
                fixture.Statistics,
                fixture.Votes,
                fixture.BlueTower,
                fixture.RedTower,
                BluePlayerKey);
            Font builtInFallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Font presenterFont = fixture.Hud.ClockTextComponent.font;

            Assert.That(presenterFont, Is.Not.Null);
            Assert.That(presenterFont, Is.Not.SameAs(builtInFallback),
                "On Windows, the presenter should own a dynamic OS CJK font rather than the built-in fallback.");
            createdFonts.Add(presenterFont);

            // EditMode-created MonoBehaviours may not have received Awake/OnDestroy from the
            // editor lifecycle, so exercise the cleanup callback contract directly here.
            InvokePrivate(fixture.Hud, "OnDestroy");

            Assert.That(presenterFont == null, Is.True,
                "The presenter cleanup callback must release its owned native dynamic Font object.");

            Object.DestroyImmediate(fixture.Hud.gameObject);
        }

        private MatchFixture CreateFixture(
            string objectPrefix,
            bool configureStatistics = true,
            bool configureVotes = true)
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit blueTower = CreateCombatUnit($"{objectPrefix}-blue-tower", TeamId.Blue, 100f);
            CombatUnit redTower = CreateCombatUnit($"{objectPrefix}-red-tower", TeamId.Red, 100f);
            Transform minionParent = CreateGameObject($"{objectPrefix}-minions").transform;
            MinionWaveSpawner spawner = CreateGameObject($"{objectPrefix}-spawner")
                .AddComponent<MinionWaveSpawner>();
            spawner.Configure(
                minionParent,
                layout,
                blueTower,
                redTower,
                CreateMaterial(Color.blue),
                CreateMaterial(Color.red),
                8,
                8);

            MatchOutcomeController match = CreateGameObject($"{objectPrefix}-match")
                .AddComponent<MatchOutcomeController>();
            match.Configure(blueTower, redTower, spawner);

            OperatorRosterController roster = CreateGameObject($"{objectPrefix}-roster")
                .AddComponent<OperatorRosterController>();
            roster.OperatorSpawned += (_, liveOperator) => Track(liveOperator.gameObject);
            for (int index = 0; index < 6; index++)
            {
                TeamId team = index < 3 ? TeamId.Blue : TeamId.Red;
                OperatorType operatorType = (OperatorType)(index % 3);
                string stableKey = GetStableKey(team, index % 3);
                GameObject template = CreateOperatorTemplate(
                    $"{objectPrefix}-{stableKey}-template",
                    team,
                    operatorType);
                roster.RegisterSlot(
                    stableKey,
                    team,
                    operatorType,
                    template,
                    new Vector3(index * 2f, 0f, 0f),
                    stableKey == BluePlayerKey);
            }

            roster.StartMatch();
            MatchStatisticsController statistics = CreateGameObject($"{objectPrefix}-statistics")
                .AddComponent<MatchStatisticsController>();
            if (configureStatistics)
            {
                statistics.Configure(match, roster, blueTower, redTower);
            }

            TeamExitVoteController votes = CreateGameObject($"{objectPrefix}-votes")
                .AddComponent<TeamExitVoteController>();
            if (configureVotes)
            {
                votes.Configure(match, roster, autoApproveComputerVotes: true);
            }

            GameObject eventSystemObject = CreateGameObject($"{objectPrefix}-event-system");
            EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
            GameObject canvasObject = CreateRectTransformObject($"{objectPrefix}-canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject skillHud = CreateRectTransformObject($"{objectPrefix}-skill-hud");
            skillHud.transform.SetParent(canvas.transform, false);
            GameObject hudObject = CreateRectTransformObject($"{objectPrefix}-match-hud");
            hudObject.transform.SetParent(canvas.transform, false);
            MatchHudPresenter hud = hudObject.AddComponent<MatchHudPresenter>();

            return new MatchFixture(
                match,
                roster,
                blueTower,
                redTower,
                statistics,
                votes,
                canvas,
                skillHud,
                eventSystem,
                hud);
        }

        private GameObject CreateOperatorTemplate(string name, TeamId team, OperatorType operatorType)
        {
            GameObject template = CreateGameObject(name);
            CombatUnit unit = template.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, 100f, 20f, 0f, 6f, 0.5f, true, true);
            template.AddComponent<OperatorIdentity>();
            template.AddComponent<UnitStatModifiers>();
            Material material = CreateMaterial(team == TeamId.Blue ? Color.blue : Color.red);
            template.AddComponent<MeshRenderer>().sharedMaterial = material;
            template.AddComponent<DeathCorpsePresenter>().Configure(unit, material, 0);
            template.SetActive(false);
            return template;
        }

        private CombatUnit CreateCombatUnit(string name, TeamId team, float maxHealth)
        {
            GameObject gameObject = CreateGameObject(name);
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, maxHealth, 20f, 0f, 6f, 0.5f, true, true);
            return unit;
        }

        private Material CreateMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            materials.Add(material);
            return material;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            Track(gameObject);
            return gameObject;
        }

        private GameObject CreateRectTransformObject(string name)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            Track(gameObject);
            return gameObject;
        }

        private void Track(GameObject gameObject)
        {
            if (gameObject != null && !gameObjects.Contains(gameObject))
            {
                gameObjects.Add(gameObject);
            }
        }

        private static string GetStableKey(TeamId team, int slotIndex)
        {
            string[] operatorNames = { "exusiai", "eyjafjalla", "silverash" };
            return $"hud-test-{team.ToString().ToLowerInvariant()}-{operatorNames[slotIndex]}";
        }

        private static void InvokePrivate(MonoBehaviour behaviour, string methodName)
        {
            MethodInfo method = behaviour.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(behaviour, null);
        }

        private sealed class MatchFixture
        {
            public MatchFixture(
                MatchOutcomeController match,
                OperatorRosterController roster,
                CombatUnit blueTower,
                CombatUnit redTower,
                MatchStatisticsController statistics,
                TeamExitVoteController votes,
                Canvas canvas,
                GameObject skillHud,
                EventSystem eventSystem,
                MatchHudPresenter hud)
            {
                Match = match;
                Roster = roster;
                BlueTower = blueTower;
                RedTower = redTower;
                Statistics = statistics;
                Votes = votes;
                Canvas = canvas;
                SkillHud = skillHud;
                EventSystem = eventSystem;
                Hud = hud;
            }

            public MatchOutcomeController Match { get; }

            public OperatorRosterController Roster { get; }

            public CombatUnit BlueTower { get; }

            public CombatUnit RedTower { get; }

            public MatchStatisticsController Statistics { get; }

            public TeamExitVoteController Votes { get; }

            public Canvas Canvas { get; }

            public GameObject SkillHud { get; }

            public EventSystem EventSystem { get; }

            public MatchHudPresenter Hud { get; }
        }
    }
}
