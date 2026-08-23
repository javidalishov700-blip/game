using System;
using UnityEngine;

namespace SliceBlast.Audio
{
    /// <summary>
    /// Synthesises the whole sound bank at runtime. No audio assets to ship, no import
    /// settings to get wrong, and every clip is a few kilobytes of PCM in memory.
    /// </summary>
    public static class ProceduralSfx
    {
        private const int SampleRate = 44100;

        /// <summary>Dry wooden knock — a block landing.</summary>
        public static AudioClip Thud()
        {
            return Build("sfx_thud", 0.22f, (t, duration) =>
            {
                float env = Mathf.Exp(-16f * t);
                float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(170f, 68f, t / duration) * t);
                float knock = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-110f * t) * 0.45f;
                return (body * 0.85f + knock) * env;
            });
        }

        /// <summary>Sliced-off chunk — short filtered noise sweep.</summary>
        public static AudioClip Slice()
        {
            float phase = 0f;
            return Build("sfx_slice", 0.28f, (t, duration) =>
            {
                float env = Mathf.Exp(-11f * t) * Mathf.Clamp01(t * 60f);
                float noise = UnityEngine.Random.value * 2f - 1f;
                phase = Mathf.Lerp(phase, noise, 0.35f); // cheap low-pass, softens the hiss
                float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(520f, 180f, t / duration) * t) * 0.35f;
                return (phase * 0.7f + tone) * env;
            });
        }

        /// <summary>Plucked note for a perfect placement; the ladder comes from playback pitch.</summary>
        public static AudioClip Note()
        {
            return Build("sfx_note", 0.55f, (t, duration) =>
            {
                float env = Mathf.Exp(-6.5f * t) * Mathf.Clamp01(t * 400f);
                float f = 440f;
                float fundamental = Mathf.Sin(2f * Mathf.PI * f * t);
                float octave = Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.35f;
                float fifth = Mathf.Sin(2f * Mathf.PI * f * 3f * t) * 0.15f;
                float shimmer = Mathf.Sin(2f * Mathf.PI * f * 4.02f * t) * 0.08f * Mathf.Exp(-14f * t);
                return (fundamental + octave + fifth + shimmer) * env * 0.45f;
            });
        }

        /// <summary>Sub-heavy impact for the blast.</summary>
        public static AudioClip Boom()
        {
            return Build("sfx_boom", 0.85f, (t, duration) =>
            {
                float env = Mathf.Exp(-4.5f * t);
                float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(150f, 34f, Mathf.Sqrt(t / duration)) * t);
                float grit = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-26f * t) * 0.4f;
                return (sub * 0.9f + grit) * env * 0.8f;
            });
        }

        /// <summary>Rising sweep layered over the blast — the "reward" flourish.</summary>
        public static AudioClip Rise()
        {
            return Build("sfx_rise", 0.45f, (t, duration) =>
            {
                float p = t / duration;
                float env = Mathf.Sin(Mathf.PI * p) * 0.6f;
                float sweep = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(320f, 1500f, p * p) * t);
                float harmonic = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(640f, 3000f, p * p) * t) * 0.25f;
                return (sweep + harmonic) * env;
            });
        }

        /// <summary>Descending tone for the end of a run.</summary>
        public static AudioClip Fail()
        {
            return Build("sfx_fail", 0.8f, (t, duration) =>
            {
                float p = t / duration;
                float env = Mathf.Exp(-3.2f * t);
                float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(330f, 90f, p) * t);
                float detune = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(327f, 88f, p) * t) * 0.6f;
                return (tone + detune) * env * 0.45f;
            });
        }

        /// <summary>Soft UI tick.</summary>
        public static AudioClip Tick()
        {
            return Build("sfx_tick", 0.09f, (t, duration) =>
            {
                float env = Mathf.Exp(-55f * t);
                return Mathf.Sin(2f * Mathf.PI * 900f * t) * env * 0.5f;
            });
        }


        /// <summary>Electric block: buzzing charge with a fast tremolo.</summary>
        public static AudioClip Zap()
        {
            return Build("sfx_zap", 0.5f, (t, duration) =>
            {
                float p = t / duration;
                float env = Mathf.Exp(-4.5f * t) * Mathf.Clamp01(t * 200f);
                float tremolo = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 26f * t);
                float saw = Mathf.Repeat(Mathf.Lerp(180f, 420f, p) * t, 1f) * 2f - 1f;
                float top = Mathf.Sin(2f * Mathf.PI * 1400f * t) * 0.2f;
                return (saw * 0.6f + top) * env * tremolo * 0.7f;
            });
        }

        /// <summary>Glass block shattering.</summary>
        public static AudioClip Shatter()
        {
            return Build("sfx_shatter", 0.55f, (t, duration) =>
            {
                float env = Mathf.Exp(-9f * t);
                float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-22f * t);
                float ring = Mathf.Sin(2f * Mathf.PI * 2600f * t) * 0.35f * Mathf.Exp(-6f * t);
                float ring2 = Mathf.Sin(2f * Mathf.PI * 3700f * t) * 0.22f * Mathf.Exp(-8f * t);
                return (noise + ring + ring2) * env * 0.8f;
            });
        }

        /// <summary>Steel block landing: a heavy pound with a metallic tail.</summary>
        public static AudioClip Pound()
        {
            return Build("sfx_pound", 0.6f, (t, duration) =>
            {
                float env = Mathf.Exp(-7f * t);
                float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 45f, t / duration) * t);
                float clang = Mathf.Sin(2f * Mathf.PI * 720f * t) * 0.25f * Mathf.Exp(-14f * t);
                float grit = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-40f * t) * 0.3f;
                return (sub + clang + grit) * env * 0.9f;
            });
        }

        /// <summary>Glitch feint: a bit-crushed stutter.</summary>
        public static AudioClip Glitch()
        {
            float held = 0f;
            int counter = 0;

            return Build("sfx_glitch", 0.22f, (t, duration) =>
            {
                if (counter++ % 220 == 0)
                {
                    held = UnityEngine.Random.value * 2f - 1f;
                }

                float env = Mathf.Exp(-14f * t);
                float tone = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 620f * t)) * 0.35f;
                return (held * 0.6f + tone) * env * 0.7f;
            });
        }

        /// <summary>Glitch jackpot: a quick rising arpeggio.</summary>
        public static AudioClip Jackpot()
        {
            float[] steps = { 1f, 1.26f, 1.5f, 2f, 2.52f };

            return Build("sfx_jackpot", 0.75f, (t, duration) =>
            {
                int index = Mathf.Clamp(Mathf.FloorToInt(t / duration * steps.Length), 0, steps.Length - 1);
                float local = t - index * (duration / steps.Length);
                float env = Mathf.Exp(-11f * local) * Mathf.Exp(-1.2f * t);
                float f = 523.25f * steps[index];
                float tone = Mathf.Sin(2f * Mathf.PI * f * t) + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.3f;
                return tone * env * 0.5f;
            });
        }

        private static AudioClip Build(string name, float duration, Func<float, float, float> sample)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = Mathf.Clamp(sample(t, duration), -1f, 1f);
            }

            // Fade the tail so no clip ends on a discontinuity (audible as a pop on device).
            int fade = Mathf.Min(256, count);
            for (int i = 0; i < fade; i++)
            {
                data[count - 1 - i] *= i / (float)fade;
            }

            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
