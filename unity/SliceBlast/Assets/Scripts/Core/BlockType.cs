using UnityEngine;

namespace SliceBlast.Core
{
    public enum BlockType : byte
    {
        Standard = 0,
        Neon = 1,
        Electric = 2,
        Glass = 3,
        Steel = 4,
        Glitch = 5
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
        private static readonly BlockDefinition[] Definitions =
        {
            new BlockDefinition
            {
                Type = BlockType.Standard,
                Tint = Color.white,
                SpeedMultiplier = 1f,
                SpawnWeight = 100f,
                UsesPalette = true,
                Label = "STANDARD"
            },
            new BlockDefinition
            {
                Type = BlockType.Neon,
                Tint = new Color(1f, 0.18f, 0.78f),
                SpeedMultiplier = 1f,
                SpawnWeight = 3f,
                UsesPalette = false,
                Label = "NEON"
            },
            new BlockDefinition
            {
                Type = BlockType.Electric,
                Tint = new Color(1f, 0.95f, 0.15f),
                SpeedMultiplier = 1.1f,
                SpawnWeight = 3f,
                UsesPalette = false,
                Label = "ELECTRIC"
            },
            new BlockDefinition
            {
                Type = BlockType.Glass,
                // The rarest thing in the game: a shield should feel like a gift.
                Tint = new Color(0.78f, 0.97f, 1f),
                SpeedMultiplier = 0.95f,
                SpawnWeight = 1.5f,
                UsesPalette = false,
                Label = "GLASS"
            },
            new BlockDefinition
            {
                Type = BlockType.Steel,
                Tint = new Color(0.66f, 0.70f, 0.78f),
                SpeedMultiplier = 1.5f,
                SpawnWeight = 3f,
                UsesPalette = false,
                Label = "STEEL"
            },
            new BlockDefinition
            {
                Type = BlockType.Glitch,
                Tint = new Color(0.58f, 0.32f, 1f),
                SpeedMultiplier = 1.15f,
                SpawnWeight = 2f,
                UsesPalette = false,
                Label = "GLITCH"
            }
        };

        public static BlockDefinition Get(BlockType type)
        {
            int index = (int)type;
            return index >= 0 && index < Definitions.Length ? Definitions[index] : Definitions[0];
        }

        public static int Count => Definitions.Length;

        public static BlockDefinition At(int index) => Definitions[Mathf.Clamp(index, 0, Definitions.Length - 1)];

        public static bool IsSpecial(BlockType type) => type != BlockType.Standard;
    }
}
