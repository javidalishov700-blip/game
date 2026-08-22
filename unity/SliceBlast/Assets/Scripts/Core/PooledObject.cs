using UnityEngine;

namespace SliceBlast.Core
{
    [DisallowMultipleComponent]
    public abstract class PooledObject : MonoBehaviour
    {
        private static MaterialPropertyBlock s_propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Renderer tintTarget;

        private Transform _cachedTransform;

        internal BlockPool Owner;

        public Color Tint { get; private set; } = Color.white;

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

            if (ReferenceEquals(tintTarget, null))
            {
                tintTarget = GetComponentInChildren<Renderer>(true);
            }

            CacheComponents();
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

        // MaterialPropertyBlock keeps every block on one material, so the whole tower
        // stays in a single GPU-instanced draw call.
        public void SetTint(Color color)
        {
            Tint = color;

            if (ReferenceEquals(tintTarget, null))
            {
                return;
            }

            if (s_propertyBlock == null)
            {
                s_propertyBlock = new MaterialPropertyBlock();
            }

            tintTarget.GetPropertyBlock(s_propertyBlock);
            s_propertyBlock.SetColor(BaseColorId, color);
            s_propertyBlock.SetColor(ColorId, color);
            tintTarget.SetPropertyBlock(s_propertyBlock);
        }
    }
}
