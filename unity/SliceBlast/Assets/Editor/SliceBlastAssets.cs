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

            if (AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath) == null)
            {
                Shader shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
                Material lit = new Material(shader) { enableInstancing = true };
                AssetDatabase.CreateAsset(lit, LitMaterialPath);
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath) == null)
            {
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
                Material unlit = new Material(shader);
                AssetDatabase.CreateAsset(unlit, UnlitMaterialPath);
            }
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
#if UNITY_IOS
            UnityEditor.Build.NamedBuildTarget target = UnityEditor.Build.NamedBuildTarget.iOS;
            UnityEditor.iOS.iOSPlatformIconKind kind = UnityEditor.iOS.iOSPlatformIconKind.Application;

            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(target, kind);
            for (int i = 0; i < icons.Length; i++)
            {
                icons[i].SetTexture(icon, 0);
            }

            PlayerSettings.SetPlatformIcons(target, kind, icons);
#elif UNITY_ANDROID
            UnityEditor.Build.NamedBuildTarget target = UnityEditor.Build.NamedBuildTarget.Android;
            UnityEditor.Android.AndroidPlatformIconKind kind = UnityEditor.Android.AndroidPlatformIconKind.Legacy;

            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(target, kind);
            for (int i = 0; i < icons.Length; i++)
            {
                icons[i].SetTexture(icon, 0);
            }

            PlayerSettings.SetPlatformIcons(target, kind, icons);
#endif
        }

        /// <summary>
        /// Draws the icon: a dark gradient with three stacked bars and one sliced-off chunk —
        /// the game's whole idea readable at 60 pixels.
        /// </summary>
        private static Texture2D DrawIcon()
        {
            Texture2D texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);

            Color top = new Color(0.18f, 0.16f, 0.42f);
            Color bottom = new Color(0.05f, 0.05f, 0.11f);

            Color[] barColors =
            {
                new Color(1f, 0.79f, 0.29f),
                new Color(0.36f, 0.91f, 0.77f),
                new Color(0.40f, 0.66f, 1f)
            };

            // x, y, halfWidth, halfHeight (normalised, y up)
            Vector4[] bars =
            {
                new Vector4(0.50f, 0.30f, 0.30f, 0.075f),
                new Vector4(0.46f, 0.47f, 0.26f, 0.075f),
                new Vector4(0.53f, 0.64f, 0.21f, 0.075f)
            };

            // The chunk that got sliced off the top layer, tumbling away.
            Vector4 chunk = new Vector4(0.24f, 0.70f, 0.055f, 0.055f);

            Color[] pixels = new Color[IconSize * IconSize];
            float unit = 1f / IconSize;

            for (int y = 0; y < IconSize; y++)
            {
                float v = y / (float)(IconSize - 1);
                Color background = Color.Lerp(bottom, top, Mathf.Pow(v, 1.4f));

                for (int x = 0; x < IconSize; x++)
                {
                    float u = x / (float)(IconSize - 1);
                    Color color = background;

                    for (int i = 0; i < bars.Length; i++)
                    {
                        Vector4 bar = bars[i];
                        float distance = RoundedRect(u, v, bar.x, bar.y, bar.z, bar.w, 0.03f);
                        float mask = 1f - Mathf.Clamp01(distance / (unit * 2.5f));

                        if (mask > 0f)
                        {
                            // Slight vertical shading keeps the bars from looking like flat stickers.
                            float shade = Mathf.InverseLerp(bar.y - bar.w, bar.y + bar.w, v);
                            Color face = Color.Lerp(barColors[i] * 0.72f, barColors[i], shade);
                            color = Color.Lerp(color, face, mask);
                        }
                    }

                    float chunkDistance = RoundedRect(u, v, chunk.x, chunk.y, chunk.z, chunk.w, 0.02f);
                    float chunkMask = 1f - Mathf.Clamp01(chunkDistance / (unit * 2.5f));
                    if (chunkMask > 0f)
                    {
                        color = Color.Lerp(color, new Color(1f, 0.55f, 0.42f), chunkMask);
                    }

                    color.a = 1f;
                    pixels[y * IconSize + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static float RoundedRect(float x, float y, float centerX, float centerY, float halfWidth, float halfHeight, float radius)
        {
            float dx = Mathf.Abs(x - centerX) - (halfWidth - radius);
            float dy = Mathf.Abs(y - centerY) - (halfHeight - radius);

            float outsideX = Mathf.Max(dx, 0f);
            float outsideY = Mathf.Max(dy, 0f);
            float outside = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY);

            return outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
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
