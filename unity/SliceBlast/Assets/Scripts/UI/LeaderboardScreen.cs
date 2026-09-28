// The all-time board, drawn by the game rather than handed off to Game Center's own sheet:
// the top 99 with gold, silver and bronze medals, and the player's own standing pinned
// underneath wherever it is — "99+" once it falls off the list.
//
// Like the shop it owns no platform logic. The bootstrap asks Leaderboards for a page and
// hands the answer back through Complete; this file only draws. Every row is built once, on
// first open, and rewritten in place after that.
using System;
using System.Globalization;
using SliceBlast.Meta;
using SliceBlast.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace SliceBlast.UI
{
    [DisallowMultipleComponent]
    public sealed class LeaderboardScreen : MonoBehaviour
    {
        /// <summary>How many places the board lists; anything past this reads "99+".</summary>
        public const int RowCount = 99;

        private const float RowHeight = 128f;
        private const float RowGap = 12f;
        private const int MaxNameLength = 18;

        // Game Center can simply never answer. Past this the screen says so instead of
        // spinning for as long as the player is patient.
        private const float LoadTimeout = 15f;

        private static readonly Color Silver = new Color(0.8f, 0.84f, 0.9f);
        private static readonly Color Bronze = new Color(0.87f, 0.56f, 0.33f);
        private static readonly Color RowFill = new Color(0.16f, 0.18f, 0.3f, 0.85f);
        private static readonly Color LocalFill = new Color(0.2f, 0.42f, 0.4f, 0.95f);

        private sealed class Row
        {
            public RectTransform Root;
            public Image Fill;
            public Image Medal;
            public Text Rank;
            public Text Name;
            public Text Score;
        }

        public event Action Closed;

        private Font _font;
        private CanvasGroup _group;
        private float _targetAlpha;

        private ScrollRect _scroll;
        private RectTransform _content;
        private Row[] _rows;
        private Row _you;
        private Text _youCaption;
        private Text _status;

        // The gold medal's shine: a glow behind it that breathes, and a bar of light that
        // sweeps across the face every couple of seconds.
        private Image _goldGlow;
        private RectTransform _goldSheen;

        private int _token;
        private bool _loading;
        private float _loadStarted;

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

            // Fully opaque: at 0.96 the title screen's wordmark was still faintly visible
            // behind every row, competing with the board's own text.
            Image dim = UiKit.CreateImage("Dim", root, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 1f));
            UiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true; // nothing behind the board is tappable while it is open

            // Everything below sits on this instead of directly on root: the dim above has to
            // reach every physical edge, but a control here must not, or it ends up under the
            // notch/Dynamic Island or the home indicator.
            RectTransform safeContent = UiKit.CreateSafeAreaChild("SafeContent", root);

            BuildHeader(safeContent);
            BuildList(safeContent);
            BuildYouRow(safeContent);

            _status = UiKit.CreateText(_font, "Status", safeContent, 44, FontStyle.Bold, UiKit.Dim, TextAnchor.MiddleCenter);
            UiKit.Anchor(_status.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(60f, -120f), new Vector2(-60f, 120f));
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Nothing to say and no standing to show until the first answer arrives.
            HideStatus();
            _you.Root.gameObject.SetActive(false);
            _youCaption.gameObject.SetActive(false);
        }

        private void BuildHeader(RectTransform root)
        {
            Text title = UiKit.CreateText(_font, "BoardTitle", root, 88, FontStyle.Bold, Color.white, TextAnchor.UpperCenter);
            UiKit.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -190f), new Vector2(0f, -80f));
            UiKit.Bind(title, "board.title", 640f);

            Text subtitle = UiKit.CreateText(_font, "BoardSubtitle", root, 36, FontStyle.Bold, UiKit.Gold, TextAnchor.UpperCenter);
            UiKit.Anchor(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -250f), new Vector2(0f, -200f));
            UiKit.Bind(subtitle, "board.subtitle", 900f);

            MenuControl close = UiKit.CreateButton(_font, "Close", root, string.Empty, 0, new Color(1f, 1f, 1f, 0.14f), Color.white, IconShape.Close);
            RectTransform closeRect = close.Root;
            closeRect.anchorMin = new Vector2(0f, 1f);
            closeRect.anchorMax = new Vector2(0f, 1f);
            closeRect.pivot = new Vector2(0f, 1f);
            closeRect.sizeDelta = new Vector2(120f, 120f);
            closeRect.anchoredPosition = new Vector2(40f, -70f);
            close.Button.onClick.AddListener(() => Closed?.Invoke());
        }

        private void BuildList(RectTransform root)
        {
            RectTransform viewport = UiKit.CreateChild("Viewport", root);
            UiKit.Anchor(viewport, Vector2.zero, Vector2.one, new Vector2(40f, 260f), new Vector2(-40f, -290f));
            viewport.gameObject.AddComponent<RectMask2D>();

            // Invisible, but a raycast target: rows are not, so this is what a drag lands on.
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;

            _content = UiKit.CreateChild("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;

            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.inertia = true;
            _scroll.decelerationRate = 0.12f;
            _scroll.scrollSensitivity = 30f;

            _rows = new Row[RowCount];

            for (int i = 0; i < RowCount; i++)
            {
                Row row = CreateRow(_content, "Row" + (i + 1));
                row.Root.anchorMin = new Vector2(0f, 1f);
                row.Root.anchorMax = new Vector2(1f, 1f);
                row.Root.pivot = new Vector2(0.5f, 1f);
                row.Root.sizeDelta = new Vector2(0f, RowHeight);
                row.Root.anchoredPosition = new Vector2(0f, -i * (RowHeight + RowGap));
                row.Root.gameObject.SetActive(false);
                _rows[i] = row;
            }

            BuildGoldShine(_rows[0]);
        }

        private void BuildYouRow(RectTransform root)
        {
            _youCaption = UiKit.CreateText(_font, "YouCaption", root, 32, FontStyle.Bold, UiKit.Mint, TextAnchor.LowerLeft);
            UiKit.Anchor(_youCaption.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(64f, 214f), new Vector2(-40f, 256f));
            UiKit.Bind(_youCaption, "board.your_rank");

            _you = CreateRow(root, "You");
            _you.Root.anchorMin = new Vector2(0f, 0f);
            _you.Root.anchorMax = new Vector2(1f, 0f);
            _you.Root.pivot = new Vector2(0.5f, 0f);
            _you.Root.offsetMin = new Vector2(40f, 70f);
            _you.Root.offsetMax = new Vector2(-40f, 70f + RowHeight + 10f);
        }

        private Row CreateRow(Transform parent, string name)
        {
            Row row = new Row();
            row.Root = UiKit.CreateChild(name, parent);

            row.Fill = UiKit.CreatePanel("Fill", row.Root, RowFill);
            UiKit.Stretch(row.Fill.rectTransform);

            row.Medal = UiKit.CreateImage("Medal", row.Root, UiKit.Gold);
            row.Medal.sprite = IconFactory.GetSprite(IconShape.Disc);
            RectTransform medal = row.Medal.rectTransform;
            medal.anchorMin = new Vector2(0f, 0.5f);
            medal.anchorMax = new Vector2(0f, 0.5f);
            medal.pivot = new Vector2(0.5f, 0.5f);
            medal.sizeDelta = new Vector2(96f, 96f);
            medal.anchoredPosition = new Vector2(84f, 0f);

            // Over the medal rather than inside it, so the number stays on top of the sheen.
            row.Rank = UiKit.CreateText(_font, "Rank", row.Root, 46, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            RectTransform rank = row.Rank.rectTransform;
            rank.anchorMin = new Vector2(0f, 0.5f);
            rank.anchorMax = new Vector2(0f, 0.5f);
            rank.pivot = new Vector2(0.5f, 0.5f);
            rank.sizeDelta = new Vector2(150f, 96f);
            rank.anchoredPosition = new Vector2(84f, 0f);

            row.Name = UiKit.CreateText(_font, "Name", row.Root, 44, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            UiKit.Anchor(row.Name.rectTransform, Vector2.zero, Vector2.one, new Vector2(160f, 0f), new Vector2(-300f, 0f));
            row.Name.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.Name.verticalOverflow = VerticalWrapMode.Truncate;

            row.Score = UiKit.CreateText(_font, "Score", row.Root, 48, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            UiKit.Anchor(row.Score.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-300f, 0f), new Vector2(-36f, 0f));

            return row;
        }

        /// <summary>
        /// Gives the first row's medal its shine. The sheen is clipped to the disc by a Mask on
        /// the medal itself, so the bar of light never spills past the coin's edge.
        /// </summary>
        private void BuildGoldShine(Row first)
        {
            _goldGlow = UiKit.CreateImage("GoldGlow", first.Root, UiKit.Gold);
            _goldGlow.sprite = IconFactory.GetSprite(IconShape.Glow);
            RectTransform glow = _goldGlow.rectTransform;
            glow.anchorMin = new Vector2(0f, 0.5f);
            glow.anchorMax = new Vector2(0f, 0.5f);
            glow.pivot = new Vector2(0.5f, 0.5f);
            glow.sizeDelta = new Vector2(190f, 190f);
            glow.anchoredPosition = new Vector2(84f, 0f);
            glow.SetSiblingIndex(first.Medal.transform.GetSiblingIndex());

            Mask mask = first.Medal.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            Image sheen = UiKit.CreateImage("Sheen", first.Medal.transform, new Color(1f, 1f, 1f, 0.7f));
            _goldSheen = sheen.rectTransform;
            _goldSheen.anchorMin = new Vector2(0.5f, 0.5f);
            _goldSheen.anchorMax = new Vector2(0.5f, 0.5f);
            _goldSheen.pivot = new Vector2(0.5f, 0.5f);
            _goldSheen.sizeDelta = new Vector2(26f, 180f);
            _goldSheen.localRotation = Quaternion.Euler(0f, 0f, -24f);
            _goldSheen.anchoredPosition = new Vector2(-200f, 0f);
        }

        public void Show()
        {
            IsOpen = true;
            _targetAlpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            _scroll.verticalNormalizedPosition = 1f;
        }

        public void Hide()
        {
            IsOpen = false;
            _targetAlpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        /// <summary>
        /// Puts the board into its loading state and returns a token for Complete. An answer
        /// carrying an older token is from a load the player has since walked away from.
        /// </summary>
        public int BeginLoad()
        {
            _token++;
            _loading = true;
            _loadStarted = Clock.Unscaled;

            // The previous answer stays on screen while the next one loads, when there is
            // one; the status line only covers an empty board.
            if (!_rows[0].Root.gameObject.activeSelf)
            {
                ShowStatus(Loc.T("board.loading"));
            }

            return _token;
        }

        public void Complete(int token, LeaderboardPage page)
        {
            if (token != _token || !_loading)
            {
                return;
            }

            _loading = false;

            if (page == null)
            {
                ClearRows();
                ShowStatus(Loc.T("board.error"));
                return;
            }

            int shown = Mathf.Min(page.Top != null ? page.Top.Length : 0, RowCount);

            for (int i = 0; i < RowCount; i++)
            {
                if (i < shown)
                {
                    LeaderboardEntry entry = page.Top[i];
                    Fill(_rows[i], entry.Rank, entry.Name, entry.Score, entry.IsLocalPlayer);
                    _rows[i].Root.gameObject.SetActive(true);
                }
                else
                {
                    _rows[i].Root.gameObject.SetActive(false);
                }
            }

            _content.sizeDelta = new Vector2(0f, shown * (RowHeight + RowGap));

            if (shown == 0)
            {
                ShowStatus(Loc.T("board.empty"));
            }
            else
            {
                HideStatus();
            }

            if (page.HasLocalScore)
            {
                Fill(_you, page.LocalRank, page.LocalName, page.LocalScore, true);
            }
            else
            {
                Fill(_you, 0, page.LocalName, 0, true);
            }

            _you.Root.gameObject.SetActive(true);
            _youCaption.gameObject.SetActive(true);
        }

        private void Fill(Row row, int rank, string name, long score, bool isLocal)
        {
            Color medal = MedalColor(rank);
            bool hasMedal = medal.a > 0f;

            row.Medal.enabled = hasMedal;
            row.Medal.color = medal;

            if (_goldGlow != null && row == _rows[0])
            {
                _goldGlow.gameObject.SetActive(rank == 1);
            }

            // Past the list the exact number stops mattering to anyone but the player, and
            // three digits no longer fit the badge anyway.
            row.Rank.text = rank <= 0 ? "—" : rank > RowCount ? "99+" : rank.ToString(CultureInfo.InvariantCulture);
            row.Rank.fontSize = rank > RowCount ? 38 : 46;
            row.Rank.color = hasMedal ? UiKit.Ink : Color.white;
            SetShadow(row.Rank, !hasMedal);

            row.Name.text = Shorten(name);
            row.Name.color = rank == 1 ? UiKit.Gold : Color.white;

            row.Score.text = rank <= 0 ? "—" : score.ToString("N0", CultureInfo.InvariantCulture);
            row.Score.color = hasMedal ? medal : Color.white;

            row.Fill.color = isLocal ? LocalFill : RowFill;
        }

        private static Color MedalColor(int rank)
        {
            switch (rank)
            {
                case 1:
                    return UiKit.Gold;
                case 2:
                    return Silver;
                case 3:
                    return Bronze;
                default:
                    return Color.clear;
            }
        }

        // Dark digits on a bright medal read cleanly without the drop shadow every other label
        // carries; with it they look smudged.
        private static void SetShadow(Text text, bool on)
        {
            Shadow shadow = text.GetComponent<Shadow>();

            if (shadow != null)
            {
                shadow.enabled = on;
            }
        }

        private static string Shorten(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Loc.T("board.player");
            }

            return name.Length <= MaxNameLength ? name : name.Substring(0, MaxNameLength - 1) + "…";
        }

        private void ClearRows()
        {
            for (int i = 0; i < RowCount; i++)
            {
                _rows[i].Root.gameObject.SetActive(false);
            }

            _content.sizeDelta = Vector2.zero;
            _you.Root.gameObject.SetActive(false);
            _youCaption.gameObject.SetActive(false);
        }

        private void ShowStatus(string text)
        {
            _status.text = text;
            _status.gameObject.SetActive(true);
        }

        private void HideStatus()
        {
            _status.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_group == null)
            {
                return;
            }

            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, Clock.UnscaledDelta * 6f);

            if (_group.alpha <= 0f)
            {
                return;
            }

            if (_loading && Clock.Unscaled - _loadStarted > LoadTimeout)
            {
                Complete(_token, null);
            }

            AnimateGold(Clock.Unscaled);
        }

        private void AnimateGold(float time)
        {
            if (_goldGlow == null || !_goldGlow.gameObject.activeInHierarchy)
            {
                return;
            }

            // A slow breath: 1.6 seconds from dim to bright and back.
            float breath = 0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 2f / 1.6f);
            UiKit.SetAlpha(_goldGlow, Mathf.Lerp(0.3f, 0.85f, breath));
            float scale = Mathf.Lerp(0.92f, 1.12f, breath);
            _goldGlow.rectTransform.localScale = new Vector3(scale, scale, 1f);

            // The sweep takes 0.55 seconds of every 2.4, and waits off the coin the rest of
            // the time — a glint, not a strobe.
            const float Period = 2.4f;
            const float Sweep = 0.55f;
            float phase = Mathf.Repeat(time, Period);
            float x = phase < Sweep ? Mathf.Lerp(-80f, 80f, phase / Sweep) : -200f;
            _goldSheen.anchoredPosition = new Vector2(x, 0f);
        }
    }
}
