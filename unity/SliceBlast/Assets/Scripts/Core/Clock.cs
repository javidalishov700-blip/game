// Unscaled time, as every screen, fade and camera move reads it. Identical to Unity's own
// Time.unscaledDeltaTime / Time.unscaledTime in the shipped game.
//
// The store-capture build (SLICEBLAST_SCREENSHOTS) renders far slower than real time and locks
// game time to 30 frames per second with Time.captureDeltaTime — which Unity applies to scaled
// time only. Reading unscaled time through here keeps menus, banners and the camera on that
// same locked clock, so the recorded footage plays back at true speed.
using UnityEngine;

namespace SliceBlast
{
    public static class Clock
    {
#if SLICEBLAST_SCREENSHOTS
        public static float UnscaledDelta => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;

        public static float Unscaled => Time.captureDeltaTime > 0f ? Time.frameCount * Time.captureDeltaTime : Time.unscaledTime;
#else
        public static float UnscaledDelta => Time.unscaledDeltaTime;

        public static float Unscaled => Time.unscaledTime;
#endif
    }
}
