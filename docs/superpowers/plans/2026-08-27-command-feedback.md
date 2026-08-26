# Command Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让合法攻击目标具有悬停准星反馈，并让 Q 仅在按住期间显示真实攻击范围和接受攻击确认。

**Architecture:** `PlayerCommandController` 管理 Q 按住状态与命令语义，公开当前指针射线命中结果。新增独立的 `CommandFeedbackPresenter` 复用控制器射线和 `TargetRules`，负责系统光标和运行时 `LineRenderer` 范围环；它不生成命令也不决定攻击规则。

**Tech Stack:** Unity 6000.3.15f1、C# 9、Unity Input System 1.17.0、Unity Test Framework、NUnit。

## Global Constraints

- 以 `docs/superpowers/specs/2026-08-27-command-feedback-design.md` 为权威规格。
- 保持既有右键和 Q+左键命令语义；Q+左键地面、墙体或空白处绝不创建到鼠标位置的移动。
- `CommandFeedbackPresenter` 使用 `TargetRules.IsLegal` 和 `CombatUnit.AttackRange`，不得复制或放宽目标合法性规则。
- 不修改或提交用户持有的 `Assets/Game/Scenes/PrototypeArena.unity`、Packages、ProjectSettings 或日志；场景只由 `PrototypeSceneBuilder` 在用户触发菜单后重建。
- 所有新 C# 使用 C# 9 花括号命名空间；运行时代码在 `Assets/Game/Scripts`，测试在 `Assets/Tests`。
- Unity 批处理测试仅在临时干净 checkout 中运行；若 LicenseClient 505 在编译前阻断，记录完整错误，绝不称测试通过。

---

### Task 1: Q hold-state command semantics

**Files:**
- Modify: `Assets/Game/Scripts/Commands/PlayerCommandController.cs`
- Modify: `Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs`

**Interfaces:**
- Consumes: `GameInputActions.AttackMove`, `AttackMoveState`, `InputAction.canceled`.
- Produces: `PlayerCommandController.IsAttackMoveHeld` and `TryGetPointerHit(out RaycastHit hit)`.

- [ ] **Step 1: Write failing input regression tests**

Add an `InputTestFixture` test with a real keyboard, player, motor, and controller:

```csharp
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
```

Add a second test that presses and releases Q, then clicks the left mouse button on a targetable cube and asserts `CurrentCommand` remains unset: releasing Q must prevent a later left click from issuing attack intent.

- [ ] **Step 2: Run focused PlayMode test and observe RED**

Run `PlayerCommandInputPlayModeTests` in a temporary clean checkout. Expected: compilation failure because `IsAttackMoveHeld` and the canceled callback do not exist, or a recorded LicenseClient 505 block before compilation.

- [ ] **Step 3: Implement the hold-state API**

In `PlayerCommandController`:

```csharp
private bool isAttackMoveHeld;

public bool IsAttackMoveHeld => isAttackMoveHeld;

private void OnAttackMove(...)
{
    isAttackMoveHeld = true;
    ArmAttackMove();
}

private void OnAttackMoveCanceled(...)
{
    isAttackMoveHeld = false;
    attackMoveState.Cancel();
}
```

Subscribe and unsubscribe `input.AttackMove.canceled` in `Awake` and `OnDestroy`. In `OnConfirm`, return unless both `IsAttackMoveArmed` and `IsAttackMoveHeld` are true. In every existing cancellation path—right click while armed, Esc, and `OnDisable`—set `isAttackMoveHeld = false` and cancel the state. Expose:

```csharp
public bool TryGetPointerHit(out RaycastHit hit)
{
    // Move the current private TryRaycast implementation here unchanged.
}
```

Update internal calls to use this method.

- [ ] **Step 4: Run focused verification**

Run the focused PlayMode fixture in the clean checkout and inspect `git diff --check -- Assets/Game/Scripts/Commands/PlayerCommandController.cs Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs`. Expected: passing tests, or LicenseClient 505 before compilation; whitespace check exits successfully.

- [ ] **Step 5: Commit Task 1**

```bash
git add Assets/Game/Scripts/Commands/PlayerCommandController.cs Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs
git commit -m "feat: require held q for attack command"
```

### Task 2: Hover cursor and attack-range feedback

**Files:**
- Create: `Assets/Game/Scripts/Commands/CommandFeedbackPresenter.cs`
- Create: `Assets/Game/Scripts/Commands/CommandFeedbackPresenter.cs.meta`
- Modify: `Assets/Game/Editor/PrototypeSceneBuilder.cs`
- Modify: `Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs`
- Modify: `Assets/Tests/PlayMode/PlayerMovementPlayModeTests.cs`

**Interfaces:**
- Consumes: `CombatUnit`, `PlayerCommandController.IsAttackMoveHeld`, `PlayerCommandController.TryGetPointerHit(out RaycastHit)`, and `TargetRules.IsLegal(CombatUnit, CombatUnit)`.
- Produces: `CommandFeedbackPresenter.IsAttackRangeVisible`, `IsHoveringLegalTarget`, and `RangeRingRadius`.

- [ ] **Step 1: Write failing feedback tests**

In `PlayerCommandInputPlayModeTests`, construct a player with `CombatUnit` configured as Blue, ground, range `6f`, ground attack enabled; add its `PlayerCommandController` and `CommandFeedbackPresenter`. Create a main camera and a red `Targetable` cube with a valid red ground `CombatUnit`.

Add:

```csharp
[UnityTest]
public IEnumerator HeldQShowsTheConfiguredAttackRangeAndReleaseHidesIt()
{
    Press(keyboard.qKey);
    yield return null;
    Assert.That(feedback.IsAttackRangeVisible, Is.True);
    Assert.That(feedback.RangeRingRadius, Is.EqualTo(6f));

    Release(keyboard.qKey);
    yield return null;
    Assert.That(feedback.IsAttackRangeVisible, Is.False);
}
```

Position the mouse over the target, yield one frame, and assert `IsHoveringLegalTarget == true`. Change its team to Blue using `Configure`, yield one frame, and assert false. Add separate false checks for a `Targetable` cube without `CombatUnit` and a target killed via `TakePhysicalDamage(100f)`.

In `PlayerMovementPlayModeTests`, assert the loaded `Player_Exusiai` has a `CommandFeedbackPresenter`.

- [ ] **Step 2: Run focused tests and observe RED**

Run `PlayerCommandInputPlayModeTests|PlayerMovementPlayModeTests` in a temporary clean checkout. Expected: compilation failure because the presenter does not exist, or LicenseClient 505 before compilation.

- [ ] **Step 3: Implement the presenter**

Create `CommandFeedbackPresenter` on the player:

```csharp
public bool IsAttackRangeVisible { get; private set; }
public bool IsHoveringLegalTarget { get; private set; }
public float RangeRingRadius => combatUnit == null ? 0f : combatUnit.AttackRange;
```

In `Awake`, obtain same-object `CombatUnit` and `PlayerCommandController`, then create a childless `GameObject("AttackRangeRing")` with a `LineRenderer`. The ring uses 65 points, is closed, tracks the owner XZ position at `y = 0.05f`, has width `0.06f`, and draws a yellow circle from `RangeRingRadius`. It is enabled only while `commandController.IsAttackMoveHeld`.

Generate one 24×24 transparent `Texture2D` with a two-pixel yellow circle and cache it in the component. Each update, call `commandController.TryGetPointerHit`; if the first hit is a `Targetable` object with a `CombatUnit` and `TargetRules.IsLegal(combatUnit, target)`, set `IsHoveringLegalTarget = true` and call `Cursor.SetCursor(circleTexture, new Vector2(12f, 12f), CursorMode.Auto)`. Otherwise set false and call `Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto)` only when the visual state changes. In `OnDestroy`, restore the default cursor and `Destroy` the component-owned cursor texture and ring object.

- [ ] **Step 4: Attach feedback in the builder**

In `PrototypeSceneBuilder.CreatePlayer`, add the presenter after `CombatUnit` and `PlayerCommandController` exist:

```csharp
player.AddComponent<CommandFeedbackPresenter>();
```

Do not modify the scene YAML directly; the user rebuilds it with `Arknights Frontline → Build Prototype Arena`.

- [ ] **Step 5: Run focused verification and manual gate**

Run the focused PlayMode tests in the clean checkout, then run `git diff --check` scoped to Task 2 files. If tests execute, expect no failures. If LicenseClient 505 prevents compilation, retain the exact message.

Manual gate after the scene rebuild:

1. Enter Play Mode and hover `TrainingDummy_Red`: cursor becomes a yellow circle.
2. Hover either tower or ground: cursor returns to default.
3. Hold Q: yellow range ring appears at the player’s feet; release Q: it disappears.
4. Hold Q and left-click the dummy: player pursues then attacks.
5. Hold Q and click ground: player does not move to click location.

- [ ] **Step 6: Commit Task 2**

```bash
git add Assets/Game/Scripts/Commands/CommandFeedbackPresenter.cs Assets/Game/Scripts/Commands/CommandFeedbackPresenter.cs.meta Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs Assets/Tests/PlayMode/PlayerMovementPlayModeTests.cs
git commit -m "feat: add command targeting feedback"
```

## Plan self-review

- Spec coverage: Task 1 makes Q a true hold-state and exposes a shared pointer hit; Task 2 renders legal-target and exact-range feedback, attaches it to the generated player, and covers the stated manual behavior.
- Scope: no target rules, damage, movement destinations, HUD, skills, towers, or scene YAML changes are introduced.
- Type consistency: Task 2 consumes `IsAttackMoveHeld` and `TryGetPointerHit` supplied by Task 1; all listed presenter properties are explicitly produced by Task 2.
