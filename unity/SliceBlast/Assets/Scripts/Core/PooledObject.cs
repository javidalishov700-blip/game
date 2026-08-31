using UnityEngine;

namespace SliceBlast.Core
{
    [DisallowMultipleComponent]
    public abstract class PooledObject : MonoBehaviour
    {
        private static MaterialPropertyBlock s_propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Glossiness");

        private const float DefaultMetallic = 0f;
        private const float DefaultSmoothness = 0.35f;

        [SerializeField] private Renderer tintTarget;

        private Transform _cachedTransform;

        internal BlockPool Owner;

        public Color Tint { get; private set; } = Color.white;

        private Material _defaultMaterial;

        public Transform CachedTransform
        {
            get
            {
                if (ReferenceEquals(_cachedTransform, null))
                {
                    _cachedTransform = transform;
                }

                return _cachedTransform;
            }
        }

        /// <summary>Opt in to pool-driven ticking (debris self-expires; stack blocks do not).</summary>
        public virtual bool RequiresTick => false;

        internal void InitPooled(BlockPool owner)
        {
            Owner = owner;
            _cachedTransform = transform;

            if (tintTarget == null)
            {
                tintTarget = GetComponentInChildren<Renderer>(true);
            }

            // _defaultMaterial is captured lazily in ResetMaterial rather than here: a freshly
            // Instantiate()-d pooled clone is cloned from an inactive template and is itself
            // still inactive at this point, never having gone through Unity's own Awake/
            // OnEnable — reading sharedMaterial that early throws UnassignedReferenceException
            // even though tintTarget itself is a perfectly valid reference.
            CacheComponents();
        }

        /// <summary>Swaps the shared material (used for the see-through Glass block).</summary>
        public void SetMaterial(Material material)
        {
            if (tintTarget != null && material != null)
            {
                tintTarget.sharedMaterial = material;
            }
        }

        public void ResetMaterial()
        {
            if (tintTarget == null)
            {
                return;
            }

            // First real call happens from OnSpawned, after the pool has already activated
            // this instance — safe to read sharedMaterial by then.
            if (_defaultMaterial == null)
            {
                _defaultMaterial = tintTarget.sharedMaterial;
            }

            if (_defaultMaterial != null)
            {
                tintTarget.sharedMaterial = _defaultMaterial;
            }
        }

        protected virtual void CacheComponents()
        {
        }

        public virtual void OnSpawned()
        {
        }

        public virtual void OnDespawned()
        {
        }

        /// <summary>Return false to be released back into the pool.</summary>
        public virtual bool Tick(float deltaTime) => true;

        public void Release()
        {
            if (Owner != null)
            {
                Owner.Release(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        // One material for every block; colour, glow and surface all ride in the same
        // MaterialPropertyBlock so a block can look like neon, metal or glass on its own.
        public void SetTint(Color color)
        {
            Tint = color;

            // A recycled block must never inherit the last one's glow or sheen.
            Write(color, Color.black, DefaultMetallic, DefaultSmoothness);
        }

        /// <summary>Colour plus emission, leaving <see cref="Tint"/> as the base to return to.</summary>
        public void SetGlow(Color color, Color emission)
        {
            Write(color, emission, null, null);
        }

        /// <summary>Steel is metal, glass is polished: the surface is per block, not per material.</summary>
        public void SetSurface(float metallic, float smoothness)
        {
            Write(null, null, metallic, smoothness);
        }

        private void Write(Color? color, Color? emission, float? metallic, float? smoothness)
        {
            if (tintTarget == null)
            {
                tintTarget = GetComponentInChildren<Renderer>(true);

                if (tintTarget == null)
                {
                    return;
                }
            }

            if (s_propertyBlock == null)
            {
                s_propertyBlock = new MaterialPropertyBlock();
            }

            // Fetch first: every writer only touches the properties it cares about.
            tintTarget.GetPropertyBlock(s_propertyBlock);

            if (color.HasValue)
            {
                s_propertyBlock.SetColor(BaseColorId, color.Value);
                s_propertyBlock.SetColor(ColorId, color.Value);
            }

            if (emission.HasValue)
            {
                s_propertyBlock.SetColor(EmissionId, emission.Value);
            }

            if (metallic.HasValue)
            {
                s_propertyBlock.SetFloat(MetallicId, metallic.Value);
            }

            if (smoothness.HasValue)
            {
                s_propertyBlock.SetFloat(SmoothnessId, smoothness.Value);
            }

            tintTarget.SetPropertyBlock(s_propertyBlock);
        }
    }
}
