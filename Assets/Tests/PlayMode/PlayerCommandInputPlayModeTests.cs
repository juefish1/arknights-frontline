using System.Collections;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Common;
using ArknightsFrontline.Input;
using ArknightsFrontline.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class PlayerCommandInputPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator DisablingFeedbackClearsVisibleStateAndHidesRangeRingUntilReenabled()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            CommandFeedbackPresenter feedback = CreateFeedbackPresenter(out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            GameObject target = CreateTargetableCube(TeamId.Red);
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.qKey);
            yield return null;
            Assert.That(feedback.IsAttackRangeVisible, Is.True);
            Assert.That(feedback.IsHoveringLegalTarget, Is.True);
            Assert.That(GameObject.Find("AttackRangeRing").GetComponent<LineRenderer>().enabled, Is.True);

            feedback.enabled = false;
            yield return null;
            Assert.That(feedback.IsAttackRangeVisible, Is.False);
            Assert.That(feedback.IsHoveringLegalTarget, Is.False);
            Assert.That(GameObject.Find("AttackRangeRing").GetComponent<LineRenderer>().enabled, Is.False);

            Release(keyboard.qKey);
            feedback.enabled = true;
            yield return null;
            Assert.That(feedback.IsHoveringLegalTarget, Is.True);

            Press(keyboard.qKey);
            yield return null;
            Assert.That(feedback.IsAttackRangeVisible, Is.True);

            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator HeldQShowsTheConfiguredAttackRangeAndReleaseHidesIt()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            CommandFeedbackPresenter feedback = CreateFeedbackPresenter(out GameObject player);

            Press(keyboard.qKey);
            yield return null;
            Assert.That(feedback.IsAttackRangeVisible, Is.True);
            Assert.That(feedback.RangeRingRadius, Is.EqualTo(6f));

            Release(keyboard.qKey);
            yield return null;
            Assert.That(feedback.IsAttackRangeVisible, Is.False);
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
        public IEnumerator ReleasingQClearsAttackMoveHoldState()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            PlayerCommandController controller = CreateControllerAt(Vector3.zero, out _, out GameObject player);

            Press(keyboard.qKey);
            yield return null;
            Assert.That(controller.IsAttackMoveArmed, Is.True);
            Assert.That(controller.IsAttackMoveHeld, Is.True);

            Release(keyboard.qKey);
            yield return null;
            Assert.That(controller.IsAttackMoveArmed, Is.False);
            Assert.That(controller.IsAttackMoveHeld, Is.False);
            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator ReleasingQPreventsLaterLeftClickFromIssuingAttackIntent()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(Vector3.zero, out _, out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.layer = LayerMask.NameToLayer("Targetable");
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.qKey);
            yield return null;
            Release(keyboard.qKey);
            yield return null;
            Press(mouse.leftButton);
            yield return null;

            Assert.That(controller.CurrentCommand, Is.Null);
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator ConfirmingAttackWhileQRemainsHeldPreservesHoldState()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out _, out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.layer = LayerMask.NameToLayer("Targetable");
            controller.Issue(UnitCommand.Move(Vector3.zero));
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.qKey);
            yield return null;
            Press(mouse.leftButton);
            yield return null;

            Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.Attack));
            Assert.That(controller.IsAttackMoveHeld, Is.True);
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(target);
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
        public IEnumerator QPlusGroundClickIssuesNearestInRangeIntentWithoutMovement()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out UnitMotor motor, out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.layer = LayerMask.NameToLayer("Ground");
            controller.Issue(UnitCommand.Move(Vector3.zero));
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.qKey);
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
        public IEnumerator QPlusSelectableClickIssuesAttackIntentForThatTarget()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out UnitMotor motor, out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.layer = LayerMask.NameToLayer("Targetable");
            controller.Issue(UnitCommand.Move(Vector3.zero));
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.qKey);
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
        public IEnumerator QPlusWallOccludingTargetIssuesNearestInRangeIntent()
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
            Press(keyboard.qKey);
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
        public IEnumerator QPlusNoHitIssuesNearestInRangeIntentWithoutMovement()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            PlayerCommandController controller = CreateControllerAt(new Vector3(-10f, 0f, 0f), out UnitMotor motor, out GameObject player);
            GameObject cameraObject = CreateMainCamera();
            controller.Issue(UnitCommand.Move(Vector3.zero));
            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Press(keyboard.qKey);
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

        public override void TearDown()
        {
            PlayerPrefs.DeleteKey("af.input.bindings.v1");
            base.TearDown();
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
            CreateControllerAt(Vector3.zero, out _, out player);
            CombatUnit combatUnit = player.AddComponent<CombatUnit>();
            combatUnit.Configure(TeamId.Blue, Altitude.Ground, 100f, 12f, 2f, 6f, 0.5f, true, false);
            return player.AddComponent<CommandFeedbackPresenter>();
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
    }
}
