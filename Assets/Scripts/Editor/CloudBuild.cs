using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DDZ.Editor
{
    /// <summary>
    /// 供 GitHub Actions (GameCI) 云端构建调用的入口。本机不编译，只在 CI 中运行。
    /// 用法：buildMethod = DDZ.Editor.CloudBuild.BuildAndroid
    /// </summary>
    public static class CloudBuild
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        /// <summary>云端构建 Android APK。</summary>
        public static void BuildAndroid()
        {
            ConfigurePlayerSettings();
            PrepareScene();
            Build(BuildTarget.Android, "build/Android", "DDZ.apk");
        }

        /// <summary>云端构建 Windows x64（备用目标）。</summary>
        public static void BuildWindows64()
        {
            ConfigurePlayerSettings();
            PrepareScene();
            Build(BuildTarget.StandaloneWindows64, "build/Windows64", "DDZ.exe");
        }

        private static void Build(BuildTarget target, string outputDir, string fileName)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new BuildFailedException("[CloudBuild] 没有启用场景可构建");

            string outputPath = Path.GetFullPath(Path.Combine(outputDir, fileName));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"[CloudBuild] 构建失败: {report.summary.result}, errors={report.summary.totalErrors}");
            }

            Debug.Log($"[CloudBuild] 构建成功 -> {outputPath} size={report.summary.totalSize}");
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "hdppppppp";
            PlayerSettings.productName = "DDZ";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.applicationIdentifier = "com.hdppppppp.ddz";
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersion.Android23;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersion.Android33;
            PlayerSettings.SetScriptingBackend(
                UnityEditor.Build.NamedBuildTarget.Android,
                ScriptingImplementation.Mono2x);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            Debug.Log("[CloudBuild] PlayerSettings 已配置");
        }

        /// <summary>
        /// 代码生成主场景：新建默认场景 + 挂引导组件，保存并加入构建列表。
        /// 避免在仓库里手写场景 YAML，保证云端构建始终有有效场景。
        /// </summary>
        private static void PrepareScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var root = new GameObject("GameRoot");
            root.AddComponent<Presentation.SceneBootstrapper>();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[CloudBuild] 场景已生成: {ScenePath}");
        }
    }
}
