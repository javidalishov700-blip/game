// A live run, written down the moment the app leaves the foreground so reopening the game
// puts the player back on the same tower instead of the title screen. iOS can kill a
// backgrounded app without another callback, so this is the only chance there is.
//
// Deliberately separate from PlayerProfile: the profile is what the player owns, this is a
// single in-flight run that is thrown away the moment that run ends, is abandoned, or a new
// one starts. Layers are parallel arrays rather than a list of structs for the same reason
// ProfileData keeps its missions flat — JsonUtility handles flat arrays of Unity types
// reliably across versions.
using System;
using UnityEngine;

namespace SliceBlast.Core
{
    [Serializable]
    public sealed class RunSnapshot
    {
        // Bump whenever a field changes meaning; an older snapshot is discarded, never guessed at.
        public const int CurrentVersion = 1;

        private const string Key = "sliceblast.run";
        private const int MaxLayers = 500;

        public int version = CurrentVersion;

        public int score;
        public int perfectStreak;
        public int spawnCount;
        // Where the speed ramp counts from — set by a rewarded continue, 0 otherwise. Absent
        // from an older save, which reads as 0 and means exactly that.
        public int rampOrigin;
        public int comboMultiplier = 1;
        public int blastLevel;
        public int blastCount;

        public float speed;
        public float tutorialProgress;
        public float slowdown;
        public float electricTimer;

        public int shieldCharges;
        public int runCoins;
        public int biggestBlast;
        public int perfectCount;
        public int specialCount;

        public int bankedScore;
        public int bankedBlasts;
        public int bankedPerfects;
        public int bankedSpecials;
        public bool runRecorded;

        public bool axisX;
        public float nextSizeX;
        public float nextSizeZ;

        // A Neon special that had already marked its layers but not yet fired.
        public int neonLayers;
        public Color neonColor = Color.white;

        public bool spawnerForceStandard;
        public int spawnerSinceSpecial;
        public int spawnerGap;
        public int spawnerSpawnCount;
        public int spawnerLastNeon = -1;

        // Every layer above the base platform, bottom to top. The platform itself is rebuilt.
        public Vector3[] positions = new Vector3[0];
        public Vector3[] scales = new Vector3[0];
        public Color[] tints = new Color[0];
        public int[] types = new int[0];

        public static void Save(RunSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            PlayerPrefs.SetString(Key, JsonUtility.ToJson(snapshot));
            PlayerPrefs.Save();
        }

        /// <summary>The saved run, or null if there is none or it cannot be trusted.</summary>
        public static RunSnapshot Load()
        {
            string json = PlayerPrefs.GetString(Key, string.Empty);

            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            RunSnapshot snapshot = null;

            try
            {
                snapshot = JsonUtility.FromJson<RunSnapshot>(json);
            }
            catch (Exception)
            {
                snapshot = null;
            }

            if (snapshot == null || !snapshot.IsUsable())
            {
                // A snapshot that cannot be restored would otherwise be retried, and fail, on
                // every single launch.
                Clear();
                return null;
            }

            return snapshot;
        }

        public static void Clear()
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                return;
            }

            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        private bool IsUsable()
        {
            if (version != CurrentVersion || positions == null || scales == null || tints == null || types == null)
            {
                return false;
            }

            int count = positions.Length;

            if (count > MaxLayers || scales.Length != count || tints.Length != count || types.Length != count)
            {
                return false;
            }

            if (score < 0 || perfectStreak < 0 || spawnCount < 0 || comboMultiplier < 1 || blastLevel < 0 || blastCount < 0)
            {
                return false;
            }

            if (!IsFinite(speed) || !IsFinite(tutorialProgress) || !IsFinite(slowdown) || !IsFinite(electricTimer))
            {
                return false;
            }

            if (!IsFinite(nextSizeX) || !IsFinite(nextSizeZ) || nextSizeX <= 0f || nextSizeZ <= 0f)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 p = positions[i];
                Vector3 s = scales[i];

                if (!IsFinite(p.x) || !IsFinite(p.y) || !IsFinite(p.z) || !IsFinite(s.x) || !IsFinite(s.y) || !IsFinite(s.z))
                {
                    return false;
                }

                if (s.x <= 0f || s.y <= 0f || s.z <= 0f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
