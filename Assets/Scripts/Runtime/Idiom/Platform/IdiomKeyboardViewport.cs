using UnityEngine;
using UnityEngine.EventSystems;

namespace XanaduYonder.Runtime.Idiom
{
    // Android TouchScreenKeyboard.area is always zero. Native measurements account
    // for a resized rendering surface so an IME cannot be subtracted twice.
    [DefaultExecutionOrder(-100)]
    public sealed class IdiomKeyboardViewport : MonoBehaviour
    {
        private RectTransform frame;
        private bool paused;
        private float hiddenAt = -10;
        private readonly Vector3[] corners = new Vector3[4];
        public float BottomInset { get; private set; }
        public bool IsVisible { get; private set; }
        public bool IsForeground { get; private set; } = true;
        public bool JustClosed { get { return Time.unscaledTime - hiddenAt < .3f; } }
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaClass bridge;
#endif
        public void Initialize(RectTransform safeFrame)
        {
            frame = safeFrame;
            Install();
        }

        private void Install()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (bridge != null) return;
            try
            {
                bridge = new AndroidJavaClass("com.duanyao.xanaduyonder.IdiomKeyboardBridge");
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    bridge.CallStatic("install", activity);
            }
            catch (System.Exception e) { Debug.LogWarning("IME bridge: " + e.Message); }
#endif
        }

        private void Update()
        {
            var previous = IsVisible;
            IsVisible = TouchScreenKeyboard.visible;
            IsForeground = !paused;
            var physicalOcclusion = TouchScreenKeyboard.area.height;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (bridge != null)
            {
                try
                {
                    var sample = bridge.CallStatic<int[]>("sample");
                    if (sample != null && sample.Length == 4)
                    {
                        IsVisible |= sample[0] != 0;
                        IsForeground = !paused && sample[3] != 0;
                        physicalOcclusion = sample[2] > 0 ? sample[1] * (float)Screen.height / sample[2] : 0;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("IME measurement: " + e.Message);
                    bridge.Dispose(); bridge = null;
                }
            }
#endif
            if (previous && !IsVisible) hiddenAt = Time.unscaledTime;
            if (frame == null) return;
            frame.GetWorldCorners(corners);
            var bottom = RectTransformUtility.WorldToScreenPoint(null, corners[0]).y;
            var top = RectTransformUtility.WorldToScreenPoint(null, corners[1]).y;
            var pixelsPerUnit = (top - bottom) / Mathf.Max(1, frame.rect.height);
            BottomInset = IsVisible ? Mathf.Max(0, physicalOcclusion - bottom) / Mathf.Max(.01f, pixelsPerUnit) : 0;
            BottomInset = Mathf.Min(BottomInset, Mathf.Max(0, frame.rect.height - 440));
        }

        public void Hide()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
#if UNITY_ANDROID && !UNITY_EDITOR
            if (bridge != null) bridge.CallStatic("hide");
#endif
            hiddenAt = Time.unscaledTime;
        }

        private void OnApplicationPause(bool value) { paused = value; IsForeground = !value; }
        private void OnEnable() { if (frame != null) { paused = false; Install(); } }
        private void OnDisable() { Release(); IsVisible = false; BottomInset = 0; }
        private void OnDestroy() { Release(); }
        private void Release()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (bridge != null) { bridge.CallStatic("uninstall"); bridge.Dispose(); bridge = null; }
#endif
        }
    }
}
