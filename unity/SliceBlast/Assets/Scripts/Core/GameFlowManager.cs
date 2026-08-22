// Slice & Blast — core flow controller.
// Owns the run: spawning, dynamic speed, the invisible tutorial, streaks, scoring and
// the blast reward. Presentation (audio, HUD, particles) subscribes to the events below.
using System;
using System.Collections.Generic;
using SliceBlast.Feedback;
using UnityEngine;

namespace SliceBlast.Core
{
    public enum PlacementKind : byte
    {
        Perfect = 0,
        Sliced = 1,
        Missed = 2
    }

    [DisallowMultipleComponent]
    public sealed class GameFlowManager : MonoBehaviour
    {
        private const string BestScoreKey = "sliceblast.best";

        public static GameFlowManager Instance { get; private set; }

        [Header("Pools")]
        [SerializeField] private BlockPool blockPool;
        [SerializeField] private BlockPool debrisPool;

        [Header("Layout")]
        [SerializeField] private Vector3 basePlatformSize = new Vector3(3f, 0.4f, 3f);
        [SerializeField] private Vector3 baseOrigin = Vector3.zero;
        [SerializeField] private float travelRange = 3.2f;

        [Header("Invisible Tutorial")]
        [SerializeField] private int tutorialBlocks = 3;
        [SerializeField, Range(0.2f, 1f)] private float tutorialSpeedScale = 0.5f;
        [SerializeField] private float tutorialBlendSeconds = 1.6f;

        [Header("Dynamic Speed")]
        [SerializeField] private float baseSpeed = 2.4f;
        [SerializeField] private float speedPerLayer = 0.12f;
        [SerializeField] private float maxSpeed = 9f;
        [SerializeField] private float speedSmoothing = 3.5f;
        [SerializeField, Range(0f, 0.5f)] private float comboBreakSlowdown = 0.16f;
        [SerializeField, Range(0f, 0.6f)] private float maxSlowdown = 0.32f;
        [SerializeField] private float slowdownRecovery = 0.1f;

        [Header("Blast Flow State")]
        [SerializeField] private int blastStreak = 3;
        [SerializeField] private int blastLayers = 3;
        [SerializeField] private float blastGrowth = 0.35f;
        [SerializeField] private int maxMultiplier = 5;
        [SerializeField] private float blastShake = 0.95f;
        [SerializeField] private float blastImpulse = 7f;
        [SerializeField] private float blastSpin = 6f;
        [SerializeField] private float comboSpeedBonus = 0.45f;

        [Header("Scoring")]
        [SerializeField] private int perfectBonus = 2;
        [SerializeField] private int blastLayerBonus = 15;

        // Onboarding grace: the very first block can never kill the run.
        [SerializeField] private bool forgiveFirstBlock = true;

        [Header("Feel")]
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private float perfectShake = 0.16f;
        [SerializeField] private float sliceShake = 0.07f;
        [SerializeField] private float deathTimeScale = 0.32f;
        [SerializeField] private float deathHoldSeconds = 0.45f;
        [SerializeField] private float hueStart = 0.55f;
        [SerializeField] private float hueStep = 0.028f;
        [SerializeField, Range(0f, 1f)] private float saturation = 0.55f;
        [SerializeField, Range(0f, 1f)] private float brightness = 0.95f;

        [Header("Runtime")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private int targetFrameRate = 60;

        public event Action RunStarted;
        public event Action<int, int> ScoreChanged;
        public event Action<int, Vector3> PerfectSnapped;
        public event Action<Vector3> BlockSliced;
        public event Action<int, int, Vector3> BlastFired;
        public event Action<int, int> GameEnded;

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
        private int _multiplier = 1;

        private float _speed;
        private float _tutorialProgress;
        private float _slowdown;
        private float _deathHold;

        public MovingBlock ActiveBlock => _active;
        public MovingBlock TopBlock => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
        public bool AcceptsInput => _running && _active != null && _active.IsMoving;
        public bool IsRunning => _running;
        public int PerfectStreak => _perfectStreak;
        public int BlastStreakRequirement => blastStreak;
        public int Score => _score;
        public int BestScore => _bestScore;
        public int Multiplier => _multiplier;
        public float CurrentSpeed => _speed;
        public int StackHeight => _stack.Count;

        /// <summary>Runtime wiring. Call on an inactive object so it lands before Awake.</summary>
        public void Configure(BlockPool blocks, BlockPool debris, CameraRig rig)
        {
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
                StartGame();
            }
        }

        public void StartGame()
        {
            ClearBoard();

            Time.timeScale = 1f;
            _deathHold = 0f;

            _score = 0;
            _perfectStreak = 0;
            _spawnCount = 0;
            _multiplier = 1;
            _slowdown = 0f;
            _tutorialProgress = 0f;
            _speed = baseSpeed * tutorialSpeedScale;
            _axisX = false;
            _nextSize = new Vector2(basePlatformSize.x, basePlatformSize.z);

            MovingBlock platform = SpawnBlock(baseOrigin, basePlatformSize, 0);
            platform.Freeze();
            _stack.Add(platform);

            if (cameraRig != null)
            {
                cameraRig.SnapToHeight(baseOrigin.y + basePlatformSize.y);
            }

            _running = true;
            _pendingSpawn = false;

            RunStarted?.Invoke();
            ScoreChanged?.Invoke(_score, _multiplier);
            SpawnNext();
        }

        public void Restart()
        {
            StartGame();
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

            if (!_running)
            {
                return;
            }

            UpdateDynamicSpeed(dt);

            if (_active != null)
            {
                _active.Tick(_speed, dt);
            }
        }

        // Deferred to LateUpdate so a blast fired from BlockSlicer.Update reshapes the run
        // before the next block is placed.
        private void LateUpdate()
        {
            if (_pendingSpawn && _running)
            {
                _pendingSpawn = false;
                SpawnNext();
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
            if (_running || Time.timeScale >= 1f)
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

        private void UpdateDynamicSpeed(float dt)
        {
            if (_spawnCount > tutorialBlocks)
            {
                _tutorialProgress = Mathf.MoveTowards(_tutorialProgress, 1f, dt / Mathf.Max(0.01f, tutorialBlendSeconds));
            }

            float tutorialFactor = Mathf.Lerp(tutorialSpeedScale, 1f, Mathf.SmoothStep(0f, 1f, _tutorialProgress));
            _slowdown = Mathf.MoveTowards(_slowdown, 0f, slowdownRecovery * dt);

            // Combo raises the stakes: every blast level makes the block travel faster.
            float combo = comboSpeedBonus * (_multiplier - 1);
            float target = Mathf.Min(baseSpeed + speedPerLayer * _spawnCount + combo, maxSpeed) * tutorialFactor * (1f - _slowdown);
            _speed = Mathf.LerpUnclamped(_speed, target, 1f - Mathf.Exp(-speedSmoothing * dt));
        }

        private void SpawnNext()
        {
            MovingBlock top = TopBlock;
            if (top == null)
            {
                return;
            }

            _axisX = !_axisX;

            Vector3 topPosition = top.CachedTransform.position;
            Vector3 size = new Vector3(_nextSize.x, basePlatformSize.y, _nextSize.y);
            Vector3 spawnPosition = new Vector3(topPosition.x, topPosition.y + basePlatformSize.y, topPosition.z);

            float side = (_spawnCount & 1) == 0 ? -1f : 1f;
            float pivot = _axisX ? topPosition.x : topPosition.z;

            if (_axisX)
            {
                spawnPosition.x = pivot + side * travelRange;
            }
            else
            {
                spawnPosition.z = pivot + side * travelRange;
            }

            MovingBlock block = SpawnBlock(spawnPosition, size, _stack.Count);
            block.Configure(_axisX, pivot, travelRange, -side);

            _active = block;
            _spawnCount++;

            if (cameraRig != null)
            {
                cameraRig.SetTargetHeight(spawnPosition.y);
            }
        }

        private MovingBlock SpawnBlock(Vector3 position, Vector3 size, int paletteIndex)
        {
            MovingBlock block = (MovingBlock)blockPool.Spawn(position, size, Quaternion.identity);
            block.SetTint(LayerColor(paletteIndex));
            return block;
        }

        private Color LayerColor(int index)
        {
            return Color.HSVToRGB(Mathf.Repeat(hueStart + index * hueStep, 1f), saturation, brightness);
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

            block.Freeze();
            _stack.Add(block);
            _active = null;

            Vector3 scale = block.CachedTransform.localScale;
            _nextSize = new Vector2(scale.x, scale.z);

            if (kind == PlacementKind.Perfect)
            {
                _perfectStreak++;
                _score += (1 + perfectBonus) * _multiplier;
                _slowdown = Mathf.Max(0f, _slowdown - comboBreakSlowdown * 0.5f);

                PlayImpact(block, 0.22f, 5f, true, new Color(0.6f, 1f, 0.92f));
                Haptics.Light();

                if (cameraRig != null)
                {
                    cameraRig.Shake(perfectShake);
                }

                PerfectSnapped?.Invoke(_perfectStreak, position);
            }
            else
            {
                _perfectStreak = 0;
                _score += _multiplier;
                _multiplier = 1;
                _slowdown = Mathf.Min(_slowdown + comboBreakSlowdown, maxSlowdown);

                PlayImpact(block, 0.16f, 6f, false, Color.white);
                Haptics.Medium();

                if (cameraRig != null)
                {
                    cameraRig.Shake(sliceShake);
                }

                BlockSliced?.Invoke(position);
            }

            ScoreChanged?.Invoke(_score, _multiplier);
            _pendingSpawn = true;
        }

        /// <summary>
        /// Blast: three perfect placements detonate those layers. The tower loses height but
        /// the run harvests a large bonus, the platform widens and the multiplier steps up —
        /// clearing is the pay-off, exactly like a line clear.
        /// </summary>
        public void TriggerBlast()
        {
            if (!_running || _stack.Count <= 1)
            {
                return;
            }

            // The base platform is never removed, otherwise there is nothing to build on.
            int removable = Mathf.Min(blastLayers, _stack.Count - 1);
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

                ShatterLayer(layer, epicenter);
                layer.Release();
            }

            int bonus = blastLayerBonus * removable * _multiplier;
            _score += bonus;

            _multiplier = Mathf.Min(_multiplier + 1, maxMultiplier);
            _perfectStreak = 0;
            _slowdown = 0f;

            // The width earned before the blast is kept and widened — the run gets easier
            // in space while it gets faster in time.
            _nextSize = new Vector2(
                Mathf.Min(_nextSize.x + blastGrowth, basePlatformSize.x),
                Mathf.Min(_nextSize.y + blastGrowth, basePlatformSize.z));

            MovingBlock top = TopBlock;
            if (top != null)
            {
                PlayImpact(top, 0.28f, 3.4f, true, new Color(1f, 0.79f, 0.29f));

                if (cameraRig != null)
                {
                    cameraRig.SetTargetHeight(top.CachedTransform.position.y + basePlatformSize.y);
                }
            }

            if (cameraRig != null)
            {
                cameraRig.Shake(blastShake);
            }

            Haptics.Heavy();
            BlastFired?.Invoke(_multiplier, bonus, epicenter);
            ScoreChanged?.Invoke(_score, _multiplier);
        }

        /// <summary>Bursts one layer into four pooled quadrants — a cheap, dense explosion.</summary>
        private void ShatterLayer(MovingBlock layer, Vector3 epicenter)
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
                Vector3 impulse = outward * blastImpulse + Vector3.up * (blastImpulse * 0.4f + lift);
                Vector3 torque = new Vector3(sz, 0f, -sx) * blastSpin;

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
            _multiplier = 1;

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

            // A beat of slow motion sells the failure and gives the eye time to follow the fall.
            Time.timeScale = Mathf.Clamp(deathTimeScale, 0.05f, 1f);
            _deathHold = deathHoldSeconds;

            Haptics.Heavy();
            GameEnded?.Invoke(_score, _bestScore);
        }

        private Vector3 TopBlockCenter()
        {
            MovingBlock top = TopBlock;
            return top != null ? top.CachedTransform.position : baseOrigin;
        }
    }
}
