// The Workshop: upgrades, a gallery of themes, the player's profile and the daily missions,
// with the store rows along the bottom. Built once in code like the rest of the interface,
// then refreshed in place — nothing here is created or destroyed while it is open, so opening
// it mid-session costs one layout pass and no GC.
//
// It owns no store logic. Buying a real product raises an event the bootstrap wires to the
// IAP manager, exactly as the run-over screen raises ContinueRequested rather than talking to
// the ad SDK itself. Coin purchases are local, so those it does resolve directly.
using System;
using System.Globalization;
using SliceBlast.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace SliceBlast.UI
{
    [DisallowMultipleComponent]
    public sealed class ShopScreen : MonoBehaviour
    {
        private enum Tab : byte
        {
            Upgrades = 0,
            Themes = 1,
            Profile = 2,
            Missions = 3
        }

        /// <summary>A coloured disc with a glyph on it and a soft glow behind: a card's identity mark.</summary>
        private sealed class Badge
        {
            public Image Glow;
            public Image Disc;
            public Image Glyph;
        }

        private sealed class UpgradeRow
        {
            public UpgradeId Id;
            public Color Accent;
            public Badge Badge;
            public Text Effect;
            public Image[] Pips;
            public GameObject MaxStar;
            public MenuControl Buy;
        }

        private sealed class ThemeTile
        {
            public string Id;
            public RectTransform Root;
            public GameObject Border;
            public GameObject Lock;
            public GameObject Check;
            public Text Name;
            public MenuControl Button;
        }

        private sealed class MissionRow
        {
            public RectTransform Card;
            public Badge Badge;
            public Text Label;
            public Text Progress;
            public Image Fill;
            public MenuControl Claim;
        }

        // Layout, in safe-area canvas units. The content column sits between the tab strip and
        // either the store rows or, when there is no store, the bottom of the screen.
        private const float ContentTop = -404f;
        private const float FooterReserve = 366f;
        private const float NoFooterReserve = 40f;

        private const float PageInset = 8f;
        private const float CardGap = 18f;
        private const float UpgradeCardHeight = 214f;
        private const float MissionCardHeight = 204f;
        private const float RankCardHeight = 280f;
        private const float StatTileHeight = 164f;

        private const int ThemeColumns = 3;
        private const float TileHeight = 358f;
        private const float TileGap = 16f;
        private const float PreviewHeight = 200f;
        private const int PreviewBlocks = 7;
        private const float PreviewBlockHeight = 17f;

        // BlockSpawner's own step, doubled per preview block so seven blocks show the whole band.
        private const float PreviewHueStep = 0.035f;

        private const float ChipWidth = 300f;
        private const float ChipHeight = 66f;
        private const float ChipY = -238f;
        private const float ChipOffset = 165f;

        private const int StarCount = 18;

        private static readonly Color CardFill = new Color(1f, 1f, 1f, 0.07f);

        // Opaque, unlike CardFill: an equipped tile's highlight border sits behind it and must
        // not show through the whole tile.
        private static readonly Color TileFill = new Color(0.11f, 0.12f, 0.19f, 1f);
        private static readonly Color ChipFill = new Color(1f, 1f, 1f, 0.09f);
        private static readonly Color TrackFill = new Color(1f, 1f, 1f, 0.13f);
        private static readonly Color MutedFill = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color MutedText = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color EmptyPip = new Color(1f, 1f, 1f, 0.16f);
        private static readonly Color DimGold = new Color(1f, 0.79f, 0.29f, 0.7f);
        private static readonly Color BackdropTop = new Color(0.12f, 0.1f, 0.24f, 1f);
        private static readonly Color BackdropBottom = new Color(0.04f, 0.05f, 0.09f, 1f);

        private static readonly Color Coral = new Color(1f, 0.45f, 0.42f);
        private static readonly Color Sky = new Color(0.45f, 0.8f, 1f);
        private static readonly Color Leaf = new Color(0.48f, 0.92f, 0.52f);
        private static readonly Color Ember = new Color(1f, 0.6f, 0.25f);
        private static readonly Color Violet = new Color(0.72f, 0.56f, 1f);
        private static readonly Color Bronze = new Color(0.87f, 0.56f, 0.33f);
        private static readonly Color Silver = new Color(0.8f, 0.84f, 0.9f);

        // One per PlayerRanks title, climbing from dull bronze to gold.
        private static readonly Color[] RankColors =
        {
            new Color(0.72f, 0.62f, 0.52f),
            Bronze,
            Silver,
            UiKit.Mint,
            Sky,
            Violet,
            Coral,
            UiKit.Gold
        };

        /// <summary>
        /// Product identifiers, which have to match App Store Connect exactly. Kept here
        /// beside the amounts they grant so the two can never drift apart; IapManager reads
        /// both rather than restating them.
        /// </summary>
        public static readonly string[] CoinPackIds =
        {
            "com.javidalishov.sliceblast.coins.small",
            "com.javidalishov.sliceblast.coins.medium",
            "com.javidalishov.sliceblast.coins.large"
        };

        public static readonly int[] CoinPackAmounts = { 1200, 4000, 12000 };

        public event Action Closed;
        public event Action RemoveAdsRequested;
        public event Action RestoreRequested;
        public event Action<string> CoinPackRequested;

        /// <summary>Fired when something was actually bought, so the caller can play a sound.</summary>
        public event Action<bool> PurchaseResolved;

        private Font _font;
        private CanvasGroup _group;
        private float _targetAlpha;

        private RectTransform _content;
        private MenuControl[] _tabs;
        private RectTransform[] _pages;
        private ScrollRect[] _scrolls;
        private Tab _tab = Tab.Upgrades;

        private Image _headerGlow;
        private Color _glowTint = Color.white;
        private Image[] _stars;
        private float[] _starPhase;
        private float[] _starSpeed;
        private Color _starTint = Color.white;

        private RectTransform _coinChip;
        private Text _coinLabel;
        private RectTransform _streakChip;
        private Text _streakLabel;
        private GameObject _missionBadge;
        private Text _missionBadgeLabel;

        private UpgradeRow[] _upgradeRows;
        private ThemeTile[] _themeTiles;
        private MissionRow[] _missionRows;
        private Text _resetLabel;

        private Image _rankGlow;
        private Color _rankGlowTint = Color.white;
        private Image _rankMedal;
        private Text _rankTitle;
        private Text _rankNext;
        private Text _rankProgress;
        private Image _rankFill;
        private Text[] _stats;

        private MenuControl _removeAds;
        private MenuControl _restore;
        private RectTransform[] _coinPacks;
        private Text[] _coinPackLabels;
        private bool _storeAvailable;

        private RectTransform _punchTarget;
        private float _punch;
        private float _punchAmount;
        private float _coinPunch;
        private int _shownCoins = -1;
        private float _countdown;

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

            BuildBackdrop(root);

            // Everything below sits on this instead of directly on root: the backdrop has to
            // reach every physical edge, but a control here must not, or it ends up under the
            // notch/Dynamic Island or the home indicator.
            RectTransform safeContent = UiKit.CreateSafeAreaChild("SafeContent", root);

            BuildHeader(safeContent);
            BuildTabs(safeContent);

            _content = UiKit.CreateChild("Content", safeContent);
            UiKit.Anchor(_content, Vector2.zero, Vector2.one, new Vector2(40f, FooterReserve), new Vector2(-40f, ContentTop));

            _pages = new RectTransform[4];
            _scrolls = new ScrollRect[4];

            _pages[(int)Tab.Upgrades] = BuildUpgradesPage();
            _pages[(int)Tab.Themes] = BuildThemesPage();
            _pages[(int)Tab.Profile] = BuildProfilePage();
            _pages[(int)Tab.Missions] = BuildMissionsPage();

            BuildFooter(safeContent);
            SelectTab(Tab.Upgrades);
        }

        // ---- Frame -----------------------------------------------------------------------

        private void BuildBackdrop(RectTransform root)
        {
            // Fully opaque: nothing of the title screen behind should compete with the shop's
            // own text. A deep violet fading down to the game's ink, rather than one flat fill.
            Image backdrop = UiKit.CreateImage("Backdrop", root, Color.white);
            UiKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true; // nothing behind the shop is tappable while it is open
            backdrop.gameObject.AddComponent<UiGradient>().SetColors(BackdropTop, BackdropBottom);

            // A few stars twinkling around the header, in the equipped theme's own star colour.
            // Kept to the top of the screen, clear of the cards, where they read as sky rather
            // than as noise behind the text.
            _stars = new Image[StarCount];
            _starPhase = new float[StarCount];
            _starSpeed = new float[StarCount];

            for (int i = 0; i < StarCount; i++)
            {
                Image star = UiKit.CreateImage("Star" + i, root, Color.white);
                star.sprite = IconFactory.GetSprite(IconShape.Disc);

                Vector2 at = new Vector2(
                    Mathf.Lerp(0.04f, 0.96f, Hash(i * 12.9898f + 1.3f)),
                    Mathf.Lerp(0.8f, 0.985f, Hash(i * 78.233f + 4.1f)));
                float size = Mathf.Lerp(4f, 10f, Hash(i * 5.31f + 0.7f));
                Place(star.rectTransform, at, new Vector2(size, size), Vector2.zero);

                _stars[i] = star;
                _starPhase[i] = Hash(i * 3.7f + 2.2f) * Mathf.PI * 2f;
                _starSpeed[i] = Mathf.Lerp(0.8f, 2.4f, Hash(i * 9.13f + 5.5f));
            }
        }

        private void BuildHeader(RectTransform root)
        {
            // A soft wash of the equipped theme's accent behind the title, breathing slowly.
            _headerGlow = UiKit.CreateImage("HeaderGlow", root, Color.white);
            _headerGlow.sprite = IconFactory.GetSprite(IconShape.Glow);
            Place(_headerGlow.rectTransform, new Vector2(0.5f, 1f), new Vector2(820f, 300f), new Vector2(0f, -128f));

            Text title = UiKit.CreateText(_font, "ShopTitle", root, 84, FontStyle.Bold, Color.white, TextAnchor.UpperCenter);
            UiKit.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -178f), new Vector2(0f, -76f));
            UiKit.Bind(title, "hud.workshop", 640f);

            MenuControl close = UiKit.CreateButton(_font, "Close", root, string.Empty, 0, new Color(1f, 1f, 1f, 0.14f), Color.white, IconShape.Close);
            RectTransform closeRect = close.Root;
            closeRect.anchorMin = new Vector2(0f, 1f);
            closeRect.anchorMax = new Vector2(0f, 1f);
            closeRect.pivot = new Vector2(0f, 1f);
            closeRect.sizeDelta = new Vector2(120f, 120f);
            closeRect.anchoredPosition = new Vector2(40f, -70f);
            close.Button.onClick.AddListener(() => Closed?.Invoke());

            // The balance and the streak as two chips under the title, rather than a number
            // squeezed into the title's own row where a five-digit balance ran into the word.
            _coinChip = CreateChip("CoinChip", root, IconShape.Coin, UiKit.Gold, UiKit.Gold, out _coinLabel);
            _streakChip = CreateChip("StreakChip", root, IconShape.Flame, Ember, Ember, out _streakLabel);
        }

        private RectTransform CreateChip(string name, RectTransform parent, IconShape icon, Color iconColor, Color textColor, out Text label)
        {
            Image chip = UiKit.CreatePanel(name, parent, ChipFill);
            RectTransform rect = chip.rectTransform;
            Place(rect, new Vector2(0.5f, 1f), new Vector2(ChipWidth, ChipHeight), new Vector2(0f, ChipY));

            RectTransform row = CreateCentredRow(name + "Row", rect, 12f);

            Image glyph = UiKit.CreateImage("Icon", row, iconColor);
            glyph.sprite = IconFactory.GetSprite(icon);
            glyph.preserveAspect = true;

            LayoutElement glyphSize = glyph.gameObject.AddComponent<LayoutElement>();
            glyphSize.preferredWidth = 42f;
            glyphSize.preferredHeight = 42f;

            label = UiKit.CreateText(_font, name + "Label", row, 38, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
            return rect;
        }

        private void BuildTabs(RectTransform root)
        {
            RectTransform strip = UiKit.CreateChild("Tabs", root);
            UiKit.Anchor(strip, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -384f), new Vector2(-40f, -292f));

            HorizontalLayoutGroup layout = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            string[] labels = { "shop.tab.upgrades", "shop.tab.themes", "shop.tab.profile", "shop.tab.daily" };
            _tabs = new MenuControl[labels.Length];

            for (int i = 0; i < labels.Length; i++)
            {
                // No icon: a glyph crowds a 30pt word in a quarter-width button, so tabs have
                // never shown one.
                MenuControl tab = UiKit.CreateButton(_font, "Tab" + i, strip, "-", 30, UiKit.Panel, Color.white, IconShape.None);
                UiKit.BindLabel(tab, labels[i], 200f);

                // Equal widths whatever the word: nothing on a tab's root reports a size of its
                // own, so the strip shares its width out by these weights alone — and the strip
                // is inset evenly from both edges, where the old one sat 46 in on the left and 6
                // on the right.
                LayoutElement weight = tab.Root.gameObject.AddComponent<LayoutElement>();
                weight.minWidth = 0f;
                weight.preferredWidth = 0f;
                weight.flexibleWidth = 1f;

                Tab which = (Tab)i;
                tab.Button.onClick.AddListener(() => SelectTab(which));
                _tabs[i] = tab;
            }

            // Counts finished, unclaimed missions, riding the DAILY tab's own corner so it stays
            // on that tab however the strip is laid out.
            Image dot = UiKit.CreateImage("MissionBadge", _tabs[(int)Tab.Missions].Root, UiKit.Gold);
            dot.sprite = IconFactory.GetSprite(IconShape.Disc);
            Place(dot.rectTransform, new Vector2(1f, 1f), new Vector2(46f, 46f), new Vector2(-12f, -8f));

            _missionBadgeLabel = UiKit.CreateText(_font, "Count", dot.rectTransform, 28, FontStyle.Bold, UiKit.Ink, TextAnchor.MiddleCenter);
            UiKit.Stretch(_missionBadgeLabel.rectTransform);
            DisableShadow(_missionBadgeLabel);

            _missionBadge = dot.gameObject;
            _missionBadge.SetActive(false);
        }

        /// <summary>
        /// One scrolling page. Every page scrolls, whether or not today's content needs it: a
        /// shorter screen (or a longer list later) must never push the last card under the store
        /// rows. The page itself is the viewport and clips what scrolls past its edges.
        /// </summary>
        private RectTransform CreatePage(Tab tab, string name, out RectTransform scroll)
        {
            RectTransform page = UiKit.CreateChild(name, _content);
            UiKit.Stretch(page);
            page.gameObject.AddComponent<RectMask2D>();

            // Invisible, but a raycast target: the gaps between cards are what a drag lands on.
            Image catcher = page.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;

            scroll = UiKit.CreateChild("Scroll", page);
            scroll.anchorMin = new Vector2(0f, 1f);
            scroll.anchorMax = new Vector2(1f, 1f);
            scroll.pivot = new Vector2(0.5f, 1f);
            scroll.offsetMin = Vector2.zero;
            scroll.offsetMax = Vector2.zero;

            ScrollRect scroller = page.gameObject.AddComponent<ScrollRect>();
            scroller.viewport = page;
            scroller.content = scroll;
            scroller.horizontal = false;
            scroller.vertical = true;
            scroller.movementType = ScrollRect.MovementType.Elastic;
            scroller.inertia = true;
            scroller.decelerationRate = 0.12f;
            scroller.scrollSensitivity = 30f;

            _scrolls[(int)tab] = scroller;
            return page;
        }

        private static void SetScrollHeight(RectTransform scroll, float height)
        {
            scroll.sizeDelta = new Vector2(0f, height);
        }

        // ---- Upgrades ----------------------------------------------------------------------

        private RectTransform BuildUpgradesPage()
        {
            RectTransform page = CreatePage(Tab.Upgrades, "Upgrades", out RectTransform scroll);

            int count = UpgradeCatalogue.Count;
            _upgradeRows = new UpgradeRow[count];
            float top = PageInset;

            for (int i = 0; i < count; i++)
            {
                UpgradeDefinition definition = UpgradeCatalogue.At(i);
                UpgradeRow row = new UpgradeRow { Id = definition.Id, Accent = UpgradeAccent(definition.Id) };

                RectTransform card = CreateCard(scroll, "Upgrade" + i, top, UpgradeCardHeight);
                row.Badge = CreateBadge(card, UpgradeIcon(definition.Id), row.Accent, 112f, 92f);

                // A gold star on the badge's rim once the track is complete.
                Image star = UiKit.CreateImage("MaxStar", row.Badge.Disc.rectTransform, UiKit.Gold);
                star.sprite = IconFactory.GetSprite(IconShape.Star);
                Place(star.rectTransform, new Vector2(1f, 1f), new Vector2(46f, 46f), new Vector2(-10f, -10f));
                row.MaxStar = star.gameObject;

                Text name = UiKit.CreateText(_font, "Name", card, 46, FontStyle.Bold, Color.white, TextAnchor.UpperLeft);
                UiKit.Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(174f, -76f), new Vector2(-290f, -22f));
                UiKit.Bind(name, "upgrade." + definition.Key, 560f);

                Text detail = UiKit.CreateText(_font, "Detail", card, 30, FontStyle.Normal, UiKit.Dim, TextAnchor.UpperLeft);
                UiKit.Anchor(detail.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(174f, -116f), new Vector2(-290f, -80f));
                UiKit.Bind(detail, "upgrade." + definition.Key + ".desc");
                FitOneLine(detail, 22);

                // What the next level actually buys, in numbers — "+30% → +40%" says more than
                // a row of pips ever could.
                row.Effect = UiKit.CreateText(_font, "Effect", card, 30, FontStyle.Bold, row.Accent, TextAnchor.UpperLeft);
                UiKit.Anchor(row.Effect.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(174f, -156f), new Vector2(-290f, -120f));
                FitOneLine(row.Effect, 22);

                // Tracks are different lengths; a track only ever gets the pips it can fill.
                row.Pips = new Image[definition.MaxLevel];

                for (int p = 0; p < definition.MaxLevel; p++)
                {
                    Image pip = UiKit.CreatePanel("Pip" + p, card, EmptyPip);
                    RectTransform pipRect = pip.rectTransform;
                    pipRect.anchorMin = Vector2.zero;
                    pipRect.anchorMax = Vector2.zero;
                    pipRect.pivot = Vector2.zero;
                    pipRect.sizeDelta = new Vector2(38f, 14f);
                    pipRect.anchoredPosition = new Vector2(174f + p * 48f, 26f);
                    row.Pips[p] = pip;
                }

                row.Buy = CreatePriceButton("Buy", card, 40, 40f);
                PlaceRight(row.Buy.Root, new Vector2(250f, 100f), 24f);

                UpgradeId id = definition.Id;
                row.Buy.Button.onClick.AddListener(() => BuyUpgrade(id));

                _upgradeRows[i] = row;
                top += UpgradeCardHeight + CardGap;
            }

            SetScrollHeight(scroll, top - CardGap + PageInset);
            return page;
        }

        private static Color UpgradeAccent(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.Shield:
                    return Sky;

                case UpgradeId.Luck:
                    return Leaf;

                default:
                    return Coral;
            }
        }

        private static IconShape UpgradeIcon(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.Shield:
                    return IconShape.Shield;

                case UpgradeId.Luck:
                    return IconShape.Clover;

                default:
                    return IconShape.Magnet;
            }
        }

        private static string UpgradeEffect(UpgradeId id, int level, bool maxed)
        {
            switch (id)
            {
                case UpgradeId.Magnet:
                {
                    int now = UpgradeCatalogue.MagnetWindowPercent(level);
                    return maxed
                        ? Loc.F("effect.magnet.max", now)
                        : Loc.F("effect.magnet", now, UpgradeCatalogue.MagnetWindowPercent(level + 1));
                }

                case UpgradeId.Shield:
                {
                    int now = UpgradeCatalogue.StartingShieldsAt(level);
                    int next = UpgradeCatalogue.StartingShieldsAt(level + 1);
                    return maxed ? Loc.F("effect.shield.max", now) : Loc.F(next == 1 ? "effect.shield.one" : "effect.shield", now, next);
                }

                default:
                {
                    int now = UpgradeCatalogue.SpecialGapReductionAt(level);
                    int next = UpgradeCatalogue.SpecialGapReductionAt(level + 1);
                    return maxed ? Loc.F("effect.luck.max", now) : Loc.F("effect.luck", now, next);
                }
            }
        }

        // ---- Themes ------------------------------------------------------------------------

        private RectTransform BuildThemesPage()
        {
            RectTransform page = CreatePage(Tab.Themes, "Themes", out RectTransform scroll);

            int count = ThemeCatalogue.Count;
            _themeTiles = new ThemeTile[count];

            for (int i = 0; i < count; i++)
            {
                ThemeDefinition theme = ThemeCatalogue.At(i);
                int column = i % ThemeColumns;
                int row = i / ThemeColumns;
                float top = PageInset + row * (TileHeight + TileGap);

                RectTransform tile = UiKit.CreateChild("Theme" + i, scroll);
                tile.anchorMin = new Vector2(column / (float)ThemeColumns, 1f);
                tile.anchorMax = new Vector2((column + 1) / (float)ThemeColumns, 1f);

                // Centre pivot, so the purchase pop grows the tile from its middle.
                tile.pivot = new Vector2(0.5f, 0.5f);
                tile.offsetMin = new Vector2(PageInset, -top - TileHeight);
                tile.offsetMax = new Vector2(-PageInset, -top);

                ThemeTile entry = new ThemeTile { Id = theme.Id, Root = tile };

                Image border = UiKit.CreatePanel("Border", tile, UiKit.Mint);
                UiKit.Anchor(border.rectTransform, Vector2.zero, Vector2.one, new Vector2(-5f, -5f), new Vector2(5f, 5f));
                entry.Border = border.gameObject;

                Image body = UiKit.CreatePanel("Body", tile, TileFill);
                UiKit.Stretch(body.rectTransform);

                RectTransform preview = BuildPreview(tile, theme, i);
                entry.Lock = CreateCornerMark(preview, IconShape.Lock, new Color(0f, 0f, 0f, 0.45f), Color.white, new Vector2(1f, 1f), new Vector2(-28f, -28f));
                entry.Check = CreateCornerMark(preview, IconShape.Check, UiKit.Mint, UiKit.Ink, new Vector2(0f, 1f), new Vector2(28f, -28f));

                entry.Name = UiKit.CreateText(_font, "Name", tile, 30, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                UiKit.Anchor(
                    entry.Name.rectTransform,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(8f, -12f - PreviewHeight - 52f),
                    new Vector2(-8f, -12f - PreviewHeight - 8f));
                UiKit.Bind(entry.Name, "theme." + theme.Id);
                FitOneLine(entry.Name, 22);

                entry.Button = CreatePriceButton("Action", tile, 30, 32f);
                RectTransform action = entry.Button.Root;
                action.anchorMin = new Vector2(0f, 0f);
                action.anchorMax = new Vector2(1f, 0f);
                action.pivot = new Vector2(0.5f, 0f);
                action.offsetMin = new Vector2(12f, 12f);
                action.offsetMax = new Vector2(-12f, 84f);

                string id = theme.Id;
                entry.Button.Button.onClick.AddListener(() => ChooseTheme(id));

                _themeTiles[i] = entry;
            }

            int rows = (count + ThemeColumns - 1) / ThemeColumns;
            SetScrollHeight(scroll, PageInset * 2f + rows * TileHeight + Mathf.Max(0, rows - 1) * TileGap);
            return page;
        }

        /// <summary>
        /// A little diorama of the theme: its own sky, a few of its stars, the glow of its
        /// accent and a short tower in the exact colours its blocks will wear in play — painted
        /// by ThemeCatalogue.BlockColor, the same formula the spawner uses. A swatch told the
        /// player two colours; this shows them what the run will actually look like.
        /// </summary>
        private static RectTransform BuildPreview(RectTransform tile, ThemeDefinition theme, int seed)
        {
            Image sky = UiKit.CreatePanel("Preview", tile, Color.white);
            RectTransform preview = sky.rectTransform;
            UiKit.Anchor(preview, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -12f - PreviewHeight), new Vector2(-12f, -12f));
            sky.gameObject.AddComponent<UiGradient>().SetColors(theme.SkyTop, theme.SkyBottom);

            Color starColor = new Color(theme.StarTint.r, theme.StarTint.g, theme.StarTint.b, 0.85f);

            for (int s = 0; s < 6; s++)
            {
                Image star = UiKit.CreateImage("Star" + s, preview, starColor);
                star.sprite = IconFactory.GetSprite(IconShape.Disc);

                Vector2 at = new Vector2(
                    Mathf.Lerp(0.12f, 0.88f, Hash(seed * 7.1f + s * 3.3f)),
                    Mathf.Lerp(0.78f, 0.93f, Hash(seed * 1.9f + s * 5.7f)));
                float size = Mathf.Lerp(4f, 8f, Hash(seed * 2.3f + s * 9.1f));
                Place(star.rectTransform, at, new Vector2(size, size), Vector2.zero);
            }

            Image glow = UiKit.CreateImage("Glow", preview, new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, 0.45f));
            glow.sprite = IconFactory.GetSprite(IconShape.Glow);
            Place(glow.rectTransform, new Vector2(0.5f, 0f), new Vector2(230f, 170f), new Vector2(0f, 84f));

            Image platform = UiKit.CreateImage("Platform", preview, theme.Platform);
            PlaceBottom(platform.rectTransform, new Vector2(150f, 14f), new Vector2(0f, 18f));

            for (int b = 0; b < PreviewBlocks; b++)
            {
                Color colour = ThemeCatalogue.BlockColor(theme, b * 2, PreviewHueStep);
                float width = 112f - b * 7f;
                float lean = (b % 2 == 0 ? -1f : 1f) * (2f + b);

                Image block = UiKit.CreateImage("Block" + b, preview, colour);
                PlaceBottom(block.rectTransform, new Vector2(width, PreviewBlockHeight - 1f), new Vector2(lean, 32f + b * PreviewBlockHeight));

                // A lighter strip along the top edge — the lit face that makes a flat rectangle
                // read as a block.
                Image face = UiKit.CreateImage("Face", block.rectTransform, Color.Lerp(colour, Color.white, 0.35f));
                RectTransform faceRect = face.rectTransform;
                faceRect.anchorMin = new Vector2(0f, 1f);
                faceRect.anchorMax = new Vector2(1f, 1f);
                faceRect.pivot = new Vector2(0.5f, 1f);
                faceRect.offsetMin = new Vector2(0f, -4f);
                faceRect.offsetMax = Vector2.zero;
            }

            return preview;
        }

        private static GameObject CreateCornerMark(RectTransform parent, IconShape icon, Color fill, Color ink, Vector2 corner, Vector2 offset)
        {
            Image disc = UiKit.CreateImage("Mark", parent, fill);
            disc.sprite = IconFactory.GetSprite(IconShape.Disc);
            Place(disc.rectTransform, corner, new Vector2(44f, 44f), offset);

            Image glyph = UiKit.CreateImage("Glyph", disc.rectTransform, ink);
            glyph.sprite = IconFactory.GetSprite(icon);
            glyph.preserveAspect = true;
            Place(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(26f, 26f), Vector2.zero);

            return disc.gameObject;
        }

        // ---- Profile -----------------------------------------------------------------------

        private RectTransform BuildProfilePage()
        {
            RectTransform page = CreatePage(Tab.Profile, "Profile", out RectTransform scroll);
            float top = PageInset;

            RectTransform rank = CreateCard(scroll, "Rank", top, RankCardHeight);

            _rankGlow = UiKit.CreateImage("RankGlow", rank, UiKit.Gold);
            _rankGlow.sprite = IconFactory.GetSprite(IconShape.Glow);
            PlaceLeft(_rankGlow.rectTransform, 118f, 290f);

            _rankMedal = UiKit.CreateImage("RankMedal", rank, UiKit.Gold);
            _rankMedal.sprite = IconFactory.GetSprite(IconShape.Disc);
            PlaceLeft(_rankMedal.rectTransform, 118f, 156f);

            Image star = UiKit.CreateImage("RankStar", _rankMedal.rectTransform, UiKit.Ink);
            star.sprite = IconFactory.GetSprite(IconShape.Star);
            star.preserveAspect = true;
            Place(star.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(92f, 92f), Vector2.zero);

            Text caption = UiKit.CreateText(_font, "RankCaption", rank, 26, FontStyle.Bold, UiKit.Dim, TextAnchor.UpperLeft);
            UiKit.Anchor(caption.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(222f, -62f), new Vector2(-28f, -28f));
            UiKit.Bind(caption, "shop.rank_caption");
            FitOneLine(caption, 18);

            _rankTitle = UiKit.CreateText(_font, "RankTitle", rank, 56, FontStyle.Bold, UiKit.Gold, TextAnchor.UpperLeft);
            UiKit.Anchor(_rankTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(222f, -128f), new Vector2(-28f, -62f));
            FitOneLine(_rankTitle, 36);

            _rankNext = UiKit.CreateText(_font, "RankNext", rank, 28, FontStyle.Bold, Color.white, TextAnchor.UpperLeft);
            UiKit.Anchor(_rankNext.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(222f, -170f), new Vector2(-28f, -134f));
            FitOneLine(_rankNext, 22);

            Image track = UiKit.CreatePanel("Track", rank, TrackFill);
            UiKit.Anchor(track.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(222f, 64f), new Vector2(-32f, 92f));
            track.pixelsPerUnitMultiplier = 3f;

            _rankFill = UiKit.CreatePanel("Fill", track.rectTransform, UiKit.Gold);
            RectTransform fill = _rankFill.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            _rankFill.pixelsPerUnitMultiplier = 3f;

            _rankProgress = UiKit.CreateText(_font, "RankProgress", rank, 26, FontStyle.Bold, UiKit.Dim, TextAnchor.LowerLeft);
            UiKit.Anchor(_rankProgress.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(222f, 22f), new Vector2(-32f, 58f));
            FitOneLine(_rankProgress, 20);

            top += RankCardHeight + CardGap;

            string[] labels = { "stat.best", "stat.runs", "stat.blasts", "stat.biggest", "stat.streak", "stat.collection" };
            IconShape[] icons = { IconShape.Target, IconShape.Replay, IconShape.Burst, IconShape.Bolt, IconShape.Flame, IconShape.Bag };
            Color[] colours = { UiKit.Gold, UiKit.Mint, Coral, Sky, Ember, Violet };

            _stats = new Text[labels.Length];

            for (int i = 0; i < labels.Length; i++)
            {
                int column = i % 2;
                float tileTop = top + (i / 2) * (StatTileHeight + CardGap);

                Image tile = UiKit.CreatePanel("Stat" + i, scroll, CardFill);
                RectTransform rect = tile.rectTransform;
                rect.anchorMin = new Vector2(column * 0.5f, 1f);
                rect.anchorMax = new Vector2((column + 1) * 0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(column == 0 ? PageInset : CardGap * 0.5f, -tileTop - StatTileHeight);
                rect.offsetMax = new Vector2(column == 0 ? -CardGap * 0.5f : -PageInset, -tileTop);

                CreateBadge(rect, icons[i], colours[i], 78f, 66f);

                Text value = UiKit.CreateText(_font, "Value", rect, 48, FontStyle.Bold, Color.white, TextAnchor.LowerLeft);
                UiKit.Anchor(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(124f, -4f), new Vector2(-16f, 58f));
                FitOneLine(value, 30);

                Text label = UiKit.CreateText(_font, "Label", rect, 24, FontStyle.Bold, UiKit.Dim, TextAnchor.UpperLeft);
                UiKit.Anchor(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(124f, -44f), new Vector2(-16f, -8f));
                FitOneLine(label, 18);
                UiKit.Bind(label, labels[i]);

                _stats[i] = value;
            }

            int statRows = (labels.Length + 1) / 2;
            top += statRows * StatTileHeight + (statRows - 1) * CardGap;

            SetScrollHeight(scroll, top + PageInset);
            return page;
        }

        // ---- Daily missions ----------------------------------------------------------------

        private RectTransform BuildMissionsPage()
        {
            RectTransform page = CreatePage(Tab.Missions, "Missions", out RectTransform scroll);
            float top = PageInset;

            _resetLabel = UiKit.CreateText(_font, "Reset", scroll, 30, FontStyle.Bold, UiKit.Dim, TextAnchor.MiddleCenter);
            UiKit.Anchor(_resetLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(PageInset, -top - 48f), new Vector2(-PageInset, -top));
            FitOneLine(_resetLabel, 22);
            top += 60f;

            _missionRows = new MissionRow[MissionSystem.DailyCount];

            for (int i = 0; i < _missionRows.Length; i++)
            {
                MissionRow row = new MissionRow();
                row.Card = CreateCard(scroll, "Mission" + i, top, MissionCardHeight);

                // Recoloured and re-iconed per mission kind on every refresh.
                row.Badge = CreateBadge(row.Card, IconShape.Target, UiKit.Gold, 96f, 82f);

                row.Label = UiKit.CreateText(_font, "Label", row.Card, 34, FontStyle.Bold, Color.white, TextAnchor.UpperLeft);
                UiKit.Anchor(row.Label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(150f, -72f), new Vector2(-272f, -24f));
                FitOneLine(row.Label, 24);

                Image track = UiKit.CreatePanel("Track", row.Card, TrackFill);
                UiKit.Anchor(track.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(150f, 64f), new Vector2(-272f, 92f));
                track.pixelsPerUnitMultiplier = 3f;

                row.Fill = UiKit.CreatePanel("Fill", track.rectTransform, UiKit.Mint);
                RectTransform fillRect = row.Fill.rectTransform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                row.Fill.pixelsPerUnitMultiplier = 3f;

                row.Progress = UiKit.CreateText(_font, "Progress", row.Card, 28, FontStyle.Bold, UiKit.Dim, TextAnchor.LowerLeft);
                UiKit.Anchor(row.Progress.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(150f, 18f), new Vector2(-272f, 56f));

                row.Claim = CreatePriceButton("Claim", row.Card, 38, 38f);
                PlaceRight(row.Claim.Root, new Vector2(232f, 100f), 22f);

                int index = i;
                row.Claim.Button.onClick.AddListener(() => ClaimMission(index));

                _missionRows[i] = row;
                top += MissionCardHeight + CardGap;
            }

            SetScrollHeight(scroll, top - CardGap + PageInset);
            return page;
        }

        private static Color MissionColor(MissionKind kind)
        {
            switch (kind)
            {
                case MissionKind.Perfects:
                    return UiKit.Mint;

                case MissionKind.Blasts:
                    return Coral;

                case MissionKind.Specials:
                    return Sky;

                default:
                    return UiKit.Gold;
            }
        }

        private static IconShape MissionIcon(MissionKind kind)
        {
            switch (kind)
            {
                case MissionKind.Perfects:
                    return IconShape.Check;

                case MissionKind.Blasts:
                    return IconShape.Burst;

                case MissionKind.Specials:
                    return IconShape.Bolt;

                default:
                    return IconShape.Target;
            }
        }

        // ---- Store rows --------------------------------------------------------------------

        private void BuildFooter(RectTransform root)
        {
            BuildCoinPacks(root);

            _removeAds = UiKit.CreateButton(_font, "RemoveAds", root, "-", 46, UiKit.Panel, Color.white, IconShape.Bag);
            RectTransform adsRect = _removeAds.Root;
            adsRect.anchorMin = new Vector2(0f, 0f);
            adsRect.anchorMax = new Vector2(1f, 0f);
            adsRect.pivot = new Vector2(0.5f, 0f);
            adsRect.offsetMin = new Vector2(40f, 118f);
            adsRect.offsetMax = new Vector2(-40f, 226f);
            _removeAds.Button.onClick.AddListener(() => RemoveAdsRequested?.Invoke());

            _restore = UiKit.CreateButton(_font, "Restore", root, "-", 32, new Color(1f, 1f, 1f, 0.08f), UiKit.Dim, IconShape.None);
            UiKit.BindLabel(_restore, "shop.restore", 860f);
            RectTransform restoreRect = _restore.Root;
            restoreRect.anchorMin = new Vector2(0f, 0f);
            restoreRect.anchorMax = new Vector2(1f, 0f);
            restoreRect.pivot = new Vector2(0.5f, 0f);
            restoreRect.offsetMin = new Vector2(40f, 36f);
            restoreRect.offsetMax = new Vector2(-40f, 106f);
            _restore.Button.onClick.AddListener(() => RestoreRequested?.Invoke());
        }

        /// <summary>
        /// Three coin tiers along the bottom. The button shows what the pack contains, not
        /// what it costs: the real price is per-store, per-currency and only known once the
        /// IAP catalogue has loaded, so it is filled in later by SetCoinPackPrice — and until
        /// then the row still says something true.
        /// </summary>
        private void BuildCoinPacks(RectTransform root)
        {
            _coinPackLabels = new Text[CoinPackIds.Length];
            _coinPacks = new RectTransform[CoinPackIds.Length];

            for (int i = 0; i < CoinPackIds.Length; i++)
            {
                MenuControl pack = UiKit.CreateButton(_font, "Pack" + i, root, string.Empty, 0, UiKit.Panel, UiKit.Gold, IconShape.Coin);

                RectTransform rect = pack.Root;
                rect.anchorMin = new Vector2(i / 3f, 0f);
                rect.anchorMax = new Vector2((i + 1) / 3f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.offsetMin = new Vector2(i == 0 ? 40f : 8f, 240f);
                rect.offsetMax = new Vector2(i == CoinPackIds.Length - 1 ? -40f : -8f, 348f);

                if (pack.Icon != null)
                {
                    RectTransform iconRect = pack.Icon.rectTransform;
                    iconRect.anchorMin = new Vector2(0.5f, 1f);
                    iconRect.anchorMax = new Vector2(0.5f, 1f);
                    iconRect.sizeDelta = new Vector2(40f, 40f);
                    iconRect.anchoredPosition = new Vector2(0f, -16f);
                }

                Text amount = UiKit.CreateText(_font, "PackAmount" + i, rect, 40, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Anchor(amount.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 4f), new Vector2(0f, -30f));
                amount.text = CoinPackAmounts[i].ToString(CultureInfo.InvariantCulture);

                Text price = UiKit.CreateText(_font, "PackPrice" + i, rect, 30, FontStyle.Bold, UiKit.Gold, TextAnchor.LowerCenter);
                UiKit.Anchor(price.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 10f), new Vector2(0f, 46f));
                UiKit.Bind(price, "shop.coins", 200f);
                _coinPackLabels[i] = price;

                string id = CoinPackIds[i];
                pack.Button.onClick.AddListener(() => CoinPackRequested?.Invoke(id));
                _coinPacks[i] = rect;
            }
        }

        /// <summary>
        /// Shows or hides everything that spends real money. A build without the Unity IAP
        /// package — or a device where the store never came up — cannot complete any of these,
        /// and a row that takes a tap and does nothing is exactly what App Review rejects. The
        /// run-over screen already holds its rewarded-ad button back on the same principle.
        /// Without the store rows, the pages above take the room they leave.
        /// </summary>
        public void SetStoreAvailable(bool available)
        {
            _storeAvailable = available;

            if (_removeAds != null)
            {
                _removeAds.Root.gameObject.SetActive(available);
            }

            if (_restore != null)
            {
                _restore.Root.gameObject.SetActive(available);
            }

            if (_coinPacks != null)
            {
                for (int i = 0; i < _coinPacks.Length; i++)
                {
                    if (_coinPacks[i] != null)
                    {
                        _coinPacks[i].gameObject.SetActive(available);
                    }
                }
            }

            if (_content != null)
            {
                _content.offsetMin = new Vector2(40f, available ? FooterReserve : NoFooterReserve);
            }
        }

        /// <summary>Called once the store has resolved a localised price string.</summary>
        public void SetCoinPackPrice(string productId, string price)
        {
            if (_coinPackLabels == null || string.IsNullOrEmpty(price))
            {
                return;
            }

            for (int i = 0; i < CoinPackIds.Length; i++)
            {
                if (CoinPackIds[i] == productId && _coinPackLabels[i] != null)
                {
                    _coinPackLabels[i].text = price;
                    return;
                }
            }
        }

        // ---- Shared pieces -----------------------------------------------------------------

        /// <summary>One full-width row of a page, stacked from the top of its scroll content.</summary>
        private static RectTransform CreateCard(RectTransform parent, string name, float top, float height)
        {
            Image card = UiKit.CreatePanel(name, parent, CardFill);
            RectTransform rect = card.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(PageInset, -top - height);
            rect.offsetMax = new Vector2(-PageInset, -top);
            return rect;
        }

        private static Badge CreateBadge(RectTransform parent, IconShape icon, Color accent, float size, float x)
        {
            Badge badge = new Badge();

            badge.Glow = UiKit.CreateImage("BadgeGlow", parent, new Color(accent.r, accent.g, accent.b, 0.35f));
            badge.Glow.sprite = IconFactory.GetSprite(IconShape.Glow);
            PlaceLeft(badge.Glow.rectTransform, x, size * 1.9f);

            badge.Disc = UiKit.CreateImage("Badge", parent, accent);
            badge.Disc.sprite = IconFactory.GetSprite(IconShape.Disc);
            PlaceLeft(badge.Disc.rectTransform, x, size);

            badge.Glyph = UiKit.CreateImage("Glyph", badge.Disc.rectTransform, UiKit.Ink);
            badge.Glyph.sprite = IconFactory.GetSprite(icon);
            badge.Glyph.preserveAspect = true;
            Place(badge.Glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size * 0.58f, size * 0.58f), Vector2.zero);

            return badge;
        }

        /// <summary>A button carrying a coin and a number — every price in the Workshop.</summary>
        private MenuControl CreatePriceButton(string name, RectTransform parent, int fontSize, float coinSize)
        {
            MenuControl button = UiKit.CreateButton(_font, name, parent, "0", fontSize, UiKit.Mint, UiKit.Ink, IconShape.Coin);

            // CreateButton sizes its glyph for a full-height menu button; a price reads better
            // with a coin about the height of its digits.
            LayoutElement coin = button.Icon != null ? button.Icon.GetComponent<LayoutElement>() : null;

            if (coin != null)
            {
                coin.preferredWidth = coinSize;
                coin.preferredHeight = coinSize;
            }

            return button;
        }

        /// <summary>One of the few looks every price button in the Workshop takes.</summary>
        private static void StylePrice(MenuControl button, string text, bool showCoin, Color fill, Color ink, Color coin, bool interactable)
        {
            button.Button.interactable = interactable;

            Image background = button.Button.targetGraphic as Image;

            if (background != null)
            {
                background.color = fill;
            }

            if (button.Label != null)
            {
                button.Label.text = text;
                UiKit.SetLabelColor(button.Label, ink);
            }

            if (button.Icon != null)
            {
                SetActive(button.Icon.gameObject, showCoin);
                button.Icon.color = coin;
            }
        }

        /// <summary>A horizontally centred, self-sizing row — the arrangement CreateButton uses for a glyph and its word.</summary>
        private static RectTransform CreateCentredRow(string name, RectTransform parent, float spacing)
        {
            RectTransform row = UiKit.CreateChild(name, parent);
            row.anchorMin = new Vector2(0.5f, 0.5f);
            row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0.5f, 0.5f);

            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return row;
        }

        /// <summary>Shrinks a label to stay on one line rather than wrapping or running under a button.</summary>
        private static void FitOneLine(Text text, int minSize)
        {
            UiKit.FitOneLine(text, minSize);
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        /// <summary>A square element centred <paramref name="x"/> in from its parent's left edge.</summary>
        private static void PlaceLeft(RectTransform rect, float x, float size)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        private static void PlaceRight(RectTransform rect, Vector2 size, float inset)
        {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(-inset, 0f);
        }

        private static void PlaceBottom(RectTransform rect, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        private static void DisableShadow(Text text)
        {
            Shadow shadow = text.GetComponent<Shadow>();

            if (shadow != null)
            {
                shadow.enabled = false;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        /// <summary>A repeatable pseudo-random 0..1, so decoration lands in the same place every launch.</summary>
        private static float Hash(float seed)
        {
            return Mathf.Repeat(Mathf.Sin(seed) * 43758.5453f, 1f);
        }

        private static string Format(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        // ---- State -------------------------------------------------------------------------

#if SLICEBLAST_SCREENSHOTS
        /// <summary>Store-capture autoplay only: opens a tab by index, as a tap on it would.</summary>
        public void ShowTabForCapture(int index)
        {
            SelectTab((Tab)index);
        }
#endif

        private void SelectTab(Tab tab)
        {
            _tab = tab;

            for (int i = 0; i < _pages.Length; i++)
            {
                bool selected = i == (int)tab;

                if (_pages[i] != null)
                {
                    _pages[i].gameObject.SetActive(selected);
                }

                if (_tabs[i] != null)
                {
                    Image background = _tabs[i].Button.targetGraphic as Image;

                    if (background != null)
                    {
                        background.color = selected ? UiKit.Mint : UiKit.Panel;
                    }

                    UiKit.SetLabelColor(_tabs[i].Label, selected ? UiKit.Ink : Color.white);
                }
            }

            _countdown = 0f;
            ScrollToTop(tab);
            Refresh();
        }

        private void ScrollToTop(Tab tab)
        {
            ScrollRect scroll = _scrolls != null ? _scrolls[(int)tab] : null;

            if (scroll == null || scroll.content == null)
            {
                return;
            }

            scroll.StopMovement();
            scroll.content.anchoredPosition = Vector2.zero;
        }

        public void Show()
        {
            MissionSystem.EnsureToday();

            IsOpen = true;
            _targetAlpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;

            // Coins that changed while the Workshop was closed (a run's payout) are not news
            // worth a pop; only a change made in here is.
            _shownCoins = -1;
            _countdown = 0f;

            ScrollToTop(_tab);
            Refresh();
        }

        public void Hide()
        {
            IsOpen = false;
            _targetAlpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private void BuyUpgrade(UpgradeId id)
        {
            bool bought = UpgradeCatalogue.TryPurchase(id);
            PurchaseResolved?.Invoke(bought);

            if (bought)
            {
                for (int i = 0; i < _upgradeRows.Length; i++)
                {
                    if (_upgradeRows[i].Id == id)
                    {
                        Punch(_upgradeRows[i].Badge.Disc.rectTransform, 0.28f);
                        break;
                    }
                }
            }

            Refresh();
        }

        private void ChooseTheme(string id)
        {
            bool changed;

            if (ThemeCatalogue.IsOwned(id))
            {
                // Owned already: this is an equip, which always succeeds and is not a
                // purchase — reporting it as one would play the coin sound for free.
                PlayerProfile.EquipTheme(id);
                PurchaseResolved?.Invoke(true);
                changed = true;
            }
            else
            {
                changed = ThemeCatalogue.TryPurchase(id);
                PurchaseResolved?.Invoke(changed);
            }

            if (changed)
            {
                for (int i = 0; i < _themeTiles.Length; i++)
                {
                    if (_themeTiles[i].Id == id)
                    {
                        Punch(_themeTiles[i].Root, 0.06f);
                        break;
                    }
                }
            }

            Refresh();
        }

        private void ClaimMission(int index)
        {
            int reward = MissionSystem.TryClaim(index);
            PurchaseResolved?.Invoke(reward > 0);

            if (reward > 0 && index >= 0 && index < _missionRows.Length)
            {
                Punch(_missionRows[index].Badge.Disc.rectTransform, 0.28f);
            }

            Refresh();
        }

        /// <summary>
        /// Rewrites every row from the profile. Cheap enough to call on any change — the
        /// whole screen is a few dozen Text assignments — and it means no code path has to
        /// remember which particular widget its action invalidated.
        /// </summary>
        public void Refresh()
        {
            if (_coinLabel == null)
            {
                return;
            }

            int coins = PlayerProfile.Coins;
            _coinLabel.text = Format(coins);

            if (_shownCoins >= 0 && coins != _shownCoins)
            {
                _coinPunch = 1f;
            }

            _shownCoins = coins;

            int streak = PlayerProfile.Data.dailyStreak;
            bool showStreak = streak > 1;
            SetActive(_streakChip.gameObject, showStreak);
            UiKit.SetText(_streakLabel, Loc.F("shop.streak", streak), 38, 220f);
            _coinChip.anchoredPosition = new Vector2(showStreak ? -ChipOffset : 0f, ChipY);
            _streakChip.anchoredPosition = new Vector2(ChipOffset, ChipY);

            // The Workshop wears the equipped theme: equip one and the header glow and the
            // stars change with it.
            ThemeDefinition theme = ThemeCatalogue.Equipped;
            _glowTint = theme.Accent;
            _starTint = theme.StarTint;

            RefreshUpgrades(coins);
            RefreshThemes(coins);
            RefreshProfile();
            RefreshMissions();
            UpdateResetLabel();

            int claimable = MissionSystem.ClaimableCount();
            SetActive(_missionBadge, claimable > 0);
            _missionBadgeLabel.text = claimable.ToString(CultureInfo.InvariantCulture);

            if (_removeAds != null && _storeAvailable)
            {
                bool removed = PlayerProfile.AdsRemoved;
                _removeAds.Button.interactable = !removed;

                if (_removeAds.Label != null)
                {
                    UiKit.SetText(_removeAds.Label, Loc.T(removed ? "shop.ads_removed" : "shop.remove_ads"), 46, 820f);
                    UiKit.SetLabelColor(_removeAds.Label, removed ? UiKit.Mint : Color.white);
                }
            }
        }

        private void RefreshUpgrades(int coins)
        {
            for (int i = 0; i < _upgradeRows.Length; i++)
            {
                UpgradeRow row = _upgradeRows[i];
                int level = PlayerProfile.GetUpgradeLevel(row.Id);
                bool maxed = UpgradeCatalogue.IsMaxed(row.Id);
                int cost = UpgradeCatalogue.CostOfNext(row.Id);
                bool affordable = !maxed && coins >= cost;

                for (int p = 0; p < row.Pips.Length; p++)
                {
                    row.Pips[p].color = p < level ? row.Accent : EmptyPip;
                }

                row.Effect.text = UpgradeEffect(row.Id, level, maxed);
                SetActive(row.MaxStar, maxed);

                // On a dark disabled panel the ink label would be unreadable, so the states swap
                // foreground as well as background.
                if (maxed)
                {
                    StylePrice(row.Buy, Loc.T("shop.max"), false, MutedFill, UiKit.Gold, UiKit.Gold, false);
                }
                else if (affordable)
                {
                    StylePrice(row.Buy, Format(cost), true, UiKit.Mint, UiKit.Ink, UiKit.Ink, true);
                }
                else
                {
                    StylePrice(row.Buy, Format(cost), true, MutedFill, MutedText, DimGold, false);
                }
            }
        }

        private void RefreshThemes(int coins)
        {
            string equipped = ThemeCatalogue.Equipped.Id;

            for (int i = 0; i < _themeTiles.Length; i++)
            {
                ThemeTile tile = _themeTiles[i];
                ThemeDefinition theme = ThemeCatalogue.Get(tile.Id);

                bool owned = ThemeCatalogue.IsOwned(tile.Id);
                bool active = tile.Id == equipped;
                bool affordable = coins >= theme.Price;

                SetActive(tile.Border, active);
                SetActive(tile.Check, active);
                SetActive(tile.Lock, !owned);
                tile.Name.color = owned ? Color.white : new Color(1f, 1f, 1f, 0.72f);

                if (active)
                {
                    StylePrice(tile.Button, Loc.T("shop.active"), false, MutedFill, UiKit.Mint, UiKit.Mint, false);
                }
                else if (owned)
                {
                    StylePrice(tile.Button, Loc.T("shop.equip"), false, UiKit.Mint, UiKit.Ink, UiKit.Ink, true);
                }
                else if (affordable)
                {
                    StylePrice(tile.Button, Format(theme.Price), true, UiKit.Mint, UiKit.Ink, UiKit.Ink, true);
                }
                else
                {
                    StylePrice(tile.Button, Format(theme.Price), true, MutedFill, MutedText, DimGold, false);
                }
            }
        }

        private void RefreshProfile()
        {
            ProfileData data = PlayerProfile.Data;
            long lifetime = Math.Max(0L, data.lifetimeScore);
            int rank = PlayerRanks.IndexFor(lifetime);
            Color colour = RankColors[Mathf.Clamp(rank, 0, RankColors.Length - 1)];

            _rankTitle.text = PlayerRanks.Name(rank);
            UiKit.SetLabelColor(_rankTitle, colour);
            _rankMedal.color = colour;
            _rankFill.color = colour;
            _rankGlowTint = colour;

            float ratio;

            if (PlayerRanks.IsHighest(rank))
            {
                ratio = 1f;
                _rankNext.text = Loc.T("shop.rank_top");
                _rankProgress.text = Loc.F("shop.points_total", Format(lifetime));
            }
            else
            {
                long from = PlayerRanks.Threshold(rank);
                long to = PlayerRanks.Threshold(rank + 1);
                ratio = to > from ? Mathf.Clamp01((float)(lifetime - from) / (to - from)) : 1f;
                _rankNext.text = Loc.F("shop.rank_next", PlayerRanks.Name(rank + 1));
                _rankProgress.text = Loc.F("shop.points_of", Format(lifetime), Format(to));
            }

            _rankFill.rectTransform.anchorMax = new Vector2(ratio, 1f);

            _stats[0].text = Format(data.bestScore);
            _stats[1].text = Format(data.totalRuns);
            _stats[2].text = Format(data.totalBlasts);
            _stats[3].text = Format(data.biggestBlast);
            _stats[4].text = Format(data.dailyStreak);
            _stats[5].text = CollectionPercent().ToString(CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>Everything the Workshop sells, owned — themes and upgrade levels alike.</summary>
        private static int CollectionPercent()
        {
            int have = 0;
            int total = 0;

            for (int i = 0; i < ThemeCatalogue.Count; i++)
            {
                total++;

                if (ThemeCatalogue.IsOwned(ThemeCatalogue.At(i).Id))
                {
                    have++;
                }
            }

            for (int i = 0; i < UpgradeCatalogue.Count; i++)
            {
                UpgradeDefinition upgrade = UpgradeCatalogue.At(i);
                total += upgrade.MaxLevel;
                have += Mathf.Min(PlayerProfile.GetUpgradeLevel(upgrade.Id), upgrade.MaxLevel);
            }

            return total > 0 ? Mathf.FloorToInt(100f * have / total) : 0;
        }

        private void RefreshMissions()
        {
            int count = MissionSystem.Count;

            for (int i = 0; i < _missionRows.Length; i++)
            {
                MissionRow row = _missionRows[i];
                bool exists = i < count;

                SetActive(row.Card.gameObject, exists);

                if (!exists)
                {
                    continue;
                }

                MissionDefinition definition = MissionSystem.At(i);
                int progress = MissionSystem.ProgressAt(i);
                bool complete = MissionSystem.IsComplete(i);
                bool claimed = MissionSystem.IsClaimed(i);
                Color colour = MissionColor(definition.Kind);

                row.Badge.Disc.color = colour;
                row.Badge.Glow.color = new Color(colour.r, colour.g, colour.b, 0.35f);
                row.Badge.Glyph.sprite = IconFactory.GetSprite(MissionIcon(definition.Kind));

                row.Label.text = MissionSystem.Describe(definition);
                row.Progress.text = claimed ? Loc.T("shop.claimed") : Format(progress) + " / " + Format(definition.Target);

                float ratio = definition.Target > 0
                    ? Mathf.Clamp01(progress / (float)definition.Target)
                    : 0f;

                row.Fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
                row.Fill.color = colour;

                string reward = "+" + Format(definition.Reward);

                if (claimed)
                {
                    StylePrice(row.Claim, Loc.T("shop.done"), false, MutedFill, MutedText, MutedText, false);
                }
                else if (complete)
                {
                    StylePrice(row.Claim, reward, true, UiKit.Gold, UiKit.Ink, UiKit.Ink, true);
                }
                else
                {
                    StylePrice(row.Claim, reward, true, MutedFill, MutedText, DimGold, false);
                }
            }
        }

        private void UpdateResetLabel()
        {
            if (_resetLabel == null)
            {
                return;
            }

            // Missions roll on the UTC date (PlayerProfile.TodayKey), so that is the midnight
            // this counts down to.
            TimeSpan left = DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow;
            int hours = Mathf.Max(0, (int)left.TotalHours);
            int minutes = Mathf.Max(0, left.Minutes);

            _resetLabel.text = Loc.F("shop.reset", hours, minutes.ToString("00", CultureInfo.InvariantCulture));
        }

        // ---- Animation ---------------------------------------------------------------------

        private void Punch(RectTransform target, float amount)
        {
            if (target == null)
            {
                return;
            }

            if (_punchTarget != null && _punchTarget != target)
            {
                _punchTarget.localScale = Vector3.one;
            }

            _punchTarget = target;
            _punchAmount = amount;
            _punch = 1f;
        }

        private void Update()
        {
            if (_group == null)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;

            // Unscaled: the shop opens from the run-over screen, where the death slow-motion
            // still owns Time.timeScale, and from a paused run, where it is zero.
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, dt * 6f);

            if (_group.alpha <= 0f)
            {
                return;
            }

            AnimateBackdrop(Time.unscaledTime);
            AnimatePunch(dt);

            if (IsOpen && _tab == Tab.Missions)
            {
                TickCountdown(dt);
            }
        }

        private void AnimateBackdrop(float time)
        {
            float breath = 0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 2f / 3.2f);
            _headerGlow.color = new Color(_glowTint.r, _glowTint.g, _glowTint.b, Mathf.Lerp(0.16f, 0.32f, breath));

            for (int i = 0; i < _stars.Length; i++)
            {
                float twinkle = 0.5f + 0.5f * Mathf.Sin(time * _starSpeed[i] + _starPhase[i]);
                _stars[i].color = new Color(_starTint.r, _starTint.g, _starTint.b, Mathf.Lerp(0.1f, 0.7f, twinkle));
            }

            if (_tab == Tab.Profile && _rankGlow != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 2f / 1.8f);
                _rankGlow.color = new Color(_rankGlowTint.r, _rankGlowTint.g, _rankGlowTint.b, Mathf.Lerp(0.25f, 0.6f, pulse));

                float scale = Mathf.Lerp(0.94f, 1.08f, pulse);
                _rankGlow.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void AnimatePunch(float dt)
        {
            if (_punchTarget != null && _punch > 0f)
            {
                _punch = Mathf.Max(0f, _punch - dt * 3.2f);
                float scale = 1f + Mathf.Sin(_punch * Mathf.PI) * _punchAmount;
                _punchTarget.localScale = new Vector3(scale, scale, 1f);

                if (_punch <= 0f)
                {
                    _punchTarget.localScale = Vector3.one;
                    _punchTarget = null;
                }
            }

            if (_coinPunch > 0f)
            {
                _coinPunch = Mathf.Max(0f, _coinPunch - dt * 4f);
                float scale = 1f + Mathf.Sin(_coinPunch * Mathf.PI) * 0.14f;
                _coinChip.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void TickCountdown(float dt)
        {
            _countdown -= dt;

            if (_countdown > 0f)
            {
                return;
            }

            _countdown = 1f;

            // A day can end while the Workshop is open; the next set arrives without a reopen.
            if (PlayerProfile.Data.missionDay != PlayerProfile.TodayKey())
            {
                MissionSystem.EnsureToday();
                Refresh();
                return;
            }

            UpdateResetLabel();
        }
    }
}
