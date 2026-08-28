using UnityEngine;

namespace SliceBlast.Core
{
    public enum BlockType : byte
    {
        Standard = 0,
        Neon = 1,
        Electric = 2,
        Glass = 3,
        Steel = 4
    }

    public struct BlockDefinition
    {
        public BlockType Type;
        public Color Tint;
        public float SpeedMultiplier;
        public float SpawnWeight;
        public bool UsesPalette;
        public string Label;
    }

    /// <summary>Single source of truth for how each block looks, moves and how often it shows up.</summary>
    public static class BlockCatalogue
    {
        /// <summary>Electric blue — the block, its arcs and the current it sends up the tower.</summary>
        public static readonly Color ElectricBlue = new Color(0.45f, 0.85f, 1f);

        private static readonly BlockDefinition[] Definitions =
        {
            new BlockDefinition
            {
                Type = BlockType.Standard,
                Tint = Color.white,
                SpeedMultiplier = 1f,
                SpawnWeight = 0f, // never in the lottery: standard is what fills the gaps
                UsesPalette = true,
                Label = "STANDARD"
            },
            new BlockDefinition
            {
                Type = BlockType.Neon,
                // No signature colour: the spawner rolls a new neon hue for every one of these.
                Tint = Color.white,
                SpeedMultiplier = 1f,
                SpawnWeight = 3f,
                UsesPalette = false,
                Label = "NEON"
            },
            new BlockDefinition
            {
                Type = BlockType.Electric,
                Tint = new Color(0.45f, 0.85f, 1f),
                SpeedMultiplier = 1f,
                SpawnWeight = 3f,
                UsesPalette = false,
                Label = "ELECTRIC"
            },
            new BlockDefinition
            {
                Type = BlockType.Glass,
                Tint = new Color(0.78f, 0.97f, 1f),
                SpeedMultiplier = 1f,
                SpawnWeight = 2f,
                UsesPalette = false,
                Label = "GLASS"
            },
            new BlockDefinition
            {
                Type = BlockType.Steel,
                Tint = new Color(0.66f, 0.70f, 0.78f),
                SpeedMultiplier = 1f,
                SpawnWeight = 2f,
                UsesPalette = false,
                Label = "STEEL"
            }
        };

        public static BlockDefinition Get(BlockType type)
        {
            int index = (int)type;
            return index >= 0 && index < Definitions.Length ? Definitions[index] : Definitions[0];
        }

        public static int Count => Definitions.Length;

        /// <summary>Index 0 is Standard; the lottery only ever runs over what follows it.</summary>
        public const int FirstSpecial = 1;

        public static BlockDefinition At(int index) => Definitions[Mathf.Clamp(index, 0, Definitions.Length - 1)];

        public static bool IsSpecial(BlockType type) => type != BlockType.Standard;
    }
}
