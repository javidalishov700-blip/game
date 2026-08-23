// Tap resolution: Ego-Boost magnet, axis-aligned slice math, shield forgiveness and the
// special-block vanish. Decides what happened; the flow manager decides what it means.
using UnityEngine;
using UnityEngine.EventSystems;
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
        // The magnet scales with the block: a wide platform forgives more than a sliver,
        // so late-game precision still matters while early taps feel effortless.
        [SerializeField] private float perfectThreshold = 0.06f;
        [SerializeField, Range(0f, 0.3f)] private float magnetFraction = 0.11f;
        [SerializeField, Range(0f, 0.6f)] private float maxThresholdFraction = 0.45f;
        [SerializeField] private float thresholdPerStreak = 0.01f;
        [SerializeField] private float snapSpeedScale = 0.012f;

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

            // A tap on the pause button must not also drop the block.
            if (PointerOverUi())
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
            float reference = Mathf.Min(topSize, movingSize);

            // Ego Boost: the window widens invisibly with the streak and with block speed,
            // so a run that *feels* clean stays clean.
            float threshold = Mathf.Max(perfectThreshold, reference * magnetFraction)
                              + thresholdPerStreak * flow.PerfectStreak
                              + snapSpeedScale * flow.CurrentSpeed
                              + flow.TutorialAssist;

            threshold = Mathf.Min(threshold, reference * maxThresholdFraction);

            if (Mathf.Abs(delta) <= threshold)
            {
                moving.SnapAxis(topCenter);
                flow.CommitPlacement(PlacementKind.Perfect, moving);
                return;
            }

            // Missing a special never damages the tower — it vanishes and costs the combo.
            if (BlockCatalogue.IsSpecial(moving.Type))
            {
                flow.FailSpecial(moving);
                return;
            }

            // A held shield eats the mistake whole: no slice, no lost width.
            if (flow.ConsumeShield())
            {
                moving.SnapAxis(topCenter);
                flow.CommitPlacement(PlacementKind.Shielded, moving);
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
            // Steel block or a blast has grown it past the layer beneath it.
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

        public static bool PointerOverUi()
        {
            EventSystem events = EventSystem.current;

            if (events == null)
            {
                return false;
            }

#if ENABLE_INPUT_SYSTEM
            return events.IsPointerOverGameObject();
#else
            int touchCount = Input.touchCount;

            if (touchCount > 0)
            {
                for (int i = 0; i < touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);

                    if (touch.phase == TouchPhase.Began && events.IsPointerOverGameObject(touch.fingerId))
                    {
                        return true;
                    }
                }

                return false;
            }

            return events.IsPointerOverGameObject();
#endif
        }

        public static bool TapPressedThisFrame()
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
