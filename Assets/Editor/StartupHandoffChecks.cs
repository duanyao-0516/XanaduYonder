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
    // Drives the production splash iterator and production lobby UGUI without invoking
    // AppController.Awake or opening any player store. Only the decorative hold is skipped.
    [InitializeOnLoad]
    public static class StartupHandoffChecks
    {
        private const string Pending = "Xanadu.StartupHandoffChecks.Pending";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly StringBuilder report = new StringBuilder();
        private static int checks;
        private static string output;

        static StartupHandoffChecks() { EditorApplication.playModeStateChanged += StateChanged; }

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
            new GameObject("Startup handoff checks runner").AddComponent<Runner>().StartCoroutine(Execute());
        }

        private sealed class Runner : MonoBehaviour { }

        private static IEnumerator Execute()
        {
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/validation/startup-handoff"));
            Directory.CreateDirectory(output);
            checks = 0; report.Clear();
            Font previousFont = UiFactory.Font;
            Exception failure = null;
            var scenario = CheckHandoff();
            while (failure == null)
            {
                bool more = false;
                object next = null;
                try { more = scenario.MoveNext(); if (more) next = scenario.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null || !more) break;
                yield return next;
            }
            try { (scenario as IDisposable)?.Dispose(); }
            catch (Exception error) { if (failure == null) failure = error; }
            UiFactory.Font = previousFont;
            if (failure == null) report.AppendLine("STARTUP_HANDOFF_CHECKS_PASS: " + checks + " assertions");
            else
            {
                report.AppendLine("STARTUP_HANDOFF_CHECKS_FAIL after " + checks + " assertions\n" + failure);
                Debug.LogException(failure);
            }
            File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
            Debug.Log(report.ToString());
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static IEnumerator CheckHandoff()
        {
            UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            Require(UiFactory.Font != null, "Bundled font is available");
            CheckLifecycleCleanup();
            foreach (int height in new[] { 1320, 1582 })
            using (var f = new Fixture(height))
            {
                // Let the real EventSystem choose its real StandaloneInputModule.
                yield return null;
                Require(f.Events.currentInputModule != null && f.Events.currentInputModule.isActiveAndEnabled,
                    "Real EventSystem has selected an active input module");
                f.Flush();
                Invoke(f.Host, "HandleBack");
                Require(Get<GameObject>(f.Host, "modal") == null,
                    "Back during the startup artwork cannot open an unreachable dialog and block readiness");
                var transition = (IEnumerator)Invoke(f.Splash, "Start");
                try
                {
                    int waitingFrames = 0;
                    while (waitingFrames < 6)
                    {
                        Require(transition.MoveNext(), "Splash cannot finish before lobby initialization completes");
                        Require(f.Overlay != null && f.Overlay.activeInHierarchy,
                            "Unready lobby remains covered by the active splash");
                        f.CheckOpaqueBlocker();
                        // Skip elapsed decoration time, never the readiness predicate or frame boundaries.
                        if (transition.Current is WaitForSecondsRealtime) continue;
                        f.Flush();
                        waitingFrames++;
                        yield return null;
                    }

                    Require(!Ready(f.Host), "An unfinished AppController cannot hand off input");
                    Set(f.Host, "startupComplete", true);
                    Require(Ready(f.Host), "Completed foreground lobby with active input is ready");
                    CheckReadinessConditions(f);

                    // A missing module is different from a selected-but-disabled module.
                    Object.DestroyImmediate(f.Events.currentInputModule);
                    f.Events.UpdateModules();
                    Require(!Ready(f.Host), "Missing input module prevents handoff");
                    var module = f.Events.gameObject.AddComponent<StandaloneInputModule>();
                    module.forceModuleActive = true;
                    yield return null;
                    Require(Ready(f.Host), "Input module restoration makes the rendered lobby ready again");

                    Require(transition.MoveNext() && transition.Current is WaitForEndOfFrame,
                        "Ready lobby must complete a rendered frame before the artwork is removed");
                    Set(f.Host, "focused", false);
                    f.Flush();
                    yield return null;
                    Require(transition.MoveNext() && f.Overlay.activeInHierarchy,
                        "Losing focus during the final rendered frame cancels the pending handoff");
                    f.CheckOpaqueBlocker();
                    Set(f.Host, "focused", true);

                    bool handedOff = false;
                    for (int frame = 0; frame < 12; frame++)
                    {
                        bool more = transition.MoveNext();
                        if (f.Overlay == null || !f.Overlay.activeInHierarchy)
                        {
                            handedOff = true;
                            break;
                        }
                        f.CheckOpaqueBlocker();
                        Require(more, "Splash iterator cannot finish while leaving an active input blocker");
                        if (transition.Current is WaitForSecondsRealtime) { frame--; continue; }
                        f.Flush();
                        yield return null;
                    }
                    Require(handedOff, "Ready lobby is revealed within the next rendered frames");
                    // Check synchronously, before deferred Destroy can hide an ordering mistake.
                    Require(f.Overlay != null && !f.Overlay.activeSelf,
                        "Splash deactivates immediately instead of relying on deferred destruction");
                    Require(!f.Opacity.blocksRaycasts, "Raycast blocking is cleared in the handoff frame");
                    Require(f.Probe.WasDisabled && !f.Probe.BlocksWhenDisabled,
                        "Overlay OnDisable observes raycast blocking already cleared");
                    Require(Mathf.Abs(f.Probe.AlphaWhenDisabled - 1) < .0001f,
                        "Opaque artwork switches directly to the lobby without an alpha fade");
                    f.CheckDragAndClick();
                    f.Capture("handoff-" + height);
                    Invoke(f.Host, "HandleBack");
                    Require(Get<GameObject>(f.Host, "modal") != null,
                        "Back resumes normal lobby exit confirmation immediately after handoff");
                    Invoke(f.Host, "HandleBack");
                    Require(Get<GameObject>(f.Host, "modal") == null && Ready(f.Host),
                        "Back dismisses the exit confirmation and leaves the lobby interactive");
                    Require(Get<object>(f.Host, "store") == null,
                        "Startup fixture never opens the real player store");
                    report.AppendLine("PASS 720x" + height + ": opaque readiness wait, input gates, synchronous unblock, rendered scroll drag and button click");
                }
                finally { (transition as IDisposable)?.Dispose(); }
            }
        }

        private static void CheckLifecycleCleanup()
        {
            foreach (bool disableHost in new[] { false, true })
            {
                string reason = disableHost ? "host deactivation" : "component disable";
                var host = new GameObject("Splash lifecycle " + reason); host.SetActive(false);
                GameObject rootOverlay = null;
                try
                {
                    var splash = host.AddComponent<BrandSplash>();
                    splash.Logo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Branding/XY_logo.png");
                    Require(splash.Logo != null, "Lifecycle fixture has the original splash artwork");
                    // Real activation invokes Awake. No AppController or player store is created.
                    host.SetActive(true);
                    rootOverlay = Get<GameObject>(splash, "overlay");
                    Require(rootOverlay != null && rootOverlay.activeInHierarchy && rootOverlay.transform.parent == null,
                        "Lifecycle fixture starts with an active scene-root overlay");
                    var group = rootOverlay.GetComponent<CanvasGroup>();
                    var probe = rootOverlay.AddComponent<HandoffProbe>(); probe.Group = group;
                    Require(splash.BlocksInput && group.blocksRaycasts, "Active splash owns input before " + reason);
                    if (disableHost) host.SetActive(false); else splash.enabled = false;
                    // No yield: deferred Destroy must not conceal a still-active blocker.
                    Require(!rootOverlay.activeSelf && !group.blocksRaycasts && !group.interactable,
                        reason + " synchronously deactivates and unblocks the independent root overlay");
                    Require(probe.WasDisabled && !probe.BlocksWhenDisabled,
                        reason + " clears raycast blocking before overlay OnDisable");
                    Require(!splash.BlocksInput && Get<GameObject>(splash, "overlay") == null,
                        reason + " clears the owner's overlay reference and input state");
                    Object.DestroyImmediate(splash);
                    Require(!rootOverlay.activeSelf, "OnDestroy after " + reason + " remains idempotent");
                    report.AppendLine("PASS lifecycle: " + reason + " synchronously removes scene-root input blocker");
                }
                finally
                {
                    if (rootOverlay != null) Object.DestroyImmediate(rootOverlay);
                    Object.DestroyImmediate(host);
                }
            }
        }

        private static void CheckReadinessConditions(Fixture f)
        {
            Set(f.Host, "paused", true); Require(!Ready(f.Host), "Paused app cannot hand off"); Set(f.Host, "paused", false);
            Set(f.Host, "focused", false); Require(!Ready(f.Host), "Unfocused app cannot hand off"); Set(f.Host, "focused", true);
            var page = typeof(AppController).GetNestedType("Page", BindingFlags.NonPublic);
            Set(f.Host, "activePage", Enum.Parse(page, "Game2048"));
            Require(!Ready(f.Host), "A non-lobby page cannot satisfy startup handoff");
            Set(f.Host, "activePage", Enum.Parse(page, "Lobby"));
            f.Lobby.SetActive(false); Require(!Ready(f.Host), "Inactive lobby cannot hand off"); f.Lobby.SetActive(true);
            f.Raycaster.enabled = false; Require(!Ready(f.Host), "Disabled canvas raycaster prevents handoff"); f.Raycaster.enabled = true;
            f.Scroll.enabled = false; Require(!Ready(f.Host), "Disabled lobby scroll cannot hand off"); f.Scroll.enabled = true;
            var module = f.Events.currentInputModule;
            module.enabled = false; Require(!Ready(f.Host), "Disabled current input module prevents handoff"); module.enabled = true;
            f.Events.enabled = false; Require(!Ready(f.Host), "Disabled EventSystem prevents handoff"); f.Events.enabled = true;
        }

        private static bool Ready(AppController host)
        {
            var property = typeof(AppController).GetProperty("LobbyReadyForInput", Private);
            Require(property != null, "AppController exposes its actual lobby input readiness");
            return (bool)property.GetValue(host, null);
        }
        private static object Invoke(object target, string name) { return target.GetType().GetMethod(name, Private).Invoke(target, null); }
        private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, Private).GetValue(target); }
        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, Private);
            Require(field != null, "Required production state exists: " + name);
            field.SetValue(target, value);
        }
        private static void Require(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class HandoffProbe : MonoBehaviour
        {
            internal CanvasGroup Group;
            internal bool WasDisabled, BlocksWhenDisabled;
            internal float AlphaWhenDisabled;
            private void OnDisable()
            {
                if (Group == null) return;
                WasDisabled = true; BlocksWhenDisabled = Group.blocksRaycasts; AlphaWhenDisabled = Group.alpha;
            }
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly AppController Host;
            internal readonly BrandSplash Splash;
            internal readonly GameObject Lobby, Overlay;
            internal readonly CanvasGroup Opacity;
            internal readonly HandoffProbe Probe;
            internal readonly EventSystem Events;
            internal readonly GraphicRaycaster Raycaster;
            internal readonly ScrollRect Scroll;
            private readonly GameObject owner;
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly int height;

            internal Fixture(int height)
            {
                this.height = height;
                owner = new GameObject("Isolated startup fixture");
                var inert = new GameObject("Inert production host"); inert.SetActive(false); inert.transform.SetParent(owner.transform, false);
                Host = inert.AddComponent<AppController>();
                Invoke(Host, "BuildCanvas");
                var canvas = Get<RectTransform>(Host, "canvasRoot");
                canvas.SetParent(owner.transform, false);
                camera = new GameObject("Startup fixture camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(owner.transform, false);
                camera.enabled = false; camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = height * .5f; camera.aspect = 720f / height;
                camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UiFactory.Paper;
                target = new RenderTexture(720, height, 24); target.Create(); camera.targetTexture = target;
                ConfigureCanvas(canvas.GetComponent<Canvas>());
                var frame = Get<RectTransform>(Host, "frame");
                frame.GetComponent<SafeDesignFrame>().enabled = false;
                UiFactory.Center(frame); frame.localScale = Vector3.one; frame.sizeDelta = new Vector2(720, height);
                Events = inert.GetComponentInChildren<EventSystem>(true);
                Require(Events != null, "BuildCanvas supplies the production EventSystem");
                Events.transform.SetParent(owner.transform, false);
                Events.GetComponent<StandaloneInputModule>().forceModuleActive = true;
                Invoke(Host, "BuildLobby");
                Lobby = Get<GameObject>(Host, "lobbyPage");
                Scroll = Lobby.GetComponentInChildren<ScrollRect>();
                Raycaster = canvas.GetComponent<GraphicRaycaster>();

                Splash = inert.AddComponent<BrandSplash>();
                Splash.Logo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Branding/XY_logo.png");
                Require(Splash.Logo != null, "Production splash artwork exists");
                Invoke(Splash, "Awake");
                Overlay = Get<GameObject>(Splash, "overlay");
                Overlay.transform.SetParent(owner.transform, false);
                ConfigureCanvas(Overlay.GetComponent<Canvas>());
                Opacity = Overlay.GetComponent<CanvasGroup>();
                Probe = Overlay.AddComponent<HandoffProbe>(); Probe.Group = Opacity;
                Flush();
            }

            private void ConfigureCanvas(Canvas canvas)
            {
                canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                var rect = (RectTransform)canvas.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero; rect.localScale = Vector3.one;
                rect.sizeDelta = new Vector2(720, height);
            }
            internal void Flush() { Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); }
            private PointerEventData Hit(Vector2 point)
            {
                Flush();
                var pointer = new PointerEventData(Events) { position = point, pressPosition = point, button = PointerEventData.InputButton.Left, pointerId = -1 };
                var hits = new List<RaycastResult>(); Events.RaycastAll(pointer, hits);
                Require(hits.Count > 0, "Production UGUI has a rendered raycast hit");
                pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
                return pointer;
            }
            private Vector2 ScrollPoint()
            {
                var r = Scroll.viewport.rect;
                return RectTransformUtility.WorldToScreenPoint(camera, Scroll.viewport.TransformPoint(new Vector2(r.xMin + 8, r.center.y)));
            }
            internal void CheckOpaqueBlocker()
            {
                Require(Mathf.Abs(Opacity.alpha - 1) < .0001f,
                    "Visible splash must stay fully opaque; no partially revealed lobby behind a raycast blocker");
                var pointer = Hit(ScrollPoint());
                Require(pointer.pointerCurrentRaycast.gameObject.transform.IsChildOf(Overlay.transform),
                    "Opaque splash owns the raycast until handoff");
            }
            internal void CheckDragAndClick()
            {
                var pointer = Hit(ScrollPoint());
                Require(ExecuteEvents.GetEventHandler<IDragHandler>(pointer.pointerCurrentRaycast.gameObject) == Scroll.gameObject,
                    "First post-handoff raycast reaches the real lobby ScrollRect");
                var card = LobbyDesignChecks.FindCard(Scroll.content, "Sudoku").GetComponent<Button>();
                Require(card != null, "First game uses a card-level entry after handoff");
                LobbyInputTestDriver.CheckBinding(card, Host, "EnterSudoku", Require);
                LobbyInputTestDriver.CheckGestures(Events, camera, Scroll, card, Flush, Require);
            }
            internal void Capture(string name)
            {
                Flush();
                var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(720, height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, 720, height), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }
            public void Dispose()
            {
                camera.targetTexture = null;
                if (RenderTexture.active == target) RenderTexture.active = null;
                Object.DestroyImmediate(owner); target.Release(); Object.DestroyImmediate(target);
            }
        }
    }
}
