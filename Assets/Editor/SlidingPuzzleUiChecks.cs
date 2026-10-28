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
using XanaduYonder.Core.SlidingPuzzle;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.SlidingPuzzle;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Production UGUI and rendered raycasts. Profiles are injected; player save files are never opened.
    [InitializeOnLoad]
    public static class SlidingPuzzleUiChecks
    {
        private const string Pending = "Xanadu.SlidingPuzzleUiChecks.Pending";
        private static readonly int[] ReferenceCells = { 0, 2, 3, 4, 1, 5, 6, 8, 9, 14, 7, 12, 13, 11, 10, 15 };
        private static readonly StringBuilder report = new StringBuilder();
        private static int checks;
        private static string output;

        static SlidingPuzzleUiChecks() { EditorApplication.playModeStateChanged += StateChanged; }
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
            new GameObject("Sliding Puzzle checks runner").AddComponent<Runner>().StartCoroutine(Execute());
        }
        private sealed class Runner : MonoBehaviour { }
        private static IEnumerator Execute()
        {
            yield return null;
            checks = 0; report.Clear();
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/validation/sliding-puzzle-ui"));
            Directory.CreateDirectory(output);
            report.AppendLine("Unity " + Application.unityVersion + "; production UGUI; Camera.Render; isolated profiles; reference 390x820");
            var run = Scenarios();
            while (true)
            {
                bool next = false; object current = null; Exception failure = null;
                try { next = run.MoveNext(); if (next) current = run.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null || !next)
                {
                    (run as IDisposable)?.Dispose();
                    report.AppendLine(failure == null ? "SLIDING_PUZZLE_UI_CHECKS_PASS: " + checks + " assertions" : "SLIDING_PUZZLE_UI_CHECKS_FAIL after " + checks + " assertions\n" + failure);
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
            foreach (int height in new[] { 1514, 1320, 1582, 1150 })
            using (var f = new Fixture(height))
            {
                var profile = ReferenceProfile();
                bool allowed = true; Action finishSettings = null; int home = 0;
                var page = f.Root.gameObject.AddComponent<SlidingPuzzlePage>();
                page.Initialize(f.Root, () => home++, done => { allowed = false; finishSettings = done; },
                    () => allowed, () => false, () => false, f.Root.gameObject.AddComponent<GameFeedback>(), _ => { }, profile);
                page.Enter(); page.LayoutNow(); yield return null;
                Require(!page.IsChoosing && page.Session.Game.Size == 4, "Fresh page opens free 4x4");
                Same(ReferenceCells, page.Session.Game.GetCells(), "Reference board uses the exact prototype permutation");
                page.Session.Tick(3);
                Near((float)page.Session.ElapsedSeconds, 0, "Clock stays stopped before first legal move", .001f);
                CheckGeometry(f, page); CheckFonts(f); CheckSizeLabels(f); f.Capture("free-4-reference");
                foreach (string name in new[] { "Home", "Book", "Settings", "Free mode", "Campaign mode", "Size 3", "Size 4", "Size 5", "Restart" }) f.CheckHit(f.Find(name));
                Require(!f.Find("Undo").GetComponent<Button>().interactable, "Undo is disabled without history");
                Near(f.Find("Undo").GetComponent<CanvasGroup>().alpha, .48f, "Undo without history uses prototype disabled opacity", .001f);

                f.Click(f.Find("Size 3")); page.LayoutNow(); yield return null;
                Require(page.Session.Game.Size == 3, "3x3 selector changes the active free slot");
                CheckBoard(f, page, f.Scale); CheckSizeLabels(f); f.Capture("free-3");
                f.Click(f.Find("Size 5")); page.LayoutNow(); yield return null;
                Require(page.Session.Game.Size == 5, "5x5 selector changes the active free slot");
                CheckBoard(f, page, f.Scale); CheckSizeLabels(f); f.Capture("free-5");
                f.Click(f.Find("Size 4")); page.LayoutNow(); yield return null;
                Same(ReferenceCells, page.Session.Game.GetCells(), "Returning to 4x4 keeps its independent attempt");

                var board = f.Find("SlidingPuzzle board");
                f.TapBoard(f.Find("Tile 3"), board);
                Require(page.Session.Game.Moves == 0, "A nonadjacent tile cannot move");
                var gesture = f.Begin(f.Find("Tile 1"), board);
                f.Release(gesture, gesture.position);
                ExecuteEvents.Execute(board.gameObject, gesture, ExecuteEvents.pointerClickHandler);
                Require(page.Session.Game.Moves == 1, "Rendered down/up commits once, with no onClick fallback");
                yield return new WaitForSeconds(.18f);
                page.Session.Tick(2);
                Require(page.Session.ElapsedSeconds >= 2, "First legal move starts elapsed time");
                f.Click(f.Find("Undo"));
                Require(page.Session.Game.Moves == 2 && page.Session.Game.Assisted, "Undo costs a step and marks the attempt as practice");
                Same(ReferenceCells, page.Session.Game.GetCells(), "Undo restores the board permutation");
                yield return new WaitForSeconds(.18f); f.Capture("free-practice");

                f.Click(f.Find("Book"));
                Require(page.HasDialog, "Shared book icon opens the rules dialog");
                Require(f.ContainsText("每次只移动") && f.ContainsText("撤回"), "Rules describe moving and practice semantics");
                CheckFonts(f); CheckGoal(f, page.Session.Game.Size);
                Require(!f.HitWithin(f.Center(f.Find("Tile 1")), board), "Rules overlay owns the board's rendered hit point");
                double stopped = page.Session.ElapsedSeconds; int moves = page.Session.Game.Moves;
                page.Session.Tick(5); page.Move(1);
                Require(page.Session.ElapsedSeconds == stopped && page.Session.Game.Moves == moves, "Help blocks time and movement");
                f.Capture("help"); page.Back(); Require(!page.HasDialog && home == 0, "Back dismisses help only");

                f.Click(f.Find("Restart"));
                Require(page.HasDialog, "Restart opens a choice dialog"); f.Capture("restart");
                page.Back(); Require(!page.HasDialog && page.Session.Game.Moves == moves, "Cancelling restart keeps the live attempt");
                page.Restart(); page.LayoutNow(); yield return null;
                board = f.Find("SlidingPuzzle board");
                Require(page.Session.Game.Moves == 0 && !page.Session.Game.Assisted && page.Session.ElapsedSeconds == 0, "Restart resets counters and practice without changing the puzzle");
                Same(ReferenceCells, page.Session.Game.GetCells(), "Replay restores the original puzzle");

                gesture = f.Begin(f.Find("Tile 1"), board);
                f.Release(gesture, f.Center(f.Find("Cell 0")));
                Require(page.Session.Game.Moves == 1, "Sliding an adjacent tile toward the blank commits one move");
                yield return new WaitForSeconds(.18f);
                page.Restart(); page.LayoutNow(); yield return null;
                board = f.Find("SlidingPuzzle board");
                gesture = f.Begin(f.Find("Tile 1"), board);
                f.Release(gesture, gesture.position + Vector2.down * 80 * f.Scale);
                Require(page.Session.Game.Moves == 0, "Sliding away from the blank cannot move");
                gesture = f.Begin(f.Find("Tile 1"), board);
                page.SetForeground(false); page.SetForeground(true); f.Release(gesture, gesture.position);
                Require(page.Session.Game.Moves == 0, "Background cancels an unfinished pointer sequence");
                gesture = f.Begin(f.Find("Tile 1"), board);
                page.LayoutNow(); f.Release(gesture, gesture.position);
                Require(page.Session.Game.Moves == 0, "Layout replacement cancels an unfinished pointer sequence");
                board = f.Find("SlidingPuzzle board");
                gesture = f.Begin(f.Find("Tile 1"), board, 101);
                var second = f.Begin(f.Find("Tile 2"), board, 102);
                f.Release(gesture, gesture.position); f.Release(second, second.position);
                Require(page.Session.Game.Moves == 0, "A second pointer cancels the first without submitting either");

                f.TapBoard(f.Find("Tile 1"), board); yield return new WaitForSeconds(.18f);
                f.Click(f.Find("Settings")); stopped = page.Session.ElapsedSeconds; moves = page.Session.Game.Moves;
                page.Move(5); page.Session.Tick(4);
                Require(finishSettings != null && page.Session.Game.Moves == moves && page.Session.ElapsedSeconds == stopped, "Settings callback pauses time and board input");
                allowed = true; finishSettings();
                var active = page.Session; var savedCells = active.Game.GetCells();
                page.SelectMode(1); Require(page.IsChoosing, "Campaign mode shows the level chooser");
                active.Tick(4); Require(active.ElapsedSeconds == stopped, "Inactive free slot does not keep timing");
                page.SelectMode(0); Require(ReferenceEquals(active, page.Session), "Mode switching preserves the same free attempt");
                Same(savedCells, page.Session.Game.GetCells(), "Mode switching keeps the active permutation");
                page.SetForeground(false); stopped = page.Session.ElapsedSeconds;
                page.Session.Tick(4); page.Move(5);
                Require(page.Session.ElapsedSeconds == stopped && page.Session.Game.Moves == moves, "Background pauses elapsed time and movement");
                page.SetForeground(true); page.Leave(); page.Session.Tick(4);
                Require(page.Session.ElapsedSeconds == stopped, "Leaving pauses the attempt");
                page.Enter(); page.LayoutNow(); yield return null;
                Same(savedCells, page.Session.Game.GetCells(), "Reentry restores the same attempt");

                page.SelectMode(1); page.StartLevel(0); page.LayoutNow(); yield return null;
                Require(page.Session.Game.Size == 3 && page.Session.Game.Moves == 0, "First campaign level opens its 3x3 puzzle");
                f.Capture("campaign-1");
                page.Move(5); yield return new WaitForSeconds(.18f);
                page.Move(6);
                Require(page.Session.Game.Won, "Winning move updates the model before animation completion");
                if (height == 1514) page.LayoutNow();
                else if (height == 1320) { page.ShowRules(); page.Back(); }
                else if (height == 1582) { page.SetForeground(false); page.SetForeground(true); }
                else page.Suspend();
                page.CheckEndState(); yield return null;
                Require(page.HasDialog && profile.CompletedCount == 1, "Animation cancellation preserves one recorded win and its dialog");
                f.Capture("campaign-win");
                page.LayoutNow(); yield return null;
                Require(page.HasDialog && f.ContainsText("下一关"), "Layout of an already visible win keeps the next-level action");
                var originalSizeDelta = f.Root.sizeDelta;
                f.Root.sizeDelta = originalSizeDelta + new Vector2(0, -24);
                yield return null;
                Require(page.HasDialog && f.ContainsText("下一关"), "Root resize preserves an already visible win and its next-level action");
                f.Root.sizeDelta = originalSizeDelta; page.LayoutNow(); yield return null;
                Require(page.HasDialog && f.ContainsText("下一关"), "Restoring the safe area preserves the win dialog");
                f.Capture("campaign-win-after-layout");
                page.Save(); page.Save(); Require(profile.CompletedCount == 1, "Repeated terminal saves do not duplicate completion");
                moves = page.Session.Game.Moves; page.Move(6);
                Require(page.Session.Game.Moves == moves, "Completed board cannot move");
                page.Back(); page.ShowLevels(); yield return null;
                Require(page.IsChoosing && f.Find("Level 1").GetComponent<Button>().interactable, "A win unlocks the next level in the chooser");
                f.Capture("levels-after-first-win");
                f.Click(f.Find("Home")); Require(home == 1, "Home invokes the real lobby callback");
                report.AppendLine("PASS 720x" + height + ": prototype board/geometry/font, free sizes, raycasts, taps/swipes/cancellation, practice, help/restart, timer, lifecycle, win cancellation");
            }

            foreach (int completed in new[] { 0, 6, 12, 18 })
            using (var f = new Fixture(1514))
            {
                var profile = CompletedProfile(completed); var page = NewPage(f, profile);
                page.Enter(); page.SelectMode(1); page.ShowLevels(); page.LayoutNow(); yield return null;
                Require(profile.CompletedCount == completed, "Chooser fixture uses legitimately completed catalog levels");
                CheckFonts(f); CheckCampaignGeometry(f, profile);
                for (int chapter = 0; chapter < 3; chapter++)
                {
                    if (chapter == profile.SelectedChapter) continue;
                    var control = f.Find("Chapter " + chapter); var button = control.GetComponent<Button>();
                    bool unlocked = chapter * 6 <= completed;
                    Require(button != null && button.interactable == unlocked, "Chapter availability reflects its six-level prerequisite");
                    if (!unlocked)
                    {
                        UiIconGraphic symbol = null;
                        foreach (var icon in control.GetComponentsInChildren<UiIconGraphic>()) if (icon.Kind.ToString() == "Lock") symbol = icon;
                        Require(symbol != null, "Locked chapter has the shared lock icon");
                        Require(f.Bounds((RectTransform)symbol.transform).center.x > f.Bounds(control).center.x, "Chapter lock is on the right");
                        ColorNear(symbol.color, UiFactory.Hex("A2AAA3"), "Locked chapter icon uses the approved muted gray");
                        Near(control.GetComponent<CanvasGroup>().alpha, 1, "Locked chapter row stays opaque instead of inheriting level opacity", .001f);
                        Near(symbol.color.a, 1, "Gray chapter lock remains opaque", .001f);
                    }
                }
                f.Capture("levels-completed-" + completed);
                int lastChapter = Math.Min(2, completed / 6);
                for (int chapter = 0; chapter <= lastChapter; chapter++)
                {
                    if (chapter != profile.SelectedChapter) f.Click(f.Find("Chapter " + chapter));
                    yield return null;
                    Require(profile.SelectedChapter == chapter && f.Find("Chapter summary") != null, "Current chapter is represented by its summary card");
                    CheckCampaignGeometry(f, profile);
                    int count = 0;
                    foreach (var button in f.Root.GetComponentsInChildren<Button>()) if (button.name.StartsWith("Level ", StringComparison.Ordinal)) count++;
                    Require(count == 6, "Selected chapter exposes exactly six levels");
                    for (int level = chapter * 6; level < chapter * 6 + 6; level++)
                    {
                        var control = f.Find("Level " + level);
                        Require(control.GetComponent<Button>().interactable == (level <= completed), "Level availability follows sequential progress");
                        Near(control.GetComponent<CanvasGroup>().alpha, level <= completed ? 1 : .48f, "Level opacity matches its unlocked or disabled state", .001f);
                        f.CheckHit(control);
                    }
                    f.Capture("chapter-" + chapter + "-completed-" + completed);
                }
                if (completed == 18)
                {
                    f.Click(f.Find("Level 17")); yield return null;
                    Require(!page.Session.Game.Won && page.Session.Game.Moves == 0 && page.Session.ElapsedSeconds == 0 && !page.HasDialog,
                        "Selecting the completed active level starts a fresh attempt");
                    Same(SlidingPuzzleCatalog.Get(17).InitialCells, page.Session.Game.GetCells(), "Replaying the last level restores its catalog initial board");
                    Require(profile.CompletedCount == 18, "Replaying a completed level preserves campaign progress");
                    foreach (int tile in SlidingPuzzleCatalog.Get(17).Solution)
                    {
                        int before = page.Session.Game.Moves;
                        page.Move(tile);
                        Require(page.Session.Game.Moves == before + 1, "Last-level witness advances the replay through the production page");
                        yield return new WaitForSeconds(.18f);
                    }
                    page.CheckEndState(); yield return null;
                    Require(page.Session.Game.Won && page.HasDialog && profile.CompletedCount == 18, "Replayed last level completes without duplicating progress");
                    Require(f.Find("Result continue").GetComponentInChildren<Text>().text == "返回选关", "Final completion returns to selection instead of offering a nonexistent next level");
                    f.Capture("campaign-final-win");
                }
                report.AppendLine("PASS campaign " + completed + "/18: six-level chapters, sequential unlocks, lock icons and rendered level hit areas");
            }

            var counterChecks = ExtremeNumbersScenarios();
            while (counterChecks.MoveNext()) yield return counterChecks.Current;
            var recoveryChecks = RecoveryScenarios();
            while (recoveryChecks.MoveNext()) yield return recoveryChecks.Current;
            var clockChecks = SuspendedClockScenario();
            while (clockChecks.MoveNext()) yield return clockChecks.Current;
            var hostChecks = HostScenarios();
            while (hostChecks.MoveNext()) yield return hostChecks.Current;
        }

        private static SlidingPuzzleProfile ReferenceProfile()
        {
            var data = new SlidingPuzzleProfile().Capture();
            data.FreeSessions[1] = new SlidingPuzzleSessionData { LevelIndex = -1, Snapshot = new SlidingPuzzleGame(4, ReferenceCells).GetSnapshot(), ElapsedSeconds = 0, ResultRecorded = false };
            return new SlidingPuzzleProfile(data);
        }
        private static SlidingPuzzleProfile CompletedProfile(int count)
        {
            var profile = ReferenceProfile();
            for (int index = 0; index < count; index++)
            {
                var session = profile.StartLevel(index);
                foreach (int tile in SlidingPuzzleCatalog.Get(index).Solution) Require(session.Move(tile), "Catalog witness moves legally in UI progress fixture");
                Require(session.Game.Won, "Catalog witness solves the UI progress fixture"); profile.Capture();
            }
            profile.SelectMode(1); profile.SelectChapter(0); return profile;
        }
        private static SlidingPuzzlePage NewPage(Fixture f, SlidingPuzzleProfile profile)
        {
            var page = f.Root.gameObject.AddComponent<SlidingPuzzlePage>();
            page.Initialize(f.Root, () => { }, done => done(), () => true, () => false, () => false,
                f.Root.gameObject.AddComponent<GameFeedback>(), _ => { }, profile);
            return page;
        }

        private static IEnumerator SuspendedClockScenario()
        {
            using (var f = new Fixture(1514))
            {
                double now = 1000;
                var profile = ReferenceProfile();
                Require(profile.ActiveSession.Move(1), "Clock fixture starts from a real legal move");
                var page = f.Root.gameObject.AddComponent<SlidingPuzzlePage>();
                page.Initialize(f.Root, () => { }, done => done(), () => true, () => false, () => false,
                    f.Root.gameObject.AddComponent<GameFeedback>(), _ => { }, profile, realtime: () => now);
                page.Enter(); yield return null;
                double initial = page.Session.ElapsedSeconds;
                now += 1; yield return null;
                Near((float)(page.Session.ElapsedSeconds - initial), 1, "Injected foreground clock advances elapsed time by one second", .001f);
                page.SetForeground(false);
                double suspended = page.Session.ElapsedSeconds;
                // No yielded frame occurs during this interval, matching Android process suspension.
                now += 180;
                page.SetForeground(true); yield return null;
                Near((float)(page.Session.ElapsedSeconds - suspended), 0, "First resumed frame excludes all 180 suspended seconds", .001f);
                now += 2; yield return null;
                Near((float)(page.Session.ElapsedSeconds - suspended), 2, "Foreground timing resumes normally after suspension", .001f);
                report.AppendLine("PASS injected clock: +1 foreground second, no frames during 180 background seconds, zero resume jump, then +2 foreground seconds");
            }
        }

        private static IEnumerator ExtremeNumbersScenarios()
        {
            var game = new SlidingPuzzleGame(4, ReferenceCells);
            for (int i = 0; i < SlidingPuzzleGame.MaxActions; i++)
                if (!game.TryMove(1)) throw new InvalidOperationException("Maximum-counter fixture failed to make legal alternating moves at " + i);
            Require(game.Moves == 100000 && !game.Won, "Maximum-counter fixture has a real replayable 100000-action history");
            var saved = ReferenceProfile().Capture();
            saved.FreeSessions[1] = new SlidingPuzzleSessionData
            {
                LevelIndex = -1, Snapshot = game.GetSnapshot(),
                ElapsedSeconds = SlidingPuzzleSessionData.MaximumSeconds, ResultRecorded = false
            };
            Require(saved.TryValidate(out var reason), "Maximum-counter fixture satisfies the real save contract: " + reason);
            foreach (int height in new[] { 1514, 1320, 1582, 1150 })
            using (var f = new Fixture(height))
            {
                var page = NewPage(f, new SlidingPuzzleProfile(saved));
                page.Enter(); page.LayoutNow(); yield return null;
                Require(page.Session.Game.Moves == 100000 && page.Session.ElapsedSeconds == SlidingPuzzleSessionData.MaximumSeconds,
                    "Maximum counters survive profile restoration and page initialization");
                var time = f.Find("SlidingPuzzle time").GetComponent<Text>();
                var moves = f.Find("SlidingPuzzle moves").GetComponent<Text>();
                Require(time.text == "5259600:00" && moves.text == "100000", "Maximum counters render their complete values");
                CheckNumberLine(f, time); CheckNumberLine(f, moves);
                Near(moves.fontSize, 26 * f.Scale, "Six-digit move count retains the normal font size");
                Require(time.fontSize <= Mathf.RoundToInt(26 * f.Scale) && time.fontSize > 0, "Long timer only reduces its font as needed");
                CheckSingleLineFooter(f, f.Find("SlidingPuzzle footer").GetComponent<Text>());
                f.Capture("maximum-counters");
                report.AppendLine("PASS maximum counters 720x" + height + ": 5259600:00 and 100000 remain complete and on one line");
            }
        }

        private static IEnumerator RecoveryScenarios()
        {
            foreach (bool hasBackup in new[] { true, false })
            {
                string directory = Path.Combine(output, "isolated-recovery-" + Guid.NewGuid().ToString("N"));
                try
                {
                    using (var f = new Fixture(1150))
                    {
                        var store = new SlidingPuzzleSaveStore(directory);
                        Require(store.TrySave(ReferenceProfile().Capture(), out var error), "Recovery fixture saves a valid isolated profile: " + error);
                        if (hasBackup) File.Copy(store.SavePath, store.SavePath + ".bak");
                        File.WriteAllText(store.SavePath, "{invalid primary save");
                        var page = f.Root.gameObject.AddComponent<SlidingPuzzlePage>();
                        page.Initialize(f.Root, () => { }, done => done(), () => true, () => false, () => false,
                            f.Root.gameObject.AddComponent<GameFeedback>(), _ => { }, initialStore: store);
                        page.Enter(); page.LayoutNow(); yield return null;
                        var footer = f.Find("SlidingPuzzle footer").GetComponent<Text>();
                        Require(!string.IsNullOrWhiteSpace(footer.text) && !footer.text.Contains("首次移动"), "Actual recovery warning remains visible on the page");
                        CheckSingleLineFooter(f, footer);
                        if (hasBackup) Same(ReferenceCells, page.Session.Game.GetCells(), "Actual backup recovery restores the original permutation");
                        else Require(page.Session.Game.Size == 4 && page.Session.Game.Moves == 0, "Unrecoverable save starts a fresh free attempt");
                        f.Capture(hasBackup ? "save-recovered" : "save-unrecoverable");
                    }
                }
                finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            }
            report.AppendLine("PASS isolated real save recovery: backup and unrecoverable warnings fit one line on the short screen");
        }

        private static IEnumerator HostScenarios()
        {
            using (var f = new Fixture(1514))
            {
                var inert = new GameObject("Inactive isolated Sliding Puzzle host"); inert.SetActive(false);
                try
                {
                    var host = inert.AddComponent<AppController>(); var profile = ReferenceProfile();
                    Set(host, "canvasRoot", (RectTransform)f.Root.parent); Set(host, "data", new SaveData());
                    Set(host, "activePage", Enum.Parse(typeof(AppController).GetNestedType("Page", BindingFlags.NonPublic), "SlidingPuzzle"));
                    var status = UiFactory.Label(f.Root.parent, "", 0, 0, 1, 1, 10, UiFactory.Muted); status.gameObject.SetActive(false); Set(host, "status", status);
                    var feedback = inert.AddComponent<GameFeedback>(); Set(host, "feedback", feedback);
                    var page = f.Root.gameObject.AddComponent<SlidingPuzzlePage>(); Set(host, "slidingPuzzlePage", page);
                    page.Initialize(f.Root, () => { }, after => Invoke(host, "OpenSettings", after),
                        () => Get<GameObject>(host, "modal") == null && !Get<bool>(host, "paused") && Get<bool>(host, "focused"),
                        () => false, () => false, feedback, text => Invoke(host, "SetStatus", text), profile);
                    page.Enter(); page.LayoutNow(); yield return null;
                    page.Move(1); yield return new WaitForSeconds(.18f);
                    f.Click(f.Find("Settings"));
                    Require(Get<GameObject>(host, "modal") != null && page.Session.Blocked, "Actual shared settings blocks the Sliding Puzzle session");
                    f.Capture("host-settings");
                    Invoke(host, "OnApplicationPause", true);
                    Require(Get<GameObject>(host, "modal") == null && !page.Session.Foreground, "Host background closes shared settings and pauses the page");
                    Invoke(host, "OnApplicationPause", false);
                    Require(page.Session.Foreground && !page.Session.Blocked, "Host resume clears the stale settings gate");
                    int before = page.Session.Game.Moves; page.Move(5); yield return new WaitForSeconds(.18f);
                    Require(page.Session.Game.Moves == before + 1, "Board moves again after actual host settings/background cycle");
                    f.Click(f.Find("Settings")); Get<Action>(host, "dismissModal")();
                    Require(Get<GameObject>(host, "modal") == null && !page.Session.Blocked, "Host Back dismissal resumes the page");
                    report.AppendLine("PASS actual host: shared settings, pause/resume and Back callback; no player store created");
                }
                finally { Object.DestroyImmediate(inert); }
            }
            using (var f = new Fixture(1150))
            {
                var inert = new GameObject("Short-screen Sliding Puzzle host"); inert.SetActive(false);
                try
                {
                    var host = inert.AddComponent<AppController>();
                    var safe = f.Root.gameObject.AddComponent<SafeDesignFrame>(); safe.Initialize(f.Root, (RectTransform)f.Root.parent);
                    Set(host, "frame", f.Root); Set(host, "canvasRoot", (RectTransform)f.Root.parent); Set(host, "data", new SaveData());
                    Set(host, "feedback", inert.AddComponent<GameFeedback>());
                    Invoke(host, "BuildSlidingPuzzle", ReferenceProfile(), null);
                    Require(!safe.WidthOnly, "Constructing Sliding Puzzle restores the lobby frame policy");
                    safe.WidthOnly = true;
                    Set(host, "activePage", Enum.Parse(typeof(AppController).GetNestedType("Page", BindingFlags.NonPublic), "SlidingPuzzle"));
                    var page = Get<SlidingPuzzlePage>(host, "slidingPuzzlePage"); page.Enter(); page.LayoutNow(); yield return null;
                    f.CheckHit(f.Find("Settings")); f.CheckHit(f.Find("Restart"));
                    CheckBoard(f, page, f.Scale); f.Capture("host-short-screen");
                    report.AppendLine("PASS short-screen host construction: final width-only frame, complete board and reachable controls");
                }
                finally { Object.DestroyImmediate(inert); }
            }
        }

        private static void CheckGeometry(Fixture f, SlidingPuzzlePage page)
        {
            float scale = f.Scale, left = (f.Width - 390 * scale) * .5f;
            Rect board = f.Bounds(f.Find("SlidingPuzzle board")), title = f.Bounds(f.Find("SlidingPuzzle title"));
            Near(board.xMin, left + 28 * scale, "Board left matches reference");
            Near(board.width, 334 * scale, "Board width matches reference");
            Near(f.Height - board.yMax, 384.35f * scale, "Board top matches reference");
            Near(title.xMin, left + 28 * scale, "Title shares the content left edge");
            Near(f.Height - title.yMax, 89 * scale, "Title top matches reference");
            Near(f.Find("SlidingPuzzle title").GetComponent<Text>().fontSize, 33 * scale, "Title font matches reference");
            Near(f.Find("SlidingPuzzle time").GetComponent<Text>().fontSize, 26 * scale, "Timer font matches reference");
            Near(f.Find("SlidingPuzzle moves").GetComponent<Text>().fontSize, 26 * scale, "Move font matches reference");
            Rect undo = f.Bounds(f.Find("Undo")), restart = f.Bounds(f.Find("Restart"));
            Near(undo.width, restart.width, "Two actions have equal widths");
            Near(undo.yMin, restart.yMin, "Actions share a baseline");
            Near(f.Height - undo.yMax, 735.35f * scale, "Action top matches reference");
            Require(undo.yMin >= 0 && restart.xMax <= f.Width + 1, "Short-screen actions remain inside the safe surface");
            var footer = f.Find("SlidingPuzzle footer").GetComponent<Text>();
            Require(!footer.text.Contains("\n") && !footer.text.Contains("一格一格，回到恰好的位置"), "Footer is one line and the removed slogan is absent");
            CheckSingleLineFooter(f, footer);
            ColorNear(board: f.Find("SlidingPuzzle board").GetComponent<Image>().color, expected: UiFactory.Hex("BBCBB6"), reason: "Board tray matches prototype color");
            CheckBoard(f, page, scale);
            report.AppendLine("GEOMETRY 720x" + f.Height + " scale=" + scale + " board=" + board + " title=" + title + " actions=" + undo + ";" + restart);
        }
        private static void CheckBoard(Fixture f, SlidingPuzzlePage page, float scale)
        {
            var boardRect = f.Find("SlidingPuzzle board"); Rect board = f.Bounds(boardRect);
            int n = page.Session.Game.Size; var cells = page.Session.Game.GetCells();
            float pad = board.width * .0263f, gap = board.width * .0197f, cell = (board.width - pad * 2 - gap * (n - 1)) / n;
            Near(board.width, board.height, "Board stays square");
            Require(board.yMin >= 0 && board.yMax <= f.Height + 1 && board.xMin >= 0 && board.xMax <= f.Width + 1, "Entire board fits the safe surface");
            for (int value = 1; value < n * n; value++)
            {
                var tile = f.Find("Tile " + value); Rect rect = f.Bounds(tile); int index = Array.IndexOf(cells, value);
                Near(rect.width, cell, "Tile width preserves prototype percentage gap");
                Near(rect.xMin, board.xMin + pad + index % n * (cell + gap), "Tile column matches model");
                Near(board.yMax - rect.yMax, pad + index / n * (cell + gap), "Tile row matches model");
                ColorNear(tile.GetComponent<Image>().color, UiFactory.Hex("F5F1D9"), "Tile matches prototype color");
                Require(!tile.GetComponent<Image>().raycastTarget, "Tile visuals cannot create a second input owner");
                var label = tile.GetComponentInChildren<Text>();
                Near(label.fontSize, (n == 3 ? 36 : n == 4 ? 29 : 24) * scale, "Tile font matches size variant");
                Require(label.text == value.ToString() && label.font == UiFactory.Font, "Tile number uses the shared font and correct value");
                Require(f.HitWithin(rect.center, boardRect), "Rendered tile center reaches the single board input surface");
                Require(f.HitWithin(new Vector2(rect.xMin + 2, rect.yMin + 2), boardRect), "Rendered tile edge reaches the single board input surface");
            }
        }
        private static void CheckCampaignGeometry(Fixture f, SlidingPuzzleProfile profile)
        {
            float scale = f.Scale, left = (f.Width - 390 * scale) * .5f;
            Rect summary = f.Bounds(f.Find("Chapter summary"));
            Near(summary.xMin, left + 28 * scale, "Chapter summary left matches the reference");
            Near(f.Height - summary.yMax, 249.05f * scale, "Chapter summary top matches the reference");
            Near(summary.width, 334 * scale, "Chapter summary width matches the reference");
            Near(summary.height, 111.6f * scale, "Chapter summary height matches the reference");
            Rect title = f.Bounds(f.Find("Chapter title")), progress = f.Bounds(f.Find("Chapter progress"));
            Near(title.xMin - summary.xMin, 17 * scale, "Chapter title inset matches the reference");
            Near(summary.yMax - title.yMax, 42.4f * scale, "Chapter title top matches the reference");
            Near(title.width, 300 * scale, "Chapter title width matches the reference");
            Near(title.height, 28 * scale, "Chapter title height matches the reference");
            Near(progress.xMin - summary.xMin, 17 * scale, "Chapter progress inset matches the reference");
            Near(summary.yMax - progress.yMax, 75.4f * scale, "Chapter progress top matches the reference");
            Near(progress.width, 300 * scale, "Chapter progress width matches the reference");
            Near(progress.height, 19.2f * scale, "Chapter progress height matches the reference");
            Rect firstLevel = f.Bounds(f.Find("Level " + profile.SelectedChapter * 6));
            Near(f.Height - firstLevel.yMax, 380.64f * scale, "Level grid starts at the approved reference position");
            int row = 0;
            for (int chapter = 0; chapter < 3; chapter++)
            {
                if (chapter == profile.SelectedChapter) continue;
                var control = f.Find("Chapter " + chapter); Rect bounds = f.Bounds(control);
                Rect divider = f.Bounds((RectTransform)control.Find("Chapter divider"));
                Rect rowTitle = f.Bounds((RectTransform)control.Find("Chapter row title"));
                Rect description = f.Bounds((RectTransform)control.Find("Chapter row description"));
                Near(f.Height - divider.yMax, (570.64f + row * 83.4f) * scale, "Chapter divider matches the approved vertical position");
                Near(bounds.height, 83.4f * scale, "Chapter row height matches the reference");
                Near(bounds.yMax - rowTitle.yMax, 19 * scale, "Chapter row title top matches the reference");
                Near(rowTitle.height, 24 * scale, "Chapter row title height matches the reference");
                Near(bounds.yMax - description.yMax, 44.6f * scale, "Chapter description top matches the reference");
                Near(description.height, 19.2f * scale, "Chapter description height matches the reference");
                row++;
            }
        }
        private static void CheckFonts(Fixture f)
        {
            foreach (var text in f.Root.GetComponentsInChildren<Text>()) Require(text.font == UiFactory.Font, "All page text uses the bundled shared font");
        }
        private static void CheckSizeLabels(Fixture f)
        {
            f.Flush();
            for (int size = 3; size <= 5; size++)
            {
                var label = f.Find("Size " + size).GetComponentInChildren<Text>();
                Require(label.cachedTextGenerator.lineCount == 1, "Size selector remains one line: " + size);
                Require(label.preferredWidth <= label.rectTransform.rect.width + 1, "Size selector text fits without overflow: " + size);
            }
        }
        private static void CheckGoal(Fixture f, int size)
        {
            Rect goal = f.Bounds(f.Find("Goal board"));
            for (int value = 1; value < size * size; value++)
            {
                var tile = f.Find("Goal tile " + value); Rect bounds = f.Bounds(tile);
                Require(tile.GetComponentInChildren<Text>().text == value.ToString(), "Rules show each ordered target number");
                Require(bounds.xMin >= goal.xMin && bounds.xMax <= goal.xMax && bounds.yMin >= goal.yMin && bounds.yMax <= goal.yMax, "Rules target tiles fit their miniature board");
            }
        }
        private static void CheckSingleLineFooter(Fixture f, Text footer)
        {
            f.Flush();
            Require(!footer.text.Contains("\n") && footer.cachedTextGenerator.lineCount <= 1, "Footer renders as a single line");
            Require(footer.preferredWidth <= footer.rectTransform.rect.width + 1, "Footer text fits its width without clipping or overflow");
            Require(f.Bounds(footer.rectTransform).yMin >= 0, "Footer remains visible");
        }
        private static void CheckNumberLine(Fixture f, Text text)
        {
            f.Flush();
            Require(!text.text.Contains("\n") && text.cachedTextGenerator.lineCount == 1, "Counter renders on exactly one line: " + text.name);
            Require(text.preferredWidth <= text.rectTransform.rect.width + 1, "Complete counter fits its available width: " + text.name);
            Require(text.font == UiFactory.Font, "Counter retains the shared font");
        }
        private static void ColorNear(Color board, Color expected, string reason)
        { Require(Math.Abs(board.r - expected.r) < .005f && Math.Abs(board.g - expected.g) < .005f && Math.Abs(board.b - expected.b) < .005f, reason); }
        private static void Near(float value, float expected, string reason, float tolerance = 1.1f)
        { Require(Math.Abs(value - expected) < tolerance, reason + ": " + value + " vs " + expected); }
        private static void Same(int[] expected, int[] actual, string reason)
        { Require(expected.Length == actual.Length, reason + " length"); for (int i = 0; i < expected.Length; i++) Require(expected[i] == actual[i], reason + " at " + i); }
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
            public readonly int Height, Width;
            public float Scale { get { return Mathf.Min(Width / 390f, Height / 820f); } }
            private readonly GameObject owner;
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly EventSystem events;
            public Fixture(int height, int width = 720)
            {
                Height = height; Width = width; owner = new GameObject("Sliding Puzzle UI fixture");
                var canvas = new GameObject("Fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
                canvas.SetParent(owner.transform, false); canvas.sizeDelta = new Vector2(width, height);
                camera = new GameObject("Fixture camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>(); camera.transform.SetParent(owner.transform, false);
                camera.enabled = false; camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
                camera.orthographicSize = height * .5f; camera.aspect = (float)width / height; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UiFactory.Paper;
                target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; canvas.GetComponent<Canvas>().worldCamera = camera;
                Root = UiFactory.Rect(canvas, "Sliding Puzzle fixture page", 0, 0, width, height); UiFactory.Stretch(Root);
                events = new GameObject("Fixture events", typeof(EventSystem)).GetComponent<EventSystem>(); events.transform.SetParent(owner.transform, false);
            }
            public RectTransform Find(string name)
            {
                foreach (RectTransform rect in Root.GetComponentsInChildren<RectTransform>()) if (rect.name == name) return rect;
                throw new InvalidOperationException("Missing visible production element: " + name);
            }
            public bool ContainsText(string fragment)
            { foreach (Text text in Root.GetComponentsInChildren<Text>()) if (text.text.Contains(fragment)) return true; return false; }
            public void Flush() { Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); }
            public Vector2 Center(RectTransform rect) { return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)); }
            public Rect Bounds(RectTransform rect)
            {
                Flush(); var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]); var b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
            private List<RaycastResult> Hits(Vector2 position, out PointerEventData data, int pointerId = -1)
            {
                Flush(); data = new PointerEventData(events) { position = position, pointerId = pointerId, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
                if (hits.Count > 0) data.pointerCurrentRaycast = data.pointerPressRaycast = hits[0];
                return hits;
            }
            public bool HitWithin(Vector2 position, RectTransform expected)
            { var hits = Hits(position, out var data); return hits.Count > 0 && (hits[0].gameObject.transform == expected || hits[0].gameObject.transform.IsChildOf(expected)); }
            public void CheckHit(RectTransform rect) { Require(HitWithin(Center(rect), rect), "Rendered raycast reaches " + rect.name); }
            public PointerEventData Begin(RectTransform visual, RectTransform receiver, int pointerId = -1)
            {
                var hits = Hits(Center(visual), out var data, pointerId);
                Require(hits.Count > 0 && (hits[0].gameObject.transform == receiver || hits[0].gameObject.transform.IsChildOf(receiver)), "Rendered pointer reaches " + receiver.name);
                data.pressPosition = data.position;
                var pressed = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
                data.pointerPress = pressed ?? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                return data;
            }
            public void Release(PointerEventData data, Vector2 position)
            {
                data.position = position;
                if (data.pointerPress != null) ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
            }
            public void TapBoard(RectTransform tile, RectTransform board) { var data = Begin(tile, board); Release(data, data.position); }
            public void Click(RectTransform rect)
            {
                var data = Begin(rect, rect); var clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(data.pointerCurrentRaycast.gameObject);
                Release(data, data.position);
                if (clicked != null && data.pointerPress == clicked) ExecuteEvents.Execute(clicked, data, ExecuteEvents.pointerClickHandler);
            }
            public void Capture(string name)
            {
                Flush(); var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                try { image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(output, name + "-" + Width + "x" + Height + ".png"), image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }
            public void Dispose() { camera.targetTexture = null; if (RenderTexture.active == target) RenderTexture.active = null; Object.DestroyImmediate(owner); target.Release(); Object.DestroyImmediate(target); }
        }
    }
}
