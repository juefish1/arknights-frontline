using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class TargetSelectorTests
    {
        private readonly System.Collections.Generic.List<GameObject> gameObjects = new System.Collections.Generic.List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in gameObjects)
            {
                Object.DestroyImmediate(gameObject);
            }

            gameObjects.Clear();
        }

        [Test]
        public void SelectorReturnsClosestLegalTargetInsideHorizontalRange()
        {
            CombatUnit attacker = CreateUnitAt("Blue", TeamId.Blue, Altitude.Ground, Vector3.zero, 5f, true, false);
            CombatUnit near = CreateUnitAt("Near", TeamId.Red, Altitude.Ground, new Vector3(3f, 9f, 0f), 1f, false, false);
            CreateUnitAt("Far", TeamId.Red, Altitude.Ground, new Vector3(4f, 0f, 0f), 1f, false, false);

            Assert.That(TargetSelector.FindNearestInRange(attacker), Is.EqualTo(near));
        }

        [Test]
        public void SelectorIncludesCandidateAtRangeBoundary()
        {
            CombatUnit attacker = CreateUnitAt("Blue", TeamId.Blue, Altitude.Ground, Vector3.zero, 3f, true, false);
            CombatUnit boundary = CreateUnitAt("Boundary", TeamId.Red, Altitude.Ground, new Vector3(0f, 7f, 3f), 1f, false, false);

            Assert.That(TargetSelector.FindNearestInRange(attacker), Is.EqualTo(boundary));
        }

        [Test]
        public void SelectorKeepsFirstInstanceIdWhenLegalTargetsTieOnDistance()
        {
            CombatUnit attacker = CreateUnitAt("Blue", TeamId.Blue, Altitude.Ground, Vector3.zero, 4f, true, false);
            CombatUnit firstCreated = CreateUnitAt("First", TeamId.Red, Altitude.Ground, new Vector3(-2f, 0f, 0f), 1f, false, false);
            CombatUnit secondCreated = CreateUnitAt("Second", TeamId.Red, Altitude.Ground, new Vector3(2f, 0f, 0f), 1f, false, false);
            CombatUnit expected = firstCreated.GetInstanceID() < secondCreated.GetInstanceID() ? firstCreated : secondCreated;

            Assert.That(TargetSelector.FindNearestInRange(attacker), Is.EqualTo(expected));
        }

        [Test]
        public void SelectorSkipsCandidatesRejectedByFilter()
        {
            CombatUnit attacker = CreateUnitAt("Blue", TeamId.Blue, Altitude.Ground, Vector3.zero, 6f, true, false);
            CombatUnit rejected = CreateUnitAt("Rejected", TeamId.Red, Altitude.Ground, new Vector3(2f, 0f, 0f), 0f, false, false);
            CombatUnit accepted = CreateUnitAt("Accepted", TeamId.Red, Altitude.Ground, new Vector3(4f, 0f, 0f), 0f, false, false);

            Assert.That(TargetSelector.FindNearestInRange(attacker, candidate => candidate != rejected), Is.EqualTo(accepted));
        }

        [Test]
        public void SelectorChoosesLegalInRangeCandidateNearestToClickedPoint()
        {
            CombatUnit attacker = CreateUnitAt("Blue", TeamId.Blue, Altitude.Ground, Vector3.zero, 6f, true, false);
            CreateUnitAt("NearOwner", TeamId.Red, Altitude.Ground, new Vector3(2f, 0f, 0f), 1f, false, false);
            CombatUnit nearClick = CreateUnitAt("NearClick", TeamId.Red, Altitude.Ground, new Vector3(4f, 0f, 0f), 1f, false, false);

            Assert.That(
                TargetSelector.FindNearestInRangeFromPoint(attacker, new Vector3(5f, 9f, 0f)),
                Is.EqualTo(nearClick));
        }

        [Test]
        public void PointSelectorKeepsLowestInstanceIdWhenCandidatesTieOnClickDistance()
        {
            CombatUnit attacker = CreateUnitAt("Blue", TeamId.Blue, Altitude.Ground, Vector3.zero, 6f, true, false);
            CombatUnit firstCreated = CreateUnitAt("First", TeamId.Red, Altitude.Ground, new Vector3(2f, 0f, 0f), 1f, false, false);
            CombatUnit secondCreated = CreateUnitAt("Second", TeamId.Red, Altitude.Ground, new Vector3(4f, 0f, 0f), 1f, false, false);
            CombatUnit expected = firstCreated.GetInstanceID() < secondCreated.GetInstanceID() ? firstCreated : secondCreated;

            Assert.That(TargetSelector.FindNearestInRangeFromPoint(attacker, new Vector3(3f, 0f, 0f)), Is.EqualTo(expected));
        }

        private CombatUnit CreateUnitAt(
            string name,
            TeamId team,
            Altitude altitude,
            Vector3 position,
            float attackRange,
            bool canAttackGround,
            bool canAttackAir)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.position = position;
            gameObjects.Add(gameObject);
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(team, altitude, 10f, 1f, 0f, attackRange, 1f, canAttackGround, canAttackAir);
            return unit;
        }
    }
}
