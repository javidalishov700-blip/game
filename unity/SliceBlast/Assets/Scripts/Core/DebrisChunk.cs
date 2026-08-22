using UnityEngine;

namespace SliceBlast.Core
{
    /// <summary>Pooled offcut. Falls with real physics, then shrinks out and returns itself.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DebrisChunk : PooledObject
    {
        [SerializeField] private float lifetime = 2.2f;
        [SerializeField] private float sinkDuration = 0.45f;

        private Rigidbody _body;
        private Vector3 _spawnScale;
        private float _age;

        public override bool RequiresTick => true;

        protected override void CacheComponents()
        {
            _body = GetComponent<Rigidbody>();
        }

        public override void OnSpawned()
        {
            _age = 0f;
            _spawnScale = CachedTransform.localScale;

            if (_body != null)
            {
                _body.isKinematic = false;
                ClearVelocity();
            }
        }

        public override void OnDespawned()
        {
            if (_body != null)
            {
                ClearVelocity();
                _body.isKinematic = true;
            }

            CachedTransform.localScale = _spawnScale;
        }

        public void Launch(Vector3 impulse, Vector3 torque)
        {
            if (_body == null)
            {
                return;
            }

            _body.AddForce(impulse, ForceMode.VelocityChange);
            _body.AddTorque(torque, ForceMode.VelocityChange);
        }

        public override bool Tick(float deltaTime)
        {
            _age += deltaTime;

            if (_age < lifetime)
            {
                return true;
            }

            float k = (_age - lifetime) / Mathf.Max(0.01f, sinkDuration);
            if (k >= 1f)
            {
                return false;
            }

            CachedTransform.localScale = _spawnScale * (1f - k);
            return true;
        }

        private void ClearVelocity()
        {
#if UNITY_6000_0_OR_NEWER
            _body.linearVelocity = Vector3.zero;
#else
            _body.velocity = Vector3.zero;
#endif
            _body.angularVelocity = Vector3.zero;
        }
    }
}
