using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;
public static class ExusiaiMmdImport {
 [Serializable] public class Entry {public string name,label,texture;}
 [Serializable] public class Bone {public string humanName,boneName;}
 [Serializable] public class Manifest {public Entry[] materials;public Bone[] humanBones;}
 const string Root="Assets/Game/Characters/Exusiai/MMD";
 public static void Build(){try{Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);File.WriteAllText("mmd-validation-error.txt",e.ToString());EditorApplication.Exit(1);}}
 static void Run(){
 var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
 var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("mmd-manifest.json"));
 var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new Exception("URP missing");
 var mats=new Dictionary<string,Material>();
 foreach(var e in manifest.materials){
  var tp=Root+"/Textures/"+e.texture;var ti=(TextureImporter)AssetImporter.GetAtPath(tp);ti.alphaIsTransparency=true;ti.sRGBTexture=true;ti.maxTextureSize=2048;ti.SaveAndReimport();
  var mp=Root+"/Materials/"+e.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(mp);if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,mp);}
  m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(tp));m.SetColor("_BaseColor",Color.white);m.SetFloat("_Smoothness",.18f);m.SetFloat("_Metallic",0);m.SetFloat("_Cull",0);m.SetFloat("_AlphaClip",1);m.SetFloat("_Cutoff",.3f);m.EnableKeyword("_ALPHATEST_ON");m.renderQueue=2450;m.SetOverrideTag("RenderType","TransparentCutout");EditorUtility.SetDirty(m);mats[e.name]=m;
 }
 string fbx=Root+"/Models/Exusiai_MMD.fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(fbx);
 importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=true;importer.importBlendShapes=true;importer.isReadable=true;importer.importCameras=false;importer.importLights=false;importer.globalScale=1;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.animationCompression=ModelImporterAnimationCompression.Off;
 var hd=importer.humanDescription;hd.human=manifest.humanBones.Select(b=>new HumanBone{humanName=b.humanName,boneName=b.boneName,limit=new HumanLimit{useDefaultValues=true}}).ToArray();hd.upperArmTwist=.5f;hd.lowerArmTwist=.5f;hd.upperLegTwist=.5f;hd.lowerLegTwist=.5f;hd.armStretch=.05f;hd.legStretch=.05f;hd.feetSpacing=0;hd.hasTranslationDoF=false;importer.humanDescription=hd;
 foreach(var kv in mats)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),kv.Key),kv.Value);importer.SaveAndReimport();
 var model=AssetDatabase.LoadAssetAtPath<GameObject>(fbx);var go=(GameObject)PrefabUtility.InstantiatePrefab(model);go.name="Exusiai_MMD_Visual";
 var animator=go.GetComponent<Animator>();if(!animator)throw new Exception("Animator missing");if(!animator.avatar||!animator.avatar.isValid||!animator.avatar.isHuman)throw new Exception("Humanoid avatar invalid");
 var skins=go.GetComponentsInChildren<SkinnedMeshRenderer>();if(skins.Length==0)throw new Exception("No skinned mesh");
 foreach(var r in skins){r.updateWhenOffscreen=true;if(r.sharedMaterials.Any(m=>!m||m.shader!=shader))throw new Exception("Material remap failed");if(r.bones.Any(b=>!b))throw new Exception("Missing bone reference");}
 var baked=new Mesh();skins[0].BakeMesh(baked);var vertices=baked.vertices.Select(v=>skins[0].transform.TransformPoint(v)).ToArray();float ymin=vertices.Min(v=>v.y),ymax=vertices.Max(v=>v.y);if(ymax-ymin<1.5||ymax-ymin>1.9)throw new Exception("Incorrect scale "+(ymax-ymin));
 go.transform.position=Vector3.down*ymin;
 var faceZ=skins[0].sharedMesh.GetTriangles(5).Select(i=>skins[0].transform.TransformPoint(baked.vertices[i]).z).Average();var headZ=animator.GetBoneTransform(HumanBodyBones.Head).position.z;if(faceZ<headZ)go.transform.rotation=Quaternion.Euler(0,180,0);
 var clips=AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();if(clips.Length==0)throw new Exception("No animation imported");
 var clip=UnityEngine.Object.Instantiate(clips[0]);clip.name="Exusiai_RigCheck";var clipPath=Root+"/Animations/Exusiai_RigCheck.anim";AssetDatabase.DeleteAsset(clipPath);var clipSettings=AnimationUtility.GetAnimationClipSettings(clip);clipSettings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,clipSettings);AssetDatabase.CreateAsset(clip,clipPath);
 var controllerPath=Root+"/Animations/Exusiai_RigCheck.controller";AssetDatabase.DeleteAsset(controllerPath);var controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);var state=controller.layers[0].stateMachine.AddState("RigCheck");state.motion=clip;animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
 animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
 var graph=PlayableGraph.Create("ExusiaiValidation");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var output=AnimationPlayableOutput.Create(graph,"Animation",animator);var playable=AnimationClipPlayable.Create(graph,clip);output.SetSourcePlayable(playable);graph.Play();playable.SetTime(0);graph.Evaluate(0);skins[0].BakeMesh(baked);var before=baked.vertices;
 Debug.Log("Clip "+clip.length+" human "+clip.humanMotion+" curves "+AnimationUtility.GetCurveBindings(clip).Length);
 playable.SetTime(1);graph.Evaluate(0);skins[0].BakeMesh(baked);var after=baked.vertices;float delta=Enumerable.Range(0,before.Length).Max(i=>(after[i]-before[i]).magnitude);if(delta<.005f)throw new Exception("Animation did not deform mesh: "+delta);
 int blink=Enumerable.Range(0,skins[0].sharedMesh.blendShapeCount).FirstOrDefault(i=>skins[0].sharedMesh.GetBlendShapeName(i).Contains("まばたき"));skins[0].SetBlendShapeWeight(blink,100);skins[0].BakeMesh(baked);var blinkVertices=baked.vertices;float bd=Enumerable.Range(0,after.Length).Max(i=>(blinkVertices[i]-after[i]).magnitude);if(bd<.0001)throw new Exception("Blink did not deform mesh");skins[0].SetBlendShapeWeight(blink,0);
 playable.SetTime(0);graph.Evaluate(0);graph.Destroy();
 PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/Exusiai_MMD_Visual.prefab");
 // Save a standalone inspection scene, ready for Play mode.
 
 var camera=new GameObject("Preview Camera").AddComponent<Camera>();camera.transform.position=new Vector3(2,1.4f,3.6f);camera.transform.LookAt(new Vector3(0,.9f,0));camera.backgroundColor=new Color(.16f,.18f,.23f);camera.clearFlags=CameraClearFlags.SolidColor;
 var light=new GameObject("Key Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(35,150,0);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
 EditorSceneManager.SaveScene(scene,Root+"/Scenes/Exusiai_MMD_Preview.unity");AssetDatabase.SaveAssets();
 string report="IMPORT_OK\nUnity: "+Application.unityVersion+"\nHumanoid avatar: valid / human\nSkinned renderers: "+skins.Length+"\nMaterials: "+mats.Count+"\nBlendshapes: "+skins.Sum(s=>s.sharedMesh.blendShapeCount)+"\nGeometry height including halo: "+(ymax-ymin)+" m\nGround offset: "+ymin+" m\nAnimation: "+clip.name+" (generated test only), "+clip.length+" s\nAnimated mesh max displacement: "+delta+" m\nBlink max displacement: "+bd+" m\n";File.WriteAllText("mmd-validation.txt",report);Debug.Log(report);
 }
}
