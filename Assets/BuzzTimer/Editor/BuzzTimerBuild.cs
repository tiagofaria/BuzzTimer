using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuzzTimer.Editor
{
    public static class BuzzTimerBuild
    {
        [MenuItem("Buzz Timer/Build Windows")]
        public static void BuildWindows()
        {
            const string scenePath = "Assets/BuzzTimer/Scenes/BuzzTimer.unity";
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

            PlayerSettings.companyName = "Buzz";
            PlayerSettings.productName = "BuzzTimer";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;

            const string outputPath = "Builds/Windows/BuzzTimer.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath }, locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("A build do BuzzTimer falhou: " + report.summary.result);
            Debug.Log($"BuzzTimer criado em {Path.GetFullPath(outputPath)} ({report.summary.totalSize} bytes).");
        }
    }
}
