// Three missions a day, rolled from the calendar date rather than from Random.value — the
// same day always produces the same three, so closing the app mid-mission never reshuffles
// them and a player can plan a session around one.
//
// Progress is reported by the bootstrap from signals it already handles; nothing here
// subscribes to anything, which keeps the meta layer out of the gameplay event graph.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SliceBlast.Meta
{
    public enum MissionKind : byte
    {
        RunScore = 0,
        Perfects = 1,
        Blasts = 2,
        Chain = 3,
        Specials = 4
    }

    public struct MissionDefinition
    {
        public string Id;
        public MissionKind Kind;
        public int Target;
        public int Reward;
        public string Text;
    }

    public static class MissionSystem
    {
        public const int DailyCount = 3;

        private static readonly MissionDefinition[] Pool =
        {
            new MissionDefinition { Id = "score800",  Kind = MissionKind.RunScore, Target = 800,  Reward = 120, Text = "SCORE 800 IN ONE RUN" },
            new MissionDefinition { Id = "score2000", Kind = MissionKind.RunScore, Target = 2000, Reward = 260, Text = "SCORE 2000 IN ONE RUN" },
            new MissionDefinition { Id = "score5000", Kind = MissionKind.RunScore, Target = 5000, Reward = 520, Text = "SCORE 5000 IN ONE RUN" },
            new MissionDefinition { Id = "perfect30", Kind = MissionKind.Perfects, Target = 30,   Reward = 140, Text = "LAND 30 PERFECT DROPS" },
            new MissionDefinition { Id = "perfect80", Kind = MissionKind.Perfects, Target = 80,   Reward = 300, Text = "LAND 80 PERFECT DROPS" },
            new MissionDefinition { Id = "blast5",    Kind = MissionKind.Blasts,   Target = 5,    Reward = 150, Text = "TRIGGER 5 BLASTS" },
            new MissionDefinition { Id = "blast15",   Kind = MissionKind.Blasts,   Target = 15,   Reward = 340, Text = "TRIGGER 15 BLASTS" },
            new MissionDefinition { Id = "chain3",    Kind = MissionKind.Chain,    Target = 3,    Reward = 220, Text = "SET OFF A 3-CHAIN" },
            new MissionDefinition { Id = "chain5",    Kind = MissionKind.Chain,    Target = 5,    Reward = 420, Text = "SET OFF A 5-CHAIN" },
            new MissionDefinition { Id = "special6",  Kind = MissionKind.Specials, Target = 6,    Reward = 180, Text = "LAND 6 SPECIAL BLOCKS" },
            new MissionDefinition { Id = "special15", Kind = MissionKind.Specials, Target = 15,   Reward = 360, Text = "LAND 15 SPECIAL BLOCKS" }
        };

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Changed = null;
        }

        /// <summary>Rolls a fresh set if the stored day is not today. Safe to call often.</summary>
        public static void EnsureToday()
        {
            ProfileData data = PlayerProfile.Data;
            string today = PlayerProfile.TodayKey();

            if (data.missionDay == today && data.missionIds.Count == DailyCount)
            {
                return;
            }

            data.missionDay = today;
            data.missionIds.Clear();
            data.missionProgress.Clear();
            data.missionClaimed.Clear();

            foreach (string id in RollForDay(today))
            {
                data.missionIds.Add(id);
                data.missionProgress.Add(0);
                data.missionClaimed.Add(0);
            }

            PlayerProfile.MarkDirty();
            Changed?.Invoke();
        }

        /// <summary>
        /// Three distinct pool entries, chosen from a seed derived from the date. Two missions
        /// of the same kind on one day would read as a bug, so a kind is only used once.
        /// </summary>
        private static List<string> RollForDay(string day)
        {
            var picked = new List<string>(DailyCount);
            var usedKinds = new List<MissionKind>(DailyCount);

            // Hashed by hand rather than through string.GetHashCode: the runtime is free to
            // randomise that per process, and a seed that changes on relaunch would reshuffle
            // the day's missions every time the app is reopened.
            int seed = 17;

            for (int i = 0; i < day.Length; i++)
            {
                seed = unchecked(seed * 31 + day[i]);
            }

            System.Random random = new System.Random(seed);

            // Walk the pool in a shuffled order rather than re-rolling indices: with only
            // eleven entries a retry loop can spin for a long time once most kinds are taken.
            var order = new List<int>(Pool.Length);

            for (int i = 0; i < Pool.Length; i++)
            {
                order.Add(i);
            }

            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int swap = order[i];
                order[i] = order[j];
                order[j] = swap;
            }

            for (int i = 0; i < order.Count && picked.Count < DailyCount; i++)
            {
                MissionDefinition candidate = Pool[order[i]];

                if (usedKinds.Contains(candidate.Kind))
                {
                    continue;
                }

                usedKinds.Add(candidate.Kind);
                picked.Add(candidate.Id);
            }

            // Only reachable if the pool ever shrinks below three kinds; fill rather than
            // hand back a short list the UI would have to special-case.
            for (int i = 0; picked.Count < DailyCount && i < Pool.Length; i++)
            {
                if (!picked.Contains(Pool[i].Id))
                {
                    picked.Add(Pool[i].Id);
                }
            }

            return picked;
        }

        public static MissionDefinition Definition(string id)
        {
            for (int i = 0; i < Pool.Length; i++)
            {
                if (Pool[i].Id == id)
                {
                    return Pool[i];
                }
            }

            return Pool[0];
        }

        public static int Count => Mathf.Min(DailyCount, PlayerProfile.Data.missionIds.Count);

        public static MissionDefinition At(int index)
        {
            ProfileData data = PlayerProfile.Data;

            if (index < 0 || index >= data.missionIds.Count)
            {
                return Pool[0];
            }

            return Definition(data.missionIds[index]);
        }

        public static int ProgressAt(int index)
        {
            ProfileData data = PlayerProfile.Data;
            return index >= 0 && index < data.missionProgress.Count ? data.missionProgress[index] : 0;
        }

        public static bool IsClaimed(int index)
        {
            ProfileData data = PlayerProfile.Data;
            return index >= 0 && index < data.missionClaimed.Count && data.missionClaimed[index] != 0;
        }

        public static bool IsComplete(int index)
        {
            return ProgressAt(index) >= At(index).Target;
        }

        /// <summary>How many finished missions are sitting there unclaimed — the badge count.</summary>
        public static int ClaimableCount()
        {
            int count = 0;

            for (int i = 0; i < Count; i++)
            {
                if (IsComplete(i) && !IsClaimed(i))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Counters accumulate; a personal-best style mission (a run score, a chain length)
        /// keeps the largest single value instead, which is why the two are split here rather
        /// than at the call site.
        /// </summary>
        public static void Report(MissionKind kind, int value)
        {
            if (value <= 0)
            {
                return;
            }

            ProfileData data = PlayerProfile.Data;
            bool changed = false;

            for (int i = 0; i < Count; i++)
            {
                MissionDefinition definition = At(i);

                if (definition.Kind != kind || IsClaimed(i))
                {
                    continue;
                }

                bool best = kind == MissionKind.RunScore || kind == MissionKind.Chain;
                int current = data.missionProgress[i];
                int updated = best ? Mathf.Max(current, value) : current + value;
                updated = Mathf.Min(updated, definition.Target);

                if (updated != current)
                {
                    data.missionProgress[i] = updated;
                    changed = true;
                }
            }

            if (changed)
            {
                PlayerProfile.MarkDirty();
                Changed?.Invoke();
            }
        }

        /// <summary>Pays out a finished mission. Returns the coins awarded, or zero.</summary>
        public static int TryClaim(int index)
        {
            if (!IsComplete(index) || IsClaimed(index))
            {
                return 0;
            }

            ProfileData data = PlayerProfile.Data;
            data.missionClaimed[index] = 1;

            int reward = At(index).Reward;
            PlayerProfile.AddCoins(reward);
            Changed?.Invoke();

            return reward;
        }
    }
}
