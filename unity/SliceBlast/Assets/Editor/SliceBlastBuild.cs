// Headless build entry points for CI (Codemagic) and the Unity menu.
//   Unity -batchmode -quit -nographics -projectPath unity/SliceBlast -buildTarget iOS \
//         -executeMethod SliceBlast.EditorTools.SliceBlastBuild.BuildIos
using System;
using System.IO;
using SliceBlast.Bootstrap;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SliceBlast.EditorTools
{
    public static class SliceBlastBuild
    {
        private const string SceneFolder = "Assets/Scenes";
        private const string ScenePath = SceneFolder + "/Main.unity";
        private const string DefaultBundleId = "com.sliceblast.game";
        private const string ProductName = "Slice & Blast";

        [MenuItem("Slice & Blast/Create Playable Scene")]
        public static void CreatePlayableScene()
        {
            EnsureScene(true);
        }

        [MenuItem("Slice & Blast/Build iOS Xcode Project")]
        public static void BuildIos()
        {
            Run(BuildTarget.iOS, BuildTargetGroup.iOS, "ios");
        }

        [MenuItem("Slice & Blast/Build Android APK")]
        public static void BuildAndroid()
        {
            Run(BuildTarget.Android, BuildTargetGroup.Android, "build/android/sliceblast.apk");
        }

        private static void Run(BuildTarget target, BuildTargetGroup group, string outputPath)
        {
            string scenePath = EnsureScene(false);
            ApplyPlayerSettings(target, group);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            }

            // Unity appends to an existing Xcode project; a clean folder avoids stale signing state.
            if (target == BuildTarget.iOS && Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }

            string directory = target == BuildTarget.iOS ? outputPath : Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = outputPath,
                target = target,
                targetGroup = group,
                options = BuildOptions.None
            });

            BuildSummary summary = report.summary;
            Debug.Log($"[SliceBlast] Build {summary.result} — {summary.totalSize} bytes in {summary.totalTime}.");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            }
        }

        private static string EnsureScene(bool force)
        {
            if (!force && File.Exists(ScenePath))
            {
                return ScenePath;
            }

            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject root = new GameObject("SliceBlast");
            root.AddComponent<SliceBlastBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            return ScenePath;
        }

        private static void ApplyPlayerSettings(BuildTarget target, BuildTargetGroup group)
        {
            string bundleId = EnvOr("BUNDLE_ID", DefaultBundleId);
            NamedBuildTarget named = NamedBuildTarget.FromBuildTargetGroup(group);

            PlayerSettings.companyName = EnvOr("COMPANY_NAME", "Slice Blast Games");
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(named, bundleId);
            PlayerSettings.bundleVersion = EnvOr("APP_VERSION", "1.0.0");

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.IL2CPP);
            QualitySettings.vSyncCount = 0;

            if (target != BuildTarget.iOS)
            {
                return;
            }

            PlayerSettings.iOS.targetOSVersionString = EnvOr("IOS_MIN_VERSION", "13.0");
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.iOS.buildNumber = EnvOr("PROJECT_BUILD_NUMBER", "1");

            string teamId = Environment.GetEnvironmentVariable("APPLE_TEAM_ID");
            if (!string.IsNullOrEmpty(teamId))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = teamId;
            }
        }

        private static string EnvOr(string key, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
