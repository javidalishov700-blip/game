// What the game needs from an ad network, and nothing else: can you show one right now, and
// show it. Everything above this line — when an interstitial is allowed, whether the player
// paid them away, what a rewarded ad buys — belongs to AdsManager and is the same whichever
// network is underneath.
//
// This exists because the alternative is worse. AdMob was wired directly into AdsManager, so
// standing a second network up beside it would have meant two sets of #if branches interleaved
// through one class, each one able to break the other. A network is now a file that implements
// this and gets added to a list.
using System;

namespace SliceBlast.Ads
{
    public interface IAdProvider
    {
        /// <summary>For logs and nothing else — never shown to a player.</summary>
        string Name { get; }

        bool IsInterstitialReady { get; }
        bool IsRewardedReady { get; }

        /// <summary>
        /// Called once, after App Tracking Transparency has been answered. A provider that
        /// needs the advertising identifier must not request an ad before this.
        /// </summary>
        void Initialize();

        void ShowInterstitial();

        /// <summary>
        /// onEarned fires only once the player actually finished watching. onUnavailable fires
        /// immediately when nothing is loaded, and also when the player closes the ad early
        /// without earning — exactly one of the two always fires, so the caller can rely on
        /// being told either way.
        /// </summary>
        void ShowRewarded(Action onEarned, Action onUnavailable);
    }
}
