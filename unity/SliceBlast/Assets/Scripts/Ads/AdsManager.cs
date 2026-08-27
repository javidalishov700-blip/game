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
        // Google's own published sample IDs — they always serve a test ad and never earn or
        // cost anything. Replace with the real ad unit IDs from your AdMob console before
        // shipping a build that is meant to earn revenue; shipping with these is safe, it
        // just never pays out.
#if UNITY_IOS
        private const string InterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";
        private const string RewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";
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
// Turning ads on — three real steps, none of them possible from source code alone:
//
// 1. Download the Google Mobile Ads Unity Plugin (.unitypackage) from the releases page of
//    https://github.com/googleads/googleads-mobile-unity and import it: Unity → Assets →
//    Import Package → Custom Package… This brings in the GoogleMobileAds namespace and the
//    External Dependency Manager (EDM4U) that resolves the native iOS/Android SDKs at build
//    time — it writes a Podfile into the generated Xcode project, which Codemagic's
//    xcode-to-testflight workflow now runs `pod install` for automatically.
// 2. Create an AdMob account at admob.google.com, add the app, and take its App ID and the
//    two ad unit IDs (interstitial, rewarded). Put the App ID in SliceBlastBuild's
//    GadApplicationId field (or directly where IosPostProcess writes GADApplicationIdentifier)
//    and swap the test ad unit IDs above for the real ones.
// 3. Unity → Project Settings → Player → Scripting Define Symbols → add
//    SLICEBLAST_ADS_ENABLED (for the iOS, and separately Android, tab). Only once this define
//    is set does any of the code above run instead of compiling to a no-op.
//
// Do this on a version *after* whatever is currently submitted to Apple — it changes the App
// Privacy answer from "No" to "Yes, this app collects data" (advertising identifiers), which
// needs its own App Store Connect update and cannot be folded into a build already in review.
// ---------------------------------------------------------------------------------------------
