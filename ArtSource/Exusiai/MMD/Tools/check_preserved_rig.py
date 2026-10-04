"""Numerical surface regression for exported FBX and Unity skinning."""
import os,sys,json
from pathlib import Path
import numpy as np
from scipy.spatial import cKDTree
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'PreservedRig'
def distance(a,b):
    d=np.concatenate((cKDTree(a).query(b)[0],cKDTree(b).query(a)[0]))
    return float(d.max()),float(np.percentile(d,99))
def main():
    ref=np.load(OUT/'reference.npz');reports=[]
    if '--unity' in sys.argv:
        # FBX exported -Z forward, Y up; Unity changes handedness on import.
        base=Path('/private/tmp/exusiai-preserved-unity')
        meta=json.loads((base/'preserved-unity.json').read_text())
        snapshot=json.loads((OUT/'source_snapshot.json').read_text())
        audit=json.loads((ROOT/'source_audit.json').read_text())
        assert all(b['name'] in meta['boneNames'] for b in snapshot['bones']), 'Missing original bone'
        assert all(any(n==m['name'] or n.endswith('.'+m['name']) for n in meta['shapeNames']) for m in audit['morphs']), 'Missing original morph'
        assert meta['generic'] and meta['avatarValid'] and abs(meta['duration']-3)<.001
        actual=np.fromfile(base/'preserved-vertices.bin',dtype='<f4').reshape(31,meta['vertices'],3)
        expected=ref['vertices'][:,:, [0,2,1]].copy();expected[:,:,0]*=-1;expected[:,:,2]*=-1
        frames=range(0,91,3);kind='unity'
    else:
        import bpy
        from mmd_preserved_rig import vertices
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(OUT/'Exusiai_MMD_Preserved.fbx'),use_anim=True)
        arm=next(o for o in bpy.data.objects if o.type=='ARMATURE');mesh=next(o for o in bpy.data.objects if o.type=='MESH')
        action=arm.animation_data.action
        print('IMPORTED',len(arm.data.bones),len(mesh.data.vertices),action.frame_range[:],flush=True)
        original=json.loads((OUT/'source_snapshot.json').read_text())
        imported={b.name:b.parent.name if b.parent else None for b in arm.data.bones}
        assert all(b['name'] in imported and imported[b['name']]==b['parent'] for b in original['bones'])
        start=int(action.frame_range[0]);actual=[]
        for frame in range(91):
            bpy.context.scene.frame_set(frame+start);bpy.context.view_layer.update();actual.append(vertices(mesh))
        actual=np.asarray(actual);expected=ref['vertices'];frames=range(91);kind='fbx'
    for i,frame in enumerate(frames):
        maximum,p99=distance(actual[i],expected[frame]);reports.append({'frame':frame,'max_m':maximum,'p99_m':p99})
    mx=max(x['max_m'] for x in reports);p99=max(x['p99_m'] for x in reports)
    negative=distance(actual[0]+np.array([.02,0,0]),expected[0])[0]
    report={'passed':mx<=.005 and p99<=.001,'coordinate_mapping': '(-x,z,-y)' if kind=='unity' else 'Blender world XYZ', 'max_m':mx,'max_p99_m':p99,'negative_20mm_translation_detected':negative>.005,'frames':reports}
    (OUT/(kind+'_conversion_report.json')).write_text(json.dumps(report,indent=2))
    print(kind, {k:v for k,v in report.items() if k!='frames'},flush=True)
    assert report['passed'] and report['negative_20mm_translation_detected']
if __name__=='__main__':
    try:main()
    except Exception:
        import traceback;traceback.print_exc();sys.stderr.flush();os._exit(1)
    sys.stdout.flush();os._exit(0)
