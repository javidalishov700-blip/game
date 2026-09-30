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
                Price = 2500,
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
                Price = 4000,
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
                Price = 5200,
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
                Id = "glacier",
                Name = "GLACIER",
                Price = 6000,
                SkyTop = new Color(0.07f, 0.17f, 0.27f),
                SkyBottom = new Color(0.02f, 0.04f, 0.09f),
                StarTint = new Color(0.86f, 0.97f, 1f),
                Platform = new Color(0.44f, 0.60f, 0.72f),
                Accent = new Color(0.72f, 0.95f, 1f),
                HueStart = 0.54f,
                HueSpan = 0.08f,
                Saturation = 0.32f,
                Brightness = 1f
            },
            new ThemeDefinition
            {
                Id = "mono",
                Name = "MONOLITH",
                Price = 7000,
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
                Id = "citrus",
                Name = "CITRUS",
                Price = 8000,
                SkyTop = new Color(0.14f, 0.15f, 0.03f),
                SkyBottom = new Color(0.03f, 0.04f, 0.01f),
                StarTint = new Color(0.95f, 1f, 0.62f),
                Platform = new Color(0.40f, 0.44f, 0.14f),
                Accent = new Color(0.86f, 1f, 0.26f),
                HueStart = 0.15f,
                HueSpan = 0.12f,
                Saturation = 0.64f,
                Brightness = 0.98f
            },
            new ThemeDefinition
            {
                Id = "aurora",
                Name = "AURORA",
                Price = 9000,
                SkyTop = new Color(0.04f, 0.20f, 0.24f),
                SkyBottom = new Color(0.06f, 0.02f, 0.14f),
                StarTint = new Color(0.75f, 1f, 0.95f),
                Platform = new Color(0.22f, 0.44f, 0.48f),
                Accent = new Color(0.40f, 1f, 0.86f),
                HueStart = 0.40f,
                HueSpan = 0.30f,
                Saturation = 0.58f,
                Brightness = 1f
            },
            // The three premium themes are the long-term coin sink: something still worth
            // saving for once every upgrade is maxed. Saturation stays in the same range as
            // the others so an ordinary block can never be mistaken for a neon special.
            new ThemeDefinition
            {
                Id = "abyss",
                Name = "ABYSS",
                Price = 12000,
                SkyTop = new Color(0.02f, 0.13f, 0.26f),
                SkyBottom = new Color(0f, 0.02f, 0.07f),
                StarTint = new Color(0.55f, 0.85f, 1f),
                Platform = new Color(0.14f, 0.30f, 0.44f),
                Accent = new Color(0.25f, 0.78f, 1f),
                HueStart = 0.52f,
                HueSpan = 0.10f,
                Saturation = 0.62f,
                Brightness = 0.92f
            },
            new ThemeDefinition
            {
                Id = "crimson",
                Name = "CRIMSON",
                Price = 14000,
                SkyTop = new Color(0.23f, 0.02f, 0.05f),
                SkyBottom = new Color(0.04f, 0f, 0.01f),
                StarTint = new Color(1f, 0.76f, 0.76f),
                Platform = new Color(0.46f, 0.14f, 0.18f),
                Accent = new Color(1f, 0.27f, 0.32f),
                HueStart = 0.94f,
                HueSpan = 0.09f,
                Saturation = 0.66f,
                Brightness = 0.95f
            },
            new ThemeDefinition
            {
                Id = "sakura",
                Name = "SAKURA",
                Price = 16000,
                SkyTop = new Color(0.32f, 0.13f, 0.25f),
                SkyBottom = new Color(0.07f, 0.03f, 0.08f),
                StarTint = new Color(1f, 0.86f, 0.94f),
                Platform = new Color(0.56f, 0.36f, 0.46f),
                Accent = new Color(1f, 0.62f, 0.80f),
                HueStart = 0.88f,
                HueSpan = 0.10f,
                Saturation = 0.42f,
                Brightness = 1f
            },
            new ThemeDefinition
            {
                Id = "gilded",
                Name = "GILDED",
                Price = 22000,
                SkyTop = new Color(0.14f, 0.11f, 0.05f),
                SkyBottom = new Color(0.02f, 0.015f, 0.01f),
                StarTint = new Color(1f, 0.88f, 0.55f),
                Platform = new Color(0.42f, 0.33f, 0.14f),
                Accent = new Color(1f, 0.84f, 0.35f),
                HueStart = 0.09f,
                HueSpan = 0.06f,
                Saturation = 0.62f,
                Brightness = 0.96f
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

        /// <summary>
        /// The colour an ordinary block at <paramref name="index"/> wears under a theme. Lives
        /// here rather than in the spawner so the Workshop's previews are painted by the same
        /// formula as the real tower. Ordinary blocks ping-pong through a bounded hue band on
        /// purpose: no plain block should ever drift into a neon hue and pass for a special.
        /// </summary>
        public static Color BlockColor(ThemeDefinition theme, int index, float hueStep)
        {
            // A theme that deliberately drops saturation to zero still has to produce a
            // readable ladder of blocks rather than one flat colour, so a greyscale theme walks
            // the value axis instead of the hue axis.
            if (theme.Saturation <= 0.001f)
            {
                float shade = theme.Brightness * (0.62f + Mathf.PingPong(index * hueStep * 2f, 0.38f));
                return new Color(shade, shade, shade);
            }

            float hue = theme.HueStart + Mathf.PingPong(index * hueStep, theme.HueSpan);
            return Color.HSVToRGB(Mathf.Repeat(hue, 1f), theme.Saturation, theme.Brightness);
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
