using System;
using UnityEngine;

namespace XanaduYonder.Runtime.ZhaoyunAdou
{
    // Only the Android Activity boundary belongs here; game rules and saves remain in the offline web content.
    public sealed class ZhaoyunAdouLauncher : MonoBehaviour
    {
        public const string OpenFailureMessage = "赵云与阿斗暂时无法打开，请稍后重试。";
        private const string BridgeClass = "com.xanaduyonder.zhaoyunadou.ZhaoyunAdouBridge";
        public event Action<string> Returned;
        public bool IsBusy { get; private set; }
        private float nextStatusCheck;

        public bool TryOpen(bool soundEnabled, bool hapticsEnabled)
        {
            if (IsBusy) return true;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass(BridgeClass))
                {
                    if (!bridge.CallStatic<bool>("open", activity, soundEnabled, hapticsEnabled))
                    {
                        Debug.LogError("Zhaoyun Adou launch rejected: " + bridge.CallStatic<string>("getLastError"));
                        return false;
                    }
                }
                IsBusy = true;
                nextStatusCheck = Time.unscaledTime + .25f;
                return true;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                return false;
            }
#else
            Debug.LogWarning("Zhaoyun Adou requires the bundled Android runtime.");
            return false;
#endif
        }

        private void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsBusy || Time.unscaledTime < nextStatusCheck) return;
            nextStatusCheck = Time.unscaledTime + .25f;
            string failure;
            try
            {
                using (var bridge = new AndroidJavaClass(BridgeClass))
                {
                    // open() marks the queued UI-thread launch as running too. This
                    // covers launch failures without requiring a focus-loss event.
                    if (bridge.CallStatic<bool>("isRunning")) return;
                    failure = bridge.CallStatic<string>("getLastError");
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                failure = error.Message;
            }
            IsBusy = false;
            if (!string.IsNullOrEmpty(failure)) Debug.LogError("Zhaoyun Adou: " + failure);
            Returned?.Invoke(string.IsNullOrEmpty(failure) ? null : OpenFailureMessage);
#endif
        }
    }
}
