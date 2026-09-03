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
        private const string DefaultBundleId = "com.javidalishov.sliceblast";
        private const string ProductName = "Slice Blast";

        [MenuItem("Slice & Blast/Create Playable Scene")]
        public static void CreatePlayableScene()
        {
            SliceBlastAssets.EnsureMaterials();
            EnsureScene(true);
        }

        /// <summary>
        /// Pre-export hook for Unity Build Automation, which drives the build itself:
        /// generate the scene, put it in the build list and apply the player settings.
        /// </summary>
        public static void PrepareForCloudBuild()
        {
            SliceBlastAssets.EnsureMaterials();
            SliceBlastAssets.EnsureIcon(false);

            string scenePath = EnsureScene(false);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            ApplyPlayerSettings(target, group);

            AssetDatabase.SaveAssets();
            Debug.Log($"[SliceBlast] Cloud build prepared: {scenePath} for {target}.");
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
            SliceBlastAssets.EnsureMaterials();
            SliceBlastAssets.EnsureIcon(false);

            string scenePath = EnsureScene(false);
            ApplyPlayerSettings(target, group);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                // Switching triggers a recompile, and the post-process hooks that write the
                // app icon and stamp Info.plist live behind #if UNITY_IOS — they simply do
                // not exist in the assembly running right now. Building anyway produces an
                // Xcode project with no icon, which App Store Connect rejects with 91111.
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);

                Debug.LogWarning(
                    $"[SliceBlast] Active platform switched to {target}. Scripts are recompiling — "
                    + "run the build again once that finishes.");

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(2);
                }

                return;
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

        /// <summary>
        /// Everything a player needs to be correct, applied from whichever build path is
        /// driving: our own menu items, Unity Build Automation's pre-export hook, or a
        /// third-party CI that calls the default build pipeline (GameCI and friends). The
        /// repository carries no ProjectSettings.asset, so nothing here can be assumed.
        /// </summary>
        public static void PrepareProject()
        {
            SliceBlastAssets.EnsureMaterials();
            SliceBlastAssets.EnsureIcon(false);

            string scenePath = EnsureScene(false);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            ApplyPlayerSettings(target, BuildPipeline.GetBuildTargetGroup(target));
        }

        private static void ApplyPlayerSettings(BuildTarget target, BuildTargetGroup group)
        {
            string bundleId = EnvOr("BUNDLE_ID", DefaultBundleId);
            NamedBuildTarget named = NamedBuildTarget.FromBuildTargetGroup(group);

            PlayerSettings.companyName = EnvOr("COMPANY_NAME", "Slice Blast Games");
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(named, bundleId);
            // 1.0 already cleared App Store review — Apple rejects any new build still
            // stamped 1.0.0 (code 90062/90186), so every build from here on is 1.1.0.
            PlayerSettings.bundleVersion = EnvOr("APP_VERSION", "1.1.0");

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.IL2CPP);
            QualitySettings.vSyncCount = 0;

            // The Google Mobile Ads Unity Plugin is in the repo and its own settings asset is
            // configured (Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset) — no
            // manual per-machine "Scripting Define Symbols" step needed any more.
            AddScriptingDefine(named, "SLICEBLAST_ADS_ENABLED");

            // com.unity.ads.ios-support (iOS 14 Advertising Support) is in the project —
            // AppTrackingTransparency.cs can now actually show the system permission prompt
            // instead of compiling to a no-op.
            AddScriptingDefine(named, "SLICEBLAST_ATT_ENABLED");

            ApplySplashSettings();

            if (target != BuildTarget.iOS)
            {
                return;
            }

            PlayerSettings.iOS.targetOSVersionString = EnvOr("IOS_MIN_VERSION", "13.0");
            // Portrait-only phone game. Claiming iPad support would oblige it to handle every
            // orientation for multitasking, which App Store Connect checks on the way in.
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.iOS.buildNumber = EnvOr("PROJECT_BUILD_NUMBER", "1");

            string teamId = Environment.GetEnvironmentVariable("APPLE_TEAM_ID");
            if (!string.IsNullOrEmpty(teamId))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = teamId;
            }
        }

        /// <summary>Adds a scripting define if it is not already present — never clobbers others.</summary>
        private static void AddScriptingDefine(NamedBuildTarget named, string define)
        {
            string existing = PlayerSettings.GetScriptingDefineSymbols(named);
            string[] symbols = existing.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            if (Array.IndexOf(symbols, define) >= 0)
            {
                return;
            }

            string updated = existing.Length > 0 ? existing + ";" + define : define;
            PlayerSettings.SetScriptingDefineSymbols(named, updated);
        }

        /// <summary>
        /// The game opens on its own title screen, not on a Unity logo. ProjectSettings.asset
        /// is not in the repository, so the build machine starts from Unity's defaults and
        /// this has to be applied from code on every build path.
        /// </summary>
        public static void ApplySplashSettings()
        {
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.03f, 0.03f, 0.06f);
        }

        private static string EnvOr(string key, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
