// Zero-setup entry point: one empty GameObject in an empty scene builds the whole game —
// camera rig, lighting, backdrop, pooled prefabs, audio, HUD and the flow manager.
using SliceBlast.Audio;
using SliceBlast.Core;
using SliceBlast.Feedback;
using SliceBlast.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace SliceBlast.Bootstrap
{
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class SliceBlastBootstrap : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, 0.79f, 0.29f);
        private static readonly Color Mint = new Color(0.55f, 1f, 0.9f);

        [Header("Camera")]
        [SerializeField] private Vector3 cameraAngles = new Vector3(26f, 45f, 0f);
        [SerializeField] private float cameraDistance = 18f;
        [SerializeField] private float orthographicSize = 5.5f;
        [SerializeField] private Color skyTop = new Color(0.10f, 0.11f, 0.22f);
        [SerializeField] private Color skyBottom = new Color(0.03f, 0.03f, 0.06f);

        [Header("Pools")]
        [SerializeField] private int blockPrewarm = 48;
        [SerializeField] private int blockCap = 220;
        [SerializeField] private int debrisPrewarm = 32;
        [SerializeField] private int debrisCap = 120;
        [SerializeField] private int sparksPerBurst = 16;

        [Header("Flow")]
        [SerializeField] private float restartDelay = 0.55f;

        private GameFlowManager _flow;
        private AudioDirector _audio;
        private GameHud _hud;
        private BlockPool _sparkPool;
        private BlockPool _shockwavePool;

        private bool _gameOver;
        private float _gameOverTime;

        private const string SoundKey = "sliceblast.sound";
        private const string HapticsKey = "sliceblast.haptics";

        /// <summary>
        /// The shipped scene is deliberately empty — the game builds itself here. That keeps
        /// the build independent of scene contents, serialized references and CI settings.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (FindAnyObjectByType<SliceBlastBootstrap>() != null)
            {
                return;
            }

            GameObject host = new GameObject("SliceBlast");
            host.AddComponent<SliceBlastBootstrap>();
        }

        private void Awake()
        {
            Material lit = LoadMaterial("Materials/BlockLit", "Standard");
            Material unlit = LoadMaterial("Materials/BlastUnlit", "Sprites/Default");

            MovingBlock blockTemplate = CreateBlockTemplate(lit);
            DebrisChunk debrisTemplate = CreateDebrisTemplate(lit);
            SparkBurst sparkTemplate = CreateSparkTemplate(lit);
            Shockwave shockwaveTemplate = CreateShockwaveTemplate(unlit);

            BlockPool blockPool = CreatePool("BlockPool", blockTemplate, blockPrewarm, blockCap);
            BlockPool debrisPool = CreatePool("DebrisPool", debrisTemplate, debrisPrewarm, debrisCap);
            _sparkPool = CreatePool("SparkPool", sparkTemplate, 6, 24);
            _shockwavePool = CreatePool("ShockwavePool", shockwaveTemplate, 4, 12);

            CameraRig rig = CreateCameraRig(unlit);
            CreateLighting();

            GameObject audioObject = new GameObject("Audio");
            audioObject.transform.SetParent(transform, false);
            _audio = audioObject.AddComponent<AudioDirector>();
            _audio.Initialize();

            CreateEventSystem();

            GameObject hudObject = new GameObject("HUD");
            hudObject.transform.SetParent(transform, false);
            _hud = hudObject.AddComponent<GameHud>();
            _hud.Build();

            bool soundOn = PlayerPrefs.GetInt(SoundKey, 1) == 1;
            bool hapticsOn = PlayerPrefs.GetInt(HapticsKey, 1) == 1;

            _audio.Muted = !soundOn;
            Haptics.Enabled = hapticsOn;
            _hud.SetSoundLabel(soundOn);
            _hud.SetHapticsLabel(hapticsOn);

            _hud.PauseToggled += OnPauseToggled;
            _hud.RestartRequested += OnRestartRequested;
            _hud.SoundToggled += OnSoundToggled;
            _hud.HapticsToggled += OnHapticsToggled;

            GameObject flowObject = new GameObject("GameFlow");
            flowObject.transform.SetParent(transform, false);
            flowObject.SetActive(false);

            _flow = flowObject.AddComponent<GameFlowManager>();
            flowObject.AddComponent<BlockSlicer>();
            _flow.Configure(blockPool, debrisPool, rig);

            _flow.RunStarted += OnRunStarted;
            _flow.ScoreChanged += OnScoreChanged;
            _flow.PerfectSnapped += OnPerfectSnapped;
            _flow.BlockSliced += OnBlockSliced;
            _flow.BlastFired += OnBlastFired;
            _flow.GameEnded += OnGameEnded;
            _flow.PauseChanged += OnPauseChanged;

            flowObject.SetActive(true);
        }

        private void OnDestroy()
        {
            if (_flow == null)
            {
                return;
            }

            _flow.RunStarted -= OnRunStarted;
            _flow.ScoreChanged -= OnScoreChanged;
            _flow.PerfectSnapped -= OnPerfectSnapped;
            _flow.BlockSliced -= OnBlockSliced;
            _flow.BlastFired -= OnBlastFired;
            _flow.GameEnded -= OnGameEnded;
            _flow.PauseChanged -= OnPauseChanged;

            if (_hud != null)
            {
                _hud.PauseToggled -= OnPauseToggled;
                _hud.RestartRequested -= OnRestartRequested;
                _hud.SoundToggled -= OnSoundToggled;
                _hud.HapticsToggled -= OnHapticsToggled;
            }
        }

        private static void CreateEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject events = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            events.AddComponent<StandaloneInputModule>();
#endif
        }

        private void OnPauseToggled()
        {
            _audio.PlayTick();
            _flow.TogglePause();
        }

        private void OnRestartRequested()
        {
            _audio.PlayTick();
            _gameOver = false;
            _flow.SetPaused(false);
            _flow.Restart();
        }

        private void OnSoundToggled(bool on)
        {
            _audio.Muted = !on;
            PlayerPrefs.SetInt(SoundKey, on ? 1 : 0);
            PlayerPrefs.Save();

            if (on)
            {
                _audio.PlayTick();
            }
        }

        private void OnHapticsToggled(bool on)
        {
            Haptics.Enabled = on;
            PlayerPrefs.SetInt(HapticsKey, on ? 1 : 0);
            PlayerPrefs.Save();

            if (on)
            {
                Haptics.Light();
            }
        }

        private void OnPauseChanged(bool paused)
        {
            _hud.ShowPaused(paused);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_sparkPool != null)
            {
                _sparkPool.Tick(dt);
            }

            if (_shockwavePool != null)
            {
                _shockwavePool.Tick(dt);
            }

            if (!_gameOver || Time.unscaledTime - _gameOverTime < restartDelay)
            {
                return;
            }

            if (!BlockSlicer.TapPressedThisFrame() || BlockSlicer.PointerOverUi())
            {
                return;
            }

            OnRestartRequested();
        }

        private void OnRunStarted()
        {
            _gameOver = false;
            _hud.HideGameOver();
            _hud.ShowPaused(false);
            _hud.SetHintText("TAP TO DROP");
            _hud.ShowHint(true);
        }

        private void OnScoreChanged(int score, int multiplier)
        {
            _hud.SetScore(score);
            _hud.SetMultiplier(multiplier);

            if (score > 0 && _flow.BlastCount > 0)
            {
                _hud.ShowHint(false);
            }
        }

        private void OnPerfectSnapped(int streak, Vector3 position)
        {
            _audio.PlayPerfect(streak);
            _hud.ShowStreak(streak);
            EmitSparks(position, Mint, 3.2f, 0.09f);

            // First run: spell out the combo goal until the player has seen a blast.
            if (_flow.BlastCount == 0)
            {
                int remaining = Mathf.Max(0, _flow.BlastStreakRequirement - streak);
                _hud.SetHintText(remaining > 0 ? remaining + " MORE FOR BLAST" : "BLAST!");
                _hud.ShowHint(true);
            }
        }

        private void OnBlockSliced(Vector3 position)
        {
            _audio.PlaySlice();
        }

        private void OnBlastFired(BlastInfo blast)
        {
            _audio.PlayBlast();
            _hud.ShowBanner("BLAST x" + blast.Layers, "+" + blast.Bonus, Gold);
            _hud.Flash(0.9f);

            EmitSparks(blast.Epicenter, Gold, 8.5f, 0.15f);
            EmitShockwave(blast.Epicenter, Gold);

            // Mark where the tower now ends, so the next block never appears out of nowhere.
            EmitShockwave(blast.NextTop, Mint);
            _hud.ShowHint(false);
        }

        private void OnGameEnded(int score, int best)
        {
            _gameOver = true;
            _gameOverTime = Time.unscaledTime;

            _audio.PlayGameOver();
            _hud.ShowHint(false);
            _hud.ShowGameOver(score, best);
        }

        private void EmitSparks(Vector3 position, Color color, float speed, float scale)
        {
            if (_sparkPool == null)
            {
                return;
            }

            SparkBurst burst = _sparkPool.Spawn(position, Vector3.one, Quaternion.identity) as SparkBurst;
            if (burst != null)
            {
                burst.Emit(color, speed, scale);
            }
        }

        private void EmitShockwave(Vector3 position, Color color)
        {
            if (_shockwavePool == null)
            {
                return;
            }

            Shockwave wave = _shockwavePool.Spawn(position, Vector3.one, Quaternion.identity) as Shockwave;
            if (wave != null)
            {
                wave.Play(color);
            }
        }

        private static Material LoadMaterial(string resourcePath, string fallbackShader)
        {
            Material material = Resources.Load<Material>(resourcePath);

            if (material != null)
            {
                return material;
            }

            // Editor play mode before the asset pass has run, or a stripped build: fall back.
            Shader shader = Shader.Find(fallbackShader);

            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Diffuse");
            }

            return new Material(shader) { enableInstancing = true };
        }

        private MovingBlock CreateBlockTemplate(Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BlockTemplate";

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;

            MovingBlock block = go.AddComponent<MovingBlock>();
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            return block;
        }

        private DebrisChunk CreateDebrisTemplate(Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "DebrisTemplate";
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;

            DebrisChunk chunk = go.AddComponent<DebrisChunk>();
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            return chunk;
        }

        private SparkBurst CreateSparkTemplate(Material material)
        {
            GameObject root = new GameObject("SparkTemplate");
            root.transform.SetParent(transform, false);

            Mesh cubeMesh = null;
            GameObject sample = GameObject.CreatePrimitive(PrimitiveType.Cube);
            MeshFilter sampleFilter = sample.GetComponent<MeshFilter>();
            if (sampleFilter != null)
            {
                cubeMesh = sampleFilter.sharedMesh;
            }

            DestroyImmediate(sample);

            for (int i = 0; i < Mathf.Max(4, sparksPerBurst); i++)
            {
                GameObject spark = new GameObject("Spark", typeof(MeshFilter), typeof(MeshRenderer));
                spark.transform.SetParent(root.transform, false);
                spark.GetComponent<MeshFilter>().sharedMesh = cubeMesh;

                MeshRenderer renderer = spark.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            SparkBurst burst = root.AddComponent<SparkBurst>();
            root.SetActive(false);
            return burst;
        }

        private Shockwave CreateShockwaveTemplate(Material material)
        {
            GameObject go = new GameObject("ShockwaveTemplate", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Ring(0.62f, 1f, 48);

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Shockwave wave = go.AddComponent<Shockwave>();
            go.SetActive(false);
            return wave;
        }

        private BlockPool CreatePool(string poolName, PooledObject template, int prewarm, int cap)
        {
            GameObject go = new GameObject(poolName);
            go.transform.SetParent(transform, false);
            go.SetActive(false);

            BlockPool pool = go.AddComponent<BlockPool>();
            pool.Configure(template, prewarm, cap);

            go.SetActive(true);
            return pool;
        }

        private CameraRig CreateCameraRig(Material unlitMaterial)
        {
            GameObject rigObject = new GameObject("CameraRig");
            rigObject.transform.position = Vector3.zero;

            Quaternion rotation = Quaternion.Euler(cameraAngles);

            GameObject cameraObject = new GameObject("MainCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(rigObject.transform, false);
            cameraObject.transform.localRotation = rotation;
            cameraObject.transform.localPosition = -(rotation * Vector3.forward) * cameraDistance;

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = skyBottom;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 90f;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            cameraObject.AddComponent<AudioListener>();
            CreateBackdrop(cameraObject.transform, unlitMaterial);

            return rigObject.AddComponent<CameraRig>();
        }

        // A flat gradient behind everything: costs one quad, kills the "grey test scene" look.
        private void CreateBackdrop(Transform cameraTransform, Material unlitMaterial)
        {
            Texture2D gradient = new Texture2D(1, 64, TextureFormat.RGBA32, false)
            {
                name = "SkyGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < gradient.height; y++)
            {
                float t = y / (float)(gradient.height - 1);
                gradient.SetPixel(0, y, Color.Lerp(skyBottom, skyTop, t * t));
            }

            gradient.Apply();

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Backdrop";

            Collider collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }

            Material material = new Material(unlitMaterial.shader) { mainTexture = gradient };

            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            quad.transform.SetParent(cameraTransform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 60f);
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(80f, 80f, 1f);
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new GameObject("KeyLight");
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(1f, 0.97f, 0.9f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.45f;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.42f);
        }
    }
}
