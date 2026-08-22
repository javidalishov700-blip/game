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
    }
}
