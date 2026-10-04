import bpy,sys,os,json,math,shutil
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[1]; OUT=R/'Export';OUT.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(R/'Exusiai_MMD_Source.blend'))
audit=json.loads((R/'source_audit.json').read_text())
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE');mesh=next(o for o in bpy.data.objects if o.type=='MESH')
arm.name='Exusiai_Rig';mesh.name='Exusiai_Body'
# Preserve the neutral bind pose while replacing MMD-only control dependencies.
for o in [arm,mesh]:
 w=o.matrix_world.copy();o.parent=None;o.matrix_world=w
for p in arm.pose.bones:
 for c in list(p.constraints):p.constraints.remove(c)
 p.rotation_mode='XYZ';p.rotation_euler=(0,0,0);p.location=(0,0,0);p.scale=(1,1,1)
arm.animation_data_clear()
if mesh.data.shape_keys:mesh.data.shape_keys.animation_data_clear()
for mod in list(mesh.modifiers):
 if mod.type!='ARMATURE':mesh.modifiers.remove(mod)
for side in ['左','右']:
 for suffix in ['足','ひざ','足首']:
  src=mesh.vertex_groups.get(side+suffix+'D');dest=mesh.vertex_groups.get(side+suffix)
  if src:
   if not dest:dest=mesh.vertex_groups.new(name=side+suffix)
   for v in mesh.data.vertices:
    for g in list(v.groups):
     if g.group==src.index:dest.add([v.index],g.weight,'ADD')
   mesh.vertex_groups.remove(src)
bpy.context.view_layer.objects.active=arm;arm.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for side in ['左','右']:
 arm.data.edit_bones[side+'足'].parent=arm.data.edit_bones['腰']
 arm.data.edit_bones[side+'足先EX'].parent=arm.data.edit_bones[side+'足首']
for b in list(arm.data.edit_bones):
 if b.name.startswith(('_dummy_','_shadow_')):arm.data.edit_bones.remove(b)
bpy.ops.object.mode_set(mode='OBJECT')
for o in list(bpy.data.objects):
 if o not in [arm,mesh]:bpy.data.objects.remove(o,do_unlink=True)
# PMX front is Blender -Y. The FBX exporter will convert this to Unity +Z.
# Normalize soles to zero and hair crown to 1.65 m, excluding halo/wing material.
bodyids={vi for p in mesh.data.polygons if p.material_index!=24 for vi in p.vertices}
low=min(v.co.z for v in mesh.data.vertices);crown=max(mesh.data.vertices[i].co.z for i in bodyids);scale=1.65/(crown-low)
if mesh.data.shape_keys:
 for k in mesh.data.shape_keys.key_blocks:
  for v in k.data:v.co.z-=low;v.co*=scale
 for k in list(mesh.data.shape_keys.key_blocks):
  if k.name!='Basis' and k.name not in {m['name'] for m in audit['morphs']}:mesh.shape_key_remove(k)
 for k in mesh.data.shape_keys.key_blocks:k.value=0
 for v,b in zip(mesh.data.vertices,mesh.data.shape_keys.key_blocks[0].data):v.co=b.co
else:
 for v in mesh.data.vertices:v.co.z-=low;v.co*=scale
bpy.context.view_layer.objects.active=arm;bpy.ops.object.mode_set(mode='EDIT')
for b in arm.data.edit_bones:
 b.head.z-=low;b.tail.z-=low;b.head*=scale;b.tail*=scale
bpy.ops.object.mode_set(mode='OBJECT')
# Export stable ASCII bone names and retain the original mapping.
rename={};hum={'Hips':'腰','Spine':'上半身','Chest':'上半身2','Neck':'首','Head':'頭'}
for side,label in [('左','Left'),('右','Right')]:
 for h,j in [('Shoulder','肩'),('UpperArm','腕'),('LowerArm','ひじ'),('Hand','手首'),('UpperLeg','足'),('LowerLeg','ひざ'),('Foot','足首'),('Toes','足先EX')]:hum[label+h]=side+j
for i,b in enumerate(arm.data.bones):
 old=b.name;new=next((h for h,j in hum.items() if j==old),f'Bone_{i:03d}');rename[old]=new;b.name=new
# Blender updates vertex group names with armature bone renames.
materials=[]
for i,(mat,src) in enumerate(zip(mesh.data.materials,audit['materials'])):
 mat.name=f'EXM_{i:02d}';mat.use_nodes=True;mat.node_tree.nodes.clear();n=mat.node_tree.nodes
 output=n.new('ShaderNodeOutputMaterial');bs=n.new('ShaderNodeBsdfPrincipled');bs.inputs['Roughness'].default_value=.8
 mat.node_tree.links.new(bs.outputs['BSDF'],output.inputs['Surface'])
 texpath=R/'Source'/audit['textures'][src['texture']]['path'].split('/Source/')[-1];target=OUT/'Textures'/texpath.name;target.parent.mkdir(exist_ok=True);shutil.copy2(texpath,target)
 tex=n.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(target),check_existing=True)
 mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);mat.node_tree.links.new(tex.outputs['Alpha'],bs.inputs['Alpha'])
 # Texture alpha carries eye, hair and wing cutouts; near-one PMX material alpha is treated as opaque.
 mat.surface_render_method='DITHERED';mat.use_backface_culling=False
 materials.append({'name':mat.name,'label':src['name'],'texture':target.name})
mesh.parent=arm
bpy.context.view_layer.update()
# FK test only: bends both elbows and knees and turns the head; all keys return to bind pose.
scene=bpy.context.scene;scene.render.fps=30;scene.frame_start=1;scene.frame_end=91
for name,axis,angle in [('LeftLowerArm',0,-35),('RightLowerArm',0,-35),('LeftLowerLeg',0,22),('RightLowerLeg',0,22),('Head',1,12)]:
 p=arm.pose.bones[name]
 for frame,factor in [(1,0),(31,1),(61,-.35 if name=='Head' else .45),(91,0)]:
  p.rotation_euler=(0,0,0);p.rotation_euler[axis]=math.radians(angle)*factor;p.keyframe_insert('rotation_euler',frame=frame,group=name)
arm.animation_data.action.name='Exusiai_RigCheck'
scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);mesh.select_set(True);bpy.context.view_layer.objects.active=arm
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(R/'Exusiai_Unity.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'Exusiai_MMD.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_simplify_factor=0,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=False,path_mode='RELATIVE')
bpy.ops.export_scene.gltf(filepath=str(OUT/'Exusiai_MMD.glb'),export_format='GLB',use_selection=True,export_animations=True,export_morph=True,export_skins=True)
# Use depth-tested cutouts, matching Unity, instead of view-dependent alpha sorting.
from fix_gltf_alpha import fix_alpha
fix_alpha(OUT/'Exusiai_MMD.glb')
(OUT/'manifest.json').write_text(json.dumps({'materials':materials,'humanBones':[{'humanName':k,'boneName':k} for k in hum],'bone_names':rename,'bodyHeight':1.65,'sourceScale':scale,'sourceGround':low,'vertices':len(mesh.data.vertices),'triangles':sum(len(p.vertices)-2 for p in mesh.data.polygons),'blendshapes':len(mesh.data.shape_keys.key_blocks)-1,'bones':len(arm.data.bones),'animation':'Exusiai_RigCheck — generated validation only'},ensure_ascii=False,indent=2))
# A neutral studio render for visual review.
scene.world.color=(.45,.45,.45)
def point(o,at):o.rotation_euler=(Vector(at)-o.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size in [('Key',(3,-4,5),650,4),('Fill',(-3,-2,3),450,4),('Rim',(0,3,4),650,3)]:
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=loc;point(o,(0,0,1))
d=bpy.data.cameras.new('Preview');cam=bpy.data.objects.new('Preview',d);scene.collection.objects.link(cam);scene.camera=cam;cam.location=(2,-5,2.0);point(cam,(0,0,.9));d.type='ORTHO';d.ortho_scale=2.15
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=900;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.film_transparent=True;scene.view_settings.view_transform='Standard'
(R/'Previews').mkdir(exist_ok=True)
for frame,name in [(1,'neutral'),(31,'rig_check')]:
 scene.frame_set(frame);scene.render.filepath=str(R/'Previews'/f'{name}.png');bpy.ops.render.render(write_still=True)
print('CONVERSION_OK');sys.stdout.flush();os._exit(0)
