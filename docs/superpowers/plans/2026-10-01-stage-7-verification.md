# 阶段 7 验证账本

工作区 `D:\arknights-frontline`，分支 `dev`，Unity `6000.6.2f1`。本阶段人工验收待执行；阶段 6 的“全部通过”不代替本阶段验收。

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

## 后续状态

统计、投票和 HUD 组件已验证；保存场景接线、真实鼠标点击/重载、最终完整测试及 Windows 构建仍在实施，人工验收未执行。
