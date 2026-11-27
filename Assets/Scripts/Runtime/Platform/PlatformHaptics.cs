using UnityEngine;

namespace XanaduYonder.Runtime
{
    internal static class PlatformHaptics
    {
        internal static void Pulse()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    using (var currentPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var currentActivity = currentPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var window = currentActivity.Call<AndroidJavaObject>("getWindow"))
                    using (var view = window.Call<AndroidJavaObject>("getDecorView"))
                        view.Call<bool>("performHapticFeedback", 1);
                }));
            }
#elif UNITY_IOS && !UNITY_EDITOR
            IOSHaptics.Pulse();
#endif
        }
    }
}
