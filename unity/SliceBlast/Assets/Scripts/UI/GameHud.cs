using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SliceBlast.UI
{
    /// <summary>
    /// The whole interface, built in code: score, streak, multiplier, blast banner, screen
    /// flash, the pause/settings sheet and the full-screen end-of-run screen. Every control
    /// carries a drawn icon rather than relying on a glyph, and everything animates on
    /// unscaled time so pausing and the death slow-motion stay responsive.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameHud : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, 0.79f, 0.29f);
        private static readonly Color Mint = new Color(0.36f, 0.91f, 0.77f);
        private static readonly Color Ink = new Color(0.04f, 0.05f, 0.09f);
        private static readonly Color Panel = new Color(0.16f, 0.18f, 0.3f);
        private static readonly Color ShieldBlue = new Color(0.72f, 0.95f, 1f);
        private static readonly Color BoltBlue = new Color(0.45f, 0.85f, 1f);

        // Reachable from the title screen because a store listing is not the only place a
        // player should be able to find them. Served straight off the repository through
        // jsDelivr's GitHub CDN mirror — no GitHub Pages toggle to flip, live the moment this
        // is built. The files themselves live under docs/ and already carry the eventual
        // https://javidalishov700-blip.github.io/game/... links for the day Pages is turned
        // on; only these two constants need to change when that happens.
        private const string PrivacyUrl = "https://cdn.jsdelivr.net/gh/javidalishov700-blip/game@main/docs/privacy-policy.html";
        private const string TermsUrl = "https://cdn.jsdelivr.net/gh/javidalishov700-blip/game@main/docs/terms-of-use.html";

        public event Action PauseToggled;
        public event Action RestartRequested;
        public event Action HomeRequested;
        public event Action<bool> SoundToggled;
        public event Action<bool> HapticsToggled;

        private RectTransform _safeArea;
        private Rect _appliedSafeArea;
        private Font _font;

        private Text _score;
        private Text _multiplier;
        private Text _streak;
        private Text _hint;
        private Text _banner;
        private Text _bannerBonus;
        private Text _finalScore;
        private Text _bestScore;
        private Text _restart;
        private Text _electricSeconds;
        private Text _soundLabel;
        private Text _hapticsLabel;

        private Image _flash;
        private Image _shieldIcon;
        private Image _electricIcon;
        private Image _soundIcon;
        private Image _hapticsIcon;
        private Image _homeSoundIcon;
        private Image _homeHapticsIcon;

        private CanvasGroup _gameOver;
        private CanvasGroup _pause;
        private CanvasGroup _home;
        private CanvasGroup _chrome;

        private RectTransform _titleSlice;
        private RectTransform _titleBlast;
        private RectTransform _titleCut;
        private Image _homeCrown;
        private Text _homeBest;
        private Text _homeStart;
        private Vector2 _titleSliceRest;
        private Vector2 _titleBlastRest;

        private float _scorePunch;
        private float _streakLife;
        private float _bannerLife;
        private float _flashLevel;
        private bool _hintVisible;
        private float _hintAlpha;
        private float _gameOverAlpha;
        private float _pauseAlpha;
        private float _homeAlpha;
        private float _chromeAlpha;
        private float _introTime;
        private bool _shieldActive;
        private bool _electricActive;

        private bool _soundOn = true;
        private bool _hapticsOn = true;

        public void Build()
        {
            _font = ResolveFont();

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Only the buttons opt into raycasting, so a tap anywhere else still drops a block.
            gameObject.AddComponent<GraphicRaycaster>();

            _flash = CreateImage("Flash", transform, Color.clear);
            Stretch(_flash.rectTransform);

            _safeArea = CreateChild("SafeArea", transform);
            Stretch(_safeArea);
            ApplySafeArea();

            // Everything that belongs to a run lives under one group, so the title screen
            // can hide the lot without touching a single element.
            _chrome = _safeArea.gameObject.AddComponent<CanvasGroup>();
            _chrome.alpha = 0f;

            _score = CreateText("Score", _safeArea, 150, FontStyle.Bold, Color.white, TextAnchor.UpperCenter);
            Anchor(_score.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -240f), new Vector2(0f, -60f));

            _multiplier = CreateText("Multiplier", _safeArea, 66, FontStyle.Bold, Gold, TextAnchor.UpperCenter);
            Anchor(_multiplier.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -330f), new Vector2(0f, -250f));
            _multiplier.text = string.Empty;

            _streak = CreateText("Streak", _safeArea, 56, FontStyle.Bold, Mint, TextAnchor.UpperCenter);
            Anchor(_streak.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -420f), new Vector2(0f, -340f));
            SetAlpha(_streak, 0f);

            _banner = CreateText("Banner", _safeArea, 130, FontStyle.Bold, Gold, TextAnchor.MiddleCenter);
            Anchor(_banner.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -110f), new Vector2(0f, 110f));
            SetAlpha(_banner, 0f);

            _bannerBonus = CreateText("BannerBonus", _safeArea, 78, FontStyle.Bold, Gold, TextAnchor.MiddleCenter);
            Anchor(_bannerBonus.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -220f), new Vector2(0f, -110f));
            SetAlpha(_bannerBonus, 0f);

            _hint = CreateText("Hint", _safeArea, 52, FontStyle.Bold, new Color(1f, 1f, 1f, 0.85f), TextAnchor.LowerCenter);
            Anchor(_hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 150f), new Vector2(0f, 250f));
            _hint.text = "TAP TO DROP";
            SetAlpha(_hint, 0f);

            BuildStatusBadges();
            BuildPauseButton();
            BuildGameOverScreen();
            BuildPauseSheet();
            BuildHomeScreen();
        }

        /// <summary>Shield and multiplier read as badges, not sentences.</summary>
        private void BuildStatusBadges()
        {
            _shieldIcon = CreateImage("ShieldIcon", _safeArea, ShieldBlue);
            _shieldIcon.sprite = IconFactory.GetSprite(IconShape.Shield);
            RectTransform shieldRect = _shieldIcon.rectTransform;
            shieldRect.anchorMin = new Vector2(0f, 1f);
            shieldRect.anchorMax = new Vector2(0f, 1f);
            shieldRect.pivot = new Vector2(0f, 1f);
            shieldRect.sizeDelta = new Vector2(84f, 84f);
            shieldRect.anchoredPosition = new Vector2(40f, -40f);
            SetAlpha(_shieldIcon, 0f);

            _electricIcon = CreateImage("ElectricIcon", _safeArea, BoltBlue);
            _electricIcon.sprite = IconFactory.GetSprite(IconShape.Bolt);
            RectTransform boltRect = _electricIcon.rectTransform;
            boltRect.anchorMin = new Vector2(0f, 1f);
            boltRect.anchorMax = new Vector2(0f, 1f);
            boltRect.pivot = new Vector2(0f, 1f);
            boltRect.sizeDelta = new Vector2(84f, 84f);
            boltRect.anchoredPosition = new Vector2(40f, -140f);
            SetAlpha(_electricIcon, 0f);

            _electricSeconds = CreateText("ElectricSeconds", _safeArea, 50, FontStyle.Bold, BoltBlue, TextAnchor.MiddleLeft);
            Anchor(_electricSeconds.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(128f, -224f), new Vector2(0f, -140f));
            _electricSeconds.text = string.Empty;
        }

        private void BuildPauseButton()
        {
            MenuControl control = CreateButton("PauseButton", _safeArea, string.Empty, 0, new Color(1f, 1f, 1f, 0.16f), Color.white, IconShape.Pause);
            RectTransform rect = control.Root;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(130f, 130f);
            rect.anchoredPosition = new Vector2(-40f, -40f);

            control.Button.onClick.AddListener(() => PauseToggled?.Invoke());
        }

        private void BuildPauseSheet()
        {
            RectTransform sheet = CreateChild("PauseSheet", transform);
            Stretch(sheet);

            _pause = sheet.gameObject.AddComponent<CanvasGroup>();
            _pause.alpha = 0f;
            _pause.blocksRaycasts = false;
            _pause.interactable = false;

            Image dim = CreateImage("Dim", sheet, new Color(Ink.r, Ink.g, Ink.b, 0.92f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true; // swallow taps while paused

            Text title = CreateText("Title", sheet, 96, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 420f), new Vector2(0f, 560f));
            title.text = "PAUSED";

            MenuControl resume = CreateButton("Resume", sheet, "RESUME", 58, Mint, Ink, IconShape.Play);
            PlaceMenuButton(resume, 240f);
            resume.Button.onClick.AddListener(() => PauseToggled?.Invoke());

            MenuControl restart = CreateButton("Restart", sheet, "REPLAY", 58, Panel, Color.white, IconShape.Replay);
            PlaceMenuButton(restart, 80f);
            restart.Button.onClick.AddListener(() => RestartRequested?.Invoke());

            MenuControl home = CreateButton("Home", sheet, "HOME", 58, Panel, Color.white, IconShape.Home);
            PlaceMenuButton(home, -80f);
            home.Button.onClick.AddListener(() => HomeRequested?.Invoke());

            MenuControl sound = CreateButton("Sound", sheet, "SOUND", 52, Panel, Color.white, IconShape.SoundOn);
            PlaceMenuButton(sound, -240f);
            _soundLabel = sound.Label;
            _soundIcon = sound.Icon;
            sound.Button.onClick.AddListener(() =>
            {
                SetSoundLabel(!_soundOn);
                SoundToggled?.Invoke(_soundOn);
            });

            MenuControl haptics = CreateButton("Haptics", sheet, "VIBRATION", 52, Panel, Color.white, IconShape.VibrateOn);
            PlaceMenuButton(haptics, -400f);
            _hapticsLabel = haptics.Label;
            _hapticsIcon = haptics.Icon;
            haptics.Button.onClick.AddListener(() =>
            {
                SetHapticsLabel(!_hapticsOn);
                HapticsToggled?.Invoke(_hapticsOn);
            });

            Text credit = CreateText("Credit", sheet, 34, FontStyle.Normal, new Color(1f, 1f, 1f, 0.35f), TextAnchor.LowerCenter);
            Anchor(credit.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 60f), new Vector2(0f, 130f));
            credit.text = "SLICE BLAST";
        }

        /// <summary>
        /// The title screen. The wordmark arrives in two halves that meet on a slice line —
        /// the game's whole idea in one gesture — and the platform stays visible behind it.
        /// </summary>
        private void BuildHomeScreen()
        {
            RectTransform screen = CreateChild("Home", transform);
            Stretch(screen);

            _home = screen.gameObject.AddComponent<CanvasGroup>();
            _home.alpha = 0f;
            _home.blocksRaycasts = false;
            _home.interactable = false;

            Image dim = CreateImage("Dim", screen, new Color(Ink.r, Ink.g, Ink.b, 0.55f));
            Stretch(dim.rectTransform);

            Text slice = CreateText("TitleSlice", screen, 175, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Anchor(slice.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 170f), new Vector2(0f, 380f));
            slice.text = "SLICE";
            _titleSlice = slice.rectTransform;

            Image cut = CreateImage("TitleCut", screen, Mint);
            Anchor(cut.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            cut.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            cut.rectTransform.sizeDelta = new Vector2(0f, 12f);
            cut.rectTransform.anchoredPosition = new Vector2(0f, 150f);
            _titleCut = cut.rectTransform;

            Text blast = CreateText("TitleBlast", screen, 175, FontStyle.Bold, Gold, TextAnchor.MiddleCenter);
            Anchor(blast.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -80f), new Vector2(0f, 130f));
            blast.text = "BLAST";
            _titleBlast = blast.rectTransform;

            _homeStart = CreateText("TapToStart", screen, 66, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Anchor(_homeStart.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -330f), new Vector2(0f, -230f));
            _homeStart.text = "TAP TO START";

            Image crown = CreateImage("HomeCrown", screen, Gold);
            crown.sprite = IconFactory.GetSprite(IconShape.Crown);
            crown.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            crown.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            crown.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            crown.rectTransform.sizeDelta = new Vector2(58f, 58f);
            crown.rectTransform.anchoredPosition = new Vector2(-130f, -450f);
            _homeCrown = crown;

            _homeBest = CreateText("HomeBest", screen, 54, FontStyle.Bold, Gold, TextAnchor.MiddleCenter);
            Anchor(_homeBest.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(60f, -490f), new Vector2(0f, -410f));

            MenuControl sound = CreateButton("HomeSound", screen, string.Empty, 0, new Color(1f, 1f, 1f, 0.16f), Color.white, IconShape.SoundOn);
            PlaceHomeToggle(sound, -110f);
            _homeSoundIcon = sound.Icon;
            sound.Button.onClick.AddListener(() =>
            {
                SetSoundLabel(!_soundOn);
                SoundToggled?.Invoke(_soundOn);
            });

            MenuControl haptics = CreateButton("HomeHaptics", screen, string.Empty, 0, new Color(1f, 1f, 1f, 0.16f), Color.white, IconShape.VibrateOn);
            PlaceHomeToggle(haptics, 110f);
            _homeHapticsIcon = haptics.Icon;
            haptics.Button.onClick.AddListener(() =>
            {
                SetHapticsLabel(!_hapticsOn);
                HapticsToggled?.Invoke(_hapticsOn);
            });

            CreateLink("Privacy", screen, "PRIVACY POLICY", -175f, PrivacyUrl);
            CreateLink("Terms", screen, "TERMS OF USE", 175f, TermsUrl);

            _titleSliceRest = _titleSlice.anchoredPosition;
            _titleBlastRest = _titleBlast.anchoredPosition;
        }

        /// <summary>
        /// A quiet text link along the bottom of the title screen. The tap target is a
        /// transparent image sized well past the words, so it is comfortable on a phone
        /// without the link itself shouting.
        /// </summary>
        private void CreateLink(string name, Transform parent, string label, float x, string url)
        {
            RectTransform rect = CreateChild(name, parent);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(330f, 90f);
            rect.anchoredPosition = new Vector2(x, 90f);

            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                Deselect();
                Application.OpenURL(url);
            });

            Text text = CreateText(name + "Label", parent, 34, FontStyle.Normal, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = rect.anchorMin;
            text.rectTransform.anchorMax = rect.anchorMax;
            text.rectTransform.pivot = rect.pivot;
            text.rectTransform.sizeDelta = rect.sizeDelta;
            text.rectTransform.anchoredPosition = rect.anchoredPosition;
            text.text = label;
        }

        private static void PlaceHomeToggle(MenuControl control, float x)
        {
            RectTransform rect = control.Root;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(140f, 140f);
            rect.anchoredPosition = new Vector2(x, 220f);
        }

        private void BuildGameOverScreen()
        {
            RectTransform screen = CreateChild("GameOver", transform);
            Stretch(screen);

            _gameOver = screen.gameObject.AddComponent<CanvasGroup>();
            _gameOver.alpha = 0f;
            _gameOver.blocksRaycasts = false;
            _gameOver.interactable = false;

            // Full-bleed, but see-through: the camera has just pulled back to show the
            // tower and the curtain must not be the thing hiding it.
            Image dim = CreateImage("Dim", screen, new Color(Ink.r, Ink.g, Ink.b, 0.42f));
            Stretch(dim.rectTransform);

            // The readable content sits on solid panels in the top and bottom thirds; the
            // middle band stays clear.
            Image header = CreateImage("HeaderPanel", screen, new Color(Ink.r, Ink.g, Ink.b, 0.88f));
            header.sprite = IconFactory.GetSprite(IconShape.Panel);
            header.type = Image.Type.Sliced;
            Anchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(60f, -760f), new Vector2(-60f, -200f));

            Text title = CreateText("Title", screen, 80, FontStyle.Bold, new Color(1f, 1f, 1f, 0.75f), TextAnchor.UpperCenter);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -360f), new Vector2(0f, -260f));
            title.text = "RUN OVER";

            _finalScore = CreateText("FinalScore", screen, 210, FontStyle.Bold, Color.white, TextAnchor.UpperCenter);
            Anchor(_finalScore.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -620f), new Vector2(0f, -370f));

            Image crown = CreateImage("Crown", screen, Gold);
            crown.sprite = IconFactory.GetSprite(IconShape.Crown);
            RectTransform crownRect = crown.rectTransform;
            crownRect.anchorMin = new Vector2(0.5f, 1f);
            crownRect.anchorMax = new Vector2(0.5f, 1f);
            crownRect.pivot = new Vector2(0.5f, 0.5f);
            crownRect.sizeDelta = new Vector2(62f, 62f);
            crownRect.anchoredPosition = new Vector2(-150f, -665f);

            _bestScore = CreateText("BestScore", screen, 54, FontStyle.Bold, Gold, TextAnchor.MiddleCenter);
            Anchor(_bestScore.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -700f), new Vector2(0f, -630f));

            MenuControl again = CreateButton("PlayAgain", screen, "PLAY AGAIN", 60, Mint, Ink, IconShape.Replay);
            RectTransform againRect = again.Root;
            againRect.anchorMin = new Vector2(0.5f, 0f);
            againRect.anchorMax = new Vector2(0.5f, 0f);
            againRect.pivot = new Vector2(0.5f, 0.5f);
            againRect.sizeDelta = new Vector2(700f, 132f);
            againRect.anchoredPosition = new Vector2(0f, 320f);
            again.Button.onClick.AddListener(() => RestartRequested?.Invoke());

            _restart = CreateText("RestartHint", screen, 44, FontStyle.Bold, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
            Anchor(_restart.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 170f), new Vector2(0f, 250f));
            _restart.text = "OR TAP ANYWHERE";
        }

        private static void PlaceMenuButton(MenuControl control, float y)
        {
            RectTransform rect = control.Root;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(700f, 132f);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        public void SetShield(bool active)
        {
            _shieldActive = active;
        }

        public void SetElectric(float remaining, float total)
        {
            _electricActive = remaining > 0f;

            if (_electricSeconds != null)
            {
                _electricSeconds.text = _electricActive ? "x2  " + Mathf.CeilToInt(remaining) : string.Empty;
            }
        }

        public void SetScore(int score)
        {
            if (_score == null)
            {
                return;
            }

            _score.text = score.ToString();
            _scorePunch = 1f;
        }

        public void SetMultiplier(int multiplier)
        {
            if (_multiplier != null)
            {
                _multiplier.text = multiplier > 1 ? "COMBO x" + multiplier : string.Empty;
            }
        }

        public void ShowStreak(int streak)
        {
            if (_streak == null || streak <= 0)
            {
                return;
            }

            _streak.text = "PERFECT x" + streak;
            _streakLife = 1f;
        }

        public void ShowBanner(string text, string subtitle, Color color)
        {
            if (_banner == null)
            {
                return;
            }

            _banner.text = text;
            _banner.color = new Color(color.r, color.g, color.b, 1f);
            _bannerLife = 1f;

            if (_bannerBonus != null)
            {
                _bannerBonus.text = subtitle;
                _bannerBonus.color = new Color(color.r, color.g, color.b, 1f);
            }
        }

        public void Flash(float strength)
        {
            _flashLevel = Mathf.Clamp01(_flashLevel + strength);
        }

        public void ShowHint(bool visible)
        {
            _hintVisible = visible;
        }

        public void SetHintText(string text)
        {
            if (_hint != null)
            {
                _hint.text = text;
            }
        }

        public void ShowHome(int best)
        {
            _homeAlpha = 1f;
            _chromeAlpha = 0f;
            _introTime = 0f;

            if (_home != null)
            {
                _home.blocksRaycasts = true;
                _home.interactable = true;
            }

            bool hasBest = best > 0;

            if (_homeBest != null)
            {
                _homeBest.text = hasBest ? best.ToString() : string.Empty;
            }

            if (_homeCrown != null && _homeCrown.gameObject.activeSelf != hasBest)
            {
                _homeCrown.gameObject.SetActive(hasBest);
            }
        }

        public void HideHome()
        {
            _homeAlpha = 0f;

            if (_home != null)
            {
                _home.blocksRaycasts = false;
                _home.interactable = false;
            }
        }

        /// <summary>Score, combo and the pause button only belong to a live run.</summary>
        public void ShowRunChrome(bool visible)
        {
            _chromeAlpha = visible ? 1f : 0f;
        }

        public void ShowGameOver(int score, int best)
        {
            if (_finalScore != null)
            {
                _finalScore.text = score.ToString();
            }

            if (_bestScore != null)
            {
                _bestScore.text = score >= best ? "NEW BEST!" : best.ToString();
                _bestScore.color = score >= best ? Gold : new Color(1f, 1f, 1f, 0.6f);
            }

            _gameOverAlpha = 1f;
            _chromeAlpha = 0f;

            if (_gameOver != null)
            {
                _gameOver.blocksRaycasts = true;
                _gameOver.interactable = true;
            }
        }

        public void HideGameOver()
        {
            _gameOverAlpha = 0f;

            if (_gameOver != null)
            {
                _gameOver.blocksRaycasts = false;
                _gameOver.interactable = false;
            }
        }

        public void ShowPaused(bool paused)
        {
            _pauseAlpha = paused ? 1f : 0f;

            if (_pause != null)
            {
                _pause.blocksRaycasts = paused;
                _pause.interactable = paused;
            }
        }

        public void SetSoundLabel(bool on)
        {
            _soundOn = on;

            if (_soundLabel != null)
            {
                _soundLabel.text = on ? "SOUND" : "MUTED";
            }

            ApplyToggleIcon(_soundIcon, on, IconShape.SoundOn, IconShape.SoundOff);
            ApplyToggleIcon(_homeSoundIcon, on, IconShape.SoundOn, IconShape.SoundOff);
        }

        public void SetHapticsLabel(bool on)
        {
            _hapticsOn = on;

            if (_hapticsLabel != null)
            {
                _hapticsLabel.text = on ? "VIBRATION" : "NO VIBRATION";
            }

            ApplyToggleIcon(_hapticsIcon, on, IconShape.VibrateOn, IconShape.VibrateOff);
            ApplyToggleIcon(_homeHapticsIcon, on, IconShape.VibrateOn, IconShape.VibrateOff);
        }

        private static void ApplyToggleIcon(Image target, bool on, IconShape onShape, IconShape offShape)
        {
            if (target == null)
            {
                return;
            }

            target.sprite = IconFactory.GetSprite(on ? onShape : offShape);
            target.color = on ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_safeArea != null && _appliedSafeArea != Screen.safeArea)
            {
                ApplySafeArea();
            }

            if (_score != null)
            {
                _scorePunch = Mathf.Max(0f, _scorePunch - dt * 4.5f);
                float punch = 1f + Mathf.Sin(_scorePunch * Mathf.PI) * 0.18f;
                _score.rectTransform.localScale = new Vector3(punch, punch, 1f);
            }

            if (_streak != null)
            {
                _streakLife = Mathf.Max(0f, _streakLife - dt * 1.15f);
                SetAlpha(_streak, Mathf.SmoothStep(0f, 1f, _streakLife));
                float pop = 1f + Mathf.Sin(Mathf.Clamp01((1f - _streakLife) * 4f) * Mathf.PI) * 0.25f;
                _streak.rectTransform.localScale = new Vector3(pop, pop, 1f);
            }

            if (_banner != null)
            {
                _bannerLife = Mathf.Max(0f, _bannerLife - dt * 1.05f);
                float bannerAlpha = Mathf.SmoothStep(0f, 1f, _bannerLife);
                SetAlpha(_banner, bannerAlpha);

                float grow = Mathf.Lerp(1.45f, 0.95f, Mathf.Clamp01((1f - _bannerLife) * 3.2f));
                _banner.rectTransform.localScale = new Vector3(grow, grow, 1f);

                if (_bannerBonus != null)
                {
                    SetAlpha(_bannerBonus, bannerAlpha);
                    float bonusPop = Mathf.Lerp(1.3f, 1f, Mathf.Clamp01((1f - _bannerLife) * 4f));
                    _bannerBonus.rectTransform.localScale = new Vector3(bonusPop, bonusPop, 1f);
                }
            }

            if (_flash != null)
            {
                _flashLevel = Mathf.Max(0f, _flashLevel - dt * 3.2f);
                _flash.color = new Color(1f, 0.96f, 0.85f, _flashLevel * 0.55f);
            }

            if (_hint != null)
            {
                float target = _hintVisible ? 1f : 0f;
                _hintAlpha = Mathf.MoveTowards(_hintAlpha, target, dt * 2.5f);
                float pulse = 0.65f + Mathf.Sin(Time.unscaledTime * 3.4f) * 0.35f;
                SetAlpha(_hint, _hintAlpha * pulse);
            }

            TickBadges(dt);

            if (_gameOver != null)
            {
                _gameOver.alpha = Mathf.MoveTowards(_gameOver.alpha, _gameOverAlpha, dt * 3.5f);

                if (_restart != null && _gameOver.alpha > 0.01f)
                {
                    float pulse = 0.45f + Mathf.Sin(Time.unscaledTime * 3.1f) * 0.35f;
                    SetAlpha(_restart, pulse);
                }
            }

            if (_pause != null)
            {
                _pause.alpha = Mathf.MoveTowards(_pause.alpha, _pauseAlpha, dt * 6f);
            }

            if (_chrome != null)
            {
                _chrome.alpha = Mathf.MoveTowards(_chrome.alpha, _chromeAlpha, dt * 4f);
                _chrome.blocksRaycasts = _chromeAlpha > 0.5f;
            }

            if (_home != null)
            {
                _home.alpha = Mathf.MoveTowards(_home.alpha, _homeAlpha, dt * 4.5f);

                if (_home.alpha > 0.001f)
                {
                    TickIntro(dt);
                }
            }
        }

        /// <summary>
        /// The intro: the two halves of the wordmark fly in from opposite sides and land on
        /// a slice line that wipes open between them.
        /// </summary>
        private void TickIntro(float dt)
        {
            _introTime += dt;

            float slice = EaseOut(Mathf.Clamp01(_introTime / 0.5f));
            float blast = EaseOut(Mathf.Clamp01((_introTime - 0.12f) / 0.5f));
            float cut = EaseOut(Mathf.Clamp01((_introTime - 0.32f) / 0.4f));
            float tail = Mathf.Clamp01((_introTime - 0.6f) / 0.35f);

            if (_titleSlice != null)
            {
                _titleSlice.anchoredPosition = new Vector2(_titleSliceRest.x - (1f - slice) * 1200f, _titleSliceRest.y);
                float pop = 1f + Mathf.Sin(slice * Mathf.PI) * 0.07f;
                _titleSlice.localScale = new Vector3(pop, pop, 1f);
            }

            if (_titleBlast != null)
            {
                _titleBlast.anchoredPosition = new Vector2(_titleBlastRest.x + (1f - blast) * 1200f, _titleBlastRest.y);
                float pop = 1f + Mathf.Sin(blast * Mathf.PI) * 0.07f;
                _titleBlast.localScale = new Vector3(pop, pop, 1f);
            }

            if (_titleCut != null)
            {
                _titleCut.sizeDelta = new Vector2(cut * 780f, 12f);
            }

            if (_homeStart != null)
            {
                float pulse = 0.55f + Mathf.Sin(Time.unscaledTime * 3.2f) * 0.45f;
                SetAlpha(_homeStart, tail * pulse);
            }

            if (_homeBest != null)
            {
                SetAlpha(_homeBest, tail);
            }

            if (_homeCrown != null)
            {
                SetAlpha(_homeCrown, tail);
            }
        }

        private static float EaseOut(float t)
        {
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        private void TickBadges(float dt)
        {
            if (_shieldIcon != null)
            {
                float alpha = Mathf.MoveTowards(_shieldIcon.color.a, _shieldActive ? 1f : 0f, dt * 5f);
                SetAlpha(_shieldIcon, alpha);

                if (alpha > 0.01f)
                {
                    float breathe = 1f + Mathf.Sin(Time.unscaledTime * 3f) * 0.07f;
                    _shieldIcon.rectTransform.localScale = new Vector3(breathe, breathe, 1f);
                }
            }

            if (_electricIcon != null)
            {
                float alpha = Mathf.MoveTowards(_electricIcon.color.a, _electricActive ? 1f : 0f, dt * 5f);
                SetAlpha(_electricIcon, alpha);

                if (alpha > 0.01f)
                {
                    float jolt = 1f + Mathf.Sin(Time.unscaledTime * 14f) * 0.06f;
                    _electricIcon.rectTransform.localScale = new Vector3(jolt, jolt, 1f);
                }
            }
        }

        private void ApplySafeArea()
        {
            _appliedSafeArea = Screen.safeArea;

            Vector2 min = _appliedSafeArea.position;
            Vector2 max = _appliedSafeArea.position + _appliedSafeArea.size;

            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            if (float.IsNaN(min.x) || float.IsNaN(min.y) || float.IsNaN(max.x) || float.IsNaN(max.y))
            {
                return;
            }

            _safeArea.anchorMin = min;
            _safeArea.anchorMax = max;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }

        private static RectTransform CreateChild(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateChild(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(string name, Transform parent, int size, FontStyle style, Color color, TextAnchor anchor, bool outlined = false)
        {
            RectTransform rect = CreateChild(name, parent);

            Text text = rect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            // A word sitting on a coloured panel needs a rim, not a drop shadow, to survive
            // whatever the panel behind it happens to be.
            if (outlined)
            {
                Outline outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
                outline.effectDistance = new Vector2(2.5f, -2.5f);
            }
            else
            {
                Shadow shadow = rect.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
                shadow.effectDistance = new Vector2(3f, -3f);
            }

            return text;
        }

        /// <summary>
        /// A menu control. The tap target, the glyph and the word are three separate objects
        /// rather than one nested stack, and that is the whole point: a uGUI
        /// <see cref="Selectable"/> tints its target graphic through the CanvasRenderer, and a
        /// CanvasRenderer's colour multiplies into every graphic *below* it. Parent the label
        /// to the button and the button's own state — pressed, and above all disabled, which
        /// is what a non-interactable CanvasGroup forces — silently drains the label's alpha.
        /// Kept as siblings, the icon and the word are immune to it.
        /// </summary>
        private sealed class MenuControl
        {
            public RectTransform Root;
            public Button Button;
            public Image Icon;
            public Text Label;
        }

        private MenuControl CreateButton(string name, Transform parent, string label, int fontSize, Color background, Color foreground, IconShape icon)
        {
            MenuControl control = new MenuControl();

            RectTransform root = CreateChild(name, parent);
            control.Root = root;

            // The tap target: a rounded panel with nothing underneath it to tint.
            RectTransform hit = CreateChild("Hit", root);
            Stretch(hit);

            Image image = hit.gameObject.AddComponent<Image>();
            image.sprite = IconFactory.GetSprite(IconShape.Panel);
            image.type = Image.Type.Sliced;
            image.color = background;
            image.raycastTarget = true;

            Button button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.74f, 0.74f, 0.74f, 1f);
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            control.Button = button;

            bool hasLabel = !string.IsNullOrEmpty(label);

            if (icon != IconShape.None)
            {
                Image glyph = CreateImage("Icon", root, foreground);
                glyph.sprite = IconFactory.GetSprite(icon);
                glyph.preserveAspect = true;

                RectTransform glyphRect = glyph.rectTransform;
                glyphRect.anchorMin = new Vector2(hasLabel ? 0f : 0.5f, 0.5f);
                glyphRect.anchorMax = glyphRect.anchorMin;
                glyphRect.pivot = new Vector2(0.5f, 0.5f);
                glyphRect.sizeDelta = hasLabel ? new Vector2(74f, 74f) : new Vector2(62f, 62f);
                glyphRect.anchoredPosition = hasLabel ? new Vector2(92f, 0f) : Vector2.zero;

                control.Icon = glyph;
            }

            if (hasLabel)
            {
                Text text = CreateText(name + "Label", root, fontSize, FontStyle.Bold, foreground, TextAnchor.MiddleCenter, true);
                Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(160f, 0f), new Vector2(-40f, 0f));
                control.Label = text;
            }

            // Leaving a control selected keeps it highlighted for the rest of the run.
            button.onClick.AddListener(Deselect);

            return control;
        }

        private static void Deselect()
        {
            EventSystem events = EventSystem.current;

            if (events != null)
            {
                events.SetSelectedGameObject(null);
            }
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static Font ResolveFont()
        {
            Font font = null;

            try
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (Exception)
            {
                font = null;
            }

            if (font == null)
            {
                try
                {
                    font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch (Exception)
                {
                    font = null;
                }
            }

            if (font == null)
            {
                string[] installed = Font.GetOSInstalledFontNames();

                if (installed != null && installed.Length > 0)
                {
                    font = Font.CreateDynamicFontFromOSFont(installed[0], 48);
                }
            }

            return font;
        }
    }
}
