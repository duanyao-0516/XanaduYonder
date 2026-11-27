using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.BoardGames;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Opt-in observer. Existing production-page checks own the inputs, save fixtures and camera rendering.
    [InitializeOnLoad]
    internal static class DialogScreenshotAudit
    {
        [Serializable]
        private sealed class Entry
        {
            public string suite, game, window, overlay, presentation, role, section, png;
            public int width, height;
            public DialogAlignmentAudit.Label[] alignmentLabels;
            public float scrimAlpha;
            public bool bottomAligned;
        }

        private static readonly string Output = Environment.GetEnvironmentVariable("XANADU_DIALOG_AUDIT");
        private static readonly string Suite = Environment.GetEnvironmentVariable("XANADU_DIALOG_SUITE") ?? "direct";
        private static readonly HashSet<Transform> Open = new HashSet<Transform>();
        private static readonly HashSet<string> Observed = new HashSet<string>(), Captured = new HashSet<string>();
        private static readonly Dictionary<Transform, string> Rendered = new Dictionary<Transform, string>();
        private static readonly FieldInfo BoardKind = typeof(BoardGamePage).GetField("game", BindingFlags.NonPublic | BindingFlags.Instance);
        private static bool capturing;
        private static Transform sectionScope;
        private static string sectionName;

        static DialogScreenshotAudit()
        {
            if (string.IsNullOrWhiteSpace(Output)) return;
            Directory.CreateDirectory(Path.Combine(Output, Suite));
            DialogFocusScope.VisibilityChanged += VisibilityChanged;
            DialogFocusScope.BeforeInput += SampleBeforeInput;
            Camera.onPostRender += AfterCamera;
            EditorApplication.update += SampleScreenCanvas;
        }

        private static void VisibilityChanged(Transform scope, bool visible)
        {
            if (visible) { Open.Add(scope); return; }
            if (!Open.Remove(scope)) return;
            // Closing synchronously before a fixture renders is a coverage gap worth recording.
            if (!Rendered.Remove(scope) && scope != null && scope.transform.childCount > 0)
            {
                var entry = Describe(scope, out _);
                string key = Identity(entry);
                if (Observed.Add(key)) Write(entry);
            }
        }

        private static string Game(Transform scope, Canvas canvas)
        {
            var board = scope.GetComponentInParent<BoardGamePage>(true);
            if (board != null) return BoardKind.GetValue(board).ToString();
            foreach (var element in scope.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (element == null) continue;
                string type = element.GetType().Name;
                if (type.EndsWith("Page", StringComparison.Ordinal)) return type.Substring(0, type.Length - 4);
                if (type == "LobbyView") return "Lobby";
            }
            // Host-owned settings are attached beside the active page.
            if (canvas != null)
                foreach (var element in canvas.GetComponentsInChildren<MonoBehaviour>())
                {
                    if (element == null) continue;
                    string type = element.GetType().Name;
                    if (type.EndsWith("Page", StringComparison.Ordinal)) return type.Substring(0, type.Length - 4);
                }
            var parent = scope.transform.parent;
            return parent == null ? Suite : parent.name;
        }

        private static Entry Describe(Transform scope, out Text heading)
        {
            var canvas = scope.GetComponentInParent<Canvas>(true);
            var sheet = scope.GetComponentInChildren<DialogSheetLayout>(true);
            var labels = scope.GetComponentsInChildren<Text>(true);
            heading = labels.FirstOrDefault(t => t.name == "LinkMatch dialog title" && !string.IsNullOrWhiteSpace(t.text)) ??
                labels.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.text) && t.text.Any(char.IsLetter) &&
                t.GetComponentInParent<Selectable>(true) == null);
            if (heading == null) heading = labels.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.text));
            string title = heading == null ? scope.name : heading.text.Trim().Split('\n')[0];
            var shade = scope.GetComponent<Graphic>();
            var entry = new Entry { suite = Suite, game = Game(scope, canvas), window = title, overlay = scope.name,
                presentation = sheet == null ? "dialog" : "sheet", scrimAlpha = shade == null ? -1 : shade.color.a,
                section = sectionScope == scope ? sectionName : null };
            if (entry.game == "Sudoku" && title == "一步解题提示")
            {
                var actions = scope.GetComponentsInChildren<Button>(true);
                entry.role = title + (actions.Any(b => b.name == "直接揭晓这一格") ? " · 直接提示" :
                    actions.Any(b => b.name == "揭晓") ? " · 逻辑提示" : " · 已揭晓");
            }
            if (entry.game == "Sudoku" && title == "重新开始这一局？")
                entry.role = title + (entry.scrimAlpha > .99f ? " · 遮挡盘面" : " · 可见盘面");
            if (entry.game == "Idiom" && labels.Any(t => t.text == "加入生词本" || t.text == "我记住了"))
                entry.role = "成语释义";
            if (sheet != null && canvas != null)
            {
                var a = new Vector3[4]; var b = new Vector3[4];
                ((RectTransform)canvas.transform).GetWorldCorners(a);
                ((RectTransform)sheet.transform).GetWorldCorners(b);
                float tolerance = Mathf.Max(.01f, Vector3.Distance(a[0], a[3]) * .002f);
                entry.bottomAligned = Vector3.Distance(a[0], b[0]) <= tolerance && Vector3.Distance(a[3], b[3]) <= tolerance;
            }
            return entry;
        }

        private static string Identity(Entry e) => e.game + "/" + e.overlay + "/" + e.window + "/" + e.presentation
            + (string.IsNullOrEmpty(e.role) ? "" : "/" + e.role) + (string.IsNullOrEmpty(e.section) ? "" : "/" + e.section);
        private static string Safe(string value, int limit)
        {
            var name = new StringBuilder();
            foreach (char c in value)
            {
                if (name.Length >= limit) break;
                name.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-');
            }
            return name.Length == 0 ? "dialog" : name.ToString();
        }

        private static void Write(Entry e)
        {
            File.AppendAllText(Path.Combine(Output, Suite, "windows.jsonl"), JsonUtility.ToJson(e) + "\n", new UTF8Encoding(false));
        }

        // Camera pixels describe one composited frame. Assign them only to the frontmost
        // active surface on that camera, even when a paused board remains visible beneath it.
        private static Transform ForegroundScope(Camera camera)
        {
            Transform front = null;
            Graphic top = null;
            foreach (var scope in Open)
            {
                if (scope == null || !scope.gameObject.activeInHierarchy || scope.childCount == 0) continue;
                foreach (var graphic in scope.GetComponentsInChildren<Graphic>())
                {
                    if (!graphic.isActiveAndEnabled || graphic.canvas == null || graphic.depth < 0 ||
                        graphic.canvasRenderer.cull || graphic.color.a * graphic.canvasRenderer.GetInheritedAlpha() <= .01f) continue;
                    var canvas = graphic.canvas.rootCanvas;
                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.worldCamera != camera) continue;
                    if (top != null && !(top == graphic && scope.IsChildOf(front)) && !DrawsAbove(graphic, top)) continue;
                    top = graphic;
                    front = scope;
                }
            }
            return front;
        }

        private static bool DrawsAbove(Graphic first, Graphic second)
        {
            var a = first.canvas.rootCanvas;
            var b = second.canvas.rootCanvas;
            if (a != b)
            {
                int layerA = SortingLayer.GetLayerValueFromID(a.sortingLayerID);
                int layerB = SortingLayer.GetLayerValueFromID(b.sortingLayerID);
                if (layerA != layerB) return layerA > layerB;
                if (a.sortingOrder != b.sortingOrder) return a.sortingOrder > b.sortingOrder;
                if (a.renderOrder != b.renderOrder) return a.renderOrder > b.renderOrder;
            }
            return first.depth > second.depth;
        }

        private static void AfterCamera(Camera camera)
        {
            var target = camera.targetTexture;
            if (target == null || Open.Count == 0) return;
            var scope = ForegroundScope(camera);
            if (scope == null) return;
            var sheet = scope.GetComponentInChildren<DialogSheetLayout>();
            if (sheet != null && (sheet.IsPresenting || sheet.IsDismissing)) return;
            var entry = Describe(scope, out var heading);
            string identity = Identity(entry);
            string size = target.width + "x" + target.height;
            string key = identity + "/" + size;
            if (Rendered.TryGetValue(scope, out var last) && last == key) return;
            if (Captured.Contains(key)) { Rendered[scope] = key; return; }
            entry.width = target.width; entry.height = target.height;
            var folder = Path.Combine(Output, Suite, Safe(entry.game, 40));
            Directory.CreateDirectory(folder);
            string name = Safe(entry.overlay, 36) + "-" + Safe(entry.window, 46) + "-" + size + "-" +
                Hash128.Compute(identity).ToString().Substring(0, 8) + ".png";
            var previous = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                // A newly attached world-space canvas can render one clear-only frame before its UI layer is ready.
                // Keep waiting for the first frame with drawn pixels instead of deduplicating against the blank image.
                var pixels = texture.GetRawTextureData<byte>();
                bool drawn = false;
                for (int index = 3; index < pixels.Length; index++)
                    if (pixels[index] != pixels[index % 3]) { drawn = true; break; }
                if (!drawn) return;
                Rendered[scope] = key;
                File.WriteAllBytes(Path.Combine(folder, name), texture.EncodeToPNG());
                entry.png = Safe(entry.game, 40) + "/" + name;
                entry.alignmentLabels = DialogAlignmentAudit.Measure(scope, heading, sheet);
                Write(entry);
                Captured.Add(key);
            }
            finally { Object.DestroyImmediate(texture); RenderTexture.active = previous; }
        }

        private static void SampleBeforeInput(Transform origin)
        {
            if (capturing || Open.Count == 0) return;
            for (var owner = origin; owner != null; owner = owner.parent)
            {
                if (!Open.Contains(owner) || !owner.gameObject.activeInHierarchy) continue;
                var sheet = owner.GetComponentInChildren<DialogSheetLayout>();
                if (sheet != null && sheet.IsPresenting) return;
                var current = Describe(owner, out _);
                if (current.scrimAlpha <= 0 || (Rendered.TryGetValue(owner, out string key) &&
                    key.StartsWith(Identity(current) + "/", StringComparison.Ordinal))) return;
                CaptureCurrent(owner);
                return;
            }
        }

        internal static void CaptureSection(Transform owner, string section)
        {
            if (string.IsNullOrWhiteSpace(Output) || owner == null) return;
            sectionScope = Open.FirstOrDefault(s => s != null && (owner.IsChildOf(s) || s.IsChildOf(owner)));
            if (sectionScope == null) return;
            sectionName = section;
            try { CaptureCurrent(sectionScope); }
            finally { sectionScope = null; sectionName = null; }
        }

        internal static void CaptureCurrent(Transform owner)
        {
            if (string.IsNullOrWhiteSpace(Output) || capturing || owner == null) return;
            DialogSheetGeometryChecks.CompletePresentation(owner);
            var parent = owner.GetComponentInParent<Canvas>();
            Canvas temporary = null;
            if (parent == null && owner.root is RectTransform bounds)
            {
                temporary = bounds.gameObject.AddComponent<Canvas>();
                temporary.renderMode = RenderMode.WorldSpace;
                parent = temporary;
            }
            if (parent == null) return;
            try
            {
                var canvas = parent.rootCanvas;
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) CaptureOverlay(canvas);
                else if (canvas.worldCamera != null && canvas.worldCamera.targetTexture != null)
                {
                    capturing = true;
                    try { Canvas.ForceUpdateCanvases(); canvas.worldCamera.Render(); }
                    finally { capturing = false; }
                }
                else CaptureCanvas(canvas, true);
            }
            finally { if (temporary != null) Object.DestroyImmediate(temporary); }
        }

        private static void SampleScreenCanvas()
        {
            if (!EditorApplication.isPlaying || capturing || Open.Count == 0) return;
            foreach (var scope in Open.ToArray())
            {
                if (scope == null || !scope.gameObject.activeInHierarchy || Rendered.ContainsKey(scope)) continue;
                var canvas = scope.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    CaptureOverlay(canvas.rootCanvas);
            }
        }

        // Overlay canvases normally bypass cameras. Synchronous fixtures can explicitly sample this state before Back.
        internal static void CaptureOverlay(Canvas canvas)
        {
            if (string.IsNullOrWhiteSpace(Output) || capturing || canvas.renderMode != RenderMode.ScreenSpaceOverlay) return;
            CaptureCanvas(canvas, false);
        }

        private static void CaptureCanvas(Canvas canvas, bool worldSpace)
        {
            var bounds = (RectTransform)canvas.transform;
            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            var previousDistance = canvas.planeDistance;
            var go = new GameObject("Dialog screenshot audit camera", typeof(Camera));
            var camera = go.GetComponent<Camera>(); camera.enabled = false;
            int width = Mathf.Max(1, worldSpace ? Mathf.RoundToInt(bounds.rect.width) : Screen.width);
            int height = Mathf.Max(1, worldSpace ? Mathf.RoundToInt(bounds.rect.height) : Screen.height);
            var target = RenderTexture.GetTemporary(width, height, 24);
            capturing = true;
            try
            {
                camera.targetTexture = target; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = UiFactory.Paper;
                if (worldSpace)
                {
                    camera.transform.rotation = bounds.rotation;
                    camera.transform.position = bounds.TransformPoint(bounds.rect.center) - bounds.forward * 10f;
                    camera.orthographic = true;
                    camera.orthographicSize = bounds.TransformVector(Vector3.up * bounds.rect.height * .5f).magnitude;
                    camera.aspect = (float)width / height;
                }
                else { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.planeDistance = 1; }
                canvas.worldCamera = camera;
                Canvas.ForceUpdateCanvases(); camera.Render();
            }
            finally
            {
                canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = previousDistance;
                camera.targetTexture = null; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(go);
                Canvas.ForceUpdateCanvases(); capturing = false;
            }
        }
    }
}
