// Slice & Blast — tap resolution: Ego-Boost snap, axis-aligned slice math, Blast trigger.
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SliceBlast.Core
{
    [DisallowMultipleComponent]
    public sealed class BlockSlicer : MonoBehaviour
    {
        [SerializeField] private GameFlowManager flow;

        [Header("Ego Boost (near-miss forgiveness)")]
        [SerializeField] private float perfectThreshold = 0.05f;
        [SerializeField] private float thresholdPerStreak = 0.005f;
        [SerializeField] private float maxThreshold = 0.09f;
        [SerializeField] private float snapSpeedScale = 0.004f;

        [Header("Slice")]
        [SerializeField] private float minChunkSize = 0.02f;
        [SerializeField] private float chunkImpulse = 1.8f;
        [SerializeField] private float chunkLift = 0.6f;
        [SerializeField] private float chunkSpin = 2.4f;

        private void Reset()
        {
            flow = GetComponent<GameFlowManager>();
        }

        private void Update()
        {
            if (flow == null)
            {
                flow = GameFlowManager.Instance;
            }

            if (flow == null || !flow.AcceptsInput || !TapPressedThisFrame())
            {
                return;
            }

            ResolvePlacement();
        }

        private void ResolvePlacement()
        {
            MovingBlock moving = flow.ActiveBlock;
            MovingBlock top = flow.TopBlock;
            if (moving == null || top == null)
            {
                return;
            }

            bool axisX = moving.MovingAxisX;
            Vector3 movingPosition = moving.CachedTransform.position;
            Vector3 movingScale = moving.CachedTransform.localScale;
            Vector3 topPosition = top.CachedTransform.position;
            Vector3 topScale = top.CachedTransform.localScale;

            float movingCenter = axisX ? movingPosition.x : movingPosition.z;
            float movingSize = axisX ? movingScale.x : movingScale.z;
            float topCenter = axisX ? topPosition.x : topPosition.z;
            float topSize = axisX ? topScale.x : topScale.z;

            float delta = movingCenter - topCenter;

            // Ego Boost: the window widens invisibly with the streak and with block speed,
            // so a run that *feels* clean stays clean.
            float threshold = Mathf.Min(
                perfectThreshold + thresholdPerStreak * flow.PerfectStreak + snapSpeedScale * flow.CurrentSpeed,
                maxThreshold);

            if (Mathf.Abs(delta) <= threshold)
            {
                moving.SnapAxis(topCenter);
                flow.CommitPlacement(PlacementKind.Perfect, moving);

                if (flow.PerfectStreak >= flow.BlastStreakRequirement)
                {
                    flow.TriggerBlast();
                }

                return;
            }

            float overlapMin = Mathf.Max(topCenter - topSize * 0.5f, movingCenter - movingSize * 0.5f);
            float overlapMax = Mathf.Min(topCenter + topSize * 0.5f, movingCenter + movingSize * 0.5f);
            float overlap = overlapMax - overlapMin;

            if (overlap <= minChunkSize)
            {
                flow.CommitPlacement(PlacementKind.Missed, moving);
                return;
            }

            float newCenter = (overlapMin + overlapMax) * 0.5f;

            // Two potential offcuts: the moving block can overhang on either side once a
            // Blast has grown it past the layer beneath it.
            EmitChunk(moving, axisX, movingCenter - movingSize * 0.5f, overlapMin, -1f);
            EmitChunk(moving, axisX, overlapMax, movingCenter + movingSize * 0.5f, 1f);

            moving.ApplySlice(newCenter, overlap);
            flow.CommitPlacement(PlacementKind.Sliced, moving);
        }

        private void EmitChunk(MovingBlock moving, bool axisX, float min, float max, float outward)
        {
            float size = max - min;
            if (size <= minChunkSize)
            {
                return;
            }

            Vector3 position = moving.CachedTransform.position;
            Vector3 scale = moving.CachedTransform.localScale;
            float center = (min + max) * 0.5f;

            if (axisX)
            {
                position.x = center;
                scale.x = size;
            }
            else
            {
                position.z = center;
                scale.z = size;
            }

            Vector3 axis = axisX ? Vector3.right : Vector3.forward;
            Vector3 spinAxis = axisX ? Vector3.forward : Vector3.right;
            Vector3 impulse = axis * (outward * chunkImpulse) + Vector3.up * chunkLift;
            Vector3 torque = spinAxis * (-outward * chunkSpin);

            flow.EmitDebris(position, scale, moving.Tint, impulse, torque);
        }

        private static bool TapPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
#else
            int touchCount = Input.touchCount;
            for (int i = 0; i < touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began)
                {
                    return true;
                }
            }

            return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);
#endif
        }
    }
}
