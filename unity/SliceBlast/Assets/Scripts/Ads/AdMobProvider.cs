// Google Mobile Ads, behind IAdProvider. This is the code that used to live inside
// AdsManager; the behaviour is unchanged, it simply no longer has the run cadence and the
// ads-removed policy tangled up with it.
//
// Gated behind SLICEBLAST_ADS_ENABLED: the GoogleMobileAds namespace only exists once the
// plugin has been imported into the project, and an unguarded reference would fail to compile
// the moment it landed. With the define off this compiles to a provider that is never ready
// and shows nothing, which AdsManager handles the same way it handles a network with no fill.
using System;

namespace SliceBlast.Ads
{
    public sealed class AdMobProvider : IAdProvider
    {
        public string Name => "AdMob";

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

        public bool IsInterstitialReady => _interstitialAd != null && _interstitialAd.CanShowAd();

        public bool IsRewardedReady => _rewardedAd != null && _rewardedAd.CanShowAd();

        public void Initialize()
        {
            GoogleMobileAds.Api.MobileAds.Initialize(_ =>
            {
                LoadInterstitial();
                LoadRewarded();
            });
        }

        public void ShowInterstitial()
        {
            if (IsInterstitialReady)
            {
                _interstitialAd.Show();
            }
        }

        public void ShowRewarded(Action onEarned, Action onUnavailable)
        {
            if (!IsRewardedReady)
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
        public bool IsInterstitialReady => false;

        public bool IsRewardedReady => false;

        public void Initialize()
        {
        }

        public void ShowInterstitial()
        {
        }

        public void ShowRewarded(Action onEarned, Action onUnavailable)
        {
            onUnavailable?.Invoke();
        }
#endif
    }
}
