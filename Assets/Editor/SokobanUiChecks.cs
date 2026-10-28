using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core;
using XanaduYonder.Core.Sokoban;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.Sokoban;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Real production UGUI, isolated profiles and real rendered raycasts. No player files are opened.
    [InitializeOnLoad]
    public static class SokobanUiChecks
    {
        private const string Pending = "Xanadu.SokobanUiChecks.Pending";
        private static readonly StringBuilder report = new StringBuilder();
        private static int checks;
        private static string output;
        static SokobanUiChecks() { EditorApplication.playModeStateChanged += StateChanged; }
        public static void Run()
        {
            SessionState.SetBool(Pending, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static void StateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            new GameObject("Sokoban checks runner").AddComponent<Runner>().StartCoroutine(Execute());
        }
        private sealed class Runner : MonoBehaviour { }
        private static IEnumerator Execute()
        {
            yield return null;
            checks = 0; report.Clear();
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/validation/sokoban-ui"));
            Directory.CreateDirectory(output);
            report.AppendLine("Unity " + Application.unityVersion + "; production UGUI; Camera.Render; in-memory profiles");
            var run = Scenarios();
            while (true)
            {
                bool next = false; object current = null; Exception failure = null;
                try { next = run.MoveNext(); if (next) current = run.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null || !next)
                {
                    (run as IDisposable)?.Dispose();
                    if (failure == null) report.AppendLine("SOKOBAN_UI_CHECKS_PASS: " + checks + " assertions");
                    else report.AppendLine("SOKOBAN_UI_CHECKS_FAIL after " + checks + " assertions\n" + failure);
                    File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
                    Debug.Log(report.ToString()); EditorApplication.Exit(failure == null ? 0 : 1); yield break;
                }
                yield return current;
            }
        }
        private static IEnumerator Scenarios()
        {
            UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            Require(UiFactory.Font != null, "Bundled CJK font loads");
            var catalogAsset = Resources.Load<TextAsset>("Sokoban/campaign");
            Require(catalogAsset != null, "Campaign resource loads");
            var catalog = SokobanCatalog.Parse(catalogAsset.text);
            Require(catalog.Count == 108, "All 108 production levels load");
            foreach (int height in new[] { 1455, 1320, 1582, 1150 })
            using (var f = new Fixture(height))
            {
                var profile = new SokobanProfile(catalog);
                bool allowed = true; Action finishSettings = null; int home = 0;
                var page = f.Root.gameObject.AddComponent<SokobanPage>();
                page.Initialize(f.Root, () => home++, done => { allowed = false; finishSettings = done; },
                    () => allowed, () => false, () => false, f.Root.gameObject.AddComponent<GameFeedback>(), _ => { },
                    initialCatalog: catalog, initialProfile: profile);
                page.Enter(); page.StartLevel("tutorial-001", true); page.LayoutNow();
                yield return null;
                CheckGeometry(f, page, height);
                f.Capture("compact");
                foreach (string name in new[] { "Home", "Book", "Settings", "Restart", "Fit", "Zoom in", "Move Up", "Move Down", "Move Left", "Move Right" })
                    f.CheckHit(f.Find(name));
                Require(f.Root.GetComponentInChildren<Text>().font == UiFactory.Font, "Shared font is used");
                f.Click(f.Find("Move Up"));
                yield return new WaitForSeconds(.15f);
                Require(page.Session.Game.Moves == 1 && page.Session.Game.Pushes == 1 && page.Session.Game.GoalsFilled == 1, "Dpad rendered event commits one push");
                f.Click(f.Find("Undo"));
                Require(page.Session.Game.Moves == 0 && page.Session.Game.Pushes == 0, "Undo restores counters");
                page.ShowRules(); page.Move(Direction.Up);
                Require(page.Session.Game.Moves == 0, "Help modal blocks movement");
                f.Capture("help"); page.Back();
                f.Click(f.Find("Settings")); page.Move(Direction.Up);
                Require(finishSettings != null && page.Session.Game.Moves == 0, "Settings callback and host gate block movement");
                allowed = true; finishSettings();
                page.Move(Direction.Up); yield return new WaitForSeconds(.15f);
                Require(page.Session.Game.Moves == 1, "Settings completion resumes input");
                page.SetForeground(false); page.Move(Direction.Down);
                Require(page.Session.Game.Moves == 1, "Background blocks input");
                page.SetForeground(true); page.Undo();
                for (int z = 0; z < 4; z++) page.Zoom(1);
                Require(Math.Abs(page.Viewport.Zoom - 2) < .001 && page.Session.Game.Moves == 0, "200% zoom preserves game");
                page.Viewport.Pan(100000, -100000); page.LayoutNow();
                Require(page.Viewport.OffsetX <= .01 && page.Viewport.OffsetX + page.Viewport.Columns * page.Viewport.CellSize >= page.Viewport.Width - .01, "Pan clamps horizontal map edges");
                f.Capture("zoomed"); page.ResetZoom();
                Require(page.Viewport.Zoom == 1 && Math.Abs(page.Viewport.CenterX - .5) < .001, "Fit resets center");
                page.SetHostSaveWarning("设置尚未保存，请检查设备空间。"); page.LayoutNow();
                Require(Array.Exists(f.Root.GetComponentsInChildren<Text>(), t => t.text.Contains("设置尚未保存")), "Shared save failure remains visible");
                f.Capture("save-warning"); page.SetHostSaveWarning(null); page.LayoutNow();
                string solution = catalog.Get("tutorial-001").Solution;
                for (int step = 0; step < solution.Length; step++)
                {
                    page.Move(SokobanGame.FromChar(solution[step]));
                    if (step == solution.Length - 1)
                    {
                        if (height == 1455) page.Zoom(1);
                        else if (height == 1320) { page.ShowRules(); page.Back(); }
                        else page.LayoutNow();
                    }
                    yield return new WaitForSeconds(.15f);
                }
                Require(page.Session.Game.Won && page.CompletedCount == 1, "Page solves original compact map and records one completion");
                Require(page.HasDialog, "Cancelling the final animation never suppresses the completion dialog");
                f.Capture("completed");
                page.Save(); page.Save(); Require(page.CompletedCount == 1, "Repeated save cannot duplicate completion");
                page.StartLevel("tutorial-008", true); page.LayoutNow();
                yield return null; f.Capture("tall");
                Require(page.Session.Game.Level.Height == 13 && page.Viewport.Zoom == 1, "Tall reference map fits at 100%");
                Require(page.Viewport.Columns * page.Viewport.CellSize <= page.Viewport.Width + .01 && page.Viewport.Rows * page.Viewport.CellSize <= page.Viewport.Height + .01, "Tall map is fully visible");
                page.ShowLevels(); yield return null; f.Capture("levels");
                page.StartLevel("tutorial-001", true); page.LayoutNow();
                for (int i = 0; i < 25000; i++) { page.Session.Move(Direction.Left); page.Session.Move(Direction.Right); }
                Invoke(page, "Refresh"); page.LayoutNow();
                Require(page.Session.Game.Moves == 50000, "Counter fixture reaches action limit");
                Rect levelBounds = f.Bounds(f.Find("Sokoban level"));
                Rect moveBounds = f.Bounds(f.Find("Sokoban moves"));
                Rect pushBounds = f.Bounds(f.Find("Sokoban pushes"));
                Rect goalBounds = f.Bounds(f.Find("Sokoban goals"));
                Require(levelBounds.width > 20 && levelBounds.xMax < moveBounds.xMin && moveBounds.xMax < pushBounds.xMin && pushBounds.xMax < goalBounds.xMin, "50,000-step information stays on one line without overlap");
                Near(moveBounds.center.y, pushBounds.center.y, "Counters share one baseline");
                f.Capture("maximum-count");
                page.Leave(); page.Enter(); page.LayoutNow();
                Require(page.Session.Game.Moves == 50000, "Leave and reentry keep complete action history");
                f.Click(f.Find("Home")); Require(home == 1, "Home invokes lobby callback");
                report.AppendLine("PASS 720x" + height + ": reference geometry, controls/raycast, undo, help/settings, lifecycle, zoom, records, tall map, max counter");
            }
            using (var f = new Fixture(1455))
            {
                var inert = new GameObject("Inactive isolated host"); inert.SetActive(false);
                try
                {
                    var host = inert.AddComponent<AppController>();
                    Set(host, "canvasRoot", (RectTransform)f.Root.parent);
                    Set(host, "data", new SaveData());
                    Set(host, "activePage", Enum.Parse(typeof(AppController).GetNestedType("Page", BindingFlags.NonPublic), "Sokoban"));
                    var status = UiFactory.Label(f.Root.parent, "", 0, 0, 1, 1, 10, UiFactory.Muted); status.gameObject.SetActive(false); Set(host, "status", status);
                    var page = f.Root.gameObject.AddComponent<SokobanPage>(); Set(host, "sokobanPage", page);
                    var feedback = inert.AddComponent<GameFeedback>(); Set(host, "feedback", feedback);
                    page.Initialize(f.Root, () => { }, after => Invoke(host, "OpenSettings", after),
                        () => Get<GameObject>(host, "modal") == null && !Get<bool>(host, "paused") && Get<bool>(host, "focused"),
                        () => false, () => false, feedback, text => Invoke(host, "SetStatus", text), initialCatalog: catalog, initialProfile: new SokobanProfile(catalog));
                    page.Enter(); page.LayoutNow(); yield return null;
                    f.Click(f.Find("Settings"));
                    Require(Get<GameObject>(host, "modal") != null && page.Session.Blocked, "Actual shared host settings blocks Sokoban");
                    f.Capture("host-settings");
                    Invoke(host, "OnApplicationPause", true);
                    Require(Get<GameObject>(host, "modal") == null && !page.Session.Foreground, "Background closes host modal and suspends session");
                    Invoke(host, "OnApplicationPause", false);
                    page.Move(Direction.Up); yield return new WaitForSeconds(.15f);
                    Require(page.Session.Game.Moves == 1 && !page.Session.Blocked, "Resume from settings has no stale blocked state");
                    Set(host, "saveFailed", true); Set(host, "saveWarning", "公共设置尚未保存"); page.Save();
                    Require(Array.Exists(f.Root.GetComponentsInChildren<Text>(), t => t.text.Contains("公共设置尚未保存")), "Real host save warning reaches compact game status");
                    Set(host, "saveFailed", false); Set(host, "saveWarning", null); page.Save();
                    Require(!Array.Exists(f.Root.GetComponentsInChildren<Text>(), t => t.text.Contains("公共设置尚未保存")), "Shared save recovery removes compact warning");
                    f.Click(f.Find("Settings")); Get<Action>(host, "dismissModal")();
                    Require(Get<GameObject>(host, "modal") == null && !page.Session.Blocked, "Host Android-back settings callback resumes the page");
                    page.Leave(); Invoke(host, "SetStatus", "Other page status"); page.Save();
                    Require(status.text == "Other page status", "Inactive page save preserves current host status");
                    report.AppendLine("PASS actual AppController: shared settings, pause/resume, warning/recovery, back callback, inactive-save ownership");
                }
                finally { Object.DestroyImmediate(inert); }
            }
            using (var f = new Fixture(1150))
            {
                var inert = new GameObject("Short-screen host construction"); inert.SetActive(false);
                try
                {
                    var host = inert.AddComponent<AppController>();
                    var safe = f.Root.gameObject.AddComponent<SafeDesignFrame>(); safe.Initialize(f.Root, (RectTransform)f.Root.parent);
                    Require(f.Root.rect.width > 800, "Short-screen baseline uses a wider minimum-height design frame");
                    Set(host, "frame", f.Root); Set(host, "canvasRoot", (RectTransform)f.Root.parent); Set(host, "data", new SaveData());
                    Set(host, "feedback", inert.AddComponent<GameFeedback>());
                    Invoke(host, "BuildSokoban", catalog, new SokobanProfile(catalog), null);
                    Require(!safe.WidthOnly, "Constructing Sokoban preserves the lobby frame policy");
                    safe.WidthOnly = true;
                    Set(host, "activePage", Enum.Parse(typeof(AppController).GetNestedType("Page", BindingFlags.NonPublic), "Sokoban"));
                    var page = Get<SokobanPage>(host, "sokobanPage"); page.Enter(); page.LayoutNow(); yield return null;
                    Near(f.Bounds(f.Find("Move Right")).xMax, 368.5f * 720 / 388, "Short-screen host builds controls in the final 720-wide frame");
                    Require(f.Bounds(f.Find("Settings")).xMax <= 720 && f.Bounds(f.Find("Move Right")).xMax <= 720, "Actual host construction keeps right controls inside screen");
                    f.CheckHit(f.Find("Move Right")); f.Capture("host-short-screen");
                    report.AppendLine("PASS actual short-screen host construction: initial wide frame, final width-only frame, visible/reachable right controls");
                }
                finally { Object.DestroyImmediate(inert); }
            }
        }
        private static void CheckGeometry(Fixture f, SokobanPage page, int height)
        {
            float s = 720f / 388;
            Rect board = f.Bounds(f.Find("Sokoban board viewport"));
            Rect operations = f.Bounds(f.Find("Sokoban operations"));
            Near(board.xMin, 19.5f * s, "Board left edge matches prototype");
            Near(board.width, 349 * s, "Board width matches prototype");
            Near(height - board.yMax, 163.421875f * s, "Board top matches prototype");
            Near(operations.height, 152 * s, "Operations height matches prototype");
            Near(operations.yMin, 0, "Operations align to safe bottom");
            Near(board.yMin - operations.yMax, 16 * s, "Board to controls gap matches prototype");
            Require(!f.Root.name.Contains("focus"), "Single game surface");
            foreach (Text text in f.Root.GetComponentsInChildren<Text>())
                Require(!text.text.Contains("专注") && !text.text.Contains("只能推") && !text.text.Contains("走错了"), "No removed focus/footer UI");
            Require(page.Viewport.CellSize > 0, "Visible board cell size");
        }
        private static void Near(float value, float expected, string reason) { Require(Math.Abs(value - expected) < 1.1f, reason + ": " + value + " vs " + expected); }
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); checks++; }
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static T Get<T>(object value, string field) { return (T)value.GetType().GetField(field, Members).GetValue(value); }
        private static void Set(object value, string field, object data) { value.GetType().GetField(field, Members).SetValue(value, data); }
        private static void Invoke(object value, string method, params object[] args)
        {
            foreach (var candidate in value.GetType().GetMethods(Members))
                if (candidate.Name == method && candidate.GetParameters().Length == args.Length) { candidate.Invoke(value, args); return; }
            throw new MissingMethodException(method);
        }

        private sealed class Fixture : IDisposable
        {
            public readonly RectTransform Root;
            private readonly GameObject owner;
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly EventSystem events;
            private readonly int height;
            public Fixture(int height)
            {
                this.height = height; owner = new GameObject("Sokoban UI fixture");
                var canvas = new GameObject("Fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
                canvas.SetParent(owner.transform, false); canvas.sizeDelta = new Vector2(720, height);
                camera = new GameObject("Fixture camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(owner.transform, false);
                camera.enabled = false; camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
                camera.orthographicSize = height * .5f; camera.aspect = 720f / height; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UiFactory.Paper;
                target = new RenderTexture(720, height, 24); target.Create(); camera.targetTexture = target;
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; canvas.GetComponent<Canvas>().worldCamera = camera;
                Root = UiFactory.Rect(canvas, "Sokoban fixture page", 0, 0, 720, height); UiFactory.Stretch(Root);
                events = new GameObject("Fixture events", typeof(EventSystem)).GetComponent<EventSystem>(); events.transform.SetParent(owner.transform, false);
            }
            public RectTransform Find(string name)
            {
                foreach (RectTransform rect in Root.GetComponentsInChildren<RectTransform>()) if (rect.name == name) return rect;
                throw new InvalidOperationException("Missing visible production element: " + name);
            }
            public void Flush() { Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); }
            public Rect Bounds(RectTransform rect)
            {
                Flush(); var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]); var b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
            private PointerEventData Pointer(RectTransform rect)
            {
                Flush(); var data = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)), pointerId = -1, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
                Require(hits.Count > 0 && (hits[0].gameObject.transform == rect || hits[0].gameObject.transform.IsChildOf(rect)), "Rendered raycast reaches " + rect.name);
                data.pointerCurrentRaycast = data.pointerPressRaycast = hits[0]; return data;
            }
            public void CheckHit(RectTransform rect) { Pointer(rect); }
            public void Click(RectTransform rect)
            {
                var data = Pointer(rect); var hit = data.pointerCurrentRaycast.gameObject;
                var pressed = ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerDownHandler);
                var clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit); data.pointerPress = pressed != null ? pressed : clicked;
                if (data.pointerPress != null) ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
                if (clicked != null && data.pointerPress == clicked) ExecuteEvents.Execute(clicked, data, ExecuteEvents.pointerClickHandler);
            }
            public void Capture(string name)
            {
                Flush(); var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(720, height, TextureFormat.RGB24, false);
                try { image.ReadPixels(new Rect(0, 0, 720, height), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(output, name + "-720x" + height + ".png"), image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }
            public void Dispose() { camera.targetTexture = null; if (RenderTexture.active == target) RenderTexture.active = null; Object.DestroyImmediate(owner); target.Release(); Object.DestroyImmediate(target); }
        }
    }
}
