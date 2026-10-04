# 能天使：保留 MMD 原骨骼的 Unity 版本

## 当前默认版本（2026-10-04）

默认预制体与预览场景现使用 **原 MMD 骨骼 + 已烘焙 Generic 动画**。
原始 PMX 有 205 根骨骼，MMD Tools 导入后另有 48 根求解辅助骨骼，共 253 根。新版本保留它们的名称、父子层级、静止矩阵和原顶点权重，未再合并腿部 D 骨骼权重。之前的简化 Humanoid FBX 与动画仍保留作对照，不再由默认预制体使用。

- 可编辑源文件：`PreservedRig/Exusiai_MMD_Preserved.blend`，保留全部 64 个导入约束及测试控制动画。
- 导出副本：`PreservedRig/Exusiai_MMD_Baked.blend`，先求解、逐帧记录骨骼运动，再移除约束，以便 Unity 播放。
- 3 秒测试动作包含双侧抬脚 IK、肘部弯曲、头部旋转、上臂和前臂扭转。它不是游戏待机、行走或射击动作。
- Unity 使用 Generic Avatar；原 53 个表情全部保留，另有 3 个 MMD SDEF 数据用 shape key，不作为表情使用。
- 单个 SkinnedMeshRenderer，25 个已有 URP 双面透明裁剪材质；保留侧面头发修复。
- 只通过模型外层 Transform 缩放至身体高 1.65 米，脚底归零，含光环约 1.683 米；不改骨骼绑定数据。

## 验证结果

| 检查 | 结果 |
| --- | --- |
| 91 帧：原约束求解 vs Blender 烘焙 | 最大顶点差 0.000861 毫米 |
| 烘焙骨骼 | 最大位置差 0.000734 毫米，最大角度差约 0.112°（含浮点测量误差） |
| 91 帧：FBX 导出再导入 | 双向最近点最大差 1.845 毫米；逐帧 99 分位最大 0.00844 毫米 |
| 31 次 Unity 实际动画蒙皮采样 | 双向最近点最大差 0.01691 毫米；逐帧 99 分位最大 0.00692 毫米 |
| 结构与表情 | Unity 内 253 根导入骨骼完整，原 53 个表情全部存在；Generic Avatar 有效 |
| 动画 / 眨眼实际变形 | 最大位移分别约 0.157 米 / 0.0305 米 |
| 负向检查 | 旧转换结构不一致被检出；人为平移 20 毫米被表面校验检出 |

表面门限为最大 5 毫米、99 分位 1 毫米。FBX 往返会更改顶点顺序，Unity 会因 UV/法线拆点，因此使用双向最近点距离，不把顶点编号当作跨格式对应关系。Blender 烘焙验证另外使用同编号逐顶点距离。Unity 坐标映射经静止姿势核实为 Blender 世界坐标 `(-x, z, -y)`；未对动画逐帧进行拟合或缩放来消除误差。

这些结果说明**所测动作与 MMD Tools 导入后的约束求解近似一致**。尚未与原生 MMD 播放器逐帧对照，也不能据此保证任意复杂动作都等价。

## 使用

- 打开 `Assets/Game/Characters/Exusiai/MMD/Scenes/Exusiai_MMD_Preview.unity`，进入 Play 查看测试。
- 默认角色：`Assets/Game/Characters/Exusiai/MMD/Prefabs/Exusiai_MMD_Visual.prefab`。
- 原骨骼 FBX、原始比例子预制体及测试动画位于其下 `PreservedRig/`。Animator 在子对象上。
- 默认预制体和场景的 `.meta` GUID 保持不变，但内部层级与骨骼路径已经改变；后续绑定挂点时使用新名称。
- 网页 `Viewer/index.html` 已切换到新骨骼的 GLB，保留透明裁剪和表情检查。

## 边界与后续

Unity 播放的是约束求解后烘焙的骨骼曲线，不包含运行时 MMD IK 求解器。以后制作/导入动作，应在保留约束的源文件中求解，再逐帧烘焙导出。Generic 也不直接提供 Humanoid 动作重定向。

头发、裙摆物理尚未迁移；PMX 原有 93 个刚体、126 个关节仍保存在源文件中。未验证复杂动作、根运动、射击握枪与大幅运动穿模。Toon、球面高光、描边仍未完整复刻。

## 环境与合并

Blender/bpy 4.5.3、MMD Tools 官方标签 v4.5.14（来源及校验见 `PreservedRig/environment.json`）。
Unity 验证使用独立临时项目：6000.5.9f1 / URP 17.5；正式项目为 6000.3.15f1 / URP 17.3，本机没有该编辑器，未在该版本复验，正式项目未升级。

合并复制 `Assets/Game/Characters/Exusiai/MMD`、`MMD.meta` 及目录内所有 `.meta`。`ArtSource/Exusiai/MMD` 是可选源文件、工具、记录和网页预览。现有玩法脚本、Packages 和 ProjectSettings 未在本次改动。

## 重建与审计

1. 准备 Python 3.11、bpy 4.5.3、NumPy、SciPy；安装官方 MMD Tools v4.5.14 及其 OpenCC 依赖。
2. 将 `PreservedRig/environment.json` 中的插件路径改为本机路径，设置 `EXUSIAI_ENV` 指向该环境文件。
3. 运行 `Tools/mmd_preserved_rig.py` 生成审计，带 `--build` 生成约束源文件、逐帧参考和烘焙 FBX；运行 `Tools/check_preserved_rig.py` 做 FBX 往返检查。
4. 隔离 Unity 工程中复制现有 MMD 资源，将新 FBX 放到 `MMD/PreservedRig/Models/`；将 `Export/manifest.json` 放到工程根目录并命名 `mmd-manifest.json`；将 `ExusiaiPreservedImport.cs` 放到 `Assets/Editor`，执行 `ExusiaiPreservedImport.Build`。输出 `preserved-unity.json` 和 `preserved-vertices.bin`。
5. `check_preserved_rig.py --unity` 默认读取 `/private/tmp/exusiai-preserved-unity`；换机器时调整该路径。只有所有门限通过后执行 `ExusiaiPreservedFinalize.Build` 更新默认预制体和场景。
6. `export_preserved_preview.py` 生成网页 GLB，`ExusiaiPreservedRender.Start` 在隔离 Unity 工程生成截图。截图工具会创建临时 URP 配置，不在正式工程执行。

`PreservedRig/reference.npz` 保存 91 帧参考骨骼和顶点，JSON 报告记录详细误差。`source_snapshot.json` 记录原层级、约束和权重哈希。`Legacy/` 保存替换前的说明、默认预制体及场景；旧 FBX/动画原路径未删除。`Source/` 与首次导入 `.blend` 未修改。
