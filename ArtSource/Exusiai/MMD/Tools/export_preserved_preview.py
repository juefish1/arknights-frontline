"""Display-only PBR GLB of the baked original rig, without editing bind data."""
import bpy,sys,os,json
from pathlib import Path
from mmd_preserved_rig import ROOT,OUT,bootstrap
from fix_gltf_alpha import fix_alpha
try:
 bootstrap();bpy.ops.wm.open_mainfile(filepath=str(OUT/'Exusiai_MMD_Baked.blend'))
 mesh=next(o for o in bpy.data.objects if o.type=='MESH')
 manifest=json.loads((ROOT/'Export/manifest.json').read_text())
 for mat,entry in zip(mesh.data.materials,manifest['materials']):
  mat.name=entry['name'];mat.use_nodes=True;mat.node_tree.nodes.clear();nodes=mat.node_tree.nodes
  output=nodes.new('ShaderNodeOutputMaterial');bs=nodes.new('ShaderNodeBsdfPrincipled');bs.inputs['Roughness'].default_value=.8
  tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(ROOT/'Export/Textures'/entry['texture']),check_existing=True)
  mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);mat.node_tree.links.new(tex.outputs['Alpha'],bs.inputs['Alpha']);mat.node_tree.links.new(bs.outputs['BSDF'],output.inputs['Surface'])
  mat.surface_render_method='DITHERED';mat.use_backface_culling=False
 # SDEF helper shape keys are plugin data, not facial expressions. Retain in FBX/source;
 # omit from display-only GLB to keep GPU morph textures within practical size.
 for k in list(mesh.data.shape_keys.key_blocks):
  if k.name.startswith('mmd_sdef_'):mesh.shape_key_remove(k)
 bpy.context.scene.frame_set(0)
 bpy.ops.object.select_all(action='SELECT')
 bpy.ops.export_scene.gltf(filepath=str(OUT/'Exusiai_MMD_Preserved.glb'),export_format='GLB',use_selection=True,export_animations=True,export_morph=True,export_skins=True)
 fix_alpha(OUT/'Exusiai_MMD_Preserved.glb')
 print('PRESERVED_GLB_OK',flush=True)
except Exception:
 import traceback;traceback.print_exc();sys.stderr.flush();os._exit(1)
sys.stdout.flush();os._exit(0)
