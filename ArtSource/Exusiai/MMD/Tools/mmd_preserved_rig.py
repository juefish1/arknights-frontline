"""Keep the imported MMD rig intact; bake evaluated motion only in an export copy."""
import hashlib
import json
import os
from pathlib import Path
import sys
import numpy as np
import bpy

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'PreservedRig'

def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2))

def bootstrap():
    env = json.loads(Path(os.environ.get('EXUSIAI_ENV', '/private/tmp/exusiai-preserved-rig/environment.json')).read_text())
    sys.path[:0] = [env['plugin_path'], env['opencc_path']]
    import mmd_tools
    mmd_tools.register()
    return env

def drivers(owner):
    if not owner or not owner.animation_data:
        return []
    return [{'path': f.data_path, 'index': f.array_index, 'expression': f.driver.expression,
             'variables': [{'name': v.name, 'type': v.type,
                            'targets': [{'id': getattr(t.id, 'name', None), 'path': t.data_path,
                                         'bone': t.bone_target} for t in v.targets]}
                           for v in f.driver.variables]} for f in owner.animation_data.drivers]

def snapshot(arm, mesh):
    constraints = []
    for bone in arm.pose.bones:
        for c in bone.constraints:
            settings = {}
            for prop in c.bl_rna.properties:
                if prop.identifier in ('rna_type','is_valid','error_location','error_rotation') or prop.type == 'COLLECTION':
                    continue
                v = getattr(c, prop.identifier)
                if prop.type == 'POINTER':
                    settings[prop.identifier] = getattr(v, 'name', None)
                elif getattr(prop, 'is_array', False):
                    settings[prop.identifier] = list(v)
                elif isinstance(v, (str, bool, int, float)):
                    settings[prop.identifier] = v
            constraints.append({'bone': bone.name, 'settings': settings})
    weights = [[(mesh.vertex_groups[g.group].name, g.weight) for g in v.groups] for v in mesh.data.vertices]
    geometry = np.empty(len(mesh.data.vertices)*3, dtype=np.float32)
    mesh.data.vertices.foreach_get('co', geometry)
    return {'bones': [{'name': b.name, 'parent': b.parent.name if b.parent else None,
                       'rest': [list(row) for row in b.matrix_local]} for b in arm.data.bones],
            'weights_sha256': hashlib.sha256(json.dumps(weights).encode()).hexdigest(),
            'geometry_sha256': hashlib.sha256(geometry.tobytes()).hexdigest(),
            'shapes': [k.name for k in mesh.data.shape_keys.key_blocks],
            'constraints': constraints,
            'drivers': {'armature': drivers(arm), 'mesh': drivers(mesh), 'shapes': drivers(mesh.data.shape_keys)},
            'modifiers': [{'name': m.name, 'type': m.type} for m in mesh.modifiers]}

def open_source():
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'Exusiai_MMD_Source.blend'))
    return (next(o for o in bpy.data.objects if o.type == 'ARMATURE'),
            next(o for o in bpy.data.objects if o.type == 'MESH'))

def vertices(mesh):
    obj = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    data = obj.to_mesh()
    points = np.empty((len(data.vertices),3), dtype=np.float32)
    data.vertices.foreach_get('co', points.ravel())
    matrix = np.asarray(obj.matrix_world)
    points = points @ matrix[:3,:3].T + matrix[:3,3]
    obj.to_mesh_clear()
    return points.astype(np.float32)

def audit():
    env = bootstrap()
    arm, mesh = open_source()
    source = snapshot(arm, mesh)
    write(OUT/'source_snapshot.json', source)
    write(OUT/'environment.json', env | {'bpy': bpy.app.version_string})
    print('SOURCE', len(source['bones']), 'constraints', len(source['constraints']), flush=True)
    print('DRIVERS', source['drivers'], flush=True)
    for p in arm.pose.bones:
        if p.constraints:
            print(p.name, [(c.name,c.type,c.influence,getattr(c,'subtarget',None),c.mute,c.is_valid) for c in p.constraints], flush=True)
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Exusiai_Unity.blend'))
    old = snapshot(next(o for o in bpy.data.objects if o.type=='ARMATURE'), next(o for o in bpy.data.objects if o.type=='MESH'))
    differences = [field for field in source if source[field]!=old[field]]
    assert {'bones','weights_sha256','constraints'} <= set(differences)
    write(OUT/'old_conversion_difference.json', {'different_fields': differences,'original_constraints':len(source['constraints']), 'old_constraints':len(old['constraints'])})
    print('EXPECTED_BASELINE_MISMATCH', differences, flush=True)


def pose_matrices(arm):
    evaluated = arm.evaluated_get(bpy.context.evaluated_depsgraph_get())
    return np.asarray([evaluated.matrix_world @ p.matrix for p in evaluated.pose.bones],dtype=np.float32)

def build():
    from math import radians
    from mathutils import Vector, Matrix
    bootstrap()
    arm, mesh = open_source()
    baseline = snapshot(arm,mesh)
    assert len(baseline['constraints']) == 64
    for side in ('左','右'):
        assert any(c.type=='IK' and c.influence>0 and not c.mute and c.is_valid for c in arm.pose.bones[side+'ひざ'].constraints)
    # Retain all original drivers; only replace a previous test action if one exists.
    if arm.animation_data: arm.animation_data.action=None
    scene=bpy.context.scene
    scene.render.fps=30;scene.frame_start=0;scene.frame_end=90
    tests=[]
    for name,axis,angle in [('左ひじ',0,-25),('右ひじ',0,25),('頭',1,12),
                            ('左腕捩',1,20),('右腕捩',1,-20),('左手捩',1,15),('右手捩',1,-15)]:
        pb=arm.pose.bones[name];pb.rotation_mode='XYZ'
        rest=pb.rotation_euler.copy()
        for frame,factor in [(0,0),(30,1),(60,.4),(90,0)]:
            pb.rotation_euler=rest.copy();pb.rotation_euler[axis]+=radians(angle)*factor
            pb.keyframe_insert('rotation_euler',frame=frame,group=name)
        tests.append({'bone':name,'channel':'rotation','degrees':angle,'axis':axis})
    for name,height in [('左足ＩＫ',.06),('右足ＩＫ',.04)]:
        pb=arm.pose.bones[name];rest=pb.location.copy()
        local=pb.bone.matrix_local.to_3x3().inverted() @ Vector((0,0,height))
        for frame,factor in [(0,0),(30,1),(60,.4),(90,0)]:
            pb.location=rest+local*factor;pb.keyframe_insert('location',frame=frame,group=name)
        tests.append({'bone':name,'channel':'world_vertical','meters':height})
    arm.animation_data.action.name='Exusiai_OriginalRigCheck'
    scene.frame_set(0);bpy.context.view_layer.update()
    assert snapshot(arm,mesh)==baseline
    # Make source textures portable without changing source geometry or rig.
    for im in bpy.data.images:
        if im.source=='FILE' and not im.packed_file:
            old=im.filepath.replace('\\','/')
            if '/Source/' in old:im.filepath=str(ROOT/'Source'/old.split('/Source/')[-1])
    bpy.ops.file.pack_all()
    OUT.mkdir(exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Exusiai_MMD_Preserved.blend'))
    poses=[];points=[]
    for frame in range(91):
        scene.frame_set(frame);bpy.context.view_layer.update()
        poses.append(pose_matrices(arm));points.append(vertices(mesh))
    poses=np.asarray(poses);points=np.asarray(points)
    np.savez_compressed(OUT/'reference.npz',poses=poses,vertices=points,bones=np.array([b.name for b in arm.data.bones]))
    write(OUT/'reference_manifest.json',{'fps':30,'frames':91,'unit':'meter','tests':tests,'reference':'MMD Tools imported Blender rig; physics not built', 'source_snapshot':baseline})
    print('REFERENCE_CAPTURED',points.shape,flush=True)
    scene.frame_set(0)
    bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);bpy.context.view_layer.objects.active=arm
    bpy.ops.object.mode_set(mode='POSE')
    result=bpy.ops.nla.bake(frame_start=0,frame_end=90,step=1,only_selected=False,
        visual_keying=True,clear_constraints=True,clear_parents=False,use_current_action=False,bake_types={'POSE'})
    assert 'FINISHED' in result
    bpy.ops.object.mode_set(mode='OBJECT')
    arm.animation_data.action.name='Exusiai_OriginalRigCheck_Baked'
    after=snapshot(arm,mesh)
    for field in ('bones','weights_sha256','geometry_sha256','shapes','drivers'):
        assert after[field]==baseline[field],field
    max_pos=max_angle=max_surface=0.
    worst=None
    for frame in range(91):
        scene.frame_set(frame);bpy.context.view_layer.update()
        actual=pose_matrices(arm)
        pd=np.linalg.norm(actual[:,:3,3]-poses[frame,:,:3,3],axis=1)
        max_pos=max(max_pos,float(pd.max()))
        for i in range(len(actual)):
            q1=Matrix(actual[i]).to_quaternion();q2=Matrix(poses[frame,i]).to_quaternion()
            angle=2*np.arccos(min(1.,abs(q1.dot(q2))))
            max_angle=max(max_angle,float(angle))
        sd=np.linalg.norm(vertices(mesh)-points[frame],axis=1)
        if float(sd.max())>max_surface:worst={'frame':frame,'vertex':int(sd.argmax())}
        max_surface=max(max_surface,float(sd.max()))
    report={'passed':max_pos<=.001 and max_angle<=radians(1) and max_surface<=.001,
            'bones':len(arm.data.bones),'constraints_evaluated':len(baseline['constraints']),
            'max_bone_position_m':max_pos,'max_bone_angle_degrees':float(np.degrees(max_angle)),
            'max_same_vertex_error_m':max_surface,'worst_vertex':worst,
            'hierarchy_rest_geometry_weights_shapes_preserved':True}
    write(OUT/'bake_report.json',report);print('BAKE_REPORT',report,flush=True)
    assert report['passed'],'Bake differs from constraint evaluation'
    scene.frame_set(0)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Exusiai_MMD_Baked.blend'))
    bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);mesh.select_set(True)
    for obj in (arm,mesh):
        parent=obj.parent
        while parent:parent.select_set(True);parent=parent.parent
    bpy.ops.export_scene.fbx(filepath=str(OUT/'Exusiai_MMD_Preserved.fbx'),use_selection=True,
        object_types={'ARMATURE','MESH','EMPTY'},add_leaf_bones=False,use_armature_deform_only=False,
        use_mesh_modifiers=False,bake_anim=True,bake_anim_step=1,bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_simplify_factor=0,
        apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y')
    print('PRESERVED_RIG_EXPORT_OK',flush=True)

if __name__=='__main__':
    try:
        build() if '--build' in sys.argv else audit()
    except Exception:
        import traceback
        traceback.print_exc();sys.stderr.flush();os._exit(1)
    sys.stdout.flush();os._exit(0)
