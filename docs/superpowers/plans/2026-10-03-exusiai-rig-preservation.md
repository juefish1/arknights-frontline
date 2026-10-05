# Exusiai 原 MMD 骨骼保留转换 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 从原始 MMD 导入文件重新生成使用原骨骼、原层级、原权重的 Unity 资源，以逐帧烘焙保留骨骼约束的运动效果，并用原模型与导出模型的表面位置误差判断是否近似等价。

**Architecture:** 以 Exusiai_Source.blend 为约束求解参考，保留原 PMX 为完整来源；先制作小范围控制动作，再逐帧记录求解后的骨骼姿势与表面顶点。仅在导出副本中把求解结果烘焙成普通骨骼动画，然后用 Unity Generic 保持原骨骼结构。通过 Blender FBX 回读与 Unity 蒙皮采样两层验证后，再替换现有简化版本；原源文件和旧版资源先保留。

**Tech Stack:** Blender bpy 4.5.3、MMD Tools 4.5.14、Python 3.11、FBX、Unity 6000.5.9f1 / URP 17.5（隔离验证）；正式工程 Unity 6000.3.15f1 / URP 17.3。

## Global Constraints

- 工作区：`/Users/ynie/Documents/Hobby/Game/arknights-frontline`，下文命令从此目录执行。
- 原文件：`ArtSource/Exusiai/RigSource/Source/能天使.pmx`、`Exusiai_Source.blend` 均不覆盖。
- 不新建人体骨架；保留源文件的骨骼名称、父子关系、休止姿势和所有已有蒙皮权重。不合并腿部 D 骨权重，不重新计算权重。
- 保留源 Blender 的辅助骨和约束（包括插件内部 SDEF shape key，实际表情仍是 PMX 的 53 项）；只允许在导出副本完成视觉烘焙后清除约束，且每项约束必须出现在审计记录中。
- 首版使用 Generic。Humanoid 重定向和运行时 IK 不作为本计划的等价性依据，也不默认加入当前版本。
- 比较基准是 MMD Tools 导入后的求解结果，不宣称已经与 MMD 原生播放器逐帧一致。
- PMX 包含 205 根原骨、53 个顶点表情、93 个刚体、126 个关节；Blender 可能额外生成辅助骨，不能把 Blender 总骨数强制设为 205。
- 头发、裙摆物理另属动态系统；本计划保留骨骼与原始物理数据，但不宣称恢复物理仿真。
- 基础贴图与透明裁剪保持已有修复：MASK、双面、alphaCutoff 0.3；Unity 同样采用双面裁剪。
- 不升级正式工程，不改 Packages、ProjectSettings 或玩法脚本。保留所有现有 .meta GUID。
- 临时 Python/bpy 环境已经不存在，执行时必须重建；本次仅写计划，不安装运行环境。
- 所有示例动画名称含 RigCheck，明确标注测试用途；原包没有正式动作。
- 每次提交只包含本任务明确列出的文件，不使用 git add .；若已有未提交美术文件先明确其归属，避免把之前工作混入提交。

---

## 选择与交付边界

| 路径 | 优点 | 代价 | 本计划选择 |
|---|---|---|---|
| 原骨骼＋求解后烘焙＋Generic | 保留层级和权重；不依赖 Unity 理解 MMD 约束 | 新动作每次需烘焙；不提供运行时 MMD 控制器 | 主路径 |
| 原骨骼＋Unity 运行时重建 IK/继承约束 | 可以动态调脚、手和辅助骨 | 需要逐项实现约束顺序与坐标换算 | 独立后续工作 |
| 重新绑定 Humanoid 主骨 | 容易接通通用人形动画 | 可能改变原辅助骨关系和变形 | 不用于本次替换 |

“移除现版本”指最终让预制体使用重新转换的原骨骼版本；不是直接删除已有文件。当前转换版没有另造人体骨架，问题在于约束清空、层级修改和权重合并。

## 文件结构

新增目录统一使用 `ArtSource/Exusiai/RigSource/PreservedRig/`：

- `source_snapshot.json`：约束、原名、层级、休止矩阵、权重和表情清单。
- `reference.json`：0–90 帧的骨骼矩阵及求解后的网格坐标，记录坐标单位。
- `Exusiai_Rig_Preserved.blend`：原控制骨及约束保留的可编辑工作副本。
- `Exusiai_Rig_Baked.blend`：求解后烘焙的导出副本。
- `Exusiai_OriginalRig.fbx`：供 Unity Generic 骨骼导入。
- `conversion_report.json`：误差与不支持项，必须输出 passed 布尔值。

新增工具：`Tools/preserved_rig.py`（审计、测试动作、烘焙、导出）、`Tools/check_preserved_rig.py`（FBX 回读）、`Tools/ExusiaiPreservedRigValidation.cs`（Unity 验证）。
新增验证资源路径：`Assets/Game/Characters/Exusiai/Model/PreservedRig/`，包含 Models、Animations、Prefabs、Scenes。执行前先检查该目录是否已经存在，存在则保留 GUID 并明确其内容来源。
现有 `convert_model.py` 保留为旧版转换工具；新路径不调用它的清约束、改层级、合并权重操作。

### Task 1: 建立原骨骼与约束基准

**Files:**
- Create: `ArtSource/Exusiai/RigSource/Tools/preserved_rig.py`
- Create: `ArtSource/Exusiai/RigSource/PreservedRig/source_snapshot.json`
- Test: `/private/tmp/exusiai-preserved-rig/test_source.py`

**Interfaces:**
- Consumes: `Exusiai_Source.blend`、原始 PMX 审计和 MMD Tools。
- Produces: `snapshot(arm, mesh) -> dict`；JSON 包含 bones、weights、shapes、constraints。

- [ ] **Step 1: 恢复可重复运行环境。** 使用 uv 创建 `/private/tmp/exusiai-preserved-rig/venv`，安装 `bpy==4.5.3`。从官方仓库获取 MMD Tools，读取 `blender_manifest.toml` 确认版本 4.5.14；若官方分支已变化，不自动采用新版，查找对应标签或提交并把来源、版本和 SHA-256 写入环境记录。解压随插件提供的 OpenCC wheel，不能直接从压缩 wheel 路径读取字典。随后设置 `EXUSIAI_MMD_TOOLS` 和 `EXUSIAI_OPENCC` 为实际解压路径。命令：

```bash
uv venv --python 3.11 /private/tmp/exusiai-preserved-rig/venv
uv pip install --python /private/tmp/exusiai-preserved-rig/venv/bin/python bpy==4.5.3
```

预期：import bpy 成功且 bpy.app.version 为 (4, 5, 3)。macOS bpy 若被沙盒阻止，用获准的提权执行；不更改系统 Blender。

- [ ] **Step 2: 先建立反例。** 测试脚本以环境变量里的插件路径注册 MMD Tools，打开原始 .blend 后获取快照，再打开现有简化 .blend；断言快照相同。以下代码应在旧简化版本失败：

```python
import bpy, sys, os
from pathlib import Path
sys.path[:0] = [os.environ['EXUSIAI_MMD_TOOLS'], os.environ['EXUSIAI_OPENCC']]
import mmd_tools
mmd_tools.register()
sys.path.insert(0, str(Path.cwd() / 'ArtSource/Exusiai/RigSource/Tools'))
from preserved_rig import snapshot
r = Path.cwd() / 'ArtSource/Exusiai/RigSource'
def load(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    return snapshot(next(o for o in bpy.data.objects if o.type == 'ARMATURE'),
                    next(o for o in bpy.data.objects if o.type == 'MESH'))
a = load(r / 'Exusiai_Source.blend')
b = load(r / 'Exusiai_Unity.blend')
assert a == b, 'Old conversion changed constraints, hierarchy or weights'
```

- [ ] **Step 3: 写入工具的完整审计函数。**

```python
import bpy, json, os, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'PreservedRig'
def matrix(m):
    return [list(row) for row in m]
def snapshot(arm, mesh):
    constraints = []
    for p in arm.pose.bones:
        for c in p.constraints:
            properties = {}
            for prop in c.bl_rna.properties:
                if prop.identifier == 'rna_type' or prop.type == 'COLLECTION':
                    continue
                value = getattr(c, prop.identifier)
                if prop.type == 'POINTER':
                    properties[prop.identifier] = getattr(value, 'name', None)
                elif prop.is_array:
                    properties[prop.identifier] = list(value)
                elif isinstance(value, (str, bool, int, float)):
                    properties[prop.identifier] = value
            constraints.append({'bone': p.name, 'settings': properties})
    return {
        'bones': [{'name': b.name, 'parent': b.parent.name if b.parent else None,
                   'rest': matrix(b.matrix_local)} for b in arm.data.bones],
        'weights': [[(mesh.vertex_groups[g.group].name, g.weight)
                     for g in v.groups] for v in mesh.data.vertices],
        'shapes': [k.name for k in mesh.data.shape_keys.key_blocks],
        'constraints': constraints,
    }
def bootstrap():
    sys.path[:0] = [os.environ['EXUSIAI_MMD_TOOLS'], os.environ['EXUSIAI_OPENCC']]
    import mmd_tools
    mmd_tools.register()
def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False), encoding='utf-8')
```

- [ ] **Step 4: 运行反例并生成基准。** 在临时测试脚本中将最后两行替换为 `assert a['constraints']; write(OUT / 'source_snapshot.json', a)`，并导入 OUT、write。执行：

```bash
/private/tmp/exusiai-preserved-rig/venv/bin/python /private/tmp/exusiai-preserved-rig/test_source.py
```

预期：旧版快照比较失败；源快照生成成功并存在非空约束。若约束或 driver 在注册插件后仍缺失，停止烘焙，从原 PMX 重新导入 mesh/armature/morphs 后再建立基准；不凭骨数推断恢复成功。

- [ ] **Step 5: 独立提交审计工具与基准。**

```bash
git add ArtSource/Exusiai/RigSource/Tools/preserved_rig.py ArtSource/Exusiai/RigSource/PreservedRig/source_snapshot.json
git commit -m "test: capture original Exusiai MMD rig baseline"
```

### Task 2: 在原控制结构下求解、烘焙和导出

**Files:**
- Modify: `ArtSource/Exusiai/RigSource/Tools/preserved_rig.py`
- Create: `ArtSource/Exusiai/RigSource/PreservedRig/reference.json`
- Create: `ArtSource/Exusiai/RigSource/PreservedRig/Exusiai_Rig_Preserved.blend`
- Create: `ArtSource/Exusiai/RigSource/PreservedRig/Exusiai_Rig_Baked.blend`
- Create: `ArtSource/Exusiai/RigSource/PreservedRig/Exusiai_OriginalRig.fbx`

**Interfaces:**
- Consumes: snapshot、源 rig；原控制骨名。
- Produces: `build() -> None`，所有源骨逐帧视觉烘焙；reference.json 的 poses / vertices 均为世界坐标、米制。

- [ ] **Step 1: 验证必须存在的控制骨。** 临时脚本打开源文件后断言 `右足ＩＫ`、`左足ＩＫ`、`右ひじ`、`左ひじ`、`頭` 均存在。读取 source_snapshot 的 IK 约束设置，确认左右腿 IK 启用、目标存在且 influence 非零；若目标或 IK 启用状态缺失，回到源导入纠正，不用直接转动腿骨替代 IK 验证。

- [ ] **Step 2: 将下列完整函数追加至 preserved_rig.py。** 不调用旧转换脚本，不复制旧版的骨骼变更。

```python
from math import radians

def evaluated_vertices(mesh):
    graph = bpy.context.evaluated_depsgraph_get()
    obj = mesh.evaluated_get(graph)
    data = obj.to_mesh()
    result = [list(obj.matrix_world @ v.co) for v in data.vertices]
    obj.to_mesh_clear()
    return result

def build():
    bootstrap()
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'Exusiai_Source.blend'))
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    mesh = next(o for o in bpy.data.objects if o.type == 'MESH')
    baseline = snapshot(arm, mesh)
    assert baseline['constraints'], 'Source constraints absent'
    write(OUT / 'source_snapshot.json', baseline)
    scene = bpy.context.scene
    scene.render.fps = 30
    scene.frame_start, scene.frame_end = 0, 90
    arm.animation_data_clear()
    # IK inputs and joint inputs are deliberately separate from the baked result.
    for name, axis, amount, channel in [
        ('左足ＩＫ', 2, .06, 'location'), ('右足ＩＫ', 2, .04, 'location'),
        ('左ひじ', 0, radians(-25), 'rotation_euler'),
        ('右ひじ', 0, radians(-25), 'rotation_euler'),
        ('頭', 1, radians(12), 'rotation_euler')]:
        p = arm.pose.bones[name]
        p.rotation_mode = 'XYZ'
        original = getattr(p, channel).copy()
        for frame, factor in [(0, 0), (30, 1), (60, .4), (90, 0)]:
            value = original.copy()
            value[axis] += amount * factor
            setattr(p, channel, value)
            p.keyframe_insert(channel, frame=frame, group=name)
    arm.animation_data.action.name = 'Exusiai_OriginalRigCheck'
    scene.frame_set(0)
    assert snapshot(arm, mesh) == baseline
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Exusiai_Rig_Preserved.blend'))
    reference = []
    for frame in range(91):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        evaluated_arm = arm.evaluated_get(bpy.context.evaluated_depsgraph_get())
        reference.append({'frame': frame,
            'poses': {p.name: matrix(evaluated_arm.matrix_world @ p.matrix)
                      for p in evaluated_arm.pose.bones},
            'vertices': evaluated_vertices(mesh)})
    write(OUT / 'reference.json', {'unit': 'meter', 'fps': 30, 'samples': reference})
    scene.frame_set(0)
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.select_all(action='SELECT')
    result = bpy.ops.nla.bake(frame_start=0, frame_end=90, step=1,
        only_selected=True, visual_keying=True, clear_constraints=True,
        clear_parents=False, use_current_action=False, bake_types={'POSE'})
    assert 'FINISHED' in result
    bpy.ops.object.mode_set(mode='OBJECT')
    arm.animation_data.action.name = 'Exusiai_OriginalRigCheck_Baked'
    baked_snapshot = snapshot(arm, mesh)
    for field in ('bones', 'weights', 'shapes'):
        assert baked_snapshot[field] == baseline[field], field
    for frame in range(91):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        evaluated_arm = arm.evaluated_get(bpy.context.evaluated_depsgraph_get())
        for p in evaluated_arm.pose.bones:
            from mathutils import Matrix
            expected = Matrix(reference[frame]['poses'][p.name])
            actual = evaluated_arm.matrix_world @ p.matrix
            assert (actual.translation - expected.translation).length <= .001, p.name
            assert actual.to_quaternion().rotation_difference(
                expected.to_quaternion()).angle <= radians(1), p.name
    scene.frame_set(0)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Exusiai_Rig_Baked.blend'))
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    mesh.select_set(True)
    # Include the existing model root so parent-space transforms remain intact.
    for obj in (arm, mesh):
        parent = obj.parent
        while parent:
            parent.select_set(True)
            parent = parent.parent
    bpy.ops.export_scene.fbx(filepath=str(OUT / 'Exusiai_OriginalRig.fbx'),
        use_selection=True, object_types={'ARMATURE', 'MESH', 'EMPTY'},
        add_leaf_bones=False, use_armature_deform_only=False,
        use_mesh_modifiers=False, bake_anim=True, bake_anim_step=1,
        bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False, bake_anim_simplify_factor=0,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y')
    print('PRESERVED_RIG_EXPORT_OK', flush=True)

if __name__ == '__main__':
    build()
    os._exit(0)
```

- [ ] **Step 3: 运行导出。**

```bash
/private/tmp/exusiai-preserved-rig/venv/bin/python ArtSource/Exusiai/RigSource/Tools/preserved_rig.py
```

预期：`PRESERVED_RIG_EXPORT_OK`；Preserved 文件有原约束，Baked 文件中骨骼/权重/表情快照不变，全部骨骼在 91 帧的位置误差不超过 1 mm、旋转误差不超过 1°。若失败，保留失败骨名/帧号，核对 driver、旋转继承顺序与烘焙覆盖；不得放宽阈值后直接宣称等价。

- [ ] **Step 4: 提交工具和较小审计文件。** 暂不提交庞大的 reference.json；它是本机验证产物，最终保存摘要报告。

```bash
git add ArtSource/Exusiai/RigSource/Tools/preserved_rig.py ArtSource/Exusiai/RigSource/PreservedRig/source_snapshot.json
git commit -m "feat: bake Exusiai original MMD rig without rebinding"
```

### Task 3: 用 FBX 回读检验真实表面误差

**Files:**
- Create: `ArtSource/Exusiai/RigSource/Tools/check_preserved_rig.py`
- Create: `ArtSource/Exusiai/RigSource/PreservedRig/conversion_report.json`

**Interfaces:**
- Consumes: reference.json、Exusiai_OriginalRig.fbx。
- Produces: passed、max_surface_error_m、p99_surface_error_m、weighted_bone_names；失败返回非零。

- [ ] **Step 1: 先验证表面差异指标。** 在临时脚本中构造坐标整体偏移 0.02 m，执行下述 surface_error，断言最大距离大于 0.005。该反例必须被判为失败。索引不能直接比较：FBX 会拆分 UV/法线接缝，用双向最近表面顶点距离避免把索引变化当作误差。

- [ ] **Step 2: 写入完整回读检查。**

```python
import bpy, json, os
import numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.kdtree import KDTree
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'PreservedRig'
def surface_error(a, b):
    distances = []
    for source, target in ((a, b), (b, a)):
        tree = KDTree(len(target))
        for i, xyz in enumerate(target):
            tree.insert(Vector(xyz), i)
        tree.balance()
        distances.extend(tree.find(Vector(xyz))[2] for xyz in source)
    return max(distances), float(np.percentile(distances, 99))
def run():
    reference = json.loads((OUT / 'reference.json').read_text())
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(OUT / 'Exusiai_OriginalRig.fbx'),
                             use_anim=True, automatic_bone_orientation=False)
    mesh = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
    arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    assert arm.animation_data and arm.animation_data.action
    start = arm.animation_data.action.frame_range[0]
    max_error, p99_error = 0.0, 0.0
    for sample in reference['samples']:
        bpy.context.scene.frame_set(int(start + sample['frame']))
        graph = bpy.context.evaluated_depsgraph_get()
        obj = mesh.evaluated_get(graph)
        data = obj.to_mesh()
        actual = [list(obj.matrix_world @ v.co) for v in data.vertices]
        obj.to_mesh_clear()
        maximum, p99 = surface_error(sample['vertices'], actual)
        max_error, p99_error = max(max_error, maximum), max(p99_error, p99)
    names = sorted({mesh.vertex_groups[g.group].name
                    for v in mesh.data.vertices for g in v.groups if g.weight > 0})
    report = {'passed': max_error <= .005 and p99_error <= .001,
              'max_surface_error_m': max_error, 'p99_surface_error_m': p99_error,
              'weighted_bone_names': names, 'frames': 91,
              'physics_reconstructed': False}
    (OUT / 'conversion_report.json').write_text(json.dumps(report, indent=2))
    print(report, flush=True)
    os._exit(0 if report['passed'] else 1)
if __name__ == '__main__':
    run()
```

- [ ] **Step 3: 运行完整比较。**

```bash
/private/tmp/exusiai-preserved-rig/venv/bin/python ArtSource/Exusiai/RigSource/Tools/check_preserved_rig.py
```

预期 passed=true。该检查是密集顶点的近似表面距离，不是数学上逐顶点等价的证明。另须用源权重快照确认没有修改绑定；观看肘、膝、肩、头和脚抬起的视频或关键帧。若 SDEF/QDEF 与 Unity LBS 的差异超标，报告对应区域，调查局部近似方案；不把顶点缓存动画或重绑骨骼偷偷加入本计划。

- [ ] **Step 4: 提交回读检查与摘要报告。**

```bash
git add ArtSource/Exusiai/RigSource/Tools/check_preserved_rig.py ArtSource/Exusiai/RigSource/PreservedRig/conversion_report.json
git commit -m "test: compare baked Exusiai FBX against source deformation"
```

### Task 4: 在 Unity 验证 Generic 和辅助骨跟随

**Files:**
- Create: `ArtSource/Exusiai/RigSource/Tools/ExusiaiPreservedRigValidation.cs`
- Create: `Assets/Game/Characters/Exusiai/Model/PreservedRig/Models/Exusiai_OriginalRig.fbx`
- Create: `Assets/Game/Characters/Exusiai/Model/PreservedRig/Animations/Exusiai_OriginalRigCheck.controller`
- Create: `Assets/Game/Characters/Exusiai/Model/PreservedRig/Prefabs/Exusiai_Preserved_Rig.prefab`
- Create: `ArtSource/Exusiai/RigSource/PreservedRig/UnityValidation.txt`

**Interfaces:**
- Consumes: 通过 Task 3 的 FBX；现有 EXM_00–EXM_24 材质。
- Produces: Generic Animator prefab、0–3 秒动作；世界坐标下网格导出以供与参考点云比较。

- [ ] **Step 1: 准备隔离工程。** 查看 `/Applications/Unity/Hub/Editor`，优先用已安装的 6000.3.15f1；若只有 6000.5.9f1，使用该版本并明确记录。隔离工程固定 `/private/tmp/exusiai-preserved-unity`，URP 版本匹配编辑器，不复制正式 ProjectSettings。将现有整个 MMD 资源及 .meta 复制进去，添加新 FBX，工具只放隔离工程 Assets/Editor。创建 Models/Animations/Prefabs 目录后让 Unity 生成元数据。

- [ ] **Step 2: 编写以下完整入口。** 每个蒙皮都验证骨引用，对全长每 0.1 秒采样，而不是只证明“某个顶点动了”。

```csharp
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEditor;
using UnityEditor.Animations;
public static class ExusiaiPreservedRigValidation {
 const string R="Assets/Game/Characters/Exusiai/Model";
 const string P=R+"/PreservedRig";
 public static void Build(){
  try { Run(); EditorApplication.Exit(0); }
  catch(Exception e){ Debug.LogException(e); EditorApplication.Exit(1); }
 }
 static void Run(){
  string path=P+"/Models/Exusiai_OriginalRig.fbx";
  var importer=(ModelImporter)AssetImporter.GetAtPath(path);
  importer.animationType=ModelImporterAnimationType.Generic;
  importer.importAnimation=true; importer.importBlendShapes=true;
  importer.animationCompression=ModelImporterAnimationCompression.Off;
  importer.isReadable=true;
  for(int i=0;i<25;i++){
   string name="EXM_"+i.ToString("00");
   var m=AssetDatabase.LoadAssetAtPath<Material>(R+"/Materials/"+name+".mat");
   if(!m)throw new Exception("Missing material "+name);
   // Material slots in the original blend retain their original names.
   // First remap with the material names exposed by this importer, in slot order.
  }
  importer.SaveAndReimport();
  var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
  var go=(GameObject)PrefabUtility.InstantiatePrefab(model);
  var skins=go.GetComponentsInChildren<SkinnedMeshRenderer>();
  if(skins.Length!=1)throw new Exception("Unexpected mesh split");
  var slots=skins[0].sharedMaterials;
  if(slots.Length!=25)throw new Exception("Unexpected material slots");
  for(int i=0;i<25;i++){
   var material=AssetDatabase.LoadAssetAtPath<Material>(R+"/Materials/EXM_"+i.ToString("00")+".mat");
   importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),slots[i].name),material);
  }
  UnityEngine.Object.DestroyImmediate(go);
  importer.SaveAndReimport();
  go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
  skins=go.GetComponentsInChildren<SkinnedMeshRenderer>();
  if(skins.Any(s=>s.bones.Any(b=>!b)))throw new Exception("Missing bone");
  if(skins[0].sharedMesh.blendShapeCount<53)throw new Exception("Missing expressions");
  var animator=go.GetComponent<Animator>();
  var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
      .Single(c=>!c.name.StartsWith("__preview__"));
  if(clip.humanMotion||clip.length<2.99f)throw new Exception("Expected full Generic clip");
  var graph=PlayableGraph.Create("SourceRigVerification");
  graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
  var playable=AnimationClipPlayable.Create(graph,clip);
  var output=AnimationPlayableOutput.Create(graph,"Pose",animator);
  output.SetSourcePlayable(playable);animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;graph.Play();
  var mesh=new Mesh(); Directory.CreateDirectory("reference-unity");
  for(int step=0;step<=30;step++){
   playable.SetTime(step*.1);graph.Evaluate(0);
   skins[0].BakeMesh(mesh);
   var lines=mesh.vertices.Select(v=>{
    var w=skins[0].transform.TransformPoint(v);
    // Unity (x,y,z) -> Blender (x,-z,y), before normalization or prefab placement.
    return string.Join(",",new[]{w.x,-w.z,w.y}.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));
   });
   File.WriteAllLines("reference-unity/"+(step*3).ToString("000")+".csv",lines);
  }
  playable.SetTime(0);graph.Evaluate(0);graph.Destroy();
  var controller=AnimatorController.CreateAnimatorControllerAtPath(P+"/Animations/Exusiai_OriginalRigCheck.controller");
  controller.layers[0].stateMachine.AddState("OriginalRigCheck").motion=clip;
  animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
  PrefabUtility.SaveAsPrefabAsset(go,P+"/Prefabs/Exusiai_Preserved_Rig.prefab");
  File.WriteAllText("UnityValidation.txt","GENERIC_IMPORT_OK\nUnity "+Application.unityVersion+"\nSamples 31\nBlendshapes "+skins[0].sharedMesh.blendShapeCount+"\n");
  AssetDatabase.SaveAssets();
 }
}
```

重复执行前，若 controller 已存在，使用 LoadAssetAtPath 读取并复用，禁止通过 DeleteAsset 重建已有 GUID。首次创建与复用的替换代码：

```csharp
var controllerPath=P+"/Animations/Exusiai_OriginalRigCheck.controller";
var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
var machine=controller.layers[0].stateMachine;
var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="OriginalRigCheck");
if(!state)state=machine.AddState("OriginalRigCheck");
state.motion=clip;
```

- [ ] **Step 3: 执行 Unity 检查。** 在隔离目录运行：

```bash
/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /private/tmp/exusiai-preserved-unity -executeMethod ExusiaiPreservedRigValidation.Build -logFile /private/tmp/exusiai-preserved-unity.log
```

预期 GENERIC_IMPORT_OK。将输出 CSV 与 reference.json 对应帧比较，复用 Task 3 已定义的 surface_error。以下独立脚本从工程根运行，输出阈值检查：

```python
import sys,json,numpy as np
from pathlib import Path
sys.path.insert(0,str(Path.cwd()/'ArtSource/Exusiai/RigSource/Tools'))
from check_preserved_rig import surface_error
root=Path.cwd()/'ArtSource/Exusiai/RigSource/PreservedRig'
reference=json.loads((root/'reference.json').read_text())['samples']
maximum,p99=0.,0.
for step in range(31):
    frame=step*3
    vertices=np.loadtxt(f'/private/tmp/exusiai-preserved-unity/reference-unity/{frame:03d}.csv',delimiter=',')
    a,b=surface_error(reference[frame]['vertices'],vertices)
    maximum,p99=max(maximum,a),max(p99,b)
print({'max_m':maximum,'p99_m':p99})
assert maximum<=.005 and p99<=.001
```

坐标换算须先用中立姿势验证；若 FBX 导出/Unity 导入轴向与上述假定不同，记录根矩阵并按实际矩阵换算，而不是旋转到误差最小来掩盖错误。

- [ ] **Step 4: 查看源与 Unity 并排关键帧。** 固定 0、1、2、3 秒，观察 IK 脚位移、膝盖弯曲、肘部和辅助骨跟随、头部；若只有主骨运动而加旋转骨未跟随，即使全身大部分误差较小也不通过。把源中约束影响最大的辅助骨列出逐帧变换，确认 Task 2 的骨骼阈值已经覆盖它们。

- [ ] **Step 5: 仅提交通过验证的新工具与独立资源。**

```bash
git add ArtSource/Exusiai/RigSource/Tools/ExusiaiPreservedRigValidation.cs Assets/Game/Characters/Exusiai/Model/PreservedRig Assets/Game/Characters/Exusiai/Model/PreservedRig.meta
 git commit -m "feat: validate original Exusiai rig with Unity Generic animation"
```

### Task 5: 替换简化版本并保存迁移说明

**Files:**
- Modify: `Assets/Game/Characters/Exusiai/Model/Prefabs/Exusiai.prefab`
- Modify: `Assets/Game/Characters/Exusiai/Model/Scenes/Preview.unity`
- Modify: `ArtSource/Exusiai/RigSource/README.md`
- Create: `ArtSource/Exusiai/RigSource/PreservedRig/UnityValidation.txt`

**Interfaces:**
- Consumes: Task 3/4 验证通过的新 prefab；保留旧版 FBX 作为对照。
- Produces: 现有视觉 prefab 的 GUID 不变，内部使用原骨骼新版本。

- [ ] **Step 1: 在隔离工程使用编辑器 API 替换旧 prefab 内容。** 先记录旧 prefab GUID；实例化新 prefab，保存到旧路径并销毁实例；不能删除旧 prefab.meta。执行代码：

```csharp
string oldPath="Assets/Game/Characters/Exusiai/Model/Prefabs/Exusiai.prefab";
string newPath="Assets/Game/Characters/Exusiai/Model/PreservedRig/Prefabs/Exusiai_Preserved_Rig.prefab";
string oldGuid=AssetDatabase.AssetPathToGUID(oldPath);
var source=AssetDatabase.LoadAssetAtPath<GameObject>(newPath);
var replacement=(GameObject)PrefabUtility.InstantiatePrefab(source);
replacement.name="Exusiai";
PrefabUtility.SaveAsPrefabAsset(replacement,oldPath);
UnityEngine.Object.DestroyImmediate(replacement);
if(AssetDatabase.AssetPathToGUID(oldPath)!=oldGuid)throw new Exception("Prefab GUID changed");
```

- [ ] **Step 2: 统一展示比例。** 原 rig 的 bind pose/骨骼/权重保持源数据；用 prefab 外层展示节点完成脚底归零和头顶 1.65 米的缩放，不改 FBX 内骨位置。先从中立姿势按基础身体材质顶点测量脚底和头顶，排除光环/翅膀材质槽 24。外层设 uniformScale=1.65/bodyHeight，并校正 groundOffset；误差比较继续使用未缩放源 rig 坐标。以此避免缩放表情基准和重绑矩阵。

以下代码在隔离工程中针对保存前的 replacement 执行；量取 slot 24 以外的顶点，并包裹为独立展示根节点：

```csharp
var render= replacement.GetComponentInChildren<SkinnedMeshRenderer>();
var neutral=new Mesh();render.BakeMesh(neutral);
var indices=Enumerable.Range(0,24).SelectMany(i=>render.sharedMesh.GetTriangles(i)).Distinct();
var points=indices.Select(i=>render.transform.TransformPoint(neutral.vertices[i])).ToArray();
float bottom=points.Min(v=>v.y),top=points.Max(v=>v.y);
if(top-bottom<=0)throw new Exception("Invalid body bounds");
var visualRoot=new GameObject("Exusiai");
replacement.transform.SetParent(visualRoot.transform,true);
float scale=1.65f/(top-bottom);
replacement.transform.localScale*=scale;
replacement.transform.localPosition=Vector3.down*bottom*scale;
PrefabUtility.SaveAsPrefabAsset(visualRoot,oldPath);
UnityEngine.Object.DestroyImmediate(visualRoot);
if(AssetDatabase.AssetPathToGUID(oldPath)!=oldGuid)throw new Exception("Prefab GUID changed");
```

该段替换 Step 1 的 SaveAsPrefabAsset/DestroyImmediate 两行，执行前 replacement 尚未销毁。它仅改变展示层，不进入源快照或误差比较。

- [ ] **Step 3: 更新独立预览场景。** 打开现有场景，替换旧角色实例为保留 prefab GUID 的新视觉 prefab，保留相机/灯光；确认播放的是 OriginalRigCheck 且没有旧 Humanoid 控制器。保存至原场景路径，保留 scene.meta。

在隔离工程 Editor 入口中执行：

```csharp
string scenePath="Assets/Game/Characters/Exusiai/Model/Scenes/Preview.unity";
string sceneGuid=AssetDatabase.AssetPathToGUID(scenePath);
var previewScene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
foreach(var root in previewScene.GetRootGameObjects()){
    if(root.name=="Exusiai")UnityEngine.Object.DestroyImmediate(root);
}
var newVisual=(GameObject)PrefabUtility.InstantiatePrefab(
    AssetDatabase.LoadAssetAtPath<GameObject>(oldPath),previewScene);
newVisual.name="Exusiai";
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(previewScene,scenePath);
if(AssetDatabase.AssetPathToGUID(scenePath)!=sceneGuid)throw new Exception("Scene GUID changed");
```

- [ ] **Step 4: 归档与复制。** 复制通过验证的 PreservedRig 目录及所有 .meta，以及更新的旧 prefab/场景回正式工程。对复制源/目标做 SHA-256 比较；解析 prefab/controller/scene 的 GUID 引用，确保模型、动作、材质都可解析。把旧版资源保留作对照，不进行项目范围清理。

- [ ] **Step 5: 在 README 写入具体结果。** 必须包含：原层级/权重保留；源约束在编辑版保留、导出后只保存烘焙结果；Blender 与 Unity 最大/99分位表面误差；测试动作范围；Generic 动画路径；物理、运行时 IK 和 Humanoid 重定向未实现；正式版本是否实际验证。只有已经测出的数值可写入结果。

- [ ] **Step 6: 提交替换与记录。**

```bash
git add Assets/Game/Characters/Exusiai/Model/Prefabs/Exusiai.prefab Assets/Game/Characters/Exusiai/Model/Scenes/Preview.unity ArtSource/Exusiai/RigSource/README.md ArtSource/Exusiai/RigSource/PreservedRig/UnityValidation.txt
 git commit -m "fix: use preserved MMD rig for Exusiai visual prefab"
```

## 验收与中止条件

- 原骨骼、层级、rest 矩阵与权重快照一致；新增动画不得被说成原包动作。
- 91 帧源与烘焙骨骼误差 <=1 mm、<=1°；FBX 回读和 Unity 表面误差最大 <=5 mm、99分位 <=1 mm。
- 每项约束都在源快照中可查询；烘焙经过求解后再清除，禁止“先全部删除再做新动作”。
- 保留 53 个表情，现有材质透明修复不回退。
- 若 driver、SDEF、物理或导出格式造成门槛无法达到，保留新版本供对照，出具失败区域和误差；不替换现有 prefab、不把近似失败描述为等价。
- 用户选择运行时 MMD 控制时，另写约束求解计划：逐项审计 IK、append rotation/translation、固定轴、评估顺序和后物理骨，不默认为已由烘焙覆盖。

## 计划自检

- 覆盖原骨骼恢复、约束效果、真实变形比较、Unity 导入、可回退替换五项要求。
- 骨骼快照和所有采样单位为米；转换比较在展示缩放之前进行。
- snapshot、matrix、write、bootstrap、evaluated_vertices、build、surface_error 均有定义；工具接口与后续命令一致。
- 当前环境缺少临时 bpy、无正式 MMD 动作；执行期间须恢复环境并制作标注清楚的验证动作。
- 本文是待执行计划，所有复选框尚未完成，不代表转换效果已经通过。

## 执行记录（2026-10-04）

已按用户选择在当前对话逐步执行，无子代理。由于既有美术资产尚未跟踪，转换与 Unity 验证使用临时隔离环境；未将既有未提交内容自动提交到 Git。

- [x] 恢复 bpy 4.5.3 / 官方 MMD Tools v4.5.14 环境；记录插件 ZIP SHA-256。
- [x] 原始快照：253 骨骼、64 约束；原文件未修改；旧简化版结构差异负测通过。
- [x] 保留层级、静止矩阵、名称和权重；源文件保留约束；导出副本逐帧烘焙后移除约束。
- [x] 91 帧 Blender 同编号顶点比较：最大误差 0.000861 mm；骨骼位置 0.000734 mm，角度约 0.112°。
- [x] 91 帧 FBX 往返双向最近点检查：最大 1.845 mm，逐帧 99 分位最大 0.00844 mm，通过门限。
- [x] Unity Generic 有效；253 骨骼、53 原始表情均存在。31 个采样最大误差 0.01691 mm，逐帧 99 分位最大 0.00692 mm。
- [x] 20 mm 平移负测被检出。Unity 坐标轴通过静止姿势核对为 (-x,z,-y)，修正计划中的前后轴假设；无逐帧刚体拟合。
- [x] 实际渲染斜侧面、侧面、动作峰值，检查头发裁剪、比例和变形。
- [x] 默认预制体与场景已换用新版本，原 GUID 保持；只在外层 Transform 调整尺寸；旧 FBX/动画仍保留。
- [x] 更新 README、网页预览、重建脚本、报告；交付资源与隔离工程验证资源逐文件一致，记录 SHA-256。

实施修正：不清空 animation_data，避免删除原有驱动器；参考顶点以 NPZ 压缩保存；所有辅助骨骼一起烘焙；Unity 顶点数组只获取一次；表情按原名称校验，允许额外的 3 个 SDEF 数据 shape key。网页 GLB 单独省略这 3 个内部数据 shape key。

验证基准为 MMD Tools 导入后的 Blender 求解，尚未与原生 MMD 播放器对照。Unity 使用本机 6000.5.9f1 / URP 17.5，原工程 6000.3.15f1 / URP 17.3 未升级、未在该版本复验。运行时 IK、物理、正式游戏动作与 Humanoid 重定向不在本次交付中。


## 资源路径整理（2026-10-04）

根据后续命名要求，Unity 运行时资源统一使用通用目录 `Assets/Game/Characters/Exusiai/Model/`。预览场景改名为 `Scenes/Preview.unity`，默认预制体改名为 `Prefabs/Exusiai.prefab`，层级对象名为 `Exusiai` / `Exusiai_OriginalRig`。导入 FBX 与原骨骼预制体也改为 `Exusiai_OriginalRig`。所有移动和改名沿用既有 `.meta`，GUID 不变；源 PMX、Blend、GLB 等美术工作文件仍保留在 `ArtSource/Exusiai/RigSource/` 以说明格式来源。
