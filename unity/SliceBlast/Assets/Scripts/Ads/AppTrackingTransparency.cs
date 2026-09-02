// Apple's own ATT permission prompt. AdMob's personalized ad serving links this device's
// activity across other AdMob publishers' apps — that is tracking under Apple's definition, and
// the app's Info.plist already carries NSUserTrackingUsageDescription (written by the Google
// Mobile Ads plugin's own build step). Apple's review rejects a build that ships that string but
// never actually shows the system prompt, so this has to be requested for real before any ad
// request is made.
//
// Gated behind SLICEBLAST_ATT_ENABLED until the "iOS 14 Advertising Support" package
// (com.unity.ads.ios-support) is added via Window → Package Manager → search by that name →
// Install. Without it, the Unity.Advertisement.IosSupport namespace doesn't exist and this
// would fail to compile — same reasoning as SLICEBLAST_ADS_ENABLED for the ads plugin itself.
namespace SliceBlast.Ads
{
    public static class AppTrackingTransparency
    {
        /// <summary>Call once, before the first ad request — see AdsManager.Initialize().</summary>
        public static void RequestIfNeeded()
        {
#if SLICEBLAST_ATT_ENABLED && UNITY_IOS && !UNITY_EDITOR
            var status = Unity.Advertisement.IosSupport.ATTrackingStatusBinding.GetAuthorizationTrackingStatus();

            if (status == Unity.Advertisement.IosSupport.ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                Unity.Advertisement.IosSupport.ATTrackingStatusBinding.RequestAuthorizationTracking();
            }
#endif
        }
    }
}
