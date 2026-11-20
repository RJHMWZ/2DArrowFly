using UnityEngine;

namespace HueRelay
{
    /// <summary>Shared notch/status-bar inset for gameplay and the main menu.</summary>
    public static class HueSafeArea
    {
#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern double HueTT_SafeAreaTopRatio();

        private static bool cached;
        private static Vector2 cachedSize;
        private static double topRatio = -1;
#endif

        // Refresh when a page opens or the app returns from the background.
        // Resolution changes also invalidate the cache; no per-frame JS calls.
        public static void Refresh()
        {
#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
            cached = false;
#endif
        }

        public static float TopPixels(float width, float height, Rect unitySafeArea)
        {
            float inset = Mathf.Max(0, height - unitySafeArea.yMax);
#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
            var size = new Vector2(width, height);
            if (!cached || cachedSize != size)
            {
                topRatio = HueTT_SafeAreaTopRatio();
                cachedSize = size;
                cached = true;
            }
            if (!double.IsNaN(topRatio) && !double.IsInfinity(topRatio) && topRatio >= 0 && topRatio <= .2)
                inset = Mathf.Max(inset, (float)topRatio * height);
#endif
            return inset;
        }
    }
}
