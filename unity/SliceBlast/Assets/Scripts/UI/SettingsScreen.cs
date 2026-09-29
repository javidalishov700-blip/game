// A standalone settings sheet, reached from its own icon on the title screen rather than
// buried as a tab inside the Workshop — Sound, Haptics, Language, Privacy Policy and Terms of Use don't
// belong to the shop's economy, and a player looking for "settings" was never going to think
// to open the shop to find them.
//
// Built the same way as the shop and the leaderboard: a full-bleed dim behind a safe-area-
// inset content column, faded in and out through one CanvasGroup.
using System;
using SliceBlast.Ads;
using SliceBlast.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace SliceBlast.UI
{
    [DisallowMultipleComponent]
    public sealed class SettingsScreen : MonoBehaviour
    {
        private const string PrivacyUrl = "https://javidalishov700-blip.github.io/steady-site/sliceblast/legal/privacy.html";
        private const string TermsUrl = "https://javidalishov700-blip.github.io/steady-site/sliceblast/legal/terms.html";

        private const float Margin = 60f;
        private const float ToggleLabelWidth = 780f;
        private const int ToggleLabelSize = 46;

        public event Action Closed;
        public event Action<bool> SoundToggled;
        public event Action<bool> HapticsToggled;

        private Font _font;
        private CanvasGroup _group;
        private float _targetAlpha;

        private MenuControl _sound;
        private MenuControl _haptics;

        private sealed class LanguageTile
        {
            public Language Language;
            public Image Background;
            public Text Name;
            public Image Check;
        }

        private readonly LanguageTile[] _languages = new LanguageTile[Loc.LanguageCount];

        // Developer-only: seven quick taps on the title switch AdMob to Google's demo ads so
        // the rewarded continue can be tried in TestFlight. Nothing on screen invites the taps.
        private const int TestAdsTaps = 7;
        private int _titleTaps;
        private float _lastTitleTap;
        private Text _testAdsLabel;
        private Text _adStatus;

        public bool IsOpen { get; private set; }

        public void Build(Font font)
        {
            _font = font;

            RectTransform root = (RectTransform)transform;
            UiKit.Stretch(root);

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            Image dim = UiKit.CreateImage("Dim", root, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 1f));
            UiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true; // nothing behind settings is tappable while it is open

            // Everything below sits on this instead of directly on root: the dim above has to
            // reach every physical edge, but a control here must not, or it ends up under the
            // notch/Dynamic Island or the home indicator.
            RectTransform content = UiKit.CreateSafeAreaChild("SafeContent", root);

            BuildHeader(content);

            _sound = UiKit.CreateButton(_font, "SettingsSound", content, "-", ToggleLabelSize, UiKit.Panel, Color.white, IconShape.SoundOn);
            PlaceRow(_sound.Root, 300f, 136f);
            _sound.Button.onClick.AddListener(ToggleSound);

            _haptics = UiKit.CreateButton(_font, "SettingsHaptics", content, "-", ToggleLabelSize, UiKit.Panel, Color.white, IconShape.VibrateOn);
            PlaceRow(_haptics.Root, 456f, 136f);
            _haptics.Button.onClick.AddListener(ToggleHaptics);

            BuildLanguagePicker(content, 636f);

            MenuControl privacy = UiKit.CreateButton(_font, "SettingsPrivacy", content, "-", 38, new Color(1f, 1f, 1f, 0.08f), UiKit.Dim, IconShape.Lock);
            PlaceRow(privacy.Root, 1000f, 116f);
            UiKit.BindLabel(privacy, "legal.privacy", ToggleLabelWidth);
            privacy.Button.onClick.AddListener(() => Application.OpenURL(PrivacyUrl));

            MenuControl terms = UiKit.CreateButton(_font, "SettingsTerms", content, "-", 38, new Color(1f, 1f, 1f, 0.08f), UiKit.Dim, IconShape.Document);
            PlaceRow(terms.Root, 1136f, 116f);
            UiKit.BindLabel(terms, "legal.terms", ToggleLabelWidth);
            terms.Button.onClick.AddListener(() => Application.OpenURL(TermsUrl));
        }

        private void BuildHeader(RectTransform root)
        {
            Text title = UiKit.CreateText(_font, "SettingsTitle", root, 88, FontStyle.Bold, Color.white, TextAnchor.UpperCenter);
            UiKit.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -190f), new Vector2(0f, -80f));
            UiKit.Bind(title, "settings.title", 640f);

            title.raycastTarget = true;
            Button titleButton = title.gameObject.AddComponent<Button>();
            titleButton.transition = Selectable.Transition.None;
            titleButton.onClick.AddListener(OnTitleTapped);

            _testAdsLabel = UiKit.CreateText(_font, "TestAds", root, 30, FontStyle.Bold, UiKit.Mint, TextAnchor.UpperCenter);
            UiKit.Anchor(_testAdsLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -250f), new Vector2(0f, -200f));
            _testAdsLabel.text = "TEST ADS ON";
            _testAdsLabel.gameObject.SetActive(AdsManager.TestAds);

            // What the ad SDK is doing, stage by stage, while test ads are on — the only way to
            // see why an ad does not load without a Mac and Xcode's console.
            _adStatus = UiKit.CreateText(_font, "AdStatus", root, 26, FontStyle.Normal, UiKit.Dim, TextAnchor.UpperLeft);
            UiKit.Anchor(_adStatus.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(Margin, -1560f), new Vector2(-Margin, -1280f));
            _adStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            _adStatus.gameObject.SetActive(AdsManager.TestAds);

            MenuControl close = UiKit.CreateButton(_font, "Close", root, string.Empty, 0, new Color(1f, 1f, 1f, 0.14f), Color.white, IconShape.Close);
            RectTransform closeRect = close.Root;
            closeRect.anchorMin = new Vector2(0f, 1f);
            closeRect.anchorMax = new Vector2(0f, 1f);
            closeRect.pivot = new Vector2(0f, 1f);
            closeRect.sizeDelta = new Vector2(120f, 120f);
            closeRect.anchoredPosition = new Vector2(40f, -70f);
            close.Button.onClick.AddListener(() => Closed?.Invoke());
        }

        /// <summary>
        /// A captioned row of three flag tiles. Each tile names its language in that language,
        /// so a player who landed in the wrong one can still find their own.
        /// </summary>
        private void BuildLanguagePicker(RectTransform root, float top)
        {
            Image globe = UiKit.CreateImage("LanguageIcon", root, UiKit.Dim);
            globe.sprite = IconFactory.GetSprite(IconShape.Globe);
            globe.preserveAspect = true;
            RectTransform globeRect = globe.rectTransform;
            globeRect.anchorMin = new Vector2(0f, 1f);
            globeRect.anchorMax = new Vector2(0f, 1f);
            globeRect.pivot = new Vector2(0f, 0.5f);
            globeRect.sizeDelta = new Vector2(52f, 52f);
            globeRect.anchoredPosition = new Vector2(Margin + 6f, -top - 28f);

            Text caption = UiKit.CreateText(_font, "LanguageCaption", root, 38, FontStyle.Bold, UiKit.Dim, TextAnchor.MiddleLeft);
            UiKit.Anchor(caption.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(Margin + 76f, -top - 56f), new Vector2(-Margin, -top));
            UiKit.Bind(caption, "settings.language", 700f);

            const float gap = 24f;
            const float tileTop = 76f;
            const float tileHeight = 236f;

            for (int i = 0; i < _languages.Length; i++)
            {
                Language language = (Language)i;

                MenuControl control = UiKit.CreateButton(_font, "Language" + language, root, string.Empty, 0, UiKit.Panel, Color.white, IconShape.None);
                RectTransform rect = control.Root;

                // Three equal columns across the content width, whatever that width is.
                float from = i / (float)_languages.Length;
                float to = (i + 1) / (float)_languages.Length;
                rect.anchorMin = new Vector2(from, 1f);
                rect.anchorMax = new Vector2(to, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(i == 0 ? Margin : gap * 0.5f, -top - tileTop - tileHeight);
                rect.offsetMax = new Vector2(i == _languages.Length - 1 ? -Margin : -gap * 0.5f, -top - tileTop);

                Image flag = UiKit.CreateImage("Flag", rect, Color.white);
                flag.sprite = IconFactory.GetFlag(language);
                RectTransform flagRect = flag.rectTransform;
                flagRect.anchorMin = new Vector2(0.5f, 1f);
                flagRect.anchorMax = new Vector2(0.5f, 1f);
                flagRect.pivot = new Vector2(0.5f, 1f);
                flagRect.sizeDelta = new Vector2(132f, 88f);
                flagRect.anchoredPosition = new Vector2(0f, -34f);

                Text name = UiKit.CreateText(_font, "Name", rect, 34, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Anchor(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 24f), new Vector2(-10f, 90f));
                UiKit.SetText(name, Loc.NativeName(language), 34, 190f);

                Image check = UiKit.CreateImage("Check", rect, UiKit.Ink);
                check.sprite = IconFactory.GetSprite(IconShape.Check);
                RectTransform checkRect = check.rectTransform;
                checkRect.anchorMin = new Vector2(1f, 1f);
                checkRect.anchorMax = new Vector2(1f, 1f);
                checkRect.pivot = new Vector2(1f, 1f);
                checkRect.sizeDelta = new Vector2(40f, 40f);
                checkRect.anchoredPosition = new Vector2(-14f, -14f);

                _languages[i] = new LanguageTile
                {
                    Language = language,
                    Background = control.Button.targetGraphic as Image,
                    Name = name,
                    Check = check
                };

                control.Button.onClick.AddListener(() => ChooseLanguage(language));
            }
        }

        private void OnTitleTapped()
        {
            if (!AdsManager.TestAdsAllowed)
            {
                return;
            }

            _titleTaps = Time.unscaledTime - _lastTitleTap < 1.2f ? _titleTaps + 1 : 1;
            _lastTitleTap = Time.unscaledTime;

            if (_titleTaps < TestAdsTaps)
            {
                return;
            }

            _titleTaps = 0;
            AdsManager ads = AdsManager.Instance;

            if (ads == null)
            {
                return;
            }

            bool on = !AdsManager.TestAds;
            ads.SetTestAds(on);
            _testAdsLabel.gameObject.SetActive(on);
            _adStatus.gameObject.SetActive(on);
        }

        private void ChooseLanguage(Language language)
        {
            Loc.Set(language);
            Refresh();
        }

        /// <summary>A full-width row whose top edge sits <paramref name="top"/> below the safe area's top.</summary>
        private static void PlaceRow(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(Margin, -top - height);
            rect.offsetMax = new Vector2(-Margin, -top);
        }

        private void ToggleSound()
        {
            SoundToggled?.Invoke(!PlayerProfile.Data.soundOn);
            Refresh();
        }

        private void ToggleHaptics()
        {
            HapticsToggled?.Invoke(!PlayerProfile.Data.hapticsOn);
            Refresh();
        }

        /// <summary>Rewrites the toggle rows and the language picker from the profile — cheap, called on every open.</summary>
        public void Refresh()
        {
            if (_sound == null)
            {
                return;
            }

            bool soundOn = PlayerProfile.Data.soundOn;
            UiKit.SetText(_sound.Label, Loc.T(soundOn ? "hud.sound" : "hud.muted"), ToggleLabelSize, ToggleLabelWidth);

            if (_sound.Icon != null)
            {
                _sound.Icon.sprite = IconFactory.GetSprite(soundOn ? IconShape.SoundOn : IconShape.SoundOff);
            }

            bool hapticsOn = PlayerProfile.Data.hapticsOn;
            UiKit.SetText(_haptics.Label, Loc.T(hapticsOn ? "hud.vibration" : "hud.no_vibration"), ToggleLabelSize, ToggleLabelWidth);

            if (_haptics.Icon != null)
            {
                _haptics.Icon.sprite = IconFactory.GetSprite(hapticsOn ? IconShape.VibrateOn : IconShape.VibrateOff);
            }

            Language current = Loc.Current;

            for (int i = 0; i < _languages.Length; i++)
            {
                LanguageTile tile = _languages[i];

                if (tile == null)
                {
                    continue;
                }

                bool selected = tile.Language == current;

                if (tile.Background != null)
                {
                    tile.Background.color = selected ? UiKit.Mint : UiKit.Panel;
                }

                UiKit.SetLabelColor(tile.Name, selected ? UiKit.Ink : Color.white);
                tile.Check.gameObject.SetActive(selected);
            }
        }

        public void Show()
        {
            IsOpen = true;
            _targetAlpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            Refresh();
        }

        public void Hide()
        {
            IsOpen = false;
            _targetAlpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private void Update()
        {
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, Clock.UnscaledDelta * 6f);

            if (IsOpen && _adStatus != null && _adStatus.gameObject.activeSelf && AdsManager.Instance != null)
            {
                _adStatus.text = AdsManager.Instance.DebugStatus;
            }
        }
    }
}
