// Belt and braces for CI: whenever the editor loads this project — locally or on a build
// machine — make sure the generated materials exist and the scene is in the build list.
// The build never depends on this having run, it just makes the result better when it does.
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SliceBlast.EditorTools
{
    /// <summary>
    /// Runs for every player build, including cloud builds: guarantees the Resources
    /// materials exist so the shaders they reference survive build-time stripping, and
    /// that the Unity splash is off. This is the only hook the cloud build is guaranteed
    /// to call — the pre-export method only runs when the target is configured for it.
    /// </summary>
    public sealed class SliceBlastBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                // Runs on every build path, including CI services that call the default
                // pipeline without going through our menu items.
                SliceBlastBuild.PrepareProject();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[SliceBlast] Build pre-pass skipped: {exception.Message}");
            }
        }
    }

    [InitializeOnLoad]
    public static class SliceBlastAutoSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        static SliceBlastAutoSetup()
        {
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            try
            {
                SliceBlastAssets.EnsureMaterials();
                SliceBlastBuild.ApplySplashSettings();
                EnsureSceneRegistered();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[SliceBlast] Auto setup skipped: {exception.Message}");
            }
        }

        private static void EnsureSceneRegistered()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == ScenePath && scenes[i].enabled)
                {
                    return;
                }
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[SliceBlast] Registered " + ScenePath + " in the build settings.");
        }
    }
}
