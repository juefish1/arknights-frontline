# 防御塔生命值与血条 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将两座防御塔生命值设为 500、攻击力设为 20，并显示定位在塔顶的常驻血条。

**Architecture:** 复用 `HealthBarPresenter`，让它在根节点没有 Renderer 时合并子节点 Renderer 的 bounds。构建器给每座塔添加该组件并把 `CombatUnit.Configure` 的生命参数改为 500、攻击力改为 20；场景冒烟测试验证构建结果。

**Tech Stack:** Unity 6000.3.15f1、C#、Unity UI、Unity Test Framework、NUnit。

## Global Constraints

- 将蓝、红塔的 `maxHealth` 改为 500、攻击力设为 20，并新增塔的 `HealthBarPresenter`。
- 塔的防御 40、攻击范围 9、攻击间隔 1、地空攻击能力保持不变。
- 不修改 Packages、ProjectSettings、构建日志或其他用户未提交文件。
- 用户已授权在 `dev` 分支直接开发，并允许构建器覆盖 `PrototypeArena.unity`。

---

### Task 1: 验证塔血量和塔顶血条

**Files:**

- Modify: `Assets/Game/Scripts/Combat/HealthBarPresenter.cs:96-110`
- Modify: `Assets/Game/Editor/PrototypeSceneBuilder.cs:114-120`
- Modify: `Assets/Tests/EditMode/HealthBarPresenterTests.cs`
- Modify: `Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs:36-43`
- Modify: `Assets/Game/Scenes/PrototypeArena.unity`（由构建器重建生成）

**Interfaces:**

- Consumes: `CombatUnit`、子物体 `Renderer.bounds`、现有世界空间 `HealthBarPresenter`。
- Produces: 两座塔的 `CombatUnit.MaxHealth == 500f`、`HealthBarPresenter.IsVisible == true`，且血条位于塔可见模型顶部。

- [x] **Step 1: 写入回归测试**

在 `PrototypeArenaContainsRequiredRoots` 中取得两座塔的 `CombatUnit` 后加入：

```csharp
Assert.That(arena.BlueTower.GetComponent<CombatUnit>().MaxHealth, Is.EqualTo(500f));
Assert.That(arena.RedTower.GetComponent<CombatUnit>().MaxHealth, Is.EqualTo(500f));
Assert.That(arena.BlueTower.GetComponent<HealthBarPresenter>(), Is.Not.Null);
Assert.That(arena.RedTower.GetComponent<HealthBarPresenter>(), Is.Not.Null);
Assert.That(arena.BlueTower.Find("HealthBar"), Is.Not.Null);
Assert.That(arena.RedTower.Find("HealthBar"), Is.Not.Null);
```

在现有 `HealthBarPresenter` EditMode 测试中增加一个塔形结构（根节点挂 `CombatUnit`，子节点挂 Renderer），调用 `Tick()` 后验证血条的 Y 坐标高于子 Renderer 的 bounds.max.y。

- [ ] **Step 2: 运行聚焦测试**

运行现有 PlayMode 场景测试和新增血条定位测试，验证当前场景的两塔数值、血条组件及子 Renderer 定位逻辑。若 Unity Licensing Client 或 Package Manager 阻塞，记录日志并继续静态检查。

- [x] **Step 3: 实现最小修改**

将塔配置改为：

```csharp
CombatUnit combatUnit = tower.AddComponent<CombatUnit>();
combatUnit.Configure(team, Altitude.Ground, 500f, 20f, 40f, 9f, 1f, true, true);
tower.AddComponent<HealthBarPresenter>();
```

将 `HealthBarPresenter.PositionAboveUnit()` 改为优先读取 `GetComponentsInChildren<Renderer>(true)` 的合并 bounds；没有任何 Renderer 时保留原来的 transform 上方回退位置。

- [ ] **Step 4: 验证**

不重建已有的 `PrototypeArena.unity`；核对当前序列化的两塔数据后，运行：

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ArknightsFrontline.Tests.PlayMode.ArenaSceneSmokeTests|ArknightsFrontline.Tests.PlayMode.DeathCorpsePresentationPlayModeTests" -testResults TestResults/tower-health-bars-green.xml -logFile TestResults/tower-health-bars-green.log -quit
```

预期：所有相关 PlayMode 测试通过；两座塔均为 500 生命并拥有可见血条。

- [ ] **Step 5: 人工验收并提交**

进入 Play Mode，确认血条位于两座塔顶端；对塔造成伤害后填充缩短。提交仅包含本任务列出的七个文件。

```bash
git add Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Game/Scenes/PrototypeArena.unity Assets/Game/Scripts/Combat/HealthBarPresenter.cs Assets/Tests/EditMode/HealthBarPresenterTests.cs Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs docs/superpowers/specs/2026-09-14-tower-health-bars-design.md docs/superpowers/plans/2026-09-14-tower-health-bars.md
git commit -m "feat: add tower health bars and rebalance stats"
```

## Plan self-review

- 需求覆盖：塔生命 500、两座塔血条、塔顶定位、其他战斗参数不变。
- 范围检查：未引入塔专用组件或无关项目设置。
- 类型检查：继续使用现有 `CombatUnit`、`HealthBarPresenter` 接口。
- 完整性检查：每一步包含明确文件、代码、命令和预期结果。

## 实施状态（2026-09-14）

- [x] 已更新构建器：两座塔的 `maxHealth/currentHealth` 为 500，并挂载 `HealthBarPresenter`。
- [x] 已更新血条定位：合并子层级 Renderer 的 bounds 后定位到塔顶。
- [x] 已补充 EditMode 子 Renderer 定位回归测试与 PlayMode 塔血量/血条断言。
- [x] 已同步 `PrototypeArena.unity` 的两座塔序列化数据。
- [ ] Unity 批处理重建/测试：受本机 Unity Package Manager socket 权限与 Licensing/额度限制阻塞，未宣称通过。
- [ ] 进入 Play Mode 的人工验收：需在本机 Unity 编辑器可正常启动后执行。
