// Capturing the App Store screenshots is the one submission step that still needs a human
// eye, so it may as well not also need a Mac, a device or an image editor: set the Game view
// to the size Apple asks for, play, and press the shortcut.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SliceBlast.EditorTools
{
    public static class StoreScreenshots
    {
        // App Store Connect's 6.9-inch iPhone slot, which is the only iPhone size still
        // required. Either of these is accepted; the first is what the Game view preset in
        // docs/APP_STORE.md sets up.
        private static readonly Vector2Int[] AcceptedSizes =
        {
            new Vector2Int(1290, 2796),
            new Vector2Int(1320, 2868)
        };

        private const string OutputFolder = "Screenshots";

        [MenuItem("Slice & Blast/Capture App Store Screenshot %#s")]
        public static void Capture()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Slice & Blast",
                    "Enter Play mode first — the screenshot is taken from the Game view.",
                    "OK");
                return;
            }

            Vector2 view = Handles.GetMainGameViewSize();
            int width = Mathf.RoundToInt(view.x);
            int height = Mathf.RoundToInt(view.y);

            if (!IsAccepted(width, height))
            {
                // Not fatal: capture anyway, because a wrongly sized shot of the right moment
                // is still worth having. But say so, because App Store Connect will not.
                Debug.LogWarning(
                    "[SliceBlast] Game view is " + width + "x" + height +
                    ". App Store Connect only accepts 1290x2796 or 1320x2868 for iPhone. " +
                    "Game view → resolution dropdown → + → Fixed Resolution.");
            }

            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputFolder);
            Directory.CreateDirectory(folder);

            string file = Path.Combine(
                folder,
                "sliceblast-" + width + "x" + height + "-" + DateTime.Now.ToString("HHmmss") + ".png");

            ScreenCapture.CaptureScreenshot(file);
            Debug.Log("[SliceBlast] Screenshot queued: " + file);
        }

        private static bool IsAccepted(int width, int height)
        {
            for (int i = 0; i < AcceptedSizes.Length; i++)
            {
                if (AcceptedSizes[i].x == width && AcceptedSizes[i].y == height)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
