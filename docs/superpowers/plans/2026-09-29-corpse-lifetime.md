# Corpse Lifetime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让小兵尸体在生成 5 秒后消失、干员尸体保留至同一干员再部署前显式清理，并让防御塔尸体在当前场景中永久保留。

**Architecture:** 新增挂在尸体对象上的 `CorpseLifetimeController`，由它独立计时并维护按干员 key 索引的静态登记；`DeathCorpsePresenter` 只负责把序列化的 `UnitKind` 和原对象名称传给控制器。小兵生成器与确定性场景构建器显式配置类别，保存场景通过现有构建入口重建。

**Tech Stack:** Unity 6000.6.2f1、C#、NUnit、Unity Test Framework、EditMode/PlayMode tests、Git。

**Spec:** `docs/superpowers/specs/2026-09-29-corpse-lifetime-design.md`

## Global Constraints

- 直接在 `D:\arknights-frontline` 的 `dev` 分支工作，不创建工作树。
- 执行采用子代理实施、独立复审、TDD；子代理模型最高 `gpt-6-luna`，推理强度最高 `xhigh`，超出限制时请求人工审核。
- 小兵生命周期固定为从尸体创建起 5 秒，包含空中尸体 0.3 秒坠落；达到 5 秒直接销毁，不淡出。
- 干员尸体不自动计时；未来再部署在生成新干员前以稳定 `ownerKey` 调用显式清理入口。
- 防御塔尸体在当前场景永久保留，比赛结束不清理，场景卸载时由 Unity 清理。
- 不实现撤退、复活、再部署倒计时、重新生成、对象池或新的视觉效果。
- 保持现有 `DeathCorpsePresenter.Configure(...)` 重载兼容，未传类别时默认 `UnitKind.Operator`。
- 场景构建前确认交互式 Unity 已关闭；测试带 `-quit` 未生成有效 XML 时去掉 `-quit` 重跑，结论以 XML 为准。
- 保留并不暂存 Packages、ProjectSettings、`.superpowers/brainstorm/`、日志、无关文档和已有无关工作区改动；文本文件只暂存本功能 hunks。
- `Assets/Game/Scenes/PrototypeArena.unity` 仅在 Task 3 按已确认规格由构建器重建后暂存。
- 人工 Play Mode 验收未实际执行前，只能报告“待验收”，不得称为通过。

## Review Focus

- `ClearOperatorCorpse(null)`、空字符串和未知 key 必须安全无操作，且不能误删其他干员尸体；Task 1 测试固定此行为。
- 同一个 `ownerKey` 连续产生尸体时只能留下最新对象，旧对象的 `OnDestroy` 不能反向移除新登记；Task 1 测试固定替换与登记身份检查。
- `CorpseLifetimeController.Configure(...)` 被重复调用或对象被外部销毁时不能留下静态失效引用；Task 1 测试固定重配和销毁后的清理行为。
- 小兵尸体的 5 秒必须从创建时开始，即使 `CorpseFallController` 尚未落地；Task 1 的 presenter 集成测试和 Task 2 的真实 PlayMode 测试共同固定此行为。
- 场景重载后静态干员登记不能指向上一场景的已销毁对象；Task 3 场景卸载/重载测试固定此行为。

---

### Task 1: 尸体生命周期核心与死亡表现接入

**Files:**
- Create: `Assets/Game/Scripts/Combat/CorpseLifetimeController.cs`
- Create: `Assets/Game/Scripts/Combat/CorpseLifetimeController.cs.meta`
- Create: `Assets/Tests/EditMode/CorpseLifetimeControllerTests.cs`
- Create: `Assets/Tests/EditMode/CorpseLifetimeControllerTests.cs.meta`
- Modify: `Assets/Game/Scripts/Combat/DeathCorpsePresenter.cs`
- Modify: `Assets/Tests/EditMode/DeathCorpsePresenterTests.cs`

**Interfaces:**
- Consumes: `ArknightsFrontline.Common.UnitKind`；现有 `DeathCorpsePresenter.Configure(CombatUnit, Material, int)` 与 `Configure(CombatUnit, Material, int, Vector3)`。
- Produces: `CorpseLifetimeController.MinionLifetimeSeconds = 5f`；`Configure(UnitKind unitKind, string ownerKey)`；`Tick(float deltaTime)`；`static ClearOperatorCorpse(string ownerKey)`；只读 `UnitKind`、`OwnerKey`；`DeathCorpsePresenter.UnitKind`；新增带 `UnitKind` 的 4 参数和 5 参数配置重载。

- [ ] **Step 1: 记录实施前基线与工作区边界**

确认分支、HEAD、暂存区和现有修改，并保存输出。关闭所有交互式 Unity 后，用以下编辑器执行一次完整 PlayMode，作为最终比较基线：

```powershell
git branch --show-current
git rev-parse HEAD
git status --short
& 'C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'D:\arknights-frontline' -runTests -testPlatform PlayMode -testResults 'D:\arknights-frontline\TestResults\corpse-lifetime-baseline-playmode.xml' -logFile 'D:\arknights-frontline\TestResults\corpse-lifetime-baseline-playmode.log' -quit
```

Expected: `dev`；XML 可解析并记录总数、通过数和全部非通过项。若没有有效 XML，原样去掉 `-quit` 重跑。不得把 TestResults 或日志加入提交。

- [ ] **Step 2: 写核心控制器的失败测试**

在 `CorpseLifetimeControllerTests` 中用 `DestroyImmediate` 清理每个测试对象，覆盖以下测试与断言：

```csharp
MinionExistsBeforeFiveSecondsAndIsDestroyedAtFiveSeconds()
// Tick(4.99f): corpse != null; Tick(0.01f): corpse == null

NegativeDeltaDoesNotAdvanceMinionLifetime()
// Tick(-10f), Tick(4.99f): still exists; Tick(0.01f): destroyed

OperatorPersistsUntilMatchingOwnerKeyIsCleared()
// Tick(60f): exists; wrong/null/empty key: exists; matching key: destroyed; repeat: no throw

RegisteringSameOperatorKeyReplacesOnlyTheOlderCorpse()
// first == null; second != null; clearing key destroys second

ReconfiguringOrDestroyingOperatorRemovesOnlyItsOwnRegistration()
// reconfigure away from old key, or DestroyImmediate; clearing stale key does not affect another corpse

TowerPersistsWithoutRegistryEntryOrTimer()
// Tick(60f): exists; ClearOperatorCorpse(ownerKey): exists
```

`Configure(UnitKind.Operator, ownerKey)` 对 `null` 或空 `ownerKey` 抛 `ArgumentException`；`ClearOperatorCorpse` 对同样输入无操作。

- [ ] **Step 3: 运行聚焦测试并确认 RED**

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'D:\arknights-frontline' -runTests -testPlatform EditMode -testFilter 'ArknightsFrontline.Tests.EditMode.CorpseLifetimeControllerTests' -testResults 'D:\arknights-frontline\TestResults\corpse-lifetime-task1-red.xml' -logFile 'D:\arknights-frontline\TestResults\corpse-lifetime-task1-red.log' -quit
```

Expected: FAIL/编译失败，因为 `CorpseLifetimeController` 尚不存在。确认失败原因与缺失接口一致；无有效 XML 时去掉 `-quit` 重跑。

- [ ] **Step 4: 实现最小 `CorpseLifetimeController`**

在 `ArknightsFrontline.Combat` 命名空间实现：

```csharp
public const float MinionLifetimeSeconds = 5f;
public UnitKind UnitKind { get; private set; }
public string OwnerKey { get; private set; }
public void Configure(UnitKind unitKind, string ownerKey)
public void Tick(float deltaTime)
public static void ClearOperatorCorpse(string ownerKey)
```

使用 `Dictionary<string, CorpseLifetimeController>` 保存干员登记。`Update()` 仅调用 `Tick(Time.deltaTime)`；小兵累计 `Mathf.Max(0f, deltaTime)` 并在 `>= 5f` 销毁，干员和塔不计时。重配前先按引用身份解除旧登记；同 key 新登记先销毁旧尸体再写入新引用；`OnDestroy()` 仅在字典当前值仍为自身时移除。沿用项目规则：PlayMode 使用 `Destroy`，EditMode 使用 `DestroyImmediate`。

- [ ] **Step 5: 运行核心测试并确认 GREEN**

重复 Step 3 命令，结果写入 `corpse-lifetime-task1-core-green.xml`。

Expected: `CorpseLifetimeControllerTests` 全部 PASS，XML 无 ignored/failed/error。

- [ ] **Step 6: 写 presenter 接入的失败测试**

在 `DeathCorpsePresenterTests` 增加：

```csharp
ConfiguredUnitKindIsCopiedToCreatedCorpseLifetime()
// 以 UnitKind.Minion 配置、击杀后：corpse lifetime != null，UnitKind == Minion，OwnerKey == source.name

AirMinionLifetimeStartsBeforeFallCompletes()
// 击杀空中小兵，不推进 CorpseFallController；lifetime.Tick(5f) 后 corpse == null

LegacyConfigureOverloadsDefaultToOperator()
// 3 参数和 Vector3 旧重载的 presenter.UnitKind == Operator；生成尸体的 controller 也为 Operator
```

- [ ] **Step 7: 运行 presenter 聚焦测试并确认 RED**

运行 `ArknightsFrontline.Tests.EditMode.DeathCorpsePresenterTests`。

Expected: 新测试因 `UnitKind` 配置接口或尸体控制器接线缺失而 FAIL；既有测试仍通过。

- [ ] **Step 8: 最小扩展 `DeathCorpsePresenter`**

新增 `[SerializeField] private UnitKind unitKind = UnitKind.Operator;` 和 `public UnitKind UnitKind => unitKind;`。保留两个旧重载并委托到以下新入口：

```csharp
public void Configure(CombatUnit combatUnit, Material corpseMaterial, int groundLayer, UnitKind unitKind)
public void Configure(CombatUnit combatUnit, Material corpseMaterial, int groundLayer, Vector3 corpseScale, UnitKind unitKind)
```

旧重载必须显式传 `UnitKind.Operator`。`CreateCorpse()` 在返回前添加 `CorpseLifetimeController`，调用 `Configure(unitKind, gameObject.name)`；确保控制器在空中坠落组件开始工作前已经存在。

- [ ] **Step 9: 运行 Task 1 全部聚焦测试并确认 GREEN**

过滤器：

```text
ArknightsFrontline.Tests.EditMode.CorpseLifetimeControllerTests|ArknightsFrontline.Tests.EditMode.DeathCorpsePresenterTests
```

Expected: 全部 PASS；既有材质、缩放、落点、碰撞体和 EditMode 立即销毁断言不回归。

- [ ] **Step 10: 独立复审并提交 Task 1**

复审重点：静态登记身份检查、Unity fake-null、重复配置、EditMode/PlayMode 销毁差异、旧重载兼容。修正后重跑 Step 9。只暂存上述六个文件；若已有文件含无关修改，使用 `git add -p` 只暂存本任务 hunks。

```powershell
git diff --check -- Assets/Game/Scripts/Combat/CorpseLifetimeController.cs Assets/Game/Scripts/Combat/DeathCorpsePresenter.cs Assets/Tests/EditMode/CorpseLifetimeControllerTests.cs Assets/Tests/EditMode/DeathCorpsePresenterTests.cs
git commit -m 'feat: add corpse lifetime policies'
```

---

### Task 2: 小兵生成器接线与真实运行时计时

**Files:**
- Modify: `Assets/Game/Scripts/Arena/MinionWaveSpawner.cs`
- Modify: `Assets/Tests/EditMode/MinionWaveSpawnerTests.cs`
- Modify: `Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs`

**Interfaces:**
- Consumes: Task 1 的 `DeathCorpsePresenter.Configure(CombatUnit, Material, int, UnitKind)`、`CorpseLifetimeController.UnitKind` 和 5 秒自动计时。
- Produces: 所有由 `MinionWaveSpawner` 生成的地面/空中小兵都明确配置为 `UnitKind.Minion`；真实 PlayMode 中从死亡创建起自动消失。

- [ ] **Step 1: 写生成器接线的失败 EditMode 测试**

扩展 `SpawnWaveNowCreatesFourMinionsPerSideWithRequiredComponents`，对所有八个生成单位断言：

```csharp
DeathCorpsePresenter presenter = minion.GetComponent<DeathCorpsePresenter>();
Assert.That(presenter, Is.Not.Null);
Assert.That(presenter.UnitKind, Is.EqualTo(UnitKind.Minion));
```

- [ ] **Step 2: 运行单测并确认 RED**

运行该单个测试。

Expected: FAIL，实际类别为旧重载默认的 `Operator`。

- [ ] **Step 3: 写真实 PlayMode 生命周期失败测试**

在 `DeathCorpsePresentationPlayModeTests` 新增 `SpawnedGroundAndAirMinionCorpsesExpireFiveSecondsAfterCreation()`：生成一波，击杀一个地面兵和一个空中兵，等待一帧取得两个 corpse；约 0.35 秒时断言空中尸体已落地且两个尸体仍存在；从死亡起不足 5 秒时仍存在；超过 5 秒后两个都为 Unity null。不得手动调用 `Tick`，以验证 `Update()` 接线和空中计时包含坠落时间。

- [ ] **Step 4: 运行 PlayMode 测试并确认 RED**

运行新增单测，确认因小兵仍被配置为 `Operator` 而超过 5 秒仍存在：

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'D:\arknights-frontline' -runTests -testPlatform PlayMode -testFilter 'ArknightsFrontline.Tests.PlayMode.DeathCorpsePresentationPlayModeTests' -testResults 'D:\arknights-frontline\TestResults\corpse-lifetime-task2-playmode.xml' -logFile 'D:\arknights-frontline\TestResults\corpse-lifetime-task2-playmode.log' -quit
```

Expected: FAIL，两个尸体超过 5 秒仍存在；XML 有效。

- [ ] **Step 5: 最小修改小兵生成器并确认 GREEN**

把 `SpawnMinion(...)` 内 presenter 配置改为带 `UnitKind.Minion` 的重载；不改变生成数量、属性、移动或攻击配置。重跑整个 `MinionWaveSpawnerTests` 和整个 `DeathCorpsePresentationPlayModeTests`。

Expected: 两个测试类全部 PASS；PlayMode 失败时区分行为错误与等待时间容差，不用放宽 5 秒规则掩盖问题。

- [ ] **Step 6: 独立复审并提交 Task 2**

复审重点：地面/空中统一接线、真实时间断言稳定性、现有 fixture 清理不泄漏对象。只暂存本任务 hunks。

```powershell
git diff --check -- Assets/Game/Scripts/Arena/MinionWaveSpawner.cs Assets/Tests/EditMode/MinionWaveSpawnerTests.cs Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs
git commit -m 'feat: expire minion corpses after five seconds'
```

---

### Task 3: 确定性场景接线、重建与场景生命周期测试

**Files:**
- Modify: `Assets/Game/Editor/PrototypeSceneBuilder.cs`
- Modify: `Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs`
- Regenerate: `Assets/Game/Scenes/PrototypeArena.unity`

**Interfaces:**
- Consumes: Task 1 的 presenter 类别重载、`CorpseLifetimeController.Tick(float)` 与 `ClearOperatorCorpse(string)`；Task 2 的小兵生成器接线。
- Produces: 保存场景中的玩家/训练干员为 `Operator`、双方塔为 `Tower`；场景卸载后静态登记可安全复用。

- [ ] **Step 1: 写保存场景接线的失败测试**

扩展 `PrototypeArenaContainsRequiredRoots`：玩家和 `TrainingDummy_Red` 的 `DeathCorpsePresenter.UnitKind == Operator`；两座塔的 `UnitKind == Tower`。扩展死亡测试：

```csharp
SavedOutcomeControllerResolvesDestroyedTower()
// 先击杀一个已生成小兵，再击杀塔触发结算；小兵尸体不会在结算时立即清理，
// 从自身创建起超过 5 秒后消失；tower corpse lifetime.UnitKind == Tower 且仍存在

SavedPlayerDeathCreatesGroundedCorpseWithoutRuntimeConfigure()
SavedEnemyOperatorDeathCreatesGroundedCorpse()
// lifetime.UnitKind == Operator；Tick(60f) 后仍存在
```

新增 `OperatorRegistryDoesNotRetainCorpseAcrossSceneReload()`：加载场景、击杀玩家、卸载场景、重新加载，再调用 `ClearOperatorCorpse("Player_Exusiai")`；断言无异常且新场景中的玩家仍存在。

- [ ] **Step 2: 运行场景测试并确认 RED**

运行 `ArknightsFrontline.Tests.PlayMode.ArenaSceneSmokeTests`。

Expected: 保存场景中的旧 presenter 配置仍默认为 `Operator`，至少塔类别断言失败；尚未重建前不得修改测试期望。

- [ ] **Step 3: 最小修改场景构建器**

- `CreateTower(...)` 调用 5 参数缩放重载并传 `UnitKind.Tower`，保留 `(0.3f, 1f, 0.3f)`。
- `CreatePlayer(...)` 与 `CreateTrainingDummy(...)` 调用类别重载并显式传 `UnitKind.Operator`。
- 不改变单位数值、材质、位置、碰撞、技能或比赛结束逻辑。

- [ ] **Step 4: 关闭交互式 Unity，确定性重建并核对场景 diff**

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'D:\arknights-frontline' -executeMethod ArknightsFrontline.Editor.PrototypeSceneBuilder.Build -logFile 'D:\arknights-frontline\build-corpse-lifetime-scene.log' -quit
```

Expected: 退出码 0；日志包含场景保存完成且无编译异常；`PrototypeArena.unity` 仅出现新脚本引用/类别序列化以及构建器当前确定性输出所需变化。确认 Packages、ProjectSettings 和其他用户文件未被暂存。

- [ ] **Step 5: 重跑场景测试并确认 GREEN**

重复 Step 2，结果写入 `corpse-lifetime-task3-scene-playmode.xml`。

Expected: `ArenaSceneSmokeTests` 全部 PASS，包括塔比赛结束后超过 5 秒仍存在、干员长期存在、卸载/重载静态登记安全。

- [ ] **Step 6: 独立复审并提交 Task 3**

复审重点：类别序列化、场景重建确定性、比赛结束不触发额外清理、场景卸载清除静态引用、未夹带 builder 的既有无关修改。文本文件使用交互式分块暂存；生成场景作为已批准产物整体暂存。

```powershell
git diff --check -- Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs Assets/Game/Scenes/PrototypeArena.unity
git commit -m 'feat: wire corpse lifetimes into prototype arena'
```

---

### Task 4: 全量验证、基线比较与人工验收交接

**Files:**
- Verify only: all files changed in Tasks 1-3
- Do not commit: `TestResults/**`、`*.log`、Packages、ProjectSettings、`.superpowers/brainstorm/**` 和其他用户文件

**Interfaces:**
- Consumes: Tasks 1-3 的全部行为与保存场景。
- Produces: 可复核的 EditMode/PlayMode XML 结果、完整 PlayMode 基线差异、待执行人工验收清单。

- [ ] **Step 1: 运行聚焦 EditMode 与完整 EditMode**

先运行过滤器：

```text
ArknightsFrontline.Tests.EditMode.CorpseLifetimeControllerTests|ArknightsFrontline.Tests.EditMode.DeathCorpsePresenterTests|ArknightsFrontline.Tests.EditMode.MinionWaveSpawnerTests
```

再不带 `-testFilter` 运行完整 EditMode，分别输出 `corpse-lifetime-focused-editmode.xml` 与 `corpse-lifetime-full-editmode.xml`。

Expected: 聚焦测试全部 PASS；完整结果逐项记录总数和全部非通过项。若有既有失败，不能写成通过。

- [ ] **Step 2: 运行计划内聚焦 PlayMode**

过滤器：

```text
ArknightsFrontline.Tests.PlayMode.DeathCorpsePresentationPlayModeTests|ArknightsFrontline.Tests.PlayMode.ArenaSceneSmokeTests|ArknightsFrontline.Tests.PlayMode.MinionLanePlayModeTests
```

Expected: 聚焦测试全部 PASS；尸体原有落地、材质、缩放、碰撞体移除和血条清理行为继续成立。

- [ ] **Step 3: 运行一次完整 PlayMode 并与基线比较**

不带 `-testFilter` 运行完整 PlayMode，输出 `corpse-lifetime-full-playmode.xml`。从 XML 列出所有 failed/error/skipped/inconclusive 项，与 Task 1 Step 1 的基线按测试全名比较，标注“既有”“已消失”或“新增”。不得只引用 Unity 退出码或日志摘要。

- [ ] **Step 4: 最终独立全分支复审与工作区审计**

审查规格逐条覆盖、所有提交 diff、生成场景、测试 XML 与 `git status --short`。确认提交只含本功能代码、测试、计划/规格和已批准的生成场景；任何 Task 1-3 修正都在对应测试后单独提交，复审通过后才结束。

- [ ] **Step 5: 交付人工验收清单，不宣称已通过**

请用户在 Unity 6000.6.2f1 打开 `PrototypeArena` 并进入 Play Mode：

1. 击杀一个地面小兵，确认尸体立即出现、约 5 秒时直接消失。
2. 击杀一个空中小兵，确认先在约 0.3 秒内落地，且总寿命仍从死亡时起约 5 秒，不是落地后再等 5 秒。
3. 击杀 `Player_Exusiai` 或 `TrainingDummy_Red`，等待超过 5 秒，确认尸体仍保留；当前 demo 尚无再部署流程，显式清理接口只由自动测试验证。
4. 击杀一座防御塔触发比赛结束，等待超过 5 秒，确认塔尸体永久留在当前场景。
5. 退出并重新进入 Play Mode，确认上一场景尸体全部随场景卸载清除，且 Console 没有 MissingReference/NullReference 错误。

最终报告必须包含：Task 1-3 提交、各轮 XML 计数、完整 PlayMode 全部未通过项及相对基线分类、场景构建结果，以及上述人工验收仍待用户执行。
