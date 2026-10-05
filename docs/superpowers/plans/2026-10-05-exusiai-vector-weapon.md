# Exusiai Independent Vector Weapon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans inline in this conversation. Steps use checkbox syntax for tracking. The user has authorized an independent weapon deliverable and reserved animation attachment for later visual approval.

**Goal:** Produce a simplified, colored Vector weapon with editable source and an independent Unity preview, ready for the user's visual review.

**Architecture:** Preserve the supplied FBX, derive one static mesh, assign four paint zones through one palette material, normalize its grip origin and forward direction, and generate an isolated prefab/scene. Import and geometry gates run before selective delivery. Existing character and animation assets remain unchanged.

**Tech Stack:** Existing Blender 4.5 bpy environment, Python standard library PNG writer, Unity 6000.3.25f1, URP 17.3.

## Global Constraints

- Current model branch and checkout; no new branch, commit, merge or subagents.
- Input: ArtSource/Exusiai/VectorSMG.fbx, 114,014 triangles, one mesh/material, no textures; original file unchanged.
- Authoring: ArtSource/Exusiai/Weapon. Unity: Assets/Game/Weapons/ExusiaiVector.
- Default sandy body and dark details, following the user's original reference; honor a later color preference without attaching the weapon to animations.
- One static renderer/submesh/material, no Animator, bones, collider or runtime behavior; plus three empty reference transforms.
- Length 0.58 m; Unity muzzle +Z, up +Y, origin near primary grip. This is a proposed game scale for visual review.
- Target at most 15,000 triangles; 8,000–15,000 is a budget target, not a minimum that requires adding unnecessary triangles.
- Source-to-derived sampled surface error: max 3 mm, p99 1.5 mm. Visually inspect silhouette separately.
- No image synthesis is needed for a four-color palette texture. Material is opaque; source imported Alpha=0 must not propagate.
- Use existing isolated project /private/tmp/exusiai-weapon-check; do not run exit-capable tools in the user's main Unity.

## File Map

- ArtSource/Exusiai/Weapon/Source/VectorSMG.fbx: immutable working copy.
- Authoring/settings.json and Exusiai_Vector.blend: editable configuration/model, preserving a hidden original reference.
- Export/Exusiai_Vector.fbx and Export/Textures: derived source export and palette maps.
- Tools/build_weapon.py: Blender construction/export, source audit and surface validation.
- Tools/ExusiaiWeaponAcceptance.cs: batch integration gate, temporary Assets/Editor only.
- Tools/ExusiaiWeaponBuild.cs: prefab, URP material and independent scene builder, temporary Assets/Editor only.
- Tools/ExusiaiWeaponRender.cs: five-angle Unity images, temporary Assets/Editor only.
- Validation/model-audit.json, unity-acceptance.json, delivery-validation.json, existing-character-hashes.json.
- Previews: original inspection and five final Unity views.
- Unity Models/Exusiai_Vector.fbx, Textures/Exusiai_Vector_BaseColor.png and MetallicGloss.png, Materials/Exusiai_Vector.mat and Exusiai_Vector_Stand.mat, Prefabs/Exusiai_Vector.prefab, Scenes/Preview_Exusiai_Vector.unity, and all .meta files.

## Task 1: Independent asset gate and preserved inputs

**Interfaces:** ExusiaiWeaponAcceptance.Run() consumes the final standalone prefab and scene; outputs unity-acceptance.json and exit 0/1. Missing prefab must fail first. This is a batch integration check, not a NUnit test.

- [x] Capture SHA256 of input and every existing file in Assets/Game/Characters/Exusiai/Model, then copy input into Weapon/Source.
- [x] Write the following gate to ArtSource/Tools and the isolated Assets/Editor. Run Unity -batchmode -nographics -projectPath /private/tmp/exusiai-weapon-check -executeMethod ExusiaiWeaponAcceptance.Run -logFile /private/tmp/exusiai-weapon-red.log. Expect exit 1 with Missing independent weapon prefab.

```csharp
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ExusiaiWeaponAcceptance
{
    private const string Root = "Assets/Game/Weapons/ExusiaiVector";
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    public static void Run()
    {
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Exusiai_Vector.prefab");
            Require(prefab, "Missing independent weapon prefab");
            var go = UnityEngine.Object.Instantiate(prefab);
            Require(go.transform.localScale == Vector3.one, "Weapon root has hidden scale");
            Require(go.GetComponentsInChildren<Animator>().Length == 0 && go.GetComponentsInChildren<SkinnedMeshRenderer>().Length == 0,
                "Weapon unexpectedly has character animation");
            Require(go.GetComponentsInChildren<Collider>().Length == 0, "Preview gun has gameplay colliders");
            var renderers = go.GetComponentsInChildren<MeshRenderer>();
            Require(renderers.Length == 1 && renderers[0].sharedMaterials.Length == 1, "Weapon is not one renderer/material");
            var mesh = renderers[0].GetComponent<MeshFilter>().sharedMesh;
            long triangles = (long)mesh.GetIndexCount(0) / 3;
            Require(mesh.subMeshCount == 1 && triangles > 0 && triangles <= 15000, "Weapon exceeds triangle budget: " + triangles);
            var material = renderers[0].sharedMaterial;
            Require(material && material.shader.name == "Universal Render Pipeline/Lit", "Weapon shader is not URP Lit");
            Require(material.GetFloat("_Surface") == 0 && material.GetColor("_BaseColor").a == 1, "Weapon material is transparent");
            var texture = material.GetTexture("_BaseMap") as Texture2D;
            Require(texture && texture.GetPixels().All(p => p.a > 0.99f), "Weapon color texture is missing/transparent");
            var bounds = renderers[0].bounds;
            Require(float.IsFinite(bounds.size.sqrMagnitude) && Mathf.Abs(bounds.size.z - 0.58f) < 0.003f,
                "Weapon size or forward axis is wrong: " + bounds.size);
            Require(bounds.size.x > 0.03f && bounds.size.x < 0.06f && bounds.size.y > 0.2f && bounds.size.y < 0.35f,
                "Weapon thickness/height is wrong");
            var named = go.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
            Require(named["PrimaryGrip"].localPosition == Vector3.zero, "Primary grip origin changed");
            Require(named["Muzzle"].localPosition.z > 0.3f && Vector3.Dot(named["Muzzle"].forward, go.transform.forward) > 0.999f,
                "Muzzle orientation wrong");
            Require(Mathf.Abs(bounds.max.z - 0.3681f) < 0.001f && Mathf.Abs(named["Muzzle"].position.z - bounds.max.z) < 0.001f,
                "Muzzle marker does not match the front end of imported geometry");
            Require(named["SupportGrip"].localPosition.z > 0.15f, "Support grip reference missing");
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(Root + "/Scenes/Preview_Exusiai_Vector.unity"), "Independent scene missing");
            string result = "{\"passed\":true,\"triangles\":" + triangles + ",\"unity_vertices\":" + mesh.vertexCount +
                ",\"length_m\":" + bounds.size.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"renderers\":1,\"materials\":1,\"opaque\":true,\"no_character_animation\":true}";
            File.WriteAllText("weapon-unity-acceptance.json", result);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("EXUSIAI_WEAPON_ACCEPTANCE_OK " + result); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
```

## Task 2: Derived geometry, palette and editable source

**Interfaces:** build_weapon.py consumes Weapon/Source/VectorSMG.fbx and Authoring/settings.json; produces Export FBX/textures, editable blend, and Validation/model-audit.json. The audit's muzzle_unity and support_grip_unity are float[3] used by the Unity builder.

- [x] Write the complete authoring script below. Its default settings are saved to JSON before export; subsequent runs reuse that file.
- [x] Run /private/tmp/exusiai-preserved-rig/venv/bin/python ArtSource/Exusiai/Weapon/Tools/build_weapon.py. Require source unchanged, finite geometry, triangle budget and surface error gates.

```python
import bpy, bmesh, hashlib, json, math, struct, zlib
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Source/VectorSMG.fbx'
for name in ['Authoring', 'Export/Textures', 'Validation']:
    (ROOT / name).mkdir(parents=True, exist_ok=True)
settings_path = ROOT / 'Authoring/settings.json'
defaults = {'length_m': 0.58, 'triangle_target': 12000,
            'primary_grip_source': [1.15, 0, 0.15],
            'support_grip_source': [-2.6, 0, 0.25],
            'muzzle_source': [-4.5665016, 0, 0.7390612],
            'palette_srgb': [[163, 151, 117], [35, 40, 47], [66, 72, 79], [25, 28, 32]],
            'metallic': [0.05, 0.20, 0.75, 0], 'smoothness': [0.30, 0.35, 0.45, 0.18]}
settings = json.loads(settings_path.read_text()) if settings_path.exists() else defaults
settings_path.write_text(json.dumps(settings, indent=2) + '\n')
source_hash = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(SOURCE), use_image_search=False, use_anim=False)
objects = [o for o in bpy.data.objects if o.type == 'MESH']
assert len(objects) == 1, 'Unexpected source mesh count'
gun = objects[0]; gun.name = 'Exusiai_Vector_Geometry'
bpy.context.view_layer.objects.active = gun; gun.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mesh = gun.data; mesh.calc_loop_triangles(); original_triangles = len(mesh.loop_triangles)
points = [v.co.copy() for v in mesh.vertices]
width = max(v.x for v in points) - min(v.x for v in points)
scale = settings['length_m'] / width
pivot = Vector(settings['primary_grip_source'])
def normalized(v):
    v = (Vector(v) - pivot) * scale
    return Vector((v.y, -v.x, v.z))
def unity(v):
    v = normalized(v)
    return [v.x, v.z, v.y]

mesh.materials.clear()
for name, color in zip(['Sand', 'Dark', 'Steel', 'Rubber'], settings['palette_srgb']):
    mat = bpy.data.materials.new(name); mat.diffuse_color = (*[c / 255 for c in color], 1); mesh.materials.append(mat)
def assign_paint(data, coordinates):
    parent = list(range(len(data.vertices)))
    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]; x = parent[x]
        return x
    for edge in data.edges:
        a, b = map(find, edge.vertices)
        if a != b: parent[b] = a
    counts = {}
    for v in data.vertices: counts[find(v.index)] = counts.get(find(v.index), 0) + 1
    body = max(counts, key=counts.get)
    for poly in data.polygons:
        island = find(poly.vertices[0]); center = sum((coordinates[i] for i in poly.vertices), Vector()) / len(poly.vertices)
        poly.material_index = 1
        if island == body and center.x < 0.4 and center.z < 1.23: poly.material_index = 0
        elif center.x < -3.5: poly.material_index = 2
        elif center.x > 3.95: poly.material_index = 3
assign_paint(mesh, points)
for vertex in mesh.vertices: vertex.co = normalized(vertex.co)
mesh.update()
reference = gun.copy(); reference.data = gun.data.copy(); reference.name = 'Original_Reference'
collection = bpy.data.collections.new('Original_Reference'); bpy.context.scene.collection.children.link(collection)
collection.objects.link(reference); reference.hide_render = True; reference.hide_set(True); collection.hide_render = True

planar = gun.modifiers.new('CoplanarCleanup', 'DECIMATE'); planar.decimate_type = 'DISSOLVE'
planar.angle_limit = math.radians(0.1); planar.delimit = {'MATERIAL'}
bpy.ops.object.modifier_apply(modifier=planar.name)
gun.data.calc_loop_triangles(); planar_triangles = len(gun.data.loop_triangles)
if planar_triangles > settings['triangle_target']:
    collapse = gun.modifiers.new('GameBudget', 'DECIMATE'); collapse.ratio = settings['triangle_target'] / planar_triangles
    bpy.ops.object.modifier_apply(modifier=collapse.name)
bm = bmesh.new(); bm.from_mesh(gun.data)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
# Split paint regions AFTER simplification; collapse can erase pre-existing paint boundaries.
for co, normal in [((0.4, 0, 0), (0, -1, 0)), ((0, 0, 1.23), (0, 0, 1)),
                   ((-3.5, 0, 0), (0, -1, 0)), ((3.95, 0, 0), (0, -1, 0))]:
    bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                           dist=1e-7, plane_co=normalized(co), plane_no=normal, clear_inner=False, clear_outer=False)
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces)); bm.to_mesh(gun.data); bm.free()
coordinates = [Vector((-v.co.y / scale, v.co.x / scale, v.co.z / scale)) + pivot for v in gun.data.vertices]
assign_paint(gun.data, coordinates)
mesh = gun.data
for polygon in mesh.polygons: polygon.use_smooth = True
normal = gun.modifiers.new('SurfaceNormals', 'WEIGHTED_NORMAL'); normal.keep_sharp = True; normal.weight = 50
bpy.ops.object.modifier_apply(modifier=normal.name)
mesh.calc_loop_triangles()
assert 0 < len(mesh.loop_triangles) <= 15000, 'Triangle budget failed'
assert all(math.isfinite(c) for v in mesh.vertices for c in v.co), 'Nonfinite geometry'
def tree(data):
    data.calc_loop_triangles()
    return BVHTree.FromPolygons([v.co for v in data.vertices], [list(t.vertices) for t in data.loop_triangles], all_triangles=True)
original_tree, game_tree = tree(reference.data), tree(mesh)
errors = [game_tree.find_nearest(v.co)[3] for v in list(reference.data.vertices)[::8]]
errors += [original_tree.find_nearest(v.co)[3] for v in mesh.vertices]
errors.sort(); p99 = errors[int((len(errors) - 1) * 0.99)]
assert errors[-1] <= 0.003 and p99 <= 0.0015, ('Surface error', errors[-1], p99)

def png(path, colors):
    width, height = 32, 8
    rows = b''.join(b'\0' + b''.join(bytes(colors[x // 8]) for x in range(width)) for _ in range(height))
    def chunk(kind, data): return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) +
                     chunk(b'IDAT', zlib.compress(rows)) + chunk(b'IEND', b''))
color_path = ROOT / 'Export/Textures/Exusiai_Vector_BaseColor.png'
gloss_path = ROOT / 'Export/Textures/Exusiai_Vector_MetallicGloss.png'
png(color_path, [color + [255] for color in settings['palette_srgb']])
png(gloss_path, [[round(m * 255), 0, 0, round(s * 255)] for m, s in zip(settings['metallic'], settings['smoothness'])])
slots = [p.material_index for p in mesh.polygons]
while mesh.uv_layers: mesh.uv_layers.remove(mesh.uv_layers[0])
uv = mesh.uv_layers.new(name='PaletteUV')
for poly, slot in zip(mesh.polygons, slots):
    for loop in poly.loop_indices: uv.data[loop].uv = ((slot + 0.5) / 4, 0.5)
palette = bpy.data.materials.new('Exusiai_Vector'); palette.use_nodes = True
bsdf = palette.node_tree.nodes.get('Principled BSDF'); bsdf.inputs['Alpha'].default_value = 1
bsdf.inputs['Roughness'].default_value = 0.65
texture = palette.node_tree.nodes.new('ShaderNodeTexImage'); texture.image = bpy.data.images.load(str(color_path)); texture.interpolation = 'Closest'; texture.image.pack()
palette.node_tree.links.new(texture.outputs['Color'], bsdf.inputs['Base Color'])
mesh.materials.clear(); mesh.materials.append(palette)
for poly in mesh.polygons: poly.material_index = 0
lo = [min(v.co[i] for v in mesh.vertices) for i in range(3)]; hi = [max(v.co[i] for v in mesh.vertices) for i in range(3)]
audit = {'source_sha256': source_hash, 'source_triangles': original_triangles, 'planar_triangles': planar_triangles,
         'triangles': len(mesh.loop_triangles), 'vertices': len(mesh.vertices), 'length_m': settings['length_m'],
         'dimensions_blender_m': [b - a for a, b in zip(lo, hi)], 'max_surface_error_m': errors[-1], 'p99_surface_error_m': p99,
         'palette_face_counts': [slots.count(i) for i in range(4)], 'muzzle_unity': unity(settings['muzzle_source']),
         'support_grip_unity': unity(settings['support_grip_source']), 'primary_grip_unity': [0, 0, 0]}
(ROOT / 'Validation/model-audit.json').write_text(json.dumps(audit, indent=2) + '\n')
bpy.ops.object.select_all(action='DESELECT'); gun.select_set(True); bpy.context.view_layer.objects.active = gun
bpy.ops.export_scene.fbx(filepath=str(ROOT / 'Export/Exusiai_Vector.fbx'), use_selection=True, object_types={'MESH'},
                         axis_forward='-Z', axis_up='Y', apply_unit_scale=True, add_leaf_bones=False, bake_anim=False,
                         path_mode='STRIP', mesh_smooth_type='FACE')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / 'Authoring/Exusiai_Vector.blend'))
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == source_hash, 'Source changed'
print('EXUSIAI_WEAPON_GEOMETRY_OK ' + json.dumps(audit))
```

## Task 3: Standalone Unity prefab and scene

**Interfaces:** ExusiaiWeaponBuild.Build() consumes derived FBX, palette PNGs and model-audit.json copied into the isolated project; creates one material/prefab and one independent scene. Reference points come from the model audit.

- [x] Copy Export/Exusiai_Vector.fbx into isolated Assets/Game/Weapons/ExusiaiVector/Models and palette PNGs into Textures. Copy the source audit to isolated ArtSource/Exusiai/Weapon/Validation.
- [x] Write the complete builder below to ArtSource/Tools and isolated Assets/Editor, then run ExusiaiWeaponBuild.Build in Unity batch mode. Run Task 1 gate again and require exit 0.

```csharp
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ExusiaiWeaponBuild
{
    public const string Root = "Assets/Game/Weapons/ExusiaiVector";
    [Serializable] private sealed class Audit { public float[] muzzle_unity, support_grip_unity; }
    private static T Put<T>(T asset, string path) where T : UnityEngine.Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing) { EditorUtility.CopySerialized(asset, existing); UnityEngine.Object.DestroyImmediate(asset); return existing; }
        AssetDatabase.CreateAsset(asset, path); return asset;
    }
    private static Texture2D Texture(string name, bool color)
    {
        string path = Root + "/Textures/" + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture = color; importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false; importer.mipmapEnabled = false; importer.isReadable = true;
        importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);
    public static void Build()
    {
        try
        {
            foreach (string name in new[] { "Models", "Textures", "Materials", "Prefabs", "Scenes" }) Directory.CreateDirectory(Root + "/" + name);
            AssetDatabase.Refresh();
            string path = Root + "/Models/Exusiai_Vector.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.globalScale = 1;
            importer.useFileScale = true; importer.bakeAxisConversion = true; importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.None;
            importer.meshCompression = ModelImporterMeshCompression.Off; importer.SaveAndReimport();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Exusiai_Vector" };
            material.SetColor("_BaseColor", Color.white); material.SetTexture("_BaseMap", Texture("Exusiai_Vector_BaseColor", true));
            material.SetTexture("_MetallicGlossMap", Texture("Exusiai_Vector_MetallicGloss", false));
            material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1);
            material.SetFloat("_Surface", 0); material.SetFloat("_Cull", 2); material.enableInstancing = true;
            material = Put(material, Root + "/Materials/Exusiai_Vector.mat");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var weapon = new GameObject("Exusiai_Vector");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            visual.name = "Exusiai_Vector_Visual"; visual.transform.SetParent(weapon.transform, false);
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = material;
            var audit = JsonUtility.FromJson<Audit>(File.ReadAllText("ArtSource/Exusiai/Weapon/Validation/model-audit.json"));
            foreach (string name in new[] { "PrimaryGrip", "SupportGrip", "Muzzle" })
            {
                var point = new GameObject(name).transform; point.SetParent(weapon.transform, false);
                point.localPosition = name == "Muzzle" ? V(audit.muzzle_unity) : name == "SupportGrip" ? V(audit.support_grip_unity) : Vector3.zero;
            }
            PrefabUtility.SaveAsPrefabAsset(weapon, Root + "/Prefabs/Exusiai_Vector.prefab");
            UnityEngine.Object.DestroyImmediate(weapon);
            weapon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Exusiai_Vector.prefab"));
            var standMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Exusiai_Vector_Stand" };
            standMaterial.SetColor("_BaseColor", new Color(0.12f, 0.15f, 0.18f)); standMaterial.SetFloat("_Smoothness", 0.12f);
            standMaterial = Put(standMaterial, Root + "/Materials/Exusiai_Vector_Stand.mat");
            var stand = GameObject.CreatePrimitive(PrimitiveType.Cube); stand.name = "DisplayStand";
            var bounds = weapon.GetComponentInChildren<MeshRenderer>().bounds;
            stand.transform.position = new Vector3(0, bounds.min.y - 0.018f, bounds.center.z);
            stand.transform.localScale = new Vector3(0.26f, 0.018f, 0.70f);
            stand.GetComponent<Renderer>().sharedMaterial = standMaterial; UnityEngine.Object.DestroyImmediate(stand.GetComponent<Collider>());
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.4f, 0.43f, 0.47f);
            foreach (var entry in new[] { ("KeyLight", new Vector3(45, -35, 0), 1.3f), ("FillLight", new Vector3(20, 150, 0), 0.6f) })
            {
                var light = new GameObject(entry.Item1).AddComponent<Light>(); light.type = LightType.Directional;
                light.intensity = entry.Item3; light.transform.rotation = Quaternion.Euler(entry.Item2); light.shadows = LightShadows.Soft;
            }
            var camera = new GameObject("WeaponPreviewCamera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.orthographic = true; camera.orthographicSize = 0.25f; camera.nearClipPlane = 0.01f; camera.farClipPlane = 10;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.045f, 0.06f, 0.08f); camera.allowHDR = false;
            camera.transform.position = new Vector3(0.95f, 0.28f, 0.80f); camera.transform.LookAt(bounds.center);
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), Root + "/Scenes/Preview_Exusiai_Vector.unity");
            AssetDatabase.SaveAssets(); Debug.Log("EXUSIAI_WEAPON_BUILD_OK"); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
```

## Task 4: Visual review and selective delivery

**Interfaces:** ExusiaiWeaponRender.Start() consumes the independent scene and writes weapon-previews/*.png. It uses graphics; no -nographics argument.

- [x] Write the tool below, run ExusiaiWeaponRender.Start and inspect all five views. Reject lost silhouette, distorted sights, material transparency/pink, paint bleeding, reversed muzzle or suspicious holes.
- [x] Copy only the new isolated Assets/Game/Weapons subtree and .meta, preserving any existing Weapons.meta; verify new GUID references against project/package metas, hashes against tested files and the entire character baseline.
- [x] Deliver final PNGs/audits and README. Record exact triangle/error/size values. Independent stage finished; user subsequently confirmed appearance and authorized attachment in 2026-10-05-exusiai-equipped-vector.md.

```csharp
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ExusiaiWeaponRender
{
    private static int updates;
    public static void Start()
    {
        EditorSceneManager.OpenScene(ExusiaiWeaponBuild.Root + "/Scenes/Preview_Exusiai_Vector.unity");
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/PreviewRenderer.asset");
        if (!data) { data = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(data, "Assets/PreviewRenderer.asset"); }
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/PreviewPipeline.asset");
        if (!pipeline) { pipeline = UniversalRenderPipelineAsset.Create(data); AssetDatabase.CreateAsset(pipeline, "Assets/PreviewPipeline.asset"); }
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (++updates < 50) return; EditorApplication.update -= Tick;
        try
        {
            Directory.CreateDirectory("weapon-previews");
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>(); camera.aspect = 1000f / 650;
            var gun = GameObject.Find("Exusiai_Vector"); var bounds = gun.GetComponentInChildren<MeshRenderer>().bounds;
            var target = bounds.center;
            var prepare = typeof(RenderPipelineManager).GetMethod("TryPrepareRenderPipeline", BindingFlags.Static | BindingFlags.NonPublic);
            prepare.Invoke(null, new object[] { GraphicsSettings.defaultRenderPipeline });
            foreach (var view in new[] { ("three-quarter", new Vector3(0.95f, 0.28f, 0.80f)),
                     ("side", new Vector3(1, 0.03f, target.z)), ("reverse-side", new Vector3(-1, 0.03f, target.z)),
                     ("muzzle", new Vector3(0.10f, 0.10f, 1)), ("stock", new Vector3(-0.10f, 0.10f, -1)) })
            {
                camera.transform.position = view.Item2; camera.transform.LookAt(target); camera.orthographicSize = 0.24f;
                var rt = new RenderTexture(1000, 650, 24); rt.Create();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                RenderPipeline.SubmitRenderRequest(camera, request); RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = rt; var texture = new Texture2D(1000, 650, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1000, 650), 0, 0); texture.Apply();
                File.WriteAllBytes("weapon-previews/" + view.Item1 + ".png", texture.EncodeToPNG());
                RenderTexture.active = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture);
            }
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
```

## Self-review

Scope covers supplied material, simpler static presentation, official-inspired color, independent directory/scene and deferred animation attachment. Gate signatures, audit array names and paths match all sources. Geometric error plus actual Unity material/size checks provide meaningful validation; no gameplay/runtime tests or unrelated refactors are required.

## Final verification (2026-10-05)

- Blender: 114,014 → 12,978 triangles; sampled max surface error 0.1895 mm, p99 0.1069 mm.
- Unity: 12,952 triangles, 12,427 imported vertices, length 0.5799438 m; one renderer/material, opaque, actual muzzle at +Z.
- Fresh independent batch builder, asset acceptance and five-view URP renderer exited 0. Paint boundaries cut after decimation; final five images inspected.
- Tested assets delivered selectively. Character/input SHA256 and GUID validation run with Weapon/Tools/verify_delivery.py.
- User explicitly selected sand/black colors. Independent appearance subsequently approved; attachment completed under the equipped Vector plan.
- Remain on the existing model branch per user instruction; no branch integration or commit requested.
