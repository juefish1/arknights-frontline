# Attack-Move Intent Semantics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `Q` followed by left click emit an explicit attack intent without ever converting a non-selectable click into movement.

**Architecture:** `UnitCommand` gains a targetless `AttackNearestInRange` intent and `PlayerCommandController` records every issued command in `CurrentCommand`. `AttackMoveState` owns only the one-click armed state. Stage 2 classifies the hit as selectable versus non-selectable and stops the motor for both attack intents; Stage 3 will consume the intent to apply legality, range, nearest-target selection, pursuit and damage.

**Tech Stack:** Unity 6000.3.15f1, Unity Input System 1.17.0, NUnit, Unity Test Framework, C# 9 with brace namespaces, macOS.

## Global Constraints

- Keep production code under `Assets/Game/Scripts` and tests under `Assets/Tests`; do not modify user-owned scene, package or ProjectSettings changes.
- Do not reference `CombatUnit`, target legality, attack range, pursuit, damage, or any Stage 3 type. The input layer may use the first physical raycast hit only to classify a click; it must not perform combat line-of-sight or range evaluation.
- `Q` plus a nearest `Targetable` left-click hit emits `UnitCommand.Attack(target)`; all other Q-plus-left clicks, including a wall or other nearer collider that occludes a target, emit `UnitCommand.AttackNearestInRange()`.
- `AttackNearestInRange()` and `Attack(target)` stop an existing `UnitMotor` movement. Neither command may set a movement destination in Stage 2.
- Existing direct right-click behavior stays unchanged: ground moves, target emits `Attack(target)`, right click while Q is armed only cancels.
- Run Unity tests in a disposable clean checkout if active Unity package imports cause unrelated compilation errors. Do not commit `Library/`, `Temp/`, `Logs/`, `TestResults/`, build logs or unrelated user files.

---

### Task 1: Represent and issue attack intents without movement

**Files:**
- Modify: `Assets/Game/Scripts/Commands/UnitCommand.cs`
- Modify: `Assets/Game/Scripts/Commands/AttackMoveState.cs`
- Modify: `Assets/Game/Scripts/Commands/PlayerCommandController.cs`
- Modify: `Assets/Tests/EditMode/UnitCommandTests.cs`
- Modify: `Assets/Tests/EditMode/AttackMoveStateTests.cs`
- Modify: `Assets/Tests/EditMode/PlayerCommandControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs`

**Interfaces:**
- Consumes: existing `UnitMotor.Stop()`, `GameInputActions`, `AttackMoveState.IsArmed`, and `UnitCommand.Attack(GameObject)`.
- Produces: `UnitCommandKind.AttackNearestInRange`, `UnitCommand.AttackNearestInRange()`, `AttackMoveState.Confirm()`, and nullable `PlayerCommandController.CurrentCommand`.

- [ ] **Step 1: Write failing EditMode tests for the new command and state API**

Replace the destination-carrying test with the following tests and remove every assertion that depends on `AttackMove(Vector3)` or `LastDestination`:

```csharp
[Test]
public void AttackNearestInRangeCommandHasNoTargetOrDestination()
{
    UnitCommand command = UnitCommand.AttackNearestInRange();

    Assert.That(command.Kind, Is.EqualTo(UnitCommandKind.AttackNearestInRange));
    Assert.That(command.TargetObject, Is.Null);
    Assert.That(command.Destination, Is.EqualTo(default(Vector3)));
}

[Test]
public void ConfirmDisarmsAttackMove()
{
    AttackMoveState state = new AttackMoveState();
    state.Arm();

    state.Confirm();

    Assert.That(state.IsArmed, Is.False);
}
```

Add a controller test that starts a move, then issues `AttackNearestInRange()`, and verifies: the motor is no longer moving, `CurrentTarget` is null, `CurrentCommand.HasValue` is true, and `CurrentCommand.Value.Kind` is `AttackNearestInRange`.

- [ ] **Step 2: Run the focused EditMode suite and verify RED**

Run:

```bash
UNITY_EDITOR_BIN="/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity"
"$UNITY_EDITOR_BIN" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ArknightsFrontline.Tests.EditMode.UnitCommandTests|ArknightsFrontline.Tests.EditMode.AttackMoveStateTests|ArknightsFrontline.Tests.EditMode.PlayerCommandControllerTests" -testResults "$PWD/TestResults/attack-move-intent-red.xml" -logFile "$PWD/TestResults/attack-move-intent-red.log"
```

Expected: compilation/test failures because `AttackNearestInRange`, parameterless `Confirm`, or `CurrentCommand` do not yet exist.

- [ ] **Step 3: Implement the minimum command and state changes**

Change `UnitCommandKind` to exactly `Move`, `Attack`, `AttackNearestInRange`, `Stop`; remove `AttackMove(Vector3)` and add:

```csharp
public static UnitCommand AttackNearestInRange()
{
    return new UnitCommand(UnitCommandKind.AttackNearestInRange, default, null);
}
```

Make `AttackMoveState.Confirm()` parameterless and have it only set `IsArmed` to false. Remove `LastDestination` because a click location is no longer part of an attack-move intent.

In `PlayerCommandController`, add:

```csharp
public UnitCommand? CurrentCommand { get; private set; }
```

At the beginning of `Issue(UnitCommand command)`, assign `CurrentCommand = command`. In the command switch, `Move` retains `SetDestination`; `Attack` retains `motor.Stop()` and sets `currentTarget`; `AttackNearestInRange` clears `currentTarget` and calls `motor.Stop()`; `Stop` keeps its existing clearing behavior. No attack intent calls `SetDestination`.

- [ ] **Step 4: Write a failing PlayMode test for Q plus non-selectable click**

In `PlayerCommandInputPlayModeTests`, make a configured player move first, arm Q, point the mouse at a `Ground` plane, press the left mouse button, yield one frame, then assert:

```csharp
Assert.That(controller.IsAttackMoveArmed, Is.False);
Assert.That(motor.IsMoving, Is.False);
Assert.That(controller.CurrentTarget, Is.Null);
Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.AttackNearestInRange));
```

The test must use the real Input System path and existing `CreateMainCamera` helper; it must not invoke private callbacks via reflection.

- [ ] **Step 5: Run that PlayMode test and verify RED**

Run the Unity PlayMode command with `-testFilter "ArknightsFrontline.Tests.PlayMode.PlayerCommandInputPlayModeTests"` and expect the new assertion to fail because the current code only handles `Ground` in `OnConfirm` by setting an `AttackMove` destination.

- [ ] **Step 6: Implement click classification and verify GREEN**

Replace the armed branch of `OnConfirm` with this behavior:

```csharp
if (!attackMoveState.IsArmed)
{
    return;
}

attackMoveState.Confirm();
if (TryRaycast(out RaycastHit hit)
    && hit.collider.gameObject.layer == LayerMask.NameToLayer("Targetable"))
{
    Issue(UnitCommand.Attack(hit.collider.gameObject));
    return;
}

Issue(UnitCommand.AttackNearestInRange());
```

Run the focused EditMode and `PlayerCommandInputPlayModeTests` suites again. Expected: all pass. Then run the full EditMode and PlayMode suites in the clean checkout. Expected: no compiler errors and all project tests pass.

- [ ] **Step 7: Commit only this task's source, tests, and metadata**

```bash
git add Assets/Game/Scripts/Commands/UnitCommand.cs Assets/Game/Scripts/Commands/AttackMoveState.cs Assets/Game/Scripts/Commands/PlayerCommandController.cs Assets/Tests/EditMode/UnitCommandTests.cs Assets/Tests/EditMode/AttackMoveStateTests.cs Assets/Tests/EditMode/PlayerCommandControllerTests.cs Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs Assets/Game/Scripts/Commands/UnitCommand.cs.meta Assets/Game/Scripts/Commands/AttackMoveState.cs.meta Assets/Game/Scripts/Commands/PlayerCommandController.cs.meta Assets/Tests/EditMode/UnitCommandTests.cs.meta Assets/Tests/EditMode/AttackMoveStateTests.cs.meta Assets/Tests/EditMode/PlayerCommandControllerTests.cs.meta Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs.meta
git commit -m "fix: record attack-move intents without ground movement"
```

### Task 2: Cover selectable and no-hit Q-click classification

**Files:**
- Modify: `Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs`

**Interfaces:**
- Consumes: `PlayerCommandController.CurrentCommand`, `CurrentTarget`, `UnitCommandKind.Attack`, `UnitCommandKind.AttackNearestInRange`, and real Input System mouse/keyboard devices.
- Produces: regression coverage for both previously untested Q-plus-left branches; no production API change.

- [ ] **Step 1: Add the two PlayMode regressions**

Add a test named `QPlusSelectableClickIssuesAttackIntentForThatTarget` that creates a player already moving, the existing main camera, and a cube at the center ray intersection on layer `Targetable`. It presses `Q`, presses left mouse, yields one frame, then asserts:

```csharp
Assert.That(controller.IsAttackMoveArmed, Is.False);
Assert.That(motor.IsMoving, Is.False);
Assert.That(controller.CurrentTarget, Is.EqualTo(target));
Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.Attack));
Assert.That(controller.CurrentCommand.Value.TargetObject, Is.EqualTo(target));
```

Add a test named `QPlusNoHitIssuesNearestInRangeIntentWithoutMovement` that creates a player already moving and the existing main camera but no `Ground` or `Targetable` collider. It presses `Q`, presses left mouse, yields one frame, then asserts the same disarmed/stopped/null-target conditions as the ground test and `CurrentCommand.Value.Kind == UnitCommandKind.AttackNearestInRange`.

Both tests must use `InputSystem.AddDevice`, `Press`, and the existing camera helper; each must destroy every created player, camera, and target object.

- [ ] **Step 2: Run the targeted PlayMode suite**

Run:

```bash
UNITY_EDITOR_BIN="/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity"
"$UNITY_EDITOR_BIN" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ArknightsFrontline.Tests.PlayMode.PlayerCommandInputPlayModeTests" -testResults "$PWD/TestResults/attack-move-classification.xml" -logFile "$PWD/TestResults/attack-move-classification.log"
```

Expected: all `PlayerCommandInputPlayModeTests` pass. If Unity cannot initialize the local licensing client before compilation, record the exact environment error and do not represent it as a test pass.

- [ ] **Step 3: Commit only the updated test and metadata**

```bash
git add Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs.meta
git commit -m "test: cover attack-move click classification"
```

### Task 3: Respect physical click occlusion and assert commandless cancellation

**Files:**
- Modify: `Assets/Game/Scripts/Commands/PlayerCommandController.cs`
- Modify: `Assets/Tests/EditMode/PlayerCommandControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs`

**Interfaces:**
- Consumes: `Physics.DefaultRaycastLayers`, existing `TryRaycast`, real Input System device helpers, and nullable `CurrentCommand`.
- Produces: first-hit click classification in which a wall blocks a target behind it, plus regressions for occlusion and no-command cancellation.

- [ ] **Step 1: Write the failing regression tests**

In `PlayerCommandInputPlayModeTests`, add `QPlusWallOccludingTargetIssuesNearestInRangeIntent`. Create the existing main camera and player already moving; create a wide default-layer cube wall centered on the camera's center ray before a `Targetable` cube, then press Q and left mouse at screen center. After one frame assert:

```csharp
Assert.That(controller.IsAttackMoveArmed, Is.False);
Assert.That(motor.IsMoving, Is.False);
Assert.That(controller.CurrentTarget, Is.Null);
Assert.That(controller.CurrentCommand.Value.Kind, Is.EqualTo(UnitCommandKind.AttackNearestInRange));
```

Destroy player, camera, wall, and target in the test. The wall must be created on the default layer; the target must be behind it on `Targetable`.

In `PlayerCommandControllerTests.ArmedRightClickIsCancelledByTheMoveClickPathBeforeRaycasting`, add `Assert.That(controller.CurrentCommand.HasValue, Is.False);` after cancellation, proving no `Move`, `Attack`, or nearest-target intent was issued.

- [ ] **Step 2: Run the targeted tests and verify RED**

Run the PlayerCommandInput PlayMode test class and the PlayerCommandController EditMode class with the installed Unity command. Expected before implementation: the wall test fails because the Ground/Targetable-only mask skips the wall and picks the target. If Unity cannot initialize its local licensing client before compilation, record the exact failure and do not call the test successful.

- [ ] **Step 3: Implement nearest-physical-hit classification**

In `TryRaycast`, replace the `LayerMask.GetMask("Ground", "Targetable")` value with `Physics.DefaultRaycastLayers`:

```csharp
Ray ray = mainCamera.ScreenPointToRay(input.PointerPosition.ReadValue<Vector2>());
return Physics.Raycast(ray, out hit, Mathf.Infinity, Physics.DefaultRaycastLayers);
```

Do not add target line-of-sight, combat range, legality, or pursuit logic. `HandleMoveClick` keeps moving only when the nearest hit is `Ground`; `OnConfirm` keeps issuing `Attack(target)` only when the nearest hit is `Targetable`.

- [ ] **Step 4: Verify GREEN and commit only task files**

Run focused tests, then the full EditMode and PlayMode suites in the clean checkout when the Unity licensing client permits startup. Commit only the source/test files and their existing metadata:

```bash
git add Assets/Game/Scripts/Commands/PlayerCommandController.cs Assets/Tests/EditMode/PlayerCommandControllerTests.cs Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs Assets/Game/Scripts/Commands/PlayerCommandController.cs.meta Assets/Tests/EditMode/PlayerCommandControllerTests.cs.meta Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs.meta
git commit -m "fix: respect attack-move click occlusion"
```

### Task 4: Make the occlusion regression synchronize physics deterministically

**Files:**
- Modify: `Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs`

**Interfaces:**
- Consumes: the wall and target transforms created by `QPlusWallOccludingTargetIssuesNearestInRangeIntent` and `Physics.SyncTransforms()`.
- Produces: a deterministic first-hit occlusion regression with no production behavior change.

- [ ] **Step 1: Synchronize the new colliders before dispatching input**

In `QPlusWallOccludingTargetIssuesNearestInRangeIntent`, insert this line immediately after setting the wall and target transforms/layers and before `Press(keyboard.qKey)`:

```csharp
Physics.SyncTransforms();
```

This is required because the test creates and moves colliders then performs an immediate raycast through an Input System callback in the same frame. Do not add a frame yield before the click because that would let the player motor advance and weaken the explicit stopped-motion assertion.

- [ ] **Step 2: Run the focused PlayMode class**

Run `ArknightsFrontline.Tests.PlayMode.PlayerCommandInputPlayModeTests` from a disposable clean checkout with the installed Unity editor. Expected when Unity licensing starts: all five input tests pass. If it fails before compilation with the local licensing `ResponseCode: 505 / Unsupported protocol version '1.18.1'`, record that limitation without claiming a test pass.

- [ ] **Step 3: Commit only the regression test**

```bash
git add Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs.meta
git commit -m "test: synchronize attack-move occlusion setup"
```

## Plan self-review

- Spec coverage: Tasks 1–4 cover Q plus selectable target as `Attack(target)`, Q plus every non-selectable click as nearest-in-range intent, deterministic physical click occlusion, mandatory motor stop/no destination, commandless cancellation, state consumption, and the explicit Stage 3 ownership boundary.
- Placeholder scan: no TODO/TBD or undefined follow-up behavior appears in an implementation step; Stage 3 is explicitly excluded rather than deferred inside this task.
- Type consistency: `AttackNearestInRange()`, `UnitCommandKind.AttackNearestInRange`, `AttackMoveState.Confirm()`, and nullable `CurrentCommand` use the same names in tests and production steps.
