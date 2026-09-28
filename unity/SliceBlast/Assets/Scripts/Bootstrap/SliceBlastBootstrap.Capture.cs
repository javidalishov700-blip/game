// Store capture: the real game playing itself on a build machine, recorded frame by frame —
// App Store screenshots and the raw footage for the App Preview and the promo video. Built
// only into the Linux capture player (SLICEBLAST_SCREENSHOTS, set by
// SliceBlastBuild.BuildCaptureLinux); the shipped game contains none of it.
//
// Time is locked to 30 frames per game-second (Time.captureFramerate), so however slowly a
// software renderer draws each frame, the footage plays back at true speed.
#if SLICEBLAST_SCREENSHOTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SliceBlast.Core;
using SliceBlast.Meta;
using UnityEngine;

namespace SliceBlast.Bootstrap
{
    public sealed partial class SliceBlastBootstrap
    {
        private const int CaptureWidth = 1284;
        private const int CaptureHeight = 2778;
        private const int CaptureFps = 30;
        private const int CaptureFrameLimit = 12000;

        private static string s_captureRoot;

        private readonly List<string> _stills = new List<string>();
        private string _clip;
        private int _clipFrame;
        private int _frame;
        private StreamWriter _captureLog;
        private BlockSlicer _slicer;
        private Texture2D _captureRgb;

        private PlacementEvent _lastPlacement;
        private int _lastPlacementFrame = -1000;
        private BlastEvent _lastBlast;
        private int _lastBlastFrame = -1000;
        private bool _captureRunOver;
        private bool _watching;
        private int _activeFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrepareCapture()
        {
            s_captureRoot = CaptureArgument("-captureDir") ?? Path.Combine(Application.persistentDataPath, "capture");
            Directory.CreateDirectory(s_captureRoot);

            Time.captureFramerate = CaptureFps;
            QualitySettings.vSyncCount = 0;
            UnityEngine.Random.InitState(20260928);
            Screen.SetResolution(CaptureWidth, CaptureHeight, FullScreenMode.Windowed);

            // A returning player's profile, so the Workshop and the Profile tab show what
            // they look like in use rather than on the very first launch.
            PlayerPrefs.DeleteAll();
            RunSnapshot.Clear();

            ProfileData data = PlayerProfile.Data;
            data.coins = 5840;
            data.bestScore = 640;
            data.totalRuns = 86;
            data.totalBlasts = 212;
            data.biggestBlast = 9;
            data.longestChain = 14;
            data.lifetimeScore = 41600;
            data.upgradeLevels = new[] { 3, 1, 2 };
            data.ownedThemes = new List<string> { "ember", "vapor", "sakura" };
            data.equippedTheme = ThemeCatalogue.DefaultId;
            data.dailyStreak = 6;
            data.lastPlayDay = PlayerProfile.TodayKey();
            data.sawFaultHint = true;
            data.language = (int)Language.English;
            data.soundOn = true;
            data.hapticsOn = true;
            PlayerProfile.MarkDirty();
        }

        private static string CaptureArgument(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == flag && !string.IsNullOrEmpty(args[i + 1]))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private IEnumerator RunStoreCapture()
        {
            _captureLog = new StreamWriter(Path.Combine(s_captureRoot, "events.txt"), false) { AutoFlush = true };
            _slicer = _flow.GetComponent<BlockSlicer>();

            GameEvents.BlockPlaced += CaptureOnPlaced;
            GameEvents.BlastFired += CaptureOnBlast;
            GameEvents.RunEnded += CaptureOnRunEnded;
            GameEvents.NeonCharged += CaptureOnNeon;
            GameEvents.CurrentPulsed += CaptureOnPulse;
            GameEvents.CoinsAwarded += CaptureOnCoins;

            StartCoroutine(CaptureRecorder());
            yield return null;
            yield return null;

            CaptureLog(string.Format(
                CultureInfo.InvariantCulture,
                "screen {0}x{1} dt {2:0.0000} udt {3:0.0000}",
                Screen.width, Screen.height, Time.deltaTime, Time.unscaledDeltaTime));

            int guard = 0;

            while (!_home && guard++ < 600)
            {
                yield return null;
            }

            // ---- 1. Title ----------------------------------------------------------------
            BeginClip("title");
            yield return Frames(84);
            Still("01_title");
            yield return Frames(6);
            EndClip();

            // ---- 2. A full run: build up, streaks, escalating blasts, specials, the end ----
            BeginClip("run1");
            BeginRun();
            _watching = true;
            StartCoroutine(StillWatcher("r1_"));
            // Slices build the tower up; the long unbroken stretch after drop 10 escalates the
            // blasts 3, 5, 7, 9, 11 and walks the sky through all three of its tiers.
            yield return PlayRun(i => i == 2 || i == 5 || i == 8 || i == 10 || i == 22 || i == 25 || i == 36, 40);
            yield return Frames(10);
            _watching = false;
            yield return MissUntilOver();

            guard = 0;

            while (!_captureRunOver && guard++ < 600)
            {
                yield return null;
            }

            yield return Frames(45);
            Still("06_runover_a");
            yield return Frames(30);
            Still("06_runover_b");
            yield return Frames(15);
            EndClip();

            // ---- 3. Workshop -------------------------------------------------------------
            PlayerProfile.EquipTheme("sakura");
            OnHomeRequested();
            yield return Frames(30);

            BeginClip("shop");
            OnShopRequested();
            yield return Frames(24);
            _shop.ShowTabForCapture(0);
            yield return Frames(36);
            Still("07_upgrades");
            _shop.ShowTabForCapture(1);
            yield return Frames(36);
            Still("08_themes");
            _shop.ShowTabForCapture(3);
            yield return Frames(36);
            Still("09_daily");
            _shop.ShowTabForCapture(2);
            yield return Frames(36);
            Still("10_profile");
            yield return Frames(10);
            OnShopClosed();
            yield return Frames(20);
            EndClip();

            // ---- 4. Settings and the three languages ---------------------------------------
            BeginClip("settings");
            OnSettingsRequested();
            yield return Frames(30);
            Still("11_settings_en");
            Loc.Set(Language.Turkish);
            yield return Frames(24);
            Still("11_settings_tr");
            Loc.Set(Language.Russian);
            yield return Frames(24);
            Still("11_settings_ru");
            OnSettingsClosed();
            yield return Frames(30);
            Still("12_title_ru");
            Loc.Set(Language.Turkish);
            yield return Frames(20);
            Still("12_title_tr");
            Loc.Set(Language.English);
            yield return Frames(20);
            Still("12_title_sakura");
            EndClip();

            // ---- 5. A second run in another theme, for variety in the videos ---------------
            BeginClip("run2");
            BeginRun();
            _watching = true;
            StartCoroutine(StillWatcher("r2_"));
            yield return PlayRun(i => i == 1 || i == 3 || i == 6 || i == 16 || i == 19, 30);
            yield return Frames(30);
            _watching = false;
            EndClip();

            CaptureLog("done");
            yield return Frames(3);
            File.WriteAllText(Path.Combine(s_captureRoot, "DONE"), "ok");
            _captureLog.Dispose();
            Application.Quit();
        }

        /// <summary>
        /// Drops each block exactly as its swing carries it over the target — the tower's
        /// centre for a perfect drop, a fifth of a block off it for a visible slice — so the
        /// footage never shows a block jump into place.
        /// </summary>
        private IEnumerator PlayRun(Func<int, bool> sliceAt, int drops)
        {
            int drop = 0;
            int idle = 0;
            MovingBlock current = null;
            float previous = 0f;
            bool hasPrevious = false;
            int frames = 0;

            while (drop < drops && _flow.IsRunning && _frame < CaptureFrameLimit && idle < 900)
            {
                MovingBlock moving = _flow.ActiveBlock;
                MovingBlock top = _flow.TopBlock;
                idle++;

                if (moving != current)
                {
                    current = moving;
                    hasPrevious = false;
                    frames = 0;
                }

                _activeFrames = frames;

                if (moving == null || top == null || !_flow.AcceptsInput)
                {
                    yield return null;
                    continue;
                }

                frames++;

                bool axisX = moving.MovingAxisX;
                Vector3 topPosition = top.CachedTransform.position;
                Vector3 topScale = top.CachedTransform.localScale;
                float topCenter = axisX ? topPosition.x : topPosition.z;
                float topSize = axisX ? topScale.x : topScale.z;

                bool slice = sliceAt(drop) && moving.Type == BlockType.Standard;
                float target = topCenter + (slice ? 0.2f * topSize * (drop % 2 == 0 ? 1f : -1f) : 0f);
                float position = moving.AxisCenter;

                if (hasPrevious && frames >= 10 && (target - previous) * (target - position) <= 0f)
                {
                    moving.SnapAxis(target);
                    _slicer.ScriptedDrop();
                    drop++;
                    idle = 0;
                    hasPrevious = false;
                    yield return null;
                    continue;
                }

                previous = position;
                hasPrevious = true;
                yield return null;
            }
        }

        /// <summary>Misses on purpose until the run ends — a shield or a glass block may soak up the first.</summary>
        private IEnumerator MissUntilOver()
        {
            int guard = 0;
            MovingBlock current = null;
            int frames = 0;

            while (_flow.IsRunning && guard++ < 2400)
            {
                MovingBlock moving = _flow.ActiveBlock;
                MovingBlock top = _flow.TopBlock;

                if (moving != current)
                {
                    current = moving;
                    frames = 0;
                }

                if (moving == null || top == null || !_flow.AcceptsInput)
                {
                    yield return null;
                    continue;
                }

                if (++frames < 14)
                {
                    yield return null;
                    continue;
                }

                bool axisX = moving.MovingAxisX;
                Vector3 topPosition = top.CachedTransform.position;
                Vector3 topScale = top.CachedTransform.localScale;
                float topCenter = axisX ? topPosition.x : topPosition.z;
                float topSize = axisX ? topScale.x : topScale.z;
                float side = moving.AxisCenter >= topCenter ? 1f : -1f;

                moving.SnapAxis(topCenter + side * (topSize * 0.5f + moving.AxisSize * 0.5f + 0.05f));
                _slicer.ScriptedDrop();
                yield return null;
            }
        }

        /// <summary>Takes candidate stills at the moments worth putting on the App Store page.</summary>
        private IEnumerator StillWatcher(string prefix)
        {
            int perfect = 0;
            int blast = 0;
            int electric = 0;
            int special = 0;
            int tower = 0;
            MovingBlock lastSpecial = null;

            while (_watching)
            {
                int sincePlaced = _frame - _lastPlacementFrame;
                int sinceBlast = _frame - _lastBlastFrame;
                MovingBlock active = _flow.ActiveBlock;

                if (perfect < 4 && _lastPlacement.Kind == PlacementKind.Perfect && _lastPlacement.Streak >= 2
                    && sincePlaced == 12 && sinceBlast > 40)
                {
                    Still(prefix + "perfect_" + perfect++);
                }

                if (blast < 12 && _lastBlast.Layers >= 5 && (sinceBlast == 4 || sinceBlast == 10 || sinceBlast == 18))
                {
                    Still(prefix + "blast" + _lastBlast.Layers + "_" + sinceBlast);
                    blast++;
                }

                if (electric < 4 && _lastPlacement.Kind == PlacementKind.Perfect && _lastPlacement.Type == BlockType.Electric
                    && (sincePlaced == 20 || sincePlaced == 60))
                {
                    Still(prefix + "electric_" + electric++);
                }

                if (special < 6 && active != null && active != lastSpecial && active.IsMoving
                    && BlockCatalogue.IsSpecial(active.Type) && _activeFrames == 16)
                {
                    lastSpecial = active;
                    Still(prefix + "special_" + active.Type + "_" + special++);
                }

                if (tower < 3 && active != null && _flow.StackHeight >= 11 + tower * 3 && _activeFrames == 18 && sinceBlast > 60)
                {
                    Still(prefix + "tower" + _flow.StackHeight + "_" + tower++);
                }

                yield return null;
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private void BeginClip(string name)
        {
            Directory.CreateDirectory(Path.Combine(s_captureRoot, "clips", name));
            _clip = name;
            _clipFrame = 0;
            CaptureLog("clip-begin");
        }

        private void EndClip()
        {
            CaptureLog("clip-end");
            _clip = null;
        }

        private void Still(string name)
        {
            _stills.Add(name);
            CaptureLog("still " + name);
        }

        private IEnumerator CaptureRecorder()
        {
            WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();

            while (true)
            {
                yield return endOfFrame;

                if (_clip != null || _stills.Count > 0)
                {
                    Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();

                    if (_captureRgb == null || _captureRgb.width != shot.width || _captureRgb.height != shot.height)
                    {
                        _captureRgb = new Texture2D(shot.width, shot.height, TextureFormat.RGB24, false);
                    }

                    _captureRgb.SetPixels32(shot.GetPixels32());
                    _captureRgb.Apply(false);
                    Destroy(shot);

                    if (_clip != null)
                    {
                        string path = Path.Combine(s_captureRoot, "clips", _clip, _clipFrame.ToString("D5", CultureInfo.InvariantCulture) + ".jpg");
                        File.WriteAllBytes(path, _captureRgb.EncodeToJPG(94));
                        _clipFrame++;
                    }

                    for (int i = 0; i < _stills.Count; i++)
                    {
                        Directory.CreateDirectory(Path.Combine(s_captureRoot, "stills"));
                        File.WriteAllBytes(Path.Combine(s_captureRoot, "stills", _stills[i] + ".png"), _captureRgb.EncodeToPNG());
                    }

                    _stills.Clear();
                }

                _frame++;

                if (_frame % 300 == 0)
                {
                    Debug.Log("[Capture] frame " + _frame + " clip " + (_clip ?? "-") + " stack " + (_flow != null ? _flow.StackHeight : 0));
                }

                if (_frame > CaptureFrameLimit)
                {
                    CaptureLog("frame limit reached");
                    File.WriteAllText(Path.Combine(s_captureRoot, "DONE"), "limit");
                    Application.Quit();
                }
            }
        }

        private void CaptureLog(string text)
        {
            if (_captureLog == null)
            {
                return;
            }

            _captureLog.WriteLine((_clip ?? "-") + " " + _clipFrame.ToString(CultureInfo.InvariantCulture) + " " + text);
        }

        private void CaptureOnPlaced(PlacementEvent placement)
        {
            _lastPlacement = placement;
            _lastPlacementFrame = _frame;
            CaptureLog("place " + placement.Kind + " " + placement.Type + " " + placement.Streak);
        }

        private void CaptureOnBlast(BlastEvent blast)
        {
            _lastBlast = blast;
            _lastBlastFrame = _frame;
            CaptureLog("blast " + blast.Layers + (blast.FromNeon ? " neon" : string.Empty));
        }

        private void CaptureOnRunEnded(int score, int best)
        {
            _captureRunOver = true;
            CaptureLog("runend " + score + " " + best);
        }

        private void CaptureOnNeon(Vector3 position, Color color)
        {
            CaptureLog("neon");
        }

        private void CaptureOnPulse(Vector3 position)
        {
            CaptureLog("pulse");
        }

        private void CaptureOnCoins(int amount, Vector3 position)
        {
            CaptureLog("coins " + amount);
        }
    }
}
#endif
