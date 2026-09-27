// Game Center, through Unity's built-in Social API. Deliberately not a package and not a
// plugin: UnityEngine.Social is part of the engine and is backed by GameKit on iOS already,
// so a global leaderboard costs this project no new dependency, no server and nothing extra
// for the build pipeline to resolve — which, given how much of this app's history has been
// spent fighting CocoaPods, is the entire reason it is done this way.
//
// Authentication is fire-and-forget. Everything below is safe to call whether or not it
// succeeded: an unauthenticated report is dropped, and a load answers null.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace SliceBlast.Platform
{
    /// <summary>One row of the board, already resolved to a display name.</summary>
    public struct LeaderboardEntry
    {
        public int Rank;
        public string Name;
        public long Score;
        public bool IsLocalPlayer;
    }

    /// <summary>The top of the board plus where the local player stands, wherever that is.</summary>
    public sealed class LeaderboardPage
    {
        public LeaderboardEntry[] Top;

        /// <summary>False until the player has posted a score at all.</summary>
        public bool HasLocalScore;
        public int LocalRank;
        public long LocalScore;
        public string LocalName;
    }

    public static class Leaderboards
    {
        // Must match the leaderboard ID created in App Store Connect under the app's
        // Game Center configuration.
        public const string BestScoreId = "com.javidalishov.sliceblast.best";

        private static bool _attempted;

        /// <summary>Raised once sign-in has an answer, successful or not.</summary>
        public static event Action<bool> AuthenticationResolved;

        // Every reference to the engine's Social class is fully qualified on purpose. This
        // file used to live in a namespace called SliceBlast.Social, where a bare `Social`
        // binds to *that namespace* rather than to UnityEngine.Social — C# resolves the
        // enclosing namespace first, and the result is a compile error about `localUser` not
        // existing in a namespace. Renaming to Platform fixes it; qualifying keeps it fixed
        // if anyone ever moves this file back.
        public static bool IsAuthenticated =>
            UnityEngine.Social.localUser != null && UnityEngine.Social.localUser.authenticated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _attempted = false;
            AuthenticationResolved = null;
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

            if (UnityEngine.Social.localUser == null)
            {
                AuthenticationResolved?.Invoke(false);
                return;
            }

            UnityEngine.Social.localUser.Authenticate(success => AuthenticationResolved?.Invoke(success));
        }

        /// <summary>Posts a score. Silently ignored when the player is not signed in.</summary>
        public static void ReportBestScore(int score)
        {
            if (score <= 0 || !IsAuthenticated)
            {
                return;
            }

            UnityEngine.Social.ReportScore(score, BestScoreId, success => { });
        }

        /// <summary>
        /// Loads ranks 1..count of the all-time board with Game Center display names, plus
        /// the local player's own entry even when it sits far below that. Answers null on any
        /// failure — not signed in, the leaderboard missing from App Store Connect, no network.
        /// </summary>
        public static void LoadTop(int count, Action<LeaderboardPage> done)
        {
            if (!IsAuthenticated)
            {
                done?.Invoke(null);
                return;
            }

            ILeaderboard board = UnityEngine.Social.CreateLeaderboard();
            board.id = BestScoreId;
            board.userScope = UserScope.Global;
            board.timeScope = TimeScope.AllTime;
            // Qualified on purpose: System.Range exists too, and a bare Range is ambiguous the
            // moment this file gains a `using System;`.
            board.range = new UnityEngine.SocialPlatforms.Range(1, count);

            board.LoadScores(success =>
            {
                if (!success)
                {
                    done?.Invoke(null);
                    return;
                }

                IScore[] scores = board.scores ?? new IScore[0];

                if (scores.Length == 0)
                {
                    done?.Invoke(BuildPage(board, scores, null));
                    return;
                }

                // Scores carry player IDs only; the names are a second round trip.
                string[] ids = new string[scores.Length];

                for (int i = 0; i < scores.Length; i++)
                {
                    ids[i] = scores[i].userID;
                }

                UnityEngine.Social.LoadUsers(ids, profiles => done?.Invoke(BuildPage(board, scores, profiles)));
            });
        }

        private static LeaderboardPage BuildPage(ILeaderboard board, IScore[] scores, IUserProfile[] profiles)
        {
            Dictionary<string, string> names = new Dictionary<string, string>(scores.Length);

            if (profiles != null)
            {
                foreach (IUserProfile profile in profiles)
                {
                    if (profile != null && !string.IsNullOrEmpty(profile.id) && !string.IsNullOrEmpty(profile.userName))
                    {
                        names[profile.id] = profile.userName;
                    }
                }
            }

            ILocalUser local = UnityEngine.Social.localUser;
            string localId = local != null ? local.id : null;
            string localName = local != null && !string.IsNullOrEmpty(local.userName) ? local.userName : "YOU";

            LeaderboardEntry[] top = new LeaderboardEntry[scores.Length];

            for (int i = 0; i < scores.Length; i++)
            {
                IScore score = scores[i];
                bool isLocal = !string.IsNullOrEmpty(localId) && score.userID == localId;

                top[i] = new LeaderboardEntry
                {
                    Rank = score.rank > 0 ? score.rank : i + 1,
                    Name = isLocal ? localName : names.TryGetValue(score.userID ?? string.Empty, out string name) ? name : "PLAYER",
                    Score = score.value,
                    IsLocalPlayer = isLocal
                };
            }

            Array.Sort(top, (a, b) => a.Rank.CompareTo(b.Rank));

            IScore mine = board.localUserScore;
            bool hasLocal = mine != null && mine.rank > 0 && mine.value > 0;

            return new LeaderboardPage
            {
                Top = top,
                HasLocalScore = hasLocal,
                LocalRank = hasLocal ? mine.rank : 0,
                LocalScore = hasLocal ? mine.value : 0,
                LocalName = localName
            };
        }
    }
}
