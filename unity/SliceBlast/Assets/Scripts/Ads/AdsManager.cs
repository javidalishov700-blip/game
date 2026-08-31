// Wraps the Google Mobile Ads Unity Plugin behind an interface the rest of the game never has
// to know the shape of: an interstitial that shows itself on a schedule, and a rewarded ad the
// player opts into.
//
// Gated behind the SLICEBLAST_ADS_ENABLED scripting define on purpose: the GoogleMobileAds
// namespace only exists once the Google Mobile Ads Unity Plugin has been imported by hand (see
// the note at the bottom of this file), and that is a real Unity Editor step nobody has done
// yet. Without the guard, this file would fail to compile the moment it landed — breaking the
// whole project, including rebuilds of the version already submitted to Apple, for a feature
// that was not ready. With the define left off, everything below compiles to inert stubs: ads
// are simply never shown, nothing else changes.
using System;
using UnityEngine;

namespace SliceBlast.Ads
{
    [DisallowMultipleComponent]
    public sealed class AdsManager : MonoBehaviour
    {
        // A miss between runs is an event, not a rhythm — the very first game over of a
        // session never carries one, and every one after that costs a fixed number of runs.
        private const int RunsBetweenInterstitials = 3;

        public static AdsManager Instance { get; private set; }

#if SLICEBLAST_ADS_ENABLED
        public bool IsRewardedReady => _rewardedAd != null && _rewardedAd.CanShowAd();
#else
        public bool IsRewardedReady => false;
#endif

        /// <summary>Creates the singleton the first time it is needed; safe to call repeatedly.</summary>
        public static AdsManager EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            GameObject host = new GameObject("AdsManager");
            Instance = host.AddComponent<AdsManager>();
            DontDestroyOnLoad(host);
            Instance.Initialize();
            return Instance;
        }

#if SLICEBLAST_ADS_ENABLED
        // Real ad unit IDs from the Slice & Blast AdMob app (iOS-only — this project never
        // ships Android, so that branch below is still Google's public test IDs).
#if UNITY_IOS
        private const string InterstitialAdUnitId = "ca-app-pub-4448830215845263/4186164698";
        private const string RewardedAdUnitId = "ca-app-pub-4448830215845263/3977341780";
#elif UNITY_ANDROID
        private const string InterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";
        private const string RewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
#else
        private const string InterstitialAdUnitId = "unused";
        private const string RewardedAdUnitId = "unused";
#endif

        private GoogleMobileAds.Api.InterstitialAd _interstitialAd;
        private GoogleMobileAds.Api.RewardedAd _rewardedAd;
        private int _runsSinceInterstitial;
        private bool _initialized;

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            GoogleMobileAds.Api.MobileAds.Initialize(_ =>
            {
                LoadInterstitial();
                LoadRewarded();
            });
        }

        /// <summary>Call once per completed run, after the run-over screen is shown.</summary>
        public void NotifyRunEnded()
        {
            _runsSinceInterstitial++;

            if (_runsSinceInterstitial < RunsBetweenInterstitials)
            {
                return;
            }

            if (_interstitialAd != null && _interstitialAd.CanShowAd())
            {
                _runsSinceInterstitial = 0;
                _interstitialAd.Show();
            }
        }

        /// <summary>
        /// Shows the rewarded ad if one is ready. onEarned fires only once the player actually
        /// finished watching; onUnavailable fires immediately if nothing was loaded, and also
        /// if the player closes the ad early without earning the reward.
        /// </summary>
        public void ShowRewarded(Action onEarned, Action onUnavailable)
        {
            if (_rewardedAd == null || !_rewardedAd.CanShowAd())
            {
                onUnavailable?.Invoke();
                return;
            }

            bool earned = false;

            _rewardedAd.Show(_ =>
            {
                earned = true;
                onEarned?.Invoke();
            });

            _rewardedAd.OnAdFullScreenContentClosed += HandleRewardedClosed;

            void HandleRewardedClosed()
            {
                _rewardedAd.OnAdFullScreenContentClosed -= HandleRewardedClosed;
                LoadRewarded();

                if (!earned)
                {
                    onUnavailable?.Invoke();
                }
            }
        }

        private void LoadInterstitial()
        {
            if (_interstitialAd != null)
            {
                _interstitialAd.Destroy();
                _interstitialAd = null;
            }

            GoogleMobileAds.Api.AdRequest request = new GoogleMobileAds.Api.AdRequest();

            GoogleMobileAds.Api.InterstitialAd.Load(InterstitialAdUnitId, request, (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    return;
                }

                _interstitialAd = ad;
                _interstitialAd.OnAdFullScreenContentClosed += LoadInterstitial;
                _interstitialAd.OnAdFullScreenContentFailed += _ => LoadInterstitial();
            });
        }

        private void LoadRewarded()
        {
            if (_rewardedAd != null)
            {
                _rewardedAd.Destroy();
                _rewardedAd = null;
            }

            GoogleMobileAds.Api.AdRequest request = new GoogleMobileAds.Api.AdRequest();

            GoogleMobileAds.Api.RewardedAd.Load(RewardedAdUnitId, request, (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    return;
                }

                _rewardedAd = ad;
            });
        }
#else
        private void Initialize()
        {
        }

        public void NotifyRunEnded()
        {
        }

        public void ShowRewarded(Action onEarned, Action onUnavailable)
        {
            onUnavailable?.Invoke();
        }
#endif
    }
}

// ---------------------------------------------------------------------------------------------
// Turning ads on:
//
// 1. [DONE] AdMob ad unit IDs. The interstitial/rewarded ad unit IDs are wired in above.
// 2. [DONE] Google Mobile Ads Unity Plugin imported (Assets/GoogleMobileAds).
// 3. [TODO — real Unity Editor step, one time] The plugin's own build step
//    (GoogleMobileAds.Editor.PListProcessor) writes GADApplicationIdentifier,
//    NSUserTrackingUsageDescription and SKAdNetworkItems into Info.plist itself — do NOT
//    duplicate that here. It reads the App ID and tracking description from its own settings
//    asset, which only the Unity menu can create correctly: Assets → Google Mobile Ads →
//    Settings… → iOS App ID: ca-app-pub-4448830215845263~6624625776 → User Tracking Usage
//    Description: "Slice Blast uses this to show ads that are more relevant to you. Ads still
//    show if you decline." → save. This writes Assets/GoogleMobileAds/Resources/
//    GoogleMobileAdsSettings.asset — commit and push it. Skipping this step means every iOS
//    build fails outright (PListProcessor throws when the App ID field is empty).
// 4. Unity → Project Settings → Player → iOS tab → Scripting Define Symbols → add
//    SLICEBLAST_ADS_ENABLED. Only once this define is set does any of the code above run
//    instead of compiling to a no-op.
//
// App Privacy answers and the Privacy Policy / Terms of Use updates this needs are already
// done — see docs/APP_STORE.md section 9.
// ---------------------------------------------------------------------------------------------
