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

## 后续状态

统计、HUD/退出、保存场景接线、最终完整测试及 Windows 构建仍在实施；此记录不表示这些步骤已经通过。
