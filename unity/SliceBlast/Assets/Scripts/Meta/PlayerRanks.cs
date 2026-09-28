// A builder's title, earned from lifetime score. Purely a mark of progress: it pays no coins
// and changes nothing about how the game plays, so it can never unbalance the economy — it
// just gives every run something that still counts once the shop has been bought out.
using UnityEngine;

namespace SliceBlast.Meta
{
    public static class PlayerRanks
    {
        private static readonly string[] Names =
        {
            "ROOKIE",
            "STACKER",
            "BUILDER",
            "ENGINEER",
            "ARCHITECT",
            "SKY SHAPER",
            "TOWER TITAN",
            "LEGEND"
        };

        // Lifetime points needed for each title. A solid run scores a couple of thousand, so
        // the first few arrive within a session and LEGEND is weeks of play.
        private static readonly long[] Thresholds = { 0, 2000, 6000, 15000, 35000, 75000, 150000, 300000 };

        public static int Count => Names.Length;

        public static int IndexFor(long lifetimeScore)
        {
            int index = 0;

            while (index + 1 < Thresholds.Length && lifetimeScore >= Thresholds[index + 1])
            {
                index++;
            }

            return index;
        }

        public static string Name(int index)
        {
            return Loc.T("rank." + Mathf.Clamp(index, 0, Names.Length - 1));
        }

        public static long Threshold(int index)
        {
            return Thresholds[Mathf.Clamp(index, 0, Thresholds.Length - 1)];
        }

        public static bool IsHighest(int index)
        {
            return index >= Names.Length - 1;
        }
    }
}
