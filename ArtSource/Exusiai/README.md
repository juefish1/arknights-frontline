> 新增：购买的 MMD 模型已完成独立 Unity 导入与基础骨骼验证，见 [MMD 资源说明](MMD/README.md)。下文仍描述早期静态原型。

# 能天使 · 初版静态模型

依据本次提供的能天使立绘制作，目标是可继续修改、可独立并入 Unity 项目的第一版三维外观。

## 制作范围与计划

- [x] 建立约 1.65 米高的日式角色比例、白黑制服、深色裙装与长袜、短靴。
- [x] 制作红色短发、遮眼刘海、琥珀色眼睛、光环、光翼与配枪。
- [x] 保存可编辑 Blender 文件并导出 FBX、GLB。
- [x] 检查正面、背面、脸部与三分之四渲染；检查导出文件的几何与材质。
- [x] 准备独立的 Unity 材质与外观 Prefab，记录导入验证结果。

## 资源边界

Unity 资源位于 `Assets/Game/Characters/Exusiai/`；建模脚本、Blender 源文件、预览和说明位于 `ArtSource/Exusiai/`。保留所有随附 `.meta` 文件一起迁移。

角色采用自然垂臂的展示姿势，服装背面根据正面风格补全。本版为静态造型，尚无骨骼权重或战斗动画；眼睛、头发、光环、羽翼、枪械使用独立网格，便于后续修改。

Blender 源文件使用 Z 向上、角色面朝 -Y；Unity Prefab 使用 Y 向上、面朝 +Z，脚底已按真实网格顶点对齐地面。整体含光环高约 1.729 米、光翼宽约 0.927 米。

## 查看和使用

- Unity：将 `Assets/Game/Characters/Exusiai/Prefabs/Exusiai_Visual.prefab` 拖入场景。这是纯外观 Prefab，可作为玩法角色的子物体使用；角色尺寸由父层适配。
- Blender：打开 `Exusiai_Prototype.blend`，源文件保留 288 个可分别编辑的零件以及预览灯光、相机。
- 浏览器：在项目根目录运行 `python3 -m http.server 8765 --bind 127.0.0.1 --directory ArtSource/Exusiai`，打开 `http://127.0.0.1:8765/Viewer/`。支持拖动旋转、滚轮缩放、预设视角、线框和光翼显示切换。查看器依赖已随文件保存，无需外网。
- 预览图片：`Previews/three_quarter.png`、`front.png`、`back.png`、`face.png`。

## 验证与当前限制

FBX 在独立的 Unity 6000.5.9f1 / URP 17.5.0 项目中完成导入、材质映射、Prefab 保存和重载验证。结果见 `UnityImportValidation.txt`。本项目指定的 Unity 6000.3.15f1 不在本机安装，因此该精确版本尚未实机验证；使用的 URP Lit 着色器 GUID 与当前项目的 URP 17.3.0 一致，项目配置未升级。

导出资源按用途与材质合并为 33 个网格、23 个材质；GLB 约 12.1 万三角面，Unity 导入时会移除少量退化面。当前优先验证造型，后续用于大量同屏单位前应继续减面、制作 LOD 和合并材质。颜色来自独立材质；已有基础 UV，尚未制作手绘贴图或卡通专用着色器。头发、面部、手部与衣褶仍是初步造型，背面属于根据参考补全的设计。

`Tools/build_exusiai.py` 可在 Blender 4.5 中重建模型与预览；它会覆盖本目录的生成模型以及 Unity FBX，手工细修后应先另存再运行。`Tools/ExusiaiImportValidation.cs` 是临时验证项目使用的编辑器工具，不参与主项目编译。

## 合并范围

一起迁移 `Assets/Game/Characters.meta`、`Assets/Game/Characters/` 和 `ArtSource/Exusiai/`，保留 `.meta` 的 GUID。上述路径目前尚未提交 Git。查看器使用 Three.js 0.180.0，许可证位于 `Viewer/vendor/THREE-LICENSE.txt`。
