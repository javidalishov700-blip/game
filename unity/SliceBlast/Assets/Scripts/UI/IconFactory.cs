// Every icon in the game — HUD buttons, status badges and the symbols stamped on the
// special blocks — is rasterised here from signed distance fields. No imported textures,
// no font glyphs to fail on a device, and it stays crisp at any resolution.
using System.Collections.Generic;
using UnityEngine;

namespace SliceBlast.UI
{
    public enum IconShape
    {
        None = 0,
        Panel,
        Pause,
        Play,
        Replay,
        SoundOn,
        SoundOff,
        VibrateOn,
        VibrateOff,
        Shield,
        Bolt,
        Burst,
        Chevrons,
        Glitch,
        Crown
    }

    public static class IconFactory
    {
        private const int Resolution = 128;
        private const int PanelBorder = 44;

        private static readonly Dictionary<IconShape, Texture2D> Textures = new Dictionary<IconShape, Texture2D>(16);
        private static readonly Dictionary<IconShape, Sprite> Sprites = new Dictionary<IconShape, Sprite>(16);

        public static Texture2D GetTexture(IconShape shape)
        {
            if (Textures.TryGetValue(shape, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture = Rasterize(shape);
            Textures[shape] = texture;
            return texture;
        }

        public static Sprite GetSprite(IconShape shape)
        {
            if (Sprites.TryGetValue(shape, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture = GetTexture(shape);

            Vector4 border = shape == IconShape.Panel
                ? new Vector4(PanelBorder, PanelBorder, PanelBorder, PanelBorder)
                : Vector4.zero;

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, Resolution, Resolution),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);

            sprite.name = "Sprite_" + shape;
            sprite.hideFlags = HideFlags.HideAndDontSave;

            Sprites[shape] = sprite;
            return sprite;
        }

        private static Texture2D Rasterize(IconShape shape)
        {
            Texture2D texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                name = "Icon_" + shape,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                // Static cache: never let a scene load collect these out from under the HUD.
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[Resolution * Resolution];
            float texel = 2f / Resolution;

            for (int y = 0; y < Resolution; y++)
            {
                float py = (y + 0.5f) * texel - 1f;
                int row = y * Resolution;

                for (int x = 0; x < Resolution; x++)
                {
                    float px = (x + 0.5f) * texel - 1f;

                    // Analytic coverage from the distance field: one sample, clean edges.
                    float distance = Distance(shape, new Vector2(px, py));
                    float alpha = Mathf.Clamp01(0.5f - distance / texel);

                    pixels[row + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static float Distance(IconShape shape, Vector2 p)
        {
            switch (shape)
            {
                case IconShape.Panel:
                    return Box(p, new Vector2(0.99f, 0.99f), 0.44f);

                case IconShape.Pause:
                    return Mathf.Min(
                        Box(p - new Vector2(-0.32f, 0f), new Vector2(0.14f, 0.5f), 0.06f),
                        Box(p - new Vector2(0.32f, 0f), new Vector2(0.14f, 0.5f), 0.06f));

                case IconShape.Play:
                    return Triangle(p, new Vector2(-0.36f, -0.58f), new Vector2(-0.36f, 0.58f), new Vector2(0.6f, 0f));

                case IconShape.Replay:
                {
                    // Three quarters of a ring plus a head on the open end.
                    float arc = Ring(p, 0.5f, 0.13f);
                    arc = Mathf.Max(arc, -Box(p - new Vector2(0.62f, 0.62f), new Vector2(0.56f, 0.56f), 0f));
                    float head = Triangle(p, new Vector2(-0.04f, 0.8f), new Vector2(-0.04f, 0.2f), new Vector2(0.44f, 0.5f));
                    return Mathf.Min(arc, head);
                }

                case IconShape.SoundOn:
                {
                    float speaker = Speaker(p);
                    float near = Mathf.Max(Ring(p - new Vector2(0.06f, 0f), 0.36f, 0.07f), 0.18f - p.x);
                    float far = Mathf.Max(Ring(p - new Vector2(0.06f, 0f), 0.62f, 0.07f), 0.18f - p.x);
                    return Mathf.Min(speaker, Mathf.Min(near, far));
                }

                case IconShape.SoundOff:
                    return Mathf.Min(Speaker(p), Cross(p - new Vector2(0.42f, 0f), 0.26f, 0.075f));

                case IconShape.VibrateOn:
                {
                    float phone = Box(p, new Vector2(0.24f, 0.52f), 0.1f);
                    float left = Mathf.Min(
                        Segment(p, new Vector2(-0.46f, -0.2f), new Vector2(-0.46f, 0.2f), 0.055f),
                        Segment(p, new Vector2(-0.68f, -0.34f), new Vector2(-0.68f, 0.34f), 0.055f));
                    float right = Mathf.Min(
                        Segment(p, new Vector2(0.46f, -0.2f), new Vector2(0.46f, 0.2f), 0.055f),
                        Segment(p, new Vector2(0.68f, -0.34f), new Vector2(0.68f, 0.34f), 0.055f));
                    return Mathf.Min(phone, Mathf.Min(left, right));
                }

                case IconShape.VibrateOff:
                    return Mathf.Min(
                        Box(p - new Vector2(-0.3f, 0f), new Vector2(0.22f, 0.5f), 0.1f),
                        Cross(p - new Vector2(0.36f, 0f), 0.28f, 0.08f));

                case IconShape.Shield:
                {
                    // Two overlapping discs make the curved flanks; a half-plane flattens the top.
                    float lens = Mathf.Max(Circle(p - new Vector2(1f, 0.37f), 1.5f), Circle(p - new Vector2(-1f, 0.37f), 1.5f));
                    return Mathf.Max(lens, p.y - 0.6f);
                }

                case IconShape.Bolt:
                    return Mathf.Min(
                        Mathf.Min(
                            Segment(p, new Vector2(0.2f, 0.78f), new Vector2(-0.34f, 0.04f), 0.15f),
                            Segment(p, new Vector2(-0.34f, 0.04f), new Vector2(0.14f, 0.04f), 0.15f)),
                        Segment(p, new Vector2(0.14f, 0.04f), new Vector2(-0.18f, -0.78f), 0.15f));

                case IconShape.Burst:
                {
                    float spikes = Mathf.Min(
                        Mathf.Min(
                            Segment(p, new Vector2(0f, -0.82f), new Vector2(0f, 0.82f), 0.09f),
                            Segment(p, new Vector2(-0.82f, 0f), new Vector2(0.82f, 0f), 0.09f)),
                        Mathf.Min(
                            Segment(p, new Vector2(-0.52f, -0.52f), new Vector2(0.52f, 0.52f), 0.075f),
                            Segment(p, new Vector2(-0.52f, 0.52f), new Vector2(0.52f, -0.52f), 0.075f)));

                    return Mathf.Min(spikes, Circle(p, 0.22f));
                }

                case IconShape.Chevrons:
                {
                    float upper = Mathf.Min(
                        Segment(p, new Vector2(-0.5f, 0.06f), new Vector2(0f, 0.56f), 0.115f),
                        Segment(p, new Vector2(0f, 0.56f), new Vector2(0.5f, 0.06f), 0.115f));
                    float lower = Mathf.Min(
                        Segment(p, new Vector2(-0.5f, -0.52f), new Vector2(0f, -0.02f), 0.115f),
                        Segment(p, new Vector2(0f, -0.02f), new Vector2(0.5f, -0.52f), 0.115f));
                    return Mathf.Min(upper, lower);
                }

                case IconShape.Glitch:
                    // Scan lines knocked out of alignment.
                    return Mathf.Min(
                        Mathf.Min(
                            Box(p - new Vector2(-0.14f, 0.42f), new Vector2(0.46f, 0.13f), 0.03f),
                            Box(p - new Vector2(0.2f, 0.02f), new Vector2(0.38f, 0.13f), 0.03f)),
                        Box(p - new Vector2(-0.24f, -0.38f), new Vector2(0.34f, 0.13f), 0.03f));

                case IconShape.Crown:
                {
                    float band = Box(p - new Vector2(0f, -0.42f), new Vector2(0.62f, 0.16f), 0.06f);
                    float body = Triangle(p, new Vector2(-0.72f, 0.6f), new Vector2(-0.62f, -0.34f), new Vector2(0.62f, -0.34f));
                    body = Mathf.Min(body, Triangle(p, new Vector2(0.72f, 0.6f), new Vector2(-0.62f, -0.34f), new Vector2(0.62f, -0.34f)));
                    body = Mathf.Min(body, Triangle(p, new Vector2(0f, 0.72f), new Vector2(-0.62f, -0.34f), new Vector2(0.62f, -0.34f)));
                    return Mathf.Min(band, body);
                }

                default:
                    return 1f;
            }
        }

        private static float Speaker(Vector2 p)
        {
            float body = Box(p - new Vector2(-0.52f, 0f), new Vector2(0.16f, 0.2f), 0.03f);
            float cone = Triangle(p, new Vector2(-0.36f, 0.05f), new Vector2(-0.36f, -0.05f), new Vector2(-0.02f, 0f));
            cone = Mathf.Min(cone, Triangle(p, new Vector2(-0.36f, 0.2f), new Vector2(-0.02f, 0.52f), new Vector2(-0.02f, -0.52f)));
            cone = Mathf.Min(cone, Triangle(p, new Vector2(-0.36f, -0.2f), new Vector2(-0.36f, 0.2f), new Vector2(-0.02f, -0.52f)));
            return Mathf.Min(body, cone);
        }

        private static float Cross(Vector2 p, float reach, float thickness)
        {
            return Mathf.Min(
                Segment(p, new Vector2(-reach, -reach), new Vector2(reach, reach), thickness),
                Segment(p, new Vector2(-reach, reach), new Vector2(reach, -reach), thickness));
        }

        private static float Circle(Vector2 p, float radius)
        {
            return p.magnitude - radius;
        }

        private static float Ring(Vector2 p, float radius, float thickness)
        {
            return Mathf.Abs(p.magnitude - radius) - thickness;
        }

        private static float Box(Vector2 p, Vector2 half, float radius)
        {
            float dx = Mathf.Abs(p.x) - half.x + radius;
            float dy = Mathf.Abs(p.y) - half.y + radius;

            float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);

            return outside + inside - radius;
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;
            float denominator = Mathf.Max(0.0001f, Vector2.Dot(ba, ba));
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / denominator);
            return (pa - ba * h).magnitude - thickness;
        }

        private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 e0 = b - a;
            Vector2 e1 = c - b;
            Vector2 e2 = a - c;

            Vector2 v0 = p - a;
            Vector2 v1 = p - b;
            Vector2 v2 = p - c;

            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Mathf.Max(0.0001f, Vector2.Dot(e0, e0)));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Mathf.Max(0.0001f, Vector2.Dot(e1, e1)));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Mathf.Max(0.0001f, Vector2.Dot(e2, e2)));

            float sign = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);

            float distance = Mathf.Min(Mathf.Min(Vector2.Dot(pq0, pq0), Vector2.Dot(pq1, pq1)), Vector2.Dot(pq2, pq2));
            float side = Mathf.Min(
                Mathf.Min(sign * (v0.x * e0.y - v0.y * e0.x), sign * (v1.x * e1.y - v1.y * e1.x)),
                sign * (v2.x * e2.y - v2.y * e2.x));

            return -Mathf.Sqrt(distance) * Mathf.Sign(side);
        }
    }
}
