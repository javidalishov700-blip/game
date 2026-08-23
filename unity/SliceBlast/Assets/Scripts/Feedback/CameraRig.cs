using UnityEngine;

namespace SliceBlast.Feedback
{
    /// <summary>
    /// Height follow, orthographic framing and trauma-based shake. Perlin noise, no
    /// coroutines, no allocations. Everything eases on unscaled time so the death
    /// slow-motion never stalls the camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Camera view;
        [SerializeField] private float heightOffset = 2.2f;
        [SerializeField] private float followSmoothing = 4f;
        [SerializeField] private float zoomSmoothing = 3f;
        [SerializeField] private float minOrthographicSize = 6f;

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

        private float _playSize = 5.5f;
        private float _targetSize = 5.5f;

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

            if (view == null)
            {
                view = cameraTransform.GetComponent<Camera>();
            }

            if (view != null)
            {
                _playSize = view.orthographicSize;
                _targetSize = _playSize;
            }
        }

        /// <summary>
        /// Fit the play framing to the device: <paramref name="requiredHalfWidth"/> is how
        /// much world the widest moment of a swing needs across the screen. A tall phone
        /// gets a wider orthographic size for the same world width, which is exactly why
        /// the block used to leave the frame on 19.5:9.
        /// </summary>
        public void FitPlaySize(float requiredHalfWidth, bool immediate)
        {
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 0.5625f;

            _playSize = Mathf.Max(minOrthographicSize, requiredHalfWidth / Mathf.Max(0.2f, aspect));
            _targetSize = _playSize;

            if (immediate && view != null)
            {
                view.orthographicSize = _playSize;
            }
        }

        /// <summary>The title screen sits a touch wider than play, so the run eases in.</summary>
        public void ZoomTo(float sizeScale, bool immediate)
        {
            _targetSize = _playSize * Mathf.Max(0.5f, sizeScale);

            if (immediate && view != null)
            {
                view.orthographicSize = _targetSize;
            }
        }

        /// <summary>
        /// End of run: pull back until the whole tower is in shot. 0.9 is cos(26°), the
        /// camera pitch; the constant covers the isometric footprint of the base.
        /// </summary>
        public void FrameTower(float bottomY, float topY)
        {
            float height = Mathf.Max(0f, topY - bottomY);

            _targetHeight = bottomY + height * 0.5f;
            _targetSize = Mathf.Max(_playSize, height * 0.5f * 0.9f + 1.2f);
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
            float dt = Time.unscaledDeltaTime;

            _basePosition.y = Mathf.LerpUnclamped(_basePosition.y, _targetHeight, 1f - Mathf.Exp(-followSmoothing * dt));
            _rig.position = _basePosition;

            if (view != null && !Mathf.Approximately(view.orthographicSize, _targetSize))
            {
                view.orthographicSize = Mathf.LerpUnclamped(view.orthographicSize, _targetSize, 1f - Mathf.Exp(-zoomSmoothing * dt));
            }

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
            float t = (Time.unscaledTime + _seed) * frequency;

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
