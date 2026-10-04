using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;
public static class ExusiaiPreservedImport {
 const string Root="Assets/Game/Characters/Exusiai/MMD";
 const string Preserved=Root+"/PreservedRig";
 [Serializable] public class Entry { public string name,label,texture; }
 [Serializable] public class Manifest { public Entry[] materials; }
 [Serializable] public class Report { public string unity; public int vertices,blendShapes;public string[] shapeNames,boneNames;public float duration,animationDisplacement,blinkDisplacement;public bool generic,avatarValid; }
 public static void Build(){try{Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);File.WriteAllText("preserved-error.txt",e.ToString());EditorApplication.Exit(1);}}
 static void Run(){
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("mmd-manifest.json"));
  var path=Preserved+"/Models/Exusiai_MMD_Preserved.fbx";
  var importer=(ModelImporter)AssetImporter.GetAtPath(path);
  importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
  importer.importAnimation=true;importer.importBlendShapes=true;importer.isReadable=true;importer.importCameras=false;importer.importLights=false;
  importer.globalScale=1;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.optimizeGameObjects=false;
  importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
  foreach(var e in manifest.materials) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),e.label),AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+e.name+".mat"));
  importer.SaveAndReimport();
  var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var go=(GameObject)PrefabUtility.InstantiatePrefab(model);go.name="Exusiai_MMD_OriginalRig";
  var animator=go.GetComponent<Animator>();if(!animator||!animator.avatar||!animator.avatar.isValid||animator.avatar.isHuman)throw new Exception("Generic avatar invalid");
  var skins=go.GetComponentsInChildren<SkinnedMeshRenderer>();if(skins.Length!=1)throw new Exception("Unexpected mesh count");var skin=skins[0];skin.updateWhenOffscreen=true;skin.quality=SkinQuality.Bone4;
  if(skin.bones.Any(b=>!b))throw new Exception("Missing skin bones");
  // The source slot order is stable; explicitly assign the existing 25 cutout materials.
  skin.sharedMaterials=manifest.materials.Select(e=>AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+e.name+".mat")).ToArray();
  if(skin.sharedMaterials.Any(m=>!m||m.GetFloat("_Cull")!=0||m.GetFloat("_AlphaClip")!=1))throw new Exception("Cutout material regression");
  var imported=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__"));
  var clipPath=Preserved+"/Animations/Exusiai_OriginalRigCheck.anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
  if(clip)EditorUtility.CopySerialized(imported,clip);else{clip=UnityEngine.Object.Instantiate(imported);AssetDatabase.CreateAsset(clip,clipPath);}
  clip.name="Exusiai_OriginalRigCheck";var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
  var controllerPath=Preserved+"/Animations/Exusiai_OriginalRigCheck.controller";
  var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
  if(!controller){controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);controller.layers[0].stateMachine.AddState("OriginalRigCheck");}
  controller.layers[0].stateMachine.states[0].state.motion=clip;animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
  var graph=PlayableGraph.Create("PreservedRigValidation");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
  var output=AnimationPlayableOutput.Create(graph,"Animation",animator);var playable=AnimationClipPlayable.Create(graph,clip);output.SetSourcePlayable(playable);graph.Play();
  var baked=new Mesh();Vector3[] first=null,last=null;float delta=0;
  using(var writer=new BinaryWriter(File.Create("preserved-vertices.bin"))){
   for(int frame=0;frame<=90;frame+=3){playable.SetTime(frame/30.0);graph.Evaluate(0);skin.BakeMesh(baked);var v=baked.vertices;
    if(first==null)first=v;else for(int i=0;i<v.Length;i++)delta=Mathf.Max(delta,(v[i]-first[i]).magnitude);
    foreach(var p in v){var world=skin.transform.TransformPoint(p);writer.Write(world.x);writer.Write(world.y);writer.Write(world.z);}last=v;
   }
  }
  var shapeNames=Enumerable.Range(0,skin.sharedMesh.blendShapeCount).Select(skin.sharedMesh.GetBlendShapeName).ToArray();
  int blink=Array.FindIndex(shapeNames,n=>n.Contains("まばたき"));if(blink<0)throw new Exception("Blink shape missing");
  skin.SetBlendShapeWeight(blink,100);skin.BakeMesh(baked);var blinkPoints=baked.vertices;float blinkDelta=Enumerable.Range(0,last.Length).Max(i=>(blinkPoints[i]-last[i]).magnitude);skin.SetBlendShapeWeight(blink,0);
  if(delta<.005f||blinkDelta<.0001f)throw new Exception("Animation or morph does not deform mesh");
  playable.SetTime(0);graph.Evaluate(0);graph.Destroy();
  PrefabUtility.SaveAsPrefabAsset(go,Preserved+"/Prefabs/Exusiai_MMD_OriginalRig.prefab");
  var report=new Report{unity=Application.unityVersion,vertices=skin.sharedMesh.vertexCount,blendShapes=shapeNames.Length,shapeNames=shapeNames,boneNames=go.GetComponentsInChildren<Transform>().Select(t=>t.name).ToArray(),duration=clip.length,animationDisplacement=delta,blinkDisplacement=blinkDelta,generic=true,avatarValid=animator.avatar.isValid};
  File.WriteAllText("preserved-unity.json",JsonUtility.ToJson(report,true));AssetDatabase.SaveAssets();Debug.Log("PRESERVED_UNITY_IMPORT_OK");
 }
}
