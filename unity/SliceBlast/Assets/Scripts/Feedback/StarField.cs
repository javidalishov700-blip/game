// A fixed, camera-parented field of small glowing points — built to suit a camera that
// barely moves vertically, so a classic scrolling parallax layer would just look static.
// One persistent instance, ticked explicitly from the bootstrap's own Update (no per-star
// Update() calls, no pooling, no post-processing bloom to lean on).
using UnityEngine;
using UnityEngine.Rendering;

namespace SliceBlast.Feedback
{
    public sealed class StarField : MonoBehaviour
    {
        private static MaterialPropertyBlock s_block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // One slow radial drift does double duty: it is both the "gliding in place" idle
        // motion and the optional depth cue (bigger radius reads as further along, so scale
        // grows with it) — no need for a second, separate motion system.
        private const float MinRadius = 0.4f;
        private const float MaxRadius = 7.5f;
        private const float BurstDecayPerSecond = 2.2f;
        private const float BurstScatterKick = 3.2f;
        private const float BurstGlowBoost = 0.85f;

        private Transform[] _stars;
        private Renderer[] _renderers;
        private Vector2[] _direction;
        private float[] _radius;
        private float[] _radiusSpeed;
        private float[] _twinklePhase;
        private float[] _twinkleSpeed;
        private float[] _baseAlpha;
        private float[] _baseScale;
        private Color _tint = Color.white;
        private float _burst;

        /// <summary>Builds the field once, parented to the camera so it always fills the view.</summary>
        public void Build(Transform cameraTransform, Material glowMaterial, int count, Color tint)
        {
            _tint = tint;

            transform.SetParent(cameraTransform, false);
            transform.localPosition = new Vector3(0f, 0f, 52f);
            transform.localRotation = Quaternion.identity;

            _stars = new Transform[count];
            _renderers = new Renderer[count];
            _direction = new Vector2[count];
            _radius = new float[count];
            _radiusSpeed = new float[count];
            _twinklePhase = new float[count];
            _twinkleSpeed = new float[count];
            _baseAlpha = new float[count];
            _baseScale = new float[count];

            for (int i = 0; i < count; i++)
            {
                GameObject star = GameObject.CreatePrimitive(PrimitiveType.Quad);
                star.name = "Star";

                Collider collider = star.GetComponent<Collider>();

                if (collider != null)
                {
                    Destroy(collider);
                }

                MeshRenderer renderer = star.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = glowMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Transform t = star.transform;
                t.SetParent(transform, false);
                t.localRotation = Quaternion.identity;

                _stars[i] = t;
                _renderers[i] = renderer;
                RollStar(i, Random.value);
            }
        }

        /// <summary>Sends a star back to the centre with a fresh direction and drift speed.</summary>
        private void RollStar(int index, float startFraction)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);

            // Stretched vertically to match the portrait frame rather than a perfect circle.
            _direction[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 1.35f);
            _radius[index] = Mathf.Lerp(MinRadius, MaxRadius, startFraction);
            _radiusSpeed[index] = Random.Range(0.12f, 0.28f);
            _twinklePhase[index] = Random.Range(0f, Mathf.PI * 2f);
            _twinkleSpeed[index] = Random.Range(1.1f, 2.6f);
            _baseAlpha[index] = Random.Range(0.35f, 0.9f);
            _baseScale[index] = Random.Range(0.05f, 0.12f);
        }

        /// <summary>A momentary scatter + glow kick — call on a blast or a big combo.</summary>
        public void Pulse(float strength)
        {
            _burst = Mathf.Max(_burst, strength);
        }

        public void Tick(float dt)
        {
            if (_stars == null)
            {
                return;
            }

            _burst = Mathf.MoveTowards(_burst, 0f, BurstDecayPerSecond * dt);

            if (s_block == null)
            {
                s_block = new MaterialPropertyBlock();
            }

            float span = MaxRadius - MinRadius;

            for (int i = 0; i < _stars.Length; i++)
            {
                _radius[i] += (_radiusSpeed[i] + _burst * BurstScatterKick) * dt;

                if (_radius[i] > MaxRadius)
                {
                    RollStar(i, 0f);
                }

                float depth = Mathf.Clamp01((_radius[i] - MinRadius) / span);

                _twinklePhase[i] += _twinkleSpeed[i] * dt;
                float twinkle = 0.5f + 0.5f * Mathf.Sin(_twinklePhase[i]);
                float alpha = Mathf.Clamp01(_baseAlpha[i] * Mathf.Lerp(0.4f, 1f, twinkle) + _burst * BurstGlowBoost);
                float scale = _baseScale[i] * Mathf.Lerp(0.7f, 1.6f, depth);

                Transform t = _stars[i];
                t.localPosition = new Vector3(_direction[i].x * _radius[i], _direction[i].y * _radius[i], 0f);
                t.localScale = new Vector3(scale, scale, 1f);

                Renderer renderer = _renderers[i];
                renderer.GetPropertyBlock(s_block);
                Color c = _tint * alpha;
                c.a = alpha;
                s_block.SetColor(BaseColorId, c);
                s_block.SetColor(ColorId, c);
                renderer.SetPropertyBlock(s_block);
            }
        }
    }
}
