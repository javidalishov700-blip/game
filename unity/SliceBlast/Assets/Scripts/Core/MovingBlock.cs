using UnityEngine;

namespace SliceBlast.Core
{
    /// <summary>
    /// A tower layer. Ping-pongs along one axis until it is snapped, sliced or lost, and
    /// carries whatever look its type demands: a neon glow, live arcs, glass, or metal.
    /// </summary>
    public sealed class MovingBlock : PooledObject
    {
        private static readonly Color ArcWhite = new Color(0.78f, 0.95f, 1f);

        public bool MovingAxisX { get; private set; }
        public bool IsMoving { get; private set; }
        public BlockType Type { get; private set; }

        private float _pivot;
        private float _range;
        private float _direction = 1f;
        private float _speedMultiplier = 1f;

        [SerializeField] private Transform aura;
        [SerializeField] private Renderer auraRenderer;
        [SerializeField] private Material glassMaterial;
        [SerializeField] private Transform decal;
        [SerializeField] private Renderer decalRenderer;
        [SerializeField] private Material[] decalMaterials;
        [SerializeField] private Transform arcs;
        [SerializeField] private Transform[] arcNodes;

        private static MaterialPropertyBlock s_effectBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

        private Color _auraColor = Color.white;
        private float _auraPhase;
        private float _arcTimer;
        private float _charge;

        // The colour the body actually wears, alpha included — glass is see-through, so
        // every effect has to return to this rather than to the raw tint.
        private Color _body = Color.white;

        private Vector3 _restScale;
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
            _impact = 0f;
            _auraPhase = 0f;
            _arcTimer = 0f;
            _charge = 0f;
            _body = Color.white;
            Type = BlockType.Standard;

            ResetMaterial();
            ShowAura(false);
            ShowDecal(false);
            ShowArcs(false);
        }

        /// <summary>
        /// Every pooled clone re-resolves its own children. Instantiate does remap internal
        /// references, but the whole tower animating one template's halo is not a failure
        /// worth risking on a device.
        /// </summary>
        protected override void CacheComponents()
        {
            Transform t = CachedTransform;

            aura = t.Find("Aura");
            auraRenderer = aura != null ? aura.GetComponent<Renderer>() : null;

            decal = t.Find("Symbol");
            decalRenderer = decal != null ? decal.GetComponent<Renderer>() : null;

            arcs = t.Find("Arcs");

            if (arcs != null)
            {
                arcNodes = new Transform[arcs.childCount];

                for (int i = 0; i < arcNodes.Length; i++)
                {
                    arcNodes[i] = arcs.GetChild(i);
                }
            }
        }

        /// <summary>Called once on the pooled template so every clone carries the same assets.</summary>
        public void SetupVisuals(
            Transform auraTransform,
            Renderer auraRend,
            Material glass,
            Transform decalTransform,
            Renderer decalRend,
            Material[] symbols,
            Transform arcRoot,
            Transform[] arcs6)
        {
            aura = auraTransform;
            auraRenderer = auraRend;
            glassMaterial = glass;
            decal = decalTransform;
            decalRenderer = decalRend;
            decalMaterials = symbols;
            arcs = arcRoot;
            arcNodes = arcs6;
        }

        public void Configure(bool axisX, float pivot, float range, float direction, BlockType type, float speedMultiplier)
        {
            MovingAxisX = axisX;
            _pivot = pivot;
            _range = Mathf.Max(0.01f, range);
            _direction = direction >= 0f ? 1f : -1f;
            _speedMultiplier = Mathf.Max(0.1f, speedMultiplier);
            Type = type;
            IsMoving = true;

            ApplyTypeVisual(type, Tint);
        }

        /// <summary>
        /// Each type has to be readable at a glance, without a label: a neon body that glows,
        /// a light-blue block spitting arcs, real glass, real metal.
        /// </summary>
        private void ApplyTypeVisual(BlockType type, Color tint)
        {
            _auraColor = tint;
            _body = tint;
            _charge = 0f;

            switch (type)
            {
                case BlockType.Neon:
                    SetGlow(tint, tint * 0.8f);
                    ShowAura(true);
                    break;

                case BlockType.Electric:
                    SetGlow(tint, ArcWhite * 0.6f);
                    _auraColor = ArcWhite;
                    ShowAura(true);
                    ShowArcs(true);
                    break;

                case BlockType.Glass:
                    if (glassMaterial != null)
                    {
                        SetMaterial(glassMaterial);
                    }

                    _body = new Color(tint.r, tint.g, tint.b, 0.45f);
                    SetGlow(_body, Color.black);
                    SetSurface(0f, 0.95f);
                    ShowAura(true);
                    break;

                case BlockType.Steel:
                    // Metal does not glow; it catches the key light instead.
                    SetSurface(0.9f, 0.7f);
                    ShowAura(false);
                    break;

                default:
                    ShowAura(false);
                    break;
            }

            ApplyDecal(type, tint);
        }

        /// <summary>
        /// The symbol printed on the top face — a burst, a lightning pattern, a shield. From
        /// this camera the top face is what the player reads, so it says what the block is.
        /// </summary>
        private void ApplyDecal(BlockType type, Color tint)
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

            // The symbol renderer is reused across types, so its colour always comes from the
            // property block — a neon block's symbol has to match whatever colour it rolled.
            WriteEffectColor(decalRenderer, SymbolColor(type, tint));

            ShowDecal(true);
            FitDecal();
        }

        private static Color SymbolColor(BlockType type, Color tint)
        {
            switch (type)
            {
                case BlockType.Neon:
                    return tint;

                case BlockType.Electric:
                    return ArcWhite;

                case BlockType.Glass:
                    return new Color(0.06f, 0.36f, 0.55f);

                case BlockType.Steel:
                    return new Color(0.16f, 0.18f, 0.24f);

                default:
                    return Color.white;
            }
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
            if (aura != null && aura.gameObject.activeSelf != visible)
            {
                aura.gameObject.SetActive(visible);
            }
        }

        private void ShowArcs(bool visible)
        {
            if (arcs != null && arcs.gameObject.activeSelf != visible)
            {
                arcs.gameObject.SetActive(visible);
            }
        }

        private void TickEffects(float deltaTime)
        {
            _auraPhase += deltaTime;

            switch (Type)
            {
                case BlockType.Neon:
                {
                    // A hard, fast pulse — the block that blows things up looks like it.
                    float wave = Mathf.Sin(_auraPhase * 9f);
                    SetGlow(_body, _body * (0.6f + wave * 0.4f));
                    TickAura(1.06f + wave * 0.05f, 0.34f + wave * 0.22f);
                    break;
                }

                case BlockType.Electric:
                {
                    // Erratic, like a bad contact.
                    float flicker = Random.value < 0.3f ? 0.25f : 0.7f + Random.value * 0.5f;
                    SetGlow(_body, ArcWhite * flicker);
                    TickAura(1.05f + Random.value * 0.04f, 0.18f + flicker * 0.22f);
                    TickArcs(deltaTime);
                    break;
                }

                case BlockType.Glass:
                {
                    // A slow, cold shimmer.
                    float wave = Mathf.Sin(_auraPhase * 2.4f);
                    TickAura(1.03f + wave * 0.015f, 0.22f + wave * 0.12f);
                    break;
                }
            }
        }

        private void TickAura(float scale, float alpha)
        {
            if (aura == null || auraRenderer == null || !aura.gameObject.activeSelf)
            {
                return;
            }

            aura.localScale = new Vector3(scale, scale * 1.02f, scale);

            // Additive: brightness is the alpha here, so it scales the colour itself.
            float strength = Mathf.Clamp01(alpha);
            Color c = _auraColor * strength;
            c.a = strength;

            WriteEffectColor(auraRenderer, c);
        }

        /// <summary>
        /// Live arcs crawling over the shell. Axis-aligned on purpose: a rotated child inside
        /// a non-uniformly scaled block would shear.
        /// </summary>
        private void TickArcs(float deltaTime)
        {
            if (arcs == null || arcNodes == null || !arcs.gameObject.activeSelf)
            {
                return;
            }

            _arcTimer -= deltaTime;

            if (_arcTimer > 0f)
            {
                return;
            }

            _arcTimer = 0.04f;

            Vector3 scale = CachedTransform.localScale;
            float sx = Mathf.Max(0.001f, scale.x);
            float sy = Mathf.Max(0.001f, scale.y);
            float sz = Mathf.Max(0.001f, scale.z);

            for (int i = 0; i < arcNodes.Length; i++)
            {
                Transform node = arcNodes[i];

                if (node == null)
                {
                    continue;
                }

                if (Random.value < 0.25f)
                {
                    node.localScale = Vector3.zero; // gaps in the crackle
                    continue;
                }

                bool alongX = Random.value < 0.5f;
                float length = Random.Range(0.18f, 0.45f);
                const float thickness = 0.045f;

                node.localScale = alongX
                    ? new Vector3(length / sx, thickness / sy, thickness / sz)
                    : new Vector3(thickness / sx, thickness / sy, length / sz);

                float along = Random.Range(-0.45f, 0.45f);
                float height = Random.Range(-0.45f, 0.55f);
                float face = Random.value < 0.5f ? -0.53f : 0.53f;

                node.localPosition = alongX
                    ? new Vector3(along, height, face)
                    : new Vector3(face, height, along);
            }
        }

        /// <summary>
        /// The 15-second current running up the tower: 0 restores the block, 1 is the head of
        /// the pulse. Cheap on purpose — nothing is written when the value has not moved.
        /// </summary>
        public void SetCharge(float amount)
        {
            amount = Mathf.Clamp01(amount);

            if (Mathf.Abs(amount - _charge) < 0.01f)
            {
                return;
            }

            _charge = amount;

            if (amount <= 0f)
            {
                SetGlow(_body, Type == BlockType.Neon ? _body * 0.8f : Color.black);
                return;
            }

            Color lit = Color.Lerp(_body, BlockCatalogue.ElectricBlue, amount * 0.7f);
            lit.a = _body.a;

            SetGlow(lit, BlockCatalogue.ElectricBlue * (amount * 1.5f));
        }

        private static void WriteEffectColor(Renderer target, Color color)
        {
            if (target == null)
            {
                return;
            }

            if (s_effectBlock == null)
            {
                s_effectBlock = new MaterialPropertyBlock();
            }

            target.GetPropertyBlock(s_effectBlock);
            s_effectBlock.SetColor(BaseColorId, color);
            s_effectBlock.SetColor(ColorId, color);
            s_effectBlock.SetColor(TintColorId, color); // legacy particle shaders
            target.SetPropertyBlock(s_effectBlock);
        }

        public void Tick(float speed, float deltaTime)
        {
            if (!IsMoving)
            {
                return;
            }

            TickEffects(deltaTime);

            Transform t = CachedTransform;
            Vector3 position = t.position;
            float axis = MovingAxisX ? position.x : position.z;

            axis += _direction * speed * _speedMultiplier * deltaTime;

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
            Settle();
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
            Settle();
            FitDecal();
        }

        public void Freeze()
        {
            Settle();
        }

        /// <summary>Landed: the travelling effects stop, the identity on the face stays.</summary>
        private void Settle()
        {
            IsMoving = false;
            ShowAura(false);
            ShowArcs(false);

            if (Type == BlockType.Electric)
            {
                SetGlow(_body, Color.black);
            }
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

            if (_impactFlashes && _charge <= 0f)
            {
                Color lit = Color.Lerp(_body, _flashTint, decay * decay);
                lit.a = _body.a;
                SetGlow(lit, _flashTint * (decay * decay));
            }

            if (_impact <= 0f)
            {
                CachedTransform.localScale = _restScale;

                if (_impactFlashes && _charge <= 0f)
                {
                    SetGlow(_body, Type == BlockType.Neon ? _body * 0.8f : Color.black);
                }

                return false;
            }

            return true;
        }
    }
}
