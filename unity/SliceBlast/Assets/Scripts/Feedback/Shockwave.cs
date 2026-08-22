using SliceBlast.Core;
using UnityEngine;

namespace SliceBlast.Feedback
{
    /// <summary>Expanding ring that fades out — the visual punctuation of a blast.</summary>
    public sealed class Shockwave : PooledObject
    {
        private static MaterialPropertyBlock s_block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private float duration = 0.55f;
        [SerializeField] private float startScale = 0.4f;
        [SerializeField] private float endScale = 7f;

        private Renderer _renderer;
        private Color _color = Color.white;
        private float _age;

        public override bool RequiresTick => true;

        protected override void CacheComponents()
        {
            _renderer = GetComponentInChildren<Renderer>(true);
        }

        public void Play(Color color)
        {
            _color = color;
            _age = 0f;
            CachedTransform.localScale = Vector3.one * startScale;
            Apply(1f);
        }

        public override bool Tick(float deltaTime)
        {
            _age += deltaTime;
            float p = Mathf.Clamp01(_age / duration);

            if (p >= 1f)
            {
                return false;
            }

            // Fast out, slow settle: the ring reads as a pressure wave rather than a balloon.
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            CachedTransform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, eased);
            Apply(1f - p);
            return true;
        }

        private void Apply(float alpha)
        {
            if (_renderer == null)
            {
                return;
            }

            if (s_block == null)
            {
                s_block = new MaterialPropertyBlock();
            }

            Color c = _color;
            c.a = alpha * alpha;

            _renderer.GetPropertyBlock(s_block);
            s_block.SetColor(BaseColorId, c);
            s_block.SetColor(ColorId, c);
            _renderer.SetPropertyBlock(s_block);
        }
    }
}
