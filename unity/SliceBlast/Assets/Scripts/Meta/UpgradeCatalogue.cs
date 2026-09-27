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
        public string Name;
        public string Description;
        public int MaxLevel;
    }

    public static class UpgradeCatalogue
    {
        private static readonly UpgradeDefinition[] Definitions =
        {
            new UpgradeDefinition
            {
                Id = UpgradeId.Magnet,
                Name = "MAGNET",
                Description = "Wider perfect window",
                MaxLevel = 7
            },
            new UpgradeDefinition
            {
                Id = UpgradeId.Shield,
                Name = "ARMOUR",
                Description = "Start every run shielded",
                MaxLevel = 3
            },
            new UpgradeDefinition
            {
                Id = UpgradeId.Luck,
                Name = "FORTUNE",
                Description = "Specials arrive sooner",
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
        private static readonly int[] ShieldCosts = { 600, 1500, 3000 };
        private static readonly int[] LuckCosts = { 250, 700, 1500, 2800 };

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

        /// <summary>
        /// Extra share of the reference block added to the perfect window. Capped well below
        /// BlockSlicer's own maxThresholdFraction so a fully upgraded magnet still cannot make
        /// a late-game sliver placement automatic.
        /// </summary>
        public static float MagnetBonusFraction()
        {
            return 0.0025f * PlayerProfile.GetUpgradeLevel(UpgradeId.Magnet);
        }

        /// <summary>Shields a run opens with.</summary>
        public static int StartingShields()
        {
            return PlayerProfile.GetUpgradeLevel(UpgradeId.Shield);
        }

        /// <summary>Blocks shaved off the random gap between specials.</summary>
        public static int SpecialGapReduction()
        {
            return 2 * PlayerProfile.GetUpgradeLevel(UpgradeId.Luck);
        }
    }
}
