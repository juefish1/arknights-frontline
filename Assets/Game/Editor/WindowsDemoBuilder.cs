using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ArknightsFrontline.Editor
{
    public static class WindowsDemoBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/PrototypeArena.unity";
        private const string OutputPath = "Builds/arknights-frontline-windows-x64-stage7-20261001/ArknightsFrontline.exe";

        [MenuItem("Arknights Frontline/Build Windows Demo")]
        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                throw new BuildFailedException(
                    "The active build target must be Windows x64. Restart Unity with -buildTarget Win64; this helper does not change project settings.");
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                throw new BuildFailedException("The Prototype Arena scene was not found at " + ScenePath + ".");
            }

            string absoluteOutputPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", OutputPath));
            string outputDirectory = Path.GetDirectoryName(absoluteOutputPath);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                throw new BuildFailedException("The Windows demo output path has no parent directory.");
            }

            Directory.CreateDirectory(outputDirectory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = absoluteOutputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report == null || report.summary.result != BuildResult.Succeeded)
            {
                string result = report == null ? "no BuildReport was returned" : report.summary.result.ToString();
                throw new BuildFailedException("Windows demo build failed (" + result + ").");
            }

            Debug.Log(
                "Windows demo build succeeded: " + absoluteOutputPath
                + " (" + report.summary.totalSize + " bytes).");
        }
    }
}
