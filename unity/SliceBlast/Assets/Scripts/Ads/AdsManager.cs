// The game's whole relationship with advertising: when an interstitial is allowed, what a
// rewarded ad is worth, and whether the player has paid them away. Which network actually
// serves the ad is an implementation detail behind IAdProvider.
//
// Providers are tried in order and the first one with an ad in hand shows it. That is a
// client-side waterfall, not mediation — it does not compare what each network would pay, it
// just takes the first that can fill. For this app that is the right trade: real mediation
// means another SDK, another account and another review, and the whole point of the second
// network here is to not be blocked on any one of those again.
using System;
using System.Collections;
using System.Collections.Generic;
using SliceBlast.Meta;
using UnityEngine;

namespace SliceBlast.Ads
{
    [DisallowMultipleComponent]
    public sealed class AdsManager : MonoBehaviour
    {
        // A miss between runs is an event, not a rhythm — the very first game over of a
        // session never carries one, and every one after that costs a fixed number of runs.
        private const int RunsBetweenInterstitials = 3;

        // Apple's ATT prompt silently skips itself — no alert, no error — when requested
        // before iOS has made the app's window key and visible, and this object is created
        // during Awake on the very first frame.
        private const float AttDelaySeconds = 1f;

        public static AdsManager Instance { get; private set; }

        private readonly List<IAdProvider> _providers = new List<IAdProvider>(2);

        private int _runsSinceInterstitial;
        private bool _initialized;

        /// <summary>True when any provider has a rewarded ad ready to show.</summary>
        public bool IsRewardedReady => FindRewardedProvider() != null;

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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            // Order is the waterfall. A provider whose SDK is not in the build compiles to one
            // that is never ready, so an entry here costs nothing until its package exists.
            _providers.Add(new AdMobProvider());

            StartCoroutine(RequestAttThenInitProviders());
        }

        private IEnumerator RequestAttThenInitProviders()
        {
            yield return new WaitForSeconds(AttDelaySeconds);

            // Once, for the whole app, before any network asks for the advertising identifier.
            // Doing this per provider would show the player the same system prompt twice.
            AppTrackingTransparency.RequestIfNeeded();

            for (int i = 0; i < _providers.Count; i++)
            {
                _providers[i].Initialize();
            }
        }

        /// <summary>Call once per completed run, after the run-over screen is shown.</summary>
        public void NotifyRunEnded()
        {
            // Guarded here as well as at the call site. A player who paid to remove ads must
            // never see one, and "every caller remembered to check" is not a guarantee — the
            // one place that actually shows the ad is.
            if (PlayerProfile.AdsRemoved)
            {
                return;
            }

            _runsSinceInterstitial++;

            if (_runsSinceInterstitial < RunsBetweenInterstitials)
            {
                return;
            }

            for (int i = 0; i < _providers.Count; i++)
            {
                if (_providers[i].IsInterstitialReady)
                {
                    // Only reset the counter when one actually shows. A run that found no fill
                    // should not cost the player their place in the cadence.
                    _runsSinceInterstitial = 0;
                    _providers[i].ShowInterstitial();
                    return;
                }
            }
        }

        /// <summary>
        /// Shows a rewarded ad from the first provider that has one. onEarned fires only once
        /// the player actually finished watching; onUnavailable fires immediately if nothing is
        /// loaded anywhere, and also if the player closes the ad early.
        ///
        /// Not gated on AdsRemoved: a rewarded ad is opt-in and is the one the player asked
        /// for. Removing ads buys the end of interruptions, not the end of second chances.
        /// </summary>
        public void ShowRewarded(Action onEarned, Action onUnavailable)
        {
            IAdProvider provider = FindRewardedProvider();

            if (provider == null)
            {
                onUnavailable?.Invoke();
                return;
            }

            provider.ShowRewarded(onEarned, onUnavailable);
        }

        private IAdProvider FindRewardedProvider()
        {
            for (int i = 0; i < _providers.Count; i++)
            {
                if (_providers[i].IsRewardedReady)
                {
                    return _providers[i];
                }
            }

            return null;
        }
    }
}

// ---------------------------------------------------------------------------------------------
// Adding a second network
//
// The reason this file is shaped the way it is: AdMob approval has been stuck behind an
// unexplained account review, and an app whose only revenue path is one account nobody can
// appeal to is a single point of failure. A second network is insurance, not an upgrade.
//
// To add one — Unity LevelPlay is the natural candidate, since this is a Unity project and the
// account already exists:
//
//   1. Install its package in the Unity Editor and COMMIT Packages/manifest.json. A define
//      without its package is a compile failure; this project has already been bitten by that
//      once with the ATT package.
//   2. Write Ads/LevelPlayProvider.cs implementing IAdProvider, guarded by its own define
//      exactly as AdMobProvider is.
//   3. Add the define in SliceBlastBuild.ApplyPlayerSettings, and one line to the provider
//      list in Initialize() above. Order there is the waterfall order.
//
// Nothing else in the game changes: the HUD, the run-over screen and the coin economy all talk
// to AdsManager and have never known which network is underneath.
// ---------------------------------------------------------------------------------------------
