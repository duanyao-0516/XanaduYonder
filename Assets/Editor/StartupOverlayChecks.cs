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
using XanaduYonder.Runtime;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // The real GameView framebuffer, root Overlay canvases, production creation order,
    // and actual frame-end scheduling. Camera.Render cannot validate this rendering path.
    [InitializeOnLoad]
    public static class StartupOverlayChecks
    {
        private const string Pending = "Xanadu.StartupOverlayChecks.Pending";
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly StringBuilder report = new StringBuilder();
        private static string output;
        private static double deadline;
        private static int checks;
        private static bool finished;
        private static IEnumerator scenario;
        private static Font previousFont;

        static StartupOverlayChecks() { EditorApplication.playModeStateChanged += StateChanged; }

        public static void Run()
        {
            if (Application.isBatchMode) throw new InvalidOperationException("Real Overlay capture requires a visible, non-batch GameView.");
            SessionState.SetBool(Pending, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/validation/startup-overlay"));
            Directory.CreateDirectory(output);
            report.Clear(); checks = 0; finished = false;
            previousFont = UiFactory.Font;
            deadline = EditorApplication.timeSinceStartup + 15;
            EditorApplication.update += Watchdog;
            new GameObject("Startup Overlay check runner").AddComponent<Runner>().StartCoroutine(Execute());
        }

        private sealed class Runner : MonoBehaviour { }
        private static void Watchdog()
        {
            if (!finished && EditorApplication.timeSinceStartup > deadline)
                Finish(new TimeoutException("Overlay check exceeded 15 seconds; confirm the GameView is visible and rendering."));
        }
        private static IEnumerator Execute()
        {
            scenario = CheckOverlay();
            while (!finished)
            {
                Exception failure = null; bool more = false; object next = null;
                try { more = scenario.MoveNext(); if (more) next = scenario.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null || !more) { Finish(failure); yield break; }
                yield return next;
            }
        }
        private static void Finish(Exception failure)
        {
            if (finished) return;
            finished = true; EditorApplication.update -= Watchdog;
            try { (scenario as IDisposable)?.Dispose(); }
            catch (Exception error) { if (failure == null) failure = error; }
            UiFactory.Font = previousFont;
            report.AppendLine(failure == null ? "STARTUP_OVERLAY_CHECKS_PASS: " + checks + " assertions" :
                "STARTUP_OVERLAY_CHECKS_FAIL after " + checks + " assertions\n" + failure);
            File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
            Debug.Log(report.ToString());
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static IEnumerator CheckOverlay()
        {
            using (var size = new GameViewSizeScope())
            {
                size.Set(720, 1582);
                while (Screen.width != 720 || Screen.height != 1582) yield return null;
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Require(UiFactory.Font != null, "Bundled font loads");
                using (var f = new Fixture())
                {
                    var transition = (IEnumerator)Invoke(f.Splash, "Start");
                    try
                    {
                        // Production Start reapplies ordering before its first rendered frame.
                        Require(transition.MoveNext() && transition.Current is WaitForEndOfFrame,
                            "Production startup configures ordering before the first frame-end wait");
                        yield return null;
                        yield return transition.Current;
                        f.CaptureAndCheck("waiting-first-frame", true);
                        yield return new WaitForSecondsRealtime(.25f);
                        yield return new WaitForEndOfFrame();
                        f.CaptureAndCheck("waiting-steady", true);

                        Set(f.Host, "startupComplete", true);
                        // Run the real 1.75-second hold and frame boundaries, without substitutions.
                        while (transition.MoveNext()) yield return transition.Current;
                    }
                    finally { (transition as IDisposable)?.Dispose(); }
                    Require(f.Overlay == null || !f.Overlay.activeSelf, "Production handoff removes the visible overlay");
                    yield return new WaitForEndOfFrame();
                    f.CaptureAndCheck("lobby-after-handoff", false);
                    Require(Get<object>(f.Host, "store") == null, "Fixture never opens a player save store");
                }
            }
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly AppController Host;
            internal readonly BrandSplash Splash;
            internal readonly GameObject Overlay;
            private readonly GameObject owner;
            private readonly Canvas splashCanvas, lobbyCanvas;
            private readonly EventSystem events;
            private readonly ScrollRect scroll;

            internal Fixture()
            {
                owner = new GameObject("Real Overlay fixture");
                var background = new GameObject("Background Camera", typeof(Camera)); background.transform.SetParent(owner.transform, false);
                var camera = background.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = UiFactory.Paper; camera.cullingMask = 0; camera.orthographic = true;
                var inert = new GameObject("Inert production host"); inert.SetActive(false); inert.transform.SetParent(owner.transform, false);
                Host = inert.AddComponent<AppController>();

                // BrandSplash's -10000 execution order constructs its canvas before AppController.
                Splash = inert.AddComponent<BrandSplash>();
                Splash.Logo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Branding/XY_logo.png");
                Require(Splash.Logo != null, "Original splash artwork loads");
                Invoke(Splash, "Awake");
                Overlay = Get<GameObject>(Splash, "overlay");
                // Map the inert host to its active fixture equivalent only when production
                // parents the overlay. A production scene-root canvas must remain at root.
                if (Overlay.transform.parent == inert.transform) Overlay.transform.SetParent(owner.transform, false);
                else Require(Overlay.transform.parent == null, "Production splash is a scene-root overlay");
                splashCanvas = Overlay.GetComponent<Canvas>();

                Invoke(Host, "BuildCanvas");
                var root = Get<RectTransform>(Host, "canvasRoot"); root.SetParent(owner.transform, false);
                lobbyCanvas = root.GetComponent<Canvas>();
                events = inert.GetComponentInChildren<EventSystem>(true);
                Require(events != null, "Production BuildCanvas creates its EventSystem");
                events.transform.SetParent(owner.transform, false);
                Invoke(Host, "BuildLobby");
                scroll = Get<GameObject>(Host, "lobbyPage").GetComponentInChildren<ScrollRect>();
                Require(splashCanvas.renderMode == RenderMode.ScreenSpaceOverlay && lobbyCanvas.renderMode == RenderMode.ScreenSpaceOverlay,
                    "Both production canvases retain ScreenSpaceOverlay rendering");
                Require(Overlay.transform.parent == null || Overlay.transform.GetSiblingIndex() < root.GetSiblingIndex(),
                    "Production splash hierarchy is preserved after creating it before the lobby");
                Canvas.ForceUpdateCanvases();
            }

            internal void CaptureAndCheck(string name, bool covered)
            {
                Require(!Application.isBatchMode, "Framebuffer check runs outside editor batch mode");
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    Require(image != null && image.width == 720 && image.height == 1582, "Real GameView framebuffer has the selected resolution");
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                    var pixels = image.GetPixels32();
                    Vector2[] samples = { new Vector2(.08f, .1f), new Vector2(.92f, .1f), new Vector2(.08f, .9f), new Vector2(.92f, .9f) };
                    int white = 0, total = 0; var hits = new List<RaycastResult>();
                    foreach (var sample in samples)
                    {
                        int x = Mathf.RoundToInt(sample.x * image.width), y = Mathf.RoundToInt(sample.y * image.height);
                        for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++)
                        {
                            Color32 pixel = pixels[(y + dy) * image.width + x + dx];
                            if (pixel.r >= 250 && pixel.g >= 250 && pixel.b >= 250) white++;
                            total++;
                        }
                        var pointer = new PointerEventData(events) { position = new Vector2(x, y), button = PointerEventData.InputButton.Left };
                        hits.Clear(); events.RaycastAll(pointer, hits);
                        report.AppendLine(name + " frame=" + Time.frameCount + " t=" + Time.realtimeSinceStartup.ToString("F3") +
                            " point=" + pointer.position + " firstHit=" + (hits.Count > 0 ? hits[0].gameObject.name : "none"));
                        if (covered) Require(hits.Count > 0 && hits[0].gameObject.name == "White background" &&
                            hits[0].gameObject.transform.IsChildOf(Overlay.transform), "White pixels and splash raycast blocker agree at " + sample);
                    }
                    report.AppendLine(name + " white=" + white + "/" + total + " lobby order=" + lobbyCanvas.sortingOrder +
                        " render=" + lobbyCanvas.renderOrder + (splashCanvas != null ? " splash order=" + splashCanvas.sortingOrder + " render=" + splashCanvas.renderOrder : " splash destroyed"));
                    Require(covered ? white >= total * .98f : white < total * .5f,
                        covered ? "Opaque splash paints white over all peripheral lobby regions" : "Handoff exposes the non-white lobby framebuffer");
                    if (covered)
                    {
                        int ink = 0;
                        int left = image.width / 4, bottom = image.height / 2 - image.width / 4;
                        for (int y = bottom; y < bottom + image.width / 2; y += 3)
                        for (int x = left; x < left + image.width / 2; x += 3)
                        {
                            var pixel = pixels[y * image.width + x];
                            if (pixel.r < 180 || pixel.g < 180 || pixel.b < 180) ink++;
                        }
                        Require(ink > 20, "The central splash artwork is visible, not just an empty white framebuffer");
                    }
                    else
                    {
                        var rect = scroll.viewport.rect;
                        var point = RectTransformUtility.WorldToScreenPoint(null, scroll.viewport.TransformPoint(new Vector2(rect.xMin + 8, rect.center.y)));
                        hits.Clear(); events.RaycastAll(new PointerEventData(events) { position = point }, hits);
                        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject) == scroll.gameObject,
                            "First raycast after the visually verified handoff reaches the production ScrollRect");
                    }
                }
                finally { if (image != null) Object.Destroy(image); }
            }
            public void Dispose()
            {
                if (Overlay != null) Object.DestroyImmediate(Overlay);
                Object.DestroyImmediate(owner);
            }
        }

        // Same fixed-resolution mechanism as GoldMinerUiChecks, without switching canvas render mode.
        private sealed class GameViewSizeScope : IDisposable
        {
            private readonly EditorWindow window;
            private readonly object group;
            private readonly Type groupType;
            private readonly PropertyInfo selected;
            private readonly int oldIndex;
            private int added = -1;
            internal GameViewSizeScope()
            {
                var assembly = typeof(Editor).Assembly; var viewType = assembly.GetType("UnityEditor.GameView", true);
                window = EditorWindow.GetWindow(viewType); window.Show(); window.Focus();
                selected = viewType.GetProperty("selectedSizeIndex", Instance); oldIndex = (int)selected.GetValue(window, null);
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
                var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
                var enumType = assembly.GetType("UnityEditor.GameViewSizeGroupType", true);
                string target = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget).ToString();
                group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(enumType, Enum.IsDefined(enumType, target) ? target : "Standalone") });
                groupType = group.GetType();
            }
            internal void Set(int width, int height)
            {
                var assembly = typeof(Editor).Assembly;
                var value = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize", true), Instance, null,
                    new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType", true), "FixedResolution"), (object)width, height, "Startup Overlay check" }, null);
                added = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { value });
                selected.SetValue(window, added, null); window.Focus(); window.Repaint();
            }
            public void Dispose()
            {
                selected.SetValue(window, oldIndex, null);
                if (added >= 0) groupType.GetMethod("RemoveCustomSize").Invoke(group, new object[] { added });
                window.Repaint();
            }
        }
        private static object Invoke(object target, string name) { return target.GetType().GetMethod(name, Instance).Invoke(target, null); }
        private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, Instance).GetValue(target); }
        private static void Set(object target, string name, object value) { target.GetType().GetField(name, Instance).SetValue(target, value); }
        private static void Require(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
    }
}
