using SliceBlast.Core;
using UnityEngine;

namespace SliceBlast.Feedback
{
    /// <summary>
    /// A pooled burst of small cubes with ballistic motion. One pooled object per burst,
    /// so a full blast costs a handful of transform writes rather than a particle system.
    /// </summary>
    public sealed class SparkBurst : PooledObject
    {
        private static MaterialPropertyBlock s_block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private float lifetime = 0.75f;
        [SerializeField] private float gravity = -14f;
        [SerializeField] private float drag = 1.6f;

        private Transform[] _sparks;
        private Renderer[] _renderers;
        private Vector3[] _velocities;
        private Vector3[] _spins;
        private float[] _scales;
        private float _age;

        public override bool RequiresTick => true;

        protected override void CacheComponents()
        {
            int count = CachedTransform.childCount;
            _sparks = new Transform[count];
            _renderers = new Renderer[count];
            _velocities = new Vector3[count];
            _spins = new Vector3[count];
            _scales = new float[count];

            for (int i = 0; i < count; i++)
            {
                _sparks[i] = CachedTransform.GetChild(i);
                _renderers[i] = _sparks[i].GetComponent<Renderer>();
            }
        }

        /// <summary>Fires the burst. Call right after the pool spawns it.</summary>
        public void Emit(Color color, float speed, float sparkScale)
        {
            _age = 0f;

            if (s_block == null)
            {
                s_block = new MaterialPropertyBlock();
            }

            for (int i = 0; i < _sparks.Length; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.85f + 0.25f;
                direction.Normalize();

                _velocities[i] = direction * speed * Random.Range(0.55f, 1.35f);
                _spins[i] = Random.onUnitSphere * Random.Range(180f, 720f);
                _scales[i] = sparkScale * Random.Range(0.6f, 1.4f);

                Transform spark = _sparks[i];
                spark.localPosition = Random.insideUnitSphere * 0.15f;
                spark.localRotation = Random.rotation;
                spark.localScale = Vector3.one * _scales[i];

                Renderer renderer = _renderers[i];
                if (renderer != null)
                {
                    renderer.GetPropertyBlock(s_block);
                    s_block.SetColor(BaseColorId, color);
                    s_block.SetColor(ColorId, color);
                    renderer.SetPropertyBlock(s_block);
                }
            }
        }

        public override bool Tick(float deltaTime)
        {
            _age += deltaTime;
            float remaining = 1f - Mathf.Clamp01(_age / lifetime);

            if (remaining <= 0f)
            {
                return false;
            }

            float damping = 1f - Mathf.Clamp01(drag * deltaTime);

            for (int i = 0; i < _sparks.Length; i++)
            {
                Vector3 velocity = _velocities[i];
                velocity.y += gravity * deltaTime;
                velocity *= damping;
                _velocities[i] = velocity;

                Transform spark = _sparks[i];
                spark.localPosition += velocity * deltaTime;
                spark.Rotate(_spins[i] * deltaTime, Space.Self);
                spark.localScale = Vector3.one * (_scales[i] * remaining);
            }

            return true;
        }
    }
}
