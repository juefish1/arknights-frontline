# 玩家能天使模型接入验收记录

实施分支：`codex/exusiai-arena-player`。基础提交：`a8b6e9c`。仅接入蓝方可操控能天使；五个 AI 席位保持原胶囊和战斗行为。

## 实现

- 正式模型作为 `ExusiaiVisual` 子节点，保留原玩法根、碰撞体、身份和阵营材料，隐藏玩家胶囊 Renderer。
- 实际水平位移驱动小跑，按视觉世界缩放校正播放率；逐发 `ShotRequested` 驱动转向和后坐力，不产生额外弹丸或伤害。
- 头发和裙摆的碰撞半径、惯性加速度上限、传送距离阈值随模型缩放。
- 投射物从当前 `Muzzle` 世界位置生成；其它单位和缺失挂点沿用根位置。
- 死亡、撤退引导、禁用、重新部署和对局结算清理表现状态；部署实例保留所属对局引用。
- `ExusiaiArenaSceneTools.Upgrade` 增量修改保存场景；Builder 只在 `CreatePlayer` 路径使用安装器。

## 环境与证据

实际编辑器：Unity **6000.3.25f1**，`C:/Program Files/Unity 6000.3.25f1/Editor/Unity.exe`。没有更改项目版本或增加包依赖。

完整 XML、日志、原设置备份和图像保留在 `TestResults/exusiai-arena-integration/`（Git 忽略）。Git 提交保留本记录、实现、场景和测试源码。

| 验证 | 结果 |
| --- | --- |
| 模型安装、校准、血条 | Task 1：12/12 通过 |
| 动画桥接、普通战斗、技能 | Task 2：24/24 通过 |
| 缩放骨链、安装和血条 | Task 3：18/18 通过 |
| 枪口、投射物、接线 | Task 4 EditMode：18/18 通过 |
| 枪口后的战斗、技能、动画 | Task 4 PlayMode：24/24 通过 |
| 保存场景、重复升级、席位拒绝、MOBA 场景 | Task 5 EditMode：7/7 通过 |
| 真实输入、30/60/120 FPS、E/R、撤退、再部署、AI、点选 | Task 5 PlayMode：6/6 通过 |
| 审阅修复专项回归 | `review-green.xml`：15/15 通过 |
| 完整 EditMode | `editmode-reviewed.xml`：368/368 通过 |
| 完整 PlayMode | `playmode-reviewed.xml`：99/99 通过 |

新增测试均先观察失败，再实现并验证通过。缩放测试原先在两倍尺寸下穿透碰撞代理，30/60/120 FPS 下参考响应也不一致；修正后通过。撤退测试原先保留待播放后坐力，绑定引导开始事件后通过。

原模型独立验收全部退出码 0：`ExusiaiSecondaryAcceptance.Run`、`ExusiaiMotionPlayCheck.Start`、`ExusiaiHairPlayCheck.Start30/Start/Start120`。其中 14 条链、78 根骨骼保持；帧率间最大可见网格差约 6.7 mm，射击握枪误差约 5.6 mm，刘海最大偏移约 9.5 mm。报告分别保存为 `secondary-motion-acceptance.txt`、`motion-play-validation.txt`、`hair-play-30/60/120.txt`。

## 场景增量审计

实施前 172 个序列化对象，实施后 178 个；没有删除原对象。仅三个原块发生变化：

- `1072871850`：玩家 GameObject 增加动画桥接和枪口组件。
- `1072871851`：玩家 Transform 增加模型子节点，位置、旋转、缩放保持。
- `1072871867`：玩家 MeshRenderer 关闭，原材料保持。

其余原块逐字相同；包括五个 AI 模板、roster、相机、HUD、塔、场景渲染引用。复制场景重复升级两次的测试检查原对象 GlobalObjectId、组件序列化值、材料与变换。

## 镜头验收

`screenshots/` 下保存 720p 和 1080p 图像：`deployment`、`natural-jog-30/60/120`、`natural-standing-shot-player-and-ai`、`natural-moving-attack-input`、`natural-charge-shot`、`natural-overload`、`death`、`redeployed`。

这些镜头采集于真实 Play 场景。输入测试通过虚拟鼠标/键盘进入现有命令和技能输入流程，对手位置为测试控制；不是人工试玩记录。移动途中输入攻击按原玩法停止移动并射击，未新增移动射击机制。截图来自游戏相机渲染，不包含 ScreenSpaceOverlay HUD。

已查看小跑和射击镜头：玩家正式模型与 AI 胶囊同屏；血条位于模型上方，武器、腿部动作和原地动画可见。数值验收检查可见待机几何高度 2.4 ± 0.02 m、脚底距地面 ≤0.02 m、独立根碰撞体落地及射线点选。蒙皮 Renderer 的导入边界有保守余量，因此落地测试读取实际渲染顶点，不使用隐藏胶囊或缓存边界替代几何证据。

## 原有设置

三个用户未提交文件的基准 SHA256：

| 文件 | SHA256 |
| --- | --- |
| `Assets/Game/Rendering/PrototypeURP.asset` | `B517EC80917B26988FCA1845715FD470CB4A31D935AA3657348FFEAD1D5E595B` |
| `ProjectSettings/EditorBuildSettings.asset` | `89440027C77624187CA0E6042127FB6AB59779F4C3EA4B2E77B1201DDC64AF81` |
| `ProjectSettings/GraphicsSettings.asset` | `94581F881CD88971C05B3BB51A96F2F5B825FD5D5C5FF524197B9510FB8348CD` |

每次编辑器运行结束恢复其造成的额外设置改动。完整回归结束后，三个文件的 SHA256 均与上述基准完全一致。

## 已知表现边界

首次出弹仍立即执行；未预瞄准时，沿用约 0.22 s 抬枪后显示后坐力，不延迟实际伤害。高射速下同帧视觉反馈可以合并。冲刺使用现有小跑并限制播放率，死亡沿用平面尸体，没有新增专属技能或死亡动画。

## 实施判断

- 当前 checkout 使用功能分支：保留现有 Unity 环境，`dev` 保持基础提交；代价是功能文件在此 checkout 可见。
- 用实际蒙皮顶点校准和验收：导入边界有保守余量；代价是测试进行网格烘焙。
- 保存对局控制器引用：部署克隆没有 Arena 父节点；代价是模板必须正确序列化所属对局引用。
- 保存场景审计使用独立测试类：隔离复制场景与临时单位生命周期；代价是增加一个测试文件。

## 独立代码审阅与修复

`gpt-6-astra` 对 `a8b6e9c..466e58a` 完成独立只读审阅，无 Critical 问题；发现两个 Important 问题，已分别写出失败测试并修复。专项 15/15、完整 EditMode 368/368、PlayMode 99/99 均通过；三份最终日志的失活 Animator 警告均为 0。原模型 `ExusiaiMotionPlayCheck.Start` 再次通过（退出码 0，1080 个样本），报告保存为 `motion-reviewed-validation.txt`。

1. E 在 30 FPS 完成冲刺时，最后约 0.9 m 位移被误判为传送，清掉刚发出的第一弹反馈。适配器现在保存 `DashCompleted` 事件到下次位移取样。覆盖 6.5 m 冲刺和一次取样间完成的 0.9 m 短冲刺；取消后续连发后单独检查第一弹表现，避免后续子弹掩盖问题。
2. 失活配置和禁用模型时调用 `Animator.Update(0)`，完整日志出现 52 次警告。现在立即清理逻辑状态和物理缓存，将 Animator 姿态重置推迟到其可运行时；覆盖失活配置、启停、重新启用后的射击。

审阅保留的两项 Minor 测试补充：

- 当前逐弹测试验证实际弹数、伤害和动画层，并未精确计数每弹的视觉调用或后坐力时间重启。代码只订阅逐弹事件，未新增伤害路径。
- 缩放验证使用参考 1、放大 1.5/2 和实际模型骨骼的归一化响应；接触测试使用独立三骨链。安装后的世界缩放约 1.42645，尚未完成该精确尺寸下动态腿部接触和烘焙刘海轮廓的专项测量。上文独立模型的 6.7/5.6/9.5 mm 数据来自原尺寸验收，不作为校准尺寸下全部物理边界的证明。

连续视觉质量不能仅由截图证明；本次不声称完成逐帧人工试玩审查。专属死亡/技能动画和 AI 正式模型属于已明确排除的范围。
