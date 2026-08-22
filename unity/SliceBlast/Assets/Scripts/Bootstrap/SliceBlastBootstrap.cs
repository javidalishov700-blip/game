// Zero-setup entry point: drop this on one empty GameObject in an empty scene and press Play.
// It builds the camera rig, light, pooled prefabs and the game flow at runtime.
using SliceBlast.Core;
using SliceBlast.Feedback;
using UnityEngine;
using UnityEngine.Rendering;

namespace SliceBlast.Bootstrap
{
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class SliceBlastBootstrap : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Vector3 cameraAngles = new Vector3(26f, 45f, 0f);
        [SerializeField] private float cameraDistance = 18f;
        [SerializeField] private float orthographicSize = 5.5f;
        [SerializeField] private Color background = new Color(0.07f, 0.08f, 0.12f, 1f);

        [Header("Pools")]
        [SerializeField] private int blockPrewarm = 48;
        [SerializeField] private int blockCap = 200;
        [SerializeField] private int debrisPrewarm = 32;
        [SerializeField] private int debrisCap = 120;

        [Header("HUD")]
        [SerializeField] private bool showHud = true;
        [SerializeField] private float restartDelay = 0.6f;

        private GameFlowManager _flow;
        private GUIStyle _scoreStyle;
        private GUIStyle _hintStyle;
        private int _score;
        private int _streak;
        private bool _gameOver;
        private float _gameOverTime;

        private void Awake()
        {
            Material shared = CreateMaterial();
            MovingBlock blockTemplate = CreateBlockTemplate(shared);
            DebrisChunk debrisTemplate = CreateDebrisTemplate(shared);

            BlockPool blockPool = CreatePool("BlockPool", blockTemplate, blockPrewarm, blockCap);
            BlockPool debrisPool = CreatePool("DebrisPool", debrisTemplate, debrisPrewarm, debrisCap);

            CameraRig rig = CreateCameraRig();
            CreateLighting();

            GameObject flowObject = new GameObject("GameFlow");
            flowObject.transform.SetParent(transform, false);
            flowObject.SetActive(false);

            _flow = flowObject.AddComponent<GameFlowManager>();
            flowObject.AddComponent<BlockSlicer>();
            _flow.Configure(blockPool, debrisPool, rig);

            _flow.ScoreChanged += OnScoreChanged;
            _flow.PerfectSnapped += OnPerfectSnapped;
            _flow.BlockSliced += OnBlockSliced;
            _flow.BlastFired += OnBlastFired;
            _flow.GameEnded += OnGameEnded;

            flowObject.SetActive(true);
        }

        private void OnDestroy()
        {
            if (_flow == null)
            {
                return;
            }

            _flow.ScoreChanged -= OnScoreChanged;
            _flow.PerfectSnapped -= OnPerfectSnapped;
            _flow.BlockSliced -= OnBlockSliced;
            _flow.BlastFired -= OnBlastFired;
            _flow.GameEnded -= OnGameEnded;
        }

        private void Update()
        {
            if (!_gameOver || Time.time - _gameOverTime < restartDelay)
            {
                return;
            }

            if (!BlockSlicer.TapPressedThisFrame())
            {
                return;
            }

            _gameOver = false;
            _streak = 0;
            _flow.Restart();
        }

        private void OnScoreChanged(int score) => _score = score;

        private void OnPerfectSnapped(int streak, Vector3 position) => _streak = streak;

        private void OnBlockSliced(Vector3 position) => _streak = 0;

        private void OnBlastFired(int layers, Vector3 position) => _streak = 0;

        private void OnGameEnded(int finalScore)
        {
            _gameOver = true;
            _gameOverTime = Time.time;
        }

        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Diffuse");
            }

            Material material = new Material(shader) { enableInstancing = true };
            return material;
        }

        private MovingBlock CreateBlockTemplate(Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BlockTemplate";

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;

            MovingBlock block = go.AddComponent<MovingBlock>();
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            return block;
        }

        private DebrisChunk CreateDebrisTemplate(Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "DebrisTemplate";
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;

            DebrisChunk chunk = go.AddComponent<DebrisChunk>();
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            return chunk;
        }

        private BlockPool CreatePool(string poolName, PooledObject template, int prewarm, int cap)
        {
            GameObject go = new GameObject(poolName);
            go.transform.SetParent(transform, false);
            go.SetActive(false);

            BlockPool pool = go.AddComponent<BlockPool>();
            pool.Configure(template, prewarm, cap);

            go.SetActive(true);
            return pool;
        }

        private CameraRig CreateCameraRig()
        {
            GameObject rigObject = new GameObject("CameraRig");
            rigObject.transform.position = Vector3.zero;

            Quaternion rotation = Quaternion.Euler(cameraAngles);

            GameObject cameraObject = new GameObject("MainCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(rigObject.transform, false);
            cameraObject.transform.localRotation = rotation;
            cameraObject.transform.localPosition = -(rotation * Vector3.forward) * cameraDistance;

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 80f;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            cameraObject.AddComponent<AudioListener>();

            return rigObject.AddComponent<CameraRig>();
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new GameObject("KeyLight");
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(1f, 0.97f, 0.9f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.45f;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.42f);
        }

        private void OnGUI()
        {
            if (!showHud)
            {
                return;
            }

            if (_scoreStyle == null)
            {
                _scoreStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperCenter,
                    fontStyle = FontStyle.Bold
                };

                _hintStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter
                };
            }

            _scoreStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.07f);
            _hintStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.032f);

            float width = Screen.width;
            GUI.Label(new Rect(0f, Screen.height * 0.06f, width, Screen.height * 0.12f), _score.ToString(), _scoreStyle);

            if (_streak > 0)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.18f, width, Screen.height * 0.06f), "PERFECT x" + _streak, _hintStyle);
            }

            if (_gameOver)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.45f, width, Screen.height * 0.1f), "TAP TO RESTART", _hintStyle);
            }
        }
    }
}
