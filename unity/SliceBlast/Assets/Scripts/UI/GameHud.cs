using System;
using UnityEngine;
using UnityEngine.UI;

namespace SliceBlast.UI
{
    /// <summary>
    /// The whole interface, built in code: score, streak, multiplier, blast banner, screen
    /// flash, the pause/settings sheet and the full-screen end-of-run screen. Everything
    /// animates on unscaled time so pausing and the death slow-motion stay responsive.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameHud : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, 0.79f, 0.29f);
        private static readonly Color Mint = new Color(0.36f, 0.91f, 0.77f);
        private static readonly Color Ink = new Color(0.04f, 0.05f, 0.09f);
        private static readonly Color Panel = new Color(0.13f, 0.15f, 0.26f);

        public event Action PauseToggled;
        public event Action RestartRequested;
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
        private Text _soundLabel;
        private Text _hapticsLabel;
        private Image _flash;

        private CanvasGroup _gameOver;
        private CanvasGroup _pause;

        private float _scorePunch;
        private float _streakLife;
        private float _bannerLife;
        private float _flashLevel;
        private bool _hintVisible;
        private float _hintAlpha;
        private float _gameOverAlpha;
        private float _pauseAlpha;

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

            BuildPauseButton();
            BuildPauseSheet();
            BuildGameOverScreen();
        }

        private void BuildPauseButton()
        {
            Button button = CreateButton("PauseButton", _safeArea, "II", 62, new Color(1f, 1f, 1f, 0.14f), Color.white);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(120f, 120f);
            rect.anchoredPosition = new Vector2(-40f, -40f);

            button.onClick.AddListener(() => PauseToggled?.Invoke());
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

            Button resume = CreateButton("Resume", sheet, "RESUME", 60, Mint, Ink);
            PlaceMenuButton(resume, 240f);
            resume.onClick.AddListener(() => PauseToggled?.Invoke());

            Button restart = CreateButton("Restart", sheet, "RESTART", 60, Panel, Color.white);
            PlaceMenuButton(restart, 80f);
            restart.onClick.AddListener(() => RestartRequested?.Invoke());

            Button sound = CreateButton("Sound", sheet, "SOUND  ON", 52, Panel, Color.white);
            PlaceMenuButton(sound, -80f);
            _soundLabel = sound.GetComponentInChildren<Text>();
            sound.onClick.AddListener(() =>
            {
                _soundOn = !_soundOn;
                SetSoundLabel(_soundOn);
                SoundToggled?.Invoke(_soundOn);
            });

            Button haptics = CreateButton("Haptics", sheet, "HAPTICS  ON", 52, Panel, Color.white);
            PlaceMenuButton(haptics, -220f);
            _hapticsLabel = haptics.GetComponentInChildren<Text>();
            haptics.onClick.AddListener(() =>
            {
                _hapticsOn = !_hapticsOn;
                SetHapticsLabel(_hapticsOn);
                HapticsToggled?.Invoke(_hapticsOn);
            });

            Text credit = CreateText("Credit", sheet, 34, FontStyle.Normal, new Color(1f, 1f, 1f, 0.35f), TextAnchor.LowerCenter);
            Anchor(credit.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 60f), new Vector2(0f, 130f));
            credit.text = "SLICE BLAST";
        }

        private void BuildGameOverScreen()
        {
            RectTransform screen = CreateChild("GameOver", transform);
            Stretch(screen);

            _gameOver = screen.gameObject.AddComponent<CanvasGroup>();
            _gameOver.alpha = 0f;
            _gameOver.blocksRaycasts = false;
            _gameOver.interactable = false;

            // Full-bleed curtain: the run is over, nothing behind it competes for attention.
            Image dim = CreateImage("Dim", screen, new Color(Ink.r, Ink.g, Ink.b, 0.93f));
            Stretch(dim.rectTransform);

            Text title = CreateText("Title", screen, 92, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 300f), new Vector2(0f, 430f));
            title.text = "RUN OVER";

            _finalScore = CreateText("FinalScore", screen, 230, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Anchor(_finalScore.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 40f), new Vector2(0f, 290f));

            _bestScore = CreateText("BestScore", screen, 58, FontStyle.Bold, Gold, TextAnchor.MiddleCenter);
            Anchor(_bestScore.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -60f), new Vector2(0f, 30f));

            Button again = CreateButton("PlayAgain", screen, "PLAY AGAIN", 62, Mint, Ink);
            PlaceMenuButton(again, -240f);
            again.onClick.AddListener(() => RestartRequested?.Invoke());

            _restart = CreateText("RestartHint", screen, 44, FontStyle.Bold, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
            Anchor(_restart.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 180f), new Vector2(0f, 260f));
            _restart.text = "OR TAP ANYWHERE";
        }

        private void PlaceMenuButton(Button button, float y)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(680f, 130f);
            rect.anchoredPosition = new Vector2(0f, y);
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

        public void ShowGameOver(int score, int best)
        {
            if (_finalScore != null)
            {
                _finalScore.text = score.ToString();
            }

            if (_bestScore != null)
            {
                _bestScore.text = score >= best ? "NEW BEST!" : "BEST  " + best;
                _bestScore.color = score >= best ? Gold : new Color(1f, 1f, 1f, 0.6f);
            }

            _gameOverAlpha = 1f;

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
                _soundLabel.text = on ? "SOUND  ON" : "SOUND  OFF";
            }
        }

        public void SetHapticsLabel(bool on)
        {
            _hapticsOn = on;

            if (_hapticsLabel != null)
            {
                _hapticsLabel.text = on ? "HAPTICS  ON" : "HAPTICS  OFF";
            }
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

        private Text CreateText(string name, Transform parent, int size, FontStyle style, Color color, TextAnchor anchor)
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

            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(3f, -3f);

            return text;
        }

        private Button CreateButton(string name, Transform parent, string label, int fontSize, Color background, Color foreground)
        {
            RectTransform rect = CreateChild(name, parent);

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = background;
            image.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            Text text = CreateText(name + "Label", rect, fontSize, FontStyle.Bold, foreground, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);

            return button;
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
