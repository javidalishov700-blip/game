// Everything that outlives a single run: the best score, the coin balance, what has been
// bought, what is equipped and the daily streak. One JSON blob under one PlayerPrefs key
// rather than a key per value — a scattered set of keys is how save data quietly drifts out
// of sync with itself, and it makes adding a field a migration every time.
//
// Writes are throttled: gameplay calls AddCoins in the middle of a blast, and PlayerPrefs.Save
// on iOS is a synchronous plist write. The dirty flag is flushed on a timer, on pause, and on
// quit, so the worst case a crash can cost is a few seconds of coins.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SliceBlast.Meta
{
    public enum UpgradeId : byte
    {
        Magnet = 0,
        Shield = 1,
        Luck = 2
    }

    [Serializable]
    public sealed class ProfileData
    {
        public int version = 1;

        public int bestScore;
        public int coins;
        public int totalRuns;
        public int totalBlasts;
        public int biggestBlast;
        public int longestChain;
        public long lifetimeScore;

        public bool soundOn = true;
        public bool hapticsOn = true;
        public bool adsRemoved;

        public int[] upgradeLevels = new int[3];
        public List<string> ownedThemes = new List<string>();
        public string equippedTheme = string.Empty;

        public int reviveTokens;
        public int dailyStreak;
        public string lastPlayDay = string.Empty;

        /// <summary>The fault-line explanation is shown once, ever, and then never again.</summary>
        public bool sawFaultHint;

        // Missions are stored as three parallel lists rather than a list of structs:
        // JsonUtility will not serialise a List<T> of a nested serialisable type reliably
        // across Unity versions, and three flat lists cost nothing to keep aligned.
        public List<string> missionIds = new List<string>();
        public List<int> missionProgress = new List<int>();
        public List<int> missionClaimed = new List<int>();
        public string missionDay = string.Empty;
    }

    public static class PlayerProfile
    {
        private const string ProfileKey = "sliceblast.profile";

        // 1.0 and 1.1 shipped with these three loose keys. They are read once on first load
        // and then left alone for good — never deleted, so a player who downgrades does not
        // lose their best score.
        private const string LegacyBestKey = "sliceblast.best";
        private const string LegacySoundKey = "sliceblast.sound";
        private const string LegacyHapticsKey = "sliceblast.haptics";

        private const float SaveInterval = 4f;

        private static ProfileData _data;
        private static bool _dirty;
        private static float _nextSave;

        public static event Action<int> CoinsChanged;
        public static event Action InventoryChanged;

        public static ProfileData Data
        {
            get
            {
                if (_data == null)
                {
                    Load();
                }

                return _data;
            }
        }

        public static int Coins => Data.coins;
        public static int BestScore => Data.bestScore;
        public static bool AdsRemoved => Data.adsRemoved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // A domain reload keeps static fields alive in the editor; a stale subscriber
            // list would fire into a destroyed HUD on the next play.
            CoinsChanged = null;
            InventoryChanged = null;
            _data = null;
            _dirty = false;
            _nextSave = 0f;
        }

        public static void Load()
        {
            string json = PlayerPrefs.GetString(ProfileKey, string.Empty);

            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    _data = JsonUtility.FromJson<ProfileData>(json);
                }
                catch (Exception)
                {
                    _data = null;
                }
            }

            if (_data == null)
            {
                _data = new ProfileData();
                MigrateLegacyKeys(_data);
                _dirty = true;
            }

            Repair(_data);
        }

        private static void MigrateLegacyKeys(ProfileData data)
        {
            data.bestScore = PlayerPrefs.GetInt(LegacyBestKey, 0);
            data.soundOn = PlayerPrefs.GetInt(LegacySoundKey, 1) == 1;
            data.hapticsOn = PlayerPrefs.GetInt(LegacyHapticsKey, 1) == 1;
        }

        /// <summary>
        /// A profile that came back from disk short an array — an older build, a truncated
        /// write — must never be the thing that throws on the first frame.
        /// </summary>
        private static void Repair(ProfileData data)
        {
            int upgradeCount = Enum.GetValues(typeof(UpgradeId)).Length;

            if (data.upgradeLevels == null || data.upgradeLevels.Length < upgradeCount)
            {
                int[] resized = new int[upgradeCount];

                if (data.upgradeLevels != null)
                {
                    Array.Copy(data.upgradeLevels, resized, data.upgradeLevels.Length);
                }

                data.upgradeLevels = resized;
            }

            if (data.ownedThemes == null)
            {
                data.ownedThemes = new List<string>();
            }

            if (data.missionIds == null)
            {
                data.missionIds = new List<string>();
            }

            if (data.missionProgress == null)
            {
                data.missionProgress = new List<int>();
            }

            if (data.missionClaimed == null)
            {
                data.missionClaimed = new List<int>();
            }

            // A legacy best score that survived in the old key but not in the blob (a profile
            // written by a build that never knew about it) is still the player's record.
            int legacyBest = PlayerPrefs.GetInt(LegacyBestKey, 0);

            if (legacyBest > data.bestScore)
            {
                data.bestScore = legacyBest;
                _dirty = true;
            }
        }

        public static void MarkDirty()
        {
            _dirty = true;
        }

        /// <summary>Called once a frame by the bootstrap; writes at most every few seconds.</summary>
        public static void Tick(float unscaledDeltaTime)
        {
            if (!_dirty)
            {
                return;
            }

            _nextSave -= unscaledDeltaTime;

            if (_nextSave <= 0f)
            {
                Flush();
            }
        }

        public static void Flush()
        {
            if (_data == null || !_dirty)
            {
                return;
            }

            _dirty = false;
            _nextSave = SaveInterval;

            PlayerPrefs.SetString(ProfileKey, JsonUtility.ToJson(_data));

            // Kept in step so a rollback to 1.1 still finds the player's record.
            PlayerPrefs.SetInt(LegacyBestKey, _data.bestScore);
            PlayerPrefs.SetInt(LegacySoundKey, _data.soundOn ? 1 : 0);
            PlayerPrefs.SetInt(LegacyHapticsKey, _data.hapticsOn ? 1 : 0);

            PlayerPrefs.Save();
        }

        public static void AddCoins(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            ProfileData data = Data;
            data.coins += amount;
            _dirty = true;
            CoinsChanged?.Invoke(data.coins);
        }

        public static bool TrySpend(int amount)
        {
            ProfileData data = Data;

            if (amount <= 0 || data.coins < amount)
            {
                return false;
            }

            data.coins -= amount;
            _dirty = true;
            CoinsChanged?.Invoke(data.coins);
            return true;
        }

        public static int GetUpgradeLevel(UpgradeId id)
        {
            int index = (int)id;
            int[] levels = Data.upgradeLevels;
            return index >= 0 && index < levels.Length ? levels[index] : 0;
        }

        public static void SetUpgradeLevel(UpgradeId id, int level)
        {
            int index = (int)id;
            int[] levels = Data.upgradeLevels;

            if (index < 0 || index >= levels.Length)
            {
                return;
            }

            levels[index] = Mathf.Max(0, level);
            _dirty = true;
            InventoryChanged?.Invoke();
        }

        public static bool OwnsTheme(string id)
        {
            return !string.IsNullOrEmpty(id) && Data.ownedThemes.Contains(id);
        }

        public static void GrantTheme(string id)
        {
            if (string.IsNullOrEmpty(id) || OwnsTheme(id))
            {
                return;
            }

            Data.ownedThemes.Add(id);
            _dirty = true;
            InventoryChanged?.Invoke();
        }

        public static void EquipTheme(string id)
        {
            Data.equippedTheme = id ?? string.Empty;
            _dirty = true;
            InventoryChanged?.Invoke();
        }

        public static void MarkFaultHintSeen()
        {
            if (Data.sawFaultHint)
            {
                return;
            }

            Data.sawFaultHint = true;
            _dirty = true;
        }

        public static void SetSound(bool on)
        {
            Data.soundOn = on;
            _dirty = true;
        }

        public static void SetHaptics(bool on)
        {
            Data.hapticsOn = on;
            _dirty = true;
        }

        public static void SetAdsRemoved(bool removed)
        {
            if (Data.adsRemoved == removed)
            {
                return;
            }

            Data.adsRemoved = removed;
            _dirty = true;
            InventoryChanged?.Invoke();
        }

        public static void AddReviveTokens(int count)
        {
            if (count <= 0)
            {
                return;
            }

            Data.reviveTokens += count;
            _dirty = true;
            InventoryChanged?.Invoke();
        }

        public static bool TryConsumeReviveToken()
        {
            ProfileData data = Data;

            if (data.reviveTokens <= 0)
            {
                return false;
            }

            data.reviveTokens--;
            _dirty = true;
            InventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Records a finished run and returns true if it set a new record.
        ///
        /// A run continued with a rewarded ad ends more than once, so the two kinds of number
        /// here are deliberately separate: <paramref name="runScore"/> is the tower's running
        /// total and is compared against the record, while <paramref name="scoreDelta"/> and
        /// <paramref name="blastsDelta"/> are only what has not been banked yet and are what
        /// the lifetime accumulators add. Passing the total to both would credit a revived run
        /// twice for the same tower.
        /// </summary>
        public static bool RecordRun(
            int runScore,
            int scoreDelta,
            int blastsDelta,
            int biggestBlast,
            int longestChain,
            bool countRun)
        {
            ProfileData data = Data;

            if (countRun)
            {
                data.totalRuns++;
            }

            data.totalBlasts += Mathf.Max(0, blastsDelta);
            data.lifetimeScore += Mathf.Max(0, scoreDelta);
            data.biggestBlast = Mathf.Max(data.biggestBlast, biggestBlast);
            data.longestChain = Mathf.Max(data.longestChain, longestChain);

            bool record = runScore > data.bestScore;

            if (record)
            {
                data.bestScore = runScore;
            }

            _dirty = true;
            return record;
        }

        /// <summary>Today in UTC, always on the Gregorian calendar.</summary>
        public static string TodayKey()
        {
            // InvariantCulture is not decoration: on a device set to a Thai or Umm al-Qura
            // locale, ToString("yyyy") renders the *local calendar's* year, so a stored key
            // would never match one written on a differently configured device — and the
            // streak would silently reset every time.
            return DateTime.UtcNow.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The daily streak. Counted in UTC days so a timezone hop cannot mint a free day,
        /// and returns the streak length only on the day it actually advances.
        /// </summary>
        public static int TouchDailyStreak()
        {
            ProfileData data = Data;
            DateTime today = DateTime.UtcNow.Date;
            string key = TodayKey();

            if (data.lastPlayDay == key)
            {
                return 0;
            }

            bool consecutive = false;

            if (!string.IsNullOrEmpty(data.lastPlayDay)
                && DateTime.TryParseExact(
                    data.lastPlayDay,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime previous))
            {
                consecutive = (today - previous.Date).TotalDays <= 1.5;
            }

            data.dailyStreak = consecutive ? data.dailyStreak + 1 : 1;
            data.lastPlayDay = key;
            _dirty = true;

            return data.dailyStreak;
        }
    }
}
