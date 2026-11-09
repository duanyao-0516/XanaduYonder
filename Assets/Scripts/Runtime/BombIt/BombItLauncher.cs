using System;
using UnityEngine;

namespace XanaduYonder.Runtime.BombIt
{
    // Owns only the native host boundary. Original gameplay and saves remain in the
    // bundled runtime, with a fixed per-game origin maintained by the Activity.
    public sealed class BombItLauncher : MonoBehaviour
    {
        public const string OpenFailureMessage = "Q版泡泡堂暂时无法打开，请稍后重试。";
        private const string BridgeClass = "com.xanaduyonder.bombit.BombItBridge";
        public event Action<string> Returned;
        public bool IsBusy { get; private set; }
        private float nextStatusCheck;

        public bool TryOpen(bool soundEnabled, bool hapticsEnabled)
        {
            if (IsBusy) return true;
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (!IOSWebGameBridge.OpenBombIt(soundEnabled, hapticsEnabled)) return false;
                IsBusy = true;
                nextStatusCheck = Time.unscaledTime + .25f;
                return true;
            }
            catch (Exception error) { Debug.LogException(error); return false; }
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass(BridgeClass))
                {
                    if (!bridge.CallStatic<bool>("open", activity, soundEnabled, hapticsEnabled))
                    {
                        Debug.LogError("Bomb It launch rejected: " + bridge.CallStatic<string>("getLastError"));
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
            Debug.LogWarning("Bomb It requires a bundled mobile WebView host.");
            return false;
#endif
        }

        private void Update()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (!IsBusy || Time.unscaledTime < nextStatusCheck) return;
            nextStatusCheck = Time.unscaledTime + .25f;
            string failure;
            try
            {
#if UNITY_IOS
                if (IOSWebGameBridge.IsRunning()) return;
                failure = IOSWebGameBridge.GetLastError();
#else
                using (var bridge = new AndroidJavaClass(BridgeClass))
                {
                    if (bridge.CallStatic<bool>("isRunning")) return;
                    failure = bridge.CallStatic<string>("getLastError");
                }
#endif
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                failure = error.Message;
            }
            IsBusy = false;
            if (!string.IsNullOrEmpty(failure)) Debug.LogError("Bomb It: " + failure);
            Returned?.Invoke(string.IsNullOrEmpty(failure) ? null : OpenFailureMessage);
#endif
        }
    }
}
