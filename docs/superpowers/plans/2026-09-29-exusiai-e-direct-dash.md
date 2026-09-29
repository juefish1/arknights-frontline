# 能天使 E 直接冲刺后连射 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让玩家按 E 后只需左键指定落点，快速冲刺到实际落点，再从该处攻击范围内选敌执行一次连射。

**Architecture:** 保留 `SkillDashController` 的路径解析、距离限制和完成事件；`ExusiaiSkillController` 在左键确认时启动冲刺并锁定连射参数，在 `DashCompleted` 回调中选敌并启动现有 `AttackSequenceExecutor`。指示器改为选择阶段预览冲刺落点，HUD 移除旧的第二段输入提示。

**Tech Stack:** Unity 6000.6.2f1、C#、NUnit EditMode/PlayMode、PowerShell、Git `dev` 分支。

**Spec:** `docs/superpowers/specs/2026-09-29-exusiai-e-direct-dash-design.md`；技能原始文本/Rank III 数值在 `docs/operator-profile/能天使.md`。

## Global Constraints

- 直接在 `D:\arknights-frontline` 的 `dev` 分支实施；不建 worktree。
- 子代理模型最多 `gpt-6-luna`、推理强度最多 `xhigh`；实施、独立复审、TDD。
- 只动本功能代码、测试、设计/计划/执行记录；保留用户拥有的 Packages、ProjectSettings、场景、材质、日志、未跟踪文档及已有脏改动，不覆盖或暂存无关内容。
- E 最大冲刺距离 7 米、速度 28 米/秒、E 冷却 20 秒；成功冲刺后才从真实落点选最近合法敌人。Rank III 每发 125% 攻击力、命中减速 30% 持续 2 秒，普通 4 发，确认时 R 生效则 5 发，发间隔 0.05 秒。
- 旧的「攻击点确认后 0.25 秒右键移动」流程和 `E MOVE!` 收缩条全部废止；有效冲刺启动即消耗 E。视觉残影最长 0.15 秒。
- Unity 可执行文件：`C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe`。测试结果以 XML 为准；若 `-quit` 不生成有效 XML，去掉 `-quit` 重试。
- 不重建 `PrototypeArena.unity`：本次只改已挂载组件逻辑，无场景序列化字段/构建器调整。若代码验证证明场景必须变更，先核对场景已有用户改动再决定。

## Review Focus

- 用户点到敌人碰撞体而非地面：测试必须证明取光标对应地面落点、不会预锁该敌人，且落点后仍按最近合法敌人选择。
- 位移不足 0.05 米/无命中：测试必须证明仍在选择态且不消耗冷却。
- 目标在冲刺中死亡、移出范围或有更近敌人：测试必须证明到达后才按当前世界状态选择。
- 冲刺中停止、死亡或比赛结束：测试必须证明不会在以后回调补射；冷却及冻结 HUD 正确。
- R 在冲刺中开启/结束：测试必须证明本次发数与攻击力按冲刺确认时锁定。

---

### Task 1: 冲刺速度

**Files:**
- Modify: `Assets/Game/Scripts/Skills/SkillDashController.cs`
- Test: `Assets/Tests/EditMode/SkillDashControllerTests.cs`

**Interfaces:**
- Produces: `SkillDashController.Tick(float deltaTime)` 按 28 米/秒前进；`DashCompleted` 仅到终点触发一次，`Preview(Vector3,out Vector3)` 与 `TryStart(Vector3)` 仍共享路径解析。

- [ ] **Step 1: 先写失败测试。** 将现有 14 米/秒断言更新为 28 米/秒；增加 `DashTravelsSevenMetersInQuarterSecondAndCompletesOnce`：启动 7 米冲刺，`Tick(0.125f)` 位移 3.5 米且未完成，再 `Tick(0.125f)` 恰在 7 米落点且完成事件一次，继续 Tick 不重复。
- [ ] **Step 2: 运行 `SkillDashControllerTests`，确认因旧速度而 RED。** 使用上述 Unity 版本批处理 EditMode，结果写入 `TestResults/exusiai-e-speed-red.xml`，只按 XML 判定。
- [ ] **Step 3: 将 `DashSpeed` 改为 `28f`，不改路径解析/最大距离。**
- [ ] **Step 4: 重跑 `SkillDashControllerTests` 并确认全绿。** 结果写入 `TestResults/exusiai-e-speed-green.xml`。
- [ ] **Step 5: 只暂存本任务两个文件并提交。**

### Task 2: 单次落点确认、到达后选敌连射与生命周期

**Files:**
- Modify: `Assets/Game/Scripts/Skills/ExusiaiSkillController.cs`
- Test: `Assets/Tests/EditMode/ExusiaiSkillControllerTests.cs`

**Interfaces:**
- Consumes: `SkillDashController.TryStart(Vector3)`、`DashCompleted`、`IsDashing`、`TargetSelector.FindNearestInRange(CombatUnit)`、`AttackSequenceExecutor.TryStart(AttackSequencePlan,CombatUnit)`。
- Produces: `TryConfirmCharge(Vector3 point, CombatUnit directTarget)` 仅使用 point 作为冲刺请求、忽略 directTarget；`TryHandleConfirm(Vector3,GameObject)` 消费 E 的左键；`TryHandleMoveClick(Vector3)` 不执行第二段 E；`SelectedChargeTarget` 只在到达后才赋值。保留现有公开 API，删除 `DashWindow` 状态及倒计时字段仅在相关使用和测试全部迁移后进行。

- [ ] **Step 1: 先写/改失败 EditMode 测试。** `ConfirmStartsDashWithoutFiringOrPreselectingTarget` 验证有效确认后立即 `IsDashing`、冷却为 20、伤害为零、`SelectedChargeTarget=null`；`ArrivalChoosesNearestLegalEnemyFromActualEndpoint` 验证冲刺中敌人重新分布后只选落点附近最近敌人并执行 4 发、每发 125%、30% 减速；`ArrivalWithNoTargetSpendsCooldownWithoutVolley`；`InvalidEndpointKeepsTargetingAndCooldownReady`；`OverloadAtConfirmSnapshotsFiveShotsAcrossDash`；`StopDeathSettlementAndDisablePreventDeferredVolley`；`RightClickCancelsSelectionAndCannotRedirectStartedDash`。删除/改写只验证旧第二段窗口的断言。测试要观察真实伤害、状态和位移，不只观察 mock 回调。
- [ ] **Step 2: 运行 `ExusiaiSkillControllerTests`，确认新行为至少一项因旧实现而 RED；记录失败名。** 写入 `TestResults/exusiai-e-controller-red.xml`。
- [ ] **Step 3: 最小实现。** 有效落点先由 `dash.TryStart(point)` 验证；成功后清旧攻击/命令，启动冷却并锁定 `AttackSequencePlan` 的发数/攻击力，订阅且在 `DashCompleted` 到达时调用 `TargetSelector.FindNearestInRange(owner)` 再 `TryStart(plan,target)`；无目标则结束本次待发计划。所有清理路径取消待发计划与事件订阅；不可冲刺点保持选择。取消旧第二段输入，更新快照/阶段及普通命令阻断逻辑。确保 `Tick` 中不会提前开火。
- [ ] **Step 4: 重跑 `ExusiaiSkillControllerTests` 并确认全绿。** 结果写入 `TestResults/exusiai-e-controller-green.xml`；PlayMode 旧流程断言由 Task 3 迁移。
- [ ] **Step 5: 只暂存本任务功能代码与 EditMode 测试；检查 `git diff --cached` 无用户改动后提交。**

### Task 3: 落点预览与 HUD

**Files:**
- Modify: `Assets/Game/Scripts/Skills/ExusiaiSkillIndicator.cs`
- Modify: `Assets/Game/Scripts/Skills/SkillHudPresenter.cs`
- Test: `Assets/Tests/EditMode/ExusiaiSkillIndicatorTests.cs`
- Test: `Assets/Tests/EditMode/SkillHudPresenterTests.cs`

**Interfaces:**
- Consumes: Task 2 的 `IsSelectingChargeTarget`、`SelectedChargeTarget` 和 E 快照阶段；`SkillDashController.IsDashing`、`Preview`、`Destination`。
- Produces: E 选择态 `RangeRadius=7f`、`DisplayedEndpoint` 为解析后真实落点、箭头预览；HUD `E  SELECT DEST` 且不创建旧 `DashWindowPromptTrack`；冲刺后最多 0.15 秒残影。

- [ ] **Step 1: 先写失败表现测试。** `TargetingPreviewsResolvedDashEndpointAndSevenMeterRange`、`TargetingDoesNotLockClickedEnemyBeforeArrival`、`InvalidPreviewIsRedAndDoesNotEnableDash`、`ConfirmedDashShowsPathThenFadesInPointOneFiveSeconds`、`HudPromptsForDestinationWithoutSecondClickBar`。
- [ ] **Step 2: 跑 `ExusiaiSkillIndicatorTests,SkillHudPresenterTests`，确认因旧半径/文案/提示条而 RED。** 写入 `TestResults/exusiai-e-ui-red.xml`。
- [ ] **Step 3: 最小实现。** 将预览提前到选择态；确认后的冲刺路径不依赖光标继续移动；取消旧 DashWindow 模式、锁定环和收缩条，保留必要的箭头残影及停用清理。
- [ ] **Step 4: 跑聚焦 EditMode 到绿。** 结果写入 `TestResults/exusiai-e-ui-green.xml`。
- [ ] **Step 5: 只暂存本任务代码与 EditMode 测试；检查 staged diff 后提交。** 不提交用户脏文件/日志/Unity 生成资产。

### Task 4: 输入、场景生命周期与完整验证

**Files:**
- Modify: `Assets/Game/Scripts/Skills/ExusiaiSkillController.cs`（仅旧窗口兼容 API 清理或测试发现的真实缺口）
- Modify: `Assets/Game/Scripts/Commands/CommandFeedbackPresenter.cs`（仅当聚焦测试表明需要）
- Test: `Assets/Tests/EditMode/MatchOutcomeControllerTests.cs`
- Test: `Assets/Tests/PlayMode/ExusiaiSkillsPlayModeTests.cs`
- Test: `Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs`（已有用户脏改动，只改/暂存 E 相关 hunk）
- Test: 其他完整套件指出的本功能相关旧 E 断言（先查明具体测试名）。

**Interfaces:**
- Consumes: Task 2 的单次左键冲刺/到达选敌/冷却语义；Task 3 的 7 米落点预览、`E  SELECT DEST` 文案与冲刺残影。
- Produces: 真实输入和 `PrototypeArena.unity` 场景测试均遵循单次左键 E；无旧右键第二段，生命周期和完整套件可验证。

- [ ] **Step 1: 先迁移旧 E 输入/场景/比赛结束测试。** `MatchOutcomeControllerTests` 应从旧 `TryConsumeDashMove` 改为有效落点左键确认后结束比赛；PlayMode 以真实 `PlayerCommandController` 输入验证一次左键启动、到达才发射、从落点选敌、右键只取消选择不改写已启动冲刺、HUD/指示器状态。只删除确实仅验证旧窗口的断言，不降低行为覆盖。
- [ ] **Step 2: 运行聚焦 EditMode 与 PlayMode 并确认旧行为或剩余代码缺口的 RED。** 结果写入 `TestResults/exusiai-e-input-red.xml`；必要时只对真实缺口做最小实现。
- [ ] **Step 3: 跑 `MatchOutcomeControllerTests`、`ExusiaiSkillsPlayModeTests`、E 相关 `PlayerCommandInputPlayModeTests` 到绿。** 结果写入 `TestResults/exusiai-e-input-editmode-green.xml` 与 `TestResults/exusiai-e-playmode-focused.xml`。
- [ ] **Step 4: 跑完整 EditMode 与一次完整 PlayMode。** 分别写入 `TestResults/exusiai-e-editmode-full.xml`、`TestResults/exusiai-e-playmode-full.xml`，以 XML 统计通过/失败/跳过；完整 PlayMode 未通过项逐名与阶段前 60/60 基线比较，不掩盖失败。修复本功能回归后重复相关验证，完成独立全局复审。
- [ ] **Step 5: 只暂存本任务代码/测试及本设计、计划；核对 staged diff 无用户改动后提交。** 不提交日志、Packages、ProjectSettings、场景或材质。

## 手工验收（不计自动通过）

用户在 Unity 编辑器 `PrototypeArena.unity` 中按 E、左键远近不同落点，确认冲刺速度明显快于普通移动、箭头指向实际截停点、到达后才连射、目标从落点附近选、无目标时只冲刺、R 联动为 5 发、右键取消选择且不再要求第二次点击。人工未执行前报告为「待验收」。
