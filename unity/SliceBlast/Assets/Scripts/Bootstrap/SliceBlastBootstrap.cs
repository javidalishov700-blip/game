// Composition root: builds the camera rig, lighting, backdrop, pools, spawner, audio, HUD
// and flow manager, then wires presentation to the signal bus. Nothing else knows about
// anything else.
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
        private const string SoundKey = "sliceblast.sound";
        private const string HapticsKey = "sliceblast.haptics";

        // Six is enough to read as a live discharge without looking like a swarm.
        private const int ArcNodeCount = 6;

        private static readonly Color Mint = new Color(0.55f, 1f, 0.9f);
        private static readonly Color MissRed = new Color(1f, 0.42f, 0.38f);

        [Header("Camera")]
        [SerializeField] private Vector3 cameraAngles = new Vector3(26f, 45f, 0f);
        [SerializeField] private float cameraDistance = 18f;
        [SerializeField] private float orthographicSize = 5.5f;
        [SerializeField] private Color skyTop = new Color(0.1f, 0.11f, 0.22f);
        [SerializeField] private Color skyBottom = new Color(0.03f, 0.03f, 0.06f);

        [Header("Pools")]
        [SerializeField] private int blockPrewarm = 48;
        [SerializeField] private int blockCap = 220;
        [SerializeField] private int debrisPrewarm = 40;
        [SerializeField] private int debrisCap = 160;
        [SerializeField] private int sparksPerBurst = 16;

        [Header("Flow")]
        [SerializeField] private float restartDelay = 0.55f;
        // Long enough that the tap which opened the app cannot also start the run.
        [SerializeField] private float homeInputDelay = 0.45f;

        private GameFlowManager _flow;
        private AudioDirector _audio;
        private GameHud _hud;
        private PoolManager _pools;

        private bool _gameOver;
        private float _gameOverTime;
        private bool _home;
        private float _homeTime;

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
            // Additive: with no post-processing in this pipeline, additive geometry is the
            // only thing that actually reads as a glow.
            Material glow = LoadMaterial("Materials/Glow", "Particles/Additive");
            Material glass = LoadMaterial("Materials/GlassLit", "Sprites/Default");

            MovingBlock blockTemplate = CreateBlockTemplate(lit, unlit, glow, glass);
            DebrisChunk debrisTemplate = CreateDebrisTemplate(lit);
            SparkBurst sparkTemplate = CreateSparkTemplate(lit);
            Shockwave shockwaveTemplate = CreateShockwaveTemplate(unlit);

            BlockPool blockPool = CreatePool("BlockPool", blockTemplate, blockPrewarm, blockCap);
            BlockPool debrisPool = CreatePool("DebrisPool", debrisTemplate, debrisPrewarm, debrisCap);
            BlockPool sparkPool = CreatePool("SparkPool", sparkTemplate, 6, 24);
            BlockPool shockwavePool = CreatePool("ShockwavePool", shockwaveTemplate, 4, 12);

            _pools = gameObject.AddComponent<PoolManager>();
            _pools.Register(PoolManager.Blocks, blockPool, false);
            _pools.Register(PoolManager.Debris, debrisPool, false); // ticked by the flow manager
            _pools.Register(PoolManager.Sparks, sparkPool, true);
            _pools.Register(PoolManager.Shockwaves, shockwavePool, true);

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
            _hud.HomeRequested += OnHomeRequested;
            _hud.SoundToggled += OnSoundToggled;
            _hud.HapticsToggled += OnHapticsToggled;

            GameObject spawnerObject = new GameObject("Spawner");
            spawnerObject.transform.SetParent(transform, false);
            BlockSpawner spawner = spawnerObject.AddComponent<BlockSpawner>();
            spawner.Bind(blockPool);

            Subscribe();

            GameObject flowObject = new GameObject("GameFlow");
            flowObject.transform.SetParent(transform, false);
            flowObject.SetActive(false);

            _flow = flowObject.AddComponent<GameFlowManager>();
            flowObject.AddComponent<BlockSlicer>();
            _flow.Configure(spawner, blockPool, debrisPool, rig);

            flowObject.SetActive(true);
        }

        private void OnDestroy()
        {
            Unsubscribe();

            if (_hud != null)
            {
                _hud.PauseToggled -= OnPauseToggled;
                _hud.RestartRequested -= OnRestartRequested;
                _hud.HomeRequested -= OnHomeRequested;
                _hud.SoundToggled -= OnSoundToggled;
                _hud.HapticsToggled -= OnHapticsToggled;
            }
        }

        private void Subscribe()
        {
            GameEvents.HomeShown += OnHomeShown;
            GameEvents.RunStarted += OnRunStarted;
            GameEvents.ScoreChanged += OnScoreChanged;
            GameEvents.BlockPlaced += OnBlockPlaced;
            GameEvents.BlastFired += OnBlastFired;
            GameEvents.RewardGranted += OnReward;
            GameEvents.ShieldChanged += OnShieldChanged;
            GameEvents.MultiplierTimerChanged += OnMultiplierTimer;
            GameEvents.RunEnded += OnRunEnded;
            GameEvents.PauseChanged += OnPauseChanged;
            GameEvents.NeonCharged += OnNeonCharged;
            GameEvents.CurrentPulsed += OnCurrentPulsed;
        }

        private void Unsubscribe()
        {
            GameEvents.HomeShown -= OnHomeShown;
            GameEvents.RunStarted -= OnRunStarted;
            GameEvents.ScoreChanged -= OnScoreChanged;
            GameEvents.BlockPlaced -= OnBlockPlaced;
            GameEvents.BlastFired -= OnBlastFired;
            GameEvents.RewardGranted -= OnReward;
            GameEvents.ShieldChanged -= OnShieldChanged;
            GameEvents.MultiplierTimerChanged -= OnMultiplierTimer;
            GameEvents.RunEnded -= OnRunEnded;
            GameEvents.PauseChanged -= OnPauseChanged;
            GameEvents.NeonCharged -= OnNeonCharged;
            GameEvents.CurrentPulsed -= OnCurrentPulsed;
        }

        private void Update()
        {
            if (_pools != null)
            {
                _pools.Tick(Time.deltaTime);
            }

            if (_home)
            {
                if (Time.unscaledTime - _homeTime < homeInputDelay)
                {
                    return;
                }

                if (BlockSlicer.TapPressedThisFrame() && !BlockSlicer.PointerOverUi())
                {
                    BeginRun();
                }

                return;
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

        private void BeginRun()
        {
            _home = false;
            _hud.HideHome();
            _audio.PlayStart();
            _flow.StartGame();
        }

        private void OnHomeShown(int best)
        {
            _home = true;
            _gameOver = false;
            _homeTime = Time.unscaledTime;

            _hud.HideGameOver();
            _hud.ShowPaused(false);
            _hud.ShowHint(false);
            _hud.ShowHome(best);
            _audio.PlayIntro();
        }

        private void OnRunStarted()
        {
            _gameOver = false;
            _home = false;
            _hud.HideHome();
            _hud.ShowRunChrome(true);
            _hud.HideGameOver();
            _hud.ShowPaused(false);
            _hud.SetHintText("TAP TO DROP");
            _hud.ShowHint(true);
        }

        private void OnScoreChanged(int score, int multiplier)
        {
            _hud.SetScore(score);
            _hud.SetMultiplier(multiplier);

            if (score > 0 && _flow != null && _flow.BlastCount > 0)
            {
                _hud.ShowHint(false);
            }
        }

        private void OnBlockPlaced(PlacementEvent placement)
        {
            BlockDefinition definition = BlockCatalogue.Get(placement.Type);

            switch (placement.Kind)
            {
                case PlacementKind.Perfect:
                    _audio.PlayPerfect(placement.Streak);
                    _hud.ShowStreak(placement.Streak);
                    EmitSparks(placement.Position, definition.UsesPalette ? Mint : placement.Color, 3.4f, 0.09f);
                    PlayLandingVoice(placement.Type);

                    // First run: spell out the combo goal until the player has seen a blast.
                    if (_flow != null && _flow.BlastCount == 0)
                    {
                        int remaining = Mathf.Max(0, _flow.BlastStreakRequirement - placement.Streak);
                        _hud.SetHintText(remaining > 0 ? remaining + " MORE FOR BLAST" : "BLAST!");
                        _hud.ShowHint(true);
                    }

                    break;

                case PlacementKind.Sliced:
                    _audio.PlaySlice();
                    break;

                case PlacementKind.Shielded:
                    _audio.PlayChime(1.3f);
                    _hud.Flash(0.25f);
                    EmitSparks(placement.Position, new Color(0.72f, 0.95f, 1f), 5f, 0.1f);
                    EmitShockwave(placement.Position, new Color(0.72f, 0.95f, 1f));
                    break;

                case PlacementKind.SpecialFailed:
                    // Say what was lost. A special that vanishes without a word reads as a
                    // bug rather than as the player's mistake.
                    PlayLostVoice(placement.Type);
                    _hud.ShowBanner("MISSED!", definition.Label + " LOST", MissRed);
                    EmitSparks(placement.Position, placement.Color, 5.5f, 0.11f);
                    EmitShockwave(placement.Position, placement.Color);
                    break;
            }
        }

        /// <summary>Each block says its own name when it lands.</summary>
        private void PlayLandingVoice(BlockType type)
        {
            switch (type)
            {
                case BlockType.Electric:
                    _audio.PlayZap();
                    _audio.PlayCrackle(0.5f);
                    break;

                case BlockType.Glass:
                    _audio.PlayChime();
                    break;

                case BlockType.Steel:
                    _audio.PlayPound();
                    _audio.PlayClank();
                    break;
            }
        }

        /// <summary>And its own name when it is fumbled.</summary>
        private void PlayLostVoice(BlockType type)
        {
            switch (type)
            {
                case BlockType.Neon:
                    _audio.PlayShatter();
                    _audio.PlayCrackle(0.3f);
                    break;

                case BlockType.Electric:
                    _audio.PlayShatter();
                    _audio.PlayCrackle(0.45f);
                    break;

                case BlockType.Glass:
                    _audio.PlayShatter();
                    _audio.PlayChime(0.7f);
                    break;

                case BlockType.Steel:
                    _audio.PlayClank(0.62f);
                    break;

                default:
                    _audio.PlayShatter();
                    break;
            }
        }

        private void OnBlastFired(BlastEvent blast)
        {
            _audio.PlayBlast();
            _hud.ShowBanner(blast.FromNeon ? "NEON BLAST" : "BLAST x" + blast.Layers, "+" + blast.Bonus, blast.Color);
            _hud.Flash(0.9f);

            EmitSparks(blast.Epicenter, blast.Color, 8.5f, 0.15f);
            EmitShockwave(blast.Epicenter, blast.Color);

            // Mark where the tower now ends, so the next block never appears out of nowhere.
            EmitShockwave(blast.NextTop, Mint);
            _hud.ShowHint(false);
        }

        private void OnReward(RewardEvent reward)
        {
            EmitSparks(reward.Position, reward.Color, 6f, 0.12f);

            switch (reward.Source)
            {
                case BlockType.Electric:
                    _audio.PlayZap();
                    _hud.Flash(0.35f);
                    EmitShockwave(reward.Position, reward.Color);
                    break;

                case BlockType.Steel:
                    EmitShockwave(reward.Position, reward.Color);
                    break;

                case BlockType.Glass:
                    EmitShockwave(reward.Position, reward.Color);
                    break;
            }
        }

        private void OnShieldChanged(bool active)
        {
            _hud.SetShield(active);
        }

        private void OnMultiplierTimer(float remaining, float total)
        {
            _hud.SetElectric(remaining, total);
        }

        private void OnNeonCharged(Vector3 position, Color color)
        {
            _audio.PlayNeonCharge();
            _hud.Flash(0.3f);
            EmitSparks(position, color, 5f, 0.11f);
        }

        private void OnCurrentPulsed(Vector3 position)
        {
            _audio.PlayCrackle();
            EmitSparks(position, BlockCatalogue.ElectricBlue, 2.6f, 0.06f);
        }

        private void OnRunEnded(int score, int best)
        {
            _gameOver = true;
            _gameOverTime = Time.unscaledTime;

            _audio.PlayGameOver();
            _hud.ShowHint(false);
            _hud.ShowGameOver(score, best);
        }

        private void OnPauseChanged(bool paused)
        {
            _hud.ShowPaused(paused);
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

        private void OnHomeRequested()
        {
            _audio.PlayTick();
            _gameOver = false;
            _flow.SetPaused(false);
            _flow.ShowHome();
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

        private void EmitSparks(Vector3 position, Color color, float speed, float scale)
        {
            SparkBurst burst = _pools.Spawn(PoolManager.Sparks, position, Vector3.one, Quaternion.identity) as SparkBurst;

            if (burst != null)
            {
                burst.Emit(color, speed, scale);
            }
        }

        private void EmitShockwave(Vector3 position, Color color)
        {
            Shockwave wave = _pools.Spawn(PoolManager.Shockwaves, position, Vector3.one, Quaternion.identity) as Shockwave;

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

        private MovingBlock CreateBlockTemplate(Material material, Material unlitMaterial, Material glowMaterial, Material glassMaterial)
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

            // Halo shell: an oversized transparent copy the special types animate, so each
            // one is identified by how it looks and moves rather than by a caption.
            GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Cube);
            aura.name = "Aura";

            Collider auraCollider = aura.GetComponent<Collider>();

            if (auraCollider != null)
            {
                DestroyImmediate(auraCollider);
            }

            MeshRenderer auraRenderer = aura.GetComponent<MeshRenderer>();
            auraRenderer.sharedMaterial = glowMaterial;
            auraRenderer.shadowCastingMode = ShadowCastingMode.Off;
            auraRenderer.receiveShadows = false;

            aura.transform.SetParent(go.transform, false);
            aura.transform.localScale = Vector3.one * 1.06f;
            aura.SetActive(false);

            // Symbol plate on the top face — from this camera it is the face the player
            // reads, so the bolt, the shield and the burst are legible without a caption.
            GameObject decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            decal.name = "Symbol";

            Collider decalCollider = decal.GetComponent<Collider>();

            if (decalCollider != null)
            {
                DestroyImmediate(decalCollider);
            }

            MeshRenderer decalRenderer = decal.GetComponent<MeshRenderer>();
            decalRenderer.sharedMaterial = unlitMaterial;
            decalRenderer.shadowCastingMode = ShadowCastingMode.Off;
            decalRenderer.receiveShadows = false;

            decal.transform.SetParent(go.transform, false);
            decal.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            decal.transform.localPosition = new Vector3(0f, 0.51f, 0f);
            decal.SetActive(false);

            // Live arcs for the Electric block: axis-aligned slivers that jump around its
            // shell. Rotating them inside a non-uniformly scaled block would shear them.
            GameObject arcRoot = new GameObject("Arcs");
            arcRoot.transform.SetParent(go.transform, false);

            Transform[] arcNodes = new Transform[ArcNodeCount];

            for (int i = 0; i < ArcNodeCount; i++)
            {
                GameObject node = GameObject.CreatePrimitive(PrimitiveType.Cube);
                node.name = "Arc";

                Collider nodeCollider = node.GetComponent<Collider>();

                if (nodeCollider != null)
                {
                    DestroyImmediate(nodeCollider);
                }

                MeshRenderer nodeRenderer = node.GetComponent<MeshRenderer>();
                nodeRenderer.sharedMaterial = glowMaterial;
                nodeRenderer.shadowCastingMode = ShadowCastingMode.Off;
                nodeRenderer.receiveShadows = false;

                node.transform.SetParent(arcRoot.transform, false);
                node.transform.localScale = Vector3.zero;
                arcNodes[i] = node.transform;
            }

            arcRoot.SetActive(false);

            block.SetupVisuals(
                aura.transform,
                auraRenderer,
                glassMaterial,
                decal.transform,
                decalRenderer,
                CreateSymbolMaterials(unlitMaterial, glowMaterial),
                arcRoot.transform,
                arcNodes);

            go.transform.SetParent(transform, false);
            go.SetActive(false);
            return block;
        }

        /// <summary>
        /// One shared material per block type, indexed by <see cref="BlockType"/>. Six
        /// materials for the whole game — the symbols never cost a per-block instance.
        /// </summary>
        private static Material[] CreateSymbolMaterials(Material unlitMaterial, Material glowMaterial)
        {
            Material[] materials = new Material[BlockCatalogue.Count];

            // Neon and Electric burn their symbol into the face additively — the block is
            // lit from inside. Glass and Steel take a dark engraved mark instead.
            materials[(int)BlockType.Neon] = SymbolMaterial(glowMaterial, IconShape.Burst, Color.white);
            materials[(int)BlockType.Electric] = SymbolMaterial(glowMaterial, IconShape.ElectricArcs, Color.white);
            materials[(int)BlockType.Glass] = SymbolMaterial(unlitMaterial, IconShape.Shield, Color.white);
            materials[(int)BlockType.Steel] = SymbolMaterial(unlitMaterial, IconShape.Chevrons, Color.white);

            return materials;
        }

        private static Material SymbolMaterial(Material source, IconShape shape, Color color)
        {
            return new Material(source.shader)
            {
                name = "Symbol_" + shape,
                mainTexture = IconFactory.GetTexture(shape),
                color = color,
                hideFlags = HideFlags.HideAndDontSave
            };
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
