using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class ExusiaiPreservedFinalize {
 const string Root="Assets/Game/Characters/Exusiai/MMD";
 [Serializable] public class Result { public float scale,groundOffset,bodyHeight,totalHeight;public string prefabGuid,sceneGuid; }
 public static void Build(){try{
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var wrapper=new GameObject("Exusiai_MMD_Visual");
  var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/PreservedRig/Prefabs/Exusiai_MMD_OriginalRig.prefab");
  var model=(GameObject)PrefabUtility.InstantiatePrefab(source);model.transform.SetParent(wrapper.transform,false);
  var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();skin.BakeMesh(mesh);
  var points=mesh.vertices.Select(skin.transform.TransformPoint).ToArray();
  var body=Enumerable.Range(0,24).SelectMany(i=>skin.sharedMesh.GetTriangles(i)).Distinct().ToArray();
  float low=points.Min(v=>v.y),crown=body.Max(i=>points[i].y),scale=1.65f/(crown-low);
  // Only the outer display transform changes: original bind matrices/weights remain untouched.
  model.transform.localScale=Vector3.one*scale;model.transform.localPosition=Vector3.down*low*scale;
  var prefab=Root+"/Prefabs/Exusiai_MMD_Visual.prefab";var scenePath=Root+"/Scenes/Exusiai_MMD_Preview.unity";
  var prefabGuid=AssetDatabase.AssetPathToGUID(prefab);var sceneGuid=AssetDatabase.AssetPathToGUID(scenePath);
  PrefabUtility.SaveAsPrefabAsset(wrapper,prefab);
  var camera=new GameObject("Preview Camera").AddComponent<Camera>();camera.transform.position=new Vector3(1.8f,1.3f,3.6f);camera.transform.LookAt(new Vector3(0,.87f,0));camera.fieldOfView=30;camera.backgroundColor=new Color(.16f,.18f,.23f);camera.clearFlags=CameraClearFlags.SolidColor;
  var light=new GameObject("Key Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(35,150,0);
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
  EditorSceneManager.SaveScene(scene,scenePath);AssetDatabase.SaveAssets();
  if(AssetDatabase.AssetPathToGUID(prefab)!=prefabGuid||AssetDatabase.AssetPathToGUID(scenePath)!=sceneGuid)throw new Exception("Existing GUID changed");
  var result=new Result{scale=scale,groundOffset=-low*scale,bodyHeight=1.65f,totalHeight=(points.Max(v=>v.y)-low)*scale,prefabGuid=prefabGuid,sceneGuid=sceneGuid};
  File.WriteAllText("preserved-display.json",JsonUtility.ToJson(result,true));Debug.Log("PRESERVED_FINALIZE_OK");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
