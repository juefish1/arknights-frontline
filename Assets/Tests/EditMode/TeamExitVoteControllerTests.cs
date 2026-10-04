using System.Collections.Generic;
using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class TeamExitVoteControllerTests
    {
        private const int GroundLayer = 8;
        private const string KeyPrefix = "exit-vote-test";

        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private readonly List<Material> materials = new List<Material>();

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

            foreach (GameObject corpse in FindCorpses())
            {
                Object.DestroyImmediate(corpse);
            }

            foreach (Material material in materials)
            {
                if (material != null)
                {
                    Object.DestroyImmediate(material);
                }
            }

            gameObjects.Clear();
            materials.Clear();
        }

        [Test]
        public void ConfigureRequiresExactlyThreeRosterSeatsPerTeam()
        {
            MatchFixture match = CreateMatch();
            OperatorRosterController roster = CreateRoster(2, 3, true, false);
            TeamExitVoteController vote = CreateGameObject(KeyPrefix + "-vote")
                .AddComponent<TeamExitVoteController>();

            Assert.That(
                () => vote.Configure(match.OutcomeController, roster),
                Throws.InvalidOperationException);
            Assert.That(vote.IsConfigured, Is.False);
        }

        [Test]
        public void ExitRequestIsRejectedUntilTheOutcomeIsFullyResolved()
        {
            ExitVoteFixture fixture = CreateFixture();

            Assert.That(fixture.Vote.RequestExit(fixture.BluePlayerKey), Is.False);
            fixture.Match.RedTower.TakePhysicalDamage(fixture.Match.RedTower.MaxHealth);
            Assert.That(fixture.Match.OutcomeController.IsEnding, Is.True);
            Assert.That(fixture.Match.OutcomeController.IsMatchOver, Is.False);
            Assert.That(fixture.Vote.RequestExit(fixture.BluePlayerKey), Is.False,
                "A pending tower death is not a finalized match result.");

            fixture.Match.OutcomeController.Tick();

            Assert.That(fixture.Match.OutcomeController.IsMatchOver, Is.True);
            Assert.That(fixture.Vote.RequestExit(fixture.BluePlayerKey), Is.True);
            Assert.That(fixture.Vote.AgreeCount, Is.EqualTo(1));
            Assert.That(fixture.Vote.RequiredCount, Is.EqualTo(3));
            Assert.That(fixture.Vote.IsExitApproved, Is.False,
                "With computer approval disabled, the requester contributes only one of three votes.");
        }

        [Test]
        public void TwoOfThreeVotesDoNotApproveAndForeignOrUnknownKeysAreRejected()
        {
            ExitVoteFixture fixture = CreateFinalizedFixture();
            int approvedEvents = 0;
            fixture.Vote.ExitApproved += () => approvedEvents++;

            Assert.That(fixture.Vote.RequestExit(fixture.BluePlayerKey), Is.True);
            Assert.That(fixture.Vote.AgreeCount, Is.EqualTo(1));
            Assert.That(fixture.Vote.CastVote(fixture.RedComputerKey, true), Is.False);
            Assert.That(fixture.Vote.CastVote("unknown-exit-vote-key", true), Is.False);
            Assert.That(fixture.Vote.CastVote(fixture.BlueComputerKeys[0], true), Is.True);
            Assert.That(fixture.Vote.AgreeCount, Is.EqualTo(2));
            Assert.That(fixture.Vote.IsExitApproved, Is.False);
            Assert.That(approvedEvents, Is.Zero);
        }

        [Test]
        public void UnanimousVotesApproveExactlyOnceAndDoNotResetTheMatch()
        {
            ExitVoteFixture fixture = CreateFinalizedFixture();
            MatchOutcome outcomeBeforeVote = fixture.Match.OutcomeController.Outcome;
            float elapsedBeforeVote = fixture.Match.OutcomeController.ElapsedSeconds;
            int approvedEvents = 0;
            fixture.Vote.ExitApproved += () => approvedEvents++;

            Assert.That(fixture.Vote.RequestExit(fixture.BluePlayerKey), Is.True);
            Assert.That(fixture.Vote.CastVote(fixture.BlueComputerKeys[0], true), Is.True);
            Assert.That(fixture.Vote.IsExitApproved, Is.False);
            Assert.That(fixture.Vote.CastVote(fixture.BlueComputerKeys[1], true), Is.True);

            Assert.That(fixture.Vote.AgreeCount, Is.EqualTo(3));
            Assert.That(fixture.Vote.IsExitApproved, Is.True);
            Assert.That(approvedEvents, Is.EqualTo(1));
            Assert.That(fixture.Vote.RequestExit(fixture.BluePlayerKey), Is.False);
            Assert.That(fixture.Vote.CastVote(fixture.BlueComputerKeys[1], true), Is.False);
            Assert.That(approvedEvents, Is.EqualTo(1));
            Assert.That(fixture.Match.OutcomeController.Outcome, Is.EqualTo(outcomeBeforeVote));
            Assert.That(fixture.Match.OutcomeController.ElapsedSeconds, Is.EqualTo(elapsedBeforeVote));
        }

        [Test]
        public void ANoVoteCannotBeChangedAndPreventsUnanimousApproval()
        {
            ExitVoteFixture fixture = CreateFinalizedFixture();
            int approvedEvents = 0;
            fixture.Vote.ExitApproved += () => approvedEvents++;

            Assert.That(fixture.Vote.RequestExit(fixture.BluePlayerKey), Is.True);
            Assert.That(fixture.Vote.CastVote(fixture.BlueComputerKeys[0], false), Is.True);
            Assert.That(fixture.Vote.CastVote(fixture.BlueComputerKeys[0], true), Is.False);
            Assert.That(fixture.Vote.CastVote(fixture.BlueComputerKeys[1], true), Is.True);

            Assert.That(fixture.Vote.AgreeCount, Is.EqualTo(2));
            Assert.That(fixture.Vote.HasRequestedExit, Is.True);
            Assert.That(fixture.Vote.IsExitApproved, Is.False);
            Assert.That(approvedEvents, Is.Zero);
        }

        [Test]
        public void DeadComputerSeatRemainsARequiredVoterAndComputerVotesAreAutomatic()
        {
            ExitVoteFixture fixture = CreateFixture(autoApproveComputerVotes: true);
            OperatorRosterSlot deadSeat = fixture.Roster.Slots
                .First(slot => slot.StableKey == fixture.BlueComputerKeys[0]);
            deadSeat.CurrentOperator.TakePhysicalDamage(deadSeat.CurrentOperator.MaxHealth);
            Assert.That(deadSeat.CurrentOperator, Is.Null);
            Assert.That(deadSeat.IsStopped, Is.False);

            fixture.Match.RedTower.TakePhysicalDamage(fixture.Match.RedTower.MaxHealth);
            fixture.Match.OutcomeController.Tick();
            int approvedEvents = 0;
            fixture.Vote.ExitApproved += () => approvedEvents++;

            Assert.That(fixture.Vote.RequestExit(fixture.BluePlayerKey), Is.True);

            Assert.That(fixture.Vote.RequiredCount, Is.EqualTo(3));
            Assert.That(fixture.Vote.AgreeCount, Is.EqualTo(3));
            Assert.That(fixture.Vote.IsExitApproved, Is.True);
            Assert.That(approvedEvents, Is.EqualTo(1));
        }

        [Test]
        public void TeamWithOnlyComputerSeatsCannotRequestAnExitVote()
        {
            ExitVoteFixture fixture = CreateFinalizedFixture(autoApproveComputerVotes: true);

            Assert.That(fixture.Vote.RequestExit(fixture.RedComputerKey), Is.False);
            Assert.That(fixture.Vote.HasRequestedExit, Is.False);
            Assert.That(fixture.Vote.AgreeCount, Is.Zero);
            Assert.That(fixture.Vote.IsExitApproved, Is.False);
        }

        private ExitVoteFixture CreateFinalizedFixture(bool autoApproveComputerVotes = false)
        {
            ExitVoteFixture fixture = CreateFixture(autoApproveComputerVotes);
            fixture.Match.OutcomeController.Tick(12.5f);
            fixture.Match.RedTower.TakePhysicalDamage(fixture.Match.RedTower.MaxHealth);
            fixture.Match.OutcomeController.Tick();
            return fixture;
        }

        private ExitVoteFixture CreateFixture(bool autoApproveComputerVotes = false)
        {
            MatchFixture match = CreateMatch();
            OperatorRosterController roster = CreateRoster(3, 3, true, true);
            TeamExitVoteController vote = CreateGameObject(KeyPrefix + "-vote")
                .AddComponent<TeamExitVoteController>();
            vote.Configure(match.OutcomeController, roster, autoApproveComputerVotes);
            return new ExitVoteFixture(match, roster, vote);
        }

        private OperatorRosterController CreateRoster(
            int blueSeatCount,
            int redSeatCount,
            bool includeBluePlayer,
            bool deploy)
        {
            OperatorRosterController roster = CreateGameObject(KeyPrefix + "-roster")
                .AddComponent<OperatorRosterController>();
            RegisterTeamSlots(roster, TeamId.Blue, blueSeatCount, includeBluePlayer);
            RegisterTeamSlots(roster, TeamId.Red, redSeatCount, false);

            if (deploy)
            {
                roster.StartMatch();
                TrackCurrentOperators(roster);
            }

            return roster;
        }

        private void RegisterTeamSlots(
            OperatorRosterController roster,
            TeamId team,
            int seatCount,
            bool includePlayer)
        {
            for (int index = 0; index < seatCount; index++)
            {
                string stableKey = GetKey(team, index);
                GameObject template = CreateOperatorTemplate(stableKey + "-template", team);
                roster.RegisterSlot(
                    stableKey,
                    team,
                    (OperatorType)(index % 3),
                    template,
                    new Vector3(index, 0f, 0f),
                    includePlayer && index == 0);
            }
        }

        private MatchFixture CreateMatch()
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit blueTower = CreateUnit(KeyPrefix + "-blue-tower", TeamId.Blue, layout.BlueTower, 100f);
            CombatUnit redTower = CreateUnit(KeyPrefix + "-red-tower", TeamId.Red, layout.RedTower, 100f);
            Transform minionParent = CreateGameObject(KeyPrefix + "-minions").transform;
            MinionWaveSpawner spawner = CreateGameObject(KeyPrefix + "-spawner")
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
            MatchOutcomeController outcomeController = CreateGameObject(KeyPrefix + "-outcome")
                .AddComponent<MatchOutcomeController>();
            outcomeController.Configure(blueTower, redTower, spawner);
            return new MatchFixture(blueTower, redTower, outcomeController);
        }

        private GameObject CreateOperatorTemplate(string name, TeamId team)
        {
            GameObject template = CreateGameObject(name);
            template.SetActive(false);
            CombatUnit unit = template.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, 100f, 20f, 2f, 6f, 0.5f, true, true);
            template.AddComponent<OperatorIdentity>();
            template.AddComponent<UnitStatModifiers>();
            Material material = CreateMaterial(Color.white);
            template.AddComponent<MeshRenderer>().sharedMaterial = material;
            template.AddComponent<DeathCorpsePresenter>().Configure(unit, material, GroundLayer);
            return template;
        }

        private CombatUnit CreateUnit(string name, TeamId team, Vector3 position, float maxHealth)
        {
            GameObject gameObject = CreateGameObject(name);
            gameObject.transform.position = position;
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, maxHealth, 12f, 0f, 8f, 1f, true, true);
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
            gameObjects.Add(gameObject);
            return gameObject;
        }

        private void TrackCurrentOperators(OperatorRosterController roster)
        {
            foreach (OperatorRosterSlot slot in roster.Slots)
            {
                if (slot.CurrentOperator != null && !gameObjects.Contains(slot.CurrentOperator.gameObject))
                {
                    gameObjects.Add(slot.CurrentOperator.gameObject);
                }
            }
        }

        private static string GetKey(TeamId team, int index)
        {
            return $"{KeyPrefix}-{(team == TeamId.Blue ? "blue" : "red")}-{index}";
        }

        private static GameObject[] FindCorpses()
        {
            return Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(gameObject => gameObject != null
                    && gameObject.name.StartsWith(KeyPrefix, System.StringComparison.Ordinal)
                    && gameObject.name.EndsWith("_Corpse", System.StringComparison.Ordinal))
                .ToArray();
        }

        private sealed class ExitVoteFixture
        {
            public ExitVoteFixture(
                MatchFixture match,
                OperatorRosterController roster,
                TeamExitVoteController vote)
            {
                Match = match;
                Roster = roster;
                Vote = vote;
                BluePlayerKey = GetKey(TeamId.Blue, 0);
                BlueComputerKeys = new[] { GetKey(TeamId.Blue, 1), GetKey(TeamId.Blue, 2) };
                RedComputerKey = GetKey(TeamId.Red, 0);
            }

            public MatchFixture Match { get; }

            public OperatorRosterController Roster { get; }

            public TeamExitVoteController Vote { get; }

            public string BluePlayerKey { get; }

            public string[] BlueComputerKeys { get; }

            public string RedComputerKey { get; }
        }

        private sealed class MatchFixture
        {
            public MatchFixture(
                CombatUnit blueTower,
                CombatUnit redTower,
                MatchOutcomeController outcomeController)
            {
                BlueTower = blueTower;
                RedTower = redTower;
                OutcomeController = outcomeController;
            }

            public CombatUnit BlueTower { get; }

            public CombatUnit RedTower { get; }

            public MatchOutcomeController OutcomeController { get; }
        }
    }
}
