using UnityEngine;

namespace SliceBlast.Feedback
{
    /// <summary>Height follow + trauma-based shake. Perlin noise, no coroutines, no allocations.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float heightOffset = 2.2f;
        [SerializeField] private float followSmoothing = 4f;

        [Header("Shake")]
        [SerializeField] private float maxOffset = 0.35f;
        [SerializeField] private float maxRoll = 4f;
        [SerializeField] private float frequency = 22f;
        [SerializeField] private float decay = 1.6f;

        private Transform _rig;
        private Vector3 _basePosition;
        private Vector3 _restLocalPosition;
        private Quaternion _restLocalRotation;
        private bool _rigIsCamera;
        private bool _shakeApplied;

        private float _targetHeight;
        private float _trauma;
        private float _seed;

        private void Awake()
        {
            _rig = transform;
            _basePosition = _rig.position;
            _targetHeight = _basePosition.y;
            _seed = Random.value * 100f;

            if (cameraTransform == null)
            {
                Camera main = Camera.main;
                cameraTransform = main != null ? main.transform : _rig;
            }

            _rigIsCamera = cameraTransform == _rig;
            _restLocalPosition = cameraTransform.localPosition;
            _restLocalRotation = cameraTransform.localRotation;
        }

        public void SetTargetHeight(float worldY)
        {
            _targetHeight = worldY + heightOffset;
        }

        public void SnapToHeight(float worldY)
        {
            SetTargetHeight(worldY);
            _basePosition.y = _targetHeight;
            _rig.position = _basePosition;
        }

        public void Shake(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;

            _basePosition.y = Mathf.LerpUnclamped(_basePosition.y, _targetHeight, 1f - Mathf.Exp(-followSmoothing * dt));
            _rig.position = _basePosition;

            if (_trauma <= 0f)
            {
                ResetShake();
                return;
            }

            _trauma = Mathf.Max(0f, _trauma - decay * dt);

            if (_trauma <= 0f)
            {
                ResetShake();
                return;
            }

            float shake = _trauma * _trauma;
            float t = (Time.time + _seed) * frequency;

            float x = (Mathf.PerlinNoise(t, 0f) * 2f - 1f) * maxOffset * shake;
            float y = (Mathf.PerlinNoise(0f, t) * 2f - 1f) * maxOffset * shake;
            float roll = (Mathf.PerlinNoise(t, t) * 2f - 1f) * maxRoll * shake;

            _shakeApplied = true;

            if (_rigIsCamera)
            {
                _rig.position = _basePosition + new Vector3(x, y, 0f);
                _rig.localRotation = _restLocalRotation * Quaternion.Euler(0f, 0f, roll);
                return;
            }

            cameraTransform.localPosition = _restLocalPosition + new Vector3(x, y, 0f);
            cameraTransform.localRotation = _restLocalRotation * Quaternion.Euler(0f, 0f, roll);
        }

        private void ResetShake()
        {
            if (!_shakeApplied)
            {
                return;
            }

            _shakeApplied = false;

            if (_rigIsCamera)
            {
                _rig.position = _basePosition;
                _rig.localRotation = _restLocalRotation;
                return;
            }

            cameraTransform.localPosition = _restLocalPosition;
            cameraTransform.localRotation = _restLocalRotation;
        }
    }
}
