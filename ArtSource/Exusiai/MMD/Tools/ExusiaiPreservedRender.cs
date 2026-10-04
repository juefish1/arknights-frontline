using System.IO;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class ExusiaiPreservedRender {
 static int frames;
 public static void Start(){
  
  EditorSceneManager.OpenScene("Assets/Game/Characters/Exusiai/MMD/Scenes/Exusiai_MMD_Preview.unity");
  var data=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(data,"Assets/PreviewRenderer.asset");var pipeline=UniversalRenderPipelineAsset.Create(data);AssetDatabase.CreateAsset(pipeline,"Assets/PreviewPipeline.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;AssetDatabase.SaveAssets();EditorApplication.update+=Tick;
 }
 static void Tick(){if(++frames<40)return;EditorApplication.update-=Tick;try{
  var cam=Object.FindFirstObjectByType<Camera>();cam.transform.position=new Vector3(1.8f,1.3f,3.6f);cam.transform.LookAt(new Vector3(0,.87f,0));cam.fieldOfView=30;cam.aspect=900f/1100f;
  foreach(var shot in new[]{"three","side","motion"}){
  cam.transform.position=shot=="side"?new Vector3(3.9f,1.1f,0):new Vector3(1.8f,1.3f,3.6f);cam.transform.LookAt(new Vector3(0,.87f,0));
  var animator=Object.FindFirstObjectByType<Animator>();
  var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Game/Characters/Exusiai/MMD/PreservedRig/Animations/Exusiai_OriginalRigCheck.anim");var graph=PlayableGraph.Create("Preview");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var output=AnimationPlayableOutput.Create(graph,"Preview",animator);var playable=AnimationClipPlayable.Create(graph,clip);output.SetSourcePlayable(playable);graph.Play();playable.SetTime(shot=="motion"?1:0);graph.Evaluate(0);
  var skin=animator.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();skin.BakeMesh(baked);var display=new GameObject("CapturedSkin");display.transform.SetParent(skin.transform,false);display.AddComponent<MeshFilter>().sharedMesh=baked;display.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;
  var rt=new RenderTexture(900,1100,24,RenderTextureFormat.ARGB32);rt.Create();var prepare=typeof(RenderPipelineManager).GetMethod("TryPrepareRenderPipeline",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);if(prepare==null)throw new System.Exception("Prepare method unavailable");prepare.Invoke(null,new object[]{GraphicsSettings.defaultRenderPipeline});Debug.Log("Pipeline asset "+GraphicsSettings.defaultRenderPipeline+" current "+RenderPipelineManager.currentPipeline);var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};RenderPipeline.SubmitRenderRequest(cam,request);
  RenderTexture.active=rt;var tex=new Texture2D(900,1100,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,900,1100),0,0);tex.Apply();File.WriteAllBytes("preserved-"+shot+".png",tex.EncodeToPNG());RenderTexture.active=null;rt.Release();Object.DestroyImmediate(display);Object.DestroyImmediate(baked);skin.enabled=true;graph.Destroy();}EditorApplication.Exit(0);
 }catch(System.Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
