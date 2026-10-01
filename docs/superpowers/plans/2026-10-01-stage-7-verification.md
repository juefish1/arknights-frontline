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

## Task 2：六槽位统计（进行中）

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

## Task 3：退出投票核心（HUD 尚未完成）

- 初次实施先写了完整投票实现，未遵守先 RED 顺序；已纠正：备份本任务实现，将 RequestExit/CastVote 改为编译有效的失败入口，再运行真实行为测试。
- RED：执行目录 `vote-red-results.xml`，7 项中 5 项失败；失败为三席表态、死队友自动同意、结算前拒绝/结算后接受、两票不足、全票仅一次等预期行为断言。
- 恢复实现后 GREEN：`vote-green-results.xml`，7/7，无非通过项。
- 独立静态复审：`stage7_match_impl`，无阻塞项。默认参数自动同意的单独覆盖为非阻塞建议。
- Unity 进程退出；ProjectSettings.asset 原字节已恢复，场景、Packages 与材质哈希不变。
- 投票核心提交：`656ad54`。
- HUD 初始行为 RED：`TestResults/stage7-task3-hud-red.xml`，7/7 失败，编译与夹具正常；覆盖真实 UGUI、非满塔血、六行快照、三个胜负标题及真实 Button 注入退出回调。生产 HUD/Bootstrap 尚未完成。

## 后续状态

统计、HUD、保存场景接线、最终完整测试及 Windows 构建仍在实施；此记录不表示这些步骤已经通过。
