# 阶段 2：玩家操作 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让蓝方玩家胶囊支持可重绑定的点击移动、攻击意图、停止与镜头居中，为后续战斗阶段提供独立命令入口。

**Architecture:** Unity Input System 只在输入层创建动作；`UnitCommand` 是不依赖战斗代码的不可变值对象。`PlayerCommandController` 将射线命中和动作转为命令，`UnitMotor` 只负责受场地约束的 XZ 移动；`Q` 后左键只记录指定目标或当前范围最近目标的攻击意图，绝不将不可选中点击转换为移动；攻击移动状态可独立测试，当前不实现伤害。

**Tech Stack:** Unity 6000.3.15f1、Unity Input System 1.17.0、URP、NUnit、Unity Test Framework、macOS。所有新增 C# 文件使用花括号命名空间，保持项目 C# 9 编译器兼容。

## Global Constraints

- 只实现阶段 2 的玩家操作：不引入战斗、目标伤害、兵线、AI、技能或 HUD。
- 输入默认值为：右键移动/直接攻击、`Q` 攻击移动、`S` 停止、`W/E/R` 技能槽、`B` 撤退、`Space` 镜头居中；所有动作必须可重绑定。
- 指针位置为 `<Pointer>/position`；确认是 `<Mouse>/leftButton`；取消是 `<Keyboard>/escape` 和 `<Mouse>/rightButton`。
- `PlayerCommandController` 对鼠标使用默认物理射线层（忽略 `Ignore Raycast`），只把最近命中的 `Targetable` 视为可选中目标；墙体和其他最近碰撞体都必须阻挡后方目标的选择。右键在攻击移动瞄准时只取消、不移动。
- `Q` 后左键命中 `Targetable` 时记录 `Attack(target)` 意图；命中 `Ground`、墙体、其他不可选中对象或没有射线命中时记录 `AttackNearestInRange()` 意图。阶段 2 不判断合法性或范围，也不追击或造成伤害。
- `AttackNearestInRange()` 必须停止已有移动，且没有目标时绝不向左键位置移动；阶段 3 才会解析该意图。
- 运行时代码在 `Assets/Game/Scripts`，测试在 `Assets/Tests`。不得提交 `Library/`、`Temp/`、`Logs/`、`Builds/`、`TestResults/`、`.DS_Store`、构建日志或用户未跟踪的 ProjectSettings/Packages 修改。
- Unity 命令使用 `/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity`；如果包或证书请求无法完成，使用现有代理环境而不是修改项目包版本。

---

## Test commands

```bash
UNITY_EDITOR_BIN="/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity"
mkdir -p TestResults
"$UNITY_EDITOR_BIN" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$PWD/TestResults/editmode.xml" -logFile "$PWD/TestResults/editmode.log"
"$UNITY_EDITOR_BIN" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testResults "$PWD/TestResults/playmode.xml" -logFile "$PWD/TestResults/playmode.log"
```

### Task 1：可重绑定 Input System 动作与命令值对象

**Files:**
- Create: `Assets/Game/Scripts/Input/GameInputActions.cs`
- Create: `Assets/Game/Scripts/Input/InputBindingStore.cs`
- Create: `Assets/Game/Scripts/Commands/UnitCommand.cs`
- Create: `Assets/Tests/EditMode/GameInputActionsTests.cs`
- Create: `Assets/Tests/EditMode/UnitCommandTests.cs`

**Interfaces:**
- Produces: `GameInputActions.MoveClick`, `AttackMove`, `Confirm`, `Stop`, `Skill1`, `Skill2`, `Skill3`, `Retreat`, `CenterCamera`, `Cancel`, `PointerPosition`; `InputBindingStore.Save/Load`; `UnitCommand.Move/Attack/AttackNearestInRange/Stop`。
- Consumes: Unity Input System and `UnityEngine.GameObject` only; this task must not reference future `CombatUnit`.

- [ ] **Step 1: Write the failing binding and command tests**

```csharp
[Test]
public void DefaultsMatchTheApprovedKeyboardAndMouseLayout()
{
    using (var input = new GameInputActions())
    {
        Assert.That(input.MoveClick.bindings[0].effectivePath, Is.EqualTo("<Mouse>/rightButton"));
        Assert.That(input.AttackMove.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/q"));
        Assert.That(input.Skill1.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/w"));
        Assert.That(input.Skill2.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/e"));
        Assert.That(input.Skill3.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/r"));
        Assert.That(input.Retreat.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/b"));
    }
}

[Test]
public void AttackNearestInRangeCommandDoesNotCarryADestination()
{
    UnitCommand command = UnitCommand.AttackNearestInRange();
    Assert.That(command.Kind, Is.EqualTo(UnitCommandKind.AttackNearestInRange));
    Assert.That(command.TargetObject, Is.Null);
    Assert.That(command.Destination, Is.EqualTo(default(Vector3)));
}

[Test]
public void BindingOverridesRoundTripThroughStore()
{
    PlayerPrefs.DeleteKey("af.input.bindings.v1");
    using (var first = new GameInputActions())
    {
        first.AttackMove.ApplyBindingOverride(0, "<Keyboard>/a");
        InputBindingStore.Save(first.Asset);
    }
    using (var second = new GameInputActions())
    {
        InputBindingStore.Load(second.Asset);
        Assert.That(second.AttackMove.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/a"));
    }
}
```

- [ ] **Step 2: Run EditMode and observe RED**

Run the EditMode command. Expected: compiler failures because `GameInputActions`, `InputBindingStore`, `UnitCommand`, and `UnitCommandKind` do not exist.

- [ ] **Step 3: Implement the minimum input and command API**

`GameInputActions` creates one `InputActionMap("Gameplay")`, exposes its `InputActionAsset`, creates the listed actions and bindings, and implements `IDisposable` by disposing its asset/map. `InputBindingStore.Save` writes `SaveBindingOverridesAsJson()` to PlayerPrefs key `af.input.bindings.v1`, calls `PlayerPrefs.Save()`, and `Load` applies only non-empty saved JSON.

`UnitCommand` is an immutable struct with `Kind`, `Destination`, and optional `GameObject TargetObject`. `Move(Vector3)`, `AttackNearestInRange()`, and `Stop()` construct commands without a target. `Attack(GameObject)` rejects null with `ArgumentNullException`. `UnitCommandKind` contains `Move`, `Attack`, `AttackNearestInRange`, `Stop`. Do not resolve a combat component in this stage.

- [ ] **Step 4: Run focused and complete EditMode GREEN**

Expected: all existing EditMode tests pass with no compiler errors.

- [ ] **Step 5: Commit only Task 1 files**

```bash
git add Assets/Game/Scripts/Input Assets/Game/Scripts/Commands/UnitCommand.cs Assets/Tests/EditMode/GameInputActionsTests.cs Assets/Tests/EditMode/UnitCommandTests.cs
git commit -m "feat: define rebindable MOBA input actions"
```

### Task 2：平面移动、攻击移动状态与相机联动

**Files:**
- Create: `Assets/Game/Scripts/Movement/UnitMotor.cs`
- Create: `Assets/Game/Scripts/Commands/AttackMoveState.cs`
- Create: `Assets/Game/Scripts/Commands/PlayerCommandController.cs`
- Modify: `Assets/Game/Scripts/Camera/MobaCameraController.cs`
- Modify: `Assets/Game/Editor/PrototypeSceneBuilder.cs`
- Create: `Assets/Tests/EditMode/AttackMoveStateTests.cs`
- Create: `Assets/Tests/PlayMode/PlayerMovementPlayModeTests.cs`

**Interfaces:**
- Produces: `UnitMotor.Configure(float, ArenaLayout)`, `SetDestination(Vector3)`, `Stop()`, `Tick(float)`, `IsMoving`; `AttackMoveState.Arm/Confirm/Cancel`; `PlayerCommandController.Issue(UnitCommand)` and `CurrentCommand`。
- Consumes: `ArenaLayout.Clamp`, `GameInputActions`, `UnitCommand`, `MobaCameraController.CenterOn`。

- [ ] **Step 1: Write the failing state and motion tests**

```csharp
[Test]
public void ConfirmDisarmsAttackMove()
{
    AttackMoveState state = new AttackMoveState();
    state.Arm();
    state.Confirm();
    Assert.That(state.IsArmed, Is.False);
}

[UnityTest]
public IEnumerator MotorReachesClampedDestination()
{
    GameObject go = new GameObject("Motor");
    UnitMotor motor = go.AddComponent<UnitMotor>();
    motor.Configure(5f, ArenaLayout.CreateDefault());
    motor.SetDestination(new Vector3(100f, 0f, 0f));
    for (int i = 0; i < 700; i++) yield return null;
    Assert.That(go.transform.position.x, Is.EqualTo(50f).Within(0.05f));
    Object.Destroy(go);
}
```

- [ ] **Step 2: Run the appropriate suites and observe RED**

Expected: compiler failures because `UnitMotor` and `AttackMoveState` do not exist.

- [ ] **Step 3: Implement minimal movement and command flow**

`UnitMotor` uses `Vector3.MoveTowards` on XZ in `Tick`, clamps all destinations through `ArenaLayout`, uses `Update` to call `Tick(Time.deltaTime)`, and exposes `IsMoving`. `AttackMoveState` has no Unity dependencies: `Arm` sets `IsArmed`; `Confirm` and `Cancel` only disarm.

`PlayerCommandController` owns `GameInputActions`, enables/disables them with the component, raycasts against the default physical layers (therefore ignores `Ignore Raycast`) and only treats the nearest hit as selectable when it is `Targetable`; a wall or any other nearer collider blocks a target behind it. It exposes `Issue(UnitCommand)` and records `CurrentCommand`. Right-click ground issues `Move`; right-click target issues `Attack`; `Q` arms attack move; its next left click always disarms it, then issues `Attack(target)` for a nearest `Targetable` hit or `AttackNearestInRange()` for every other click. Both attack intents stop the existing motor movement and cause no damage or pursuit in this stage. `S` issues `Stop`; `Esc` and right click while armed cancel without issuing movement. `Space` calls `MobaCameraController.CenterOn(transform)`.

Update the deterministic builder to create blue capsule `Player_Exusiai` at blue deployment with `UnitMotor` and `PlayerCommandController`, configure motor speed `5`, create/assign `Ground` and `Targetable` layers as needed, and assign the main camera's centering target to the player. Do not alter the Stage 1 arena geometry.

- [ ] **Step 4: Run full GREEN and perform Stage 2 manual gate**

Run both test suites. In Unity, verify right-click ground movement, `Q` then left-click target or ground records an attack intent and does not move the capsule, `S` stop, `Esc`/right-click cancellation, edge pan, middle drag, and `Space` centering; confirm Input Debugger actions are Input System actions rather than `UnityEngine.Input`.

- [ ] **Step 5: Commit only Task 2 files**

```bash
git add Assets/Game/Scripts/Movement Assets/Game/Scripts/Commands/AttackMoveState.cs Assets/Game/Scripts/Commands/PlayerCommandController.cs Assets/Game/Scripts/Camera/MobaCameraController.cs Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Game/Scenes/PrototypeArena.unity Assets/Tests/EditMode/AttackMoveStateTests.cs Assets/Tests/PlayMode/PlayerMovementPlayModeTests.cs
git commit -m "feat: add click movement and attack-move commands"
```

**Stage 2 gate:** all tests pass; player capsule can be controlled for five minutes without an input conflict or stuck command. In this pre-combat stage, Q+left-click must never visibly move the capsule; target legality, in-range selection, pursuit and actual attack are a Stage 3 manual gate.
