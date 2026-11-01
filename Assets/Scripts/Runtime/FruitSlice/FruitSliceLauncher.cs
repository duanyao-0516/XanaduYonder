using System;
using UnityEngine;

namespace XanaduYonder.Runtime.FruitSlice
{
    // Unity owns the lobby; the Android Activity owns the browser and its complete lifetime.
    public sealed class FruitSliceLauncher : MonoBehaviour
    {
        private const string BridgeClass = "com.xanaduyonder.fruitslice.FruitSliceBridge";
        public const string OpenFailureMessage = "切水果大师暂时无法打开，请稍后重试。";
        public event Action<string> Returned;
        public bool IsBusy { get; private set; }
        private float nextCheck;

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
                    if (!bridge.CallStatic<bool>("open", activity, soundEnabled, hapticsEnabled)) return false;
                }
                IsBusy = true;
                nextCheck = Time.unscaledTime + .25f;
                return true;
            }
            catch (Exception error) { Debug.LogException(error); return false; }
#else
            Debug.LogWarning("Fruit Slice requires Android WebView and the bundled offline content.");
            return false;
#endif
        }

        private void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsBusy || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + .25f;
            string failure;
            try
            {
                using (var bridge = new AndroidJavaClass(BridgeClass))
                {
                    if (bridge.CallStatic<bool>("isRunning")) return;
                    failure = bridge.CallStatic<string>("getLastError");
                }
            }
            catch (Exception error) { failure = error.Message; }
            IsBusy = false;
            if (!string.IsNullOrEmpty(failure)) Debug.LogError("Fruit Slice: " + failure);
            Returned?.Invoke(string.IsNullOrEmpty(failure) ? null : OpenFailureMessage);
#endif
        }
    }
}
