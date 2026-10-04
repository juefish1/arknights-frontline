# 阶段 7 验证账本

本账本记录原工作区 `D:\arknights-frontline`、`dev` 分支在 Unity `6000.6.2f1` 下的阶段 7 验证。当前 `dev` 已改用 Unity `6000.3.25f1`；以下测试和构建结果为历史证据，不代表当前版本已通过相同验证。阶段 6 的“全部通过”不代替本阶段验收。

## 范围与保护

用户确认 Windows 文件版；取消 15 分钟强制结算；结果面板不再提供重新开始。玩家确认退出时，两名电脑队友自动同意，三个同队常驻槽位全部同意才触发退出。

Packages、ProjectSettings、用户材质/渲染配置、MinionLanePlayModeTests、未跟踪文档等不纳入功能提交。场景先备份，再原位升级。Unity 测试可能迁移 ProjectSettings.asset，运行后恢复开始时的原字节备份。

## 开始基线

- 起始 HEAD：`ff87582838d09ed3334241d03cb20fe32417d732`。
- 完整 EditMode：304/304，通过；无失败、跳过或不确定项。
- 完整 PlayMode：78/78，通过；无失败、跳过或不确定项。
- 原始 XML、日志、文件哈希与字节备份：`.superpowers/sdd/2026-10-01-stage-7-results-and-restart/`。目录沿用最初名称，内容以新退出设计为准。
- 需求文档提交：`f23cca0`。

## Task 1：计时与冻结

- 初始 RED：`TestResults/stage7-task1-red.xml`，12 项中 5 项失败；其中移动冻结项因测试夹具未初始化输入而报错，不将该项算作真实行为 RED。
- 修正夹具并暂时移除本任务新增冻结接线后，`TestResults/stage7-task1-freeze-red.xml` 的单项测试因玩家命令未停止而失败，取得真实行为 RED。
- 恢复接线后的 GREEN：`TestResults/stage7-task1-green.xml`，12/12。
- 玩家命令聚焦回归：`TestResults/stage7-task1-player-command-green.xml`，9/9。
- 独立静态复审：`stage7_design_review`，无阻塞项。有限 delta 显式测试与运行时重复 Configure 契约为非阻塞建议；当前配置只在启动时执行。
- 实现提交：`8a27ca0`。

## Task 2：六槽位统计

- 最小编译有效的空实现先于行为 RED。
- 首次默认权限启动遇到 UPM IPC 超时，无 XML，不算 RED；提升权限重跑成功。
- 有效 RED：`TestResults/stage7-stats-red-elevated.xml`，15/15 未通过、无跳过或不确定项，编译无 CS error。
- 测试涵盖真实伤害、重入致命命中、过量塔伤害、死亡/撤退/再部署、伪造身份、监听重绑、只读快照，以及真实塔尸体销毁后缓存血量。
- 初次 GREEN：`TestResults/stage7-stats-green.xml`，14/15；唯一失败 `ArknightsFrontline.Tests.EditMode.MatchStatisticsControllerTests.SnapshotUsesCachedTowerHealthAfterDeathPresenterDestroysTowerInEditMode`。空来源伤害被归属过滤提前排除，导致塔血缓存未更新；已改为所有实际伤害先同步血量，再过滤干员伤害归属。
- 修复后聚焦 GREEN：`TestResults/stage7-stats-related-green.xml`，26/26（统计 15、CombatUnit 6、OperatorRoster 5），无非通过项。
- 原始 14/15 XML 保留，未用新结果覆盖。
- 独立复审发现配置契约缺口：统计未拒绝错误的固定六席/蓝红各三席和塔阵营接线。正常 Builder 数据未损坏，但组件应明确拒绝非法配置。
- 补充 RED：`TestResults/stage7-stats-validation-red.xml`，18 项中原 15 项通过、新 3 项预期失败。
- 修复 GREEN：`TestResults/stage7-stats-validation-green.xml`，29/29（统计 18、CombatUnit 6、OperatorRoster 5），无非通过项。
- 独立复审 `stage7_baseline` 复核配置校验修正，无剩余阻塞项；旧干员 delegate 的销毁/重绑更多依赖代码检查，后续可补针对性测试，非当前缺陷。Unity 已退出，配置原字节恢复，场景哈希未变。

## Task 3：退出投票与 HUD

- 初次实施先写了完整投票实现，未遵守先 RED 顺序；已纠正：备份本任务实现，将 RequestExit/CastVote 改为编译有效的失败入口，再运行真实行为测试。
- RED：执行目录 `vote-red-results.xml`，7 项中 5 项失败；失败为三席表态、死队友自动同意、结算前拒绝/结算后接受、两票不足、全票仅一次等预期行为断言。
- 恢复实现后 GREEN：`vote-green-results.xml`，7/7，无非通过项。
- 独立静态复审：`stage7_match_impl`，无阻塞项。默认参数自动同意的单独覆盖为非阻塞建议。
- Unity 进程退出；ProjectSettings.asset 原字节已恢复，场景、Packages 与材质哈希不变。
- 投票核心提交：`656ad54`。
- HUD 初始行为 RED：`TestResults/stage7-task3-hud-red.xml`，7/7 失败，编译与夹具正常；覆盖真实 UGUI、非满塔血、六行快照、三个胜负标题及真实 Button 注入退出回调。生产 HUD/Bootstrap 尚未完成。
- 补充检查发现每帧重建结果行、MatchHud 默认 100×100 未铺满 Canvas，以及动态字体未释放；正在补充行为 RED 修复。
- 额外 RED 首次因 SceneTest 的不可用 NUnit 标记和 Builder 的 Object 类型歧义编译失败，无 XML，不计 RED；两处已修。
- `stage7-task3-extra-red-2.xml` 的 3 项失败中，行数实得 10（期望 6）与未升级场景引用缺失是行为 RED；布局项为夹具 InvalidCastException，不计行为 RED。
- 修正夹具后 `stage7-task3-extra-red-3.xml`，3/3 有效行为 RED：布局默认居中、行数 10、场景引用缺失。原失败日志/XML保留，不覆盖。
- 初始字体 `stage7-task3-font-red.xml` 0/1 与第一轮 `stage7-task3-hud-green.xml` 9/10 中的字体失败，不能证明真实 Unity 生命周期：EditMode 夹具未自动调用 OnDestroy。此项更正为清理回调契约测试，其他 9 项通过证据仍有效。
- 显式调用 OnDestroy，临时撤除本任务自有 ReleaseOwnedFont 调用后的 `stage7-task3-font-cleanup-red.xml` 0/1 为真实回调清理 RED；恢复后 HUD `stage7-task3-hud-green-2.xml` 10/10。
- 真实生命周期：`MatchHudPresenterPlayModeTests.DestroyingAnActiveHudReleasesItsOwnedDynamicFont` 在活动场景中直接销毁 HUD、等待两帧，不依赖场景资源卸载；暂时撤除清理调用时 `stage7-task3-font-play-red.xml` 0/1，恢复调用后 `stage7-task3-font-play-green.xml` 1/1。
- 行复用、根容器全 Canvas 布局和自有字体释放已修正。鼠标点击、保存场景、重载归零和最终整体套件仍待后续验证，不能由 EditMode Button.onClick.Invoke 代替鼠标命中证据。
- 独立最终复审 `stage7_design_review`：HUD/bootstrap 无剩余阻塞项；复核 10/10 EditMode 与 1/1 生命周期 XML。Windows 构建助手的输出路径已固定为项目绝对路径。真实鼠标点击、保存场景/重载及中文视觉仍未通过验收。
- 统计实现提交：`316b2a0`；HUD 实现将作为独立提交，仅包含上述已验证代码和测试。

## Task 4：保存场景、整体回归与 Windows 文件

- HUD 提交：`acb4d96`。
- 独立跨模块复审 `stage7_design_review` 未发现实现阻塞项；原位升级仅保存目标场景，Windows 输出路径为项目绝对路径。该结论不替代实际升级、构建或人工验收。
- 新增独立 `MatchResultsPlayModeTests` 四项保存场景测试；检查实际顶部 Text、901 秒仍比赛、真实致命命中和非零塔伤害、冻结六行快照、虚拟鼠标经保存的 UI 输入模块和 GraphicRaycaster 点击退出、重复物理点击一次回调，以及单场景重载后六活体与数据归零。测试自身不写 PlayerPrefs；teardown 卸载原型场景进入空场景，恢复输入测试运行时与时间比例。
- 首次 `stage7-saved-play-red.log` 因不支持的 `[NonParallelizable]` 编译失败，无 XML，不计行为 RED；仅移除该标记后重跑。
- 有效保存场景 RED：`TestResults/stage7-saved-play-red-2.xml`，0/4，通过编译、无跳过或不确定项；四项均在 `FindSavedArena` 因保存场景缺少统计组件而失败。这证明场景接线缺失，不宣称所有下游断言已分别取得 RED。
- 升级前场景 SHA-256 仍为 `565D3C01925A6D187767DD8756093DA99856F8950C932C3B73F47A42429F238F`，158 个原 YAML 对象块未改变；已确认没有 Unity 进程，开始原位升级。后续结果待补。
- 原位升级成功：158→164 个 YAML 块，新增 6 块；旧块逐块比较全部保留，仅 ArenaBootstrap 组件列表及 Canvas 子列表追加。摄像机、既有组件和材质引用未改变。第二次升级后场景 SHA-256 与首次完全相同：`3222FE4FE7EE9E52F1D3B8F4DC39B4154276CB1270F6DD180AB415ECAC491A59`。
- 独立场景复审 `stage7_baseline` 复核上述保留比较与新增引用，无阻塞项；未以静态复审代替 PlayMode。
- 首轮保存场景 GREEN：`TestResults/stage7-saved-play-green.xml`，3/4。唯一失败 `ArknightsFrontline.Tests.PlayMode.MatchResultsPlayModeTests.RealMouseClickOnSavedResultButtonApprovesThreeVotesAndInvokesInjectedExitOnce` 在 ClickAt 的 GraphicRaycaster 命中断言失败，尚未投递鼠标输入。其余时间/统计快照/重载归零三项通过。
- 启用图形设备复跑同项：`TestResults/stage7-pointer-graphics.xml`，0/1，仍为上述射线命中失败；不能据此归因于 `-nographics`。仅增强断言诊断信息，不改生产或断言条件；诊断复跑待结果。所有旧 XML 保留。
- 诊断复跑 `TestResults/stage7-pointer-diagnostic.xml`，0/1，明确真实布局缺陷：测试 Game View 为 640×480，按钮中心 `(320,-34)`、四角 Y 为 -61 至 -7，完全落在屏幕下方；Image depth=26、cull=false、raycastTarget=true。不是输入投递问题或无图形设备的推断。计划只缩放自有结果卡片以适配窗口，不改共享 Canvas；补尺寸/重设窗口测试并保持目标 1280×720、1920×1080 原尺寸。
- 新增布局行为 RED：`TestResults/stage7-layout-red.xml`，0/1；测试依次模拟 640×480、1280×720、1920×1080，并汇总卡片边界、恢复原尺度及共享 Canvas/技能 HUD 保留断言。实际失败为 640×480 四角越界；之后两个目标尺寸原尺度断言正常。实现仅调整自有结果卡片局部缩放，保留单边 32px 边距。
- 修复后 EditMode：`TestResults/stage7-hud-scene-green.xml`，12/12（HUD 11、保存场景引用 1），无非通过项。
- 修复后真实 PlayMode：`TestResults/stage7-saved-play-green-2.xml`，5/5（保存场景四项、实际字体销毁一项），仍使用 `-nographics`。实际鼠标经射线、InputSystemUIInputModule 和 Button 批准 3/3 退出，重复点击只执行一次注入回调；重载后非零统计与投票归零、新六活体和旧字体释放均通过。
- 独立布局最终复审 `stage7_design_review` 无阻塞项：只修改自有卡片，两个目标分辨率保持原尺度、较小窗口不越界。物理显示器中文可读性及真实 Windows 进程退出仍须人工验收。
- 最终完整 EditMode 首轮：`TestResults/stage7-final-editmode.xml`，346/346，无失败、跳过或不确定项；保存场景文件哈希未改变。
- 最终聚焦 PlayMode 首轮：`TestResults/stage7-final-focused-playmode.xml`，67/68。唯一非通过项仍为 `ArknightsFrontline.Tests.PlayMode.MatchResultsPlayModeTests.RealMouseClickOnSavedResultButtonApprovesThreeVotesAndInvokesInjectedExitOnce`，但本次射线命中已通过，失败在第 174 行 `HasRequestedExit` 为 false（尚未形成退出投票），与先前坐标越界不同。独立五项套件通过而混合套件失败，正在诊断夹具/模块输入状态；XML 保留，不隐藏。
- 串行最终套件在上述失败后停止；完整 PlayMode 尚未启动，不将聚焦结果称为完整结果。ProjectSettings.asset 已恢复原字节，Unity 已退出。
- 定向混合套件诊断 `TestResults/stage7-focused-diagnostic.xml` 仍为 67/68：保存 UI 模块是当前模块、正常启用且 EventSystem 有焦点；鼠标设备存在且当前位置正确，但 Point/Click 动作虽然 enabled，controls 均为 0。正在核查导入的共享 DefaultInputActions 资产跨 InputTestFixture 重置的缓存隔离；没有改生产模块或直接调用按钮代替真实输入。
- 额度恢复后继续：分支仍为 dev，场景与配置哈希均保持，Unity 进程为 0；沿用原执行目录及原失败证据，不重复已通过的早期任务。
- 只读源码诊断与独立复审确认：InputTestFixture 保存/重置输入 action 全局状态，UI 模块另持按 InputAction 对象计数的静态引用状态；重复使用包内导入 DefaultInputActions 运行态是当前 controls=0 的更可信原因，而非 UI 失焦。批准仅鼠标测试内隔离原资产副本：克隆前验证实际场景原件十个动作引用，副本 JSON/绑定/IDs 保持一致，经同一保存模块的 actionsAsset setter 重映射，仍走真实鼠标流程。清理只销毁测试副本与 setter 创建的自有引用，先卸载模块再恢复输入夹具。此项不是生产输入补丁；GREEN 待跑。
- 测试隔离完成且独立最终复审无阻塞项。混合聚焦 GREEN：`TestResults/stage7-final-focused-playmode-2.xml`，68/68，无失败、跳过或不确定项；包含保存场景 4、字体 1、场景冒烟 7、技能 14、玩家输入 29、槽位生命周期 8、撤退输入 5。原 67/68 和定向诊断 XML 全部保留。生产 UI 模块/退出代码未作输入缓存补丁。
- 现在开始一次完整 PlayMode；完整结果尚待解析，不能由聚焦 68/68 代替。
- 一次完整 PlayMode 最终结果：`TestResults/stage7-final-full-playmode-2.xml`，83/83，无失败、跳过或不确定项。文件名后缀 `-2` 表示串行回归流程第二次尝试；第一次在聚焦失败后停止，未执行完整 PlayMode。本阶段实际完整 PlayMode 只运行了这一次。
- 按 fullname 比较开始基线 78 项：旧项全部存在并通过，无遗漏或新增回归；新增 5 项（字体销毁、保存场景计时/HUD、统计快照、真实点击、重载归零）全部通过。完整 PlayMode 所有未通过项：**无**。此前聚焦失败及修正证据已保留于上文。
- 完整测试后 Unity 已退出，场景 SHA-256 仍为 `3222FE4FE7EE9E52F1D3B8F4DC39B4154276CB1270F6DD180AB415ECAC491A59`，ProjectSettings.asset 恢复为 `1B6C58EDD8C67A49E8E015818C9D0F09E9DE4F9E426803C9C63B53011A9E7BBA`；开始 Windows x64 构建。产物与启动检查结果待补。

- 历史 Windows x64 构建于 2026-10-02 使用 Unity 6000.6.2f1 完成，进程退出码 0；`TestResults/stage7-windows-build.log` 的 BuildReport 为 Success，助手报告总大小 132846674 bytes。输出路径中的 `20261001` 是阶段开始日期，不是构建完成日期。
- 文件版启动检查：隐藏启动本次构建 exe，`-batchmode -nographics` 持续 15 秒后仍存活；`TestResults/stage7-windows-player-smoke.log` 无 Exception/NullReference/加载失败。仅在核对本次 PID、可执行路径和专用日志参数后关闭本次进程；当前无遗留 Unity/游戏进程。无图形启动不证明中文视觉、实际结算退出或性能通过。
- 新 ZIP：`Builds/arknights-frontline-windows-x64-stage7-20261001.zip`，51238799 bytes，199 个条目；SHA-256 `8601C889F3FED2928B79E26DB6FDBDEEEB7499B831F9EFEFA950A09734A9BE22`。exe、UnityPlayer、CrashHandler、场景 globalgamemanagers、自有 Runtime DLL、Mono 运行库和试玩说明均已核对存在。首次核对误用了默认 Assembly-CSharp.dll 文件名，本项目使用 asmdef；按实际 `ArknightsFrontline.Runtime.dll` 重新核对通过，未改构建产物。旧试玩包保留。
- 构建结束后 ProjectSettings.asset 恢复原字节；场景、Packages manifest/lock 哈希仍与前述一致。只读保存比较再次确认旧 158 块全部保留、新增 6 块，无差异缺失。未提交 Packages、ProjectSettings、材质、渲染设置、其他用户测试及未跟踪用户文件。
- `git diff --check` 在代码与账本范围无问题；保存场景仍有 Unity YAML 空字段自带尾空格及获批旧文件 ID 改动，未为格式化而改写用户场景。

## 最终状态

实现与场景交付提交：`7902b0d`（dev）；包含获批现有场景改动及阶段 7 新接线。此前阶段提交为 `f23cca0`（设计）、`8a27ca0`（无限计时/冻结）、`656ad54`（全员投票）、`316b2a0`（统计）、`acb4d96`（HUD）。提交后无暂存残留，范围外用户改动继续保留。

阶段 7 实现、独立复审、保存场景升级、自动化回归与 Windows 文件包完成。EditMode 346/346、聚焦 PlayMode 68/68、完整 PlayMode 83/83；完整未通过项无，旧 78 项基线无遗漏。用户于 2026-10-03 在会话中确认人工验收通过；清单见 `2026-10-01-stage-7-manual-acceptance.md`。未收到机器配置、比赛时长或帧率数值，不据此声称 8–12 分钟或 60 FPS 测量达标。
