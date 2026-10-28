using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.Zuma;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Runs production page coroutines with the direction-ready callback controlled by the fixture.
    // This checks presentation handoff, not Android's physical orientation delivery.
    [InitializeOnLoad]
    public static class ZumaNavigationChecks
    {
        private const string Pending = "Xanadu.ZumaNavigationChecks.Pending";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly StringBuilder Report = new StringBuilder();
        private static int assertions, cases, failures;

        static ZumaNavigationChecks() { EditorApplication.playModeStateChanged += StateChanged; }
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
            new GameObject("Zuma navigation checks").AddComponent<Runner>().StartCoroutine(AfterFrame());
        }
        private static IEnumerator AfterFrame() { yield return null; Execute(); }
        private static void Execute()
        {
            assertions = cases = failures = 0; Report.Clear(); var oldFont = UiFactory.Font;
            try
            {
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Check("exit retains the old canvas until the lobby handoff", ExitRetainsCanvas);
                Check("stale exit cannot release a newer presentation", StaleExit);
                Check("unfinished canvas cannot cover the host", HiddenConstruction);
                Check("entry publishes only a complete selection", ReadySelection);
                Check("real corrupt-save error is presented before host hides", ReadySaveError);
                Check("Back cancels entry once without publishing a stale page", CancelEntry);
                Check("inactive Zuma cannot rotate another active game on resume", InactiveOrientation);
                Check("exit cancels an already-pressed skin button", PendingSkinButton);
                Check("exit blocks an already-pressed standard button", PendingStandardButton);
                Report.AppendLine("LIMITS: production Unity objects/coroutines; direction readiness is controlled, no device orientation or physical input.");
                Report.AppendLine((failures == 0 ? "ZUMA_NAVIGATION_CHECKS_PASS: " : "ZUMA_NAVIGATION_CHECKS_FAIL: ") + cases + " cases, " + assertions + " assertions, " + failures + " failures");
            }
            catch (Exception e) { failures++; Report.AppendLine(e.ToString()); }
            finally
            {
                UiFactory.Font = oldFont;
                var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/logs"));
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "zuma-navigation-checks.txt"), Report.ToString());
                Debug.Log(Report.ToString()); EditorApplication.Exit(failures == 0 ? 0 : 1);
            }
        }

        private static void ExitRetainsCanvas(Fixture f)
        {
            var canvas = f.AttachVisibleCanvas();
            var routine = (IEnumerator)Invoke(f.Page, "ExitRoutine", 7);
            Require(routine.MoveNext(), "Exit starts the direction wait");
            Require(f.OrientationRequests.Count == 1 && f.OrientationRequests[0] == ScreenOrientation.Portrait,
                "Explicit Zuma exit still requests portrait");
            Require(Get<GameObject>(f.Page, "canvasObject") == canvas && canvas.activeSelf,
                "The last Zuma picture stays visible throughout the direction wait");
            Require(!canvas.GetComponent<GraphicRaycaster>().enabled, "Retained picture cannot dispatch old dialog actions");
            Require(f.LobbyReturns == 0, "Lobby is not announced before readiness");
            CompleteDirection(routine, true);
            Require(!routine.MoveNext(), "Exit finishes after readiness");
            Require(f.LobbyReturns == 1 && Get<GameObject>(f.Page, "canvasObject") == null && !f.Page.IsVisible,
                "Canvas cleanup and one lobby handoff finish in the same continuation");
        }

        private static void StaleExit(Fixture f)
        {
            var canvas = f.AttachVisibleCanvas();
            var routine = (IEnumerator)Invoke(f.Page, "ExitRoutine", 7);
            Require(routine.MoveNext(), "Old exit reaches its wait");
            Set(f.Page, "navigationToken", 8);
            CompleteDirection(routine, true);
            Require(!routine.MoveNext(), "Superseded exit ends");
            Require(Get<GameObject>(f.Page, "canvasObject") == canvas && canvas.activeSelf && f.LobbyReturns == 0,
                "A stale callback cannot remove the active presentation or return the lobby");
        }

        private static void HiddenConstruction(Fixture f)
        {
            Set(f.Page, "assets", new ZumaAssets());
            Invoke(f.Page, "BuildCanvas");
            Require(!Get<GameObject>(f.Page, "canvasObject").activeSelf,
                "An unfinished Zuma canvas stays hidden while the host is still visible");
        }

        private static void ReadySelection(Fixture f)
        {
            var notifications = 0;
            SubscribeReady(f.Page, () =>
            {
                notifications++;
                var canvas = Get<GameObject>(f.Page, "canvasObject");
                Require(canvas != null && canvas.activeSelf, "New page is active before the host is hidden");
                Require(canvas.transform.Find("1280x720 safe game frame/Zuma scene/Map preview") != null,
                    "The actual selection content exists before the ready notification");
            });
            Set(f.Page, "data", ZumaSaveData.CreateNew());
            var routine = (IEnumerator)Invoke(f.Page, "EnterRoutine", 7);
            Require(routine.MoveNext(), "Entry first waits for orientation");
            Require(f.OrientationRequests.Count == 1 && f.OrientationRequests[0] == ScreenOrientation.LandscapeLeft,
                "Explicit Zuma entry still requests landscape");
            Require(Get<GameObject>(f.Page, "canvasObject") == null && notifications == 0,
                "Waiting entry leaves host presentation ownership intact");
            CompleteDirection(routine, true);
            Require(!routine.MoveNext(), "Ready entry completes");
            Require(notifications == 1 && !Get<bool>(f.Page, "transition"), "Exactly one complete selection is published");
            Require(!routine.MoveNext() && notifications == 1, "Completed entry cannot notify twice");
        }

        private static void ReadySaveError(Fixture f)
        {
            Directory.CreateDirectory(f.Directory);
            File.WriteAllText(Path.Combine(f.Directory, "zuma-v1.json"), "{bad-json");
            var notifications = 0;
            SubscribeReady(f.Page, () =>
            {
                notifications++;
                Require(Get<ZumaView>(f.Page, "view").HasDialog, "Real load failure has its recovery dialog before handoff");
                Require(Get<GameObject>(f.Page, "canvasObject").activeSelf, "Recovery UI is visible before host hides");
            });
            var routine = (IEnumerator)Invoke(f.Page, "EnterRoutine", 7);
            Require(routine.MoveNext(), "Error fixture waits for orientation"); CompleteDirection(routine, true);
            Require(!routine.MoveNext() && notifications == 1, "A completed error page is published once");
            Require(File.ReadAllText(Path.Combine(f.Directory, "zuma-v1.json")) == "{bad-json", "Showing the error never overwrites the invalid save");
        }

        private static void CancelEntry(Fixture f)
        {
            var notifications = 0; SubscribeReady(f.Page, () => notifications++);
            Set(f.Page, "data", ZumaSaveData.CreateNew());
            var routine = (IEnumerator)Invoke(f.Page, "EnterRoutine", 7);
            Require(routine.MoveNext(), "Cancelable entry is awaiting direction");
            f.Page.HandleBack();
            int token = Get<int>(f.Page, "navigationToken");
            Require(token > 7, "Back invalidates pending entry immediately");
            f.Page.HandleBack(); f.Page.Exit();
            Require(Get<int>(f.Page, "navigationToken") == token, "Repeated Back/Exit does not restart the exit wait");
            CompleteDirection(routine, true);
            Require(!routine.MoveNext() && notifications == 0 && Get<GameObject>(f.Page, "canvasObject") == null,
                "Late entry readiness cannot build a stale canvas after cancellation");
        }

        private static void InactiveOrientation(Fixture f)
        {
            Set(f.Page, "visible", false); Set(f.Page, "transition", false);
            f.Page.HostActivity(false);
            f.Page.HostActivity(true);
            Require(f.OrientationRequests.Count == 0,
                "Inactive Zuma never requests an orientation when another game resumes");
        }

        private static void PendingSkinButton(Fixture f)
        {
            var canvas = f.AttachVisibleCanvas(); var commands = 0;
            var button = ZumaArt.Button(canvas.transform, Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
                100, 100, 180, 72, () => commands++);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var pointer = new UnityEngine.EventSystems.PointerEventData(null) {
                pointerId = -1,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            Require(RectTransformUtility.RectangleContainsScreenPoint(rect, pointer.position), "Late release fixture hits the real skin button");
            button.OnPointerDown(pointer);
            var routine = (IEnumerator)Invoke(f.Page, "ExitRoutine", 7);
            Require(routine.MoveNext(), "Pressed skin fixture reaches exit wait");
            button.OnPointerUp(pointer);
            Require(commands == 0, "Cached pointerUp cannot trigger an old RESET/RETRY action while exiting");
            Require(button.gameObject.activeInHierarchy, "Canceling a press preserves the old button image");
        }

        private static void PendingStandardButton(Fixture f)
        {
            var canvas = f.AttachVisibleCanvas(); var commands = 0;
            var button = UiFactory.Button(canvas.transform, "Old dialog action", 100, 100, 180, 72, () => commands++);
            var pointer = new UnityEngine.EventSystems.PointerEventData(null) { pointerId = -1 };
            button.OnPointerDown(pointer);
            var routine = (IEnumerator)Invoke(f.Page, "ExitRoutine", 7);
            Require(routine.MoveNext(), "Pressed standard fixture reaches exit wait");
            button.OnPointerUp(pointer); button.OnPointerClick(pointer);
            Require(commands == 0, "Cached click cannot trigger an old standard-button action while exiting");
            Require(button.gameObject.activeInHierarchy, "Disabling input preserves the old standard button image");
        }

        private static void CompleteDirection(IEnumerator routine, bool ready)
        {
            var callback = (Action<bool>)routine.Current.GetType().GetField("completed", Fields).GetValue(routine.Current);
            callback(ready);
        }
        private static void SubscribeReady(ZumaPage page, Action callback)
        {
            var ready = typeof(ZumaPage).GetEvent("PresentationReady", Fields);
            Require(ready != null, "Page exposes a complete-presentation handoff");
            ready.AddEventHandler(page, callback);
        }
        private static void Check(string name, Action<Fixture> check)
        {
            cases++;
            try { using (var f = new Fixture()) check(f); Report.AppendLine("PASS " + name); }
            catch (Exception e) { failures++; Report.AppendLine("FAIL " + name + "\n" + e); }
        }
        private static void Require(bool condition, string message)
        { assertions++; if (!condition) throw new InvalidOperationException(message); }
        private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, Fields).GetValue(target); }
        private static void Set(object target, string name, object value) { target.GetType().GetField(name, Fields).SetValue(target, value); }
        private static object Invoke(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, Fields).Invoke(target, args); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        }
        private sealed class Fixture : IDisposable
        {
            internal readonly ZumaPage Page;
            internal readonly string Directory = Path.Combine(Path.GetTempPath(), "zuma-navigation-" + Guid.NewGuid().ToString("N"));
            internal int LobbyReturns;
            internal readonly List<ScreenOrientation> OrientationRequests = new List<ScreenOrientation>();
            private readonly GameObject host = new GameObject("Zuma navigation fixture");
            internal Fixture()
            {
                Page = host.AddComponent<ZumaPage>(); Page.enabled = false;
                Page.Initialize(() => LobbyReturns++, () => false, () => false, (a, b) => true, null);
                Set(Page, "requestOrientation", (Action<ScreenOrientation>)OrientationRequests.Add);
                Set(Page, "store", new ZumaSaveStore(Directory, id => null));
                Set(Page, "visible", true); Set(Page, "transition", true); Set(Page, "navigationToken", 7);
            }
            internal GameObject AttachVisibleCanvas()
            {
                var canvas = new GameObject("Existing Zuma picture", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvas.transform.SetParent(host.transform, false); Set(Page, "canvasObject", canvas); return canvas;
            }
            public void Dispose()
            {
                Set(Page, "store", null); Object.DestroyImmediate(host);
                if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true);
            }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
