# 阶段 6 电脑角色与再部署实施计划

**目标：**把阶段 5 的单人技能演示扩展为固定 3v3 灰盒对局，提供五名无技能电脑干员、玩家与电脑共用的主动撤退、递增再部署、技能初动恢复，以及可在保存场景中验证的 HUD 和镜头接线。

**权威规格：**`docs/superpowers/specs/2026-09-30-stage-6-ai-and-redeployment-design.md`。本计划不替代该规格；若实现中发现规格歧义，应先记录并请求用户决定，不通过测试名称悄悄改规则。

**实施方式：**直接在 `D:\arknights-frontline` 的 `dev` 分支工作，不创建工作树。沿用子代理实施、独立复审、TDD；任何子代理模型不得高于 `gpt-6-luna`，推理强度不得高于 `xhigh`，超出能力限制时请求人工审核。每个任务先写真实行为测试并取得预期 RED，再做最小实现、取得 GREEN、独立复审，最后只提交本任务文件或 hunk。阶段 6 人工验收在用户实际执行前始终标为待验收。

## 全局边界与证据

- 当前计划基准为 `dev` 的 `3a86bbe`。阶段 5 后的最后一次有效全套 XML 为 `TestResults/exusiai-e-final-verify-editmode.xml` 260/260 和 `TestResults/exusiai-e-final-verify-playmode.xml` 62/62；Task 0 要重跑当前基线，不能仅引用旧结果。
- Unity Windows 编辑器为 `C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe`。每次测试等 Unity 进程结束并解析有效 XML；批处理启动的返回码、空日志或尚在写入的 XML 不能替代结果。若 `-quit` 产生无效 XML，去掉 `-quit` 重试；优先沿用此前成功的不带 `-quit` 命令。
- 开始前记录 `git branch --show-current`、`git rev-parse HEAD`、`git status --short`、暂存区和受影响文件差异。保留 Packages、ProjectSettings、`.superpowers/brainstorm/`、日志、无关文档及所有其他用户改动。任何新的 `.cs` 或测试文件都需相应 `.meta`。
- `Assets/Game/Editor/PrototypeSceneBuilder.cs` 已有用户摄像机改动；`Assets/Game/Scenes/PrototypeArena.unity` 已有用户重建结果，现有差异约 843 行增删且包含大量 Unity fileID 变化。Task 6 前必须备份这两个文件的字节内容到本任务忽略的执行记录目录，记录 SHA-256，并逐项核对用户摄像机行为。只允许暂存阶段 6 的构建器 hunk。若生成场景无法在不夹带用户旧改动的情况下安全暂存，停止场景提交并请用户决定是否先单独提交其场景/摄像机改动；不得自行重置、覆盖或打包提交。
- Unity 可能迁移用户已有的 ProjectSettings 或渲染资产。测试/构建前后记录受影响文件 hash 和 `git status`；任何非任务资产变化均保持未暂存，不自动恢复无备份内容。场景构建前确认交互式 Unity 已关闭。
- 每个提交前执行 `git diff --cached --check`、检查 staged 文件和 hunk；从未通过的测试 XML 中列出具体失败名。完整 PlayMode 的所有未通过项必须与 Task 0 基线逐项比较，不得只报总数。

## 任务依赖与接口

| 顺序 | 任务 | 主要接口 | 依赖 |
| --- | --- | --- | --- |
| 0 | 预检和全套基线 | 用户脏文件边界、EditMode/PlayMode XML | 无 |
| 1 | 伤害来源 | `CombatUnit` 来源感知伤害事件、投射物传递攻击者 | 0 |
| 2 | 身份与常驻槽位 | `OperatorIdentity`、部署/离场/倒计时、尸体清理 | 1 |
| 3 | 撤退与玩家 B 输入 | 共用撤退控制、输入阻断与离场通知 | 2 |
| 4 | 简单电脑 | 优先级、跟线、低血量撤退、安全重试 | 1–3 |
| 5 | 比赛冻结与玩家反馈 | MatchOutcome、HUD/镜头重绑、倒计时 | 2–4 |
| 6 | 确定性场景接线 | 六个模板/槽位、移除假人、保存场景 | 2–5，场景安全门 |
| 7 | 端到端与完成验证 | 完整套件、独立整体复审、人工清单 | 0–6 |

Task 1 修改的伤害事件由 Task 3 撤退和 Task 4 电脑仇恨共同消费，事件语义必须在 Task 1 固定。Task 2 创建的新活体由 Task 3/4 配置，Task 5 只订阅槽位事件，不直接生成角色。Task 6 仅接线和生成场景，不在构建器中复制角色规则。任务之间顺序提交；复审发现的问题由原任务实施代理按新的 RED 测试修复，并做针对性重审。

## Task 0 预检与可重复基线

**文件：**只读项目文件；XML、日志和工作记录写入忽略的 `TestResults/` 与 `.superpowers/sdd/2026-10-01-stage-6-ai-and-redeployment/`。

- [ ] 确认实际工作目录是 `D:\arknights-frontline`、分支是 `dev`，记录 HEAD、完整 status、暂存区与受影响文件差异。重点保存构建器和场景的当前 hash、用户摄像机 hunk，以及 `PlayerCommandInputPlayModeTests.cs` 的既有摄像机测试 hunk。
- [ ] 确认交互式 Unity 已关闭，使用 Unity 6000.6.2f1 顺序运行完整 EditMode、完整 PlayMode，写 `TestResults/stage6-baseline-editmode.xml` 和 `TestResults/stage6-baseline-playmode.xml`。解析 total/passed/failed/skipped/inconclusive 与全部未通过 fullname；若与 260/260、62/62 不同，先查清原因，不在不明基线上写功能代码。
- [ ] 记录创建、死亡、尸体、技能 HUD、镜头及保存场景的现有生命周期调用顺序。确认 Task 1–6 的新增接口不会要求保存对已销毁 `CombatUnit` 的强引用。

## Task 1 伤害来源与事件

**文件：**修改 `Assets/Game/Scripts/Combat/CombatUnit.cs`、`Assets/Game/Scripts/Combat/Projectile.cs`；测试 `Assets/Tests/EditMode/CombatUnitTests.cs`、`Assets/Tests/EditMode/ProjectileTests.cs`。

**接口：**保留 `TakePhysicalDamage(float)`；新增带 `CombatUnit attacker` 的入口以及实际生命损失通知。事件必须让订阅者区分敌方干员、敌方塔、小兵和无来源伤害，但不改变投射物伤害公式、死亡事件只发一次或技能减速。

- [ ] 先写 RED：带来源伤害通知的攻击者与实际损失生命值正确；过量伤害只报告剩余生命；零/负伤害不触发正伤害；旧入口仍可调用且来源为空；投射物命中传递真实攻击者；死亡仍只通知一次。运行聚焦 EditMode，保留预期失败 XML。
- [ ] 最小修改 `CombatUnit` 和投射物；伤害事件与 `Died` 的同帧顺序必须让后续槽位把致命伤害计为死亡，而不是引导中断后撤退成功。
- [ ] 聚焦 EditMode 转 GREEN，再运行现有伤害、投射物、技能相关 EditMode 测试。独立复审事件重复调用、无来源兼容和结算时投射物取消；仅提交本任务文件。

## Task 2 干员身份与常驻槽位

**文件：**新增运行时 `OperatorIdentity`、`OperatorRosterController` 与必要的槽位状态对象及 `.meta`；测试新增对应 EditMode 类，并扩展尸体生命周期测试。角色对象从场景内非激活模板实例化，不能引用 `UnityEditor`。

**接口：**一个槽位只保存稳定 key、队伍、角色类型、模板、部署点、离场次数、当前活体、等待剩余时间和停止状态。提供只读快照、`Tick(float)`、死亡/成功撤退入口、`StopForMatch()`，以及玩家实例生成/离场通知供 Task 5 订阅；具体公开方法签名在 RED 测试中固定。

- [ ] 先写 RED：六个身份键互不冲突；非激活模板不被目标选择器选中；初次部署仅生成一个满血活体；死亡 8 秒后生成新实例并清理同 key 尸体；第二至第六次等待是 12/16/20/24/24 秒；主动撤退的首次等待 5.6 秒且无尸体；重复通知不重复计次或双重生成；重载/停止后不产生迟到实例。
- [ ] 用可手动 `Tick` 的槽位状态实现计时，不通过已销毁活体继续运行。实例化前清理对应尸体，保持其他干员、小兵和塔尸体不变；新活体必须重设身份、名称、满血和初始组件状态。
- [ ] 聚焦 EditMode 转 GREEN；独立复审事件订阅解绑、Unity `Destroy` 延迟、静态尸体登记和多个槽位互不影响。只提交新运行时文件/测试及其 `.meta`。

## Task 3 共用撤退控制与真实 B 输入

**文件：**新增 `OperatorRetreatController` 及测试；修改 `PlayerCommandController.cs` 和仅必要的技能/攻击接口；扩展 `PlayerCommandInputPlayModeTests.cs` 的 B 相关 hunk，不能暂存该文件原有摄像机测试 hunk。

**接口：**`TryBegin`、`Tick(float)`、中断/完成事件和只读剩余时间；角色活着且不在 E 冲刺时可引导 1.5 秒。槽位只消费完成事件；电脑和玩家使用同一控制器。死亡和比赛结束优先取消引导。

- [ ] 先写 EditMode RED：开始引导停止移动/普攻/连射/未确认 E，阻断后续移动、攻击、技能、取消输入；已开启 R 的持续/冷却照常计时；E 冲刺中 B 无效；敌方干员或塔的正伤害中断，小兵/友军/零伤害/无来源不打断；致命伤害走死亡；1.5 秒完成仅通知一次且不生成尸体。
- [ ] 加真实输入 PlayMode RED：按 B 进入引导，连续按 B 或其他指令不叠加/改写，受塔或敌干员攻击时中断，完成后进入槽位折减等待。使用实际输入动作而非直接调用 `TryBegin` 冒充玩家测试。
- [ ] 最小接线撤退控制、玩家输入和必要的技能命令阻断。聚焦 EditMode 与 PlayMode 转 GREEN；独立复审 A/W/E/R、右键、Esc、停止键及普通攻击不回归。仅暂存本任务 hunk。

## Task 4 五名无技能电脑角色

**文件：**新增独立 `SimpleOperatorAiController`（及必要的纯选择辅助）和测试，沿用已有 `UnitMotor`、`BasicAttackController`、`TargetRules`、`LaneMinionController`、`TowerCombatController`。

**接口：**AI `Configure` 接收干员身份、槽位、友方塔与退路；`Tick(float)` 先判比赛/死亡与低血量，再选射程内合法目标，最后跟线或回塔。AI 不调用任何 W/E/R 方法，也不由玩家、兵线或塔反向依赖。

- [ ] 先写 RED：最近 2 秒伤害自己的敌干员优先于另一更近干员；之后按最近敌干员、小兵、塔排序；非法/射程外目标不被攻击；同级同距按 `EntityId` 排序；银灰拒绝空中，能天使/小羊可对空；目标离开射程即清除；有兵线时跟最近友军小兵后方 2 米，无兵线时回友方塔前 4 米；低于 25% 开始共用撤退，引导被打断后安静 1.5 秒才重试，期间照常跟线/攻击。
- [ ] 最小实现，不加行为树或寻路；活动实例销毁时解绑伤害监听和攻击目标。聚焦 EditMode 转 GREEN，补 PlayMode 测试证明五名电脑在真实帧中移动、普攻、撤退和重新出现且不释放技能。
- [ ] 独立复审目标分类、时间边界、场景对象遍历、性能和比赛结束停止；只提交本任务代码/测试。

## Task 5 比赛冻结与玩家可见反馈

**文件：**修改 `MatchOutcomeController.cs`、`SkillHudPresenter.cs` 或新增最小玩家部署状态 Presenter；必要时修改 `MobaCameraController.cs` 的极小 hunk，但该文件已有用户改动，必须隔离暂存。扩展对应 EditMode/PlayMode 测试。

**接口：**槽位的玩家生成/离场事件重新绑定 HUD 和 `MobaCameraController`；首次生成及再部署时居中一次，玩家手动移镜头后不持续拉回。引导显示 `B RETREAT`，等待显示 `REDEPLOY` 及剩余秒数，技能操作反馈在无活体时隐藏。

- [ ] 先写 RED：新玩家实例接手 HUD 和镜头目标，旧实例销毁后不保留失效引用；R/E/W 重置状态正确；用户拖动/边缘移镜头后不被每帧强制居中；等待和引导倒计时可见；比赛结算开始即停止槽位、AI、撤退、计时与生成，结算同帧不得补生。
- [ ] 实现事件式重绑和最小 UI，避免在 `MatchOutcomeController` 内放生成逻辑。聚焦 EditMode/PlayMode 转 GREEN，并跑原有 MatchOutcome、技能 HUD、镜头测试。
- [ ] 独立复审比赛停止顺序、对象销毁后的 Unity null 语义及用户摄像机 hunk 边界；仅提交本任务修改。

## Task 6 构建器接线与保存场景

**文件：**修改 `Assets/Game/Editor/PrototypeSceneBuilder.cs`，经安全门批准后修改并保存 `Assets/Game/Scenes/PrototypeArena.unity`；扩展 `ArenaSceneSmokeTests.cs` 和必要的场景生命周期 PlayMode 测试。两个测试文件也有用户未提交改动，必须隔离 hunk。

- [ ] 构建前关闭交互式 Unity；在忽略的执行记录目录备份当前构建器与场景原文件、SHA-256、`git diff` 和摄像机关键值。新构建器生成六个非激活角色模板、六个槽位、五名电脑、玩家反馈、两个部署区；移除 `TrainingDummy_Red`；蓝方玩家能天使与红方电脑能天使用 1000/50/2/6/0.50/5，小羊银灰按规格表。
- [ ] 先写保存场景 RED：载入现有 `PrototypeArena.unity` 后场上六名活体、无训练假人、模板非激活、双方技能组件归属正确、摄像机用户行为与已有断言保持；初次运行必因旧场景缺槽位而失败。该测试不应通过手工构造新场景来绕过保存资产。
- [ ] 最小修改构建器并重建/保存场景；重新加载后的聚焦场景测试转 GREEN。检查摄像机朝向、缩放、居中、渲染和用户已修改材质是否保留；核对 ProjectSettings/Packages 未被暂存。
- [ ] 比较生成场景与 HEAD、构建前用户版本。若不能把阶段 6 场景改动与用户旧摄像机/场景改动安全分离，就暂停提交并请求用户明确决定；不得把当前用户改动整体纳入本任务提交。可继续完成不依赖场景提交的代码测试，但不可称场景交付完成。
- [ ] 只有安全门通过后暂存阶段 6 构建器 hunk、经批准的场景和本任务测试 hunk，独立复审后提交。

## Task 7 端到端验证与交付

- [ ] 跑聚焦 EditMode：槽位、伤害、撤退、电脑、匹配、技能 HUD；跑聚焦 PlayMode：真实 B 输入、保存场景六人开局、死亡/撤退再部署、尸体清除、镜头/HUD 重绑、结算冻结。每个结果必须有最终有效 XML。
- [ ] 跑完整 EditMode 与完整 PlayMode 各至少一次。以有效 XML 为准记录 total/passed/failed/skipped/inconclusive，列出每个未通过测试的 fullname，与 Task 0 基线逐项比较；修复本阶段回归后重新跑受影响聚焦和全套。
- [ ] 独立整体复审从 Task 0 基准到当前 HEAD 的范围、规格符合度、测试质量与用户脏文件隔离。重大/重要问题先用失败测试复现，再修复并做针对性复审；不得以测试全绿替代独立复审。
- [ ] 汇报每个任务提交、场景构建/保存结果、完整测试未通过项和阶段 6 人工验收清单。用户尚未在 Unity 编辑器验收阶段 6 前，报告“待验收”；不要把阶段 5 的人工通过套用于阶段 6。

## 阶段 6 人工验收清单

- [x] 正式场景 3v3 开局、六名干员辨识明确且无红方训练假人。
- [x] 玩家 B 引导、敌干员/塔伤害中断、小兵伤害不中断，期间输入不会触发移动/攻击/技能。
- [x] 死亡与撤退后的倒计时、重新部署地点、满血和干员尸体清除时机正确。
- [x] 五名电脑跟兵线、回塔、按优先级普攻，低生命撤退和重新出现；银灰不对空。
- [x] 玩家重新部署后镜头、输入、W/E/R 和 HUD 正常，E 冲刺仍保持阶段 5 已验收的手感。
- [x] 任一防御塔被摧毁后，电脑、倒计时和生成全部冻结。

**验收结论（2026-10-01）：**用户确认阶段 6 人工验收“全部通过”。阶段 6 已完成；最终自动化证据为完整 EditMode 304/304、完整 PlayMode 78/78。任务提交为 `91eb6e8`、`b608346`、`d27d08e`、`8cf2d7b`、`e88b004`、`24a5328`。此结论记录用户反馈，不表示本计划文档中的人工步骤由自动化测试代替。
