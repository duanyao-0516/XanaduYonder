using System;
using UnityEngine;

namespace XanaduYonder.Runtime.CutTheRope
{
    // The launcher owns only Activity handoff and hall input. WebView owns gameplay/saves.
    public sealed class CutTheRopeLauncher : MonoBehaviour
    {
        public const string OpenFailureMessage = "割绳子暂时无法打开，请稍后重试。";
        private const string BridgeClass = "com.xanaduyonder.cuttherope.CutTheRopeBridge";
        private CutTheRopeSession session;
        private bool focused = true;
        private bool paused;
        public event Action<string> Returned;
        public bool IsBusy => session != null && session.IsBusy;

        public bool TryOpen(bool soundEnabled)
        {
            if (session == null)
            {
                session = new CutTheRopeSession(new AndroidBridge());
                session.Returned += OnReturned;
            }
            var opened = session.TryOpen(soundEnabled);
            if (!opened && !IsBusy && !string.IsNullOrEmpty(session.LastError))
                Debug.LogWarning("Cut the Rope launch failed: " + session.LastError);
            return opened;
        }

        private void LateUpdate()
        {
            session?.Tick(Time.unscaledTimeAsDouble, focused && !paused,
                Input.GetKey(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetMouseButton(0) || Input.touchCount != 0);
        }

        private void OnApplicationPause(bool value) { paused = value; }
        private void OnApplicationFocus(bool value) { focused = value; }

        private void OnReturned(string failure)
        {
            if (!string.IsNullOrEmpty(failure)) Debug.LogError("Cut the Rope: " + failure);
            Returned?.Invoke(string.IsNullOrEmpty(failure) ? null : OpenFailureMessage);
        }

        private void OnDestroy()
        {
            session?.Dispose();
            session = null;
            Returned = null;
        }

        private sealed class AndroidBridge : ICutTheRopeBridge
        {
            public bool Open(bool soundEnabled)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass(BridgeClass))
                    return bridge.CallStatic<bool>("open", activity, soundEnabled);
#else
                return false;
#endif
            }

            public bool IsRunning()
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var bridge = new AndroidJavaClass(BridgeClass))
                    return bridge.CallStatic<bool>("isRunning");
#else
                return false;
#endif
            }

            public string GetLastError()
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var bridge = new AndroidJavaClass(BridgeClass))
                    return bridge.CallStatic<string>("getLastError");
#else
                return "The bundled Android runtime is required.";
#endif
            }
        }
    }
}
