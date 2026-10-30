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
        // The page must hold its final composer position while native and Unity
        // describe different surface sizes, not combine old inset with new height.
        public bool LayoutMeasurementReady { get; private set; } = true;
        public bool JustClosed { get { return Time.unscaledTime - hiddenAt < .3f; } }
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaClass bridge;
        private float unmatchedSampleAt = -1;
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

        // SafeDesignFrame refreshes in Update. Convert all native coordinates
        // against that completed frame before IdiomPage's default-order LateUpdate.
        private void LateUpdate()
        {
            LayoutMeasurementReady = true;
            var previous = IsVisible;
            var visible = TouchScreenKeyboard.visible;
            IsForeground = !paused;
            var physicalOcclusion = TouchScreenKeyboard.area.height;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (bridge != null)
            {
                try
                {
                    var sample = bridge.CallStatic<int[]>("sample");
                    bool nativeVisible, nativeForeground;
                    float nativeOcclusion;
                    if (TryConvertNativeSample(sample, Screen.width, Screen.height,
                        out nativeVisible, out nativeForeground, out nativeOcclusion))
                    {
                        // The native packet describes one measurement. Do not OR
                        // in Unity's independently cached keyboard visibility.
                        visible = nativeVisible;
                        IsForeground = !paused && nativeForeground;
                        physicalOcclusion = nativeOcclusion;
                        unmatchedSampleAt = -1;
                    }
                    else
                    {
                        // Android and Unity can publish a resized surface on
                        // different frames. Briefly retain the last valid inset,
                        // never scale an old surface sample into new geometry.
                        if (unmatchedSampleAt < 0) unmatchedSampleAt = Time.unscaledTime;
                        if (Time.unscaledTime - unmatchedSampleAt < .15f)
                        {
                            LayoutMeasurementReady = false;
                            return;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("IME measurement: " + e.Message);
                    bridge.Dispose(); bridge = null;
                }
            }
#endif
            IsVisible = visible;
            if (previous && !IsVisible) hiddenAt = Time.unscaledTime;
            if (frame == null) return;
            frame.GetWorldCorners(corners);
            var bottom = RectTransformUtility.WorldToScreenPoint(null, corners[0]).y;
            var top = RectTransformUtility.WorldToScreenPoint(null, corners[1]).y;
            var pixelsPerUnit = (top - bottom) / Mathf.Max(1, frame.rect.height);
            BottomInset = IsVisible ? Mathf.Max(0, physicalOcclusion - bottom) / Mathf.Max(.01f, pixelsPerUnit) : 0;
            BottomInset = Mathf.Min(BottomInset, Mathf.Max(0, frame.rect.height - 440));
        }

        internal static bool TryConvertNativeSample(int[] sample, int screenWidth, int screenHeight,
            out bool visible, out bool foreground, out float occlusion)
        {
            visible = false; foreground = false; occlusion = 0;
            if (sample == null || sample.Length != 5 || screenWidth <= 0 || screenHeight <= 0 ||
                sample[2] <= 0 || sample[4] <= 0 || sample[1] < 0 || sample[1] > sample[2] ||
                sample[0] < 0 || sample[0] > 1 || sample[3] < 0 || sample[3] > 1) return false;
            // Different rendering resolutions are fine; a different aspect ratio
            // means this packet belongs to the surface before/after a resize.
            var nativeRatio = (double)sample[4] / sample[2];
            var unityRatio = (double)screenWidth / screenHeight;
            if (System.Math.Abs(nativeRatio / unityRatio - 1) > .025) return false;
            visible = sample[0] != 0;
            foreground = sample[3] != 0;
            occlusion = visible ? sample[1] * (float)screenHeight / sample[2] : 0;
            return true;
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
            LayoutMeasurementReady = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            unmatchedSampleAt = -1;
            if (bridge != null) { bridge.CallStatic("uninstall"); bridge.Dispose(); bridge = null; }
#endif
        }
    }
}
