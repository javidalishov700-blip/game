// Every word the game shows, in English, Turkish and Russian. One table in code rather than
// asset files, for the same reason the whole interface is built in code: nothing to import,
// nothing for the build to lose.
//
// Strings are already upper-case where the UI shows them upper-case — Turkish in particular
// cannot be upper-cased mechanically (i → İ, ı → I), so the table spells each one out.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SliceBlast.Meta
{
    public enum Language : byte
    {
        English = 0,
        Turkish = 1,
        Russian = 2
    }

    public static class Loc
    {
        public const int LanguageCount = 3;

        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>(160);

        private static bool _resolved;
        private static Language _current;

        /// <summary>Raised after the language changes, so screens can rewrite their words.</summary>
        public static event Action Changed;

        public static Language Current
        {
            get
            {
                if (!_resolved)
                {
                    Resolve();
                }

                return _current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Changed = null;
            _resolved = false;
        }

        /// <summary>The player's own choice if they made one, otherwise the phone's language.</summary>
        private static void Resolve()
        {
            _resolved = true;
            int saved = PlayerProfile.Data.language;

            if (saved >= 0 && saved < LanguageCount)
            {
                _current = (Language)saved;
                return;
            }

            switch (Application.systemLanguage)
            {
                case SystemLanguage.Turkish:
                    _current = Language.Turkish;
                    break;

                case SystemLanguage.Russian:
                case SystemLanguage.Ukrainian:
                case SystemLanguage.Belarusian:
                    _current = Language.Russian;
                    break;

                default:
                    _current = Language.English;
                    break;
            }
        }

        public static void Set(Language language)
        {
            PlayerProfile.SetLanguage((int)language);

            if (_resolved && _current == language)
            {
                return;
            }

            _resolved = true;
            _current = language;
            Changed?.Invoke();
        }

        /// <summary>A language's name written in that language — what its picker button says.</summary>
        public static string NativeName(Language language)
        {
            switch (language)
            {
                case Language.Turkish:
                    return "TÜRKÇE";

                case Language.Russian:
                    return "РУССКИЙ";

                default:
                    return "ENGLISH";
            }
        }

        /// <summary>The word for a key in the current language, falling back to English, then the key.</summary>
        public static string T(string key)
        {
            if (key == null)
            {
                return string.Empty;
            }

            if (!Table.TryGetValue(key, out string[] row))
            {
                return key;
            }

            string text = row[(int)Current];
            return string.IsNullOrEmpty(text) ? row[0] : text;
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, T(key), args);
        }

        private static void Add(string key, string english, string turkish, string russian)
        {
            Table[key] = new[] { english, turkish, russian };
        }

        static Loc()
        {
            // ---- Play -----------------------------------------------------------------------
            Add("hud.tap_to_drop", "TAP TO DROP", "BIRAKMAK İÇİN DOKUN", "НАЖМИ, ЧТОБЫ БРОСИТЬ");
            Add("hud.more_for_blast", "{0} MORE FOR BLAST", "PATLAMAYA {0} KALDI", "ЕЩЁ {0} ДО ВЗРЫВА");
            Add("hud.blast", "BLAST!", "PATLAMA!", "ВЗРЫВ!");
            Add("hud.missed", "MISSED!", "KAÇTI!", "ПРОМАХ!");
            Add("hud.lost", "{0} LOST", "{0} KAYBOLDU", "ПОТЕРЯНО: {0}");
            Add("hud.neon_blast", "NEON BLAST", "NEON PATLAMA", "НЕОН-ВЗРЫВ");
            Add("hud.block_blast", "{0} BLOCK BLAST", "{0} BLOK PATLAMA", "ВЗРЫВ {0} БЛОКОВ");
            Add("hud.combo", "COMBO x{0}", "KOMBO x{0}", "КОМБО x{0}");
            Add("hud.perfect", "PERFECT x{0}", "MÜKEMMEL x{0}", "ИДЕАЛЬНО x{0}");

            // ---- Pause sheet ------------------------------------------------------------------
            Add("hud.paused", "PAUSED", "DURAKLATILDI", "ПАУЗА");
            Add("hud.resume", "RESUME", "DEVAM", "ПРОДОЛЖИТЬ");
            Add("hud.replay", "REPLAY", "TEKRAR", "ЗАНОВО");
            Add("hud.home", "HOME", "ANA MENÜ", "МЕНЮ");
            Add("hud.sound", "SOUND", "SES", "ЗВУК");
            Add("hud.muted", "MUTED", "SESSİZ", "БЕЗ ЗВУКА");
            Add("hud.vibration", "VIBRATION", "TİTREŞİM", "ВИБРАЦИЯ");
            Add("hud.no_vibration", "NO VIBRATION", "TİTREŞİM KAPALI", "БЕЗ ВИБРАЦИИ");
            Add("hud.privacy_choices", "PRIVACY CHOICES", "GİZLİLİK SEÇENEKLERİ", "КОНФИДЕНЦИАЛЬНОСТЬ");

            // ---- Title and run-over screens ---------------------------------------------------
            Add("hud.tap_to_start", "TAP TO START", "BAŞLAMAK İÇİN DOKUN", "НАЖМИ, ЧТОБЫ ИГРАТЬ");
            Add("hud.workshop", "WORKSHOP", "ATÖLYE", "МАСТЕРСКАЯ");
            Add("hud.run_over", "RUN OVER", "OYUN BİTTİ", "ИГРА ОКОНЧЕНА");
            Add("hud.play_again", "PLAY AGAIN", "TEKRAR OYNA", "ИГРАТЬ СНОВА");
            Add("hud.continue", "CONTINUE", "DEVAM ET", "ПРОДОЛЖИТЬ");
            Add("hud.tap_anywhere", "OR TAP ANYWHERE", "YA DA HERHANGİ BİR YERE DOKUN", "ИЛИ НАЖМИ В ЛЮБОМ МЕСТЕ");
            Add("hud.new_best", "NEW BEST!", "YENİ REKOR!", "НОВЫЙ РЕКОРД!");
            Add("hud.run_coins", "+{0} COINS", "+{0} ALTIN", "МОНЕТЫ: +{0}");
            Add("legal.privacy", "PRIVACY POLICY", "GİZLİLİK POLİTİKASI", "КОНФИДЕНЦИАЛЬНОСТЬ");
            Add("legal.terms", "TERMS OF USE", "KULLANIM ŞARTLARI", "УСЛОВИЯ");

            // ---- Settings ---------------------------------------------------------------------
            Add("settings.title", "SETTINGS", "AYARLAR", "НАСТРОЙКИ");
            Add("settings.language", "LANGUAGE", "DİL", "ЯЗЫК");

            // ---- Leaderboard ------------------------------------------------------------------
            Add("board.title", "TOP 99", "İLK 99", "ТОП-99");
            Add("board.subtitle", "ALL-TIME BEST SCORES", "TÜM ZAMANLARIN EN İYİLERİ", "ЛУЧШИЕ РЕЗУЛЬТАТЫ");
            Add("board.your_rank", "YOUR RANK", "SENİN SIRAN", "ВАШЕ МЕСТО");
            Add("board.loading", "LOADING…", "YÜKLENİYOR…", "ЗАГРУЗКА…");
            Add("board.error", "COULDN'T REACH GAME CENTER.\nTRY AGAIN IN A MOMENT.", "GAME CENTER'A ULAŞILAMADI.\nBİRAZ SONRA TEKRAR DENE.", "НЕТ СВЯЗИ С GAME CENTER.\nПОПРОБУЙТЕ ПОЗЖЕ.");
            Add("board.empty", "NO SCORES YET.\nPLAY A RUN AND TAKE FIRST PLACE.", "HENÜZ SKOR YOK.\nBİR OYUN OYNA, ZİRVEYE YERLEŞ.", "ПОКА НЕТ РЕЗУЛЬТАТОВ.\nСЫГРАЙТЕ И ЗАЙМИТЕ ПЕРВОЕ МЕСТО.");
            Add("board.player", "PLAYER", "OYUNCU", "ИГРОК");
            Add("board.you", "YOU", "SEN", "ВЫ");

            // ---- Workshop ---------------------------------------------------------------------
            Add("shop.tab.upgrades", "UPGRADES", "GELİŞTİR", "УЛУЧШЕНИЯ");
            Add("shop.tab.themes", "THEMES", "TEMALAR", "ТЕМЫ");
            Add("shop.tab.profile", "PROFILE", "PROFİL", "ПРОФИЛЬ");
            Add("shop.tab.daily", "DAILY", "GÜNLÜK", "ЗАДАНИЯ");
            Add("shop.streak", "STREAK {0}", "SERİ {0}", "СЕРИЯ {0}");
            Add("shop.max", "MAX", "MAKS", "МАКС");
            Add("shop.active", "ACTIVE", "AKTİF", "АКТИВНА");
            Add("shop.equip", "EQUIP", "KULLAN", "ВЫБРАТЬ");
            Add("shop.claimed", "CLAIMED", "ALINDI", "ПОЛУЧЕНО");
            Add("shop.done", "DONE", "TAMAM", "ГОТОВО");
            Add("shop.reset", "NEW MISSIONS IN {0}H {1}M", "YENİ GÖREVLER: {0}S {1}DK", "НОВЫЕ ЗАДАНИЯ ЧЕРЕЗ {0} Ч {1} МИН");
            Add("shop.remove_ads", "REMOVE ADS", "REKLAMLARI KALDIR", "УБРАТЬ РЕКЛАМУ");
            Add("shop.ads_removed", "ADS REMOVED", "REKLAMLAR KALDIRILDI", "РЕКЛАМА ОТКЛЮЧЕНА");
            Add("shop.restore", "RESTORE PURCHASES", "SATIN ALIMLARI GERİ YÜKLE", "ВОССТАНОВИТЬ ПОКУПКИ");
            Add("shop.coins", "COINS", "ALTIN", "МОНЕТЫ");
            Add("shop.rank_caption", "BUILDER RANK", "USTALIK RÜTBESİ", "РАНГ СТРОИТЕЛЯ");
            Add("shop.rank_top", "HIGHEST RANK REACHED", "EN YÜKSEK RÜTBEDESİN", "ВЫСШИЙ РАНГ");
            Add("shop.rank_next", "NEXT: {0}", "SIRADAKİ: {0}", "ДАЛЕЕ: {0}");
            Add("shop.points_of", "{0} / {1} POINTS", "{0} / {1} PUAN", "{0} / {1} ОЧКОВ");
            Add("shop.points_total", "{0} LIFETIME POINTS", "TOPLAM {0} PUAN", "ВСЕГО ОЧКОВ: {0}");

            Add("stat.best", "BEST SCORE", "EN İYİ SKOR", "РЕКОРД");
            Add("stat.runs", "RUNS PLAYED", "OYNANAN OYUN", "СЫГРАНО ИГР");
            Add("stat.blasts", "BLASTS", "PATLAMALAR", "ВЗРЫВЫ");
            Add("stat.biggest", "BIGGEST BLAST", "EN BÜYÜK PATLAMA", "МАКС. ВЗРЫВ");
            Add("stat.streak", "DAY STREAK", "GÜN SERİSİ", "ДНЕЙ ПОДРЯД");
            Add("stat.collection", "COLLECTION", "KOLEKSİYON", "КОЛЛЕКЦИЯ");

            Add("effect.magnet", "WINDOW +{0}% → +{1}%", "ALAN +{0}% → +{1}%", "ЗОНА +{0}% → +{1}%");
            Add("effect.magnet.max", "WINDOW +{0}% · MAX", "ALAN +{0}% · MAKS", "ЗОНА +{0}% · МАКС");
            Add("effect.shield", "{0} → {1} SHIELDS", "{0} → {1} KALKAN", "ЩИТЫ: {0} → {1}");
            Add("effect.shield.one", "{0} → {1} SHIELD", "{0} → {1} KALKAN", "ЩИТЫ: {0} → {1}");
            Add("effect.shield.max", "{0} SHIELDS · MAX", "{0} KALKAN · MAKS", "ЩИТЫ: {0} · МАКС");
            Add("effect.luck", "{0} → {1} BLOCKS SOONER", "{0} → {1} BLOK ERKEN", "НА {0} → {1} БЛОКОВ РАНЬШЕ");
            Add("effect.luck.max", "{0} BLOCKS SOONER · MAX", "{0} BLOK ERKEN · MAKS", "НА {0} БЛОКОВ РАНЬШЕ · МАКС");

            Add("upgrade.magnet", "MAGNET", "MIKNATIS", "МАГНИТ");
            Add("upgrade.magnet.desc", "Wider perfect window", "Daha geniş mükemmel alan", "Шире зона идеального броска");
            Add("upgrade.shield", "ARMOUR", "ZIRH", "БРОНЯ");
            Add("upgrade.shield.desc", "Start every run shielded", "Her oyuna kalkanla başla", "Щит в начале каждой игры");
            Add("upgrade.luck", "FORTUNE", "ŞANS", "УДАЧА");
            Add("upgrade.luck.desc", "Specials arrive sooner", "Özel bloklar daha erken gelir", "Особые блоки приходят раньше");

            Add("theme.midnight", "MIDNIGHT", "GECE YARISI", "ПОЛНОЧЬ");
            Add("theme.ember", "EMBER", "KOR", "УГЛИ");
            Add("theme.vapor", "VAPOUR", "BUHAR", "ПАР");
            Add("theme.signal", "SIGNAL", "SİNYAL", "СИГНАЛ");
            Add("theme.mono", "MONOLITH", "MONOLİT", "МОНОЛИТ");
            Add("theme.aurora", "AURORA", "KUTUP IŞIĞI", "СИЯНИЕ");
            Add("theme.abyss", "ABYSS", "DERİNLİK", "БЕЗДНА");
            Add("theme.sakura", "SAKURA", "SAKURA", "САКУРА");
            Add("theme.gilded", "GILDED", "YALDIZ", "ЗОЛОТО");

            Add("rank.0", "ROOKIE", "ÇAYLAK", "НОВИЧОК");
            Add("rank.1", "STACKER", "İSTİFÇİ", "УКЛАДЧИК");
            Add("rank.2", "BUILDER", "İNŞAATÇI", "СТРОИТЕЛЬ");
            Add("rank.3", "ENGINEER", "MÜHENDİS", "ИНЖЕНЕР");
            Add("rank.4", "ARCHITECT", "MİMAR", "АРХИТЕКТОР");
            Add("rank.5", "SKY SHAPER", "GÖK USTASI", "ПОКОРИТЕЛЬ НЕБА");
            Add("rank.6", "TOWER TITAN", "KULE DEVİ", "ТИТАН БАШЕН");
            Add("rank.7", "LEGEND", "EFSANE", "ЛЕГЕНДА");

            // Keyed by MissionKind name; every target is large enough that Russian's "many"
            // plural form is the only one ever needed.
            Add("mission.RunScore", "SCORE {0} IN ONE RUN", "TEK OYUNDA {0} SKOR YAP", "НАБЕРИ {0} ОЧКОВ ЗА ИГРУ");
            Add("mission.Perfects", "LAND {0} PERFECT DROPS", "{0} MÜKEMMEL BIRAKIŞ YAP", "СДЕЛАЙ {0} ИДЕАЛЬНЫХ БРОСКОВ");
            Add("mission.Blasts", "TRIGGER {0} BLASTS", "{0} PATLAMA YAP", "УСТРОЙ {0} ВЗРЫВОВ");
            Add("mission.Specials", "LAND {0} SPECIAL BLOCKS", "{0} ÖZEL BLOK YERLEŞTİR", "ПОСТАВЬ {0} ОСОБЫХ БЛОКОВ");

            // Keyed by BlockType name, for the "… LOST" banner.
            Add("block.Standard", "BLOCK", "BLOK", "БЛОК");
            Add("block.Neon", "NEON", "NEON", "НЕОН");
            Add("block.Electric", "ELECTRIC", "ELEKTRİK", "ЭЛЕКТРО");
            Add("block.Glass", "GLASS", "CAM", "СТЕКЛО");
            Add("block.Steel", "STEEL", "ÇELİK", "СТАЛЬ");
        }
    }
}
