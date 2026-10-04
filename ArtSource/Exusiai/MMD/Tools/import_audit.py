import sys,os,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
plugin=Path('/private/tmp/exusiai-mmd-tools/blender_mmd_tools-main')
sys.path.insert(0,str(plugin))
sys.path.insert(0,'/private/tmp/exusiai-mmd-deps')
import bpy
import mmd_tools
mmd_tools.register()
from mmd_tools.core import pmx
p=ROOT/'Source/能天使.pmx'
m=pmx.load(str(p))
def simple(o):
 if isinstance(o,(str,int,float,bool,type(None))): return o
 if isinstance(o,(tuple,list)): return [simple(x) for x in o]
 if hasattr(o,'__dict__'): return {k:simple(v) for k,v in vars(o).items()}
 return str(o)
audit={'counts':{k:len(v) for k,v in vars(m).items() if isinstance(v,list)},'textures':simple(m.textures),'materials':simple(m.materials),'bones':simple(m.bones),'morphs':[{'type':type(x).__name__,'name':x.name} for x in m.morphs]}
(ROOT/'source_audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2))
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.mmd_tools.import_model(filepath=str(p),types={'MESH','ARMATURE','MORPHS'},scale=.08,rename_bones=False)
objects=[]
for o in bpy.context.scene.objects:
 d={'name':o.name,'type':o.type,'dimensions':list(o.dimensions)}
 if o.type=='ARMATURE':d['bones']=[{'name':b.name,'parent':b.parent.name if b.parent else None,'head':list(b.head_local),'tail':list(b.tail_local)} for b in o.data.bones]
 if o.type=='MESH':d.update(vertices=len(o.data.vertices),polygons=len(o.data.polygons),materials=[x.name for x in o.data.materials],shapes=[k.name for k in o.data.shape_keys.key_blocks] if o.data.shape_keys else [])
 objects.append(d)
(ROOT/'blender_audit.json').write_text(json.dumps(objects,ensure_ascii=False,indent=2))
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Exusiai_MMD_Source.blend'))
print('SUCCESS',audit['counts'],objects[:1]);sys.stdout.flush();os._exit(0)
