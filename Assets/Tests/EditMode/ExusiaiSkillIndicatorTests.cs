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

            Assert.That(skills.BeginChargeTargeting(), Is.True);
            indicator.Refresh();
            Assert.That(indicator.Mode, Is.EqualTo(ExusiaiSkillIndicatorMode.ChargeTargeting));
            Assert.That(indicator.RangeRadius, Is.EqualTo(7f));

            GameObject targetObject = Track(new GameObject("Target"));
            CombatUnit target = targetObject.AddComponent<CombatUnit>();
            target.Configure(TeamId.Red, Altitude.Ground, 100f, 1f, 0f, 1f, 1f, false, false);
            targetObject.transform.position = new Vector3(2f, 0f, 0f);
            Assert.That(skills.TryConfirmCharge(target.transform.position, target), Is.True);
            indicator.Refresh();
            Assert.That(indicator.IsVisible, Is.True);
            Assert.That(indicator.RangeRadius, Is.EqualTo(7f));
            Assert.That(indicator.DisplayedEndpoint, Is.EqualTo(dash.Destination));
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
        public void TargetingPreviewsResolvedDashEndpointAndSevenMeterRange()
        {
            SetCachedPointer(new Vector3(20f, 0f, 0f));
            Assert.That(skills.BeginChargeTargeting(), Is.True);

            indicator.Refresh();

            Assert.That(indicator.Mode, Is.EqualTo(ExusiaiSkillIndicatorMode.ChargeTargeting));
            Assert.That(indicator.RangeRadius, Is.EqualTo(7f));
            Assert.That(indicator.DisplayedEndpoint, Is.EqualTo(new Vector3(7f, 0f, 0f)));
            LineRenderer range = GetRenderer("rangeRenderer");
            Assert.That(range.startColor, Is.EqualTo(Color.blue));
            Assert.That(Vector3.Distance(range.GetPosition(0), range.GetPosition(64)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(range.GetPosition(0), new Vector3(0f, 0.05f, 0f)), Is.EqualTo(7f).Within(0.001f));
            LineRenderer arrow = GetRenderer("arrowRenderer");
            Assert.That(arrow.enabled, Is.True);
            Assert.That(arrow.GetPosition(1), Is.EqualTo(new Vector3(7f, 0.05f, 0f)));
        }

        [Test]
        public void TargetingDoesNotLockClickedEnemyBeforeArrival()
        {
            GameObject targetObject = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            CombatUnit target = targetObject.AddComponent<CombatUnit>();
            target.Configure(TeamId.Red, Altitude.Ground, 100f, 1f, 0f, 1f, 1f, false, false);
            targetObject.transform.position = Vector3.right * 4f;
            Physics.SyncTransforms();
            SetCachedPointer(target.transform.position);
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            indicator.Refresh();

            Assert.That(skills.SelectedChargeTarget, Is.Null);
            Assert.That(EnabledLoopRendererCount(), Is.EqualTo(1));
            Assert.That(skills.TryConfirmCharge(target.transform.position, target), Is.True);
            dash.Tick(0.05f);
            indicator.Refresh();

            Assert.That(skills.SelectedChargeTarget, Is.Null);
            Assert.That(dash.IsDashing, Is.True);
            Assert.That(EnabledLoopRendererCount(), Is.Zero);
        }

        [Test]
        public void InvalidPreviewIsRedAndDoesNotEnableDash()
        {
            SetCachedPointer(Vector3.zero);
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            indicator.Refresh();

            LineRenderer range = GetRenderer("rangeRenderer");
            LineRenderer arrow = GetRenderer("arrowRenderer");
            Assert.That(range.startColor, Is.EqualTo(Color.red));
            Assert.That(arrow.startColor, Is.EqualTo(Color.red));
            Assert.That(arrow.enabled, Is.True);
            Assert.That(indicator.DisplayedEndpoint, Is.EqualTo(owner.transform.position));
            Assert.That(skills.TryConfirmCharge(Vector3.zero, null), Is.False);
            Assert.That(skills.IsSelectingChargeTarget, Is.True);
            Assert.That(dash.IsDashing, Is.False);
        }

        [Test]
        public void ConfirmedDashShowsPathThenFadesInPointOneFiveSeconds()
        {
            SetCachedPointer(new Vector3(20f, 0f, 0f));
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            indicator.Refresh();

            Assert.That(skills.TryConfirmCharge(new Vector3(20f, 0f, 0f), null), Is.True);
            Vector3 destination = dash.Destination;
            SetCachedPointer(new Vector3(0f, 0f, 4f));
            indicator.Refresh();

            LineRenderer arrow = GetRenderer("arrowRenderer");
            Assert.That(arrow.enabled, Is.True);
            Assert.That(indicator.DisplayedEndpoint, Is.EqualTo(destination));
            Assert.That(arrow.GetPosition(1), Is.EqualTo(new Vector3(7f, 0.05f, 0f)));

            dash.Tick(0.25f);
            InvokePrivate(indicator, "RefreshWithDelta", 0f);
            Assert.That(arrow.enabled, Is.True);

            InvokePrivate(indicator, "RefreshWithDelta", 0.149f);
            Assert.That(arrow.enabled, Is.True);
            InvokePrivate(indicator, "RefreshWithDelta", 0.002f);
            Assert.That(arrow.enabled, Is.False);
        }

        [Test]
        public void ShortDashCompletedBeforeIndicatorRefreshStillShowsArrivalTrail()
        {
            Vector3 previewPoint = Vector3.right * 0.08f;
            Vector3 clickedPoint = Vector3.right * 0.12f;
            SetCachedPointer(previewPoint);
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            indicator.Refresh();
            Assert.That(GetRenderer("arrowRenderer").enabled, Is.True);
            Assert.That(indicator.DisplayedEndpoint, Is.EqualTo(previewPoint));

            Assert.That(skills.TryConfirmCharge(clickedPoint, null), Is.True);
            dash.Tick(0.1f);
            Assert.That(dash.IsDashing, Is.False);

            InvokePrivate(indicator, "RefreshWithDelta", 0.2f);

            LineRenderer arrow = GetRenderer("arrowRenderer");
            Assert.That(arrow.enabled, Is.True);
            Assert.That(indicator.DisplayedEndpoint, Is.EqualTo(new Vector3(0.12f, 0f, 0f)));
            Assert.That(arrow.GetPosition(0), Is.EqualTo(new Vector3(0f, 0.05f, 0f)));
            Assert.That(arrow.GetPosition(1), Is.EqualTo(new Vector3(0.12f, 0.05f, 0f)));

            InvokePrivate(indicator, "RefreshWithDelta", 0.15f);
            Assert.That(arrow.enabled, Is.False);
        }

        [Test]
        public void RefreshWithoutPointerKeepsTargetingHiddenAndDisableHidesRenderers()
        {
            Assert.That(skills.BeginChargeTargeting(), Is.True);
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

        private int EnabledLoopRendererCount()
        {
            int count = 0;
            foreach (LineRenderer renderer in owner.GetComponentsInChildren<LineRenderer>())
            {
                if (renderer.enabled && renderer.loop) count++;
            }
            return count;
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

        private static void InvokePrivate(object instance, string methodName, float argument)
        {
            instance.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(instance, new object[] { argument });
        }
    }
}
