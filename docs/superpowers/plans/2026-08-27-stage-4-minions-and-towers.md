# 阶段 4：兵线与防御塔 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在原型场景中实现双向兵线推进与交战、防御塔自动攻击，以及由塔摧毁触发并冻结战斗的胜负结果。

**Architecture:** 兵线、塔与波次分别由独立组件驱动，继续复用 `CombatUnit`、`TargetRules`、`TargetSelector`、`BasicAttackController` 和 `Projectile`。`MatchOutcomeController` 是本阶段唯一的结果入口：它在同一更新周期内汇总塔死亡，确定唯一结果后停止波次、控制器、攻击和已有投射物。

**Tech Stack:** Unity 6000.3.15f1、Unity Input System 1.17.0、URP、Unity Test Framework、NUnit、C# 9 花括号命名空间、macOS。

## Global Constraints

- 以 `docs/superpowers/specs/2026-08-27-stage-4-minions-and-towers-design.md` 为本计划的权威规格。
- 只实现兵线、塔与即时胜负；不实现角色完整数值重配、电脑角色、技能、撤退、重生、HUD、统计、重新开始或 15 分钟超时。
- 复用现有战斗入口；不得修改玩家右键、`A`+左键、停止或镜头语义。
- 新增运行时代码位于 `Assets/Game/Scripts`，测试位于 `Assets/Tests`，使用 C# 9 花括号命名空间。
- 不修改或提交用户持有的 `Assets/Game/Scenes/PrototypeArena.unity`、Packages、ProjectSettings、构建日志或无关未提交文件。场景变化只提交 `Assets/Game/Editor/PrototypeSceneBuilder.cs`；用户在 Unity 中手动执行菜单并保存场景。
- Unity 自动测试只能在临时干净 checkout 中运行；若 Unity 在测试发现前仍因 LicenseClient `ResponseCode: 505 / Unsupported protocol version '1.18.1'` 失败，记录为环境阻塞，绝不宣称测试通过。

---

## File Structure

| 路径 | 职责 |
| --- | --- |
| `Assets/Game/Scripts/Combat/TargetSelector.cs` | 在保持现有最近合法目标 API 的同时，提供带候选过滤器的最近目标查找。 |
| `Assets/Game/Scripts/Combat/Projectile.cs` | 提供显式取消入口，保证结算结束时飞行中的弹道不命中。 |
| `Assets/Game/Scripts/Arena/LaneMinionController.cs` | 驱动单个小兵的推进、非塔优先索敌、攻塔回退和结束停机。 |
| `Assets/Game/Scripts/Arena/TowerCombatController.cs` | 驱动塔的原地最近目标索敌与结束停机。 |
| `Assets/Game/Scripts/Arena/MinionWaveSpawner.cs` | 负责立即首波、25 秒周期和确定性四兵种生成。 |
| `Assets/Game/Scripts/Arena/MatchOutcome.cs` | 定义 `None`、`BlueVictory`、`RedVictory`、`Draw` 四种只读结果。 |
| `Assets/Game/Scripts/Arena/MatchOutcomeController.cs` | 汇总两座塔死亡并冻结所有阶段 4 战斗活动。 |
| `Assets/Game/Editor/PrototypeSceneBuilder.cs` | 生成可选中可受击的塔、兵线/结果控制器及所需配置。 |
| `Assets/Tests/EditMode/*` | 对每个无 UI 的规则组件做确定性测试。 |
| `Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs` | 验证重建后的场景含完整阶段 4 根组件并能出现首波。 |

### Task 1: 可过滤的目标选择与自主单位控制

**Files:**
- Modify: `Assets/Game/Scripts/Combat/TargetSelector.cs`
- Create: `Assets/Game/Scripts/Arena/LaneMinionController.cs`
- Create: `Assets/Game/Scripts/Arena/TowerCombatController.cs`
- Create: `Assets/Tests/EditMode/LaneMinionControllerTests.cs`
- Create: `Assets/Tests/EditMode/TowerCombatControllerTests.cs`
- Modify: `Assets/Tests/EditMode/TargetSelectorTests.cs`

**Interfaces:**
- Consumes: `CombatUnit`, `TargetRules`, `BasicAttackController`, `UnitMotor`, `ArenaLayout`。
- Produces: `TargetSelector.FindNearestInRange(CombatUnit, Predicate<CombatUnit>)`, `LaneMinionController.Configure(...)`, `LaneMinionController.Tick(float)`, `LaneMinionController.StopForMatch()`, `TowerCombatController.Configure(...)`, `TowerCombatController.Tick(float)`, `TowerCombatController.StopForMatch()`。

- [ ] **Step 1: 写出失败的选择与自主控制测试**

在 `TargetSelectorTests` 中新增候选过滤器案例，验证过滤器排除较近目标时会返回较远的合法目标：

```csharp
[Test]
public void SelectorSkipsCandidatesRejectedByFilter()
{
    CombatUnit attacker = CreateUnitAt("Blue", TeamId.Blue, Altitude.Ground, Vector3.zero, 6f, true, false);
    CombatUnit rejected = CreateUnitAt("Rejected", TeamId.Red, Altitude.Ground, new Vector3(2f, 0f, 0f), 0f, false, false);
    CombatUnit accepted = CreateUnitAt("Accepted", TeamId.Red, Altitude.Ground, new Vector3(4f, 0f, 0f), 0f, false, false);

    Assert.That(TargetSelector.FindNearestInRange(attacker, candidate => candidate != rejected), Is.EqualTo(accepted));
}
```

新建 `LaneMinionControllerTests`，用 `CreateUnit` 工厂创建附带 `UnitMotor`、`BasicAttackController` 的蓝方地面兵、红方地面敌人与红塔。覆盖以下可直接调用 `Tick` 的断言：无目标时蓝兵向 `redTower.transform.position` 的 X 轴正方向移动；范围内敌兵时 `motor.IsMoving` 为假且 `attack.CurrentTarget` 为敌兵；敌兵死亡后重新推进；敌兵与塔都在范围内时仍攻击敌兵；只有塔在范围内时攻击塔；地面兵忽略红方空中兵。

新建 `TowerCombatControllerTests`，创建原地蓝塔和两个红方单位，验证塔选择较近者、较近者死亡后切到另一个、空中合法性取决于 `CanAttackAir`，并验证 `StopForMatch()` 清空 `BasicAttackController.CurrentTarget` 且后续 `Tick` 不重新选目标。

- [ ] **Step 2: 运行测试并确认 RED**

运行 EditMode 过滤：`TargetSelectorTests|LaneMinionControllerTests|TowerCombatControllerTests`。

预期：新重载和两个控制器尚不存在，编译失败；若 Unity 在测试发现前报 LicenseClient 505，则记录该环境阻塞。

- [ ] **Step 3: 实现最小目标选择与控制器**

将 `TargetSelector` 的已有实现收敛到可过滤重载；无过滤器的原 API 必须保持原行为：

```csharp
public static CombatUnit FindNearestInRange(CombatUnit attacker)
{
    return FindNearestInRange(attacker, null);
}

public static CombatUnit FindNearestInRange(CombatUnit attacker, Predicate<CombatUnit> filter)
{
    // 保留既有 FindObjectsByType(...InstanceID)、合法性、水平距离和严格小于 tie-break。
    // 仅在 filter != null && !filter(candidate) 时跳过候选者。
}
```

实现 `LaneMinionController`。`Configure` 必须拒绝任何空的 owner/motor/attack/enemyTower 参数，保存 `forwardDestination`，并在拥有者死亡时调用 `StopForMatch`。`Tick` 的核心顺序必须如下：

```csharp
public void Tick(float deltaTime)
{
    if (stopped || owner.IsDead) { StopForMatch(); return; }
    CombatUnit unitTarget = TargetSelector.FindNearestInRange(owner,
        candidate => candidate.GetComponent<TowerCombatController>() == null);
    CombatUnit target = unitTarget ?? FindTowerIfInRange();
    if (target != null) { motor.Stop(); attack.SetTarget(target); return; }
    attack.ClearTarget();
    motor.SetDestination(forwardDestination);
}
```

`FindTowerIfInRange` 只在 `TargetRules.IsLegal(owner, enemyTower)` 且它的水平距离不大于 `owner.AttackRange` 时返回敌塔。`Update` 调用 `Tick(Time.deltaTime)`；`StopForMatch` 将 `stopped` 置真、停止 motor 并清除攻击。波次生成器调用 `Configure` 时给蓝兵 `layout.RedTower`、红兵 `layout.BlueTower` 作为前进终点。

实现 `TowerCombatController`。`Configure` 订阅 owner 的死亡；`Tick` 在未停止且存活时执行 `attack.SetTarget(TargetSelector.FindNearestInRange(owner))`，未找到目标则 `attack.ClearTarget()`；`StopForMatch` 与 `OnOwnerDied` 都只清除攻击并阻止之后重选。塔绝不添加或使用 `UnitMotor`。

- [ ] **Step 4: 运行聚焦 GREEN 与全量 EditMode**

运行 `TargetSelectorTests|LaneMinionControllerTests|TowerCombatControllerTests`，随后运行完整 EditMode 套件。

预期：新测试绿色，既有 TargetSelector 的同距 InstanceID 规则不变；或记录同一 LicenseClient 预编译阻塞。

- [ ] **Step 5: 提交 Task 1**

```bash
git add Assets/Game/Scripts/Combat/TargetSelector.cs Assets/Game/Scripts/Arena/LaneMinionController.cs Assets/Game/Scripts/Arena/TowerCombatController.cs Assets/Tests/EditMode/TargetSelectorTests.cs Assets/Tests/EditMode/LaneMinionControllerTests.cs Assets/Tests/EditMode/TowerCombatControllerTests.cs
git commit -m "feat: add autonomous minion and tower targeting"
```

### Task 2: 波次生成与固定兵种配置

**Files:**
- Create: `Assets/Game/Scripts/Arena/MinionWaveSpawner.cs`
- Create: `Assets/Tests/EditMode/MinionWaveSpawnerTests.cs`

**Interfaces:**
- Consumes: Task 1 的 `LaneMinionController.Configure(...)`、`CombatUnit`、`UnitMotor`、`BasicAttackController`、`ArenaLayout`。
- Produces: `MinionWaveSpawner.Configure(...)`, `MinionWaveSpawner.Tick(float)`, `MinionWaveSpawner.StopForMatch()`, `MinionWaveSpawner.SpawnWaveNow()` 和 `public int SpawnedWaveCount { get; }`。

- [ ] **Step 1: 写出失败的波次测试**

创建一个 spawner、两座已配置 CombatUnit 的塔和空父物体；通过 `Configure` 注入 `ArenaLayout.CreateDefault()`、父物体、塔引用与蓝/红材质可为空的测试配置。调用 `SpawnWaveNow()` 并断言：`SpawnedWaveCount == 1`；父物体下蓝红合计八个 `CombatUnit`；每方恰有三个 `Altitude.Ground` 和一个 `Altitude.Air`；每个生成物具有 `UnitMotor`、`BasicAttackController` 和 `LaneMinionController`。

再覆盖：`Tick(24.99f)` 不增波，`Tick(0.01f)` 增至第二波；`StopForMatch()` 后 `Tick(100f)` 不再增波；蓝地面兵具 `400/35/10/1.5/1.2`、仅地面、3.0 移速，红空中兵具 `280/28/5/4.5/1.0`、可攻击地空、3.2 移速。

- [ ] **Step 2: 运行测试并确认 RED**

运行 `MinionWaveSpawnerTests`。预期：类型不存在而编译失败，或 LicenseClient 505 在测试发现前阻塞。

- [ ] **Step 3: 实现确定性生成器**

实现以下稳定 API，所有配置引用为空时抛 `ArgumentNullException`，材质为空时保留 Unity 默认材质：

```csharp
public void Configure(
    Transform minionParent, ArenaLayout arenaLayout,
    CombatUnit blueTower, CombatUnit redTower,
    Material blueMaterial, Material redMaterial, int targetableLayer)
```

`Awake` 若所有序列化引用已由场景配置完成则自行调用同一配置路径；`Start` 必须调用 `SpawnWaveNow()`，并将周期计时归零。`Update` 调用 `Tick(Time.deltaTime)`。

`SpawnWaveNow()` 必须一次生成蓝、红各四个单位。每方使用三个固定 Z 偏移 `-2f`、`0f`、`2f` 生成地面兵，使用 Z=`4f` 生成空中兵；蓝兵起点在 `layout.BlueTower + new Vector3(6f, 1f, offset)`，红兵在 `layout.RedTower + new Vector3(-6f, 1f, offset)`。每个物体是 `PrimitiveType.Capsule`（地面）或 `PrimitiveType.Sphere`（空中），名为 `BlueGroundMinion`、`BlueAirMinion`、`RedGroundMinion`、`RedAirMinion` 加波次和序号；设置 `Targetable` 层、阵营颜色、`CombatUnit` 固定值、`UnitMotor.Configure(speed, layout)`、`BasicAttackController` 和 `LaneMinionController.Configure`。

`Tick` 仅在未停止时累计非负 delta；使用 `while (elapsed >= 25f)` 减去 25 并生成一波，防止大帧跳过波次。`StopForMatch` 置停止标记，不销毁已生成对象。

- [ ] **Step 4: 运行聚焦 GREEN 与全量 EditMode**

运行 `MinionWaveSpawnerTests`，随后完整 EditMode。

预期：首波与 25 秒周期精确，生成数值符合固定表；或记录 LicenseClient 环境阻塞。

- [ ] **Step 5: 提交 Task 2**

```bash
git add Assets/Game/Scripts/Arena/MinionWaveSpawner.cs Assets/Tests/EditMode/MinionWaveSpawnerTests.cs
git commit -m "feat: spawn opposing minion waves"
```

### Task 3: 塔死亡结果与结算冻结

**Files:**
- Create: `Assets/Game/Scripts/Arena/MatchOutcome.cs`
- Create: `Assets/Game/Scripts/Arena/MatchOutcomeController.cs`
- Modify: `Assets/Game/Scripts/Combat/Projectile.cs`
- Create: `Assets/Tests/EditMode/MatchOutcomeControllerTests.cs`
- Modify: `Assets/Tests/EditMode/ProjectileTests.cs`

**Interfaces:**
- Consumes: Task 1 的 `LaneMinionController.StopForMatch()`、`TowerCombatController.StopForMatch()`；Task 2 的 `MinionWaveSpawner.StopForMatch()`；`BasicAttackController.ClearTarget()`。
- Produces: `enum MatchOutcome { None, BlueVictory, RedVictory, Draw }`, `MatchOutcomeController.Configure(CombatUnit, CombatUnit, MinionWaveSpawner)`, `MatchOutcomeController.Tick()`, `public bool IsMatchOver { get; }`, `public MatchOutcome Outcome { get; }`, `Projectile.Cancel()`。

- [ ] **Step 1: 写出失败的结果与取消测试**

`MatchOutcomeControllerTests` 创建蓝红塔、spawner、各一个 Lane/Tower 控制器和已指定目标的 `BasicAttackController`。测试依次：只杀红塔并 `Tick()` 后为 `BlueVictory`；只杀蓝塔后为 `RedVictory`；先杀两塔、再单次 `Tick()` 后为 `Draw`；确定结果后 spawner 不再生成，两个攻击控制器的当前目标为空，兵/塔控制器的再 `Tick` 不会重新目标。

在 `ProjectileTests` 中生成已初始化、距离目标很近的弹道，调用 `Cancel()` 后 `Tick(10f)`；断言 `IsFinished` 为真且目标生命不变。

- [ ] **Step 2: 运行测试并确认 RED**

运行 `MatchOutcomeControllerTests|ProjectileTests`。预期：新类型/API 缺失，或 LicenseClient 505 阻塞。

- [ ] **Step 3: 实现只读结果与冻结**

新增：

```csharp
public enum MatchOutcome
{
    None,
    BlueVictory,
    RedVictory,
    Draw
}
```

`MatchOutcomeController` 在 `Configure` 中订阅两座塔 `Died` 事件，并保存 `blueTowerDestroyed` 与 `redTowerDestroyed`。`Tick` 只在尚未结束且至少一座塔已死亡时结算；因此两次同步伤害后再调用一次 `Tick` 必须得到 `Draw`。`Update` 调用 `Tick()`，`OnDestroy` 取消订阅。

确定结果后，精确执行：`spawner.StopForMatch()`；遍历 `FindObjectsByType<LaneMinionController>` 与 `TowerCombatController` 调用 `StopForMatch()`；遍历 `FindObjectsByType<BasicAttackController>` 调用 `ClearTarget()` 并设置 `enabled = false`；遍历 `FindObjectsByType<Projectile>` 调用 `Cancel()`。最后将 `IsMatchOver` 置真；此顺序确保停机发生时已确定结果，且结果不再被二次覆盖。

在 `Projectile` 中新增：

```csharp
public void Cancel()
{
    IsFinished = true;
    if (Application.isPlaying)
    {
        Destroy(gameObject);
    }
}
```

让原有 `Finish()` 调用 `Cancel()`，使取消和自然结束共享同一完成语义。

- [ ] **Step 4: 运行聚焦 GREEN 与全量 EditMode**

运行 `MatchOutcomeControllerTests|ProjectileTests`，再运行完整 EditMode。

预期：三种结果、同周期平局与结算后无伤害均通过；或记录环境阻塞。

- [ ] **Step 5: 提交 Task 3**

```bash
git add Assets/Game/Scripts/Arena/MatchOutcome.cs Assets/Game/Scripts/Arena/MatchOutcomeController.cs Assets/Game/Scripts/Combat/Projectile.cs Assets/Tests/EditMode/MatchOutcomeControllerTests.cs Assets/Tests/EditMode/ProjectileTests.cs
git commit -m "feat: resolve tower destruction outcomes"
```

### Task 4: 确定性场景构建与阶段验收

**Files:**
- Modify: `Assets/Game/Editor/PrototypeSceneBuilder.cs`
- Modify: `Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs`
- Create: `Assets/Tests/PlayMode/MinionLanePlayModeTests.cs`

**Interfaces:**
- Consumes: Task 1–3 的所有公开配置 API。
- Produces: 菜单重建后含有两座配置完成的塔、`MinionWaveSpawner` 与 `MatchOutcomeController` 的 `PrototypeArena`。

- [ ] **Step 1: 写出失败的场景测试**

扩展 `ArenaSceneSmokeTests`：加载 `PrototypeArena` 后断言 `ArenaBootstrap.BlueTower` 与 `RedTower` 各自存在 `CombatUnit`、`BasicAttackController`、`TowerCombatController` 和根 `BoxCollider`；断言 `MatchOutcomeController` 与 `MinionWaveSpawner` 存在且 `MatchOutcomeController.Outcome == MatchOutcome.None`。

新建 `MinionLanePlayModeTests`：加载场景、等待一帧，查找 `CombatUnit` 名称中含 `Minion` 的对象，断言合计八个、每队三地一空；记录一个蓝地面兵初始 X，连续 yield 20 帧后断言其 X 增大或它已在攻击有效目标（`BasicAttackController.CurrentTarget != null`）。

- [ ] **Step 2: 运行场景测试并确认 RED**

运行 `ArenaSceneSmokeTests|MinionLanePlayModeTests`。预期：当前构建器尚未添加组件/首波，断言失败；或 LicenseClient 505 阻塞。

- [ ] **Step 3: 更新场景构建器**

将 `CreateTower` 改为在塔根物体添加 `BoxCollider`（`center = new Vector3(0f, 3f, 0f)`、`size = new Vector3(3f, 6f, 3f)`）、设置根层为 `Targetable`、添加并配置 `CombatUnit`、`BasicAttackController` 和 `TowerCombatController`；蓝塔 `TeamId.Blue`、红塔 `TeamId.Red`，两者均为地面、`6000f, 150f, 40f, 9f, 1f, true, true`。删除由 `GameObject.CreatePrimitive` 自动添加到视觉子物体上的 `BoxCollider`，防止鼠标命中没有 `CombatUnit` 的子物体。保留视觉子物体和 `ArenaBootstrap.AssignTowers`。

在 `Build` 中，在塔创建后取得其 `CombatUnit`，创建 `MatchOutcomeController` 与 `MinionWaveSpawner`，分别调用：

```csharp
outcome.Configure(blueTowerUnit, redTowerUnit, waveSpawner);
waveSpawner.Configure(arenaRoot.transform, layout, blueTowerUnit, redTowerUnit,
    blueMaterial, redMaterial, targetableLayer);
```

更新相机构建默认值为用户认可的手动起点 `position = new Vector3(0f, 42f, -34f)`、`rotation = Quaternion.Euler(55f, 0f, 0f)`、`camera.fieldOfView = 55f`；保持透视投影。不要改动或暂存用户现有场景文件。

- [ ] **Step 4: 重建、运行 GREEN 并进行人工门禁**

在 Unity 编辑器执行 **Arknights Frontline → Build Prototype Arena**，然后保存场景。运行完整 EditMode 与 PlayMode；若自动执行仍遇 LicenseClient 505，保存失败证据并改为人工门禁。

人工门禁：确认首波双方各四兵立即出现；确认双方小兵相遇、交战、清场后继续推进并攻击塔；手动消灭一座塔，确认控制器结果正确、新兵不再出现且现存兵/塔停止攻击；最后按 `⌘S` 保存。若需要第二训练目标或相机微调，只能在最后一次 Build 后手动完成。

- [ ] **Step 5: 提交 Task 4**

```bash
git add Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs Assets/Tests/PlayMode/MinionLanePlayModeTests.cs
git commit -m "feat: build minion and tower prototype arena"
```

## Plan self-review

- 规格覆盖：Task 1 实现推进、索敌、地空限制、兵优先与塔索敌；Task 2 实现立即首波、25 秒周期和固定兵种；Task 3 实现塔摧毁、平局与战斗冻结；Task 4 提供可重建场景、场景测试和人工验收。
- 范围控制：没有任务引入英雄重配、技能、电脑、撤退、重生、HUD、统计、重开或超时。
- 类型一致性：Task 2 只使用 Task 1 产生的 `LaneMinionController.Configure`；Task 3 只调用三个已定义的 `StopForMatch` API；Task 4 使用同一套 `Configure` 签名。
- 占位扫描：计划没有占位标记或未命名的后续实现；LicenseClient 是明确记录的环境验证分支。
