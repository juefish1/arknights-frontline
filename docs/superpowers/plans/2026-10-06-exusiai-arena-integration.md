# Exusiai Arena Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让当前 PrototypeArena 中可操控的玩家能天使使用正式模型、Vector 武器与玩法驱动的动作，并正确跨越死亡、撤退和重新部署。

**Architecture:** 保留胶囊玩法根，把正式模型挂为视觉子节点。玩法程序集中的适配组件读取位移及实际出弹事件，驱动独立模型模块；用增量场景工具保存引用，并为真实枪口提供可选投射物起点。

**Tech Stack:** Unity 6000.3.25f1、URP、C#、Animator/Humanoid、现有 NUnit EditMode/PlayMode 测试及模型独立验收入口。

**Spec:** `docs/superpowers/specs/2026-10-06-exusiai-arena-integration-design.md`。复用资源约定：`docs/superpowers/specs/2026-10-05-exusiai-animation-secondary-motion-design.md`。

## Global Constraints

- 用户已确认仅接入可操控的玩家能天使；红方 AI 能天使及其余四席保持原有模型和表现。
- 场景升级只选择 `isPlayerControlled=true && operatorType=Exusiai` 的唯一席位；Builder 只在 CreatePlayer 路径安装模型，计算机干员模板路径不接入。
- 动画原地播放，Animator.applyRootMotion=false；第一枪不能因抬枪动画推迟。
- 攻击数值、射速、伤害结算、攻击范围、投射物速度、技能冷却和重新部署规则保持原有约定。
- 校准可见待机高度 2.4 ± 0.02 m、最低点距地面 ≤0.02 m；玩法根中心高度 1.2 m，统一缩放 1.2，保留根 CapsuleCollider。
- 不修改主 FBX、蒙皮、骨骼父子关系或共享配置资源；保留原腿骨同步和十四条次级运动链。
- 当前保存场景采用增量升级，不调用 `PrototypeSceneBuilder.Build()`；安装器可重复运行。
- 不改用户未提交的 `Assets/Game/Rendering/PrototypeURP.asset`、`ProjectSettings/EditorBuildSettings.asset`、`ProjectSettings/GraphicsSettings.asset`；实施前备份并记录 SHA256。
- 不增加包依赖；保持模型程序集不依赖玩法程序集。每个新 Unity 文件提交对应 `.meta`。

## Review Focus

- 玩家多弹序列与按轮攻击通知不能混用：只绑定逐弹事件，不重复反馈或增加实际弹数。Task 2 的事件与技能测试覆盖。
- 第一次开火、E 冲刺后立即开火：即使没有普通攻击目标或预瞄准也不丢反馈、不延迟伤害。Task 2 和 Task 4 覆盖。
- 模型在 1.2 倍玩法根下再次放大：脚步率和碰撞半径按有效世界缩放变化，根没有双重位移。Task 1、2、3 覆盖。
- 死亡/撤退再部署、暂停和传送：引用属于新实例，物理缓存不跨角色共享，没有暂停后的巨大速度。Task 2、3、5 覆盖。
- 保存场景存在用户配置：重复升级仅安装玩家模型，红方 AI 能天使及其余四席不安装模型、适配器或枪口组件，原场景引用和设置可回归。Task 5 覆盖。

---

## 文件结构与职责

| 文件 | 职责 |
| --- | --- |
| `Assets/Game/Editor/ExusiaiArenaSceneTools.cs` | 临时单位安装、统一校准、增量升级当前场景 |
| `Assets/Game/Scripts/Combat/ExusiaiCombatPresentation.cs` | 玩法状态、逐弹反馈与模型表现之间的适配 |
| `Assets/Game/Scripts/Combat/ProjectileSpawnPoint.cs` | 可选枪口起点，无模型时回退 |
| `Assets/Game/Scripts/Combat/Projectile.cs` | 初始化采用可选起点；结算逻辑不变 |
| `Assets/Game/Scripts/Combat/HealthBarPresenter.cs` | 以启用的可见网格定位血条 |
| `Assets/Game/Characters/Exusiai/Model/Scripts/ExusiaiPresentation.cs` | 只补执行顺序；保留现有动作接口 |
| `Assets/Game/Characters/Exusiai/Model/Scripts/ExusiaiSecondaryMotion.cs` | 按统一世界缩放处理长度相关模拟值 |
| `Assets/Game/Editor/PrototypeSceneBuilder.cs` | 新建场景时复用接入工具 |
| `Assets/Game/Scenes/PrototypeArena.unity` | 保存玩家一席模板的正式模型与适配引用 |
| `Assets/Tests/EditMode/ExusiaiArenaSceneWiringTests.cs` | 校准、增量安装与保存引用验收 |
| `Assets/Tests/PlayMode/ExusiaiCombatPresentationPlayModeTests.cs` | 真实 Animator、移动/逐弹事件/生命周期验收 |
| `Assets/Tests/EditMode/ExusiaiScaledSecondaryMotionTests.cs` | 校准缩放下的实际骨链与碰撞验收 |
| `Assets/Tests/EditMode/ProjectileSpawnPointTests.cs` | 起点和回退规则 |
| `Assets/Tests/PlayMode/ExusiaiArenaIntegrationPlayModeTests.cs` | 保存场景、技能、枪口及部署回归 |

运行时和两个测试 `.asmdef` 显式增加 `ArknightsFrontline.Model.Runtime` 引用；当前编辑器工具在默认 Editor 程序集，不新增 Editor `.asmdef`。

## 验证命令约定

执行者先确认 Unity **6000.3.25f1** 的真实路径并设置 PowerShell `$UnityExe` 为其 `Editor/Unity.exe`；项目路径取当前执行 checkout。仓库的旧 `.unity-editor/6000.3.15f1` 目录不能证明新版本编辑器可运行。缺少正确编辑器时报告验证限制，不改 `ProjectVersion.txt` 或自动安装。

在没有另一个 Unity 实例占用该 checkout 时执行。以下三个变量在执行阶段设置：

```powershell
$IntegrationProject = (Get-Location).Path
$IntegrationResults = Join-Path $IntegrationProject 'TestResults/exusiai-arena-integration'
New-Item -ItemType Directory -Force -Path $IntegrationResults | Out-Null
```

按任务替换 `-testFilter` 的具体测试类：

```powershell
& $UnityExe -batchmode -projectPath $IntegrationProject -runTests -testPlatform EditMode -testFilter ArknightsFrontline.Tests.EditMode.ExusiaiArenaSceneWiringTests -testResults "$IntegrationResults/wiring.xml" -logFile "$IntegrationResults/wiring.log"
& $UnityExe -batchmode -projectPath $IntegrationProject -runTests -testPlatform PlayMode -testFilter ArknightsFrontline.Tests.PlayMode.ExusiaiCombatPresentationPlayModeTests -testResults "$IntegrationResults/presentation.xml" -logFile "$IntegrationResults/presentation.log"
```

测试不使用 `-quit`；成功标准是 XML 的 failed=0 且有实际测试用例，不仅是进程退出码。图像验收不使用 `-nographics`。

## Task 1: 正式模型挂接与可见尺寸

**Files:** Create `Assets/Game/Editor/ExusiaiArenaSceneTools.cs`、`Assets/Tests/EditMode/ExusiaiArenaSceneWiringTests.cs`；Modify `Assets/Game/Scripts/Combat/HealthBarPresenter.cs`、`Assets/Game/Scripts/ArknightsFrontline.Runtime.asmdef`、`Assets/Tests/EditMode/ArknightsFrontline.EditModeTests.asmdef`、`Assets/Tests/PlayMode/ArknightsFrontline.PlayModeTests.asmdef`。

**Interfaces:**
- Consumes: `ArenaVisualMetrics.OperatorCenterHeight`、`OperatorScale` 和正式预制体、原血条 `Configure(CombatUnit)`、`Tick()`。
- Produces: `public static GameObject ExusiaiArenaSceneTools.AttachVisual(GameObject operatorRoot)`，返回唯一 `ExusiaiVisual`。根必须有能天使 `OperatorIdentity`、`CombatUnit` 与 `PlayerCommandController`，否则在修改前抛异常；缺少资源也在修改单位前抛异常。
- Test fixture helper: `private static GameObject CreatePlayerOperator()` 创建蓝方失活测试根，配置胶囊、CombatUnit、OperatorIdentity、PlayerCommandController，中心 y=1.2、缩放=1.2；测试销毁自己的对象并恢复场景。

- [ ] **Step 1: 写失败测试 `AttachVisualUsesDeliveredPrefabAndKeepsGameplayRoot`、`AttachVisualIsIdempotent`、`VisibleBodyIsGroundedAtArenaScale`、`HealthBarIgnoresHiddenCapsule`、`AttachVisualRejectsAiOperatorWithoutMutation`。** 仅向玩家测试根安装；AI 测试根缺少 PlayerCommandController 时安装应抛错，并保持原 Renderer、子节点和组件；关键断言：

```csharp
Assert.That(visual.GetComponent<ExusiaiPresentation>(), Is.Not.Null);
Assert.That(visual.GetComponentInChildren<Animator>(true).applyRootMotion, Is.False);
Assert.That(root.GetComponent<Renderer>().enabled, Is.False);
Assert.That(root.GetComponent<CapsuleCollider>().enabled, Is.True);
Assert.That(ExusiaiArenaSceneTools.AttachVisual(root), Is.SameAs(visual));
Assert.That(bodyBounds.size.y, Is.EqualTo(2.4f).Within(0.02f));
Assert.That(bodyBounds.min.y, Is.EqualTo(0f).Within(0.02f));
Assert.That(bar.position.y, Is.GreaterThanOrEqualTo(visibleBounds.max.y + 0.34f));
```

另外把玩家隐藏胶囊故意放大；血条仍以可见子网格为准。无子网格单位仍使用旧回退。预览驱动器不能出现在返回层级。误传 AI 根时断言 `Assert.Throws<ArgumentException>(() => ExusiaiArenaSceneTools.AttachVisual(aiRoot))`，并验证没有安装模型或禁用 AI Renderer。

- [ ] **Step 2: 用上面的 EditMode 命令运行，确认新工具/引用尚缺失或新断言失败。** 测试先加入模型程序集引用，以免把程序集不可见误当作行为失败。
- [ ] **Step 3: 实现 `AttachVisual` 与血条包围盒筛选。** 先验证玩家组件与能天使身份，用 `PrefabUtility.InstantiatePrefab` 保留正式资源连接；按设计文档的待机包围盒校准，仅改外层视觉根。根材料、层、碰撞、身份、中心和移动数值保持；用 Animator 控制对象设玩家初始朝向 +X。临时校准实例/动画采样清理完整；不在真实模板上运行预览或物理循环。血条优先根可见网格，否则合并启用网格并排除线/拖尾。重复安装返回既有子节点并验证来源，遇到同名非正式对象抛错。
- [ ] **Step 4: 运行 `ExusiaiArenaSceneWiringTests` 和 `HealthBarPresenterTests`。** 两者 failed=0；确认根 CapsuleCollider 世界尺寸和队伍材质没有变化。
- [ ] **Step 5: 提交本任务列出的文件及 `.meta`。** Commit: `feat: attach Exusiai visual to operator roots`。本任务不升级保存场景。

## Task 2: 移动、逐弹与生命周期适配

**Files:** Create `Assets/Game/Scripts/Combat/ExusiaiCombatPresentation.cs`、`Assets/Tests/PlayMode/ExusiaiCombatPresentationPlayModeTests.cs`；Modify `ExusiaiArenaSceneTools.cs`、`Assets/Game/Characters/Exusiai/Model/Scripts/ExusiaiPresentation.cs`。

**Interfaces:**
- Consumes: Task 1 的 `AttachVisual`；`UnitMotor.MovementSpeed`、`BasicAttackController.CurrentTarget`、`AttackSequenceExecutor.ShotRequested`、`SkillDashController.IsDashing`、`CombatUnit.Died`、`OperatorRetreatController.IsGuiding`、`MatchOutcomeController.MatchEnding`、现有全部 ExusiaiPresentation 接口。
- Produces: `public void ExusiaiCombatPresentation.Configure(ExusiaiPresentation target, Transform visualRoot)`、`public void Tick(float deltaTime)`、`public void ResetForDeployment()`；只读属性 `public ExusiaiPresentation Presentation`、`public Transform VisualRoot` 用于接线核对。序列化这两个模型引用；依赖从同一玩法根解析，match 用 `GetComponentInParent<MatchOutcomeController>()` 从所属 ArenaBootstrap 根解析。重复 Configure 先解绑旧事件；OnEnable 重置并订阅，OnDisable/OnDestroy 解绑。

- [ ] **Step 1: 写失败 PlayMode 测试。** 实际实例化 Task 1 的正式模型，测试名与断言如下；不得用一个假的 Animator 替代：
  - `MotorDisplacementDrivesJogWithoutRootMotion`：从根向 +X 实际移动，`MoveSpeed>0`，`JogRate=clamp((实测速度/视觉世界缩放)/2.4,0.01,3)`，播放腿骨有变化；表现更新不额外改变玩法根位置。
  - `ArrivingAndPausingDoNotProduceFalseVelocity`：到达后下一帧 MoveSpeed 逐渐归零；`Tick(0)` 不写 NaN，暂停期间移位后恢复不播放巨大速度。
  - `PlayerSequenceShotsDoNotDuplicateRoundNotifications`：用玩家 executor 的单弹、多弹序列逐帧真实出弹驱动 Recoil；一轮多弹不叠加 AttackRequested 反馈，取消序列后不再开火。计数与原 executor 逐弹事件及实际投射物数量相同。
  - `MissingPlayerSequenceDisablesAdapter`：移除玩家 executor 后适配器报告缺失依赖并停止，不回退到 AI 普攻事件，不生成假开火。
  - `ChargeShotUsesEmittedTargetWithoutBasicAttackTarget`：清空普通目标后直接从 executor 开始 Charge 计划，模型朝本弹目标转向并播放后坐力。
  - `DisableEnableAndReconfigureDoNotDuplicateFeedback`：重复 Configure/启停两次后单次出弹仍为一份反馈；旧源发弹不驱动新引用。
  - `TeleportAndMatchEndClearPendingPresentation`：非冲刺大位移后无高速度残留；结束时 pending shot 清除，恢复待机，根没有动画驱动位移。

```csharp
Assert.That(animator.GetFloat("JogRate"), Is.EqualTo(expectedRate).Within(0.02f));
Assert.That(root.transform.position, Is.EqualTo(positionAfterMotorTick));
Assert.That(float.IsFinite(animator.GetFloat("JogRate")), Is.True);
Assert.That(animator.GetCurrentAnimatorStateInfo(2).IsName("Recoil"), Is.True);
Assert.That(projectilesAfter - projectilesBefore, Is.EqualTo(expectedShots));
```

- [ ] **Step 2: 运行 `ExusiaiCombatPresentationPlayModeTests`，确认缺失适配行为导致失败。** 新测试必须调用实际 `UnitMotor.Tick` / `AttackSequenceExecutor.TryStart/Tick`，不能只重复设置 Animator 参数。
- [ ] **Step 3: 实现适配组件和顺序。** 适配组件 execution order=50；ExusiaiPresentation=100。按设计里的方向优先级、暂停取样、缩放速度和传送阈值实现 `Tick`。要求玩家 executor 且只监听其逐弹事件，缺失时记录配置错误并禁用适配器；事件中先设方向再 PlayShot。末段实际位移不是 `IsMoving?MovementSpeed:0`。清理瞄准不影响战斗的 target/sequence；对局结束用 ResetPresentation 清掉 pendingShot。重复安装补齐、序列化适配组件引用。
- [ ] **Step 4: 运行本任务测试和现有 `ExusiaiSkillsPlayModeTests`、`BasicCombatPlayModeTests`。** 确认原有弹数、伤害和技能时序不变。记录第一弹最多存在原有 0.22 s 抬枪反馈差，不声称已经加入前摇。
- [ ] **Step 5: 提交本任务文件及 `.meta`。** Commit: `feat: drive Exusiai animation from combat facts`。

## Task 3: 校准缩放下的头发和裙摆

**Files:** Modify `Assets/Game/Characters/Exusiai/Model/Scripts/ExusiaiSecondaryMotion.cs`；Create `Assets/Tests/EditMode/ExusiaiScaledSecondaryMotionTests.cs`。

**Interfaces:**
- Consumes: Task 1 外层视觉根的正统一世界缩放、已有 `ExusiaiSecondaryMotion.ResetSimulation()`、`RestorePose()`、`Simulate(float)`。
- Produces: 原接口不变；在世界长度计算中使用组件所在正式视觉根 `transform.lossyScale.x`，参考值=1。不修改共享 profile。

- [ ] **Step 1: 写 `ScaledChainCollisionUsesScaledRadii`、`ScaledTeleportResetsWithoutStretching`、`DeploymentInstancesDoNotShareSimulationState`。** 用独立验证实例 A 取参考缩放1，B 取 Task 1 的校准缩放；按比例设置相同运动并对实际骨链采样，不仅测试 OutsideCapsule 函数。实例隔离测试用于玩家旧部署与新部署，不在场景中安装 AI 模型。

```csharp
Assert.That(float.IsFinite(bone.position.sqrMagnitude), Is.True);
Assert.That(Mathf.Abs(actualLength / restLength - 1f), Is.LessThanOrEqualTo(0.01f));
Assert.That(contactDistance, Is.GreaterThanOrEqualTo(effectiveRadius - 0.001f));
Assert.That(hairVertexOffset, Is.LessThanOrEqualTo(worldScale * 0.015f));
```

在30/60/120 FPS 分别覆盖跑动、急停、暂停恢复、启停、瞬移；一个实例重置不改变另一实例的状态。
- [ ] **Step 2: 运行 `ExusiaiScaledSecondaryMotionTests`，确认放大时碰撞或缩放一致性断言失败。** 碰撞样本选择裙摆实际接触腿代理的姿势，不能只采样无接触静止状态。
- [ ] **Step 3: 在真实碰撞投影中将 capsule.radius 与 spring.radius 乘世界缩放；传送距离和惯性加速度上限60也按该缩放调整。** stiffness、damping、角度、fixedStep/maxSubsteps 保持。保留所有链恢复/骨骼同步顺序；不改变骨骼位置/缩放。
- [ ] **Step 4: 跑新测试，并在独立 Unity 进程分别执行原模型验收。** EditMode 使用 `-executeMethod ExusiaiSecondaryAcceptance.Run`；真实 Play 使用 `-executeMethod ExusiaiMotionPlayCheck.Start`，不带 `-quit`，该入口会自行退出并写报告。原刘海回归另分别执行 `ExusiaiHairPlayCheck.Start30`、`Start`、`Start120`。独立入口会修改 `EditorSettings`、生成 `Assets/Motion_PlayTest.unity` 与 ArtSource 报告：在临时验证 checkout 运行，或事前备份其会写入的设置并在结束后恢复、清理新生成测试场景及 meta。ArtSource 在此 checkout 尚不存在，报告属新证据，不作为旧验证记录。原缩放1与校准缩放均有检查结果。
- [ ] **Step 5: 提交脚本、测试及 `.meta`。** Commit: `fix: scale Exusiai secondary motion for arena visuals`。

## Task 4: 从真实枪口生成投射物

**Files:** Create `Assets/Game/Scripts/Combat/ProjectileSpawnPoint.cs`、`Assets/Tests/EditMode/ProjectileSpawnPointTests.cs`；Modify `Assets/Game/Scripts/Combat/Projectile.cs`、`ExusiaiArenaSceneTools.cs`。

**Interfaces:**
- Consumes: 正式模型的唯一 `Muzzle` Transform、现有 `Projectile.Initialize` 两个重载。
- Produces: `public void ProjectileSpawnPoint.Configure(Transform muzzle)`、`public Vector3 ResolvePosition()`、只读 `public Transform Muzzle`；序列化 muzzle；找不到/非有限时返回组件所属玩法根位置。

- [ ] **Step 1: 写 `ProjectileStartsAtConfiguredMuzzle`、`MissingDestroyedOrNonfiniteMuzzleFallsBackToRoot`、`TurningAndRedeploymentResolveLiveMuzzle`。** 测试 Initialize 的真实起点，以及旋转/克隆之后的世界位置；无组件的塔/其它单位保持旧起点。

```csharp
Assert.That(Vector3.Distance(projectile.transform.position, muzzle.position), Is.LessThanOrEqualTo(0.001f));
Assert.That(projectileWithoutModel.transform.position, Is.EqualTo(attacker.transform.position));
Assert.That(clonedOrigin.ResolvePosition(), Is.EqualTo(clonedMuzzle.position));
```

- [ ] **Step 2: 运行 `ProjectileSpawnPointTests`，确认当前投射物从根位置生成而失败。**
- [ ] **Step 3: 实现组件，并仅替换 `Projectile.Initialize` 内起点赋值。** 仅玩家安装枪口组件并从 Muzzle 出弹；AI 与其它无组件单位继续从原根位置出弹，并增加此回归断言。安装器找到玩家模型的唯一 Muzzle 后配置。既不改投射物速度16，也不改 Tick 的水平追踪、命中、护甲、减速或攻击范围。
- [ ] **Step 4: 运行新测试、原 `ProjectileTests`、`BasicCombatPlayModeTests` 与 `ExusiaiSkillsPlayModeTests`。** 同时检查首弹未抬枪时照常发射且后续仍可命中。
- [ ] **Step 5: 提交文件及 `.meta`。** Commit: `feat: spawn Exusiai projectiles at weapon muzzle`。

## Task 5: 增量升级当前场景和真实对局验收

**Files:** Modify `ExusiaiArenaSceneTools.cs`、`Assets/Game/Editor/PrototypeSceneBuilder.cs`、`Assets/Game/Scenes/PrototypeArena.unity`、`Assets/Tests/EditMode/ExusiaiArenaSceneWiringTests.cs`、`Assets/Tests/EditMode/MobaViewSceneTests.cs`、`Assets/Tests/PlayMode/MobaViewPresentationPlayModeTests.cs`；Create `Assets/Tests/PlayMode/ExusiaiArenaIntegrationPlayModeTests.cs`、`docs/superpowers/plans/2026-10-06-exusiai-arena-integration-verification.md`。

**Interfaces:**
- Consumes: Tasks 1–4 的安装器、适配组件和枪口组件；原 ArenaRosterBootstrap 序列化 slots、OperatorRosterController.Slots 和 Spawn/Depart 事件。
- Produces: `public static void ExusiaiArenaSceneTools.Upgrade()`，菜单 `Arknights Frontline/Integrate Exusiai Model`，拒绝 Play 中操作；保存增量场景。`public static void UpgradeScene(Scene scene)` 提供同一增量修改路径但不保存，仅供 Upgrade 与复制场景测试使用。只选择 `isPlayerControlled=true && operatorType=Exusiai` 的唯一席位，缺少或多于一席在修改前报错。Builder 仅在 `CreatePlayer` 路径调用 `AttachVisual`，`CreateComputerOperatorTemplate` 不增加接入调用。

- [ ] **Step 1: 备份场景和三个用户改动文件到 `TestResults/exusiai-arena-integration/before/` 并记录 SHA256。** 读取当前 git status 及场景对象清单。执行阶段根据所选执行方式使用 worktree 技能；若使用隔离 checkout，额外带入并校验这3份用户设置的副本，避免丢失当前渲染环境。只提交本计划文件。
- [ ] **Step 2: 写保存场景与部署验收。** `SavedSceneIntegratesOnlyPlayerExusiai`：按 roster slot 的 `isPlayerControlled` 和 operatorType 检查唯一玩家席模型/适配/枪口非空、没有 ExusiaiPreviewDriver、引用位于玩家模板下；其余五席（含红方 AI 能天使）无正式模型、适配器和枪口组件，原 Renderer 仍启用。`UpgradeTwicePreservesExistingSceneConfiguration`：复制场景到测试临时 asset，运行两次 Upgrade，原根对象 fileID/roster/camera/HUD/材料/渲染引用保持，仅玩家一席增加模型和组件，AI 模板的原组件、变换、材料和引用保持；使用 `UpgradeScene(Scene)` 以免改真实场景。`UpgradeRejectsMissingOrMultiplePlayerExusiaiSlots`：没有或多于一个匹配席位时抛错，所有模板保持升级前状态。

真实 Play 写 `SavedSceneMovesShootsAndRedeploysPlayerModel`、`ChargeAndOverloadKeepOriginalGameplayWhileAnimating`、`RetreatPauseAndSettlementLeaveNoGhostShots`、`AiExusiaiKeepsOriginalCapsuleAndAttackBehavior`。通过实际保存场景部署，玩家死亡后调用 roster.Tick(30f)，检查玩家新实例本地引用和物理；撤退完成仍遵循旧冷却。红方 AI 能天使死亡再部署后仍是胶囊、无本次新增组件，普攻仍从根位置出弹。现有 MOBA 测试仅为玩家能天使读取启用的 SkinnedMeshRenderer 可见包围盒，其余五席继续检查根胶囊；所有根 CapsuleCollider 独立验证，不能用隐藏胶囊代替玩家可见模型的落地证据。EditMode 中的模板是失活对象，校准几何使用临时启用的副本，不以模板 `activeInHierarchy=false` 把所有网格过滤掉。

```csharp
Assert.That(playerExusiaiSlots.Count(), Is.EqualTo(1));
Assert.That(integratedTemplates.Count(), Is.EqualTo(1));
Assert.That(aiExusiai.GetComponentInChildren<ExusiaiPresentation>(true), Is.Null);
Assert.That(aiExusiai.GetComponent<ExusiaiCombatPresentation>(), Is.Null);
Assert.That(aiExusiai.GetComponent<ProjectileSpawnPoint>(), Is.Null);
Assert.That(aiExusiai.GetComponent<Renderer>().enabled, Is.True);
Assert.That(live.GetComponent<ExusiaiCombatPresentation>(), Is.Not.Null);
Assert.That(live.GetComponentInChildren<ExusiaiPreviewDriver>(true), Is.Null);
Assert.That(muzzle.IsChildOf(live.transform), Is.True);
Assert.That(hit.collider.GetComponent<CombatUnit>(), Is.SameAs(live));
Assert.That(redeployed.GetComponent<ExusiaiCombatPresentation>().Presentation.transform.IsChildOf(redeployed.transform), Is.True);
Assert.That(redeployed.GetComponent<ExusiaiCombatPresentation>().VisualRoot.IsChildOf(redeployed.transform), Is.True);
Assert.That(redeployed.GetComponent<ProjectileSpawnPoint>().Muzzle.IsChildOf(redeployed.transform), Is.True);
```

重新部署还须检查 presentation、visualRoot、muzzle 与旧实例引用不同，并且射击和随动实际可运行；不能仅以组件存在替代生命周期验收。
- [ ] **Step 3: 先运行接入场景的新测试，确认现有保存场景仍缺少模型。**
- [ ] **Step 4: 实现增量 `Upgrade()`，同步 Builder 的 CreatePlayer 调用，再在当前场景执行菜单或 batch `-executeMethod ArknightsFrontline.Editor.ExusiaiArenaSceneTools.Upgrade`。** 修改前验证唯一可操控能天使席位，保存前验证仅该席有正式资源和新增组件；不触碰模板身份或其余场景对象。验证已升级场景，再运行第二次确认不产生重复内容。
- [ ] **Step 5: 运行完整原 EditMode/PlayMode 回归及模型独立验收。** 使用上面的命令去掉 `-testFilter` 分别写 `editmode-final.xml`、`playmode-final.xml`。只有 XML 全部通过才报告通过；对失败按具体触发查因，不扩大改动去重写玩法。验证后比较用户3文件 SHA256并恢复工具造成的额外改动。
- [ ] **Step 6: 在真实游戏相机下验证并保存图像。** 场景自然输入控制玩家移动/攻击/冲刺，记录玩家部署、5 m/s 小跑、站定/移动射击输入、E 冲刺后攻击、R 连射、死亡/再部署，以及玩家正式模型与 AI 原胶囊同屏；至少1080p和720p。观察玩家脚底滑动、首次抬枪、双手握枪、枪口位置、急转时刘海和裙摆、血条与敌我辨识。另在30/60/120 FPS 跑同样的玩家移动/转向/暂停片段；图像和日志写入 TestResults 子目录。若只布置静态构图，明确标注，不能作为自然对局动画证据。
- [ ] **Step 7: 写验证记录并提交本任务变更及 `.meta`。** 包含实际编辑器版本、测试结果、视觉证据路径、原设置 SHA256比较、场景增量审计和已知首弹视觉差。Commit: `feat: integrate Exusiai models into prototype arena`。

## 计划自审结果与交接

- 模型接线/尺寸与血条：Task 1；移动、朝向、逐弹和首次开火：Task 2；缩放物理：Task 3；枪口：Task 4；保存引用、部署与对局回归：Task 5。
- 不创建新的攻击前摇、技能动画、死亡动画、角色注册系统或包依赖。
- 最主要风险是校准缩放后的骨链碰撞，以及序列第一弹早于抬枪表现；两者已有明确验证和行为约定。
- 该文档是计划，不代表接入已经实现，也不代表 Unity 测试已运行。
- 推荐本会话直接执行（Native）：五个任务共享一个玩家安装器与同一组场景引用，连续实施便于保持校准和事件绑定一致。也可选择逐任务子代理实施并审阅；接入范围已按用户要求确定为仅可操控能天使，执行方式待用户审阅选择。
