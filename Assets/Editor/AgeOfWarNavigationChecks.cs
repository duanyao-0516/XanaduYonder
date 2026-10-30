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
using XanaduYonder.Core.AgeOfWar;
using XanaduYonder.Core.AgeOfWar.One;
using XanaduYonder.Core.AgeOfWar.Two;
using XanaduYonder.Runtime.AgeOfWar;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Production coroutines and Unity objects, with only physical orientation readiness controlled.
    // This suite does not claim Android rotation, safe-area delivery, or physical input coverage.
    [InitializeOnLoad]
    public static class AgeOfWarNavigationChecks
    {
        private const string Pending = "Xanadu.AgeOfWarNavigationChecks.Pending";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly StringBuilder Report = new StringBuilder();
        private static int assertions, cases, failures;

        static AgeOfWarNavigationChecks() { EditorApplication.playModeStateChanged += StateChanged; }
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
            new GameObject("Age of War navigation checks").AddComponent<Runner>().StartCoroutine(AfterFrame());
        }
        private static IEnumerator AfterFrame()
        {
            yield return null;
            assertions = cases = failures = 0;
            Report.Clear();
            for (int edition = 1; edition <= 2; edition++)
            {
                Check(edition, "unfinished canvas stays hidden", HiddenConstruction);
                Check(edition, "entry publishes exactly one complete original loading screen", ReadyMenu);
                Check(edition, "stale entry cannot construct a page", StaleEntry);
                Check(edition, "Back cancels entry once", CancelEntry);
                Check(edition, "failed direction never publishes a menu", FailedDirection);
                Check(edition, "exit retains the last picture with input disabled", ExitRetainsCanvas);
                Check(edition, "stale exit cannot release another presentation", StaleExit);
                Check(edition, "background resets production clock and preserves page identity", BackgroundClock);
                Check(edition, "opening pauses without catch-up and Back exits", OpeningLifecycle);
                Check(edition, "inactive resume cannot rotate or create a page", InactiveOrientation);
                yield return CheckAsync(edition, "exit releases original audio and presentation resources", ExitReleasesResources);
            }
            Report.AppendLine("LIMITS: production Unity objects and Page coroutines; direction-ready callbacks are controlled. No physical Android orientation, safe-area delivery, device lifecycle, or touch-input claim.");
            Report.AppendLine((failures == 0 ? "AGE_OF_WAR_NAVIGATION_CHECKS_PASS: " : "AGE_OF_WAR_NAVIGATION_CHECKS_FAIL: ") + cases + " cases, " + assertions + " assertions, " + failures + " failures");
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/logs"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "age-of-war-navigation-checks.txt"), Report.ToString());
            Debug.Log(Report.ToString());
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static void HiddenConstruction(Fixture f)
        {
            Set(f.Page, "assets", AgeOfWarAssets.Load(f.Edition));
            Invoke(f.Page, "BuildCanvas");
            Require(!Get<GameObject>(f.Page, "canvasObject").activeSelf, "Canvas remains hidden before completed presentation handoff");
            Require(Get<AgeOfWarView>(f.Page, "view").SceneKey == null, "Hidden construction does not bypass the original opening");
        }
        private static void ReadyMenu(Fixture f)
        {
            int notifications = 0;
            SubscribeReady(f.Page, () =>
            {
                notifications++;
                var canvas = Get<GameObject>(f.Page, "canvasObject");
                var view = Get<AgeOfWarView>(f.Page, "view");
                Require(canvas != null && canvas.activeSelf, "Canvas is visible before notifying the host");
                var opening = Get<AgeOfWarOpening>(f.Page, "opening");
                Require(view != null && view.SceneKey == null && opening != null && !opening.Done, "Ready notification contains the verified local loading screen");
                Require(canvas.transform.Find("Original stage") != null, "Actual original stage exists before handoff");
                if(f.Edition==1)opening.Advance(1.0/40);
                var point = f.Edition == 1 ? new Vector2(325, 241) : new Vector2(359, 236);
                Require(opening.Hit(point) != null, "Original loaded Play hit region exists before handoff");
                Require(Get<AgeOfWarAudio>(f.Page, "audioPlayer") != null, "Original audio owner is initialized before handoff");
            });
            var routine = f.EnterRoutine();
            Require(routine.MoveNext(), "Entry reaches direction wait first");
            Require(f.OrientationRequests.Count == 1 && f.OrientationRequests[0] == ScreenOrientation.LandscapeLeft, "Entry requests landscape exactly once");
            Require(Get<GameObject>(f.Page, "canvasObject") == null && notifications == 0, "Waiting entry leaves host ownership unchanged");
            CompleteDirection(routine, true);
            Require(!routine.MoveNext(), "Ready entry completes");
            Require(notifications == 1 && !Get<bool>(f.Page, "transition"), "Exactly one complete loading screen is published");
            Require(!routine.MoveNext() && notifications == 1, "Completed iterator cannot notify twice");
            f.Page.Enter(f.Edition == 1 ? 2 : 1);
            Require(Get<int>(f.Page, "edition") == f.Edition && f.OrientationRequests.Count == 1, "Entering an already visible page cannot replace the active edition");
        }
        private static void StaleEntry(Fixture f)
        {
            int notifications = 0;
            SubscribeReady(f.Page, () => notifications++);
            var routine = f.EnterRoutine();
            Require(routine.MoveNext(), "Entry reaches orientation wait");
            Set(f.Page, "navigationToken", 8);
            CompleteDirection(routine, true);
            Require(!routine.MoveNext(), "Stale entry terminates");
            Require(notifications == 0 && Get<GameObject>(f.Page, "canvasObject") == null && Get<AgeOfWarAssets>(f.Page, "assets") == null, "Stale readiness cannot allocate original resources or publish a page");
        }
        private static void CancelEntry(Fixture f)
        {
            int notifications = 0;
            SubscribeReady(f.Page, () => notifications++);
            var routine = f.EnterRoutine();
            Require(routine.MoveNext(), "Cancelable entry waits for direction");
            f.Page.HandleBack();
            int token = Get<int>(f.Page, "navigationToken");
            Require(token > 7 && Get<bool>(f.Page, "leaving"), "Back immediately invalidates the pending entry");
            f.Page.HandleBack(); f.Page.Exit();
            Require(Get<int>(f.Page, "navigationToken") == token, "Repeated Back and Exit cannot restart cancellation");
            Require(f.OrientationRequests.Count == 2 && f.OrientationRequests[1] == ScreenOrientation.Portrait, "Cancellation requests one portrait return");
            CompleteDirection(routine, true);
            Require(!routine.MoveNext() && notifications == 0 && Get<GameObject>(f.Page, "canvasObject") == null, "Late readiness cannot publish a canceled menu");
        }
        private static void FailedDirection(Fixture f)
        {
            int notifications = 0;
            SubscribeReady(f.Page, () => notifications++);
            var routine = f.EnterRoutine();
            Require(routine.MoveNext(), "Entry waits for direction");
            CompleteDirection(routine, false);
            Require(!routine.MoveNext(), "Unready entry ends");
            Require(!string.IsNullOrEmpty(f.Page.Error) && notifications == 0 && Get<GameObject>(f.Page, "canvasObject") == null, "Orientation timeout does not create a hidden or incomplete page");
            Require(Get<bool>(f.Page, "leaving"), "Failed entry begins one normal exit handoff");
        }
        private static void ExitRetainsCanvas(Fixture f)
        {
            var canvas = f.AttachVisibleCanvas();
            var routine = (IEnumerator)Invoke(f.Page, "ExitRoutine", 7);
            Require(routine.MoveNext(), "Exit begins direction wait");
            Require(f.OrientationRequests.Count == 1 && f.OrientationRequests[0] == ScreenOrientation.Portrait, "Exit requests portrait");
            Require(Get<GameObject>(f.Page, "canvasObject") == canvas && canvas.activeSelf, "The last game image remains visible throughout orientation wait");
            Require(!canvas.GetComponent<GraphicRaycaster>().enabled, "Retained image cannot dispatch old UI actions");
            Require(f.LobbyReturns == 0, "No lobby handoff before portrait readiness");
            CompleteDirection(routine, true);
            Require(!routine.MoveNext(), "Ready exit completes");
            Require(f.LobbyReturns == 1 && Get<GameObject>(f.Page, "canvasObject") == null && !f.Page.IsVisible, "Cleanup and one lobby handoff complete together");
            Require(!canvas.activeSelf, "Retired canvas is hidden immediately while Unity schedules destruction");
        }
        private static void StaleExit(Fixture f)
        {
            var canvas = f.AttachVisibleCanvas();
            var routine = (IEnumerator)Invoke(f.Page, "ExitRoutine", 7);
            Require(routine.MoveNext(), "Old exit reaches direction wait");
            Set(f.Page, "navigationToken", 8);
            CompleteDirection(routine, true);
            Require(!routine.MoveNext(), "Superseded exit terminates");
            Require(Get<GameObject>(f.Page, "canvasObject") == canvas && canvas.activeSelf && f.LobbyReturns == 0, "Stale exit cannot release another active presentation");
        }
        private static void BackgroundClock(Fixture f)
        {
            f.CompleteEntry();
            var canvas = Get<GameObject>(f.Page, "canvasObject");
            var view = Get<AgeOfWarView>(f.Page, "view");
            var assets = Get<AgeOfWarAssets>(f.Page, "assets");
            IAgeOfWarBattle battle = f.Edition == 1 ? (IAgeOfWarBattle)new AgeOfWarOneBattle(1, 17) : new AgeOfWarTwoBattle(.6, 17);
            var clock = new AgeOfWarClock(battle.TickRate);
            Set(f.Page, "battle", battle); Set(f.Page, "clock", clock);
            Require(clock.Advance(.9 / battle.TickRate, true, battle.Tick) == 0, "Fixture contains a real incomplete simulation tick");
            Set(f.Page, "pressed", true); Set(f.Page, "slider", "music");
            f.Page.HostActivity(false);
            Require(Get<double>(clock, "accumulator") == 0, "Background clears the production clock remainder");
            Require(!Get<bool>(f.Page, "pressed") && Get<string>(f.Page, "slider") == "", "Background cancels held input and slider ownership");
            long before = battle.Capture().Tick;
            Invoke(f.Page, "Update"); Invoke(f.Page, "Update");
            Require(battle.Capture().Tick == before, "Actual Page.Update does not tick while backgrounded");
            Require(Get<bool>(Get<AgeOfWarAudio>(f.Page, "audioPlayer"), "suspended"), "Background suspends original audio");
            f.Page.HostActivity(true);
            Require(clock.Advance(.2 / battle.TickRate, true, battle.Tick) == 0, "Resume cannot combine pre-background elapsed time into a tick");
            Require(Get<GameObject>(f.Page, "canvasObject") == canvas && Get<AgeOfWarView>(f.Page, "view") == view && Get<AgeOfWarAssets>(f.Page, "assets") == assets && ReferenceEquals(Get<IAgeOfWarBattle>(f.Page, "battle"), battle), "Resume preserves the same scene, resources and battle");
            Require(f.OrientationRequests.Count == 1, "Background and resume do not request another rotation");
        }
        private static void OpeningLifecycle(Fixture f)
        {
            f.CompleteEntry();
            var opening=Get<AgeOfWarOpening>(f.Page,"opening");
            var view=Get<AgeOfWarView>(f.Page,"view");
            if(f.Edition==1)opening.Advance(1.0/40);
            var point=f.Edition==1?new Vector2(325,241):new Vector2(359,236);
            var hit=opening.Hit(point);opening.Pointer(hit,true,false);opening.Pointer(hit,false,true);
            opening.Advance(.1);int before=opening.SourceFrame;
            f.Page.HostActivity(false);opening.Advance(10);
            Require(opening.SourceFrame==before,"A suspended opening cannot consume background elapsed time");
            f.Page.HostActivity(true);opening.Advance(0);
            Require(opening.SourceFrame==before,"Resume preserves source frame without replaying background time");
            Require(!opening.Done&&view.SceneKey==null,"Clicking Play starts the original movie without skipping to the menu");
            f.Page.HandleBack();
            Require(Get<bool>(f.Page,"leaving"),"Android Back leaves the game during its non-skippable opening");
        }
        private static void InactiveOrientation(Fixture f)
        {
            Set(f.Page, "visible", false); Set(f.Page, "transition", false);
            f.Page.HostActivity(false); f.Page.HostActivity(true);
            Require(f.OrientationRequests.Count == 0, "Inactive page cannot rotate a different active game");
            Require(Get<GameObject>(f.Page, "canvasObject") == null && Get<AgeOfWarAssets>(f.Page, "assets") == null, "Inactive lifecycle notifications cannot create a new presentation");
        }
        private static IEnumerator ExitReleasesResources(Fixture f)
        {
            f.CompleteEntry();
            var audio = Get<AgeOfWarAudio>(f.Page, "audioPlayer");
            audio.SelectMusic("music/1");
            var music = Get<AudioSource>(audio, "music");
            var effects = Get<AudioSource>(audio, "effects");
            audio.StartOpeningStream(f.Edition==1?"intro-stream":"sponsor-stream");
            var stream = Get<AudioSource>(audio,"openingStream");
            var canvas = Get<GameObject>(f.Page, "canvasObject");
            var assets = Get<AgeOfWarAssets>(f.Page, "assets");
            Require(music != null && effects != null && music != effects && music.clip != null, "Both real original audio sources and soundtrack are owned before exit");
            Set(f.Page, "transition", true); Set(f.Page, "leaving", true);
            var routine = (IEnumerator)Invoke(f.Page, "ExitRoutine", 7);
            Require(routine.MoveNext(), "Resource exit waits for direction");
            Require(Get<bool>(audio, "suspended") && !music.isPlaying && !effects.isPlaying && !stream.isPlaying, "Exit wait silences both sources while retaining the last image");
            f.Page.HostActivity(false);
            f.Page.HostActivity(true);
            Require(Get<bool>(audio, "suspended") && !music.isPlaying && !effects.isPlaying && !stream.isPlaying, "Background and foreground during exit must preserve suspended audio while Direction is pending");
            Require(Get<AgeOfWarAssets>(f.Page, "assets") == assets && canvas.activeSelf, "Assets remain alive while the retained canvas is visible");
            CompleteDirection(routine, true);
            Require(!routine.MoveNext(), "Resource exit finishes after readiness");
            Require(Get<AgeOfWarAudio>(f.Page, "audioPlayer") == null && Get<AgeOfWarAssets>(f.Page, "assets") == null && Get<AgeOfWarView>(f.Page, "view") == null && Get<AgeOfWarViewport>(f.Page, "viewport") == null && Get<AgeOfWarClock>(f.Page, "clock") == null && Get<IAgeOfWarBattle>(f.Page, "battle") == null, "Exit releases all presentation ownership references");
            Require(f.LobbyReturns == 1, "Resource release announces the lobby once");
            yield return null;
            Require(music == null && effects == null && stream == null && audio == null && canvas == null, "After Unity's destroy boundary music, effects, opening stream and the old canvas are actually destroyed");
        }

        private static void CompleteDirection(IEnumerator routine, bool ready)
        {
            var direction = routine.Current;
            Require(direction != null, "Production coroutine yielded its Direction iterator");
            var callback = direction.GetType().GetField("result", Fields);
            Require(callback != null, "Direction iterator retains its actual completion callback");
            ((Action<bool>)callback.GetValue(direction))(ready);
        }
        private static void SubscribeReady(AgeOfWarPage page, Action callback)
        {
            var ready = typeof(AgeOfWarPage).GetEvent("PresentationReady", Fields);
            Require(ready != null, "Page exposes the completed-presentation event");
            ready.GetAddMethod(true).Invoke(page, new object[] { callback });
        }
        private static void Check(int edition, string name, Action<Fixture> body)
        {
            cases++;
            try { using (var f = new Fixture(edition)) body(f); Report.AppendLine("PASS edition " + edition + ": " + name); }
            catch (Exception error) { failures++; Report.AppendLine("FAIL edition " + edition + ": " + name + "\n" + error); }
        }
        private static IEnumerator CheckAsync(int edition, string name, Func<Fixture, IEnumerator> body)
        {
            cases++;
            using (var fixture = new Fixture(edition))
            {
                var routine = body(fixture);
                bool failed = false;
                while (true)
                {
                    bool next = false; object value = null;
                    try { next = routine.MoveNext(); if (next) value = routine.Current; }
                    catch (Exception error) { failed = true; failures++; Report.AppendLine("FAIL edition " + edition + ": " + name + "\n" + error); }
                    if (failed || !next) break;
                    yield return value;
                }
                if (!failed) Report.AppendLine("PASS edition " + edition + ": " + name);
            }
        }
        private static void Require(bool condition, string message)
        { assertions++; if (!condition) throw new InvalidOperationException(message); }
        private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, Fields).GetValue(target); }
        private static void Set(object target, string name, object value) { target.GetType().GetField(name, Fields).SetValue(target, value); }
        private static object Invoke(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, Fields).Invoke(target, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private sealed class Fixture : IDisposable
        {
            internal readonly AgeOfWarPage Page;
            internal readonly int Edition;
            internal readonly List<ScreenOrientation> OrientationRequests = new List<ScreenOrientation>();
            internal int LobbyReturns;
            private readonly GameObject host = new GameObject("Age of War navigation fixture");
            internal Fixture(int edition)
            {
                Edition = edition;
                Page = host.AddComponent<AgeOfWarPage>(); Page.enabled = false;
                Page.Initialize(() => LobbyReturns++, () => false, enabled => { });
                Set(Page, "requestOrientation", (Action<ScreenOrientation>)OrientationRequests.Add);
                Set(Page, "edition", edition); Set(Page, "visible", true); Set(Page, "transition", true); Set(Page, "navigationToken", 7);
                Set(Page, "oldTargetFps", Application.targetFrameRate); Set(Page, "originalCursor", Cursor.visible);
            }
            internal IEnumerator EnterRoutine() { return (IEnumerator)Invoke(Page, "EnterRoutine", 7); }
            internal void CompleteEntry()
            {
                var routine = EnterRoutine();
                Require(routine.MoveNext(), "Fixture enters production direction wait");
                CompleteDirection(routine, true);
                Require(!routine.MoveNext() && Get<GameObject>(Page, "canvasObject") != null, "Fixture reaches complete original menu");
            }
            internal GameObject AttachVisibleCanvas()
            {
                var canvas = new GameObject("Existing Age of War picture", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvas.transform.SetParent(host.transform, false); Set(Page, "canvasObject", canvas); return canvas;
            }
            public void Dispose() { Page.StopAllCoroutines(); Object.DestroyImmediate(host); }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
