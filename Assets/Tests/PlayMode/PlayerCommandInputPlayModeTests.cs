using System.Collections;
using System.Collections.Generic;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Common;
using ArknightsFrontline.Input;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class PlayerCommandInputPlayModeTests : InputTestFixture
    {
        private readonly HashSet<EntityId> baselineRootIds = new HashSet<EntityId>();
        private Scene cleanupScene;

        public override void Setup()
        {
            base.Setup();
            PlayerPrefs.DeleteKey("af.input.bindings.v1");
            cleanupScene = SceneManager.GetActiveScene();
            baselineRootIds.Clear();
            if (!cleanupScene.IsValid() || !cleanupScene.isLoaded)
            {
                return;
            }

            foreach (GameObject root in cleanupScene.GetRootGameObjects())
            {
                baselineRootIds.Add(root.GetEntityId());
            }
        }

        [UnityTest]
        public IEnumerator DisablingFeedbackClearsVisibleStateAndHidesRangeRingUntilReenabled()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            CommandFeedbackPresenter feedback = CreateFeedbackPresenter(out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            GameObject target = CreateTargetableCube(TeamId.Red);
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.aKey);
            yield return null;
            Assert.That(feedback.IsAttackRangeVisible, Is.True);
            Assert.That(feedback.IsHoveringLegalTarget, Is.True);
            Assert.That(GameObject.Find("AttackRangeRing").GetComponent<LineRenderer>().enabled, Is.True);

            feedback.enabled = false;
            yield return null;
            Assert.That(feedback.IsAttackRangeVisible, Is.False);
            Assert.That(feedback.IsHoveringLegalTarget, Is.False);
            Assert.That(GameObject.Find("AttackRangeRing").GetComponent<LineRenderer>().enabled, Is.False);

            Release(keyboard.aKey);
            feedback.enabled = true;
            yield return null;
            Assert.That(feedback.IsHoveringLegalTarget, Is.True);

            Press(keyboard.aKey);
            yield return null;
            Assert.That(feedback.IsAttackRangeVisible, Is.True);

            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator HeldAShowsTheConfiguredAttackRangeAndReleaseHidesIt()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            CommandFeedbackPresenter feedback = CreateFeedbackPresenter(out GameObject player);

            Press(keyboard.aKey);
            yield return null;
            Assert.That(feedback.IsAttackRangeVisible, Is.True);
            Assert.That(feedback.RangeRingRadius, Is.EqualTo(6f));

            Release(keyboard.aKey);
            yield return null;
            Assert.That(feedback.IsAttackRangeVisible, Is.False);
            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator ChargeTargetingSuppressesHeldAAttackRingAndRestoresItAfterCancellation()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            PlayerCommandController controller = CreateControllerAt(Vector3.zero, out _, out GameObject player);
            ExusiaiSkillController skills = ConfigureExusiaiSkillPipeline(player, controller);
            CommandFeedbackPresenter feedback = player.AddComponent<CommandFeedbackPresenter>();
            feedback.ConfigureSkillController(skills);

            Press(keyboard.aKey);
            yield return null;
            Assert.That(feedback.IsAttackRangeVisible, Is.True);

            Assert.That(skills.BeginChargeTargeting(), Is.True);
            yield return null;
            Assert.That(feedback.IsAttackRangeVisible, Is.False);

            Assert.That(skills.CancelChargeTargeting(), Is.True);
            yield return null;
            Assert.That(feedback.IsAttackRangeVisible, Is.True);

            Release(keyboard.aKey);
            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator HoveringHostileLegalTargetSetsLegalHoverFeedback()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            CommandFeedbackPresenter feedback = CreateFeedbackPresenter(out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            GameObject target = CreateTargetableCube(TeamId.Red);
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            yield return null;
            Assert.That(feedback.IsHoveringLegalTarget, Is.True);

            target.GetComponent<CombatUnit>().Configure(
                TeamId.Blue, Altitude.Ground, 10f, 0f, 0f, 0f, 0f, false, false);
            yield return null;
            Assert.That(feedback.IsHoveringLegalTarget, Is.False);

            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator HoveringTargetableWithoutCombatUnitDoesNotSetLegalHoverFeedback()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            CommandFeedbackPresenter feedback = CreateFeedbackPresenter(out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.layer = LayerMask.NameToLayer("Targetable");
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            yield return null;
            Assert.That(feedback.IsHoveringLegalTarget, Is.False);

            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator HoveringDeadTargetDoesNotSetLegalHoverFeedback()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            CommandFeedbackPresenter feedback = CreateFeedbackPresenter(out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            GameObject target = CreateTargetableCube(TeamId.Red);
            target.GetComponent<CombatUnit>().TakePhysicalDamage(100f);
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            yield return null;
            Assert.That(feedback.IsHoveringLegalTarget, Is.False);

            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator ReleasingAClearsAttackMoveHoldState()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            PlayerCommandController controller = CreateControllerAt(Vector3.zero, out _, out GameObject player);

            Press(keyboard.aKey);
            yield return null;
            Assert.That(controller.IsAttackMoveArmed, Is.True);
            Assert.That(controller.IsAttackMoveHeld, Is.True);

            Release(keyboard.aKey);
            yield return null;
            Assert.That(controller.IsAttackMoveArmed, Is.False);
            Assert.That(controller.IsAttackMoveHeld, Is.False);
            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator ReleasingAPreventsLaterLeftClickFromIssuingAttackIntent()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(Vector3.zero, out _, out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.layer = LayerMask.NameToLayer("Targetable");
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.aKey);
            yield return null;
            Release(keyboard.aKey);
            yield return null;
            Press(mouse.leftButton);
            yield return null;

            Assert.That(controller.CurrentCommand, Is.Null);
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator ConfirmingAttackWhileARemainsHeldPreservesHoldState()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            CommandFeedbackPresenter feedback = CreateFeedbackPresenterAt(new Vector3(-10f, 0f, 0f), out _, out GameObject player);
            PlayerCommandController controller = player.GetComponent<PlayerCommandController>();
            GameObject cameraObject = CreateMainCamera();
            GameObject target = CreateTargetableCube(TeamId.Red);
            controller.Issue(UnitCommand.Move(Vector3.zero));
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.aKey);
            yield return null;
            Press(mouse.leftButton);
            yield return null;

            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.Attack));
            Assert.That(controller.IsAttackMoveHeld, Is.True);
            Assert.That(feedback.IsAttackRangeVisible, Is.True);
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator ExusiaiSelectingChargeConsumesOutOfRangeTargetAndGroundConfirmsBeforeAttackMove()
        {
            PlayerPrefs.DeleteKey("af.input.bindings.v1");
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(
                new Vector3(-10f, 0f, 0f), out _, out GameObject player);
            ExusiaiSkillController skills = ConfigureExusiaiSkillPipeline(player, controller);
            GameObject cameraObject = CreateMainCamera();
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.layer = LayerMask.NameToLayer("Ground");
            GameObject target = CreateTargetableCube(TeamId.Red);
            controller.Issue(UnitCommand.Move(Vector3.zero));
            int originalRevision = controller.CommandRevision;
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.aKey);
            yield return null;
            Assert.That(controller.IsAttackMoveArmed, Is.True);

            Press(keyboard.eKey);
            yield return null;
            Release(keyboard.eKey);
            yield return null;
            Assert.That(skills.IsSelectingChargeTarget, Is.True);

            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            Assert.That(skills.IsSelectingChargeTarget, Is.True);
            Assert.That(skills.Snapshot.ChargeCooldown, Is.Zero);
            Assert.That(controller.CommandRevision, Is.EqualTo(originalRevision));
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.Move));

            Object.Destroy(target);
            yield return null;

            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            Release(keyboard.aKey);
            yield return null;
            Assert.That(skills.IsSelectingChargeTarget, Is.True);
            Assert.That(skills.Snapshot.ChargeCooldown, Is.Zero);
            Assert.That(controller.CommandRevision, Is.EqualTo(originalRevision));
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.Move));

            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(ground);
        }

        [UnityTest]
        public IEnumerator EscapeAfterConfirmationCancelsHeldAFeedback()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            CommandFeedbackPresenter feedback = CreateFeedbackPresenterAt(new Vector3(-10f, 0f, 0f), out _, out GameObject player);
            PlayerCommandController controller = player.GetComponent<PlayerCommandController>();
            GameObject cameraObject = CreateMainCamera();
            GameObject target = CreateTargetableCube(TeamId.Red);
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.aKey);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Assert.That(controller.IsAttackMoveArmed, Is.False);
            Assert.That(controller.IsAttackMoveHeld, Is.True);
            Assert.That(feedback.IsAttackRangeVisible, Is.True);

            Press(keyboard.escapeKey);
            yield return null;

            Assert.That(controller.IsAttackMoveHeld, Is.False);
            Assert.That(feedback.IsAttackRangeVisible, Is.False);
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator SharedRightClickAfterConfirmationCancelsHeldAFeedbackWithoutMoving()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            CommandFeedbackPresenter feedback = CreateFeedbackPresenterAt(new Vector3(-10f, 0f, 0f), out UnitMotor motor, out GameObject player);
            PlayerCommandController controller = player.GetComponent<PlayerCommandController>();
            GameObject cameraObject = CreateMainCamera();
            GameObject target = CreateTargetableCube(TeamId.Red);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.layer = LayerMask.NameToLayer("Ground");
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.aKey);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Assert.That(controller.IsAttackMoveArmed, Is.False);
            Assert.That(controller.IsAttackMoveHeld, Is.True);
            Assert.That(feedback.IsAttackRangeVisible, Is.True);

            Press(mouse.rightButton);
            yield return null;

            Assert.That(controller.IsAttackMoveHeld, Is.False);
            Assert.That(feedback.IsAttackRangeVisible, Is.False);
            Assert.That(motor.IsMoving, Is.False);
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.Attack));
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(target);
            Object.Destroy(ground);
        }

        [UnityTest]
        public IEnumerator DefaultSharedRightClickCancelsArmedCommandWithoutMoving()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out UnitMotor motor, out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.layer = LayerMask.NameToLayer("Ground");
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            controller.ArmAttackMove();
            Press(mouse.rightButton);
            yield return null;

            Assert.That(controller.IsAttackMoveArmed, Is.False);
            Assert.That(motor.IsMoving, Is.False);
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(ground);
        }

        [UnityTest]
        public IEnumerator APlusGroundClickIssuesNearestInRangeIntentWithoutMovement()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out UnitMotor motor, out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.layer = LayerMask.NameToLayer("Ground");
            controller.Issue(UnitCommand.Move(Vector3.zero));
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.aKey);
            Press(mouse.leftButton);
            yield return null;

            Assert.That(controller.IsAttackMoveArmed, Is.False);
            Assert.That(motor.IsMoving, Is.False);
            Assert.That(controller.CurrentTarget, Is.Null);
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.AttackNearestInRange));
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(ground);
        }

        [UnityTest]
        public IEnumerator APlusSelectableClickIssuesAttackIntentForThatTarget()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out UnitMotor motor, out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            ConfigurePlayerCombatUnit(player);
            GameObject target = CreateTargetableCube(TeamId.Red);
            controller.Issue(UnitCommand.Move(Vector3.zero));
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.aKey);
            Press(mouse.leftButton);
            yield return null;

            Assert.That(controller.IsAttackMoveArmed, Is.False);
            Assert.That(motor.IsMoving, Is.False);
            Assert.That(controller.CurrentTarget, Is.EqualTo(target));
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.Attack));
            Assert.That(controller.CurrentCommand.Value.TargetObject, Is.EqualTo(target));
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator APlusFriendlyTargetableClickIssuesNearestInRangeIntent()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out UnitMotor motor, out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            ConfigurePlayerCombatUnit(player);
            GameObject target = CreateTargetableCube(TeamId.Blue);
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.aKey);
            Press(mouse.leftButton);
            yield return null;

            Assert.That(motor.IsMoving, Is.False);
            Assert.That(controller.CurrentTarget, Is.Null);
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.AttackNearestInRange));
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator APlusDeadTargetableClickIssuesNearestInRangeIntent()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out UnitMotor motor, out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            ConfigurePlayerCombatUnit(player);
            GameObject target = CreateTargetableCube(TeamId.Red);
            target.GetComponent<CombatUnit>().TakePhysicalDamage(100f);
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.aKey);
            Press(mouse.leftButton);
            yield return null;

            Assert.That(motor.IsMoving, Is.False);
            Assert.That(controller.CurrentTarget, Is.Null);
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.AttackNearestInRange));
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator APlusTargetableTowerWithoutCombatUnitIssuesNearestInRangeIntent()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out UnitMotor motor, out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            ConfigurePlayerCombatUnit(player);
            GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "TargetableTower";
            tower.layer = LayerMask.NameToLayer("Targetable");
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.aKey);
            Press(mouse.leftButton);
            yield return null;

            Assert.That(motor.IsMoving, Is.False);
            Assert.That(controller.CurrentTarget, Is.Null);
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.AttackNearestInRange));
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(tower);
        }

        [UnityTest]
        public IEnumerator APlusWallOccludingTargetIssuesNearestInRangeIntent()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out UnitMotor motor, out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = LayerMask.NameToLayer("Default");
            wall.transform.SetPositionAndRotation(new Vector3(0f, 5f, -5f), Quaternion.identity);
            wall.transform.localScale = new Vector3(10f, 10f, 1f);
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.position = Vector3.zero;
            target.layer = LayerMask.NameToLayer("Targetable");
            controller.Issue(UnitCommand.Move(Vector3.zero));
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Physics.SyncTransforms();
            Press(keyboard.aKey);
            Press(mouse.leftButton);
            yield return null;

            Assert.That(controller.IsAttackMoveArmed, Is.False);
            Assert.That(motor.IsMoving, Is.False);
            Assert.That(controller.CurrentTarget, Is.Null);
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.AttackNearestInRange));
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(wall);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator APlusNoHitIssuesNearestInRangeIntentWithoutMovement()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out UnitMotor motor, out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            controller.Issue(UnitCommand.Move(Vector3.zero));
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.aKey);
            Press(mouse.leftButton);
            yield return null;

            Assert.That(controller.IsAttackMoveArmed, Is.False);
            Assert.That(motor.IsMoving, Is.False);
            Assert.That(controller.CurrentTarget, Is.Null);
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.AttackNearestInRange));
            Object.Destroy(player);
            Object.Destroy(cameraObject);
        }

        [UnityTest]
        public IEnumerator SavedCancelRebindingIsLoadedAndCancelsTheControllerOwnedActionAsset()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            using (var savedActions = new GameInputActions())
            {
                savedActions.Cancel.ApplyBindingOverride(0, "<Keyboard>/c");
                InputBindingStore.Save(savedActions.Asset);
            }

            PlayerCommandController controller = CreateControllerAt(Vector3.zero, out _, out GameObject player);
            InputAction cancel = controller.InputActions.FindAction("Cancel");
            Assert.That(cancel.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/c"));

            controller.ArmAttackMove();
            Press(keyboard.cKey);
            yield return null;

            Assert.That(controller.IsAttackMoveArmed, Is.False);
            PlayerPrefs.DeleteKey("af.input.bindings.v1");
            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator SkillActionsUseOnlyTheCurrentHandlerAcrossDisableAndClear()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            PlayerCommandController controller = CreateControllerAt(Vector3.zero, out _, out GameObject player);
            RecordingSkillInputHandler first = new RecordingSkillInputHandler();
            RecordingSkillInputHandler second = new RecordingSkillInputHandler();
            controller.SetSkillInputHandler(first);

            Press(keyboard.eKey);
            yield return null;
            Release(keyboard.eKey);
            yield return null;
            Assert.That(first.Skill2Count, Is.EqualTo(1));

            controller.SetSkillInputHandler(second);
            Press(keyboard.rKey);
            yield return null;
            Release(keyboard.rKey);
            yield return null;
            Assert.That(first.Skill3Count, Is.Zero);
            Assert.That(second.Skill3Count, Is.EqualTo(1));

            controller.enabled = false;
            yield return null;
            controller.enabled = true;
            yield return null;
            Press(keyboard.eKey);
            yield return null;
            Release(keyboard.eKey);
            yield return null;
            Assert.That(second.Skill2Count, Is.EqualTo(1));

            controller.SetSkillInputHandler(null);
            Press(keyboard.rKey);
            yield return null;
            Release(keyboard.rKey);
            yield return null;
            Assert.That(second.Skill3Count, Is.EqualTo(1));
            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator HandledConfirmReceivesSinglePointerHitWithoutIssuingAttackMove()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out _, out GameObject player);
            ConfigurePlayerCombatUnit(player);
            RecordingSkillInputHandler handler = new RecordingSkillInputHandler { AcceptConfirm = true };
            controller.SetSkillInputHandler(handler);
            GameObject cameraObject = CreateMainCamera();
            GameObject target = CreateTargetableCube(TeamId.Red);
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.aKey);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            Release(keyboard.aKey);
            yield return null;

            Assert.That(handler.ConfirmCount, Is.EqualTo(1));
            Assert.That(handler.LastConfirmHit, Is.EqualTo(target));
            Assert.That(controller.CurrentCommand, Is.Null);
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator HandledCancelConsumesSharedRightClickBeforeItCanBecomeMove()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out UnitMotor motor, out GameObject player);
            RecordingSkillInputHandler handler = new RecordingSkillInputHandler { AcceptCancel = true };
            controller.SetSkillInputHandler(handler);
            GameObject cameraObject = CreateMainCamera();
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.layer = LayerMask.NameToLayer("Ground");
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(mouse.rightButton);
            yield return null;
            Release(mouse.rightButton);
            yield return null;

            Assert.That(handler.CancelCount, Is.EqualTo(1));
            Assert.That(handler.MoveClickCount, Is.Zero);
            Assert.That(motor.IsMoving, Is.False);
            Assert.That(controller.CurrentCommand, Is.Null);
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(ground);
        }

        [UnityTest]
        public IEnumerator SkillMoveWindowConsumesClickThenOrdinaryMoveResumesAfterHandlerDeclines()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out UnitMotor motor, out GameObject player);
            RecordingSkillInputHandler handler = new RecordingSkillInputHandler { AcceptMoveClick = true };
            controller.SetSkillInputHandler(handler);
            GameObject cameraObject = CreateMainCamera();
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.layer = LayerMask.NameToLayer("Ground");
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(mouse.rightButton);
            yield return null;
            Release(mouse.rightButton);
            yield return null;
            Assert.That(handler.MoveClickCount, Is.EqualTo(1));
            Assert.That(motor.IsMoving, Is.False);

            handler.AcceptMoveClick = false;
            Press(mouse.rightButton);
            yield return null;
            Release(mouse.rightButton);
            yield return null;
            Assert.That(handler.MoveClickCount, Is.EqualTo(2));
            Assert.That(motor.IsMoving, Is.True);
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.Move));
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(ground);
        }

        [UnityTest]
        public IEnumerator BlockingSkillStatePreventsAttackMoveAndNormalMoveButStopStillIssues()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out UnitMotor motor, out GameObject player);
            RecordingSkillInputHandler handler = new RecordingSkillInputHandler
            {
                BlockAttackMove = true,
                BlockNormalCommands = true
            };
            controller.SetSkillInputHandler(handler);
            GameObject cameraObject = CreateMainCamera();
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.layer = LayerMask.NameToLayer("Ground");
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            controller.Issue(UnitCommand.Move(Vector3.zero));

            Press(keyboard.aKey);
            yield return null;
            Release(keyboard.aKey);
            yield return null;
            Assert.That(controller.IsAttackMoveArmed, Is.False);

            handler.BlockAttackMove = false;
            handler.BlockNormalCommands = false;
            Keyboard preArmedKeyboard = InputSystem.AddDevice<Keyboard>();
            Press(preArmedKeyboard.aKey);
            yield return null;
            Assert.That(controller.IsAttackMoveArmed, Is.True);

            handler.BlockNormalCommands = true;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            Release(preArmedKeyboard.aKey);
            yield return null;
            Assert.That(handler.ConfirmCount, Is.EqualTo(1));
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.Move));

            Press(mouse.rightButton);
            yield return null;
            Release(mouse.rightButton);
            yield return null;
            Assert.That(handler.MoveClickCount, Is.EqualTo(1));
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.Move));

            Press(keyboard.sKey);
            yield return null;
            Release(keyboard.sKey);
            yield return null;
            Assert.That(handler.StopCount, Is.EqualTo(1));
            Assert.That(motor.IsMoving, Is.False);
            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.Stop));
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(ground);
        }

        public override void TearDown()
        {
        }

        [UnityTearDown]
        public IEnumerator TearDownAfterRuntimeObjectsAreDestroyed()
        {
            try
            {
                foreach (InputDevice device in InputSystem.devices)
                {
                    InputSystem.ResetDevice(device, true);
                }

                if (cleanupScene.IsValid() && cleanupScene.isLoaded)
                {
                    foreach (GameObject root in cleanupScene.GetRootGameObjects())
                    {
                        if (!baselineRootIds.Contains(root.GetEntityId()))
                        {
                            Object.Destroy(root);
                        }
                    }
                }

                yield return null;
            }
            finally
            {
                PlayerPrefs.DeleteKey("af.input.bindings.v1");
                base.TearDown();
            }
        }

        private static PlayerCommandController CreateControllerAt(Vector3 position, out UnitMotor motor, out GameObject player)
        {
            player = new GameObject("Player");
            player.transform.position = position;
            motor = player.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());
            return player.AddComponent<PlayerCommandController>();
        }

        private static CommandFeedbackPresenter CreateFeedbackPresenter(out GameObject player)
        {
            return CreateFeedbackPresenterAt(Vector3.zero, out _, out player);
        }

        private static CommandFeedbackPresenter CreateFeedbackPresenterAt(
            Vector3 position,
            out UnitMotor motor,
            out GameObject player)
        {
            CreateControllerAt(position, out motor, out player);
            ConfigurePlayerCombatUnit(player);
            return player.AddComponent<CommandFeedbackPresenter>();
        }

        private static void ConfigurePlayerCombatUnit(GameObject player)
        {
            CombatUnit combatUnit = player.AddComponent<CombatUnit>();
            combatUnit.Configure(TeamId.Blue, Altitude.Ground, 100f, 12f, 2f, 6f, 0.5f, true, false);
        }

        private static ExusiaiSkillController ConfigureExusiaiSkillPipeline(
            GameObject player,
            PlayerCommandController commands)
        {
            UnitMotor motor = player.GetComponent<UnitMotor>();
            CombatUnit owner = player.AddComponent<CombatUnit>();
            owner.Configure(TeamId.Blue, Altitude.Ground, 100f, 12f, 2f, 6f, 0.5f, true, false);
            UnitStatModifiers modifiers = player.AddComponent<UnitStatModifiers>();
            AttackSequenceExecutor executor = player.AddComponent<AttackSequenceExecutor>();
            executor.Configure(owner);
            BasicAttackController attacks = player.AddComponent<BasicAttackController>();
            attacks.Configure(owner, executor);
            SkillDashController dash = player.AddComponent<SkillDashController>();
            dash.Configure(motor, ArenaLayout.CreateDefault(), 0);
            ExusiaiSkillController skills = player.AddComponent<ExusiaiSkillController>();
            skills.Configure(owner, commands, attacks, executor, modifiers, dash);
            commands.SetSkillInputHandler(skills);
            return skills;
        }

        private static GameObject CreateTargetableCube(TeamId team)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.layer = LayerMask.NameToLayer("Targetable");
            CombatUnit combatUnit = target.AddComponent<CombatUnit>();
            combatUnit.Configure(team, Altitude.Ground, 10f, 0f, 0f, 0f, 0f, false, false);
            return target;
        }

        private static GameObject CreateMainCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 10f, -10f),
                Quaternion.Euler(45f, 0f, 0f));
            cameraObject.AddComponent<UnityEngine.Camera>();
            return cameraObject;
        }

        private sealed class RecordingSkillInputHandler : IPlayerSkillInputHandler
        {
            public bool BlockAttackMove { get; set; }

            public bool BlockNormalCommands { get; set; }

            public bool AcceptConfirm { get; set; }

            public bool AcceptMoveClick { get; set; }

            public bool AcceptCancel { get; set; }

            public int Skill2Count { get; private set; }

            public int Skill3Count { get; private set; }

            public int ConfirmCount { get; private set; }

            public int MoveClickCount { get; private set; }

            public int CancelCount { get; private set; }

            public int StopCount { get; private set; }

            public GameObject LastConfirmHit { get; private set; }

            public bool BlocksAttackMove => BlockAttackMove;

            public bool BlocksNormalCommands => BlockNormalCommands;

            public void HandleSkill2()
            {
                Skill2Count++;
            }

            public void HandleSkill3()
            {
                Skill3Count++;
            }

            public bool TryHandleConfirm(Vector3 worldPoint, GameObject hitObject)
            {
                ConfirmCount++;
                LastConfirmHit = hitObject;
                return AcceptConfirm;
            }

            public bool TryHandleMoveClick(Vector3 worldPoint)
            {
                MoveClickCount++;
                return AcceptMoveClick;
            }

            public bool TryHandleCancel()
            {
                CancelCount++;
                return AcceptCancel;
            }

            public void HandleStop()
            {
                StopCount++;
            }
        }
    }
}
