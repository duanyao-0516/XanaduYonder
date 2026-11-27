using UnityEngine;

namespace XanaduYonder.Runtime
{
    // A sheet paints to the edge of its own Canvas. Its content stops above the home gesture.
    internal sealed class DialogSheetLayout : MonoBehaviour
    {
        private RectTransform surface;
        private RectTransform content;
        private float designWidth, contentHeight;
#if UNITY_ANDROID && !UNITY_EDITOR
        private int sampledWidth, sampledHeight, requestGeneration;
        private volatile float androidBottomFraction;
#endif

        public static void Attach(RectTransform surface, RectTransform content, float designWidth, float contentHeight)
        {
            if (content.parent != surface || designWidth <= 0 || contentHeight <= 0)
                throw new System.ArgumentException("A sheet needs a direct content child and positive design dimensions.");
            var layout = surface.gameObject.AddComponent<DialogSheetLayout>();
            layout.surface = surface;
            layout.content = content;
            layout.designWidth = designWidth;
            layout.contentHeight = contentHeight;
            surface.anchorMin = surface.anchorMax = new Vector2(.5f, 0);
            surface.pivot = new Vector2(.5f, 0);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(designWidth, contentHeight);
            layout.Refresh();
        }

        private void OnEnable() { Canvas.preWillRenderCanvases += Refresh; }
        private void OnDisable() { Canvas.preWillRenderCanvases -= Refresh; }

        private void Refresh()
        {
            if (content == null || !(surface.parent is RectTransform parent)) return;
            var canvas = parent.GetComponentInParent<Canvas>();
            var boundary = canvas == null ? parent : (RectTransform)canvas.transform;
            var bounds = boundary.rect;
            if (bounds.width <= 0 || bounds.height <= 0) return;

            var safe = bounds;
            if (canvas != null && canvas.rootCanvas.renderMode != RenderMode.WorldSpace)
            {
                var safePixels = Screen.safeArea;
#if UNITY_ANDROID && !UNITY_EDITOR
                if (sampledWidth != Screen.width || sampledHeight != Screen.height)
                {
                    sampledWidth = Screen.width;
                    sampledHeight = Screen.height;
                    SampleAndroidBottom();
                }
                safePixels.yMin = Mathf.Max(safePixels.yMin, androidBottomFraction * Screen.height);
#endif
                var camera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
                if (safePixels.width > 0 && safePixels.height > 0 &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(boundary, safePixels.min, camera, out var min) &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(boundary, safePixels.max, camera, out var max))
                {
                    var overlap = Rect.MinMaxRect(Mathf.Max(bounds.xMin, min.x), Mathf.Max(bounds.yMin, min.y),
                        Mathf.Min(bounds.xMax, max.x), Mathf.Min(bounds.yMax, max.y));
                    if (overlap.width > 0 && overlap.height > 0) safe = overlap;
                }
            }

            var body = ContentBounds(bounds, safe, designWidth, contentHeight);
            float scale = body.width / designWidth;
            Vector2 dimensions = new Vector2(bounds.width / scale, (body.yMax - bounds.yMin) / scale);
            if (surface.sizeDelta != dimensions) surface.sizeDelta = dimensions;
            var position = boundary.TransformPoint(new Vector3(bounds.center.x, bounds.yMin));
            if (surface.position != position) surface.position = position;
            if (surface.rotation != boundary.rotation) surface.rotation = boundary.rotation;
            // The lobby has a 390-unit scaled parent. Other hosts use 720 units and may rotate their safe frame.
            var xUnit = parent.TransformVector(surface.localRotation * Vector3.right).magnitude;
            var yUnit = parent.TransformVector(surface.localRotation * Vector3.up).magnitude;
            if (xUnit <= 0 || yUnit <= 0) return;
            var localScale = new Vector3(scale * boundary.TransformVector(Vector3.right).magnitude / xUnit,
                scale * boundary.TransformVector(Vector3.up).magnitude / yUnit, 1);
            if (surface.localScale != localScale) surface.localScale = localScale;
            var offset = new Vector2((body.center.x - bounds.center.x) / scale, 0);
            if (content.anchoredPosition != offset) content.anchoredPosition = offset;
        }

        // Inputs are in the same Canvas coordinate system. The safe rectangle already includes platform insets.
        internal static Rect ContentBounds(Rect canvas, Rect safe, float designWidth, float contentHeight)
        {
            float shortSide = Mathf.Min(safe.width, safe.height);
            // The sheet's existing button/footer padding is inside the body. Keep a separate 12-point minimum
            // outside it on a gestureless or immersive display. A reported system inset replaces this floor.
            float clearance = shortSide * (12f / 390f);
            float bottom = Mathf.Max(safe.yMin, canvas.yMin + clearance);
            float available = Mathf.Max(.001f, safe.yMax - bottom - clearance);
            float scale = Mathf.Min(shortSide / designWidth, available / contentHeight);
            float width = designWidth * scale;
            return new Rect(safe.center.x - width * .5f, bottom, width, contentHeight * scale);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void OnApplicationFocus(bool focused)
        {
            if (focused) sampledWidth = 0;
        }

        private void SampleAndroidBottom()
        {
            int generation = System.Threading.Interlocked.Increment(ref requestGeneration);
            androidBottomFraction = 0;
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                activity?.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        float ratio = ReadAndroidBottom();
                        if (generation == System.Threading.Volatile.Read(ref requestGeneration))
                            androidBottomFraction = ratio;
                    }
                    catch (AndroidJavaException error) { Debug.LogWarning("Sheet window insets: " + error.Message); }
                }));
        }

        private static float ReadAndroidBottom()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var window = activity?.Call<AndroidJavaObject>("getWindow"))
            using (var decor = window?.Call<AndroidJavaObject>("getDecorView"))
            using (var insets = decor?.Call<AndroidJavaObject>("getRootWindowInsets"))
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                if (insets == null) return 0;
                int sdk = version.GetStatic<int>("SDK_INT");
                int bottom;
                if (sdk >= 30)
                {
                    using (var types = new AndroidJavaClass("android.view.WindowInsets$Type"))
                    using (var gesture = insets.Call<AndroidJavaObject>("getInsetsIgnoringVisibility", types.CallStatic<int>("mandatorySystemGestures")))
                    using (var visibleNav = insets.Call<AndroidJavaObject>("getInsets", types.CallStatic<int>("navigationBars")))
                        bottom = System.Math.Max(gesture.Get<int>("bottom"), visibleNav.Get<int>("bottom"));
                }
                else if (sdk == 29)
                {
                    using (var gesture = insets.Call<AndroidJavaObject>("getMandatorySystemGestureInsets"))
                        bottom = System.Math.Max(gesture.Get<int>("bottom"), insets.Call<int>("getSystemWindowInsetBottom"));
                }
                else bottom = insets.Call<int>("getSystemWindowInsetBottom");
                // WindowInsets use decor coordinates. Exclude space below the Android content view if the
                // window has already inset Unity. Normalizing also supports a downscaled Unity surface.
                using (var id = new AndroidJavaClass("android.R$id"))
                using (var view = decor.Call<AndroidJavaObject>("findViewById", id.GetStatic<int>("content")))
                using (var rect = new AndroidJavaObject("android.graphics.Rect"))
                {
                    int height = view?.Call<int>("getHeight") ?? 0;
                    if (height <= 0 || !view.Call<bool>("getGlobalVisibleRect", rect)) return 0;
                    int below = decor.Call<int>("getHeight") - rect.Get<int>("bottom");
                    return (float)System.Math.Max(0, bottom - below) / height;
                }
            }
        }
#endif
    }
}
