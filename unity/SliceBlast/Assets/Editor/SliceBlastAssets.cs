// Generates the few real assets the game needs: shader-backed materials that survive
// build-time shader stripping, and a procedurally drawn app icon.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SliceBlast.EditorTools
{
    public static class SliceBlastAssets
    {
        private const string ResourcesFolder = "Assets/Resources";
        private const string MaterialsFolder = ResourcesFolder + "/Materials";
        private const string LitMaterialPath = MaterialsFolder + "/BlockLit.mat";
        private const string UnlitMaterialPath = MaterialsFolder + "/BlastUnlit.mat";
        private const string GlowMaterialPath = MaterialsFolder + "/Glow.mat";
        private const string GlassMaterialPath = MaterialsFolder + "/GlassLit.mat";
        private const string IconFolder = "Assets/Icons";
        private const string IconPath = IconFolder + "/AppIcon.png";

        private const int IconSize = 1024;

        [MenuItem("Slice & Blast/Regenerate Art Assets")]
        public static void RegenerateAll()
        {
            EnsureMaterials();
            EnsureIcon(true);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Runtime code creates its materials from Resources so the shaders are guaranteed to
        /// be in the player. Shader.Find alone is not enough — unreferenced shaders get stripped.
        /// </summary>
        public static void EnsureMaterials()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder(ResourcesFolder, "Materials");

            // Blocks: one lit material for the whole tower. Emission is enabled here so every
            // block can drive _EmissionColor from its own property block — that is what makes
            // a neon block glow in a pipeline with no post-processing.
            Material lit = LoadOrCreate(LitMaterialPath, "Standard", "Legacy Shaders/Diffuse");

            if (lit != null)
            {
                lit.enableInstancing = true;
                lit.EnableKeyword("_EMISSION");
                lit.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                lit.SetColor("_EmissionColor", Color.black);
                EditorUtility.SetDirty(lit);
            }

            LoadOrCreate(UnlitMaterialPath, "Sprites/Default", "Unlit/Transparent");

            // Halos, arcs and burned-in symbols: additive, so they add light instead of
            // painting over what is behind them.
            LoadOrCreate(GlowMaterialPath, "Particles/Additive", "Legacy Shaders/Particles/Additive", "Mobile/Particles/Additive", "Sprites/Default");

            // Glass: the Standard shader in transparent mode, lit and polished, so the block
            // is genuinely see-through instead of a flat unlit quad colour.
            Material glass = LoadOrCreate(GlassMaterialPath, "Standard", "Sprites/Default");

            if (glass != null && glass.shader != null && glass.shader.name == "Standard")
            {
                // The tag has to go on before the blend mode, or the shader validates the
                // material as opaque and throws the render queue back to Geometry.
                glass.SetOverrideTag("RenderType", "Transparent");
                glass.SetFloat("_Mode", 3f);
                glass.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                glass.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                glass.SetInt("_ZWrite", 0);
                glass.SetFloat("_Glossiness", 0.9f);
                glass.DisableKeyword("_ALPHATEST_ON");
                glass.EnableKeyword("_ALPHABLEND_ON");
                glass.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                glass.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                EditorUtility.SetDirty(glass);
            }
        }

        /// <summary>Loads the material if it is already there, creates it from the first shader that resolves if not.</summary>
        private static Material LoadOrCreate(string path, params string[] shaderNames)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                return existing;
            }

            Shader shader = null;

            for (int i = 0; i < shaderNames.Length && shader == null; i++)
            {
                shader = Shader.Find(shaderNames[i]);
            }

            if (shader == null)
            {
                Debug.LogWarning($"[SliceBlast] No shader found for {path}.");
                return null;
            }

            Material material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static void EnsureIcon(bool force)
        {
            try
            {
                EnsureFolder("Assets", "Icons");

                if (force || !File.Exists(IconPath))
                {
                    File.WriteAllBytes(IconPath, DrawIcon().EncodeToPNG());
                    AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
                }

                TextureImporter importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.alphaIsTransparency = false;
                    importer.mipmapEnabled = false;
                    importer.isReadable = true;
                    importer.maxTextureSize = 2048;
                    importer.SaveAndReimport();
                }

                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
                if (icon != null)
                {
                    ApplyIcon(icon);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SliceBlast] Icon generation skipped: {exception.Message}");
            }
        }

        private static void ApplyIcon(Texture2D icon)
        {
#if UNITY_IOS || UNITY_ANDROID
#if UNITY_IOS
            BuildTargetGroup group = BuildTargetGroup.iOS;
            PlatformIconKind kind = UnityEditor.iOS.iOSPlatformIconKind.Application;
#else
            BuildTargetGroup group = BuildTargetGroup.Android;
            PlatformIconKind kind = UnityEditor.Android.AndroidPlatformIconKind.Legacy;
#endif
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(group, kind);

            for (int i = 0; i < icons.Length; i++)
            {
                icons[i].SetTexture(icon, 0);
            }

            PlayerSettings.SetPlatformIcons(group, kind, icons);
#endif
        }

        /// <summary>
        /// Draws the icon: an isometric stack under a cut beam, the severed corner tumbling
        /// away. Geometry is precomputed once and the pixel loop only tests it, so a 1024
        /// icon with 2x supersampling costs a second of build time and no assets.
        /// </summary>
        public static Texture2D DrawIcon()
        {
            const int samples = 2;

            Color backgroundTop = new Color(0.17f, 0.13f, 0.40f);
            Color backgroundBottom = new Color(0.035f, 0.035f, 0.075f);
            Color glow = new Color(0.80f, 0.28f, 0.85f);
            Color beamCore = Color.white;
            Color beamHalo = new Color(0.85f, 0.95f, 1f);

            Color gold = new Color(1f, 0.78f, 0.28f);
            Color mint = new Color(0.32f, 0.92f, 0.76f);
            Color blue = new Color(0.40f, 0.66f, 1f);
            Color coral = new Color(1f, 0.50f, 0.36f);

            const float radius = 0.245f;
            const float sideHeight = 0.128f;
            const float centerX = 0.5f;
            const float topY = 0.578f;

            // Cut plane, parallel to the isometric edge, just right of the block centre.
            Vector2 cutDirection = new Vector2(0.62f, -0.31f).normalized;
            Vector2 cutOrigin = new Vector2(centerX + 0.015f, topY + 0.012f);
            Vector2 cutA = cutOrigin - cutDirection;
            Vector2 cutB = cutOrigin + cutDirection;
            float cornerSign = Mathf.Sign(Side(new Vector2(0.95f, 0.95f), cutA, cutB));
            Vector2 severedOffset = new Vector2(0.055f, 0.055f);

            Vector2[][] stackFaces = new Vector2[6][];
            Color[] stackColors = new Color[6];
            BuildCube(stackFaces, stackColors, 0, centerX, 0.300f, radius, sideHeight, gold);
            BuildCube(stackFaces, stackColors, 3, centerX, 0.440f, radius, sideHeight, mint);

            Vector2[][] topFaces = new Vector2[3][];
            Color[] topColors = new Color[3];
            BuildCube(topFaces, topColors, 0, centerX, topY, radius, sideHeight, blue);

            Vector2[][] severedFaces = new Vector2[3][];
            Color[] severedColors = new Color[3];
            BuildCube(severedFaces, severedColors, 0, centerX, topY, radius, sideHeight, coral);

            Vector2[] sparks =
            {
                new Vector2(0.78f, 0.83f),
                new Vector2(0.86f, 0.74f),
                new Vector2(0.70f, 0.90f)
            };

            float[] sparkRadii = { 0.016f, 0.011f, 0.009f };

            Texture2D texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[IconSize * IconSize];
            float inverse = 1f / (IconSize * samples);

            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    float r = 0f;
                    float g = 0f;
                    float b = 0f;

                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = (x * samples + sx + 0.5f) * inverse;
                            float v = 1f - (y * samples + sy + 0.5f) * inverse;
                            Vector2 p = new Vector2(u, v);

                            Color c = Color.Lerp(backgroundBottom, backgroundTop, Mathf.Pow(v, 1.3f));

                            float distanceToCentre = Vector2.Distance(p, new Vector2(0.5f, 0.5f)) * 3f;
                            c = Color.Lerp(c, glow, Mathf.Clamp01(0.30f * Mathf.Exp(-distanceToCentre * distanceToCentre)));

                            c = PaintFaces(c, p, stackFaces, stackColors);

                            bool keep = (Side(p, cutA, cutB) >= 0f) != (cornerSign >= 0f);
                            if (keep)
                            {
                                c = PaintFaces(c, p, topFaces, topColors);
                            }

                            Vector2 severedPoint = p - severedOffset;
                            if ((Side(severedPoint, cutA, cutB) >= 0f) == (cornerSign >= 0f))
                            {
                                c = PaintFaces(c, severedPoint, severedFaces, severedColors);
                            }

                            Vector2 fromCut = p - cutOrigin;
                            float along = Vector2.Dot(fromCut, cutDirection);
                            float across = Mathf.Abs(-fromCut.x * cutDirection.y + fromCut.y * cutDirection.x);
                            float taper = Mathf.Exp(-Mathf.Pow(Mathf.Abs(along / 0.40f), 4f));

                            c = Color.Lerp(c, beamCore, Mathf.Clamp01(Mathf.Exp(-Mathf.Pow(across * 52f, 2f)) * 0.95f * taper));
                            c = Color.Lerp(c, beamHalo, Mathf.Clamp01(Mathf.Exp(-Mathf.Pow(across * 15f, 2f)) * 0.22f * taper));

                            for (int i = 0; i < sparks.Length; i++)
                            {
                                if (Vector2.Distance(p, sparks[i]) < sparkRadii[i])
                                {
                                    c = Color.white;
                                }
                            }

                            r += c.r;
                            g += c.g;
                            b += c.b;
                        }
                    }

                    float weight = 1f / (samples * samples);
                    pixels[y * IconSize + x] = new Color(r * weight, g * weight, b * weight, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static void BuildCube(Vector2[][] faces, Color[] colors, int offset, float cx, float cy, float radius, float sideHeight, Color color)
        {
            Vector2 north = new Vector2(cx, cy + radius * 0.5f);
            Vector2 east = new Vector2(cx + radius, cy);
            Vector2 south = new Vector2(cx, cy - radius * 0.5f);
            Vector2 west = new Vector2(cx - radius, cy);
            Vector2 drop = new Vector2(0f, -sideHeight);

            faces[offset] = new[] { north, east, south, west };
            faces[offset + 1] = new[] { west, south, south + drop, west + drop };
            faces[offset + 2] = new[] { south, east, east + drop, south + drop };

            colors[offset] = color;
            colors[offset + 1] = color * 0.56f;
            colors[offset + 2] = color * 0.79f;
        }

        private static Color PaintFaces(Color current, Vector2 point, Vector2[][] faces, Color[] colors)
        {
            for (int i = 0; i < faces.Length; i++)
            {
                if (InsideQuad(point, faces[i]))
                {
                    Color face = colors[i];
                    return new Color(face.r, face.g, face.b, 1f);
                }
            }

            return current;
        }

        private static bool InsideQuad(Vector2 point, Vector2[] quad)
        {
            bool positive = true;
            bool negative = true;

            for (int i = 0; i < 4; i++)
            {
                float s = Side(point, quad[i], quad[(i + 1) & 3]);

                if (s < 0f)
                {
                    positive = false;
                }

                if (s > 0f)
                {
                    negative = false;
                }
            }

            return positive || negative;
        }

        private static float Side(Vector2 point, Vector2 a, Vector2 b)
        {
            return (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;

            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
