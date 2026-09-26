// Headless build entry points for CI (Codemagic) and the Unity menu.
//   Unity -batchmode -quit -nographics -projectPath unity/SliceBlast -buildTarget iOS \
//         -executeMethod SliceBlast.EditorTools.SliceBlastBuild.BuildIos
using System;
using System.IO;
using System.Reflection;
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

        // What an Xcode project needs before Codemagic can link AdMob into it: the SDK itself,
        // pulled in by CocoaPods, and Google's Objective-C bridge the C# plugin calls into.
        private const string AdsPod = "Google-Mobile-Ads-SDK";
        private const string AdsBridge = "unity-plugin-library";

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
            // "ios" beside the project from the menu; wherever the CI runner asks for it when
            // GameCI launches this method with -customBuildPath. Honouring that argument is
            // what lets CI call *this* entry point rather than Unity's default build — see
            // .github/workflows/ios-xcode.yml for why that matters.
            Run(BuildTarget.iOS, BuildTargetGroup.iOS, CommandLineArgument("-customBuildPath", "ios"));
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

            if (target == BuildTarget.iOS)
            {
                LoadPodDependencies();
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

            bool succeeded = summary.result == BuildResult.Succeeded;

            if (succeeded && target == BuildTarget.iOS)
            {
                string problem = FindAdsLinkProblem(outputPath);

                if (problem != null)
                {
                    succeeded = false;
                    string message = $"This Xcode project cannot be shipped: {problem}. Codemagic would "
                        + "fail to link it, or worse, produce an app whose ads never load.";
                    Debug.LogError("[SliceBlast] " + message);

                    if (!Application.isBatchMode)
                    {
                        EditorUtility.DisplayDialog("Slice & Blast", message, "OK");
                    }
                }
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(succeeded ? 0 : 1);
            }
        }

        /// <summary>
        /// Makes the External Dependency Manager read every *Dependencies.xml now, before the
        /// build. It normally does that on its first editor update, which a batch-mode build
        /// launched with -executeMethod reaches only after the build is over — and with
        /// CocoaPods integration set to None its own pre-build refresh is skipped too. The
        /// result was a Podfile with no pods in it: a project that builds, then cannot link
        /// AdMob. The method is private, hence reflection; if a future EDM4U renames it, the
        /// Podfile check after the build says so instead of letting that pass silently.
        /// </summary>
        private static void LoadPodDependencies()
        {
            Type resolver = null;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                resolver = assembly.GetType("Google.IOSResolver", false);

                if (resolver != null)
                {
                    break;
                }
            }

            MethodInfo refresh = resolver?.GetMethod(
                "RefreshXmlDependencies", BindingFlags.NonPublic | BindingFlags.Static);

            if (refresh == null)
            {
                Debug.LogWarning("[SliceBlast] Could not ask the External Dependency Manager to load its pods.");
                return;
            }

            refresh.Invoke(null, null);
        }

        /// <summary>Why the generated project could not link AdMob, or null if it can.</summary>
        private static string FindAdsLinkProblem(string xcodeProject)
        {
            string podfile = Path.Combine(xcodeProject, "Podfile");

            if (!File.Exists(podfile) || !File.ReadAllText(podfile).Contains(AdsPod))
            {
                return $"its Podfile does not list {AdsPod}";
            }

            string pbxproj = Path.Combine(xcodeProject, "Unity-iPhone.xcodeproj", "project.pbxproj");

            if (!File.Exists(pbxproj) || !File.ReadAllText(pbxproj).Contains(AdsBridge))
            {
                return $"it does not contain {AdsBridge}, the Google Mobile Ads bridge from Assets/Plugins/iOS";
            }

            return null;
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
            // 1.2: the fault line, the workshop, coins, missions and themes. Every build made
            // from this point on carries that content, so it is stamped 1.2 whatever became of
            // 1.1 — if 1.1 was approved, a new 1.1.0 upload is rejected outright (90062); if it
            // is still in review, a 1.2.0 build goes to TestFlight on its own train and does
            // not touch that review.
            PlayerSettings.bundleVersion = EnvOr("APP_VERSION", "1.2.0");

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

        /// <summary>The value following <paramref name="flag"/> on Unity's command line, or the fallback.</summary>
        private static string CommandLineArgument(string flag, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == flag && !string.IsNullOrEmpty(args[i + 1]))
                {
                    return args[i + 1];
                }
            }

            return fallback;
        }
    }
}
