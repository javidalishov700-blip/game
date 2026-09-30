// The three permanent upgrades and what each level actually does. Every effect here is read
// live by gameplay (BlockSlicer, BlockSpawner, GameFlowManager) rather than copied into a
// run at start, so a purchase made on the run-over screen is in force on the very next tap.
//
// Deliberately only three tracks, each short. A long upgrade tree in a game whose whole loop
// is one tap turns the shop into the game; these exist to make a returning player's tower
// feel measurably kinder than it did on day one, and then stop.
using UnityEngine;

namespace SliceBlast.Meta
{
    public struct UpgradeDefinition
    {
        public UpgradeId Id;

        /// <summary>Stem of the Loc keys for this track's name ("upgrade.KEY") and blurb ("upgrade.KEY.desc").</summary>
        public string Key;
        public int MaxLevel;
    }

    public static class UpgradeCatalogue
    {
        private static readonly UpgradeDefinition[] Definitions =
        {
            new UpgradeDefinition
            {
                Id = UpgradeId.Magnet,
                Key = "magnet",
                MaxLevel = 7
            },
            new UpgradeDefinition
            {
                Id = UpgradeId.Shield,
                Key = "shield",
                MaxLevel = 3
            },
            new UpgradeDefinition
            {
                Id = UpgradeId.Luck,
                Key = "luck",
                MaxLevel = 4
            }
        };

        // Level 1 is cheap enough to be the first thing a player buys with one good run's
        // coins; the top of each track costs several sessions. Magnet is the exception on
        // both counts: at the old 5 levels and a flat 0.006 per level, a maxed Magnet nearly
        // doubled the perfect window on its own — indistinguishable from cheating rather than
        // an upgrade. Two more levels spread the same ceiling thinner, and the price of
        // reaching it now runs several times the cost of maxing Shield or Fortune outright.
        private static readonly int[] MagnetCosts = { 300, 900, 2200, 4500, 8500, 15000, 26000 };
        private static readonly int[] ShieldCosts = { 1000, 2500, 5000 };
        private static readonly int[] LuckCosts = { 400, 1100, 2400, 4500 };

        public static int Count => Definitions.Length;

        public static UpgradeDefinition At(int index)
        {
            return Definitions[Mathf.Clamp(index, 0, Definitions.Length - 1)];
        }

        public static UpgradeDefinition Get(UpgradeId id)
        {
            int index = (int)id;
            return index >= 0 && index < Definitions.Length ? Definitions[index] : Definitions[0];
        }

        public static int MaxLevel(UpgradeId id) => Get(id).MaxLevel;

        /// <summary>Cost of the next level, or 0 when the track is already maxed.</summary>
        public static int CostOfNext(UpgradeId id)
        {
            int level = PlayerProfile.GetUpgradeLevel(id);
            int[] costs = CostsFor(id);

            return level >= 0 && level < costs.Length ? costs[level] : 0;
        }

        public static bool IsMaxed(UpgradeId id)
        {
            return PlayerProfile.GetUpgradeLevel(id) >= MaxLevel(id);
        }

        public static bool TryPurchase(UpgradeId id)
        {
            if (IsMaxed(id))
            {
                return false;
            }

            int cost = CostOfNext(id);

            if (cost <= 0 || !PlayerProfile.TrySpend(cost))
            {
                return false;
            }

            PlayerProfile.SetUpgradeLevel(id, PlayerProfile.GetUpgradeLevel(id) + 1);
            return true;
        }

        private static int[] CostsFor(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.Shield:
                    return ShieldCosts;

                case UpgradeId.Luck:
                    return LuckCosts;

                default:
                    return MagnetCosts;
            }
        }

        // ---- Live effect queries, read by gameplay ------------------------------------

        // A maxed Magnet makes the perfect window 45% wider, at every block size. It was 70%,
        // which players read as a hack — and because it was added on top of a fixed floor, it
        // did nothing at all for small blocks while handing big ones a +56% window. 30% was
        // tried and read as too little; 45% is felt on every level without deciding the game.
        private const float MagnetMaxWindowBonus = 0.45f;

        /// <summary>
        /// How much wider the perfect window is, as a fraction of the base window (0.3 is 30%
        /// wider). Read live by BlockSlicer, at the level the player has chosen to run with.
        /// </summary>
        public static float MagnetWindowBonus()
        {
            return MagnetWindowBonusAt(PlayerProfile.GetActiveUpgradeLevel(UpgradeId.Magnet));
        }

        public static float MagnetWindowBonusAt(int level)
        {
            return MagnetMaxWindowBonus * Mathf.Clamp01(level / (float)MaxLevel(UpgradeId.Magnet));
        }

        /// <summary>How much wider than the base window Magnet makes it at a level, in percent.</summary>
        public static int MagnetWindowPercent(int level)
        {
            return Mathf.RoundToInt(MagnetWindowBonusAt(level) * 100f);
        }

        /// <summary>Shields a run opens with.</summary>
        public static int StartingShields()
        {
            return StartingShieldsAt(PlayerProfile.GetActiveUpgradeLevel(UpgradeId.Shield));
        }

        public static int StartingShieldsAt(int level)
        {
            return Mathf.Max(0, level);
        }

        /// <summary>Blocks shaved off the random gap between specials.</summary>
        public static int SpecialGapReduction()
        {
            return SpecialGapReductionAt(PlayerProfile.GetActiveUpgradeLevel(UpgradeId.Luck));
        }

        public static int SpecialGapReductionAt(int level)
        {
            return 2 * Mathf.Max(0, level);
        }
    }
}
