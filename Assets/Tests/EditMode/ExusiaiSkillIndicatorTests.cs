using System.Collections.Generic;
using System.Reflection;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class ExusiaiSkillIndicatorTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private CombatUnit owner;
        private ExusiaiSkillController skills;
        private SkillDashController dash;
        private ExusiaiSkillIndicator indicator;
        private PlayerCommandController commands;

        [SetUp]
        public void SetUp()
        {
            GameObject ownerObject = Track(new GameObject("Exusiai"));
            owner = ownerObject.AddComponent<CombatUnit>();
            owner.Configure(TeamId.Blue, Altitude.Ground, 100f, 50f, 0f, 6f, 0.5f, true, false);
            UnitMotor motor = ownerObject.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());
            commands = ownerObject.AddComponent<PlayerCommandController>();
            InvokePrivate(commands, "Awake");
            UnitStatModifiers modifiers = ownerObject.AddComponent<UnitStatModifiers>();
            AttackSequenceExecutor executor = ownerObject.AddComponent<AttackSequenceExecutor>();
            executor.Configure(owner);
            BasicAttackController attacks = ownerObject.AddComponent<BasicAttackController>();
            attacks.Configure(owner, executor);
            dash = ownerObject.AddComponent<SkillDashController>();
            dash.Configure(motor, ArenaLayout.CreateDefault(), 0);
            skills = ownerObject.AddComponent<ExusiaiSkillController>();
            skills.Configure(owner, commands, attacks, executor, modifiers, dash);
            indicator = ownerObject.AddComponent<ExusiaiSkillIndicator>();
            indicator.Configure(owner, skills, commands, dash);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in gameObjects)
            {
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            }
            gameObjects.Clear();
        }

        [Test]
        public void RefreshTracksSkillModesAndCreatesClosedGeometryOnce()
        {
            indicator.Refresh();
            Assert.That(indicator.Mode, Is.EqualTo(ExusiaiSkillIndicatorMode.None));
            Assert.That(indicator.RangePointCount, Is.EqualTo(65));
            Assert.That(indicator.MarkerPointCount, Is.EqualTo(33));

            Assert.That(skills.BeginChargeTargeting(), Is.True);
            indicator.Refresh();
            Assert.That(indicator.Mode, Is.EqualTo(ExusiaiSkillIndicatorMode.ChargeTargeting));
            Assert.That(indicator.RangeRadius, Is.EqualTo(6f));

            GameObject targetObject = Track(new GameObject("Target"));
            CombatUnit target = targetObject.AddComponent<CombatUnit>();
            target.Configure(TeamId.Red, Altitude.Ground, 100f, 1f, 0f, 1f, 1f, false, false);
            targetObject.transform.position = new Vector3(2f, 0f, 0f);
            Assert.That(skills.TryConfirmCharge(target.transform.position, target), Is.True);
            indicator.Refresh();
            Assert.That(indicator.Mode, Is.EqualTo(ExusiaiSkillIndicatorMode.DashWindow));
            Assert.That(indicator.RangeRadius, Is.EqualTo(7f));
        }

        [Test]
        public void CommandControllerRunsBeforeIndicatorAndIndicatorHidesWithoutCachedPointer()
        {
            DefaultExecutionOrder controllerOrder = typeof(PlayerCommandController)
                .GetCustomAttribute<DefaultExecutionOrder>();
            DefaultExecutionOrder indicatorOrder = typeof(ExusiaiSkillIndicator)
                .GetCustomAttribute<DefaultExecutionOrder>();
            Assert.That(controllerOrder, Is.Not.Null);
            Assert.That(indicatorOrder, Is.Not.Null);
            Assert.That(controllerOrder.order, Is.LessThan(indicatorOrder.order));

            Assert.That(skills.BeginChargeTargeting(), Is.True);
            indicator.Refresh();

            Assert.That(indicator.Mode, Is.EqualTo(ExusiaiSkillIndicatorMode.ChargeTargeting));
            Assert.That(indicator.IsVisible, Is.False);
        }

        [Test]
        public void CachedPointerDrawsClosedBlueOrRedTargetingAndClampedDashGeometry()
        {
            SetCachedPointer(new Vector3(3f, 0f, 0f));
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            indicator.Refresh();
            LineRenderer range = GetRenderer("rangeRenderer");
            LineRenderer marker = GetRenderer("markerRenderer");
            Assert.That(range.startColor, Is.EqualTo(Color.blue));
            Assert.That(Vector3.Distance(range.GetPosition(0), range.GetPosition(64)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(marker.GetPosition(0), marker.GetPosition(32)), Is.LessThan(0.001f));

            SetCachedPointer(new Vector3(8f, 0f, 0f));
            indicator.Refresh();
            Assert.That(range.startColor, Is.EqualTo(Color.red));

            Assert.That(skills.CancelChargeTargeting(), Is.True);
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            Assert.That(skills.TryConfirmCharge(Vector3.zero, null), Is.True);
            SetCachedPointer(new Vector3(20f, 0f, 0f));
            indicator.Refresh();
            LineRenderer arrow = GetRenderer("arrowRenderer");
            Assert.That(Vector3.Distance(owner.transform.position, indicator.DisplayedEndpoint), Is.EqualTo(7f).Within(0.001f));
            Assert.That(arrow.GetPosition(1), Is.EqualTo(new Vector3(7f, 0.05f, 0f)));

            SetCachedPointer(Vector3.zero);
            indicator.Refresh();
            Assert.That(arrow.startColor, Is.EqualTo(Color.red));
            InvokePrivate(indicator, "OnDisable");
            Assert.That(range.enabled, Is.False);
            Assert.That(arrow.enabled, Is.False);
            Assert.That(marker.enabled, Is.False);
        }

        [Test]
        public void RefreshWithoutPointerKeepsDashPreviewSafeAndDisableHidesRenderers()
        {
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            Assert.That(skills.TryConfirmCharge(Vector3.zero, null), Is.True);
            indicator.Refresh();

            Assert.That(indicator.DisplayedEndpoint, Is.EqualTo(Vector3.zero));
            Assert.That(indicator.IsVisible, Is.False);

            indicator.enabled = false;
            InvokePrivate(indicator, "OnDisable");
            Assert.That(indicator.IsVisible, Is.False);
        }

        private GameObject Track(GameObject gameObject)
        {
            gameObjects.Add(gameObject);
            return gameObject;
        }

        private LineRenderer GetRenderer(string fieldName)
        {
            return (LineRenderer)typeof(ExusiaiSkillIndicator)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(indicator);
        }

        private void SetCachedPointer(Vector3 point)
        {
            GameObject hitObject = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            hitObject.transform.position = point;
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(point + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f), Is.True);
            typeof(PlayerCommandController)
                .GetField("cachedPointerHit", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(commands, hit);
            typeof(PlayerCommandController)
                .GetField("hasCachedPointerHit", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(commands, true);
        }

        private static void InvokePrivate(object instance, string methodName)
        {
            instance.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(instance, null);
        }
    }
}
