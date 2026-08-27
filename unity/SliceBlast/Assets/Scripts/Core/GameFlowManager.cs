// Slice & Blast — the rules. Owns the tower, the run state, scoring, the combo blast,
// the 15-second Electric multiplier and the Glass shield. Raises signals; never touches
// audio, HUD or particles.
using System.Collections.Generic;
using SliceBlast.Feedback;
using UnityEngine;

namespace SliceBlast.Core
{
    [DisallowMultipleComponent]
    public sealed class GameFlowManager : MonoBehaviour
    {
        private const string BestScoreKey = "sliceblast.best";

        public static GameFlowManager Instance { get; private set; }

        [Header("Systems")]
        [SerializeField] private BlockSpawner spawner;
        [SerializeField] private BlockPool blockPool;
        [SerializeField] private BlockPool debrisPool;
        [SerializeField] private CameraRig cameraRig;

        [Header("Layout")]
        [SerializeField] private Vector3 basePlatformSize = new Vector3(3f, 0.4f, 3f);
        [SerializeField] private Vector3 baseOrigin = Vector3.zero;

        // The rewarded-ad "keep going": a real second chance, not a free one — the next block
        // comes in noticeably smaller than whatever was standing at death, and never wider
        // than the tower's own opening platform no matter how wide that last layer was.
        [SerializeField, Range(0.1f, 1f)] private float reviveSizeFraction = 0.6f;

        [Header("Invisible Tutorial")]
        [SerializeField] private int tutorialBlocks = 3;
        [SerializeField, Range(0.2f, 1f)] private float tutorialSpeedScale = 0.5f;
        [SerializeField] private float tutorialBlendSeconds = 2.2f;
        // Until the player has seen their first blast the magnet is wide open, so the
        // opening three taps land perfectly and the reward teaches itself.
        [SerializeField, Range(0f, 0.5f)] private float tutorialAssistFraction = 0.1f;

        // Toned down from 0.1/8.5/0.22: the old ramp reached max speed by layer ~60 and piled
        // a further +22% per combo level on top of that, so a tall, confident run stacked two
        // separate speed penalties on exactly the player who least deserved a punishing one.
        // Special blocks' own speedMultiplier (BlockCatalogue) then multiplied whatever this
        // produced — Steel at old maxSpeed was already close to double baseSpeed. The curve
        // still climbs to a real late-game challenge; it no longer compounds into one.
        [Header("Dynamic Speed")]
        [SerializeField] private float baseSpeed = 2.4f;
        [SerializeField] private float speedPerLayer = 0.085f;
        [SerializeField] private float maxSpeed = 7.6f;
        [SerializeField] private float speedSmoothing = 1.5f;
        [SerializeField] private float comboSpeedBonus = 0.16f;
        [SerializeField, Range(0f, 0.5f)] private float comboBreakSlowdown = 0.16f;
        [SerializeField, Range(0f, 0.6f)] private float maxSlowdown = 0.32f;
        [SerializeField] private float slowdownRecovery = 0.1f;

        [Header("Blast")]
        [SerializeField] private int blastStreak = 3;
        [SerializeField] private int blastBaseLayers = 3;
        [SerializeField] private int blastLayerStep = 2;
        [SerializeField] private int neonLayers = 3;
        [SerializeField] private int maxMultiplier = 9;
        [SerializeField] private float blastShake = 0.95f;
        [SerializeField] private float blastImpulse = 7f;
        [SerializeField] private float blastSpin = 6f;
        [SerializeField] private float blastPauseSeconds = 0.55f;

        [Header("Special Blocks")]
        [SerializeField] private float electricDuration = 15f;
        [SerializeField] private int electricMultiplier = 2;
        [SerializeField, Range(0f, 1f)] private float steelExpansion = 0.15f;
        [SerializeField] private int specialPerfectBonus = 25;
        // Neon marks the layers it is taking, holds for a beat, and only then breaks them.
        [SerializeField] private float neonFuseSeconds = 0.4f;
        [SerializeField] private float currentSpeed = 7f;
        [SerializeField] private float currentWidth = 2.2f;
        [SerializeField] private float currentPulseInterval = 0.35f;
        // The camera only ever frames the top of a tall tower — a sweep that starts at the
        // true bottom spends its first several seconds climbing through layers nobody can
        // see. Confining it to the top slice of the stack means it is on screen, and visibly
        // finishing a lap, from the moment the current starts.
        [SerializeField] private int currentVisibleLayers = 14;

        [Header("Scoring")]
        [SerializeField] private int perfectBonus = 2;
        [SerializeField] private int blastLayerBonus = 15;

        // Onboarding grace: the very first block can never kill the run.
        [SerializeField] private bool forgiveFirstBlock = true;

        [Header("Feel")]
        [SerializeField] private float perfectShake = 0.16f;
        [SerializeField] private float sliceShake = 0.07f;
        [SerializeField] private float deathTimeScale = 0.32f;
        [SerializeField] private float deathHoldSeconds = 0.45f;

        // A 7-block blast or bigger earns a beat of freeze-frame — short enough to read as a
        // punch, not a stutter, and only for the handful of blasts a run actually escalates to.
        [SerializeField] private int hitstopBlastThreshold = 7;
        [SerializeField] private float hitstopSeconds = 0.15f;
        [SerializeField] private float hitstopTimeScale = 0.02f;

        [Header("Runtime")]
        [SerializeField] private bool autoStart = true;
        // The tap that starts a run must not also drop its first block.
        [SerializeField] private float startInputLock = 0.3f;
        [SerializeField] private float homeZoom = 1.16f;
        [SerializeField] private int targetFrameRate = 60;

        private static readonly Color Gold = new Color(1f, 0.79f, 0.29f);
        private static readonly Color Mint = new Color(0.6f, 1f, 0.92f);

        private readonly List<MovingBlock> _stack = new List<MovingBlock>(128);
        private readonly List<MovingBlock> _animating = new List<MovingBlock>(16);

        private MovingBlock _active;
        private Vector2 _nextSize;
        private bool _axisX;
        private bool _running;
        private bool _pendingSpawn;

        private int _score;
        private int _bestScore;
        private int _perfectStreak;
        private int _spawnCount;
        private int _comboMultiplier = 1;
        private int _blastLevel;
        private int _blastCount;

        private float _speed;
        private float _tutorialProgress;
        private float _slowdown;
        private float _deathHold;
        private float _hitstopHold;
        private float _spawnDelay;
        private float _electricTimer;
        private float _idleGuard;
        private float _inputLock;

        private float _neonFuse;
        private int _neonLayers;
        private Color _neonColor = Color.white;

        private float _currentPhase;
        private float _currentPulse;
        private bool _currentActive;

        public MovingBlock ActiveBlock => _active;
        public MovingBlock TopBlock => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
        public bool AcceptsInput => _running && !IsPaused && _inputLock <= 0f && _active != null && _active.IsMoving;
        public bool IsRunning => _running;
        public bool IsHome { get; private set; }
        public bool IsPaused { get; private set; }
        public bool HasShield { get; private set; }
        public int PerfectStreak => _perfectStreak;
        public int BlastStreakRequirement => blastStreak;
        public int Score => _score;
        public int BestScore => _bestScore;
        public int ComboMultiplier => _comboMultiplier;
        public int TotalMultiplier => _comboMultiplier * (_electricTimer > 0f ? electricMultiplier : 1);
        public float CurrentSpeed => _speed;
        public int StackHeight => _stack.Count;
        public int BlastCount => _blastCount;
        public int NextBlastLayers => blastBaseLayers + blastLayerStep * _blastLevel;

        /// <summary>Extra magnet radius while the opening tutorial is still running.</summary>
        public float TutorialAssist
        {
            get
            {
                if (_blastCount > 0 || _active == null)
                {
                    return 0f;
                }

                MovingBlock top = TopBlock;

                if (top == null)
                {
                    return 0f;
                }

                Vector3 size = top.CachedTransform.localScale;
                float axisSize = _active.MovingAxisX ? size.x : size.z;
                return axisSize * tutorialAssistFraction;
            }
        }

        /// <summary>Runtime wiring. Call on an inactive object so it lands before Awake.</summary>
        public void Configure(BlockSpawner blockSpawner, BlockPool blocks, BlockPool debris, CameraRig rig)
        {
            spawner = blockSpawner;
            blockPool = blocks;
            debrisPool = debris;
            cameraRig = rig;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);

            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            if (targetFrameRate > 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = targetFrameRate;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (autoStart)
            {
                ShowHome();
            }
        }

        /// <summary>
        /// The title screen state: the board is reset and the base platform is standing,
        /// but nothing moves until the player taps. Also where a run returns on restart.
        /// </summary>
        public void ShowHome()
        {
            ResetBoard(true);
            IsHome = true;

            if (cameraRig != null)
            {
                cameraRig.ZoomTo(homeZoom, true);
            }

            GameEvents.RaiseHomeShown(_bestScore);
        }

        public void StartGame()
        {
            // Not snapped: the camera eases in from the title framing (or from the pulled
            // back end-of-run shot) while the first block slides on.
            ResetBoard(false);

            IsHome = false;
            _running = true;
            _inputLock = Mathf.Max(0f, startInputLock);

            GameEvents.RaiseRunStarted();
            GameEvents.RaiseScoreChanged(_score, TotalMultiplier);

            SpawnNext();
        }

        /// <summary>Everything both the title screen and a fresh run need: one clean board.</summary>
        private void ResetBoard(bool snapCamera)
        {
            ClearBoard();

            IsPaused = false;
            Time.timeScale = 1f;
            _deathHold = 0f;
            _hitstopHold = 0f;
            _spawnDelay = 0f;
            _electricTimer = 0f;
            _idleGuard = 0f;
            _inputLock = 0f;
            _neonFuse = 0f;
            _currentPhase = 0f;
            _currentPulse = 0f;
            _currentActive = false;

            _score = 0;
            _perfectStreak = 0;
            _spawnCount = 0;
            _comboMultiplier = 1;
            _blastLevel = 0;
            _blastCount = 0;
            _slowdown = 0f;
            _tutorialProgress = 0f;
            _speed = baseSpeed * tutorialSpeedScale;
            _axisX = false;
            _nextSize = new Vector2(basePlatformSize.x, basePlatformSize.z);

            _running = false;
            _pendingSpawn = false;

            HasShield = false;
            spawner.ResetRun();

            MovingBlock platform = (MovingBlock)blockPool.Spawn(baseOrigin, basePlatformSize, Quaternion.identity);
            platform.SetTint(new Color(0.35f, 0.4f, 0.55f));
            platform.Freeze();
            _stack.Add(platform);

            // The platform lands rather than appears.
            PlayImpact(platform, 0.3f, 3.5f, false, Color.white);

            if (cameraRig != null)
            {
                // Camera yawed 45°: a block of half-extent a spans 1.414a across the screen
                // and a swing of t moves its centre 0.707t. Fit the swing plus most of the
                // block — only the widest opening block clips a corner at full extension.
                float requiredHalfWidth = 0.707f * spawner.TravelRange
                                          + 1.13f * (basePlatformSize.x * 0.5f)
                                          + 0.2f;

                cameraRig.FitPlaySize(requiredHalfWidth, snapCamera);

                if (snapCamera)
                {
                    cameraRig.SnapToHeight(baseOrigin.y + basePlatformSize.y);
                }
                else
                {
                    cameraRig.SetTargetHeight(baseOrigin.y + basePlatformSize.y);
                }
            }

            GameEvents.RaisePauseChanged(false);
            GameEvents.RaiseShieldChanged(false);
            GameEvents.RaiseMultiplierTimer(0f, electricDuration);
        }

        public void Restart()
        {
            StartGame();
        }

        public void SetPaused(bool paused)
        {
            if (!_running || IsPaused == paused)
            {
                return;
            }

            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            GameEvents.RaisePauseChanged(paused);
        }

        public void TogglePause()
        {
            SetPaused(!IsPaused);
        }

        private void ClearBoard()
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i] != null)
                {
                    _stack[i].Release();
                }
            }

            _stack.Clear();
            _animating.Clear();

            if (_active != null)
            {
                _active.Release();
                _active = null;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (debrisPool != null)
            {
                debrisPool.Tick(dt);
            }

            TickImpacts(dt);
            RecoverFromDeathSlowMotion();
            TickHitstop();

            if (_inputLock > 0f)
            {
                _inputLock = Mathf.Max(0f, _inputLock - Time.unscaledDeltaTime);
            }

            if (!_running || IsPaused)
            {
                return;
            }

            TickElectric(dt);
            UpdateDynamicSpeed(dt);
            GuardAgainstStall(dt);

            TickNeonFuse(dt);

            if (_active != null)
            {
                _active.Tick(_speed, dt);
            }
        }

        // Deferred to LateUpdate so a blast reshapes the tower before the next block lands.
        private void LateUpdate()
        {
            // Nothing slides in while neon is holding the layers it is about to take.
            if (!_pendingSpawn || !_running || IsPaused || _neonFuse > 0f)
            {
                return;
            }

            if (_spawnDelay > 0f)
            {
                _spawnDelay -= Time.unscaledDeltaTime;
                return;
            }

            _pendingSpawn = false;
            SpawnNext();
        }

        /// <summary>
        /// Safety net: whatever goes wrong in a reward path, the run must never sit there
        /// with nothing to tap. If no block is in flight and none is queued, queue one.
        /// </summary>
        private void GuardAgainstStall(float deltaTime)
        {
            if (_active != null || _pendingSpawn)
            {
                _idleGuard = 0f;
                return;
            }

            _idleGuard += deltaTime;

            if (_idleGuard > 1.5f)
            {
                _idleGuard = 0f;
                _spawnDelay = 0f;
                _pendingSpawn = true;
            }
        }

        private void TickElectric(float deltaTime)
        {
            if (_electricTimer <= 0f)
            {
                if (_currentActive)
                {
                    ClearCurrent();
                }

                return;
            }

            float previous = _electricTimer;
            _electricTimer = Mathf.Max(0f, _electricTimer - deltaTime);
            GameEvents.RaiseMultiplierTimer(_electricTimer, electricDuration);

            TickCurrent(deltaTime);

            if (_electricTimer <= 0f && previous > 0f)
            {
                ClearCurrent();
                GameEvents.RaiseScoreChanged(_score, TotalMultiplier);
            }
        }

        /// <summary>
        /// The current: a bright head running up the tower for as long as the Electric
        /// multiplier lasts. Only the few layers around the head write anything.
        /// </summary>
        private void TickCurrent(float deltaTime)
        {
            int count = _stack.Count;

            if (count == 0)
            {
                return;
            }

            _currentActive = true;
            _currentPhase += deltaTime * currentSpeed;

            // Loop inside the top slice of the tower rather than the whole height: on a tall
            // tower the base is off camera, and a sweep that has to climb all the way from it
            // burns most of the 15 seconds before it is ever visible.
            int windowSize = Mathf.Min(count, currentVisibleLayers);
            int windowStart = count - windowSize;
            float span = windowSize + 3f;
            float head = Mathf.Repeat(_currentPhase, span);

            for (int i = 0; i < count; i++)
            {
                MovingBlock layer = _stack[i];

                if (layer == null)
                {
                    continue;
                }

                if (i < windowStart)
                {
                    layer.SetCharge(0f);
                    continue;
                }

                float local = i - windowStart;
                layer.SetCharge(Mathf.Clamp01(1f - Mathf.Abs(local - head) / currentWidth));
            }

            _currentPulse -= deltaTime;

            if (_currentPulse <= 0f)
            {
                _currentPulse = currentPulseInterval;

                int localIndex = Mathf.Clamp(Mathf.RoundToInt(head), 0, windowSize - 1);
                MovingBlock at = _stack[windowStart + localIndex];

                if (at != null)
                {
                    GameEvents.RaiseCurrentPulsed(at.CachedTransform.position);
                }
            }
        }

        private void ClearCurrent()
        {
            _currentActive = false;
            _currentPhase = 0f;
            _currentPulse = 0f;

            for (int i = 0; i < _stack.Count; i++)
            {
                if (_stack[i] != null)
                {
                    _stack[i].SetCharge(0f);
                }
            }
        }

        /// <summary>Neon: the marked layers hold for a beat, then go.</summary>
        private void TickNeonFuse(float deltaTime)
        {
            if (_neonFuse <= 0f)
            {
                return;
            }

            _neonFuse -= deltaTime;

            if (_neonFuse <= 0f)
            {
                _neonFuse = 0f;
                TriggerBlast(_neonLayers, true);
            }
        }

        /// <summary>Paints the layers neon is about to take in that block's own colour.</summary>
        private void MarkNeonLayers(int count)
        {
            int marked = 0;

            for (int i = _stack.Count - 1; i >= 1 && marked < count; i--, marked++)
            {
                MovingBlock layer = _stack[i];

                if (layer == null)
                {
                    continue;
                }

                layer.SetGlow(_neonColor, _neonColor * 1.4f);
                PlayImpact(layer, 0.18f, 4.5f, false, _neonColor);
            }
        }

        private void TickImpacts(float deltaTime)
        {
            for (int i = _animating.Count - 1; i >= 0; i--)
            {
                MovingBlock block = _animating[i];

                if (block == null || !block.TickImpact(deltaTime))
                {
                    int last = _animating.Count - 1;
                    _animating[i] = _animating[last];
                    _animating.RemoveAt(last);
                }
            }
        }

        private void RecoverFromDeathSlowMotion()
        {
            if (_running || IsPaused || Time.timeScale >= 1f)
            {
                return;
            }

            if (_deathHold > 0f)
            {
                _deathHold -= Time.unscaledDeltaTime;
                return;
            }

            Time.timeScale = Mathf.MoveTowards(Time.timeScale, 1f, Time.unscaledDeltaTime * 1.4f);
        }

        /// <summary>
        /// A hard freeze rather than an eased one — a hitstop reads as a punch precisely
        /// because it lets go all at once instead of ramping back up. Runs independently of
        /// death's slow motion and of pause, both of which own Time.timeScale in their own
        /// states; a blast never fires in either of those, so the two never actually collide.
        /// </summary>
        private void TickHitstop()
        {
            if (_hitstopHold <= 0f)
            {
                return;
            }

            _hitstopHold -= Time.unscaledDeltaTime;

            if (_hitstopHold <= 0f && !IsPaused)
            {
                Time.timeScale = 1f;
            }
        }

        private void UpdateDynamicSpeed(float dt)
        {
            if (_spawnCount > tutorialBlocks)
            {
                _tutorialProgress = Mathf.MoveTowards(_tutorialProgress, 1f, dt / Mathf.Max(0.01f, tutorialBlendSeconds));
            }

            float tutorialFactor = Mathf.Lerp(tutorialSpeedScale, 1f, Mathf.SmoothStep(0f, 1f, _tutorialProgress));
            _slowdown = Mathf.MoveTowards(_slowdown, 0f, slowdownRecovery * dt);

            // Combo raises the stakes, but gently: the target creeps up and the block eases
            // into it over a couple of seconds rather than jumping.
            float combo = comboSpeedBonus * (_comboMultiplier - 1);
            float target = Mathf.Min(baseSpeed + speedPerLayer * _spawnCount + combo, maxSpeed) * tutorialFactor * (1f - _slowdown);
            _speed = Mathf.LerpUnclamped(_speed, target, 1f - Mathf.Exp(-speedSmoothing * dt));
        }

        private void SpawnNext()
        {
            MovingBlock top = TopBlock;

            if (top == null || spawner == null)
            {
                return;
            }

            _axisX = !_axisX;

            MovingBlock block = spawner.Spawn(top.CachedTransform.position, _nextSize, basePlatformSize.y, _axisX, _stack.Count);
            _active = block;
            _spawnCount++;

            if (cameraRig != null)
            {
                cameraRig.SetTargetHeight(block.CachedTransform.position.y);
            }
        }

        /// <summary>Consumed by the slicer when a Standard block is about to be cut.</summary>
        public bool ConsumeShield()
        {
            if (!HasShield)
            {
                return false;
            }

            HasShield = false;
            GameEvents.RaiseShieldChanged(false);
            return true;
        }

        // Called by BlockSlicer once a tap has been resolved.
        public void CommitPlacement(PlacementKind kind, MovingBlock block)
        {
            if (!_running || block == null)
            {
                return;
            }

            if (kind == PlacementKind.Missed)
            {
                MovingBlock top = TopBlock;

                if (!forgiveFirstBlock || _stack.Count != 1 || top == null)
                {
                    EndRun(block);
                    return;
                }

                Vector3 topPosition = top.CachedTransform.position;
                block.SnapAxis(block.MovingAxisX ? topPosition.x : topPosition.z);
                kind = PlacementKind.Perfect;
            }

            Vector3 position = block.CachedTransform.position;
            BlockType type = block.Type;
            Color tint = block.Tint;

            block.Freeze();
            _stack.Add(block);
            _active = null;

            Vector3 scale = block.CachedTransform.localScale;
            _nextSize = new Vector2(scale.x, scale.z);

            switch (kind)
            {
                case PlacementKind.Perfect:
                    _perfectStreak++;
                    _score += (1 + perfectBonus) * TotalMultiplier;
                    _slowdown = Mathf.Max(0f, _slowdown - comboBreakSlowdown * 0.5f);
                    PlayImpact(block, 0.22f, 5f, true, Mint);
                    Haptics.Light();
                    Shake(perfectShake);
                    break;

                case PlacementKind.Shielded:
                    _score += TotalMultiplier;
                    PlayImpact(block, 0.2f, 5f, true, new Color(0.72f, 0.95f, 1f));
                    Haptics.Medium();
                    Shake(sliceShake);
                    break;

                default:
                    _perfectStreak = 0;
                    _comboMultiplier = 1;
                    _blastLevel = 0;
                    _score += TotalMultiplier;
                    _slowdown = Mathf.Min(_slowdown + comboBreakSlowdown, maxSlowdown);
                    PlayImpact(block, 0.16f, 6f, false, Color.white);
                    Haptics.Medium();
                    Shake(sliceShake);
                    break;
            }

            GameEvents.RaiseBlockPlaced(new PlacementEvent
            {
                Type = type,
                Kind = kind,
                Position = position,
                Color = tint,
                Streak = _perfectStreak
            });

            if (kind == PlacementKind.Perfect)
            {
                ApplySpecialReward(type, position);
            }

            GameEvents.RaiseScoreChanged(_score, TotalMultiplier);
            _pendingSpawn = true;

            if (kind == PlacementKind.Perfect && _perfectStreak >= blastStreak && _neonFuse <= 0f)
            {
                TriggerBlast(NextBlastLayers, false);
            }
        }

        private void ApplySpecialReward(BlockType type, Vector3 position)
        {
            switch (type)
            {
                case BlockType.Neon:
                    _score += specialPerfectBonus * TotalMultiplier;
                    _neonColor = _stack.Count > 0 ? _stack[_stack.Count - 1].Tint : Color.white;
                    _neonLayers = neonLayers;
                    _neonFuse = Mathf.Max(0.05f, neonFuseSeconds);
                    MarkNeonLayers(neonLayers);
                    GameEvents.RaiseNeonCharged(position, _neonColor);
                    break;

                case BlockType.Electric:
                    _electricTimer = electricDuration;
                    _score += specialPerfectBonus * TotalMultiplier;
                    _currentPulse = 0f;
                    GameEvents.RaiseMultiplierTimer(_electricTimer, electricDuration);
                    Reward(type, string.Empty, string.Empty, BlockCatalogue.ElectricBlue, position, 0);
                    break;

                case BlockType.Glass:
                    HasShield = true;
                    _score += specialPerfectBonus * TotalMultiplier;
                    GameEvents.RaiseShieldChanged(true);
                    Reward(type, string.Empty, string.Empty, new Color(0.78f, 0.97f, 1f), position, 0);
                    break;

                case BlockType.Steel:
                    GrowByFraction(steelExpansion);
                    _score += specialPerfectBonus * TotalMultiplier;
                    Shake(0.45f);
                    Haptics.Heavy();
                    Reward(type, string.Empty, string.Empty, new Color(0.82f, 0.86f, 0.92f), position, 0);
                    break;

            }
        }

        /// <summary>
        /// A fumbled special never touches the tower: it shatters, the combo dies and the
        /// spawner puts a plain block up next so the rhythm can restart.
        /// </summary>
        public void FailSpecial(MovingBlock block)
        {
            if (!_running || block == null)
            {
                return;
            }

            Vector3 position = block.CachedTransform.position;
            BlockType type = block.Type;
            Color tint = block.Tint;

            ShatterLayer(block, position, 0.55f);
            block.Release();
            _active = null;

            _perfectStreak = 0;
            _comboMultiplier = 1;
            _blastLevel = 0;
            _slowdown = Mathf.Min(_slowdown + comboBreakSlowdown, maxSlowdown);

            spawner.ForceStandardNext();

            Haptics.Medium();
            Shake(sliceShake);

            GameEvents.RaiseBlockPlaced(new PlacementEvent
            {
                Type = type,
                Kind = PlacementKind.SpecialFailed,
                Position = position,
                Color = tint,
                Streak = 0
            });

            GameEvents.RaiseScoreChanged(_score, TotalMultiplier);
            _pendingSpawn = true;
        }

        /// <summary>
        /// Detonates the top layers. The combo blast escalates (3, 5, 7 …) while the streak
        /// holds; a Neon block always clears its own fixed count and widens the base further.
        /// </summary>
        public void TriggerBlast(int requestedLayers, bool fromNeon)
        {
            if (!_running || _stack.Count <= 1)
            {
                return;
            }

            int available = _stack.Count - 1; // the base platform is never removed
            int removable = Mathf.Min(Mathf.Max(1, requestedLayers), available);
            bool exhausted = removable < requestedLayers;

            Vector3 epicenter = _stack[_stack.Count - 1].CachedTransform.position;

            for (int i = 0; i < removable; i++)
            {
                int last = _stack.Count - 1;
                MovingBlock layer = _stack[last];
                _stack.RemoveAt(last);

                int index = _animating.IndexOf(layer);

                if (index >= 0)
                {
                    _animating.RemoveAt(index);
                }

                ShatterLayer(layer, epicenter, 1f);
                layer.Release();
            }

            int bonus = blastLayerBonus * removable * TotalMultiplier;
            _score += bonus;
            _blastCount++;

            _comboMultiplier = Mathf.Min(_comboMultiplier + 1, maxMultiplier);
            _blastLevel = exhausted ? 0 : _blastLevel + 1;
            _perfectStreak = 0;
            _slowdown = 0f;

            Vector3 nextTop = epicenter;
            MovingBlock top = TopBlock;

            if (top != null)
            {
                // The exposed layer becomes the new base: clearing downwards hands the
                // player back the width those lower blocks still have.
                Vector3 exposed = top.CachedTransform.localScale;
                _nextSize = new Vector2(exposed.x, exposed.z);

                nextTop = top.CachedTransform.position;
                PlayImpact(top, 0.3f, 3f, true, Gold);

                if (cameraRig != null)
                {
                    cameraRig.SetTargetHeight(nextTop.y + basePlatformSize.y);
                }
            }

            Shake(blastShake);

            if (removable >= hitstopBlastThreshold)
            {
                _hitstopHold = hitstopSeconds;
                Time.timeScale = hitstopTimeScale;
            }

            // Hold the next spawn so the explosion, the camera drop and the new top all
            // read before another block slides in.
            _spawnDelay = blastPauseSeconds;

            Haptics.Heavy();

            GameEvents.RaiseBlastFired(new BlastEvent
            {
                Layers = removable,
                Multiplier = _comboMultiplier,
                Bonus = bonus,
                Epicenter = epicenter,
                NextTop = nextTop,
                Color = fromNeon ? _neonColor : Gold,
                FromNeon = fromNeon
            });

            GameEvents.RaiseScoreChanged(_score, TotalMultiplier);
        }

        /// <summary>Widens the next platform by a share of its current size.</summary>
        private void GrowByFraction(float fraction)
        {
            _nextSize = new Vector2(
                Mathf.Min(_nextSize.x * (1f + fraction), basePlatformSize.x),
                Mathf.Min(_nextSize.y * (1f + fraction), basePlatformSize.z));
        }

        private void Reward(BlockType source, string headline, string detail, Color color, Vector3 position, int points)
        {
            GameEvents.RaiseReward(new RewardEvent
            {
                Source = source,
                Headline = headline,
                Detail = detail,
                Color = color,
                Position = position,
                Points = points
            });
        }

        private void Shake(float amount)
        {
            if (cameraRig != null)
            {
                cameraRig.Shake(amount);
            }
        }

        /// <summary>Bursts one layer into four pooled quadrants — a cheap, dense explosion.</summary>
        private void ShatterLayer(MovingBlock layer, Vector3 epicenter, float force)
        {
            Transform t = layer.CachedTransform;
            Vector3 center = t.position;
            Vector3 scale = t.localScale;
            Vector3 quadrant = new Vector3(scale.x * 0.5f, scale.y, scale.z * 0.5f);
            Color tint = layer.Tint;

            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f;
                float sz = (i & 2) == 0 ? -1f : 1f;

                Vector3 position = new Vector3(
                    center.x + sx * quadrant.x * 0.5f,
                    center.y,
                    center.z + sz * quadrant.z * 0.5f);

                Vector3 outward = new Vector3(sx, 0f, sz).normalized;
                float lift = Mathf.Max(0.3f, position.y - epicenter.y + 1f);
                Vector3 impulse = (outward * blastImpulse + Vector3.up * (blastImpulse * 0.4f + lift)) * force;
                Vector3 torque = new Vector3(sz, 0f, -sx) * blastSpin * force;

                EmitDebris(position, quadrant, tint, impulse, torque);
            }
        }

        private void PlayImpact(MovingBlock block, float strength, float speed, bool flash, Color flashColor)
        {
            if (block == null)
            {
                return;
            }

            block.PlayImpact(strength, speed, flash, flashColor);

            if (!_animating.Contains(block))
            {
                _animating.Add(block);
            }
        }

        public void EmitDebris(Vector3 position, Vector3 scale, Color tint, Vector3 impulse, Vector3 torque)
        {
            if (debrisPool == null)
            {
                return;
            }

            DebrisChunk chunk = debrisPool.Spawn(position, scale, Quaternion.identity) as DebrisChunk;

            if (chunk == null)
            {
                return;
            }

            chunk.SetTint(tint);
            chunk.Launch(impulse, torque);
        }

        private void EndRun(MovingBlock block)
        {
            _running = false;
            _active = null;
            _pendingSpawn = false;
            _comboMultiplier = 1;
            _blastLevel = 0;
            _electricTimer = 0f;
            _neonFuse = 0f;
            HasShield = false;
            ClearCurrent();

            Transform t = block.CachedTransform;
            Vector3 fallDirection = (t.position - TopBlockCenter()).normalized;
            EmitDebris(t.position, t.localScale, block.Tint, fallDirection * 1.5f, Vector3.up * 0.5f);
            block.Release();

            if (_score > _bestScore)
            {
                _bestScore = _score;
                PlayerPrefs.SetInt(BestScoreKey, _bestScore);
                PlayerPrefs.Save();
            }

            // Pull back far enough to show the run: base platform to the last layer placed.
            if (cameraRig != null)
            {
                MovingBlock top = TopBlock;
                float topY = top != null
                    ? top.CachedTransform.position.y + basePlatformSize.y
                    : baseOrigin.y + basePlatformSize.y;

                cameraRig.FrameTower(baseOrigin.y - basePlatformSize.y, topY);
            }

            // A beat of slow motion sells the failure and gives the eye time to follow the fall.
            Time.timeScale = Mathf.Clamp(deathTimeScale, 0.05f, 1f);
            _deathHold = deathHoldSeconds;

            Haptics.Heavy();

            GameEvents.RaiseShieldChanged(false);
            GameEvents.RaiseMultiplierTimer(0f, electricDuration);
            GameEvents.RaiseRunEnded(_score, _bestScore);
        }

        /// <summary>
        /// The rewarded-ad "keep going": a real second chance, not a free one. The next block
        /// lands noticeably smaller than whatever was standing at death — capped so it is
        /// never wider than the tower's own opening platform, however wide that layer was —
        /// and play resumes exactly where it stopped rather than restarting the tower.
        /// </summary>
        public bool TryRevive()
        {
            MovingBlock top = TopBlock;

            if (_running || top == null)
            {
                return false;
            }

            _nextSize = new Vector2(
                Mathf.Min(_nextSize.x * reviveSizeFraction, basePlatformSize.x),
                Mathf.Min(_nextSize.y * reviveSizeFraction, basePlatformSize.z));

            // A continued run is a fresh mini-run for streak purposes — carrying a pre-death
            // streak across the revive would hand out blasts and bonuses it was not earned.
            _perfectStreak = 0;

            _running = true;
            _pendingSpawn = true;
            _spawnDelay = 0f;
            Time.timeScale = 1f;
            _deathHold = 0f;

            if (cameraRig != null)
            {
                // Same fit as a fresh board (see ResetBoard) — the pulled-back run-over shot
                // has to zoom back in to a normal play view before the next block can swing.
                float requiredHalfWidth = 0.707f * spawner.TravelRange
                                          + 1.13f * (basePlatformSize.x * 0.5f)
                                          + 0.2f;

                cameraRig.FitPlaySize(requiredHalfWidth, false);
                cameraRig.SetTargetHeight(top.CachedTransform.position.y + basePlatformSize.y);
            }

            return true;
        }

        private Vector3 TopBlockCenter()
        {
            MovingBlock top = TopBlock;
            return top != null ? top.CachedTransform.position : baseOrigin;
        }
    }
}
