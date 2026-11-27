using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace XanaduYonder.Runtime
{
    // A sheet paints to the edge of its own Canvas. Its content stops above the home gesture.
    internal sealed class DialogSheetLayout : MonoBehaviour
    {
        private RectTransform surface;
        private RectTransform content, viewport;
        private ScrollRect scroll;
        private RectMask2D clip;
        private GameObject lastSelection;
        private readonly Vector3[] selectionCorners = new Vector3[4];
        private float designWidth, contentHeight;
        private const float DismissDuration = .20f;
        private Action didDismiss;
        private Image shade;
        private Color initialShade;
        private float elapsed, travel;
        private int dismissalFrame;
        internal bool IsDismissing => didDismiss != null;
        internal RectTransform Content => content;
        internal float NormalizedPosition
        {
            get => scroll != null && scroll.enabled ? scroll.verticalNormalizedPosition : 1;
            set { if (scroll != null && scroll.enabled) scroll.verticalNormalizedPosition = Mathf.Clamp01(value); }
        }

        internal static void SetContentHeight(Transform content, float height)
        {
            var layout = content.GetComponentInParent<DialogSheetLayout>();
            if (layout == null || layout.content != content) throw new ArgumentException("Expected the current sheet content.");
            layout.contentHeight = Mathf.Max(1, height);
            layout.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, layout.contentHeight);
            layout.Refresh();
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        private int sampledWidth, sampledHeight, requestGeneration;
        private volatile float androidBottomFraction;
#endif

        internal static RectTransform Create(Image overlay, string name, float designWidth, float height, Action back)
        {
            var surface = UiFactory.Panel(overlay.transform, name, 0, 0, designWidth, height, DialogUi.Surface, false);
            DialogCorners.ApplyTop(surface, DialogCorners.Sheet * designWidth / 720f);
            surface.raycastTarget = true;
            var body = UiFactory.Rect(surface.transform, "Sheet content", 0, 0, designWidth, height);
            Attach(surface.rectTransform, body, designWidth, height);
            DialogOutsideTap.Bind(overlay, AfterClose(body, back));
            return body;
        }

        // Menu callbacks retain their original owner; late input from a replaced sheet is inert.
        internal static Action AfterClose(Transform body, Action next)
        {
            var layout = body.GetComponentInParent<DialogSheetLayout>();
            var owner = body.GetComponentInParent<DialogFocusScope>().gameObject;
            return () =>
            {
                if (layout != null && layout.isActiveAndEnabled && owner.activeInHierarchy) Dismiss(owner, next);
            };
        }

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
            layout.viewport = UiFactory.Rect(surface, "Sheet viewport", 0, 0, designWidth, contentHeight);
            layout.viewport.anchorMin = layout.viewport.anchorMax = layout.viewport.pivot = new Vector2(.5f, 1);
            content.SetParent(layout.viewport, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(designWidth, contentHeight);
            layout.Refresh();
        }

        // The full overlay continues to intercept pointers and Submit until its owner releases it.
        internal static void Dismiss(GameObject overlay, Action finished)
        {
            if (overlay == null) return;
            var layout = overlay.GetComponentInChildren<DialogSheetLayout>(true);
            if (layout == null || layout.IsDismissing) return;
            DialogFocusScope.ObserveVisible(overlay.transform);
            if (!Application.isPlaying || !layout.isActiveAndEnabled)
            {
                finished();
                return;
            }
            layout.shade = overlay.GetComponent<Image>();
            layout.initialShade = layout.shade.color;
            layout.didDismiss = finished;
            layout.dismissalFrame = Time.frameCount;
            // Preserve the resting appearance as the entire sheet becomes non-interactive.
            foreach (var button in overlay.GetComponentsInChildren<Button>())
            {
                if (!button.IsInteractable()) continue;
                var colors = button.colors;
                colors.disabledColor = colors.normalColor;
                button.colors = colors;
            }
            if (!overlay.TryGetComponent<CanvasGroup>(out var group)) group = overlay.AddComponent<CanvasGroup>();
            group.interactable = false;
        }

        private void Update()
        {
            if (IsDismissing && dismissalFrame != Time.frameCount) AdvanceDismissal(Time.unscaledDeltaTime);
        }

        internal void AdvanceDismissal(float unscaledSeconds)
        {
            if (!IsDismissing) return;
            elapsed += Mathf.Max(0, unscaledSeconds);
            float t = Mathf.Clamp01(elapsed / DismissDuration);
            travel = t * (.4f + .6f * t);
            var color = initialShade;
            color.a *= 1 - t;
            shade.color = color;
            Refresh();
            if (t < 1) return;
            var finish = didDismiss;
            didDismiss = null;
            finish();
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
            var position = boundary.TransformPoint(new Vector3(bounds.center.x, bounds.yMin - (body.yMax - bounds.yMin) * travel));
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
            if (viewport.anchoredPosition != offset) viewport.anchoredPosition = offset;
            var visibleSize = new Vector2(designWidth, body.height / scale);
            if (viewport.sizeDelta != visibleSize) viewport.sizeDelta = visibleSize;
            bool overflow = contentHeight > visibleSize.y + .1f;
            if (overflow && scroll == null)
            {
                viewport.gameObject.AddComponent<Image>().color = Color.clear;
                clip = viewport.gameObject.AddComponent<RectMask2D>();
                scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport;
                scroll.content = content;
                scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 28;
            }
            if (scroll != null)
            {
                clip.enabled = overflow;
                scroll.enabled = overflow;
            }
            if (!overflow && content.anchoredPosition != Vector2.zero) content.anchoredPosition = Vector2.zero;
        }

        // Directional navigation keeps the newly selected action visible without snapping a touch drag back.
        private void LateUpdate()
        {
            if (scroll == null || !scroll.enabled || IsDismissing) return;
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == lastSelection) return;
            lastSelection = selected;
            if (selected == null || !(selected.transform is RectTransform item) || !item.IsChildOf(content)) return;
            Reveal(item);
        }

        internal void Reveal(RectTransform item)
        {
            if (scroll == null || !scroll.enabled || item == null || !item.IsChildOf(content)) return;
            item.GetWorldCorners(selectionCorners);
            float bottom = viewport.InverseTransformPoint(selectionCorners[0]).y;
            float top = viewport.InverseTransformPoint(selectionCorners[1]).y;
            float delta = bottom < viewport.rect.yMin ? viewport.rect.yMin - bottom :
                top > viewport.rect.yMax ? viewport.rect.yMax - top : 0;
            if (delta != 0) content.anchoredPosition += new Vector2(0, delta);
        }

        // Inputs are in the same Canvas coordinate system. The safe rectangle already includes platform insets.
        internal static Rect ContentBounds(Rect canvas, Rect safe, float designWidth, float contentHeight)
        {
            float shortSide = Mathf.Min(safe.width, safe.height);
            // The sheet's existing button/footer padding is inside the body. Keep a separate 12-point minimum
            // outside it on a gestureless or immersive display. A reported system inset replaces this floor.
            float clearance = shortSide * (12f / 390f);
            float bottom = Mathf.Max(safe.yMin, canvas.yMin + clearance);
            // Even a long settings list leaves a reachable band of scrim above the sheet.
            float available = Mathf.Max(.001f, safe.yMax - bottom - shortSide * (48f / 390f));
            float height = Mathf.Min(contentHeight * shortSide / designWidth, available);
            return new Rect(safe.center.x - shortSide * .5f, bottom, shortSide, height);
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
