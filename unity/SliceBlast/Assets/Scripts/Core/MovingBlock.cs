using UnityEngine;

namespace SliceBlast.Core
{
    /// <summary>A tower layer. Ping-pongs along one axis until it is snapped, sliced or missed.</summary>
    public sealed class MovingBlock : PooledObject
    {
        public bool MovingAxisX { get; private set; }
        public bool IsMoving { get; private set; }

        private float _pivot;
        private float _range;
        private float _direction = 1f;

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
        }

        public void Configure(bool axisX, float pivot, float range, float direction)
        {
            MovingAxisX = axisX;
            _pivot = pivot;
            _range = Mathf.Max(0.01f, range);
            _direction = direction >= 0f ? 1f : -1f;
            IsMoving = true;
        }

        public void Tick(float speed, float deltaTime)
        {
            if (!IsMoving)
            {
                return;
            }

            Transform t = CachedTransform;
            Vector3 position = t.position;
            float axis = MovingAxisX ? position.x : position.z;

            axis += _direction * speed * deltaTime;

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
        }

        public void Freeze()
        {
            IsMoving = false;
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
