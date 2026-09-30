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
                MaxLevel = 2
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
        private static readonly int[] ShieldCosts = { 1000, 2500 };
        private static readonly int[] LuckCosts = { 400, 1100, 2400, 4500 };

        // Armour used to go to three shields, which is three free misses in every run. It now
        // stops at two. What the old third level cost is listed here so anyone who had already
        // bought it is paid back (see RefundForRemovedLevels) rather than left with a level
        // that no longer exists.
        private static readonly int[] ShieldRemovedCosts = { 5000 };

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

        // A maxed Magnet makes the perfect window 30% wider. It was 70% (0.003 a level), which
        // players read as a hack: near-misses that should have sliced were landing perfect.
        // Thirty percent is still felt on every level, and no longer decides the game.
        private const float MagnetPerLevel = 0.0013f;

        // Mirrors BlockSlicer.magnetFraction, the base perfect window as a share of the block.
        // Only used to put Magnet's bonus into words the Workshop can show ("+30% wider").
        private const float BaseMagnetWindow = 0.03f;

        /// <summary>
        /// Extra share of the reference block added to the perfect window. Capped well below
        /// BlockSlicer's own maxThresholdFraction so a fully upgraded magnet still cannot make
        /// a late-game sliver placement automatic.
        /// </summary>
        public static float MagnetBonusFraction()
        {
            return MagnetBonusAt(PlayerProfile.GetUpgradeLevel(UpgradeId.Magnet));
        }

        public static float MagnetBonusAt(int level)
        {
            return MagnetPerLevel * Mathf.Max(0, level);
        }

        /// <summary>How much wider than the base window Magnet makes it at a level, in percent.</summary>
        public static int MagnetWindowPercent(int level)
        {
            return Mathf.RoundToInt(MagnetBonusAt(level) / BaseMagnetWindow * 100f);
        }

        /// <summary>Shields a run opens with.</summary>
        public static int StartingShields()
        {
            return StartingShieldsAt(PlayerProfile.GetUpgradeLevel(UpgradeId.Shield));
        }

        public static int StartingShieldsAt(int level)
        {
            // Capped: a profile saved while Armour still had a third level can hold a 3 here.
            return Mathf.Clamp(level, 0, MaxLevel(UpgradeId.Shield));
        }

        /// <summary>
        /// Coins owed to a profile that holds more levels of a track than it now has — the
        /// listed price of each level that was taken away.
        /// </summary>
        public static int RefundForRemovedLevels(UpgradeId id, int ownedLevel)
        {
            if (id != UpgradeId.Shield)
            {
                return 0;
            }

            int refund = 0;

            for (int level = MaxLevel(id); level < ownedLevel; level++)
            {
                int index = level - MaxLevel(id);

                if (index >= 0 && index < ShieldRemovedCosts.Length)
                {
                    refund += ShieldRemovedCosts[index];
                }
            }

            return refund;
        }

        /// <summary>Blocks shaved off the random gap between specials: one a level (it was two).</summary>
        public static int SpecialGapReduction()
        {
            return SpecialGapReductionAt(PlayerProfile.GetUpgradeLevel(UpgradeId.Luck));
        }

        public static int SpecialGapReductionAt(int level)
        {
            return Mathf.Max(0, level);
        }
    }
}
