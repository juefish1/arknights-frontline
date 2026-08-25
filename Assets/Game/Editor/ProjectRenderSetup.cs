using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ArknightsFrontline.Editor
{
    [InitializeOnLoad]
    public static class ProjectRenderSetup
    {
        private const string RendererAssetPath = "Assets/Game/Rendering/PrototypeRenderer.asset";
        private const string PipelineAssetPath = "Assets/Game/Rendering/PrototypeURP.asset";

        static ProjectRenderSetup()
        {
            EditorApplication.delayCall += Configure;
        }

        [MenuItem("Arknights Frontline/Configure Prototype Rendering")]
        public static void Configure()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Rendering"))
            {
                AssetDatabase.CreateFolder("Assets/Game", "Rendering");
            }

            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererAssetPath);
            }

            UniversalRenderPipelineAsset pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipelineAsset == null)
            {
                pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
            }

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            int activeQualityLevel = QualitySettings.GetQualityLevel();
            for (int qualityLevel = 0; qualityLevel < QualitySettings.names.Length; qualityLevel++)
            {
                QualitySettings.SetQualityLevel(qualityLevel, false);
                QualitySettings.renderPipeline = pipelineAsset;
            }

            QualitySettings.SetQualityLevel(activeQualityLevel, false);

            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();
        }
    }
}
