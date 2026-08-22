using UnityEngine;
using UnityEngine.UI;

namespace SliceBlast.UI
{
    /// <summary>
    /// The whole HUD, built in code: score, streak, multiplier, blast banner, screen flash
    /// and the end-of-run panel. Runs on unscaled time so the death slow-motion never
    /// stalls the interface.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameHud : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, 0.79f, 0.29f);
        private static readonly Color Mint = new Color(0.36f, 0.91f, 0.77f);
        private static readonly Color Ink = new Color(0.04f, 0.05f, 0.09f);

        private RectTransform _safeArea;
        private Rect _appliedSafeArea;

        private Text _score;
        private Text _multiplier;
        private Text _streak;
        private Text _hint;
        private Text _banner;
        private Text _finalScore;
        private Text _bestScore;
        private Text _restart;
        private Image _flash;
        private CanvasGroup _gameOver;

        private float _scorePunch;
        private float _streakLife;
        private float _bannerLife;
        private float _flashLevel;
        private bool _hintVisible;
        private float _hintAlpha;
        private float _gameOverAlpha;

        public void Build()
        {
            Font font = ResolveFont();

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Flash first so it sits behind the text; no GraphicRaycaster anywhere, so the
            // HUD never eats a tap and every touch reaches the game.
            _flash = CreateImage("Flash", transform, Color.clear);
            _flash.rectTransform.anchorMin = Vector2.zero;
            _flash.rectTransform.anchorMax = Vector2.one;
            _flash.rectTransform.offsetMin = Vector2.zero;
            _flash.rectTransform.offsetMax = Vector2.zero;

            _safeArea = CreateChild("SafeArea", transform);
            _safeArea.anchorMin = Vector2.zero;
            _safeArea.anchorMax = Vector2.one;
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;
            ApplySafeArea();

            _score = CreateText("Score", _safeArea, font, 150, FontStyle.Bold, Color.white, TextAnchor.UpperCenter);
            Anchor(_score.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -240f), new Vector2(0f, -60f));

            _multiplier = CreateText("Multiplier", _safeArea, font, 66, FontStyle.Bold, Gold, TextAnchor.UpperCenter);
            Anchor(_multiplier.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -330f), new Vector2(0f, -250f));
            _multiplier.text = string.Empty;

            _streak = CreateText("Streak", _safeArea, font, 56, FontStyle.Bold, Mint, TextAnchor.UpperCenter);
            Anchor(_streak.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -420f), new Vector2(0f, -340f));
            SetAlpha(_streak, 0f);

            _banner = CreateText("Banner", _safeArea, font, 130, FontStyle.Bold, Gold, TextAnchor.MiddleCenter);
            Anchor(_banner.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -110f), new Vector2(0f, 110f));
            SetAlpha(_banner, 0f);

            _hint = CreateText("Hint", _safeArea, font, 52, FontStyle.Bold, new Color(1f, 1f, 1f, 0.85f), TextAnchor.LowerCenter);
            Anchor(_hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 150f), new Vector2(0f, 250f));
            _hint.text = "TAP TO DROP";
            SetAlpha(_hint, 0f);

            BuildGameOverPanel(font);
        }

        private void BuildGameOverPanel(Font font)
        {
            RectTransform panel = CreateChild("GameOver", _safeArea);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;

            _gameOver = panel.gameObject.AddComponent<CanvasGroup>();
            _gameOver.alpha = 0f;
            _gameOver.blocksRaycasts = false;
            _gameOver.interactable = false;

            Image dim = CreateImage("Dim", panel, new Color(Ink.r, Ink.g, Ink.b, 0.78f));
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = Vector2.zero;
            dim.rectTransform.offsetMax = Vector2.zero;
            dim.raycastTarget = false;

            Text title = CreateText("Title", panel, font, 84, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 230f), new Vector2(0f, 350f));
            title.text = "RUN OVER";

            _finalScore = CreateText("FinalScore", panel, font, 190, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Anchor(_finalScore.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 20f), new Vector2(0f, 220f));

            _bestScore = CreateText("BestScore", panel, font, 56, FontStyle.Bold, Gold, TextAnchor.MiddleCenter);
            Anchor(_bestScore.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -80f), new Vector2(0f, 10f));

            _restart = CreateText("Restart", panel, font, 58, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Anchor(_restart.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -300f), new Vector2(0f, -200f));
            _restart.text = "TAP TO PLAY AGAIN";
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
            if (_multiplier == null)
            {
                return;
            }

            _multiplier.text = multiplier > 1 ? "COMBO x" + multiplier : string.Empty;
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

        public void ShowBanner(string text, Color color)
        {
            if (_banner == null)
            {
                return;
            }

            _banner.text = text;
            _banner.color = new Color(color.r, color.g, color.b, 1f);
            _bannerLife = 1f;
        }

        public void Flash(float strength)
        {
            _flashLevel = Mathf.Clamp01(_flashLevel + strength);
        }

        public void ShowHint(bool visible)
        {
            _hintVisible = visible;
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
        }

        public void HideGameOver()
        {
            _gameOverAlpha = 0f;
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
                SetAlpha(_banner, Mathf.SmoothStep(0f, 1f, _bannerLife));
                float grow = Mathf.Lerp(1.45f, 0.95f, Mathf.Clamp01((1f - _bannerLife) * 3.2f));
                _banner.rectTransform.localScale = new Vector3(grow, grow, 1f);
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
                    float pulse = 0.55f + Mathf.Sin(Time.unscaledTime * 3.1f) * 0.45f;
                    SetAlpha(_restart, pulse);
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

        private static Text CreateText(string name, Transform parent, Font font, int size, FontStyle style, Color color, TextAnchor anchor)
        {
            RectTransform rect = CreateChild(name, parent);

            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
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
            catch (System.Exception)
            {
                font = null;
            }

            if (font == null)
            {
                try
                {
                    font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch (System.Exception)
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
