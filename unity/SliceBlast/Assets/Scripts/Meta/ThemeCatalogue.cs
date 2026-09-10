// Cosmetic themes. A theme restates every colour the game generates at runtime — the sky
// gradient, the star field, the hue band ordinary blocks walk through, the base platform and
// the accent that blasts and the wordmark are drawn in.
//
// Nothing here touches a texture or a material asset: the whole game already tints procedural
// geometry through a MaterialPropertyBlock, so a theme is just a different set of numbers fed
// into systems that were already reading numbers. That is why themes cost almost nothing to
// add and why they can be swapped mid-session without a reload.
using UnityEngine;

namespace SliceBlast.Meta
{
    public struct ThemeDefinition
    {
        public string Id;
        public string Name;
        public int Price;

        public Color SkyTop;
        public Color SkyBottom;
        public Color StarTint;
        public Color Platform;
        public Color Accent;

        public float HueStart;
        public float HueSpan;
        public float Saturation;
        public float Brightness;
    }

    public static class ThemeCatalogue
    {
        public const string DefaultId = "midnight";

        private static readonly ThemeDefinition[] Definitions =
        {
            new ThemeDefinition
            {
                Id = DefaultId,
                Name = "MIDNIGHT",
                Price = 0,
                SkyTop = new Color(0.10f, 0.11f, 0.22f),
                SkyBottom = new Color(0.03f, 0.03f, 0.06f),
                StarTint = new Color(0.85f, 0.90f, 1f),
                Platform = new Color(0.35f, 0.40f, 0.55f),
                Accent = new Color(1f, 0.79f, 0.29f),
                HueStart = 0.45f,
                HueSpan = 0.40f,
                Saturation = 0.55f,
                Brightness = 0.95f
            },
            new ThemeDefinition
            {
                Id = "ember",
                Name = "EMBER",
                Price = 900,
                SkyTop = new Color(0.26f, 0.09f, 0.06f),
                SkyBottom = new Color(0.05f, 0.02f, 0.03f),
                StarTint = new Color(1f, 0.85f, 0.70f),
                Platform = new Color(0.45f, 0.28f, 0.22f),
                Accent = new Color(1f, 0.62f, 0.22f),
                HueStart = 0.02f,
                HueSpan = 0.11f,
                Saturation = 0.68f,
                Brightness = 0.97f
            },
            new ThemeDefinition
            {
                Id = "vapor",
                Name = "VAPOUR",
                Price = 1400,
                SkyTop = new Color(0.22f, 0.07f, 0.28f),
                SkyBottom = new Color(0.04f, 0.02f, 0.09f),
                StarTint = new Color(1f, 0.78f, 0.95f),
                Platform = new Color(0.42f, 0.26f, 0.52f),
                Accent = new Color(1f, 0.42f, 0.80f),
                HueStart = 0.78f,
                HueSpan = 0.22f,
                Saturation = 0.62f,
                Brightness = 1f
            },
            new ThemeDefinition
            {
                Id = "signal",
                Name = "SIGNAL",
                Price = 1800,
                SkyTop = new Color(0.03f, 0.14f, 0.08f),
                SkyBottom = new Color(0.01f, 0.03f, 0.02f),
                StarTint = new Color(0.72f, 1f, 0.80f),
                Platform = new Color(0.20f, 0.38f, 0.26f),
                Accent = new Color(0.42f, 1f, 0.52f),
                HueStart = 0.30f,
                HueSpan = 0.14f,
                Saturation = 0.60f,
                Brightness = 0.95f
            },
            new ThemeDefinition
            {
                Id = "mono",
                Name = "MONOLITH",
                Price = 2400,
                SkyTop = new Color(0.13f, 0.13f, 0.14f),
                SkyBottom = new Color(0.02f, 0.02f, 0.02f),
                StarTint = Color.white,
                Platform = new Color(0.38f, 0.38f, 0.40f),
                Accent = Color.white,
                HueStart = 0f,
                HueSpan = 0f,
                // Saturation zero is what actually makes this greyscale: the hue band is
                // ignored entirely and every ordinary block lands on a value ramp.
                Saturation = 0f,
                Brightness = 0.92f
            },
            new ThemeDefinition
            {
                Id = "aurora",
                Name = "AURORA",
                Price = 3000,
                SkyTop = new Color(0.04f, 0.20f, 0.24f),
                SkyBottom = new Color(0.06f, 0.02f, 0.14f),
                StarTint = new Color(0.75f, 1f, 0.95f),
                Platform = new Color(0.22f, 0.44f, 0.48f),
                Accent = new Color(0.40f, 1f, 0.86f),
                HueStart = 0.40f,
                HueSpan = 0.30f,
                Saturation = 0.58f,
                Brightness = 1f
            }
        };

        public static int Count => Definitions.Length;

        public static ThemeDefinition At(int index)
        {
            return Definitions[Mathf.Clamp(index, 0, Definitions.Length - 1)];
        }

        public static ThemeDefinition Get(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < Definitions.Length; i++)
                {
                    if (Definitions[i].Id == id)
                    {
                        return Definitions[i];
                    }
                }
            }

            return Definitions[0];
        }

        /// <summary>The theme every system reads. Falls back to the default if the equipped
        /// one was removed from the catalogue by an update.</summary>
        public static ThemeDefinition Equipped => Get(PlayerProfile.Data.equippedTheme);

        public static bool IsOwned(string id)
        {
            return id == DefaultId || PlayerProfile.OwnsTheme(id);
        }

        public static bool TryPurchase(string id)
        {
            if (IsOwned(id))
            {
                return false;
            }

            ThemeDefinition theme = Get(id);

            if (theme.Id != id || !PlayerProfile.TrySpend(theme.Price))
            {
                return false;
            }

            PlayerProfile.GrantTheme(id);
            PlayerProfile.EquipTheme(id);
            return true;
        }
    }
}
