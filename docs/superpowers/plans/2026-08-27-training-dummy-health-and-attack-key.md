# 训练目标生命值与攻击键位 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将原型场景训练目标的生命值设为 1000，并将攻击指令默认按键从 `Q` 改为 `A`。

**Architecture:** 场景数据继续由 `PrototypeSceneBuilder` 生成，避免直接修改用户未提交的 Unity 场景。输入映射继续由 `GameInputActions` 作为唯一默认键位来源；已有的 `InputBindingStore` 覆盖机制不变，因此已保存的自定义绑定仍优先于默认值。

**Tech Stack:** Unity 6000.3.15f1、Unity Input System 1.17.0、NUnit、Unity Test Framework、C# 9。

## Global Constraints

- 不修改或提交用户持有的 `Assets/Game/Scenes/PrototypeArena.unity`、Packages、ProjectSettings 或构建日志。
- 训练目标仅将最大和初始生命值由 `40f` 调整至 `1000f`；攻击、防御、攻击间隔和目标规则保持不变。
- `AttackMove` 的默认绑定必须为 `<Keyboard>/a`；保存的用户绑定覆盖必须继续生效。
- Unity 自动化测试若在编译前被 LicenseClient `ResponseCode: 505 / Unsupported protocol version '1.18.1'` 阻断，记录为环境阻塞，不宣称测试通过。

---

### Task 1: 训练目标 1000 生命值

**Files:**
- Modify: `Assets/Game/Editor/PrototypeSceneBuilder.cs:130`
- Modify: `Assets/Tests/PlayMode/PlayerMovementPlayModeTests.cs:72-73`

**Interfaces:**
- Consumes: `CombatUnit.Configure(TeamId, Altitude, float, float, float, float, float, bool, bool)`。
- Produces: 通过 `Arknights Frontline → Build Prototype Arena` 生成的 `TrainingDummy_Red.MaxHealth == 1000f` 与 `CurrentHealth == 1000f`。

- [ ] **Step 1: 先修改场景验收测试为失败预期**

在 `PlayerMovementPlayModeTests` 的训练目标断言中改为：

```csharp
Assert.That(unit.MaxHealth, Is.EqualTo(1000f));
Assert.That(unit.CurrentHealth, Is.EqualTo(1000f));
```

- [ ] **Step 2: 运行场景测试并确认 RED**

运行：

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter ArknightsFrontline.Tests.PlayMode.PlayerMovementPlayModeTests -testResults TestResults/training-dummy-health-playmode.xml -logFile TestResults/training-dummy-health-playmode.log -quit
```

预期：在尚未更新构建器时，生命值断言失败；若许可证服务在测试启动前失败，则记录完整 LicenseClient 错误。

- [ ] **Step 3: 更新场景构建器的唯一配置来源**

将训练目标配置替换为：

```csharp
combatUnit.Configure(TeamId.Red, Altitude.Ground, 1000f, 0f, 2f, 0f, 0f, false, false);
```

- [ ] **Step 4: 重建并验证**

运行同一 PlayMode 测试。若测试环境可用，预期测试通过；随后在 Unity 使用 `Arknights Frontline → Build Prototype Arena` 重建场景，并在 Inspector 核实 `TrainingDummy_Red` 的 `Max Health` 和 `Current Health` 均为 1000。

- [ ] **Step 5: 提交任务文件**

```bash
git add Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Tests/PlayMode/PlayerMovementPlayModeTests.cs
git commit -m "feat: increase training dummy health"
```

### Task 2: 攻击指令默认键位改为 A

**Files:**
- Modify: `Assets/Game/Scripts/Input/GameInputActions.cs:16`
- Modify: `Assets/Tests/EditMode/GameInputActionsTests.cs:16`
- Modify: `Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs` 中每个 `keyboard.qKey` 输入模拟。

**Interfaces:**
- Consumes: `GameInputActions.AttackMove` 与 `InputBindingStore.Load(InputActionAsset)`。
- Produces: 未保存自定义覆盖时，`AttackMove.bindings[0].effectivePath == "<Keyboard>/a"`；按住 `A` 保持现有攻击指令与范围圈行为。

- [ ] **Step 1: 先修改默认绑定断言及输入模拟**

将默认绑定断言改为：

```csharp
Assert.That(input.AttackMove.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/a"));
```

在 `PlayerCommandInputPlayModeTests` 中把所有：

```csharp
Press(keyboard.qKey);
Release(keyboard.qKey);
```

分别改为：

```csharp
Press(keyboard.aKey);
Release(keyboard.aKey);
```

保留测试中显式调用 `ApplyBindingOverride` 的逻辑，以继续验证保存的自定义覆盖。

- [ ] **Step 2: 运行聚焦测试并确认 RED**

运行：

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter ArknightsFrontline.Tests.EditMode.GameInputActionsTests -testResults TestResults/attack-key-editmode.xml -logFile TestResults/attack-key-editmode.log -quit
```

预期：在默认绑定仍是 `Q` 时默认键位断言失败；若许可证服务在测试启动前失败，则记录完整 LicenseClient 错误。

- [ ] **Step 3: 修改默认输入映射**

将 `GameInputActions` 中的攻击指令创建改为：

```csharp
AttackMove = AddAction("AttackMove", InputActionType.Button, "<Keyboard>/a", "ffeb75c4-54fd-456f-a397-0b2f3b0a3f02");
```

不改动 `InputBindingStore`；它仍会在存在保存覆盖时加载该覆盖。

- [ ] **Step 4: 运行输入测试并手动验证**

运行 Task 2 的 EditMode 测试和：

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter ArknightsFrontline.Tests.PlayMode.PlayerCommandInputPlayModeTests -testResults TestResults/attack-key-playmode.xml -logFile TestResults/attack-key-playmode.log -quit
```

若测试环境可用，预期通过。手动进入 Play Mode：按住 `A` 时显示攻击范围，`A + 左键` 能下达攻击，单独按 `Q` 不触发攻击指令。

- [ ] **Step 5: 提交任务文件**

```bash
git add Assets/Game/Scripts/Input/GameInputActions.cs Assets/Tests/EditMode/GameInputActionsTests.cs Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs
git commit -m "feat: bind attack move to a"
```

## Plan self-review

- 规格覆盖：Task 1 覆盖训练目标生命值及场景测试；Task 2 覆盖默认攻击键位、默认绑定测试和真实输入模拟。
- 范围：不改战斗结算、目标选择、序列化场景、Packages 或 ProjectSettings。
- 类型一致性：所有改动复用现有 `CombatUnit.Configure`、`GameInputActions.AttackMove` 和 `InputBindingStore` 接口，无新增运行时接口。
