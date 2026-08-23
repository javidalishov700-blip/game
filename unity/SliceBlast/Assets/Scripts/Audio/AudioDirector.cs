using UnityEngine;

namespace SliceBlast.Audio
{
    /// <summary>
    /// Voice-pooled playback for the procedural bank. Perfect placements climb a
    /// pentatonic ladder — the single most addictive piece of feedback in the game.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioDirector : MonoBehaviour
    {
        // Major pentatonic, two octaves. A streak literally plays a melody upward.
        private static readonly float[] Ladder =
        {
            1f, 1.1225f, 1.2599f, 1.4983f, 1.6818f,
            2f, 2.2449f, 2.5198f, 2.9966f, 3.3636f, 4f
        };

        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.85f;
        [SerializeField] private int voiceCount = 10;

        private AudioSource[] _voices;
        private int _voiceIndex;

        private AudioClip _thud;
        private AudioClip _slice;
        private AudioClip _note;
        private AudioClip _boom;
        private AudioClip _rise;
        private AudioClip _fail;
        private AudioClip _tick;
        private AudioClip _zap;
        private AudioClip _shatter;
        private AudioClip _pound;
        private AudioClip _glitch;
        private AudioClip _jackpot;

        public bool Muted { get; set; }

        public void Initialize()
        {
            _thud = ProceduralSfx.Thud();
            _slice = ProceduralSfx.Slice();
            _note = ProceduralSfx.Note();
            _boom = ProceduralSfx.Boom();
            _rise = ProceduralSfx.Rise();
            _fail = ProceduralSfx.Fail();
            _tick = ProceduralSfx.Tick();
            _zap = ProceduralSfx.Zap();
            _shatter = ProceduralSfx.Shatter();
            _pound = ProceduralSfx.Pound();
            _glitch = ProceduralSfx.Glitch();
            _jackpot = ProceduralSfx.Jackpot();

            _voices = new AudioSource[Mathf.Max(2, voiceCount)];
            for (int i = 0; i < _voices.Length; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.ignoreListenerPause = true;
                _voices[i] = source;
            }
        }

        public void PlayPlace()
        {
            Play(_thud, 1f, 0.9f);
        }

        public void PlaySlice()
        {
            Play(_slice, Random.Range(0.94f, 1.06f), 0.8f);
            Play(_thud, 0.88f, 0.6f);
        }

        public void PlayPerfect(int streak)
        {
            int step = Mathf.Clamp(streak - 1, 0, Ladder.Length - 1);
            Play(_note, Ladder[step], 0.75f);
            Play(_thud, 1.15f, 0.35f);
        }

        public void PlayBlast()
        {
            Play(_boom, 1f, 1f);
            Play(_rise, 1f, 0.7f);
        }

        public void PlayGameOver()
        {
            Play(_fail, 1f, 0.9f);
        }

        /// <summary>Title screen: a low swell with two notes settling on top of it.</summary>
        public void PlayIntro()
        {
            Play(_rise, 0.55f, 0.55f);
            Play(_note, Ladder[0], 0.45f);
            Play(_note, Ladder[4], 0.35f);
        }

        /// <summary>The tap that starts a run.</summary>
        public void PlayStart()
        {
            Play(_rise, 1.15f, 0.7f);
            Play(_note, Ladder[5], 0.6f);
        }

        public void PlayTick()
        {
            Play(_tick, 1f, 0.5f);
        }

        public void PlayZap()
        {
            Play(_zap, 1f, 0.75f);
        }

        public void PlayShatter()
        {
            Play(_shatter, Random.Range(0.95f, 1.08f), 0.8f);
        }

        public void PlayPound()
        {
            Play(_pound, 1f, 0.95f);
        }

        public void PlayGlitch()
        {
            Play(_glitch, Random.Range(0.9f, 1.2f), 0.6f);
        }

        public void PlayJackpot()
        {
            Play(_jackpot, 1f, 0.85f);
        }

        private void Play(AudioClip clip, float pitch, float volume)
        {
            if (Muted || clip == null || _voices == null)
            {
                return;
            }

            AudioSource source = _voices[_voiceIndex];
            _voiceIndex = (_voiceIndex + 1) % _voices.Length;

            source.pitch = pitch;
            source.volume = volume * masterVolume;
            source.clip = clip;
            source.Play();
        }
    }
}
