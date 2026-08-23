using UnityEngine;

namespace SliceBlast.Core
{
    /// <summary>A tower layer. Ping-pongs along one axis until it is snapped, sliced or lost.</summary>
    public sealed class MovingBlock : PooledObject
    {
        public bool MovingAxisX { get; private set; }
        public bool IsMoving { get; private set; }
        public BlockType Type { get; private set; }

        private float _pivot;
        private float _range;
        private float _direction = 1f;
        private float _speedMultiplier = 1f;

        private float _glitchJump;
        private bool _glitched;
        private bool _glitchPending;

        [SerializeField] private Transform aura;
        [SerializeField] private Renderer auraRenderer;
        [SerializeField] private Material transparentMaterial;
        [SerializeField] private Transform decal;
        [SerializeField] private Renderer decalRenderer;
        [SerializeField] private Material[] decalMaterials;

        private static MaterialPropertyBlock s_auraBlock;
        private static readonly int AuraBaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int AuraColorId = Shader.PropertyToID("_Color");

        private Color _auraColor = Color.white;
        private float _auraPhase;

        private Vector3 _restScale;
        private Color _restTint;
        private Color _flashTint;
        private float _impact;
        private float _impactStrength;
        private float _impactSpeed = 4f;
        private bool _impactFlashes;

        public float AxisCenter
        {
            get
            {
                Vector3 p = CachedTransform.position;
                return MovingAxisX ? p.x : p.z;
            }
        }

        public float AxisSize
        {
            get
            {
                Vector3 s = CachedTransform.localScale;
                return MovingAxisX ? s.x : s.z;
            }
        }

        public override void OnSpawned()
        {
            IsMoving = false;
            _direction = 1f;
            _speedMultiplier = 1f;
            _glitchJump = 0f;
            _glitched = false;
            _glitchPending = false;
            _impact = 0f;
            _auraPhase = 0f;
            Type = BlockType.Standard;

            ResetMaterial();
            ShowAura(false);
            ShowDecal(false);
        }

        /// <summary>Called once on the pooled template so every clone carries the same assets.</summary>
        public void SetupVisuals(Transform auraTransform, Renderer auraRend, Material transparent, Transform decalTransform, Renderer decalRend, Material[] symbols)
        {
            aura = auraTransform;
            auraRenderer = auraRend;
            transparentMaterial = transparent;
            decal = decalTransform;
            decalRenderer = decalRend;
            decalMaterials = symbols;
        }

        /// <summary>
        /// Each type has to be readable at a glance, without a label: colour, halo behaviour
        /// and — for Glass — an actually see-through body.
        /// </summary>
        private void ApplyTypeVisual(BlockType type, Color tint)
        {
            _auraColor = tint;

            switch (type)
            {
                case BlockType.Glass:
                    if (transparentMaterial != null)
                    {
                        SetMaterial(transparentMaterial);
                        SetTint(new Color(tint.r, tint.g, tint.b, 0.45f));
                    }

                    ShowAura(true);
                    break;

                case BlockType.Neon:
                case BlockType.Electric:
                case BlockType.Glitch:
                    ShowAura(true);
                    break;

                default:
                    ShowAura(false);
                    break;
            }

            ApplyDecal(type);
        }

        /// <summary>
        /// The symbol printed on the top face — a bolt, a shield, a burst. From this camera
        /// the top face is the biggest thing on screen, so the block says what it is before
        /// the player has to think about colour.
        /// </summary>
        private void ApplyDecal(BlockType type)
        {
            if (decal == null || decalRenderer == null || decalMaterials == null)
            {
                return;
            }

            int index = (int)type;

            if (index < 0 || index >= decalMaterials.Length || decalMaterials[index] == null)
            {
                ShowDecal(false);
                return;
            }

            decalRenderer.sharedMaterial = decalMaterials[index];
            ShowDecal(true);
            FitDecal();
        }

        private void ShowDecal(bool visible)
        {
            if (decal != null && decal.gameObject.activeSelf != visible)
            {
                decal.gameObject.SetActive(visible);
            }
        }

        /// <summary>Keeps the symbol a constant world size while the block is resized.</summary>
        private void FitDecal()
        {
            if (decal == null || !decal.gameObject.activeSelf)
            {
                return;
            }

            Vector3 scale = CachedTransform.localScale;
            float sx = Mathf.Max(0.001f, scale.x);
            float sy = Mathf.Max(0.001f, scale.y);
            float sz = Mathf.Max(0.001f, scale.z);

            float size = Mathf.Min(0.55f, Mathf.Min(sx, sz) * 0.6f);

            decal.localScale = new Vector3(size / sx, size / sz, 1f);
            decal.localPosition = new Vector3(0f, 0.5f + 0.01f / sy, 0f);
        }

        private void ShowAura(bool visible)
        {
            if (aura == null)
            {
                return;
            }

            if (aura.gameObject.activeSelf != visible)
            {
                aura.gameObject.SetActive(visible);
            }
        }

        private void TickAura(float deltaTime)
        {
            if (aura == null || !aura.gameObject.activeSelf || auraRenderer == null)
            {
                return;
            }

            _auraPhase += deltaTime;

            float scale;
            float alpha;
            Vector3 offset = Vector3.zero;

            switch (Type)
            {
                case BlockType.Neon:
                    // A hard, fast pulse — the block that blows things up looks like it.
                    // The halo stays translucent enough to read the symbol underneath.
                    scale = 1.06f + Mathf.Sin(_auraPhase * 11f) * 0.05f;
                    alpha = 0.34f + Mathf.Sin(_auraPhase * 11f) * 0.22f;
                    break;

                case BlockType.Electric:
                    // Erratic flicker, like a bad contact.
                    scale = 1.05f + Random.value * 0.04f;
                    alpha = Random.value < 0.35f ? 0.12f : 0.34f + Random.value * 0.22f;
                    break;

                case BlockType.Glitch:
                    // Displacement: the halo tears away from the body at random.
                    scale = 1.04f;
                    alpha = 0.34f + Mathf.Sin(_auraPhase * 17f) * 0.22f;
                    if (Random.value < 0.2f)
                    {
                        float jitter = (Random.value - 0.5f) * 0.35f;
                        offset = MovingAxisX ? new Vector3(jitter, 0f, 0f) : new Vector3(0f, 0f, jitter);
                    }

                    break;

                case BlockType.Glass:
                    // A slow, cold shimmer.
                    scale = 1.03f + Mathf.Sin(_auraPhase * 2.4f) * 0.015f;
                    alpha = 0.25f + Mathf.Sin(_auraPhase * 2.4f) * 0.12f;
                    break;

                default:
                    return;
            }

            aura.localScale = new Vector3(scale, scale * 1.02f, scale);
            aura.localPosition = offset;

            if (s_auraBlock == null)
            {
                s_auraBlock = new MaterialPropertyBlock();
            }

            Color c = _auraColor;
            c.a = Mathf.Clamp01(alpha);

            auraRenderer.GetPropertyBlock(s_auraBlock);
            s_auraBlock.SetColor(AuraBaseColorId, c);
            s_auraBlock.SetColor(AuraColorId, c);
            auraRenderer.SetPropertyBlock(s_auraBlock);
        }

        public void Configure(bool axisX, float pivot, float range, float direction, BlockType type, float speedMultiplier, float glitchJump)
        {
            MovingAxisX = axisX;
            _pivot = pivot;
            _range = Mathf.Max(0.01f, range);
            _direction = direction >= 0f ? 1f : -1f;
            _speedMultiplier = Mathf.Max(0.1f, speedMultiplier);
            _glitchJump = Mathf.Max(0f, glitchJump);
            _glitched = false;
            _glitchPending = false;
            Type = type;
            IsMoving = true;

            ApplyTypeVisual(type, Tint);
        }

        /// <summary>True once, on the frame the glitch block jumps — for the sound and the flash.</summary>
        public bool ConsumeGlitchPulse()
        {
            if (!_glitchPending)
            {
                return false;
            }

            _glitchPending = false;
            return true;
        }

        public void Tick(float speed, float deltaTime)
        {
            if (!IsMoving)
            {
                return;
            }

            TickAura(deltaTime);

            Transform t = CachedTransform;
            Vector3 position = t.position;
            float axis = MovingAxisX ? position.x : position.z;

            axis += _direction * speed * _speedMultiplier * deltaTime;

            // Feint: halfway in, the block skips forward and the read the player had is gone.
            if (_glitchJump > 0f && !_glitched && Mathf.Abs(axis - _pivot) <= _range * 0.5f)
            {
                axis += _direction * _glitchJump;
                _glitched = true;
                _glitchPending = true;
            }

            float min = _pivot - _range;
            float max = _pivot + _range;

            if (axis > max)
            {
                axis = Mathf.Max(min, max - (axis - max));
                _direction = -1f;
            }
            else if (axis < min)
            {
                axis = Mathf.Min(max, min + (min - axis));
                _direction = 1f;
            }

            if (MovingAxisX)
            {
                position.x = axis;
            }
            else
            {
                position.z = axis;
            }

            t.position = position;
        }

        /// <summary>Ego Boost: magnetically pull the block into perfect alignment.</summary>
        public void SnapAxis(float center)
        {
            Transform t = CachedTransform;
            Vector3 position = t.position;

            if (MovingAxisX)
            {
                position.x = center;
            }
            else
            {
                position.z = center;
            }

            t.position = position;
            IsMoving = false;
            ShowAura(false);
        }

        /// <summary>Resize to the surviving overlap. Cheaper and cleaner than runtime mesh surgery.</summary>
        public void ApplySlice(float center, float size)
        {
            Transform t = CachedTransform;
            Vector3 position = t.position;
            Vector3 scale = t.localScale;

            if (MovingAxisX)
            {
                position.x = center;
                scale.x = size;
            }
            else
            {
                position.z = center;
                scale.z = size;
            }

            t.position = position;
            t.localScale = scale;
            IsMoving = false;
            ShowAura(false);
            FitDecal();
        }

        public void Freeze()
        {
            IsMoving = false;
            ShowAura(false);
        }

        /// <summary>
        /// Squash-and-stretch on landing, optionally flashing to a colour and settling back.
        /// Ticked by the flow manager so nothing needs its own Update.
        /// </summary>
        public void PlayImpact(float strength, float speed, bool flash, Color flashColor)
        {
            if (_impact <= 0f)
            {
                _restScale = CachedTransform.localScale;
                _restTint = Tint;
            }

            _impactStrength = strength;
            _impactSpeed = Mathf.Max(0.5f, speed);
            _impactFlashes = flash;
            _flashTint = flashColor;
            _impact = 1f;
        }

        /// <summary>Returns false once the impact animation has settled.</summary>
        public bool TickImpact(float deltaTime)
        {
            if (_impact <= 0f)
            {
                return false;
            }

            _impact = Mathf.Max(0f, _impact - deltaTime * _impactSpeed);

            float progress = 1f - _impact;
            float decay = _impact;
            float wobble = Mathf.Sin(progress * Mathf.PI * 2.2f) * decay * _impactStrength;

            CachedTransform.localScale = new Vector3(
                _restScale.x * (1f + wobble * 0.45f),
                _restScale.y * (1f - wobble),
                _restScale.z * (1f + wobble * 0.45f));

            if (_impactFlashes)
            {
                SetTint(Color.Lerp(_restTint, _flashTint, decay * decay));
            }

            if (_impact <= 0f)
            {
                CachedTransform.localScale = _restScale;

                if (_impactFlashes)
                {
                    SetTint(_restTint);
                }

                return false;
            }

            return true;
        }
    }
}
