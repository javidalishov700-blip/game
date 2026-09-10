// Game Center, through UnityEngine.SocialPlatforms. Deliberately not a package and not a
// plugin: the Social API is part of the engine, and on iOS it is backed by GameKit already,
// so a global leaderboard costs this project no new dependency, no server and nothing extra
// for the build pipeline to resolve — which, given how much of this app's history has been
// spent fighting CocoaPods, is the entire reason it is done this way.
//
// Authentication is fire-and-forget. Everything below is safe to call whether or not it
// succeeded: an unauthenticated report is dropped, and the board UI simply does not open.
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace SliceBlast.Social
{
    public static class Leaderboards
    {
        // Must match the leaderboard ID created in App Store Connect under the app's
        // Game Center configuration.
        public const string BestScoreId = "com.javidalishov.sliceblast.best";

        private static bool _attempted;

        public static bool IsAuthenticated => Social.localUser != null && Social.localUser.authenticated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _attempted = false;
        }

        /// <summary>
        /// Asks Game Center to sign the player in, once per session. Declining is a normal
        /// outcome, not an error — iOS shows its own banner and the game carries on without
        /// a leaderboard, so nothing here reports failure to the player.
        /// </summary>
        public static void Authenticate()
        {
            if (_attempted || IsAuthenticated)
            {
                return;
            }

            _attempted = true;

            if (Social.localUser == null)
            {
                return;
            }

            Social.localUser.Authenticate(success => { });
        }

        /// <summary>Posts a score. Silently ignored when the player is not signed in.</summary>
        public static void ReportBestScore(int score)
        {
            if (score <= 0 || !IsAuthenticated)
            {
                return;
            }

            Social.ReportScore(score, BestScoreId, success => { });
        }

        /// <summary>
        /// Opens Game Center's own leaderboard UI. Returns false when there is nothing to
        /// open, so the caller can leave its button hidden rather than showing one that does
        /// nothing when tapped.
        /// </summary>
        public static bool ShowUi()
        {
            if (!IsAuthenticated)
            {
                Authenticate();
                return false;
            }

            Social.ShowLeaderboardUI();
            return true;
        }
    }
}
