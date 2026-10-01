using System;
using System.Collections.Generic;
using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using MatchOutcome = ArknightsFrontline.Common.MatchOutcome;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class MatchStatisticsControllerTests
    {
        private const string BlueExusiaiKey = "stats-test-blue-exusiai";
        private const string BlueEyjafjallaKey = "stats-test-blue-eyjafjalla";
        private const string BlueSilverAshKey = "stats-test-blue-silverash";
        private const string RedExusiaiKey = "stats-test-red-exusiai";
        private const string RedEyjafjallaKey = "stats-test-red-eyjafjalla";
        private const string RedSilverAshKey = "stats-test-red-silverash";

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

            foreach (GameObject corpse in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (corpse != null && corpse.name.StartsWith("stats-test-", StringComparison.Ordinal)
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

            gameObjects.Clear();
            materials.Clear();
        }

        [Test]
        public void ConfigureCreatesSixStableRowsAndBindsAlreadySpawnedLives()
        {
            MatchFixture fixture = CreateFixture("stats-test-initial", configureStatistics: false);
            Assert.That(fixture.Roster.Slots.All(slot => slot.CurrentOperator != null), Is.True);

            fixture.Statistics.Configure(
                fixture.Match,
                fixture.Roster,
                fixture.BlueTower,
                fixture.RedTower);

            IReadOnlyList<MatchResultRow> rows = fixture.Statistics.GetRows();
            Assert.That(rows.Count, Is.EqualTo(6));
            Assert.That(rows.Select(row => row.StableKey), Is.EqualTo(new[]
            {
                BlueExusiaiKey,
                BlueEyjafjallaKey,
                BlueSilverAshKey,
                RedExusiaiKey,
                RedEyjafjallaKey,
                RedSilverAshKey
            }));
            Assert.That(rows.All(row => row.Kills == 0 && row.Deaths == 0 && row.TowerDamage == 0f), Is.True);

            CombatUnit blueExusiai = GetLive(fixture, BlueExusiaiKey);
            CombatUnit redExusiai = GetLive(fixture, RedExusiaiKey);
            redExusiai.TakePhysicalDamage(redExusiai.MaxHealth + 50f, blueExusiai);
            fixture.RedTower.TakePhysicalDamage(fixture.RedTower.MaxHealth);
            fixture.Match.Tick();

            MatchResultSnapshot snapshot = fixture.Statistics.Snapshot;
            Assert.That(fixture.Statistics.IsFrozen, Is.True);
            Assert.That(GetRow(snapshot, BlueExusiaiKey).Kills, Is.EqualTo(1));
            Assert.That(GetRow(snapshot, RedExusiaiKey).Deaths, Is.EqualTo(1));
        }

        [Test]
        public void ConfigureWithTheSameReferencesDoesNotDuplicateDamageSubscriptions()
        {
            MatchFixture fixture = CreateFixture("stats-test-idempotent");
            fixture.Statistics.Configure(
                fixture.Match,
                fixture.Roster,
                fixture.BlueTower,
                fixture.RedTower);

            CombatUnit blueExusiai = GetLive(fixture, BlueExusiaiKey);
            CombatUnit redExusiai = GetLive(fixture, RedExusiaiKey);
            redExusiai.TakePhysicalDamage(redExusiai.MaxHealth, blueExusiai);
            fixture.RedTower.TakePhysicalDamage(fixture.RedTower.MaxHealth);
            fixture.Match.Tick();

            Assert.That(GetRow(fixture.Statistics.Snapshot, BlueExusiaiKey).Kills, Is.EqualTo(1));
            Assert.That(GetRow(fixture.Statistics.Snapshot, RedExusiaiKey).Deaths, Is.EqualTo(1));
        }

        [Test]
        public void DeathsCountWithoutCreditForNullMinionFriendlyOrUnregisteredSources()
        {
            MatchFixture fixture = CreateFixture("stats-test-filtering");
            CombatUnit blueExusiai = GetLive(fixture, BlueExusiaiKey);
            CombatUnit blueEyjafjalla = GetLive(fixture, BlueEyjafjallaKey);
            CombatUnit blueSilverAsh = GetLive(fixture, BlueSilverAshKey);
            CombatUnit redExusiai = GetLive(fixture, RedExusiaiKey);
            CombatUnit redEyjafjalla = GetLive(fixture, RedEyjafjallaKey);
            CombatUnit redSilverAsh = GetLive(fixture, RedSilverAshKey);

            GameObject copiedIdentityObject = CreateCombatObject("stats-test-copied-identity", TeamId.Blue, 100f);
            OperatorIdentity copiedIdentity = copiedIdentityObject.AddComponent<OperatorIdentity>();
            copiedIdentity.Configure(BlueExusiaiKey, TeamId.Blue, OperatorType.Exusiai);
            CombatUnit copiedBlueIdentity = copiedIdentityObject.GetComponent<CombatUnit>();

            CombatUnit unregisteredMinion = CreateCombatUnit("stats-test-minion-attacker", TeamId.Red, 100f);
            redExusiai.TakePhysicalDamage(redExusiai.MaxHealth, null);
            redEyjafjalla.TakePhysicalDamage(redEyjafjalla.MaxHealth, copiedBlueIdentity);
            blueSilverAsh.TakePhysicalDamage(blueSilverAsh.MaxHealth, blueEyjafjalla);
            redSilverAsh.TakePhysicalDamage(redSilverAsh.MaxHealth, unregisteredMinion);

            CombatUnit minionVictim = CreateCombatUnit("stats-test-minion-victim", TeamId.Red, 10f);
            minionVictim.TakePhysicalDamage(minionVictim.MaxHealth, blueExusiai);

            fixture.RedTower.TakePhysicalDamage(fixture.RedTower.MaxHealth);
            fixture.Match.Tick();

            MatchResultSnapshot snapshot = fixture.Statistics.Snapshot;
            Assert.That(GetRow(snapshot, BlueExusiaiKey).Kills, Is.Zero);
            Assert.That(GetRow(snapshot, BlueExusiaiKey).Deaths, Is.Zero);
            Assert.That(GetRow(snapshot, BlueEyjafjallaKey).Kills, Is.Zero);
            Assert.That(GetRow(snapshot, BlueEyjafjallaKey).Deaths, Is.Zero);
            Assert.That(GetRow(snapshot, BlueSilverAshKey).Deaths, Is.EqualTo(1));
            Assert.That(GetRow(snapshot, RedExusiaiKey).Deaths, Is.EqualTo(1));
            Assert.That(GetRow(snapshot, RedEyjafjallaKey).Deaths, Is.EqualTo(1));
            Assert.That(GetRow(snapshot, RedSilverAshKey).Deaths, Is.EqualTo(1));
            Assert.That(snapshot.Rows.Sum(row => row.Kills), Is.Zero);
        }

        [Test]
        public void ReentrantDamageCountsOnlyTheNestedLethalAttackerAndOneDeath()
        {
            MatchFixture fixture = CreateFixture("stats-test-reentrant", configureStatistics: false);
            CombatUnit victim = GetLive(fixture, RedExusiaiKey);
            CombatUnit outerAttacker = GetLive(fixture, BlueExusiaiKey);
            CombatUnit nestedLethalAttacker = GetLive(fixture, BlueEyjafjallaKey);
            bool nestedHitApplied = false;

            victim.DamageTaken += (_, __) =>
            {
                if (nestedHitApplied)
                {
                    return;
                }

                nestedHitApplied = true;
                victim.TakePhysicalDamage(victim.MaxHealth + 1f, nestedLethalAttacker);
            };

            fixture.Statistics.Configure(
                fixture.Match,
                fixture.Roster,
                fixture.BlueTower,
                fixture.RedTower);
            victim.TakePhysicalDamage(1f, outerAttacker);

            fixture.RedTower.TakePhysicalDamage(fixture.RedTower.MaxHealth);
            fixture.Match.Tick();

            MatchResultSnapshot snapshot = fixture.Statistics.Snapshot;
            Assert.That(GetRow(snapshot, RedExusiaiKey).Deaths, Is.EqualTo(1));
            Assert.That(GetRow(snapshot, BlueEyjafjallaKey).Kills, Is.EqualTo(1));
            Assert.That(GetRow(snapshot, BlueExusiaiKey).Kills, Is.Zero);
        }

        [Test]
        public void RetreatDoesNotCountAsDeathAndStatsAccumulateAcrossRedeployments()
        {
            MatchFixture fixture = CreateFixture("stats-test-redeploy");
            OperatorRosterSlot redSlot = GetSlot(fixture, RedExusiaiKey);
            CombatUnit blueExusiai = GetLive(fixture, BlueExusiaiKey);

            Assert.That(fixture.Roster.NotifySuccessfulRetreat(redSlot.CurrentOperator), Is.True);
            fixture.Roster.Tick(5.6f);
            Assert.That(redSlot.CurrentOperator, Is.Not.Null);
            redSlot.CurrentOperator.TakePhysicalDamage(redSlot.CurrentOperator.MaxHealth, blueExusiai);

            fixture.Roster.Tick(12f);
            Assert.That(redSlot.CurrentOperator, Is.Not.Null);
            redSlot.CurrentOperator.TakePhysicalDamage(redSlot.CurrentOperator.MaxHealth, blueExusiai);

            fixture.RedTower.TakePhysicalDamage(fixture.RedTower.MaxHealth);
            fixture.Match.Tick();

            MatchResultSnapshot snapshot = fixture.Statistics.Snapshot;
            Assert.That(GetRow(snapshot, RedExusiaiKey).Deaths, Is.EqualTo(2));
            Assert.That(GetRow(snapshot, BlueExusiaiKey).Kills, Is.EqualTo(2));
        }

        [Test]
        public void TowerDamageUsesActualEnemyTowerHealthLossAndRejectsFriendlyOrUnregisteredDamage()
        {
            MatchFixture fixture = CreateFixture("stats-test-tower-damage");
            CombatUnit blueExusiai = GetLive(fixture, BlueExusiaiKey);
            CombatUnit redExusiai = GetLive(fixture, RedExusiaiKey);

            fixture.RedTower.TakePhysicalDamage(25f, blueExusiai);
            fixture.RedTower.TakePhysicalDamage(1000f, blueExusiai);
            fixture.BlueTower.TakePhysicalDamage(45f, redExusiai);

            CombatUnit copiedIdentity = CreateCombatUnit("stats-test-tower-fake", TeamId.Blue, 100f);
            OperatorIdentity copiedOperatorIdentity = copiedIdentity.gameObject.AddComponent<OperatorIdentity>();
            copiedOperatorIdentity.Configure(BlueExusiaiKey, TeamId.Blue, OperatorType.Exusiai);
            fixture.BlueTower.TakePhysicalDamage(20f, copiedIdentity);

            fixture.Match.Tick();

            MatchResultSnapshot snapshot = fixture.Statistics.Snapshot;
            Assert.That(snapshot.Outcome, Is.EqualTo(MatchOutcome.BlueVictory));
            Assert.That(snapshot.RedTowerCurrentHealth, Is.Zero);
            Assert.That(snapshot.RedTowerMaxHealth, Is.EqualTo(fixture.RedTower.MaxHealth));
            Assert.That(GetRow(snapshot, BlueExusiaiKey).TowerDamage, Is.EqualTo(100f));
            Assert.That(GetRow(snapshot, RedExusiaiKey).TowerDamage, Is.EqualTo(45f));
        }

        [Test]
        public void SnapshotUsesCachedTowerHealthAfterDeathPresenterDestroysTowerInEditMode()
        {
            MatchFixture fixture = CreateFixture("stats-test-destroyed-tower");
            GameObject towerObject = fixture.RedTower.gameObject;
            Material towerMaterial = CreateMaterial(Color.red);
            towerObject.AddComponent<MeshRenderer>().sharedMaterial = towerMaterial;
            towerObject.AddComponent<DeathCorpsePresenter>()
                .Configure(fixture.RedTower, towerMaterial, 0, UnitKind.Tower);

            fixture.RedTower.TakePhysicalDamage(fixture.RedTower.MaxHealth);

            Assert.That(towerObject == null, Is.True, "EditMode death presentation should DestroyImmediate the tower.");

            fixture.Match.Tick();

            MatchResultSnapshot snapshot = fixture.Statistics.Snapshot;
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot.Outcome, Is.EqualTo(MatchOutcome.BlueVictory));
            Assert.That(snapshot.RedTowerCurrentHealth, Is.Zero);
            Assert.That(snapshot.RedTowerMaxHealth, Is.EqualTo(100f));
        }

        [Test]
        public void FriendlyTowerDamageIsExcludedWhileOpposingOperatorDamageCounts()
        {
            MatchFixture fixture = CreateFixture("stats-test-friendly-tower");
            CombatUnit blueExusiai = GetLive(fixture, BlueExusiaiKey);
            CombatUnit redExusiai = GetLive(fixture, RedExusiaiKey);

            fixture.BlueTower.TakePhysicalDamage(20f, blueExusiai);
            fixture.BlueTower.TakePhysicalDamage(11f, redExusiai);
            fixture.RedTower.TakePhysicalDamage(fixture.RedTower.MaxHealth);
            fixture.Match.Tick();

            Assert.That(GetRow(fixture.Statistics.Snapshot, BlueExusiaiKey).TowerDamage, Is.Zero);
            Assert.That(GetRow(fixture.Statistics.Snapshot, RedExusiaiKey).TowerDamage, Is.EqualTo(11f));
        }

        [Test]
        public void ReconfigureAndUnbindDetachOldExactDelegatesAndResetRows()
        {
            MatchFixture first = CreateFixture("stats-test-rebind-first");
            MatchFixture second = CreateFixture("stats-test-rebind-second", configureStatistics: false);
            CombatUnit firstBlue = GetLive(first, BlueExusiaiKey);
            CombatUnit secondBlue = GetLive(second, BlueExusiaiKey);

            first.RedTower.TakePhysicalDamage(10f, firstBlue);
            Assert.That(GetRow(first.Statistics.GetRows(), BlueExusiaiKey).TowerDamage, Is.EqualTo(10f));

            first.Statistics.Configure(
                second.Match,
                second.Roster,
                second.BlueTower,
                second.RedTower);

            Assert.That(GetRow(first.Statistics.GetRows(), BlueExusiaiKey).TowerDamage, Is.Zero);
            first.RedTower.TakePhysicalDamage(20f, firstBlue);
            Assert.That(GetRow(first.Statistics.GetRows(), BlueExusiaiKey).TowerDamage, Is.Zero);

            second.RedTower.TakePhysicalDamage(9f, secondBlue);
            Assert.That(GetRow(first.Statistics.GetRows(), BlueExusiaiKey).TowerDamage, Is.EqualTo(9f));

            first.Statistics.Unbind();
            second.RedTower.TakePhysicalDamage(5f, secondBlue);
            Assert.That(GetRow(first.Statistics.GetRows(), BlueExusiaiKey).TowerDamage, Is.EqualTo(9f));
        }

        [Test]
        public void SameFrameTowerDeathsFreezeOneImmutableSnapshotAfterBothHits()
        {
            MatchFixture fixture = CreateFixture("stats-test-same-frame");
            CombatUnit blueExusiai = GetLive(fixture, BlueExusiaiKey);
            CombatUnit redExusiai = GetLive(fixture, RedExusiaiKey);
            int resultReadyCount = 0;
            MatchResultSnapshot callbackSnapshot = null;
            fixture.Statistics.ResultReady += snapshot =>
            {
                resultReadyCount++;
                callbackSnapshot = snapshot;
                Assert.That(fixture.Statistics.IsFrozen, Is.True);
                Assert.That(fixture.Statistics.Snapshot, Is.SameAs(snapshot));
            };

            fixture.RedTower.TakePhysicalDamage(1000f, blueExusiai);
            fixture.BlueTower.TakePhysicalDamage(1000f, redExusiai);

            Assert.That(fixture.Statistics.IsFrozen, Is.False);
            Assert.That(fixture.Statistics.Snapshot, Is.Null);

            fixture.Match.Tick();
            fixture.Match.Tick();

            Assert.That(resultReadyCount, Is.EqualTo(1));
            Assert.That(callbackSnapshot, Is.SameAs(fixture.Statistics.Snapshot));
            Assert.That(callbackSnapshot.Outcome, Is.EqualTo(MatchOutcome.Draw));
            Assert.That(GetRow(callbackSnapshot, BlueExusiaiKey).TowerDamage, Is.EqualTo(100f));
            Assert.That(GetRow(callbackSnapshot, RedExusiaiKey).TowerDamage, Is.EqualTo(100f));
            Assert.That(callbackSnapshot.ElapsedSeconds, Is.EqualTo(fixture.Match.ElapsedSeconds));

            MatchResultRow savedRow = GetRow(callbackSnapshot, BlueExusiaiKey);
            GetLive(fixture, BlueEyjafjallaKey).TakePhysicalDamage(1000f, redExusiai);
            Assert.That(GetRow(callbackSnapshot, BlueExusiaiKey), Is.EqualTo(savedRow));
            Assert.That(fixture.Statistics.GetRows().ToArray(), Is.EqualTo(callbackSnapshot.Rows.ToArray()));

            IList<MatchResultRow> readOnlyRows = (IList<MatchResultRow>)callbackSnapshot.Rows;
            Assert.That(readOnlyRows.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => readOnlyRows[0] = savedRow);
        }

        [Test]
        public void ConfigureAfterResolvedMatchFreezesImmediately()
        {
            MatchFixture fixture = CreateFixture("stats-test-late-configure", configureStatistics: false);
            fixture.RedTower.TakePhysicalDamage(fixture.RedTower.MaxHealth);
            fixture.Match.Tick();

            fixture.Statistics.ResultReady += _ => fixture.ResultReadyCount++;
            fixture.Statistics.Configure(
                fixture.Match,
                fixture.Roster,
                fixture.BlueTower,
                fixture.RedTower);

            Assert.That(fixture.Statistics.IsFrozen, Is.True);
            Assert.That(fixture.Statistics.Snapshot, Is.Not.Null);
            Assert.That(fixture.Statistics.Snapshot.Outcome, Is.EqualTo(MatchOutcome.BlueVictory));
            Assert.That(fixture.Statistics.Snapshot.Rows.Count, Is.EqualTo(6));
            Assert.That(fixture.ResultReadyCount, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ConfigureRejectsMissingDependencies(int missingDependency)
        {
            MatchFixture fixture = CreateFixture("stats-test-null", configureStatistics: false);

            Assert.Throws<ArgumentNullException>(() =>
            {
                fixture.Statistics.Configure(
                    missingDependency == 0 ? null : fixture.Match,
                    missingDependency == 1 ? null : fixture.Roster,
                    missingDependency == 2 ? null : fixture.BlueTower,
                    missingDependency == 3 ? null : fixture.RedTower);
            });
        }

        [Test]
        public void ConfigureRejectsRosterWithFewerThanSixSlots()
        {
            MatchFixture fixture = CreateFixture(
                "stats-test-roster-short",
                configureStatistics: false,
                slotTeams: new[] { TeamId.Blue, TeamId.Blue, TeamId.Blue, TeamId.Red, TeamId.Red });

            Assert.Throws<InvalidOperationException>(() => fixture.Statistics.Configure(
                fixture.Match,
                fixture.Roster,
                fixture.BlueTower,
                fixture.RedTower));
        }

        [Test]
        public void ConfigureRejectsSixSlotsWithUnbalancedTeams()
        {
            MatchFixture fixture = CreateFixture(
                "stats-test-roster-unbalanced",
                configureStatistics: false,
                slotTeams: new[] { TeamId.Blue, TeamId.Blue, TeamId.Red, TeamId.Red, TeamId.Red, TeamId.Red });

            Assert.Throws<InvalidOperationException>(() => fixture.Statistics.Configure(
                fixture.Match,
                fixture.Roster,
                fixture.BlueTower,
                fixture.RedTower));
        }

        [Test]
        public void ConfigureRejectsBlueAndRedTowerReferencesSwapped()
        {
            MatchFixture fixture = CreateFixture("stats-test-swapped-towers", configureStatistics: false);

            Assert.Throws<ArgumentException>(() => fixture.Statistics.Configure(
                fixture.Match,
                fixture.Roster,
                fixture.RedTower,
                fixture.BlueTower));
        }

        private MatchFixture CreateFixture(
            string objectPrefix,
            bool configureStatistics = true,
            TeamId[] slotTeams = null)
        {
            ArenaLayout layout = ArenaLayout.CreateDefault();
            CombatUnit blueTower = CreateCombatUnit($"{objectPrefix}-blue-tower", TeamId.Blue, 100f);
            CombatUnit redTower = CreateCombatUnit($"{objectPrefix}-red-tower", TeamId.Red, 100f);
            GameObject minionParent = CreateGameObject($"{objectPrefix}-minions");
            MinionWaveSpawner spawner = CreateGameObject($"{objectPrefix}-spawner")
                .AddComponent<MinionWaveSpawner>();
            spawner.Configure(
                minionParent.transform,
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
            string[] keys =
            {
                BlueExusiaiKey,
                BlueEyjafjallaKey,
                BlueSilverAshKey,
                RedExusiaiKey,
                RedEyjafjallaKey,
                RedSilverAshKey
            };
            TeamId[] configuredTeams = slotTeams ?? new[]
            {
                TeamId.Blue,
                TeamId.Blue,
                TeamId.Blue,
                TeamId.Red,
                TeamId.Red,
                TeamId.Red
            };
            for (int index = 0; index < configuredTeams.Length; index++)
            {
                TeamId team = configuredTeams[index];
                OperatorType operatorType = (OperatorType)(index % 3);
                GameObject template = CreateOperatorTemplate(
                    $"{objectPrefix}-{keys[index]}-template",
                    team,
                    operatorType);
                roster.RegisterSlot(
                    keys[index],
                    team,
                    operatorType,
                    template,
                    new Vector3(index * 2f, 0f, 0f),
                    index == 0);
            }

            roster.StartMatch();
            MatchStatisticsController statistics = CreateGameObject($"{objectPrefix}-statistics")
                .AddComponent<MatchStatisticsController>();
            if (configureStatistics)
            {
                statistics.Configure(match, roster, blueTower, redTower);
            }

            return new MatchFixture(match, roster, blueTower, redTower, statistics);
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
            template.AddComponent<DeathCorpsePresenter>().Configure(unit, material, 8);
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

        private GameObject CreateCombatObject(string name, TeamId team, float maxHealth)
        {
            GameObject gameObject = CreateGameObject(name);
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, maxHealth, 20f, 0f, 6f, 0.5f, true, true);
            return gameObject;
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

        private void Track(GameObject gameObject)
        {
            if (gameObject != null && !gameObjects.Contains(gameObject))
            {
                gameObjects.Add(gameObject);
            }
        }

        private static CombatUnit GetLive(MatchFixture fixture, string stableKey)
        {
            return GetSlot(fixture, stableKey).CurrentOperator;
        }

        private static OperatorRosterSlot GetSlot(MatchFixture fixture, string stableKey)
        {
            return fixture.Roster.Slots.Single(slot => slot.StableKey == stableKey);
        }

        private static MatchResultRow GetRow(MatchResultSnapshot snapshot, string stableKey)
        {
            return GetRow(snapshot.Rows, stableKey);
        }

        private static MatchResultRow GetRow(IReadOnlyList<MatchResultRow> rows, string stableKey)
        {
            return rows.Single(row => row.StableKey == stableKey);
        }

        private sealed class MatchFixture
        {
            public MatchFixture(
                MatchOutcomeController match,
                OperatorRosterController roster,
                CombatUnit blueTower,
                CombatUnit redTower,
                MatchStatisticsController statistics)
            {
                Match = match;
                Roster = roster;
                BlueTower = blueTower;
                RedTower = redTower;
                Statistics = statistics;
            }

            public MatchOutcomeController Match { get; }

            public OperatorRosterController Roster { get; }

            public CombatUnit BlueTower { get; }

            public CombatUnit RedTower { get; }

            public MatchStatisticsController Statistics { get; }

            public int ResultReadyCount { get; set; }
        }
    }
}
