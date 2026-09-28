// Google Mobile Ads, behind IAdProvider: Google's consent flow (UMP) first, then the SDK, then
// one interstitial and one rewarded ad kept loaded — retried with backoff when a load fails.
// The run cadence and the ads-removed policy live in AdsManager, not here.
//
// Gated behind SLICEBLAST_ADS_ENABLED: the GoogleMobileAds namespace only exists once the
// plugin has been imported into the project, and an unguarded reference would fail to compile
// the moment it landed. With the define off this compiles to a provider that is never ready
// and shows nothing, which AdsManager handles the same way it handles a network with no fill.
using System;
using System.Threading.Tasks;

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

        // Google's public demo units for iOS. They always fill, show a "Test Ad" label and
        // pay nothing — used only while AdsManager.TestAds is switched on from the hidden
        // developer toggle, so the continue offer and interstitials can be tried out in
        // TestFlight before the AdMob account serves real ads.
        private const string TestInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";
        private const string TestRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";

        private static string InterstitialUnit => AdsManager.TestAds ? TestInterstitialAdUnitId : InterstitialAdUnitId;
        private static string RewardedUnit => AdsManager.TestAds ? TestRewardedAdUnitId : RewardedAdUnitId;

        // A failed load is tried again after 2, 4, 8 … seconds, levelling off at 64. Without
        // this a single miss — no fill in a new account's first hours, a dropped connection —
        // left that format empty for the rest of the session.
        private const int MaxBackoffExponent = 6;

        private GoogleMobileAds.Api.InterstitialAd _interstitialAd;
        private GoogleMobileAds.Api.RewardedAd _rewardedAd;
        private int _interstitialFailures;
        private int _rewardedFailures;
        private bool _started;

        private string _consentStatus = "consent: waiting for ATT";
        private string _sdkStatus = "sdk: not started";
        private string _interstitialStatus = "interstitial: -";
        private string _rewardedStatus = "rewarded: -";

        public string DebugStatus =>
            _consentStatus + "\n" + _sdkStatus + "\n" + _interstitialStatus + "\n" + _rewardedStatus
            + (IsRewardedReady ? "\nREWARDED READY" : string.Empty);

        public bool IsInterstitialReady => _interstitialAd != null && _interstitialAd.CanShowAd();

        public bool IsRewardedReady => _rewardedAd != null && _rewardedAd.CanShowAd();

        public bool PrivacyOptionsRequired =>
            GoogleMobileAds.Ump.Api.ConsentInformation.PrivacyOptionsRequirementStatus
            == GoogleMobileAds.Ump.Api.PrivacyOptionsRequirementStatus.Required;

        public void Initialize()
        {
            // Every plugin callback below is handed to AdsManager.Post, which runs it on the main
            // thread on the next frame. The plugin's own RaiseAdEventsOnUnityMainThread must NOT
            // be used here: it queues callbacks on an executor the plugin only creates inside
            // MobileAds.Initialize — and Initialize waits for the consent answer, which was
            // itself sitting in that queue. Ads never started, in any build, because of it.
            _consentStatus = "consent: checking";

            // Google's consent flow (UMP) comes first. For players in the EEA, the UK and
            // Switzerland it shows the GDPR message configured under AdMob → Privacy &
            // messaging; everywhere else it answers "not required" straight away and ads start
            // as they always did. Update runs every launch so a changed decision is picked up.
            GoogleMobileAds.Ump.Api.ConsentInformation.Update(
                new GoogleMobileAds.Ump.Api.ConsentRequestParameters(),
                updateError => AdsManager.Post(() =>
                {
                    if (updateError != null)
                    {
                        _consentStatus = "consent: update failed " + updateError.Message;
                        // Offline, most likely. Whatever was consented to last time still
                        // stands, and CanRequestAds() already reflects it.
                        StartIfConsented();
                        return;
                    }

                    GoogleMobileAds.Ump.Api.ConsentForm.LoadAndShowConsentFormIfRequired(formError => AdsManager.Post(() =>
                    {
                        _consentStatus = formError != null ? "consent: form error " + formError.Message : "consent: ok";
                        StartIfConsented();
                    }));
                }));
        }

        public void ShowPrivacyOptions()
        {
            // The player can grant consent here that they refused at launch, so ads may be
            // allowed to start only now.
            GoogleMobileAds.Ump.Api.ConsentForm.ShowPrivacyOptionsForm(_ => AdsManager.Post(StartIfConsented));
        }

        /// <summary>
        /// Ads are requested only once Google's consent flow says they may be. Where consent is
        /// required and has not been given, that means none — the policy AdMob approves an
        /// account against, not a revenue choice.
        /// </summary>
        private void StartIfConsented()
        {
            if (_started)
            {
                return;
            }

            if (!GoogleMobileAds.Ump.Api.ConsentInformation.CanRequestAds())
            {
                _sdkStatus = "sdk: blocked, consent says ads may not be requested";
                return;
            }

            _started = true;
            _sdkStatus = "sdk: initializing";

            GoogleMobileAds.Api.MobileAds.Initialize(_ => AdsManager.Post(() =>
            {
                _sdkStatus = "sdk: ready" + (AdsManager.TestAds ? " (TEST UNITS)" : " (real units)");
                LoadInterstitial();
                LoadRewarded();
            }));
        }

        public void ShowInterstitial()
        {
            if (IsInterstitialReady)
            {
                _interstitialAd.Show();
            }
        }

        public void Reload()
        {
            if (!_started)
            {
                return;
            }

            _interstitialFailures = 0;
            _rewardedFailures = 0;
            LoadInterstitial();
            LoadRewarded();
        }

        public void ShowRewarded(Action onEarned, Action onUnavailable)
        {
            if (!IsRewardedReady)
            {
                onUnavailable?.Invoke();
                return;
            }

            GoogleMobileAds.Api.RewardedAd ad = _rewardedAd;
            bool earned = false;
            bool finished = false;

            // Closed and Failed both end the showing, and exactly one of onEarned or
            // onUnavailable must still reach the caller — an ad that fails to present used to
            // leave the continue offer hanging with neither.
            void Finish()
            {
                if (finished)
                {
                    return;
                }

                finished = true;
                ad.OnAdFullScreenContentClosed -= HandleClosed;
                ad.OnAdFullScreenContentFailed -= HandleFailed;
                LoadRewarded();

                if (!earned)
                {
                    onUnavailable?.Invoke();
                }
            }

            void HandleClosed()
            {
                AdsManager.Post(Finish);
            }

            void HandleFailed(GoogleMobileAds.Api.AdError error)
            {
                AdsManager.Post(Finish);
            }

            ad.OnAdFullScreenContentClosed += HandleClosed;
            ad.OnAdFullScreenContentFailed += HandleFailed;

            // Posted in the order the SDK reports them, so a reward that arrives before the
            // close is still counted before Finish decides whether it was earned.
            ad.Show(_ => AdsManager.Post(() =>
            {
                earned = true;
                onEarned?.Invoke();
            }));
        }

        private void LoadInterstitial()
        {
            if (_interstitialAd != null)
            {
                _interstitialAd.Destroy();
                _interstitialAd = null;
            }

            _interstitialStatus = "interstitial: loading";

            GoogleMobileAds.Api.InterstitialAd.Load(InterstitialUnit, new GoogleMobileAds.Api.AdRequest(), (ad, error) => AdsManager.Post(() =>
            {
                if (error != null || ad == null)
                {
                    _interstitialStatus = "interstitial: failed " + (error != null ? error.GetCode() + " " + error.GetMessage() : "no ad");
                    RetryAfterBackoff(++_interstitialFailures, LoadInterstitial);
                    return;
                }

                _interstitialFailures = 0;
                _interstitialStatus = "interstitial: ready";
                _interstitialAd = ad;
                _interstitialAd.OnAdFullScreenContentClosed += () => AdsManager.Post(LoadInterstitial);
                _interstitialAd.OnAdFullScreenContentFailed += _ => AdsManager.Post(LoadInterstitial);
            }));
        }

        private void LoadRewarded()
        {
            if (_rewardedAd != null)
            {
                _rewardedAd.Destroy();
                _rewardedAd = null;
            }

            _rewardedStatus = "rewarded: loading";

            GoogleMobileAds.Api.RewardedAd.Load(RewardedUnit, new GoogleMobileAds.Api.AdRequest(), (ad, error) => AdsManager.Post(() =>
            {
                if (error != null || ad == null)
                {
                    _rewardedStatus = "rewarded: failed " + (error != null ? error.GetCode() + " " + error.GetMessage() : "no ad");
                    RetryAfterBackoff(++_rewardedFailures, LoadRewarded);
                    return;
                }

                _rewardedFailures = 0;
                _rewardedStatus = "rewarded: loaded";
                _rewardedAd = ad;
            }));
        }

        // Started from a load callback that AdsManager.Post already moved to the main thread,
        // so the await resumes there too, through Unity's synchronisation context.
        private static async void RetryAfterBackoff(int failures, Action load)
        {
            int seconds = 1 << Math.Min(failures, MaxBackoffExponent);
            await Task.Delay(seconds * 1000);

            // Leaving play mode in the editor does not cancel a pending delay.
            if (UnityEngine.Application.isPlaying)
            {
                load();
            }
        }
#else
        public bool IsInterstitialReady => false;

        public bool IsRewardedReady => false;

        public bool PrivacyOptionsRequired => false;

        public void Initialize()
        {
        }

        public void ShowPrivacyOptions()
        {
        }

        public void ShowInterstitial()
        {
        }

        public void Reload()
        {
        }

        public string DebugStatus => "ads are not compiled into this build (SLICEBLAST_ADS_ENABLED off)";

        public void ShowRewarded(Action onEarned, Action onUnavailable)
        {
            onUnavailable?.Invoke();
        }
#endif
    }
}
