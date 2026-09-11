// The meta screen: upgrades, themes, daily missions and the store rows. Built once in code
// like the rest of the interface, then refreshed in place — nothing here is created or
// destroyed while it is open, so opening it mid-session costs one layout pass and no GC.
//
// It owns no store logic. Buying a real product raises an event the bootstrap wires to the
// IAP manager, exactly as the run-over screen raises ContinueRequested rather than talking to
// the ad SDK itself. Coin purchases are local, so those it does resolve directly.
using System;
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
            Missions = 2
        }

        private sealed class UpgradeRow
        {
            public UpgradeId Id;
            public Text Name;
            public Text Detail;
            public Text Cost;
            public MenuControl Buy;
            public Image[] Pips;
        }

        private sealed class ThemeRow
        {
            public string Id;
            public Text Name;
            public Text State;
            public Image Swatch;
            public MenuControl Button;
        }

        private sealed class MissionRow
        {
            public int Index;
            public Text Label;
            public Text Progress;
            public RectTransform Fill;
            public MenuControl Claim;
            public Text Reward;
        }

        private const int MaxPips = 5;

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

        private Text _coinLabel;
        private Text _streakLabel;
        private RectTransform _content;
        private MenuControl[] _tabs;
        private RectTransform[] _pages;
        private Tab _tab = Tab.Upgrades;

        private UpgradeRow[] _upgradeRows;
        private ThemeRow[] _themeRows;
        private MissionRow[] _missionRows;
        private MenuControl _removeAds;
        private MenuControl _restore;
        private RectTransform[] _coinPacks;
        private Text _missionBadge;
        private Text[] _coinPackLabels;
        private bool _storeAvailable;

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

            Image dim = UiKit.CreateImage("Dim", root, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.96f));
            UiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true; // nothing behind the shop is tappable while it is open

            BuildHeader(root);
            BuildTabs(root);

            _content = UiKit.CreateChild("Content", root);
            UiKit.Anchor(_content, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(40f, 366f), new Vector2(-40f, -430f));

            _pages = new RectTransform[3];

            _pages[(int)Tab.Upgrades] = BuildUpgradesPage();
            _pages[(int)Tab.Themes] = BuildThemesPage();
            _pages[(int)Tab.Missions] = BuildMissionsPage();

            BuildFooter(root);
            SelectTab(Tab.Upgrades);
        }

        private void BuildHeader(RectTransform root)
        {
            Text title = UiKit.CreateText(_font, "ShopTitle", root, 88, FontStyle.Bold, Color.white, TextAnchor.UpperCenter);
            UiKit.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -190f), new Vector2(0f, -80f));
            title.text = "WORKSHOP";

            MenuControl close = UiKit.CreateButton(_font, "Close", root, string.Empty, 0, new Color(1f, 1f, 1f, 0.14f), Color.white, IconShape.Close);
            RectTransform closeRect = close.Root;
            closeRect.anchorMin = new Vector2(0f, 1f);
            closeRect.anchorMax = new Vector2(0f, 1f);
            closeRect.pivot = new Vector2(0f, 1f);
            closeRect.sizeDelta = new Vector2(120f, 120f);
            closeRect.anchoredPosition = new Vector2(40f, -70f);
            close.Button.onClick.AddListener(() => Closed?.Invoke());

            Image coin = UiKit.CreateImage("HeaderCoin", root, UiKit.Gold);
            coin.sprite = IconFactory.GetSprite(IconShape.Coin);
            coin.preserveAspect = true;
            RectTransform coinRect = coin.rectTransform;
            coinRect.anchorMin = new Vector2(1f, 1f);
            coinRect.anchorMax = new Vector2(1f, 1f);
            coinRect.pivot = new Vector2(1f, 1f);
            coinRect.sizeDelta = new Vector2(62f, 62f);
            coinRect.anchoredPosition = new Vector2(-40f, -98f);

            _coinLabel = UiKit.CreateText(_font, "HeaderCoins", root, 56, FontStyle.Bold, UiKit.Gold, TextAnchor.UpperRight);
            UiKit.Anchor(_coinLabel.rectTransform, new Vector2(0.4f, 1f), new Vector2(1f, 1f), new Vector2(0f, -170f), new Vector2(-114f, -100f));

            _streakLabel = UiKit.CreateText(_font, "Streak", root, 38, FontStyle.Bold, UiKit.Mint, TextAnchor.UpperCenter);
            UiKit.Anchor(_streakLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -244f), new Vector2(0f, -196f));
        }

        private void BuildTabs(RectTransform root)
        {
            _tabs = new MenuControl[3];

            string[] labels = { "UPGRADES", "THEMES", "DAILY" };
            IconShape[] icons = { IconShape.Chevrons, IconShape.Burst, IconShape.Target };

            for (int i = 0; i < _tabs.Length; i++)
            {
                MenuControl tab = UiKit.CreateButton(_font, "Tab" + i, root, labels[i], 38, UiKit.Panel, Color.white, icons[i]);

                RectTransform rect = tab.Root;
                rect.anchorMin = new Vector2(i / 3f, 1f);
                rect.anchorMax = new Vector2((i + 1) / 3f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(46f, -390f);
                rect.offsetMax = new Vector2(-6f, -290f);

                // The icon crowds a 38pt word in a third-width button; the label alone is
                // clearer at this size, so the glyph is dropped and the text recentred.
                if (tab.Icon != null)
                {
                    tab.Icon.gameObject.SetActive(false);
                }

                if (tab.Label != null)
                {
                    UiKit.Anchor(tab.Label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                }

                Tab which = (Tab)i;
                tab.Button.onClick.AddListener(() => SelectTab(which));
                _tabs[i] = tab;
            }

            // Sits on the DAILY tab and counts finished, unclaimed missions.
            _missionBadge = UiKit.CreateText(_font, "MissionBadge", root, 34, FontStyle.Bold, UiKit.Ink, TextAnchor.MiddleCenter);
            RectTransform badgeRect = _missionBadge.rectTransform;
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.sizeDelta = new Vector2(54f, 54f);
            badgeRect.anchoredPosition = new Vector2(-46f, -286f);
        }

        private RectTransform BuildUpgradesPage()
        {
            RectTransform page = UiKit.CreateChild("Upgrades", _content);
            UiKit.Stretch(page);

            int count = UpgradeCatalogue.Count;
            _upgradeRows = new UpgradeRow[count];

            for (int i = 0; i < count; i++)
            {
                UpgradeDefinition definition = UpgradeCatalogue.At(i);
                RectTransform card = CreateCard(page, i, 190f);

                UpgradeRow row = new UpgradeRow { Id = definition.Id };

                row.Name = UiKit.CreateText(_font, "Name", card, 52, FontStyle.Bold, Color.white, TextAnchor.UpperLeft);
                UiKit.Anchor(row.Name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(34f, -84f), new Vector2(-320f, -24f));
                row.Name.text = definition.Name;

                row.Detail = UiKit.CreateText(_font, "Detail", card, 34, FontStyle.Normal, UiKit.Dim, TextAnchor.UpperLeft);
                UiKit.Anchor(row.Detail.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(34f, -126f), new Vector2(-320f, -84f));
                row.Detail.text = definition.Description;

                row.Pips = new Image[MaxPips];

                for (int p = 0; p < MaxPips; p++)
                {
                    Image pip = UiKit.CreatePanel("Pip" + p, card, Color.white);
                    RectTransform pipRect = pip.rectTransform;
                    pipRect.anchorMin = new Vector2(0f, 0f);
                    pipRect.anchorMax = new Vector2(0f, 0f);
                    pipRect.pivot = new Vector2(0f, 0f);
                    pipRect.sizeDelta = new Vector2(46f, 16f);
                    pipRect.anchoredPosition = new Vector2(34f + p * 56f, 34f);

                    // Tracks are different lengths; the spare pips simply never exist.
                    pip.gameObject.SetActive(p < definition.MaxLevel);
                    row.Pips[p] = pip;
                }

                row.Buy = UiKit.CreateButton(_font, "Buy", card, string.Empty, 0, UiKit.Mint, UiKit.Ink, IconShape.None);
                RectTransform buyRect = row.Buy.Root;
                buyRect.anchorMin = new Vector2(1f, 0.5f);
                buyRect.anchorMax = new Vector2(1f, 0.5f);
                buyRect.pivot = new Vector2(1f, 0.5f);
                buyRect.sizeDelta = new Vector2(264f, 108f);
                buyRect.anchoredPosition = new Vector2(-28f, 0f);

                row.Cost = UiKit.CreateText(_font, "Cost", buyRect, 44, FontStyle.Bold, UiKit.Ink, TextAnchor.MiddleCenter, true);
                UiKit.Anchor(row.Cost.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                UpgradeId id = definition.Id;
                row.Buy.Button.onClick.AddListener(() => BuyUpgrade(id));

                _upgradeRows[i] = row;
            }

            return page;
        }

        private RectTransform BuildThemesPage()
        {
            RectTransform page = UiKit.CreateChild("Themes", _content);
            UiKit.Stretch(page);

            int count = ThemeCatalogue.Count;
            _themeRows = new ThemeRow[count];

            for (int i = 0; i < count; i++)
            {
                ThemeDefinition definition = ThemeCatalogue.At(i);
                RectTransform card = CreateCard(page, i, 132f);

                ThemeRow row = new ThemeRow { Id = definition.Id };

                // The swatch is the theme's own sky and accent side by side — the honest
                // preview, since those are the two colours the player will actually be
                // looking at for the whole run.
                Image sky = UiKit.CreatePanel("Sky", card, definition.SkyTop);
                RectTransform skyRect = sky.rectTransform;
                skyRect.anchorMin = new Vector2(0f, 0.5f);
                skyRect.anchorMax = new Vector2(0f, 0.5f);
                skyRect.pivot = new Vector2(0f, 0.5f);
                skyRect.sizeDelta = new Vector2(96f, 84f);
                skyRect.anchoredPosition = new Vector2(28f, 0f);

                Image accent = UiKit.CreatePanel("Accent", card, definition.Accent);
                RectTransform accentRect = accent.rectTransform;
                accentRect.anchorMin = new Vector2(0f, 0.5f);
                accentRect.anchorMax = new Vector2(0f, 0.5f);
                accentRect.pivot = new Vector2(0f, 0.5f);
                accentRect.sizeDelta = new Vector2(40f, 84f);
                accentRect.anchoredPosition = new Vector2(128f, 0f);
                row.Swatch = accent;

                row.Name = UiKit.CreateText(_font, "Name", card, 48, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
                UiKit.Anchor(row.Name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(196f, 0f), new Vector2(-300f, 0f));
                row.Name.text = definition.Name;

                row.Button = UiKit.CreateButton(_font, "Equip", card, string.Empty, 0, UiKit.Mint, UiKit.Ink, IconShape.None);
                RectTransform buttonRect = row.Button.Root;
                buttonRect.anchorMin = new Vector2(1f, 0.5f);
                buttonRect.anchorMax = new Vector2(1f, 0.5f);
                buttonRect.pivot = new Vector2(1f, 0.5f);
                buttonRect.sizeDelta = new Vector2(252f, 92f);
                buttonRect.anchoredPosition = new Vector2(-24f, 0f);

                row.State = UiKit.CreateText(_font, "State", buttonRect, 40, FontStyle.Bold, UiKit.Ink, TextAnchor.MiddleCenter, true);
                UiKit.Anchor(row.State.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                string id = definition.Id;
                row.Button.Button.onClick.AddListener(() => ChooseTheme(id));

                _themeRows[i] = row;
            }

            return page;
        }

        private RectTransform BuildMissionsPage()
        {
            RectTransform page = UiKit.CreateChild("Missions", _content);
            UiKit.Stretch(page);

            _missionRows = new MissionRow[MissionSystem.DailyCount];

            for (int i = 0; i < _missionRows.Length; i++)
            {
                RectTransform card = CreateCard(page, i, 200f);

                MissionRow row = new MissionRow { Index = i };

                row.Label = UiKit.CreateText(_font, "Label", card, 40, FontStyle.Bold, Color.white, TextAnchor.UpperLeft);
                UiKit.Anchor(row.Label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -76f), new Vector2(-280f, -22f));

                Image track = UiKit.CreatePanel("Track", card, new Color(1f, 1f, 1f, 0.13f));
                UiKit.Anchor(track.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(30f, 62f), new Vector2(-280f, 98f));

                Image fill = UiKit.CreatePanel("Fill", track.rectTransform, UiKit.Mint);
                RectTransform fillRect = fill.rectTransform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                row.Fill = fillRect;

                row.Progress = UiKit.CreateText(_font, "Progress", card, 32, FontStyle.Bold, UiKit.Dim, TextAnchor.LowerLeft);
                UiKit.Anchor(row.Progress.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(30f, 20f), new Vector2(-280f, 56f));

                row.Claim = UiKit.CreateButton(_font, "Claim", card, string.Empty, 0, UiKit.Gold, UiKit.Ink, IconShape.None);
                RectTransform claimRect = row.Claim.Root;
                claimRect.anchorMin = new Vector2(1f, 0.5f);
                claimRect.anchorMax = new Vector2(1f, 0.5f);
                claimRect.pivot = new Vector2(1f, 0.5f);
                claimRect.sizeDelta = new Vector2(232f, 104f);
                claimRect.anchoredPosition = new Vector2(-24f, 0f);

                row.Reward = UiKit.CreateText(_font, "Reward", claimRect, 40, FontStyle.Bold, UiKit.Ink, TextAnchor.MiddleCenter, true);
                UiKit.Anchor(row.Reward.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                int index = i;
                row.Claim.Button.onClick.AddListener(() => ClaimMission(index));

                _missionRows[i] = row;
            }

            return page;
        }

        private void BuildFooter(RectTransform root)
        {
            BuildCoinPacks(root);

            _removeAds = UiKit.CreateButton(_font, "RemoveAds", root, "REMOVE ADS", 46, UiKit.Panel, Color.white, IconShape.Bag);
            RectTransform adsRect = _removeAds.Root;
            adsRect.anchorMin = new Vector2(0f, 0f);
            adsRect.anchorMax = new Vector2(1f, 0f);
            adsRect.pivot = new Vector2(0.5f, 0f);
            adsRect.offsetMin = new Vector2(40f, 118f);
            adsRect.offsetMax = new Vector2(-40f, 226f);
            _removeAds.Button.onClick.AddListener(() => RemoveAdsRequested?.Invoke());

            _restore = UiKit.CreateButton(_font, "Restore", root, "RESTORE PURCHASES", 32, new Color(1f, 1f, 1f, 0.08f), UiKit.Dim, IconShape.None);
            MenuControl restore = _restore;
            RectTransform restoreRect = restore.Root;
            restoreRect.anchorMin = new Vector2(0f, 0f);
            restoreRect.anchorMax = new Vector2(1f, 0f);
            restoreRect.pivot = new Vector2(0.5f, 0f);
            restoreRect.offsetMin = new Vector2(40f, 36f);
            restoreRect.offsetMax = new Vector2(-40f, 106f);

            if (restore.Label != null)
            {
                UiKit.Anchor(restore.Label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }

            restore.Button.onClick.AddListener(() => RestoreRequested?.Invoke());
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
                amount.text = CoinPackAmounts[i].ToString();

                Text price = UiKit.CreateText(_font, "PackPrice" + i, rect, 30, FontStyle.Bold, UiKit.Gold, TextAnchor.LowerCenter);
                UiKit.Anchor(price.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 10f), new Vector2(0f, 46f));
                price.text = "COINS";
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

        /// <summary>One row of the list, stacked from the top of the content area.</summary>
        private RectTransform CreateCard(Transform parent, int index, float height)
        {
            const float gap = 18f;

            Image card = UiKit.CreatePanel("Card" + index, parent, new Color(1f, 1f, 1f, 0.07f));

            RectTransform rect = card.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -(index + 1) * height - index * gap);
            rect.offsetMax = new Vector2(0f, -index * (height + gap));

            return rect;
        }

        private void SelectTab(Tab tab)
        {
            _tab = tab;

            for (int i = 0; i < _pages.Length; i++)
            {
                if (_pages[i] != null)
                {
                    _pages[i].gameObject.SetActive(i == (int)tab);
                }

                if (_tabs[i] != null)
                {
                    Image background = _tabs[i].Button.targetGraphic as Image;

                    if (background != null)
                    {
                        background.color = i == (int)tab ? UiKit.Mint : UiKit.Panel;
                    }

                    if (_tabs[i].Label != null)
                    {
                        _tabs[i].Label.color = i == (int)tab ? UiKit.Ink : Color.white;
                    }
                }
            }

            Refresh();
        }

        public void Show()
        {
            MissionSystem.EnsureToday();

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

        private void BuyUpgrade(UpgradeId id)
        {
            bool bought = UpgradeCatalogue.TryPurchase(id);
            PurchaseResolved?.Invoke(bought);
            Refresh();
        }

        private void ChooseTheme(string id)
        {
            if (ThemeCatalogue.IsOwned(id))
            {
                // Owned already: this is an equip, which always succeeds and is not a
                // purchase — reporting it as one would play the coin sound for free.
                PlayerProfile.EquipTheme(id);
                PurchaseResolved?.Invoke(true);
            }
            else
            {
                PurchaseResolved?.Invoke(ThemeCatalogue.TryPurchase(id));
            }

            Refresh();
        }

        private void ClaimMission(int index)
        {
            int reward = MissionSystem.TryClaim(index);
            PurchaseResolved?.Invoke(reward > 0);
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
            _coinLabel.text = coins.ToString();

            int streak = PlayerProfile.Data.dailyStreak;
            _streakLabel.text = streak > 1 ? "DAY STREAK " + streak : string.Empty;

            RefreshUpgrades(coins);
            RefreshThemes(coins);
            RefreshMissions();

            int claimable = MissionSystem.ClaimableCount();
            _missionBadge.text = claimable > 0 ? claimable.ToString() : string.Empty;
            _missionBadge.color = claimable > 0 ? UiKit.Gold : Color.clear;

            if (_removeAds != null && _storeAvailable)
            {
                bool removed = PlayerProfile.AdsRemoved;
                _removeAds.Button.interactable = !removed;

                if (_removeAds.Label != null)
                {
                    _removeAds.Label.text = removed ? "ADS REMOVED" : "REMOVE ADS";
                    _removeAds.Label.color = removed ? UiKit.Mint : Color.white;
                }
            }
        }

        private void RefreshUpgrades(int coins)
        {
            for (int i = 0; i < _upgradeRows.Length; i++)
            {
                UpgradeRow row = _upgradeRows[i];
                int level = PlayerProfile.GetUpgradeLevel(row.Id);
                int max = UpgradeCatalogue.MaxLevel(row.Id);
                bool maxed = level >= max;
                int cost = UpgradeCatalogue.CostOfNext(row.Id);

                for (int p = 0; p < row.Pips.Length; p++)
                {
                    if (p >= max)
                    {
                        continue;
                    }

                    row.Pips[p].color = p < level ? UiKit.Mint : new Color(1f, 1f, 1f, 0.16f);
                }

                bool affordable = !maxed && coins >= cost;

                row.Cost.text = maxed ? "MAX" : cost.ToString();
                row.Buy.Button.interactable = affordable;

                Image background = row.Buy.Button.targetGraphic as Image;

                if (background != null)
                {
                    background.color = maxed
                        ? new Color(1f, 1f, 1f, 0.12f)
                        : affordable ? UiKit.Mint : new Color(1f, 1f, 1f, 0.18f);
                }

                // On a dark disabled panel the ink label would be unreadable, so the two
                // states swap foreground as well as background.
                row.Cost.color = affordable ? UiKit.Ink : new Color(1f, 1f, 1f, 0.55f);
            }
        }

        private void RefreshThemes(int coins)
        {
            string equipped = ThemeCatalogue.Equipped.Id;

            for (int i = 0; i < _themeRows.Length; i++)
            {
                ThemeRow row = _themeRows[i];
                ThemeDefinition definition = ThemeCatalogue.Get(row.Id);

                bool owned = ThemeCatalogue.IsOwned(row.Id);
                bool active = row.Id == equipped;
                bool affordable = owned || coins >= definition.Price;

                if (active)
                {
                    row.State.text = "ACTIVE";
                }
                else if (owned)
                {
                    row.State.text = "EQUIP";
                }
                else
                {
                    row.State.text = definition.Price.ToString();
                }

                row.Button.Button.interactable = !active && affordable;

                Image background = row.Button.Button.targetGraphic as Image;

                if (background != null)
                {
                    background.color = active
                        ? new Color(1f, 1f, 1f, 0.12f)
                        : affordable ? UiKit.Mint : new Color(1f, 1f, 1f, 0.18f);
                }

                row.State.color = !active && affordable ? UiKit.Ink : new Color(1f, 1f, 1f, 0.6f);
                row.Name.color = owned ? Color.white : new Color(1f, 1f, 1f, 0.7f);
            }
        }

        private void RefreshMissions()
        {
            int count = MissionSystem.Count;

            for (int i = 0; i < _missionRows.Length; i++)
            {
                MissionRow row = _missionRows[i];
                bool exists = i < count;

                row.Label.transform.parent.gameObject.SetActive(exists);

                if (!exists)
                {
                    continue;
                }

                MissionDefinition definition = MissionSystem.At(i);
                int progress = MissionSystem.ProgressAt(i);
                bool complete = MissionSystem.IsComplete(i);
                bool claimed = MissionSystem.IsClaimed(i);

                row.Label.text = definition.Text;
                row.Progress.text = claimed ? "CLAIMED" : progress + " / " + definition.Target;

                float ratio = definition.Target > 0
                    ? Mathf.Clamp01(progress / (float)definition.Target)
                    : 0f;

                row.Fill.anchorMax = new Vector2(ratio, 1f);

                row.Reward.text = claimed ? "DONE" : "+" + definition.Reward;
                row.Claim.Button.interactable = complete && !claimed;

                Image background = row.Claim.Button.targetGraphic as Image;

                if (background != null)
                {
                    background.color = complete && !claimed ? UiKit.Gold : new Color(1f, 1f, 1f, 0.16f);
                }

                row.Reward.color = complete && !claimed ? UiKit.Ink : new Color(1f, 1f, 1f, 0.55f);
            }
        }

        private void Update()
        {
            if (_group == null)
            {
                return;
            }

            // Unscaled: the shop opens from the run-over screen, where the death slow-motion
            // still owns Time.timeScale.
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, Time.unscaledDeltaTime * 6f);
        }
    }
}
