// Belt and braces for CI: whenever the editor loads this project — locally or on a build
// machine — make sure the generated materials exist and the scene is in the build list.
// The build never depends on this having run, it just makes the result better when it does.
using UnityEditor;
using UnityEngine;

namespace SliceBlast.EditorTools
{
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
