"""Build an editable, stylized static Exusiai model using Blender 4.5 bpy.

Run in Blender background mode, or Python with bpy installed:
    blender -b --python ArtSource/Exusiai/Tools/build_exusiai.py
The model has no dependency on game scripts or scene generation.
"""
from pathlib import Path
import json
import math
import sys
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / 'ArtSource/Exusiai'
ASSETS = ROOT / 'Assets/Game/Characters/Exusiai'
PREVIEWS = SOURCE / 'Previews'
for p in (SOURCE, ASSETS / 'Models', PREVIEWS):
    p.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for block in list(bpy.data.materials):
    bpy.data.materials.remove(block)

character = bpy.data.collections.new('EXUSIAI • Model')
bpy.context.scene.collection.children.link(character)
studio = bpy.data.collections.new('STUDIO • Preview only')
bpy.context.scene.collection.children.link(studio)
model_objects = []
palette = {}

def srgb(value):
    return value / 12.92 if value <= .04045 else ((value + .055) / 1.055) ** 2.4

def material(name, color, roughness=.65, metallic=0., emission=0.):
    rgb = tuple(int(color[i:i+2], 16) / 255 for i in (0, 2, 4))
    linear = tuple(srgb(c) for c in rgb)
    mat = bpy.data.materials.new('EX_' + name)
    mat.diffuse_color = (*linear, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*linear, 1)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    if emission:
        bsdf.inputs['Emission Color'].default_value = (*linear, 1)
        bsdf.inputs['Emission Strength'].default_value = emission
    palette[mat.name] = {'hex': color, 'roughness': roughness, 'metallic': metallic, 'emission': emission}
    return mat

skin = material('Skin', 'F3CDBA', .76)
skin_shadow = material('SkinWarm', 'D79887', .8)
ivory = material('UniformIvory', 'E9E6DF', .78)
white = material('SeamWhite', 'FAF4E8', .66)
ink = material('UniformInk', '222C37', .82)
slate = material('FabricSlate', '4D5360', .86)
black = material('RubberBlack', '151B23', .7)
pink = material('AccentMagenta', 'D3225F', .5)
hair = material('HairBurgundy', '59283D', .64)
hair_dark = material('HairShadow', '382232', .68)
hair_light = material('HairHighlight', '813B53', .64)
hair_glint = material('HairGlint', 'A66079', .65)
stocking = material('Stockings', '242538', .86)
steel = material('Hardware', 'B5B6AC', .38, .65)
gold = material('WingGold', 'B9A47D', .5, .25)
halo = material('HaloLight', 'FFE6A9', .4, .1, .6)
wing = material('WingIvory', 'F6EBD2', .68, 0, .12)
gun_tan = material('WeaponSand', 'AD9973', .65, .18)
gun_dark = material('WeaponDark', '333841', .5, .35)
eye_white = material('EyeWhite', 'FFF3E8', .68)
iris = material('IrisAmber', 'BD5B1F', .4)
iris_light = material('IrisGold', 'F7B552', .45)
eye_dark = material('Eyeliner', '482B30', .8)

def register(obj, mat=None, is_model=True):
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    (character if is_model else studio).objects.link(obj)
    if mat:
        obj.data.materials.append(mat)
    if is_model:
        model_objects.append(obj)
    return obj

def mesh(name, verts, faces, mat, smooth=True):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    register(obj, mat)
    for face in data.polygons:
        face.use_smooth = smooth
    return obj

def finish(obj, sub=0, bevel=0):
    if sub:
        mod = obj.modifiers.new('Soft silhouette', 'SUBSURF')
        mod.levels = mod.render_levels = sub
    if bevel:
        mod = obj.modifiers.new('Edge bevel', 'BEVEL')
        mod.width, mod.segments = bevel, 2
    return obj

def ellipsoid(name, center, scale, mat, segments=24, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    register(obj, mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj

def box(name, center, scale, mat, bevel=.003, rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if rotation:
        obj.rotation_euler = rotation
    register(obj, mat)
    return finish(obj, bevel=bevel)

def curve(name, points, radius, mat, cyclic=False):
    data = bpy.data.curves.new(name, 'CURVE')
    data.dimensions = '3D'
    data.resolution_u = 5
    data.bevel_depth = radius
    data.bevel_resolution = 1
    spline = data.splines.new('BEZIER')
    spline.bezier_points.add(len(points)-1)
    for point, coord in zip(spline.bezier_points, points):
        point.co = coord
        point.handle_left_type = point.handle_right_type = 'AUTO'
    spline.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, data)
    register(obj, mat)
    return obj

def cylinder_between(name, start, end, r1, mat, r2=None, vertices=16):
    a, b = Vector(start), Vector(end)
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r1 if r2 is None else r2, depth=(b-a).length, location=(a+b)/2)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = (b-a).to_track_quat('Z', 'Y').to_euler()
    register(obj, mat)
    for p in obj.data.polygons:
        p.use_smooth = len(p.vertices) == 4
    return obj

def loft(name, rings, mat, segments=32, sub=1):
    # Each ring: z, x-radius, y-radius, x-center, y-center.
    verts = []
    for z, rx, ry, cx, cy in rings:
        for i in range(segments):
            a = i * 2 * math.pi / segments
            verts.append((cx + rx*math.sin(a), cy - ry*math.cos(a), z))
    faces = []
    for j in range(len(rings)-1):
        for i in range(segments):
            a = j*segments+i
            b = j*segments+(i+1)%segments
            faces.append((a, b, b+segments, a+segments))
    faces += [tuple(reversed(range(segments))), tuple((len(rings)-1)*segments+i for i in range(segments))]
    return finish(mesh(name, verts, faces, mat), sub=sub)

def ribbon(name, points, widths, mat, depth=.012, sub=1):
    # Tapered, solid hair locks, with horizontal width and rounded cross section.
    verts = []
    for p, w in zip(points, widths):
        for k in range(8):
            a = k*math.pi/4
            verts.append((p[0]+w*math.cos(a), p[1]+depth*math.sin(a), p[2]))
    faces = []
    for j in range(len(points)-1):
        for k in range(8):
            faces.append((j*8+k, j*8+(k+1)%8, (j+1)*8+(k+1)%8, (j+1)*8+k))
    faces.extend([tuple(reversed(range(8))), tuple((len(points)-1)*8+k for k in range(8))])
    return finish(mesh(name, verts, faces, mat), sub=sub)

def plate(name, outline, thickness, mat, bevel=.002):
    # Outline is a polygon of XYZ points, extruded along Y.
    verts = [(x,y-thickness/2,z) for x,y,z in outline]+[(x,y+thickness/2,z) for x,y,z in outline]
    n = len(outline)
    faces = [tuple(reversed(range(n))), tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return finish(mesh(name, verts, faces, mat, smooth=False), bevel=bevel)

# BODY / CLOTHING ------------------------------------------------------------
torso_rings = [
    (.815,.126,.074,0,.0), (.83,.132,.079,0,0),
    (.91,.123,.075,0,.0), (.995,.104,.066,0,.008),
    (1.07,.098,.064,0,.005), (1.14,.113,.079,0,-.002),
    (1.205,.139,.086,0,.001), (1.26,.144,.069,0,.011),
    (1.294,.133,.059,0,.015), (1.32,.065,.044,0,.007)]
loft('Uniform • fitted jacket', torso_rings, ivory)
loft('Neck', [(1.292,.036,.03,0,0),(1.32,.033,.03,0,0),(1.393,.033,.03,0,0),(1.41,.042,.038,0,0)], skin, sub=1)

# Side panels follow body curvature rather than intersecting boxes.
for s, side in [(-1,'L'), (1,'R')]:
    verts=[]
    for z,rx,ry,cx,cy in torso_rings:
        for a in (.97,1.25,1.57,1.89,2.2):
            verts.append((s*(rx+.0015)*math.sin(a), cy-(ry+.002)*math.cos(a), z))
    faces=[(j*5+i,j*5+i+1,(j+1)*5+i+1,(j+1)*5+i) for j in range(len(torso_rings)-1) for i in range(4)]
    finish(mesh('Uniform • black side panel '+side,verts,faces,ink),sub=1)
    curve('Panel piping '+side,[(s*rx*.83,cy-ry*.57-.002,z) for z,rx,ry,cx,cy in torso_rings[1:-1]],.0018,slate)

# Jacket center zip, placket and lower twin seams.
curve('Front zipper',[(0,-.079,.83),(0,-.082,.93),(0,-.064,1.06),(0,-.086,1.18),(0,-.071,1.265)],.002,slate)
for s in (-1,1):
    curve('Zipper tape',[(s*.006,-.078,.845),(s*.006,-.080,.96),(s*.006,-.066,1.065),(s*.006,-.088,1.185),(s*.006,-.071,1.265)],.001,white)
for z,rx,ry in [(.835,.132,.08),(.853,.133,.079),(.868,.13,.077)]:
    curve('Jacket hem',[(rx*math.sin(a),-ry*math.cos(a),z) for a in [i*math.pi/12 for i in range(24)]],.002,slate,True)

# Raised collar and padded hood at the back.
loft('Collar • charcoal',[(1.277,.077,.051,0,.0),(1.29,.083,.052,0,.0),(1.34,.066,.043,0,.0),(1.35,.064,.042,0,0)],ink,sub=1)
curve('Collar seam',[(-.064,-.026,1.34),(0,-.046,1.346),(.064,-.026,1.34)],.002,slate)
ellipsoid('Hood • folded', (0,.059,1.276),(.109,.035,.065),ink)
curve('Hood edge',[(-.095,.059,1.295),(-.08,.082,1.33),(0,.091,1.34),(.08,.082,1.33),(.095,.059,1.295)],.004,slate)
for s in (-1,1):
    box('Shoulder magenta strap',(s*.105,.005,1.308),(.019,.102,.009),pink,.002,rotation=(0,s*.13,0))
    box('Shoulder strap clip',(s*.105,-.023,1.31),(.026,.012,.013),steel,.002)
    curve('Hood cord',[(s*.038,-.057,1.296),(s*.036,-.084,1.233),(s*.046,-.09,1.195)],.002,ink)
    cylinder_between('Cord tip',(s*.046,-.09,1.19),(s*.046,-.09,1.201),.0035,steel)

# Black webbing belt around waist, rectangular buckle and keepers.
loft('Belt • waist',[(.949,.121,.079,0,.005),(.951,.122,.08,0,.005),(.993,.112,.075,0,.005),(.995,.111,.074,0,.005)],black,sub=0)
box('Belt buckle outer',(.015,-.078,.972),(.043,.011,.047),steel,.003)
box('Belt buckle inset',(.015,-.085,.972),(.025,.004,.030),black,.001)
box('Belt buckle pin',(.012,-.089,.973),(.003,.003,.035),steel,.0005)
for x in (-.088,-.06,.072,.094):
    box('Belt keeper',(x,-.062,.969),(.01,.016,.045),slate,.001)
box('Belt pouch',(.126,.018,.956),(.037,.067,.071),ink,.006)

# Asymmetric wrap skirt with a diagonal hem and overlapping front flap.
N=40
verts=[]
for ring in range(4):
    for i in range(N):
        a=2*math.pi*i/N
        if ring==0: z,rx,ry=.891,.135,.092
        elif ring==1: z,rx,ry=.866,.139,.096
        elif ring==2: z,rx,ry=.756,.149,.09
        else: z,rx,ry=.702+.038*math.sin(a),.158,.094
        verts.append((rx*math.sin(a),-ry*math.cos(a),z))
faces=[(j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i) for j in range(3) for i in range(N)]
skirt=mesh('Skirt • asymmetric wrap',verts,faces,ink)
solid=skirt.modifiers.new('Fabric thickness','SOLIDIFY'); solid.thickness=.003
finish(skirt,sub=1)
plate('Skirt • diagonal outer fold',[(-.134,-.043,.858),(-.13,-.095,.823),(-.15,-.100,.697),(.122,-.08,.797),(.132,-.042,.84)],.004,slate)
plate('Skirt • dark lower wrap',[(-.15,-.105,.695),(-.144,-.099,.733),(.13,-.085,.819),(.113,-.093,.76)],.003,ink)
curve('Skirt gold piping',[(-.148,-.108,.702),(-.058,-.11,.737),(.031,-.103,.775),(.125,-.09,.812)],.0018,gold)
for i in range(3):
    x=-.12+i*.013
    curve('Skirt chevron',[(x,-.107,.722),(x+.002,-.108,.752),(x-.003,-.107,.771)],.0018,gold)
curve('Skirt upper fold',[(-.117,-.086,.843),(-.053,-.102,.821),(.02,-.103,.817),(.12,-.072,.834)],.0015,slate)

# Legs: continuous shaped surfaces, no stacked spheres.
for s,side in [(-1,'L'),(1,'R')]:
    loft('Stocking leg '+side,[
        (.15,.033,.035,s*.068,.003),(.19,.034,.036,s*.068,.004),
        (.28,.035,.041,s*.069,.018),(.40,.044,.046,s*.071,.022),
        (.485,.042,.042,s*.071,.008),(.53,.039,.041,s*.072,-.005),
        (.60,.045,.049,s*.075,-.002),(.70,.055,.059,s*.078,.002),
        (.79,.06,.066,s*.077,.004),(.825,.055,.059,s*.075,.004)],stocking,segments=24,sub=1)
    # Boot upper with padded ankle, shaped toe and magenta outsole.
    loft('Boot upper '+side,[(.029,.047,.093,s*.069,-.036),(.045,.05,.098,s*.069,-.038),(.075,.051,.092,s*.069,-.035),(.098,.048,.078,s*.069,-.021),(.125,.042,.053,s*.069,.003),(.18,.043,.044,s*.069,.006),(.222,.047,.045,s*.069,.01),(.232,.046,.044,s*.069,.01)],ink,segments=28,sub=1)
    loft('Boot sole '+side,[(.012,.048,.096,s*.069,-.039),(.017,.051,.101,s*.069,-.039),(.037,.051,.101,s*.069,-.039),(.043,.049,.098,s*.069,-.039)],black,segments=28,sub=1)
    curve('Boot magenta welt '+side,[(s*.069+.05*math.sin(a),-.039-.098*math.cos(a),.026) for a in [2*math.pi*i/40 for i in range(40)]],.0028,pink,True)
    loft('Boot collar '+side,[(.195,.045,.045,s*.069,.01),(.206,.05,.048,s*.069,.01),(.237,.049,.046,s*.069,.01),(.243,.045,.043,s*.069,.01)],slate,segments=24,sub=1)
    box('Boot tongue '+side,(s*.069,-.038,.185),(.041,.012,.10),ink,.005,rotation=(.18,0,0))
    box('Boot silver tag '+side,(s*.069,-.046,.219),(.024,.006,.023),steel,.002)
    for j in range(5):
        z=.09+j*.019
        y=-.092+j*.01
        for t in (-1,1):
            ellipsoid('Lace eyelet',(s*.069+t*.026,y,z),(.0038,.0025,.0038),steel,12,8)
        curve('Boot lace',[ (s*.069-.026,y-.003,z),(s*.069+.026,y+.006,z+.017)],.0014,white)
        curve('Boot lace',[ (s*.069+.026,y-.003,z),(s*.069-.026,y+.006,z+.017)],.0014,white)
    curve('Toe stitch '+side,[(s*.069-.033,-.102,.071),(s*.069,-.119,.074),(s*.069+.033,-.102,.071)],.001,slate)

# Arms, rolled cuffs, wrists and separate fingers.
for s,side in [(-1,'L'),(1,'R')]:
    loft('Sleeve '+side,[(1.061,.039,.043,s*.207,.003),(1.081,.044,.046,s*.205,.006),(1.15,.049,.05,s*.194,.016),(1.23,.055,.052,s*.17,.018),(1.281,.057,.049,s*.147,.014)],ink,segments=24,sub=1)
    loft('White rolled cuff '+side,[(1.049,.045,.049,s*.211,.002),(1.053,.049,.052,s*.211,.002),(1.09,.05,.052,s*.207,.003),(1.101,.047,.049,s*.205,.005)],ivory,segments=24,sub=1)
    curve('Cuff edge '+side,[(s*.211+.047*math.sin(a),.002-.051*math.cos(a),1.056) for a in [2*math.pi*i/24 for i in range(24)]],.0015,slate,True)
    loft('Forearm '+side,[(.908,.024,.025,s*.235,-.009),(.934,.026,.028,s*.232,-.007),(.983,.029,.032,s*.225,-.002),(1.022,.032,.034,s*.219,.001),(1.064,.034,.035,s*.212,.003),(1.079,.033,.035,s*.209,.004)],skin,segments=24,sub=1)
    cylinder_between('Wrist pink band '+side,(s*.235,-.009,.916),(s*.233,-.008,.943),.028,pink,vertices=24)
    cylinder_between('Glove cuff '+side,(s*.237,-.008,.891),(s*.235,-.009,.917),.029,ink,vertices=24)
    ellipsoid('Fingerless glove palm '+side,(s*.24,-.01,.864),(.031,.020,.038),ink)
    box('Glove back opening '+side,(s*.241,-.030,.866),(.029,.006,.030),skin,.005)
    box('Glove wrist buckle '+side,(s*.237,-.034,.904),(.032,.009,.015),steel,.002)
    for j in range(4):
        x=s*(.219+j*.014)
        top=.853-abs(j-1.5)*.003
        cylinder_between('Finger '+side+str(j),(x,-.011,top),(x+s*.002,-.018,top-.031),.006,skin,.0055,12)
        ellipsoid('Fingertip '+side+str(j),(x+s*.002,-.018,top-.031),(.0055,.0058,.006),skin,12,8)
    cylinder_between('Thumb '+side,(s*.216,-.017,.879),(s*.205,-.032,.853),.009,skin,.007,12)
    # Pink arm stripe stays on the outer side of the sleeve.
    curve('Sleeve magenta piping '+side,[(s*.195,-.017,1.266),(s*.211,-.023,1.22),(s*.232,-.022,1.15),(s*.237,-.024,1.102)],.0025,pink)

# Long pink equipment ribbon at the right hip.
plate('Hip ribbon',[(.139,.018,.95),(.155,.02,.951),(.169,.014,.732),(.153,.012,.72)],.005,pink)
box('Ribbon clip',(.148,.009,.932),(.023,.012,.029),gun_dark,.002)
ellipsoid('Ribbon rivet',(.161,.007,.737),(.003,.002,.003),steel,12,8)

# Chest badge and minimalist uniform insignia.
badge=box('Chest ID badge',(.067,-.084,1.218),(.043,.004,.05),white,.0015,rotation=(0,0,-.08))
box('Badge magenta header',(.067,-.088,1.231),(.034,.002,.006),pink,.0003)
box('Badge photo',(.056,-.088,1.216),(.010,.002,.014),slate,.0003)
for i in range(3):
    box('Badge text rule',(.074,-.088,1.222-i*.006),(.014,.001,.0014),slate,.0002)
curve('Chest emblem',[(-.064,-.087,1.193),(-.044,-.089,1.247),(-.043,-.089,1.196)],.003,ink)
curve('Chest emblem crossbar',[(-.069,-.088,1.216),(-.027,-.089,1.234)],.0025,ink)

# HEAD / FACE ---------------------------------------------------------------
head_rings=[
    (1.386,.013,.017,0,-.022),(1.397,.033,.031,0,-.018),
    (1.42,.059,.049,0,-.008),(1.447,.074,.062,0,-.002),
    (1.48,.086,.071,0,.001),(1.514,.09,.078,0,.006),
    (1.55,.087,.077,0,.009),(1.586,.073,.066,0,.01),
    (1.619,.048,.046,0,.01),(1.632,.012,.014,0,.01)]
loft('Face • shaped anime head',head_rings,skin,segments=40,sub=1)

def face_y(x,z,offset=0):
    for a,b in zip(head_rings,head_rings[1:]):
        if a[0] <= z <= b[0]:
            t=(z-a[0])/(b[0]-a[0])
            rx=a[1]*(1-t)+b[1]*t
            ry=a[2]*(1-t)+b[2]*t
            cy=a[4]*(1-t)+b[4]*t
            return cy-ry*math.sqrt(max(.03,1-(x/rx)**2))-offset
    return -.07-offset

for s,side in [(-1,'L'),(1,'R')]:
    ellipsoid('Ear '+side,(s*.088,.005,1.475),(.017,.012,.028),skin)
    ellipsoid('Ear inner '+side,(s*.093,-.005,1.476),(.009,.003,.016),skin_shadow,16,12)
    # Eye silhouette, on the curved face surface.
    outline=[(.015,1.506),(.026,1.518),(.046,1.521),(.065,1.514),(.074,1.505),(.060,1.492),(.040,1.489),(.023,1.494)]
    verts=[(s*x,face_y(s*x,z,.003),z) for x,z in outline]
    verts.append((s*.043,face_y(s*.043,1.504,.004),1.504))
    faces=[(8,i,(i+1)%8) for i in range(8)]
    mesh('Eye white '+side,verts,faces,eye_white)
    cx=s*.044
    eye_center=(cx,face_y(cx,1.504,.006),1.504)
    eye=ellipsoid('Amber iris '+side,eye_center,(.0106,.0022,.0134),iris,24,16)
    eye.rotation_euler.z=s*.36
    ellipsoid('Iris gold lower '+side,(cx,eye_center[1]-.0018,1.498),(.0079,.0015,.0058),iris_light,20,12)
    ellipsoid('Pupil '+side,(cx,eye_center[1]-.003,1.505),(.004,.0015,.008),eye_dark,20,12)
    ellipsoid('Eye highlight '+side,(cx-.0035,eye_center[1]-.0048,1.511),(.003,.001,.0037),white,16,12)
    ellipsoid('Eye secondary glint '+side,(cx+.003,eye_center[1]-.004,1.497),(.0013,.0007,.0015),white,12,8)
    top=outline[:5]
    curve('Upper eyeliner '+side,[(s*x,face_y(s*x,z,.0045),z) for x,z in top],.0018,eye_dark)
    curve('Lower eyeliner '+side,[(s*x,face_y(s*x,z,.004),z) for x,z in outline[4:]],.0008,skin_shadow)
    curve('Eyelash tip '+side,[(s*.065,face_y(s*.065,1.514,.005),1.514),(s*.077,face_y(s*.073,1.517,.005),1.517)],.0013,eye_dark)
    curve('Eyebrow '+side,[(s*.022,face_y(s*.022,1.537,.002),1.537),(s*.044,face_y(s*.044,1.54,.002),1.54),(s*.066,face_y(s*.066,1.535,.002),1.535)],.0017,hair_dark)
    # Sparse, fine blush marks instead of opaque cheek patches.
    for j in range(3):
        x=s*(.054+j*.005)
        curve('Cheek blush',[(x,face_y(x,1.475,.0015),1.475),(x+s*.0018,face_y(x+s*.0018,1.48,.0015),1.48)],.0006,skin_shadow)

ellipsoid('Nose • subtle bridge',(0,-.069,1.481),(.007,.006,.013),skin,20,12)
curve('Nose accent',[(.002,-.078,1.473),(.006,-.075,1.471)],.00065,skin_shadow)
curve('Mouth • slight smile',[(-.014,face_y(-.014,1.445,.002),1.445),(-.003,face_y(-.003,1.442,.002),1.442),(.008,face_y(.008,1.443,.002),1.443)],.00085,eye_dark)
curve('Lower lip light',[(-.005,face_y(-.005,1.438,.002),1.438),(.005,face_y(.005,1.438,.002),1.438)],.0008,skin_shadow)

# HAIR: a continuous cap plus layered solid tapered locks.
verts=[]; faces=[]
for j in range(17):
    for i in range(64):
        a=2*math.pi*i/64
        maxpolar=1.2+1.25*math.sin(a/2)**.65
        polar=.015+(maxpolar-.015)*j/16
        verts.append((.104*math.sin(polar)*math.sin(a),.011-.092*math.sin(polar)*math.cos(a),1.538+.119*math.cos(polar)))
for j in range(16):
    for i in range(64):
        faces.append((j*64+i,j*64+(i+1)%64,(j+1)*64+(i+1)%64,(j+1)*64+i))
cap=mesh('Hair • continuous cap',verts,faces,hair)
solid=cap.modifiers.new('Hair cap thickness','SOLIDIFY'); solid.thickness=.006
finish(cap,sub=1)

# Main fringe sweeps across the character's left eye (viewer right).
fringe=[
 ('Fringe 01', [(-.046,-.035,1.64),(-.068,-.071,1.61),(-.075,-.089,1.568),(-.08,-.083,1.526),(-.086,-.069,1.494)], [.005,.022,.026,.018,.0004], hair),
 ('Fringe 02', [(-.026,-.047,1.646),(-.05,-.083,1.611),(-.058,-.096,1.575),(-.058,-.096,1.543),(-.051,-.092,1.527)], [.004,.024,.022,.014,.0004], hair_light),
 ('Fringe 03', [(-.005,-.045,1.649),(-.019,-.086,1.616),(-.017,-.105,1.582),(-.002,-.108,1.545),(.02,-.104,1.509)], [.004,.027,.03,.023,.0004], hair),
 ('Fringe 04', [(.015,-.039,1.648),(.016,-.077,1.619),(.026,-.101,1.58),(.046,-.109,1.538),(.057,-.098,1.494),(.047,-.086,1.459)], [.004,.026,.035,.034,.023,.0004], hair),
 ('Fringe 05', [(.042,-.02,1.636),(.056,-.065,1.606),(.074,-.085,1.565),(.087,-.088,1.526),(.079,-.074,1.484),(.065,-.060,1.463)], [.004,.025,.025,.024,.018,.0004], hair_light),
]
for name,points,widths,mat in fringe:
    ribbon('Hair • '+name,points,widths,mat,depth=.005,sub=1)

# Side and back locks; tips flare to reproduce the short tousled silhouette.
for s,side in [(-1,'L'),(1,'R')]:
    ribbon('Hair • temple '+side,[(s*.068,-.003,1.615),(s*.093,-.035,1.571),(s*.099,-.041,1.518),(s*.092,-.045,1.465),(s*.098,-.036,1.416)], [.01,.022,.022,.019,.0005],hair,depth=.012,sub=2)
    ribbon('Hair • side sweep '+side,[(s*.082,.017,1.591),(s*.108,.004,1.542),(s*.11,-.004,1.487),(s*.115,-.001,1.445),(s*.139,-.009,1.437)], [.005,.024,.026,.022,.0004],hair_dark,depth=.017,sub=2)
    ribbon('Hair • side highlight '+side,[(s*.087,.008,1.588),(s*.11,-.016,1.543),(s*.113,-.018,1.49),(s*.124,-.017,1.45)], [.002,.009,.01,.0004],hair_light,depth=.006,sub=2)
    ribbon('Hair • lower flick '+side,[(s*.074,.045,1.53),(s*.097,.039,1.48),(s*.104,.028,1.439),(s*.13,.015,1.413)], [.004,.024,.024,.0004],hair,depth=.017,sub=2)
    ribbon('Hair • nape flick '+side,[(s*.058,.069,1.503),(s*.066,.072,1.455),(s*.08,.054,1.419),(s*.112,.045,1.404)], [.004,.026,.024,.0004],hair_dark,depth=.012,sub=2)

for i in range(9):
    a=math.pi*.43 + i*math.pi*1.14/8
    sx=math.sin(a)
    sy=-math.cos(a)
    ribbon('Hair • back layer %02d'%i,
        [(sx*.025,.009+sy*.025,1.652),(sx*.08,.01+sy*.079,1.61),(sx*.102,.01+sy*.097,1.552),(sx*.101,.011+sy*.093,1.486),(sx*.106,.011+sy*.093,1.422+abs(sx)*.018)],
        [.002,.021,.028,.026,.0005],hair_light if i%3==0 else hair,depth=.011,sub=2)

# Fine surface grooves for crown direction, not individual hair strands.
for i in range(7):
    x=-.06+i*.019
    curve('Hair crown fine line',[(x*.3,-.048,1.644),(x*.8,-.079,1.619),(x,-.096,1.582)],.0005,hair_light if i%3==0 else hair_dark)

# Swept grooves follow the crown surface and visually join fringe to back hair.
for i in range(14):
    a=2*math.pi*i/14
    points=[]
    for polar in (.12,.32,.58,.85,1.05):
        points.append((.105*math.sin(polar)*math.sin(a),.011-.093*math.sin(polar)*math.cos(a),1.538+.120*math.cos(polar)))
    curve('Crown strand groove',points,.00065,hair_light if i%3==0 else hair_dark)

# HALO ---------------------------------------------------------------------
bpy.ops.mesh.primitive_torus_add(major_segments=72,minor_segments=10,location=(0,.004,1.737),major_radius=.099,minor_radius=.004)
obj=bpy.context.object; obj.name='Halo • luminous ring'; register(obj,halo)
for p in obj.data.polygons: p.use_smooth=True
curve('Halo fine inner ring',[(.09*math.sin(a),.004+.09*math.cos(a),1.737) for a in [2*math.pi*i/72 for i in range(72)]],.0009,white,True)

# LIGHT WINGS ---------------------------------------------------------------
# Layered angular feather planes sweep backwards in depth, with warm edges.
for s,side in [(-1,'L'),(1,'R')]:
    curve('Wing root '+side,[(s*.061,.074,1.217),(s*.106,.115,1.249),(s*.157,.136,1.285)],.010,ink)
    wing_shapes=[
        [(.092,.103,1.263),(.169,.129,1.331),(.274,.145,1.41),(.359,.17,1.52),(.328,.17,1.398),(.243,.151,1.321),(.155,.119,1.26)],
        [(.105,.117,1.244),(.205,.144,1.28),(.306,.167,1.324),(.443,.195,1.395),(.391,.193,1.298),(.28,.167,1.255),(.173,.132,1.235)],
        [(.104,.12,1.214),(.208,.147,1.22),(.316,.175,1.224),(.463,.204,1.256),(.39,.198,1.199),(.281,.17,1.177),(.175,.141,1.191)],
        [(.105,.114,1.18),(.2,.14,1.17),(.298,.164,1.139),(.416,.184,1.142),(.364,.18,1.095),(.277,.158,1.093),(.164,.129,1.137)],
        [(.096,.107,1.147),(.17,.128,1.113),(.248,.149,1.064),(.33,.169,1.03),(.278,.157,1.016),(.221,.142,1.04),(.141,.117,1.10)]
    ]
    for i,outline in enumerate(wing_shapes):
        coords=[(s*x,y,z) for x,y,z in outline]
        plate('Light feather '+side+str(i),coords,.005,wing,bevel=.001)
        curve('Feather gold edge '+side+str(i),[(s*x,y-.003,z) for x,y,z in outline[:4]],.0012,gold)
        curve('Feather light edge '+side+str(i),[(s*x,y-.004,z) for x,y,z in outline[3:]],.001,white)
    ellipsoid('Wing base clasp '+side,(s*.092,.098,1.211),(.025,.014,.056),ink)

# WEAPON: compact sand/black carbine held barrel-down, separate assembly.
# These are purely visual meshes; there is no shooting or damage logic.
weapon_root=bpy.data.objects.new('Weapon • grip attachment',None)
character.objects.link(weapon_root)
weapon_root.location=(.257,-.005,.849)
model_objects.append(weapon_root)
weapon_start=len(model_objects)
plate('Carbine receiver',[(.265,-.018,.847),(.309,-.018,.853),(.333,-.018,.819),(.328,-.018,.717),(.271,-.018,.70),(.255,-.018,.729)],.031,gun_tan,.003)
box('Carbine black upper rail',(.333,-.018,.777),(.012,.038,.137),gun_dark,.002)
box('Carbine lower foregrip',(.277,-.018,.677),(.028,.032,.098),gun_tan,.003)
box('Carbine barrel shroud',(.307,-.018,.667),(.031,.034,.095),gun_dark,.002)
cylinder_between('Carbine barrel',(.307,-.018,.566),(.307,-.018,.642),.007,gun_dark,vertices=16)
cylinder_between('Carbine muzzle',(.307,-.018,.563),(.307,-.018,.581),.009,black,vertices=16)
box('Carbine pistol grip',(.252,-.018,.81),(.028,.029,.053),gun_dark,.003,rotation=(0,-.23,0))
box('Carbine magazine',(.249,-.018,.733),(.048,.024,.032),gun_dark,.003,rotation=(0,.18,0))
plate('Carbine stock',[(.269,-.018,.849),(.306,-.018,.853),(.306,-.018,.909),(.29,-.018,.93),(.275,-.018,.916)],.029,gun_dark,.003)
box('Carbine stock shoulder pad',(.29,-.018,.925),(.043,.038,.013),black,.002)
for z in (.75,.78,.811):
    cylinder_between('Carbine receiver fastener',(.315,-.037,z),(.315,-.033,z),.003,steel,vertices=12)
for j in range(5):
    box('Carbine rail tooth',(.34,-.018,.728+j*.022),(.008,.04,.008),black,.001)
for z in (.649,.667,.685):
    box('Carbine vent',(.279,-.036,z),(.013,.003,.008),black,.001)
curve('Carbine trigger guard',[(.261,-.035,.821),(.237,-.035,.818),(.235,-.035,.798),(.258,-.035,.792)],.0027,gun_dark)
box('Carbine safety accent',(.294,-.036,.808),(.012,.003,.004),pink,.0006)
for obj in model_objects[weapon_start:]:
    matrix=obj.matrix_world.copy()
    obj.parent=weapon_root
    obj.matrix_world=matrix

# Sleeve lettering is actual mesh geometry, so the export has no font dependency.
def text_object(name,text,location,size,mat,rotation=(math.pi/2,0,0)):
    data=bpy.data.curves.new(name,'FONT'); data.body=text; data.size=size
    data.align_x='CENTER'; data.extrude=.00015; data.resolution_u=3
    obj=bpy.data.objects.new(name,data); obj.location=location; obj.rotation_euler=rotation
    register(obj,mat); return obj
text_object('Sleeve lettering','A N G E L',(.205,-.045,1.16),.013,ivory,rotation=(math.pi/2,0,math.pi/2))
text_object('Cuff label','P.L.',(.211,-.052,1.069),.011,ink)

# EXPORT -------------------------------------------------------------------
root_obj=bpy.data.objects.new('Exusiai_Visual',None)
character.objects.link(root_obj)
for obj in model_objects:
    if obj.parent is None:
        obj.parent=root_obj
root_obj['asset_stage']='Initial static model; no skeleton or animations'
root_obj['reference']='User-provided Exusiai illustration; unseen back details inferred'

# Convert curves and text; apply geometry modifiers for portable exports.
bpy.ops.object.select_all(action='DESELECT')
for obj in model_objects:
    if obj.type in {'CURVE','FONT'}:
        obj.select_set(True)
        bpy.context.view_layer.objects.active=obj
        bpy.ops.object.convert(target='MESH')
        obj.select_set(False)
    if obj.type == 'MESH':
        bpy.context.view_layer.objects.active=obj
        for modifier in list(obj.modifiers):
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        # Generate a non-overlapping UV map per object for future texture work.
        # Current look is defined by material colors, so there are no image dependencies.
        obj.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.015)
        bpy.ops.object.mode_set(mode='OBJECT')
        obj.select_set(False)

bpy.context.view_layer.update()
meshes=[obj for obj in character.objects if obj.type=='MESH']
stats={'mesh_objects':len(meshes),'vertices':sum(len(o.data.vertices) for o in meshes),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),'materials':len(palette),'rigged':False,'animations':False}
corners=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
stats['bounds_m']={axis:[round(min(v[i] for v in corners),4),round(max(v[i] for v in corners),4)] for i,axis in enumerate('XYZ')}
(SOURCE/'model_manifest.json').write_text(json.dumps({'stats':stats,'materials':palette},indent=2)+'\n')

def select_character():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in character.objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=root_obj

# Keep the editable master split into pieces. Export a combined copy grouped by
# material and purpose, reducing runtime draw calls while preserving accessories.
export_collection=bpy.data.collections.new('Export temporary')
scene=bpy.context.scene
scene.collection.children.link(export_collection)
export_root=bpy.data.objects.new('Exusiai_Visual',None)
export_collection.objects.link(export_root)
groups={}
for obj in meshes:
    name=obj.name.lower()
    category=('Hair' if name.startswith(('hair','crown')) else
              'Wings' if name.startswith(('wing','light feather','feather')) else
              'Halo' if name.startswith('halo') else
              'Weapon' if name.startswith('carbine') else
              'Face' if name.startswith(('face','ear','eye','amber','iris','pupil','upper eye','lower eye','cheek','nose','mouth','lower lip')) else 'Body')
    key=(category,obj.data.materials[0].name)
    duplicate=obj.copy(); duplicate.data=obj.data.copy(); duplicate.parent=None
    duplicate.matrix_world=obj.matrix_world.copy()
    export_collection.objects.link(duplicate)
    groups.setdefault(key,[]).append(duplicate)
for (category,mat_name),objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.name=category+'_'+mat_name.removeprefix('EX_')
    matrix=obj.matrix_world.copy(); obj.parent=export_root; obj.matrix_world=matrix
bpy.ops.object.select_all(action='DESELECT')
for obj in export_collection.objects: obj.select_set(True)
bpy.context.view_layer.objects.active=export_root
stats['export_mesh_objects']=len(groups)
(SOURCE/'model_manifest.json').write_text(json.dumps({'stats':stats,'materials':palette},indent=2)+'\n')
bpy.ops.export_scene.fbx(filepath=str(ASSETS/'Models/Exusiai_Prototype.fbx'),use_selection=True,object_types={'MESH','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
bpy.ops.export_scene.gltf(filepath=str(SOURCE/'Exusiai_Prototype.glb'),export_format='GLB',use_selection=True,export_yup=True,export_apply=True)
for obj in list(export_collection.objects): bpy.data.objects.remove(obj,do_unlink=True)
bpy.data.collections.remove(export_collection)

# STUDIO -------------------------------------------------------------------
# Studio is excluded from exported model files.
ground_mat=material('StudioFloor','28313C',.9)
bpy.ops.mesh.primitive_plane_add(size=200)
floor=bpy.context.object; floor.name='Studio floor'; floor.location.z=-.005
register(floor,ground_mat,is_model=False)

def area(name,position,power,size,color,target=(0,0,1.0)):
    data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size; data.color=color
    obj=bpy.data.objects.new(name,data); studio.objects.link(obj); obj.location=position
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
    return obj
area('Key • warm',(-2.8,-4.0,4.0),360,3.0,(1.0,.90,.82))
area('Fill • soft',(2.5,-2.3,2.0),210,2.5,(.80,.88,1.0))
area('Rim • top',(1.2,2.0,3.2),440,2.0,(1.0,.81,.70))
area('Face softbox',(0,-3,1.9),45,1.0,(1.0,.96,.92),target=(0,0,1.5))
world=bpy.context.scene.world
world.use_nodes=True
world.node_tree.nodes.get('Background').inputs[0].default_value=(.12,.15,.20,1)
world.node_tree.nodes.get('Background').inputs[1].default_value=.35
camera_data=bpy.data.cameras.new('Preview Camera')
camera=bpy.data.objects.new('Preview Camera',camera_data); studio.objects.link(camera)
camera_data.type='ORTHO'; camera_data.ortho_scale=2.05
scene=bpy.context.scene; scene.camera=camera
scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=48
scene.cycles.use_denoising=True
scene.render.resolution_x=1100; scene.render.resolution_y=1300; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX'
scene.view_settings.look='AgX - Medium High Contrast'
scene.render.film_transparent=False

def frame_camera(position,target=(0,0,.87),scale=2.05):
    camera.location=position
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale=scale

frame_camera((2.1,-5.8,2.1))
select_character()
# Open the Blender file focused on the character, with the render camera ready.
for screen in bpy.data.screens:
    for ar in screen.areas:
        if ar.type=='VIEW_3D':
            ar.spaces.active.region_3d.view_distance=2.3
            ar.spaces.active.region_3d.view_location=(0,0,.9)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Exusiai_Prototype.blend'))
views=[('three_quarter',(2.1,-5.8,2.1),(0,0,.87),2.05),('front',(0,-6,1.35),(0,0,.87),2.05),('back',(1.8,5.8,1.8),(0,0,.87),2.05),('face',(.65,-4,1.72),(0,-.005,1.49),.60)]
if '--quick' in sys.argv:
    views=views[:1]
for label,pos,target,scale in views:
    frame_camera(pos,target,scale)
    scene.render.filepath=str(PREVIEWS/(label+'.png'))
    bpy.ops.render.render(write_still=True)
print('MODEL_BUILD_COMPLETE '+json.dumps(stats),flush=True)
# bpy's embedded module can crash while destructing at process exit on macOS.
# All files have already been saved synchronously at this point.
import os
sys.stdout.flush()
os._exit(0)
