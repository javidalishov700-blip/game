// Stamps the export-compliance answer into Info.plist so TestFlight processes
// each upload without asking. Compiled only when iOS is the active build target.
#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace SliceBlast.EditorTools
{
    public static class IosPostProcess
    {
        [PostProcessBuild(999)]
        public static void OnPostProcessBuild(BuildTarget target, string builtPath)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            string plistPath = Path.Combine(builtPath, "Info.plist");
            if (!File.Exists(plistPath))
            {
                return;
            }

            PlistDocument plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            // No custom crypto in the game — this is the standard exemption answer.
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
