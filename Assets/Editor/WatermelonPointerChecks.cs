using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.Watermelon;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Real UGUI geometry and production pointer handlers. These mouse events and
    // synthetic secondary IDs do not replace Android native Ended/Canceled checks.
    [InitializeOnLoad]
    public static class WatermelonPointerChecks
    {
        private const string Pending = "Xanadu.WatermelonPointerChecks.Pending";
        private const string EdgeRule = "左右越界仍然可以投放，上下越界不可投放";
        private static readonly StringBuilder Report = new StringBuilder();
        private static int passed, failed;
        private static string output;

        static WatermelonPointerChecks() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            SessionState.SetBool(Pending, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            Execute();
        }
        private static void Execute()
        {
            output = Path.GetFullPath("artifacts/validation/watermelon-pointer");
            Directory.CreateDirectory(output); Report.Clear(); passed = failed = 0;
            Font previous = UiFactory.Font;
            try
            {
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Case("inside tap drops exactly once", f => {
                    var p = f.Down(.5f, .5f); f.Up(p, .5f, .5f); f.Up(p, .5f, .5f);
                    Require(f.Drops.Count == 1 && Near(f.Drops[0], .5f), "one center drop");
                });
                foreach (float x in new[] { -.02f, -.18f, 1.02f, 1.18f })
                    Case("horizontal release " + x, f => {
                        var p = f.Down(.5f, .5f); f.Drag(p, x, .5f); f.Up(p, x, .5f);
                        Require(f.Drops.Count == 1, "normal horizontal outside release must drop once");
                        Require(Near(f.Drops[0], Mathf.Clamp01(x)) && Near(f.LastAim, f.Drops[0]), "aim and release clamp to the same edge");
                    });
                foreach (var point in new[] { new Vector2(.5f, -.02f), new Vector2(.5f, 1.02f), new Vector2(-.1f, -.1f), new Vector2(1.1f, 1.1f) })
                    Case("vertical release " + point, f => {
                        var p = f.Down(.5f, .5f); f.Drag(p, point.x, point.y); f.Up(p, point.x, point.y);
                        Require(f.Drops.Count == 0, "vertical or corner release must not drop");
                    });
                Case("return from vertical excursion", f => {
                    var p = f.Down(.5f, .5f); f.Drag(p, .5f, -.1f); f.Up(p, .7f, .5f);
                    Require(f.Drops.Count == 1 && Near(f.Drops[0], .7f), "returning to valid y preserves the original gesture");
                });
                Case("outside start cannot slide in", f => {
                    var p = f.Down(-.1f, .5f); f.Drag(p, .5f, .5f); f.Up(p, .5f, .5f);
                    Require(f.Drops.Count == 0, "outside raycast cannot capture the board");
                });
                Case("cooldown start is not queued", f => {
                    f.Allowed = false; var p = f.Down(.5f, .5f); f.Allowed = true; f.Up(p, .5f, .5f);
                    Require(f.Drops.Count == 0, "rejected down must not become a later drop");
                });
                Case("secondary pointer cannot take ownership", f => {
                    var first = f.Down(.3f, .5f); var second = f.Down(.8f, .5f, -2);
                    f.Drag(second, .9f, .5f); f.Up(second, .9f, .5f);
                    Require(Near(f.LastAim, .3f) && f.Drops.Count == 0, "secondary pointer is ignored");
                    f.Up(first, .3f, .5f); f.Up(second, .8f, .5f);
                    Require(f.Drops.Count == 1 && Near(f.Drops[0], .3f), "only original pointer can drop");
                });
                Case("cancel and disable invalidate late up", f => {
                    var p = f.Down(.5f, .5f); ExecuteEvents.Execute(f.Board.gameObject, new BaseEventData(f.Events), ExecuteEvents.cancelHandler);
                    f.Up(p, .5f, .5f); p = f.Down(.5f, .5f); f.Board.gameObject.SetActive(false); f.Board.gameObject.SetActive(true); f.Up(p, .5f, .5f);
                    Require(f.Drops.Count == 0, "canceled or disabled gesture cannot resume");
                });
                Case("permission revoked while dragging", f => {
                    var p = f.Down(.5f, .5f); f.Allowed = false; f.Drag(p, .6f, .5f); f.Allowed = true; f.Up(p, .6f, .5f);
                    Require(f.Drops.Count == 0, "input gate loss cancels the gesture permanently");
                });
                Case("missing native touch cannot release", f => {
                    var p = f.Down(.5f, .5f, 12345); f.Up(p, .5f, .5f);
                    Require(f.Drops.Count == 0, "missing native finger must not drop");
                });
                Case("invalid coordinates cancel", f => {
                    var p = f.Down(.5f, .5f); p.position = new Vector2(float.NaN, 100);
                    ExecuteEvents.Execute(f.Board.gameObject, p, ExecuteEvents.dragHandler); f.Up(p, .5f, .5f);
                    Require(f.AllAimsFinite && f.Drops.Count == 0, "invalid coordinates must not aim or survive until up");
                });
                Case("failed plane conversion cancels", f => {
                    var p = f.Down(.5f, .5f); int aims = f.Aims;
                    var position = f.Board.localPosition;
                    // A 90-degree rotation can leave a tiny nonzero ray/normal dot
                    // product. A plane behind the camera deterministically rejects the ray.
                    f.Board.localPosition = position + Vector3.back * 20;
                    Vector2 ignored;
                    Require(!RectTransformUtility.ScreenPointToLocalPointInRectangle(f.Board, p.position, p.pressEventCamera, out ignored),
                        "fixture must produce an actual failed coordinate conversion");
                    ExecuteEvents.Execute(f.Board.gameObject, p, ExecuteEvents.dragHandler);
                    f.Board.localPosition = position; f.Up(p, .5f, .5f);
                    Require(f.Aims == aims && f.Drops.Count == 0, "failed conversion cannot use a default or stale point");
                });
                Case("zero width cancels", f => {
                    var p = f.Down(.5f, .5f); var size = f.Board.sizeDelta; f.Board.sizeDelta = new Vector2(0, size.y);
                    ExecuteEvents.Execute(f.Board.gameObject, p, ExecuteEvents.dragHandler); f.Board.sizeDelta = size; f.Up(p, .5f, .5f);
                    Require(f.Drops.Count == 0, "invalid board dimensions invalidate the gesture");
                });
                Case("help has bold exact rule and fits", f => f.CheckHelp());
            }
            catch (Exception error) { failed++; Report.AppendLine(error.ToString()); }
            finally { UiFactory.Font = previous; }
            Report.AppendLine("Coverage: production UGUI handlers/geometry and ShowHelp; native Android touch phases and host lifecycle require device regression.");
            Report.AppendLine((failed == 0 ? "WATERMELON_POINTER_CHECKS_PASS: " : "WATERMELON_POINTER_CHECKS_FAIL: ") + passed + " passed, " + failed + " failed");
            File.WriteAllText(Path.Combine(output, "report.txt"), Report.ToString()); Debug.Log(Report.ToString());
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }
        private static void Case(string name, Action<Fixture> check)
        {
            try { using (var f = new Fixture()) check(f); passed++; Report.AppendLine("PASS " + name); }
            catch (Exception error) { failed++; Report.AppendLine("FAIL " + name + ": " + error); }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static bool Near(float a, float b) { return Mathf.Abs(a - b) < .001f; }
        private static void Set(object value, string name, object data) { value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data); }

        private sealed class Fixture : IDisposable
        {
            internal readonly RectTransform Board;
            internal readonly EventSystem Events;
            internal readonly List<float> Drops = new List<float>();
            internal bool Allowed = true, AllAimsFinite = true;
            internal float LastAim;
            internal int Aims;
            private readonly GameObject owner;
            private readonly Canvas canvas;
            private readonly Camera camera;
            private readonly RenderTexture target;
            internal Fixture()
            {
                owner = new GameObject("Watermelon pointer fixture");
                Events = new GameObject("Fixture events", typeof(EventSystem)).GetComponent<EventSystem>(); Events.transform.SetParent(owner.transform);
                camera = new GameObject("Fixture camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(owner.transform);
                camera.enabled = false; camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
                camera.orthographicSize = 660; camera.aspect = 720f / 1320; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UiFactory.Paper;
                target = new RenderTexture(720, 1320, 24); target.Create(); camera.targetTexture = target;
                canvas = new GameObject("Fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
                canvas.transform.SetParent(owner.transform); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                var root = (RectTransform)canvas.transform; root.sizeDelta = new Vector2(720, 1320); root.localScale = Vector3.one;
                Board = UiFactory.Rect(root, "Board", 100, 300, 520, 600);
                var surface = Board.gameObject.AddComponent<Image>(); surface.color = Color.clear; surface.raycastTarget = true;
                var pointer = Board.gameObject.AddComponent<WatermelonPointer>(); pointer.CanAim = () => Allowed;
                pointer.Aim = x => { Aims++; LastAim = x; AllAimsFinite &= !float.IsNaN(x) && !float.IsInfinity(x); };
                pointer.Drop = x => Drops.Add(x);
                Flush();
            }
            private void Flush() { Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); }
            private Vector2 Point(float x, float y) { return RectTransformUtility.WorldToScreenPoint(camera, Board.TransformPoint(new Vector3(Board.rect.xMin + x * Board.rect.width, Board.rect.yMax - y * Board.rect.height, 0))); }
            internal PointerEventData Down(float x, float y, int id = -1)
            {
                Flush(); var data = new PointerEventData(Events) { pointerId = id, button = PointerEventData.InputButton.Left, position = Point(x, y) };
                var hits = new List<RaycastResult>(); Events.RaycastAll(data, hits);
                if (hits.Count != 0)
                {
                    data.pointerCurrentRaycast = data.pointerPressRaycast = hits[0];
                    data.pointerPress = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
                }
                return data;
            }
            internal void Drag(PointerEventData data, float x, float y) { data.position = Point(x, y); if (data.pointerPress != null) ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.dragHandler); }
            internal void Up(PointerEventData data, float x, float y) { data.position = Point(x, y); if (data.pointerPress != null) ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler); }
            internal void CheckHelp()
            {
                Require(UiFactory.Font != null, "bundled CJK font is available"); Board.gameObject.SetActive(false);
                foreach (int theme in new[] { 0, 2 })
                {
                    var inert = new GameObject("Inert help page"); inert.SetActive(false); inert.transform.SetParent(owner.transform);
                    var page = inert.AddComponent<WatermelonPage>();
                    var panel = UiFactory.Rect(canvas.transform, "Help panel", 62, 225, 596, 870);
                    Set(page, "visible", true); Set(page, "acceptsInput", (Func<bool>)(() => true)); Set(page, "data", new WatermelonSaveData { Theme = theme });
                    Set(page, "artwork", new WatermelonArtwork()); Set(page, "closeModal", (Action)(() => { }));
                    Set(page, "openModal", (Func<string, float, Action, RectTransform>)((title, height, close) => panel));
                    // Initialize is deliberately not called: Save sees null store/game and never opens a player file.
                    typeof(WatermelonPage).GetMethod("ShowHelp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(page, null);
                    Flush(); Capture("help-theme-" + theme + ".png");
                    var rule = panel.GetComponentsInChildren<Text>().SingleOrDefault(t => t.text == EdgeRule);
                    Require(rule != null && rule.fontStyle == FontStyle.Bold, "help must show the exact rule in real bold text");
                    Require(rule.preferredWidth <= rule.rectTransform.rect.width + 1, "bold rule fits its line");
                    foreach (var text in panel.GetComponentsInChildren<Text>())
                        Require(text.preferredHeight <= text.rectTransform.rect.height + 1, "help text must fit: " + text.text);
                    var ruleBounds = Bounds(panel, rule.rectTransform);
                    foreach (Transform child in panel)
                    {
                        if (child == rule.transform) continue;
                        Require(!ruleBounds.Overlaps(Bounds(panel, (RectTransform)child)), "bold rule cannot overlap " + child.name);
                    }
                    Require(panel.rect.Contains(ruleBounds.min) && panel.rect.Contains(ruleBounds.max), "rule is inside the modal");
                    Object.DestroyImmediate(inert); Object.DestroyImmediate(panel.gameObject);
                }
            }
            private static Rect Bounds(RectTransform parent, RectTransform child)
            {
                var corners = new Vector3[4]; child.GetWorldCorners(corners);
                var min = (Vector2)parent.InverseTransformPoint(corners[0]); var max = min;
                foreach (var corner in corners) { var local = (Vector2)parent.InverseTransformPoint(corner); min = Vector2.Min(min, local); max = Vector2.Max(max, local); }
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
            private void Capture(string name)
            {
                var previous = RenderTexture.active; RenderTexture.active = target; var image = new Texture2D(720, 1320, TextureFormat.RGB24, false);
                try { image.ReadPixels(new Rect(0, 0, 720, 1320), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); }
                finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
            }
            public void Dispose() { camera.targetTexture = null; Object.DestroyImmediate(owner); target.Release(); Object.DestroyImmediate(target); }
        }
    }
}
