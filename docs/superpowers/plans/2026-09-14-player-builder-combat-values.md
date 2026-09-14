# 玩家干员构建数值调整 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让场景构建器创建的 `Player_Exusiai` 具有 1000 点基础生命和 50 点基础攻击力。

**Architecture:** 保持现有 `PrototypeSceneBuilder.CreatePlayer` 创建与配置流程，只替换 `CombatUnit.Configure` 的生命和攻击参数。场景冒烟测试从已构建的 `Player_Exusiai` 读取 `CombatUnit`，验证最终可玩的场景数值。

**Tech Stack:** Unity 6000.3.15f1、C#、Unity Test Framework、NUnit。

## Global Constraints

- 只改变玩家干员的 `MaxHealth` 与 `AttackPower`；其余 `CombatUnit.Configure` 参数必须保持不变。
- 重新运行 `Arknights Frontline → Build Prototype Arena` 会覆盖 `Assets/Game/Scenes/PrototypeArena.unity`，使构建器新数值进入场景。
- 不修改或提交 Packages、ProjectSettings、构建日志或其他用户未提交文件。

---

### Task 1: 更新玩家构建数值并验证场景

**Files:**

- Modify: `Assets/Game/Editor/PrototypeSceneBuilder.cs:154`
- Modify: `Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs:48-52`
- Modify: `Assets/Game/Scenes/PrototypeArena.unity`（仅由场景构建器重建生成）

**Interfaces:**

- Consumes: `CombatUnit.Configure(TeamId, Altitude, float maxHealth, float attackPower, float defense, float attackRange, float attackInterval, bool canAttackGround, bool canAttackAir)`。
- Produces: 构建场景内 `Player_Exusiai.GetComponent<CombatUnit>().MaxHealth == 1000f` 且 `AttackPower == 50f`。

- [ ] **Step 1: 写入失败的场景数值测试**

在 `PrototypeArenaContainsRequiredRoots` 中，已取得 `player` 并断言其存在后，写入：

```csharp
CombatUnit playerUnit = player.GetComponent<CombatUnit>();
Assert.That(playerUnit, Is.Not.Null);
Assert.That(playerUnit.MaxHealth, Is.EqualTo(1000f));
Assert.That(playerUnit.AttackPower, Is.EqualTo(50f));
```

该测试应在构建器和已保存场景仍使用旧数值时失败；把构建器的生命值改回 `100f` 或攻击力改回 `12f` 时，测试必须再次失败。

- [ ] **Step 2: 运行测试确认 RED**

Run:

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter ArknightsFrontline.Tests.PlayMode.ArenaSceneSmokeTests.PrototypeArenaContainsRequiredRoots -testResults TestResults/player-builder-values-red.xml -logFile TestResults/player-builder-values-red.log -quit
```

Expected: FAIL，指出 `Player_Exusiai` 的生命或攻击数值不是 `1000f` 与 `50f`。

- [ ] **Step 3: 实现最小构建器修改**

将 `CreatePlayer` 中的配置调用从：

```csharp
combatUnit.Configure(TeamId.Blue, Altitude.Ground, 100f, 12f, 2f, 6f, 0.5f, true, true);
```

改为：

```csharp
combatUnit.Configure(TeamId.Blue, Altitude.Ground, 1000f, 50f, 2f, 6f, 0.5f, true, true);
```

- [ ] **Step 4: 重建场景并运行 GREEN**

在 Unity 菜单执行 `Arknights Frontline → Build Prototype Arena`，保存生成的 `Assets/Game/Scenes/PrototypeArena.unity`，随后运行：

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ArknightsFrontline.Tests.PlayMode.ArenaSceneSmokeTests|ArknightsFrontline.Tests.PlayMode.DeathCorpsePresentationPlayModeTests" -testResults TestResults/player-builder-values-green.xml -logFile TestResults/player-builder-values-green.log -quit
```

Expected: PASS；玩家生命为 1000、攻击为 50，且场景根结构和死亡平面表现保持正常。

- [ ] **Step 5: 人工验收并提交**

1. 打开 `PrototypeArena` 并进入 Play Mode。
2. 选择 `Player_Exusiai`，确认 `Combat Unit` 显示 `Max Health = 1000`、`Attack Power = 50`。
3. 确认头顶血条满格，移动、攻击和死亡平面表现未改变。

```bash
git add Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs Assets/Game/Scenes/PrototypeArena.unity
git commit -m "feat: increase player builder combat values"
```

## Plan self-review

- Spec coverage: Task 1 覆盖生命、攻击、仅限构建器玩家、场景验证和不变参数。
- Scope: 不涉及小兵、塔、输入、技能或 Packages/ProjectSettings。
- Type consistency: 测试读取既有 `CombatUnit.MaxHealth` 和 `CombatUnit.AttackPower`，构建器继续使用既有 `Configure` 接口。
- 完整性检查：所有步骤均给出确定的文件、数值、命令和预期结果。
