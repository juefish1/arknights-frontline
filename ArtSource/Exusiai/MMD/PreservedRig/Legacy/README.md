# 能天使 MMD → Unity 验证版

## 结果

- 原包：1 个 PMX、21 张贴图、58,275 顶点、94,566 三角面、205 根骨骼、53 个表情、93 个刚体、126 个关节；没有动作文件。
- 清理后：94,556 三角面，53 个表情；Unity 中一个 SkinnedMeshRenderer、25 个 URP Lit 材质。
- 米制，脚底归零，头顶高度 1.65 米，含光环约 1.683 米；预制体朝向 Unity +Z。
- Unity Humanoid Avatar 的 isValid / isHuman 均为 true。
- 附带 3 秒循环 Exusiai_RigCheck，检查肘、膝和头部。它是新制作的验证动作，不是待机、走路或射击动作。
- Unity 中采样动画并烘焙蒙皮，测得顶点最大位移约 0.216 米；眨眼权重 0→100 的最大位移约 0.030 米。原始记录见 UnityImportValidation.txt。
- 修正了表情基准的缩放；眨眼坐标与源表情按比例缩放后的误差约 1.05e-7 米。

## 使用

在 Unity 中打开本资源的 Scenes/Exusiai_MMD_Preview.unity，然后进入 Play 模式。
也可把 Prefabs/Exusiai_MMD_Visual.prefab 拖入场景。Animator 当前使用验证控制器，接入玩法时替换为游戏控制器。
运行时文件位于项目 Assets/Game/Characters/Exusiai/MMD。

## 已做的转换

移除 MMD 专用约束和插件辅助骨骼，合并腿部 D 骨骼权重至主腿骨并整理层级。导出保留 205 根骨骼，英文名与源名称对照见 Export/manifest.json。
基础贴图映射为 URP Lit，使用双面与透明裁剪；球面高光、Toon 阴影、描边没有逐一复刻。

## 仍需后续制作

- 头发、裙摆的 MMD 物理未转换到 Unity；刚体和关节数据完整保留在 PMX。
- 已配置身体主骨骼的 Humanoid 映射；手指骨骼保留，但未配置手指的人形映射或握枪姿势。
- 没有外部动作包，尚未验证走路、射击、根运动和大幅度动作穿模。
- 没有武器挂点、LOD 或移动端性能优化；当前约 9.5 万三角面、25 材质。

## 验证环境与合并

本机使用 Unity 6000.5.9f1 / URP 17.5，在临时隔离工程完成导入、蒙皮、表情和实际渲染检查。
原工程使用 6000.3.15f1 / URP 17.3，本机没有对应编辑器，尚未在该版本复验。原工程没有升级。
本次新增独立 MMD 目录和美术源文件，未修改玩法脚本及工程配置。

合并时复制整个 Assets/Game/Characters/Exusiai/MMD 及 MMD.meta，保留目录内所有 .meta 文件。目标工程需要 URP。
ArtSource/Exusiai/MMD 为可选美术源文件、预览和验证记录；已有程序化原型继续保留。

## 文件

- Source/：原始压缩包解压文件，未修改；逐文件 SHA-256 见 archive_inventory.json。
- Exusiai_MMD_Source.blend：首次导入的网格、骨骼和表情；原始 PMX 仍是完整源数据。
- Exusiai_Unity.blend：整理后的模型，贴图已打包。
- Export/：FBX、GLB、基础贴图和导出清单。
- Previews/：Blender 中立姿势、关节测试、网页及 Unity 预览。
- Viewer/index.html：GLB 交互检查，复用上级 Viewer/vendor 的 Three.js。以 ArtSource/Exusiai 为本地服务根目录，访问 /MMD/Viewer/。
- Tools/：可复查的导入、转换和 Unity 验证脚本。

## 重建说明

Blender 4.5.3 + MMD Tools 4.5.14，插件来源校验见 mmd_tools_provenance.json。
import_audit.py 和 convert_model.py 通过 bpy 执行；插件与 OpenCC 的临时路径换机器后需调整。
Unity 验证脚本应放入隔离工程的 Assets/Editor，模型和贴图复制到上述资源路径，Export/manifest.json 复制为工程根目录的 mmd-manifest.json，执行 ExusiaiMmdImport.Build。
ExusiaiMmdRender.cs 仅供隔离工程截图，会创建临时 URP 配置，不应直接在正式工程执行。

原始作者署名与说明保留在 PMX 中，原压缩包仍在项目父目录。

### 2026-10-03：修复网页侧面头发遮挡

GLB 原本将材质统一导出为 BLEND，导致头皮、发片等表面随视角发生透明排序错误。
现将 EXM 材质改为与 Unity 相同的双面 MASK（阈值 0.3），并在转换脚本中加入导出后处理，避免重新导出时复发。
已对照检查脸部与侧面预览；GLB 二进制数据完全一致，仅材质透明设置改变，骨骼、动作、贴图和几何均保留。
Unity 材质原本已使用双面透明裁剪，本次无需修改。
