using System;
using UnityEngine;
using XanaduYonder.Runtime;

namespace XanaduYonder.EditorTools
{
    internal static class DialogSheetGeometryChecks
    {
        public static void Run()
        {
            var phone = new Rect(0, 0, 1220, 2656);
            var gestureSafe = Rect.MinMaxRect(0, 78, 1220, 2506);
            var body = DialogSheetLayout.ContentBounds(phone, gestureSafe, 720, 540);
            Check(Near(body.yMin, 78) && Near(body.width, 1220),
                "The Xiaomi-sized sheet uses its reported bottom gesture band once.");
            var immersive = DialogSheetLayout.ContentBounds(phone, phone, 720, 540);
            Check(immersive.yMin > 0 && immersive.yMin < 78,
                "A zero system inset retains compact footer clearance on an immersive display.");

            var landscape = new Rect(0, 0, 850, 390);
            var cutout = Rect.MinMaxRect(80, 34, 850, 390);
            var compact = DialogSheetLayout.ContentBounds(landscape, cutout, 390, 850);
            Check(compact.yMin >= cutout.yMin && compact.yMax < cutout.yMax && compact.xMin > cutout.xMin &&
                  Near(compact.center.x, cutout.center.x) && Near(compact.width, cutout.height) && compact.height / compact.width < 850f / 390,
                "A tall sheet keeps the scale of its controls and scrolls within the available landscape height.");
            CheckOwnedCanvas();
            CheckScrollableSheet();
        }

        // Synchronous render fixtures advance the same unscaled clock as a rendered frame.
        internal static void CompleteDismissal(Transform overlay)
        {
            var sheet = overlay.GetComponentInChildren<DialogSheetLayout>();
            Check(sheet != null && sheet.IsDismissing, "A user close starts the sheet transition.");
            sheet.AdvanceDismissal(1f);
        }

        private static void CheckOwnedCanvas()
        {
            var window = new GameObject("Sheet geometry window", typeof(RectTransform), typeof(Canvas));
            try
            {
                var windowRect = (RectTransform)window.transform;
                window.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                windowRect.sizeDelta = new Vector2(1200, 900);
                var child = UiFactory.Rect(windowRect, "Owned sheet canvas", 70, 80, 430, 640);
                child.gameObject.AddComponent<Canvas>();
                child.localRotation = Quaternion.Euler(0, 0, 12);
                var safeFrame = UiFactory.Rect(child, "Inset and rotated safe frame", 24, 40, 360, 500);
                safeFrame.localScale = Vector3.one * .78f;
                safeFrame.localRotation = Quaternion.Euler(0, 0, -17);
                var surface = UiFactory.Rect(safeFrame, "Sheet", 2, 5, 330, 280);
                var content = UiFactory.Rect(surface, "Sheet content", 0, 0, 1, 1);
                DialogSheetLayout.Attach(surface, content, 390, 310);
                var expected = new Vector3[4];
                var actual = new Vector3[4];
                child.GetWorldCorners(expected);
                surface.GetWorldCorners(actual);
                Check(Vector3.Distance(expected[0], actual[0]) < .1f &&
                      Vector3.Distance(expected[3], actual[3]) < .1f,
                    "The sheet paints to both bottom corners of its nearest Canvas through a transformed safe frame.");
                content.GetWorldCorners(actual);
                float contentBottom = child.InverseTransformPoint(actual[0]).y;
                Check(contentBottom > child.rect.yMin && Near(content.rect.width, 390) && Near(content.rect.height, 310),
                    "The existing 390-unit controls keep their design space inside the physical sheet.");
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        private static void CheckScrollableSheet()
        {
            var root = new GameObject("Short settings canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var bounds = (RectTransform)root.transform;
                bounds.sizeDelta = new Vector2(800, 360);
                var surface = UiFactory.Rect(bounds, "Sheet", 0, 0, 0, 0);
                var body = UiFactory.Rect(surface, "Settings", 0, 0, 540, 690);
                var last = UiFactory.Rect(body, "Last action", 26, 600, 488, 66);
                DialogSheetLayout.Attach(surface, body, 540, 690);
                var scroll = surface.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
                Check(scroll != null && scroll.enabled && scroll.viewport.rect.height < body.rect.height,
                    "Long settings can scroll in a short landscape window.");
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 0;
                var corners = new Vector3[4];
                last.GetWorldCorners(corners);
                Check(scroll.viewport.InverseTransformPoint(corners[0]).y >= scroll.viewport.rect.yMin - 1 &&
                      scroll.viewport.InverseTransformPoint(corners[1]).y <= scroll.viewport.rect.yMax + 1 &&
                      Mathf.Abs(bounds.InverseTransformPoint(corners[0]).y - bounds.InverseTransformPoint(corners[1]).y) >= 43,
                    "The bottom action scrolls fully into view at its intended touch size.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static bool Near(float first, float second) => Mathf.Abs(first - second) < .01f;

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
