# MOBA 视角与尺寸调整：验证记录

用户已确认参数并授权实施。完成日期：2026-10-05；验证引擎：Unity 6000.3.25f1。

## 参数与入口

- 干员：高 2.4、宽 1.2，胶囊中心及六席再部署高度均为 1.2。
- 地面兵：高 1.3、宽 0.75，中心高度 0.65；空中兵直径 0.85、中心高度 1.5。
- 防御塔：3×5×3；可见模型和根碰撞体一致，中心高度 2.5。
- 可走区域维持 100×24、塔位 ±38、部署区 ±46；中央道路视觉宽 12，外围地面 160×100。新增地面仅显示，不接收移动点击。
- 透视相机：相对水平面向下 56°、水平旋转 45°、离地 20、垂直 FOV 40°。相对地面焦点偏移约 (-9.539, 20, -9.539)。
- 公共尺寸来源：`Assets/Game/Scripts/Arena/ArenaVisualMetrics.cs`；相机默认值在 `MobaCameraController.cs`。
- `PrototypeArena.unity` 已原位升级；完整场景构建器也采用新参数。菜单 `Arknights Frontline/Upgrade MOBA View` 可将相同参数应用到现有原型场景。

参数是本项目的适配值，不宣称等于 LoL 或 Dota 2 的内部数值。参考原则来自 [Riot 的清晰度设计](https://www.leagueoflegends.com/en-us/news/dev/clarity-in-league/)、[Valve 角色美术指南](https://help.steampowered.com/en/faqs/view/0688-7692-4D5A-1935)；垂直 FOV 定义参见 [Unity Camera.fieldOfView](https://docs.unity3d.com/cn/6000.0/ScriptReference/Camera-fieldOfView.html)。

## 自动化与人工画面检查

- 初始 RED：`TestResults/moba-view-red.xml`，10 项中 6 通过、4 按预期失败：旧小兵高度、缺失外围地形（两种宽高比）、旧塔/干员高度比。
- 完整 EditMode：`TestResults/moba-view-editmode.xml`，350/350 通过，无失败。
- 完整 PlayMode 首轮：`TestResults/moba-view-playmode.xml`，82/84。失败分别是旧部署高度断言，以及 Input System 包中 DefaultInputActions 路径变化。
- 修正旧高度断言为 1.2；输入资源检查改用保存场景与当前包资源共有的 GUID `ca9f5fa95ffab41fb9a615ab714db018`，继续严格检查同一资源身份，不依赖包内部目录结构。
- 完整 PlayMode 最终：`TestResults/moba-view-playmode-final.xml`，84/84 通过，无失败。使用 D3D11 图形设备。
- 新检查覆盖相机在 16:9/4:3 平移边界处的四角地面覆盖；小兵移动后的落地/悬浮；真实场景六名干员点选射线、血条顶部定位和死亡再部署后的模型/碰撞体落地。
- 实际场景渲染：`.superpowers/sdd/2026-10-04-moba-view/screenshots/` 下的 deployment、midlane、tower，各有 1080p 和 720p。已查看基地 1080p、塔前 1080p、兵线 720p，单位尺寸层级可辨，顶部与技能 HUD 可读。
- 截图由运行中的真实场景渲染；兵线和塔前单位为暂停后布置的位置，供对照构图，不是自然对局录像。截图时将屏幕覆盖 Canvas 暂时交给相机渲染，随后恢复。
- 独立只读代码复审未发现可执行问题；复审未代替运行测试或图像检查。

## 原场景与项目设置保护

- 原场景备份：`.superpowers/sdd/2026-10-04-moba-view/PrototypeArena.before.unity`。
- 原场景 SHA-256：`995492AF5DE3EB5C2363BB04A56697645E4CD9D78ECF338F155B265773891961`。
- 调整后 SHA-256：`B82882ED2575FB655953B5DB446DA6E427037FE2F27955A41912106FD5C6705A`。
- 相对本轮开始备份：164→172 个 YAML 对象块，原有对象删除数 0；15 个原有块变化均属于单位/塔变换与碰撞体、六席部署位置、相机、ArenaBootstrap 的新增子节点引用。详细记录在同目录 `scene-audit.json`。
- 没有重建或覆盖用户现有场景；已有材质、渲染设置、输入模块引用保持。Unity 测试自动移除的 `SENTIS_ANALYTICS_ENABLED` 设置已从本轮备份恢复。
- 改动保留在工作区。未重新打包原有 Windows 试玩包；体验新设置应运行当前 Unity 场景。
