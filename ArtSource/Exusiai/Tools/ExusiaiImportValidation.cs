using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class ExusiaiImportValidation
{
    [Serializable] public class Palette { public Entry[] materials; }
    [Serializable] public class Entry { public string name; public string hex; public float roughness; public float metallic; public float emission; }
    public static void Build()
    {
        const string root = "Assets/Game/Characters/Exusiai";
        const string modelPath = root + "/Models/Exusiai_Prototype.fbx";
        var palette = JsonUtility.FromJson<Palette>(File.ReadAllText("palette.json"));
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new Exception("URP Lit shader not found");
        var materials = new Dictionary<string, Material>();
        foreach (var entry in palette.materials)
        {
            string path = root + "/Materials/" + entry.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            ColorUtility.TryParseHtmlString("#" + entry.hex, out var color);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 1f - entry.roughness);
            material.SetFloat("_Metallic", entry.metallic);
            if (entry.emission > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * entry.emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            EditorUtility.SetDirty(material);
            materials[entry.name] = material;
        }
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.importCameras = false;
        importer.importLights = false;
        importer.globalScale = 1f;
        importer.isReadable = true;
        foreach (var pair in materials)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (!model) throw new Exception("FBX import failed");
        var rootObject = new GameObject("Exusiai_Visual");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = "Model";
        instance.transform.SetParent(rootObject.transform, false);
        // Verify the face points toward Unity +Z, independent of FBX axis conventions.
        var irisMesh = instance.GetComponentsInChildren<MeshFilter>().First(m => m.name == "Face_IrisAmber");
        float irisZ = irisMesh.sharedMesh.vertices.Average(v => irisMesh.transform.TransformPoint(v).z);
        if (irisZ < 0) instance.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var renderers = rootObject.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) throw new Exception("No mesh renderers");
        var bounds = renderers[0].bounds;
        foreach (var r in renderers)
        {
            bounds.Encapsulate(r.bounds);
            if (r.sharedMaterials.Any(m => m == null || m.shader != shader))
                throw new Exception("Missing or non-URP material on " + r.name);
        }
        // Use actual vertices: rotated mesh bounding boxes can extend below the soles.
        var vertices = instance.GetComponentsInChildren<MeshFilter>()
            .SelectMany(m => m.sharedMesh.vertices.Select(v => m.transform.TransformPoint(v))).ToArray();
        float lowestVertex = vertices.Min(v => v.y);
        instance.transform.localPosition = Vector3.down * lowestVertex;
        bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        if (bounds.size.y < 1.5f || bounds.size.y > 1.9f || bounds.size.x > 1.2f)
            throw new Exception("Unexpected model dimensions: " + bounds.size);
        string prefabPath = root + "/Prefabs/Exusiai_Visual.prefab";
        PrefabUtility.SaveAsPrefabAsset(rootObject, prefabPath);
        UnityEngine.Object.DestroyImmediate(rootObject);
        importer.isReadable = false;
        importer.SaveAndReimport();
        AssetDatabase.SaveAssets();
        var check = PrefabUtility.LoadPrefabContents(prefabPath);
        if (check.GetComponentsInChildren<MeshRenderer>().Length != renderers.Length)
            throw new Exception("Prefab renderer count changed");
        int triangles = check.GetComponentsInChildren<MeshFilter>().Sum(m => m.sharedMesh.GetIndexCount(0) > 0 ? (int)m.sharedMesh.GetIndexCount(0) / 3 : 0);
        PrefabUtility.UnloadPrefabContents(check);
        string result = "IMPORT_OK\nUnity " + Application.unityVersion + "\nRenderers: " + renderers.Length + "\nTriangles: " + triangles + "\nRenderer bounds: " + bounds.size + "\nGeometry height: " + (vertices.Max(v => v.y) - lowestVertex) + " m\nForward: +Z\nFeet: grounded by actual mesh vertices\nMaterials: " + materials.Count + "\nPrefab: " + prefabPath + "\n";
        File.WriteAllText("import-validation.txt", result);
        Debug.Log(result);
        EditorApplication.Exit(0);
    }
}
