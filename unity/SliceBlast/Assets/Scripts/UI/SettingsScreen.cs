// A standalone settings sheet, reached from its own icon on the title screen rather than
// buried as a tab inside the Workshop — Sound, Haptics, Privacy Policy and Terms of Use don't
// belong to the shop's economy, and a player looking for "settings" was never going to think
// to open the shop to find them.
//
// Built the same way as the shop and the leaderboard: a full-bleed dim behind a safe-area-
// inset content column, faded in and out through one CanvasGroup.
using System;
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

        private const float RowHeight = 140f;
        private const float RowGap = 20f;

        public event Action Closed;
        public event Action<bool> SoundToggled;
        public event Action<bool> HapticsToggled;

        private Font _font;
        private CanvasGroup _group;
        private float _targetAlpha;

        private MenuControl _sound;
        private MenuControl _haptics;

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

            _sound = UiKit.CreateButton(_font, "SettingsSound", content, "SOUND", 46, UiKit.Panel, Color.white, IconShape.SoundOn);
            PlaceRow(_sound.Root, 0);
            _sound.Button.onClick.AddListener(ToggleSound);

            _haptics = UiKit.CreateButton(_font, "SettingsHaptics", content, "VIBRATION", 46, UiKit.Panel, Color.white, IconShape.VibrateOn);
            PlaceRow(_haptics.Root, 1);
            _haptics.Button.onClick.AddListener(ToggleHaptics);

            MenuControl privacy = UiKit.CreateButton(_font, "SettingsPrivacy", content, "PRIVACY POLICY", 38, new Color(1f, 1f, 1f, 0.08f), UiKit.Dim, IconShape.None);
            PlaceRow(privacy.Root, 2);
            privacy.Button.onClick.AddListener(() => Application.OpenURL(PrivacyUrl));

            MenuControl terms = UiKit.CreateButton(_font, "SettingsTerms", content, "TERMS OF USE", 38, new Color(1f, 1f, 1f, 0.08f), UiKit.Dim, IconShape.None);
            PlaceRow(terms.Root, 3);
            terms.Button.onClick.AddListener(() => Application.OpenURL(TermsUrl));
        }

        private void BuildHeader(RectTransform root)
        {
            Text title = UiKit.CreateText(_font, "SettingsTitle", root, 88, FontStyle.Bold, Color.white, TextAnchor.UpperCenter);
            UiKit.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -190f), new Vector2(0f, -80f));
            title.text = "SETTINGS";

            MenuControl close = UiKit.CreateButton(_font, "Close", root, string.Empty, 0, new Color(1f, 1f, 1f, 0.14f), Color.white, IconShape.Close);
            RectTransform closeRect = close.Root;
            closeRect.anchorMin = new Vector2(0f, 1f);
            closeRect.anchorMax = new Vector2(0f, 1f);
            closeRect.pivot = new Vector2(0f, 1f);
            closeRect.sizeDelta = new Vector2(120f, 120f);
            closeRect.anchoredPosition = new Vector2(40f, -70f);
            close.Button.onClick.AddListener(() => Closed?.Invoke());
        }

        private static void PlaceRow(RectTransform rect, int index)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(60f, -320f - (index + 1) * RowHeight - index * RowGap);
            rect.offsetMax = new Vector2(-60f, -320f - index * (RowHeight + RowGap));
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

        /// <summary>Rewrites the two toggle rows from the profile — cheap, called on every open.</summary>
        public void Refresh()
        {
            bool soundOn = PlayerProfile.Data.soundOn;

            if (_sound.Label != null)
            {
                _sound.Label.text = soundOn ? "SOUND" : "MUTED";
            }

            if (_sound.Icon != null)
            {
                _sound.Icon.sprite = IconFactory.GetSprite(soundOn ? IconShape.SoundOn : IconShape.SoundOff);
            }

            bool hapticsOn = PlayerProfile.Data.hapticsOn;

            if (_haptics.Label != null)
            {
                _haptics.Label.text = hapticsOn ? "VIBRATION" : "NO VIBRATION";
            }

            if (_haptics.Icon != null)
            {
                _haptics.Icon.sprite = IconFactory.GetSprite(hapticsOn ? IconShape.VibrateOn : IconShape.VibrateOff);
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
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, Time.unscaledDeltaTime * 6f);
        }
    }
}
