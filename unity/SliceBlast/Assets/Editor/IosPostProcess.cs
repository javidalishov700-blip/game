// Runs on the generated Xcode project — the one place in the pipeline that is guaranteed
// to execute for every iOS build, cloud builds included. Stamps the export-compliance
// answer and writes the app icon straight into the asset catalog.
#if UNITY_IOS
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace SliceBlast.EditorTools
{
    public static class IosPostProcess
    {
        private const string AppIconName = "AppIcon";

        // The "universal" entry is Xcode 14's single-size icon feature, which supplies the
        // home-screen icon once ASSETCATALOG_COMPILER_INCLUDE_ALL_APPICON_ASSETS is on. The
        // "ios-marketing" entry is listed explicitly too, rather than left for that feature
        // to derive, because it is specifically the one App Store Connect reads for its own
        // listing — the slot that was showing empty.
        private const string ContentsJson = @"{
  ""images"" : [
    {
      ""filename"" : ""Icon-1024.png"",
      ""idiom"" : ""universal"",
      ""platform"" : ""ios"",
      ""size"" : ""1024x1024""
    },
    {
      ""filename"" : ""Icon-1024.png"",
      ""idiom"" : ""ios-marketing"",
      ""scale"" : ""1x"",
      ""size"" : ""1024x1024""
    }
  ],
  ""info"" : {
    ""author"" : ""xcode"",
    ""version"" : 1
  }
}
";

        [PostProcessBuild(999)]
        public static void OnPostProcessBuild(BuildTarget target, string builtPath)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            StampInfoPlist(builtPath);
            WriteAppIcon(builtPath);
        }

        private static void StampInfoPlist(string builtPath)
        {
            try
            {
                string plistPath = Path.Combine(builtPath, "Info.plist");

                if (!File.Exists(plistPath))
                {
                    return;
                }

                PlistDocument plist = new PlistDocument();
                plist.ReadFromFile(plistPath);

                // No custom crypto in the game — this is the standard exemption answer.
                plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

                // Names the asset catalog icon set. Its absence is a processing rejection
                // ("Missing Info.plist value CFBundleIconName") even when the icon is there.
                plist.root.SetString("CFBundleIconName", AppIconName);

                // A portrait-only app that also claims iPad support is rejected unless it
                // opts out of multitasking, which requires all four orientations.
                plist.root.SetBoolean("UIRequiresFullScreen", true);

                // App Store Connect rejects a repeated build number, and cloud builds do not
                // increment one. Minutes since 2024 is monotonic, compact and good for decades.
                string build = ((int)(DateTime.UtcNow - new DateTime(2024, 1, 1)).TotalMinutes).ToString();
                plist.root.SetString("CFBundleVersion", build);

                plist.WriteToFile(plistPath);
                Debug.Log("[SliceBlast] CFBundleVersion set to " + build);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SliceBlast] Info.plist stamp skipped: {exception.Message}");
            }
        }

        /// <summary>
        /// App Store Connect rejects uploads without a 1024 icon (error 91111). Writing it
        /// here rather than through PlayerSettings keeps it independent of CI configuration.
        /// </summary>
        private static void WriteAppIcon(string builtPath)
        {
            try
            {
                string appIconSet = Path.Combine(builtPath, "Unity-iPhone/Images.xcassets/" + AppIconName + ".appiconset");
                Directory.CreateDirectory(appIconSet);

                foreach (string existing in Directory.GetFiles(appIconSet))
                {
                    File.Delete(existing);
                }

                Texture2D icon = SliceBlastAssets.DrawIcon();
                File.WriteAllBytes(Path.Combine(appIconSet, "Icon-1024.png"), icon.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(icon);

                File.WriteAllText(Path.Combine(appIconSet, "Contents.json"), ContentsJson);

                string pbxPath = PBXProject.GetPBXProjectPath(builtPath);
                PBXProject project = new PBXProject();
                project.ReadFromFile(pbxPath);

                string targetGuid = project.GetUnityMainTargetGuid();
                project.SetBuildProperty(targetGuid, "ASSETCATALOG_COMPILER_APPICON_NAME", AppIconName);

                // The single "universal" 1024 entry above is Xcode 14's single-size icon
                // feature — it only expands into the full icon family (including the
                // App Store marketing slot App Store Connect reads) when this is on. Without
                // it the binary still has a home-screen icon, which is why TestFlight looks
                // fine while the App Store Connect listing shows no icon at all.
                project.SetBuildProperty(targetGuid, "ASSETCATALOG_COMPILER_INCLUDE_ALL_APPICON_ASSETS", "YES");
                project.WriteToFile(pbxPath);

                Debug.Log("[SliceBlast] App icon written to " + appIconSet);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SliceBlast] App icon injection skipped: {exception.Message}");
            }
        }
    }
}
#endif
