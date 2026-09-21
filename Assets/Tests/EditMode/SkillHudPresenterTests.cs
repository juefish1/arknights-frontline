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
using UnityEngine.UI;

namespace ArknightsFrontline.Tests.EditMode
{
    public sealed class SkillHudPresenterTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private ExusiaiSkillController skills;
        private SkillHudPresenter hud;

        [SetUp]
        public void SetUp()
        {
            GameObject canvasObject = Track(new GameObject("Test Canvas", typeof(Canvas)));
            GameObject rootObject = Track(new GameObject("SkillHud", typeof(RectTransform)));
            rootObject.transform.SetParent(canvasObject.transform, false);
            hud = rootObject.AddComponent<SkillHudPresenter>();
            skills = CreateSkills();
            hud.Configure(skills);
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
        public void UpdateFormatsInitialAndReadyStatesUsingInvariantDecimals()
        {
            hud.Refresh();
            Assert.That(hud.WLabel, Is.EqualTo("W  0/3"));
            Assert.That(hud.ELabel, Is.EqualTo("E  READY"));
            Assert.That(hud.RLabel, Is.EqualTo("R  10.0"));

            CompleteBasicAttack();
            CompleteBasicAttack();
            CompleteBasicAttack();
            InvokePrivate(hud, "Update");

            Assert.That(hud.WLabel, Is.EqualTo("W  READY"));
        }

        [Test]
        public void RefreshPrioritizesTargetingDashWindowAndActiveOverloadText()
        {
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            hud.Refresh();
            Assert.That(hud.ELabel, Is.EqualTo("E  SELECT TARGET"));

            GameObject targetObject = Track(new GameObject("Target"));
            CombatUnit target = targetObject.AddComponent<CombatUnit>();
            target.Configure(TeamId.Red, Altitude.Ground, 100f, 1f, 0f, 1f, 1f, false, false);
            targetObject.transform.position = new Vector3(2f, 0f, 0f);
            Assert.That(skills.TryConfirmCharge(targetObject.transform.position, target), Is.True);
            hud.Refresh();
            Assert.That(hud.ELabel, Is.EqualTo("E  MOVE!"));

            skills.Tick(0.25f);
            hud.Refresh();
            Assert.That(hud.ELabel, Is.EqualTo("E  19.8"));

            skills.Tick(10f);
            Assert.That(skills.TryActivateOverload(), Is.True);
            hud.Refresh();
            Assert.That(hud.RLabel, Is.EqualTo("R  ACTIVE 10.0"));
        }

        [Test]
        public void ConfigureUsesItsExistingRectTransformForThreeNonInteractiveSlots()
        {
            hud.Configure(skills);
            hud.Refresh();

            Assert.That(hud.SlotCount, Is.EqualTo(3));
            Assert.That(hud.transform.parent.childCount, Is.EqualTo(1));
            Assert.That(hud.GetComponentsInChildren<Graphic>(true), Is.Not.Empty);
            foreach (Graphic graphic in hud.GetComponentsInChildren<Graphic>(true))
            {
                Assert.That(graphic.raycastTarget, Is.False);
            }
            Assert.That(hud.GetComponentsInChildren<Selectable>(true), Is.Empty);
            AssertSlotGeometry();
            int childCount = hud.transform.childCount;
            hud.Configure(skills);
            Assert.That(hud.transform.childCount, Is.EqualTo(childCount));

            hud.enabled = false;
            InvokePrivate(hud, "OnDisable");
            Assert.That(hud.IsVisible, Is.False);
            hud.enabled = true;
            hud.Refresh();
            Assert.That(hud.IsVisible, Is.True);
        }

        private void AssertSlotGeometry()
        {
            RectTransform first = hud.transform.GetChild(0).GetComponent<RectTransform>();
            RectTransform second = hud.transform.GetChild(1).GetComponent<RectTransform>();
            RectTransform third = hud.transform.GetChild(2).GetComponent<RectTransform>();
            Assert.That(first.sizeDelta, Is.EqualTo(new Vector2(120f, 64f)));
            Assert.That(second.sizeDelta, Is.EqualTo(new Vector2(120f, 64f)));
            Assert.That(third.sizeDelta, Is.EqualTo(new Vector2(120f, 64f)));
            Assert.That(second.anchoredPosition.x - first.anchoredPosition.x, Is.EqualTo(128f));
            Assert.That(third.anchoredPosition.x - second.anchoredPosition.x, Is.EqualTo(128f));
            Assert.That(hud.GetComponent<RectTransform>().anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)));
            Assert.That(hud.GetComponent<RectTransform>().anchorMax, Is.EqualTo(new Vector2(0.5f, 0f)));
        }

        private ExusiaiSkillController CreateSkills()
        {
            GameObject ownerObject = Track(new GameObject("Exusiai"));
            CombatUnit owner = ownerObject.AddComponent<CombatUnit>();
            owner.Configure(TeamId.Blue, Altitude.Ground, 100f, 50f, 0f, 6f, 0.5f, true, false);
            UnitMotor motor = ownerObject.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());
            PlayerCommandController commands = ownerObject.AddComponent<PlayerCommandController>();
            InvokePrivate(commands, "Awake");
            UnitStatModifiers modifiers = ownerObject.AddComponent<UnitStatModifiers>();
            AttackSequenceExecutor executor = ownerObject.AddComponent<AttackSequenceExecutor>();
            executor.Configure(owner);
            BasicAttackController attacks = ownerObject.AddComponent<BasicAttackController>();
            attacks.Configure(owner, executor);
            SkillDashController dash = ownerObject.AddComponent<SkillDashController>();
            dash.Configure(motor, ArenaLayout.CreateDefault(), 0);
            ExusiaiSkillController result = ownerObject.AddComponent<ExusiaiSkillController>();
            result.Configure(owner, commands, attacks, executor, modifiers, dash);
            return result;
        }

        private void CompleteBasicAttack()
        {
            GameObject targetObject = Track(new GameObject("Target"));
            CombatUnit target = targetObject.AddComponent<CombatUnit>();
            target.Configure(TeamId.Red, Altitude.Ground, 1000f, 1f, 0f, 1f, 1f, false, false);
            targetObject.transform.position = new Vector3(2f, 0f, 0f);
            AttackSequenceExecutor executor = skills.GetComponent<AttackSequenceExecutor>();
            Assert.That(executor.TryStart(new AttackSequencePlan(
                AttackSequenceKind.Basic, 1, 0.05f, 50f, 1f, 0f, 1f, 0f, true, false), target), Is.True);
            executor.Tick(1f);
        }

        private GameObject Track(GameObject gameObject)
        {
            gameObjects.Add(gameObject);
            return gameObject;
        }

        private static void InvokePrivate(object instance, string methodName)
        {
            instance.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(instance, null);
        }
    }
}
