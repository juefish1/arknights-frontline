using System.Collections.Generic;
using System.Linq;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class OperatorRosterControllerTests
    {
        private const int GroundLayer = 8;

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
                if (corpse.name.StartsWith("roster-test-") && corpse != null)
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
        public void SixConfiguredSlotsHaveUniqueKeysAndInactiveTemplatesAreNotTargets()
        {
            OperatorRosterController roster = CreateRoster();
            CombatUnit attacker = CreateUnit("roster-test-attacker", TeamId.Red, 20f);
            string[] keys =
            {
                "roster-test-blue-exusiai",
                "roster-test-blue-eyjafjalla",
                "roster-test-blue-silverash",
                "roster-test-red-exusiai",
                "roster-test-red-eyjafjalla",
                "roster-test-red-silverash"
            };

            for (int index = 0; index < keys.Length; index++)
            {
                GameObject template = CreateTemplate(keys[index] + "-template", TeamId.Blue, 100f + index);
                roster.RegisterSlot(
                    keys[index],
                    index < 3 ? TeamId.Blue : TeamId.Red,
                    (OperatorType)(index % 3),
                    template,
                    new Vector3(index, 0f, 0f),
                    index == 0);
            }

            Assert.That(roster.Slots.Count, Is.EqualTo(6));
            Assert.That(roster.Slots.Select(slot => slot.StableKey).Distinct().Count(), Is.EqualTo(6));
            Assert.That(TargetSelector.FindNearestInRange(attacker), Is.Null);
            Assert.That(roster.Slots.All(slot => !slot.Template.activeSelf), Is.True);

            roster.StartMatch();
            TrackCurrentOperators(roster);

            Assert.That(roster.Slots.All(slot => slot.CurrentOperator != null), Is.True);
            Assert.That(TargetSelector.FindNearestInRange(attacker), Is.Not.Null);
            Assert.That(roster.Slots.All(slot => !slot.Template.activeSelf), Is.True);
        }

        [Test]
        public void InitialDeploymentCreatesNewFullHealthInstanceWithStableIdentity()
        {
            OperatorRosterController roster = CreateRoster();
            const string stableKey = "roster-test-player-exusiai";
            GameObject template = CreateTemplate("roster-test-player-template", TeamId.Blue, 1000f);
            roster.RegisterSlot(
                stableKey,
                TeamId.Blue,
                OperatorType.Exusiai,
                template,
                new Vector3(3f, 0f, -2f),
                true);

            roster.StartMatch();
            TrackCurrentOperators(roster);

            OperatorRosterSlot slot = roster.Slots[0];
            CombatUnit live = slot.CurrentOperator;
            OperatorIdentity identity = live.GetComponent<OperatorIdentity>();
            Assert.That(live.gameObject, Is.Not.SameAs(template));
            Assert.That(live.gameObject.activeInHierarchy, Is.True);
            Assert.That(live.gameObject.name, Is.EqualTo(stableKey));
            Assert.That(live.MaxHealth, Is.EqualTo(1000f));
            Assert.That(live.CurrentHealth, Is.EqualTo(1000f));
            Assert.That(identity.StableKey, Is.EqualTo(stableKey));
            Assert.That(identity.Team, Is.EqualTo(TeamId.Blue));
            Assert.That(identity.OperatorType, Is.EqualTo(OperatorType.Exusiai));
            Assert.That(slot.DepartureCount, Is.Zero);
            Assert.That(slot.RedeployRemaining, Is.Zero);
        }

        [Test]
        public void DeathUsesIncreasingCappedDelaysAndRedeploymentClearsOnlyMatchingCorpse()
        {
            OperatorRosterController roster = CreateRoster();
            const string stableKey = "roster-test-blue-eyjafjalla";
            GameObject template = CreateTemplate("roster-test-eyjafjalla-template", TeamId.Blue, 100f);
            OperatorRosterSlot slot = roster.RegisterSlot(
                stableKey,
                TeamId.Blue,
                OperatorType.Eyjafjalla,
                template,
                Vector3.zero);
            roster.StartMatch();

            GameObject unrelatedCorpse = CreateCorpse("roster-test-unrelated_Corpse", UnitKind.Operator, "roster-test-unrelated-operator");
            GameObject minionCorpse = CreateCorpse("roster-test-minion_Corpse", UnitKind.Minion, null);
            GameObject towerCorpse = CreateCorpse("roster-test-tower_Corpse", UnitKind.Tower, "roster-test-tower");
            float[] expectedDelays = { 8f, 12f, 16f, 20f, 24f, 24f };

            for (int index = 0; index < expectedDelays.Length; index++)
            {
                CombatUnit oldInstance = slot.CurrentOperator;
                if (index == 0)
                {
                    oldInstance.GetComponent<UnitStatModifiers>()
                        .SetMovementSpeedMultiplier("roster-test-stale-effect", 0.1f);
                }

                oldInstance.TakePhysicalDamage(oldInstance.MaxHealth);
                Assert.That(slot.CurrentOperator, Is.Null);
                Assert.That(slot.DepartureCount, Is.EqualTo(index + 1));
                Assert.That(slot.RedeployRemaining, Is.EqualTo(expectedDelays[index]).Within(0.0001f));
                Assert.That(FindCorpse(stableKey + "_Corpse"), Is.Not.Null);

                roster.Tick(expectedDelays[index] - 0.01f);
                Assert.That(slot.CurrentOperator, Is.Null);
                roster.Tick(0.01f);
                TrackCurrentOperators(roster);

                Assert.That(slot.CurrentOperator, Is.Not.Null);
                Assert.That(slot.CurrentOperator, Is.Not.SameAs(oldInstance));
                Assert.That(slot.CurrentOperator.CurrentHealth, Is.EqualTo(slot.CurrentOperator.MaxHealth));
                Assert.That(
                    slot.CurrentOperator.GetComponent<UnitStatModifiers>().ApplyMovementSpeed(10f),
                    Is.EqualTo(10f));
                Assert.That(FindCorpse(stableKey + "_Corpse"), Is.Null);
                Assert.That(unrelatedCorpse, Is.Not.Null);
                Assert.That(minionCorpse, Is.Not.Null);
                Assert.That(towerCorpse, Is.Not.Null);
            }
        }

        [Test]
        public void SuccessfulRetreatUsesSeventyPercentDelayAndCreatesNoCorpse()
        {
            OperatorRosterController roster = CreateRoster();
            const string stableKey = "roster-test-retreating-operator";
            GameObject template = CreateTemplate("roster-test-retreat-template", TeamId.Blue, 100f);
            OperatorRosterSlot slot = roster.RegisterSlot(
                stableKey,
                TeamId.Blue,
                OperatorType.SilverAsh,
                template,
                Vector3.zero);
            int departedNotifications = 0;
            roster.OperatorDeparted += (_, __) => departedNotifications++;
            roster.StartMatch();
            TrackCurrentOperators(roster);
            CombatUnit live = slot.CurrentOperator;

            Assert.That(roster.NotifySuccessfulRetreat(live), Is.True);
            Assert.That(live == null, Is.True);
            Assert.That(slot.DepartureCount, Is.EqualTo(1));
            Assert.That(slot.RedeployRemaining, Is.EqualTo(5.6f).Within(0.0001f));
            Assert.That(FindCorpse(stableKey + "_Corpse"), Is.Null);
            Assert.That(roster.NotifySuccessfulRetreat(live), Is.False);
            Assert.That(slot.DepartureCount, Is.EqualTo(1));
            Assert.That(departedNotifications, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateDepartureAndMatchStopCannotCauseLateDeployment()
        {
            OperatorRosterController roster = CreateRoster();
            GameObject template = CreateTemplate("roster-test-stop-template", TeamId.Blue, 100f);
            OperatorRosterSlot slot = roster.RegisterSlot(
                "roster-test-stopped-operator",
                TeamId.Blue,
                OperatorType.Exusiai,
                template,
                Vector3.zero);
            int departedNotifications = 0;
            roster.OperatorDeparted += (_, __) => departedNotifications++;
            roster.StartMatch();
            TrackCurrentOperators(roster);
            CombatUnit deadInstance = slot.CurrentOperator;
            deadInstance.TakePhysicalDamage(deadInstance.MaxHealth);

            Assert.That(roster.NotifySuccessfulRetreat(deadInstance), Is.False);
            Assert.That(slot.DepartureCount, Is.EqualTo(1));
            Assert.That(departedNotifications, Is.EqualTo(1));

            roster.StopForMatch();
            roster.Tick(100f);

            Assert.That(slot.IsStopped, Is.True);
            Assert.That(slot.CurrentOperator, Is.Null);
            Assert.That(slot.RedeployRemaining, Is.Zero);
        }

        private OperatorRosterController CreateRoster()
        {
            GameObject gameObject = new GameObject("roster-test-controller");
            gameObjects.Add(gameObject);
            return gameObject.AddComponent<OperatorRosterController>();
        }

        private GameObject CreateTemplate(string name, TeamId team, float maxHealth)
        {
            GameObject template = new GameObject(name);
            gameObjects.Add(template);
            CombatUnit unit = template.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, maxHealth, 20f, 2f, 6f, 0.5f, true, true);
            template.AddComponent<OperatorIdentity>();
            template.AddComponent<UnitStatModifiers>();
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            materials.Add(material);
            template.AddComponent<MeshRenderer>().sharedMaterial = material;
            template.AddComponent<DeathCorpsePresenter>().Configure(unit, material, GroundLayer);
            template.SetActive(false);
            return template;
        }

        private CombatUnit CreateUnit(string name, TeamId team, float maxHealth)
        {
            GameObject gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, Altitude.Ground, maxHealth, 20f, 2f, 100f, 1f, true, true);
            return unit;
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

        private GameObject CreateCorpse(string name, UnitKind kind, string ownerKey)
        {
            GameObject corpse = new GameObject(name);
            gameObjects.Add(corpse);
            CorpseLifetimeController lifetime = corpse.AddComponent<CorpseLifetimeController>();
            lifetime.Configure(kind, ownerKey);
            return corpse;
        }

        private static GameObject[] FindCorpses()
        {
            return Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(gameObject => gameObject != null && gameObject.name.EndsWith("_Corpse"))
                .ToArray();
        }

        private static GameObject FindCorpse(string name)
        {
            return FindCorpses().FirstOrDefault(corpse => corpse.name == name);
        }
    }
}
