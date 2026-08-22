using UnityEngine;

namespace SliceBlast.Feedback
{
    /// <summary>Cached, throttled device haptics. Android uses VibrationEffect on API 26+.</summary>
    public static class Haptics
    {
        public static bool Enabled = true;

        private const float MinInterval = 0.04f;
        private static float s_lastFireTime = -1f;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject s_vibrator;
        private static AndroidJavaClass s_effectClass;
        private static int s_apiLevel;
        private static bool s_initialized;
#endif

        public static void Light() => Fire(12, 40);

        public static void Medium() => Fire(22, 90);

        public static void Heavy() => Fire(45, 160);

        private static void Fire(long milliseconds, int amplitude)
        {
            if (!Enabled || Time.unscaledTime - s_lastFireTime < MinInterval)
            {
                return;
            }

            s_lastFireTime = Time.unscaledTime;

#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureAndroid();

            if (s_vibrator == null)
            {
                return;
            }

            if (s_apiLevel >= 26 && s_effectClass != null)
            {
                using (AndroidJavaObject effect = s_effectClass.CallStatic<AndroidJavaObject>(
                    "createOneShot", milliseconds, Mathf.Clamp(amplitude, 1, 255)))
                {
                    s_vibrator.Call("vibrate", effect);
                }
            }
            else
            {
                s_vibrator.Call("vibrate", milliseconds);
            }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void EnsureAndroid()
        {
            if (s_initialized)
            {
                return;
            }

            s_initialized = true;

            try
            {
                using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    s_apiLevel = version.GetStatic<int>("SDK_INT");
                }

                using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    s_vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                if (s_apiLevel >= 26)
                {
                    s_effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                }
            }
            catch (System.Exception)
            {
                s_vibrator = null;
                s_effectClass = null;
            }
        }
#endif
    }
}
