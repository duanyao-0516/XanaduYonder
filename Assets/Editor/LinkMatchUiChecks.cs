using System;
using System.Collections;
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
using XanaduYonder.Core;
using XanaduYonder.Core.LinkMatch;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.LinkMatch;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Uses production UGUI, rendered raycasts and isolated temporary storage, never player files.
    [InitializeOnLoad]
    public static class LinkMatchUiChecks
    {
        private const string Pending = "Xanadu.LinkMatchUiChecks.Pending";
        private static readonly StringBuilder Report = new StringBuilder();
        private static readonly List<string> RuntimeErrors = new List<string>();
        private static int checks;
        private static string output;
        static LinkMatchUiChecks() { EditorApplication.playModeStateChanged += StateChanged; }
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
            new GameObject("LinkMatch checks runner").AddComponent<Runner>().StartCoroutine(Execute());
        }
        private sealed class Runner : MonoBehaviour { }
        private static IEnumerator Execute()
        {
            yield return null;
            checks = 0; Report.Clear(); RuntimeErrors.Clear(); Application.logMessageReceived += LogReceived;
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/validation/link-match-ui"));
            Directory.CreateDirectory(output);
            Report.AppendLine("Unity " + Application.unityVersion + "; production UGUI; Camera.Render; isolated temporary stores");
            var scenarios = Scenarios();
            while (true)
            {
                bool next = false; object current = null; Exception failure = null;
                try { next = scenarios.MoveNext(); if (next) current = scenarios.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null || !next)
                {
                    (scenarios as IDisposable)?.Dispose();
                    Application.logMessageReceived -= LogReceived;
                    if (failure == null && RuntimeErrors.Count > 0) failure = new InvalidOperationException("Unexpected Unity runtime errors:\n" + string.Join("\n", RuntimeErrors));
                    Report.AppendLine(failure == null ? "LINK_MATCH_UI_CHECKS_PASS: " + checks + " assertions" : "LINK_MATCH_UI_CHECKS_FAIL after " + checks + " assertions\n" + failure);
                    File.WriteAllText(Path.Combine(output, "report.txt"), Report.ToString());
                    Debug.Log(Report.ToString()); EditorApplication.Exit(failure == null ? 0 : 1); yield break;
                }
                yield return current;
            }
        }
        private static void LogReceived(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) RuntimeErrors.Add(message + "\n" + stack);
        }
        private static IEnumerator Scenarios()
        {
            UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            Require(UiFactory.Font != null, "Bundled CJK font loads");
            Require(LinkMatchBoardView.FruitIds.Length == 48 && LinkMatchLevel.Count == 5, "Five levels use the complete 48-pattern catalog");
            foreach (string fruit in LinkMatchBoardView.FruitIds)
            {
                var texture = Resources.Load<Texture2D>("LinkMatch/Sprites/" + fruit);
                Require(texture != null && texture.width > 0, "Approved fruit loads: " + fruit);
                Require(texture.filterMode == FilterMode.Bilinear && texture.wrapMode == TextureWrapMode.Clamp && texture.mipmapCount == 1, "Fruit import settings preserve UI quality: " + fruit + " (" + texture.filterMode + ", " + texture.wrapMode + ", mips=" + texture.mipmapCount + ")");
            }
            foreach (Vector2Int size in new[] { new Vector2Int(388, 784), new Vector2Int(360, 640), new Vector2Int(720, 1455) })
            using (var f = new Fixture(size.x, size.y))
            {
                var profile = new LinkMatchProfile(); bool allowed = true; Action finishSettings = null; int home = 0;
                var page = f.MakePage(profile, () => home++, done => { allowed = false; finishSettings = done; }, () => allowed);
                page.Enter(); page.LayoutNow(); yield return null;
                Require(page.IsEntry && CountSlots(profile) == 0, "New lobby route opens entry without an attempt");
                page.SelectTile(0); page.Save(); page.Suspend(); page.SetForeground(false); page.SetForeground(true);
                page.Leave(); page.Save(); page.Enter();
                Require(page.IsEntry && CountSlots(profile) == 0, "Entry clicks, saves, background and reentry never fabricate progress");
                CheckEntry(f, page); f.Capture("entry-new");
                f.Click(f.Find("Mode challenge"));
                Require(profile.SelectedMode == LinkMatchMode.Challenge && CountSlots(profile) == 0, "Mode selection only updates the selected mode");
                f.Capture("entry-challenge");
                f.Click(f.Find("Entry level level-03")); page.LayoutNow(); yield return null;
                Require(!page.IsEntry && page.Session.Mode == LinkMatchMode.Challenge && page.Session.Game.Level.Id == "level-03" && page.Session.Game.Remaining == 48, "Selected challenge level opens its actual 48-tile game");
                Require(page.CompletedCount == 0 && !page.Session.IsTerminal && !page.HasDialog, "Last level cannot display a win before it is cleared");
                CheckGeometry(f, page, false); f.Capture("game-challenge-48");
                foreach (string name in new[] { "Home", "Book", "Settings", "Hint", "Shuffle", "Pause" }) f.CheckHit(f.Find(name));
                var match = page.Session.Game.FindMatch(); Require(match != null, "Generated production board has a real move");
                int remaining = page.Session.Game.Remaining;
                var pressed = f.PressTile(page, match.First);
                f.DragTile(page, pressed, match.Second); f.DragTile(page, pressed, match.First); f.Release(pressed);
                f.ClickTile(page, match.Second);
                Require(page.Session.Game.Remaining == remaining, "Dragging outside and back permanently cancels a rendered tile press"); f.ClickTile(page, match.Second);
                var firstPointer = f.PressTile(page, match.First, 1); var secondPointer = f.PressTile(page, match.Second, 2);
                f.Release(firstPointer); f.Release(secondPointer); f.ClickTile(page, match.Second);
                Require(page.Session.Game.Remaining == remaining, "A second real pointer cancels both pending tile submissions"); f.ClickTile(page, match.Second);
                pressed = f.PressTile(page, match.First); page.LayoutNow(); f.Release(pressed); f.ClickTile(page, match.Second);
                Require(page.Session.Game.Remaining == remaining, "Layout reconstruction cancels a pending old-surface release"); f.ClickTile(page, match.Second);
                f.ClickTile(page, match.First); pressed = f.PressTile(page, match.Second);
                Vector2 originalSize = f.Root.sizeDelta; f.Root.sizeDelta += new Vector2(0, 2);
                // EventSystem runs before the page's Update: no LayoutNow or yielded frame here.
                f.Release(pressed);
                Require(page.Session.Game.Remaining == remaining, "Raw root resize cancels release before the page Update can rebuild");
                f.Root.sizeDelta = originalSize; page.ShowPause(); page.ResumeGame(); page.LayoutNow();
                pressed = f.PressTile(page, match.First); page.ShowPause(); f.Release(pressed); RequireConcealed(f, page, "popup during press");
                f.ClickBackdrop(); Require(page.HasDialog && page.BoardConcealed, "Clicking the actual dialog backdrop cannot resume play");
                page.ResumeGame(); f.ClickTile(page, match.Second);
                Require(page.Session.Game.Remaining == remaining, "Popup cancellation cannot select a stale held tile after resume"); f.ClickTile(page, match.Second);
                f.ClickTile(page, match.First); Require(page.Session.Game.Remaining == remaining, "First tile selects without removing a pair");
                f.ClickTile(page, match.First); f.ClickTile(page, match.Second);
                Require(page.Session.Game.Remaining == remaining, "Clicking selected tile cancels rather than committing it");
                f.ClickTile(page, match.Second); f.ClickTile(page, match.First); f.ClickTile(page, match.Second);
                Require(page.Session.Game.Remaining == remaining - 2, "Real rendered down/up on valid second tile removes exactly two");
                var disk = f.Store.Load(out var warning);
                Require(warning == null && disk != null && Remaining(disk.Slots[LinkMatchSaveData.SlotIndex(LinkMatchMode.Challenge, "level-03")]) == remaining - 2, "Successful pair is already committed in the isolated save file");
                f.Capture("matched-path"); yield return new WaitForSecondsRealtime(.5f);
                double before = page.Session.ElapsedSeconds; yield return new WaitForSecondsRealtime(.12f);
                Require(page.Session.ElapsedSeconds > before, "Visible unblocked game advances its actual frame clock");
                f.Click(f.Find("Pause")); RequireConcealed(f, page, "pause"); before = page.Session.ElapsedSeconds;
                yield return new WaitForSecondsRealtime(.12f);
                Require(page.Session.ElapsedSeconds == before, "Pause excludes elapsed frame time"); f.Capture("pause");
                page.Back(); RequireConcealed(f, page, "pause Back");
                string pausedAttempt = page.Session.AttemptId; f.Click(f.Find("Dialog close"));
                Require(page.IsEntry && !page.Session.CanInteract && page.Session.AttemptId == pausedAttempt, "Pause close returns to entry without resuming or replacing progress");
                page.ContinueGame(); page.ShowPause(); page.ResumeGame();
                Require(!page.BoardConcealed && page.Session.CanInteract, "Explicit resume reveals board and restores input");
                f.Click(f.Find("Book")); RequireConcealed(f, page, "help"); before = page.Session.ElapsedSeconds;
                yield return new WaitForSecondsRealtime(.12f);
                Require(page.Session.ElapsedSeconds == before, "Help excludes elapsed frame time"); f.Capture("help");
                f.Click(f.Find("Dialog close")); RequireConcealed(f, page, "help close"); page.ResumeGame();
                f.Click(f.Find("Settings")); Require(finishSettings != null, "Production Settings invokes the shared settings callback");
                RequireConcealed(f, page, "settings"); before = page.Session.ElapsedSeconds;
                yield return new WaitForSecondsRealtime(.12f);
                Require(page.Session.ElapsedSeconds == before, "Shared settings excludes elapsed frame time");
                allowed = true; finishSettings(); RequireConcealed(f, page, "closed settings"); page.ResumeGame();
                page.SetForeground(false); RequireConcealed(f, page, "background"); before = page.Session.ElapsedSeconds;
                yield return new WaitForSecondsRealtime(.12f);
                page.SetForeground(true); RequireConcealed(f, page, "foreground return");
                Require(page.Session.ElapsedSeconds == before, "Background never catches up wall-clock time"); page.ResumeGame();
                for (int i = 0; i < 4; i++) f.Click(f.Find("Hint"));
                Require(page.Session.HintsUsed == 3 && page.Session.HintsRemaining == 0 && page.Session.Game.Remaining == remaining - 2, "Challenge hint quota is real and never removes tiles");
                for (int i = 0; i < 3; i++) { f.Click(f.Find("Shuffle")); yield return new WaitForSecondsRealtime(.15f); }
                Require(page.Session.ShufflesUsed == 2 && page.Session.ShufflesRemaining == 0 && page.Session.Game.Remaining == remaining - 2, "Challenge shuffle quota is real and retains remaining count");
                string attempt = page.Session.AttemptId; page.ShowPause(); page.ConfirmRestart();
                Require(page.Session.AttemptId == attempt && page.HasDialog, "Restart opens confirmation before replacing progress");
                f.Click(f.Find("Dialog secondary"));
                Require(page.Session.AttemptId == attempt, "Cancelling restart retains the attempt"); page.ResumeGame();
                int hostRetries = 0; page.RetryHostSave = () => { hostRetries++; page.SetHostSaveWarning(null); };
                page.SetHostSaveWarning("公共设置尚未保存"); page.LayoutNow();
                Require(HasText(f, "公共设置尚未保存"), "Common settings save failure remains visible in the page");
                CheckGeometry(f, page, true); f.Capture("save-warning"); f.Click(f.Find("Retry save")); page.LayoutNow();
                Require(hostRetries == 1 && !HasText(f, "公共设置尚未保存") && !page.HasDialog, "Visible retry invokes host persistence and clears its warning without opening settings");
                page.ShowEntry(); page.LayoutNow(); yield return null;
                Require(page.IsEntry && profile.GetLatestActive().AttemptId == attempt, "Entry preserves the active game for continuation");
                f.Capture("entry-resume"); f.Click(f.Find("Continue game"));
                Require(page.Session.AttemptId == attempt && page.Session.Game.Remaining == remaining - 2, "Continue restores the same attempt and remaining board");
                match = page.Session.Game.FindMatch(); f.ClickTile(page, match.First); f.ClickTile(page, match.Second);
                remaining = page.Session.Game.Remaining; page.Leave(); before = page.Session.ElapsedSeconds;
                page.SelectTile(0); page.SelectTile(1); yield return new WaitForSecondsRealtime(.5f);
                Require(page.Session.Game.Remaining == remaining && page.Session.ElapsedSeconds == before, "Leaving during path feedback cancels future input and clock work");
                page.Enter(); page.ContinueGame(); page.LayoutNow(); yield return null; f.Click(f.Find("Home"));
                Require(home == 1, "Home invokes the lobby route once");
                Report.AppendLine("PASS " + size.x + "x" + size.y + ": actual entry/game controls, board geometry, modal concealment, frame clock, quotas, persistent save and lifecycle");
            }

            // The exact approved JavaScript fixture is confined to editor validation.
            foreach (Vector2Int size in new[] { new Vector2Int(388, 784), new Vector2Int(360, 640) })
            using (var f = new Fixture(size.x, size.y))
            {
                var level = LinkMatchLevel.Get(3); var state = Snapshot(level, LinkMatchMode.Relaxed, PrototypeFixture(level), 32);
                var page = f.MakePage(ProfileWith(state)); page.Enter(); page.ContinueGame(); page.LayoutNow();
                yield return null; CheckGeometry(f, page, false); f.Capture("prototype-fixture-48");
                int first = level.Columns, second = level.Columns * 4;
                var probe = new LinkMatchGame(level, state.Tiles);
                Require(probe.TryMatch(first, second, out var path) && path.Turns == 2 && path.Points[1].X == -1, "Approved fixture has the real two-turn outside-left connection");
                f.ClickTile(page, first); f.Capture("prototype-selected"); f.ClickTile(page, second);
                Require(page.Session.Game.Remaining == 46, "Approved outer-route fixture uses the production matcher");
                var route = f.Find("LinkMatch path").GetComponent<LinkMatchGraphic>();
                Require(route.Points != null && route.Points.Length == path.Points.Count, "Successful match displays the actual production path");
                foreach (var point in route.Points) Require(point.x >= 0 && point.y >= 0 && point.x <= page.Surface.rect.width && point.y <= page.Surface.rect.height, "Outer route stays inside board clipping bounds");
                f.Capture("prototype-outer-route");
                page.SetHostSaveWarning("公共设置尚未保存"); page.LayoutNow(); CheckGeometry(f, page, true);
                if (size.x == 360) Near((float)page.Layout.Cell, 48.25f, "Compact warning retains the approved 48.25-pixel target");
                f.Capture("prototype-warning-48");
            }

            foreach (Vector2Int size in new[] { new Vector2Int(388, 784), new Vector2Int(360, 640) })
            foreach (int number in new[] { 4, 5 })
            using (var f = new Fixture(size.x, size.y))
            {
                var level = LinkMatchLevel.Get(number); int total = level.Columns * level.Rows;
                var page = f.MakePage(ProfileWith(Snapshot(level, LinkMatchMode.Challenge, PrototypeFixture(level), 32)));
                page.Enter(); page.ContinueGame(); page.LayoutNow(); yield return null;
                CheckGeometry(f, page, false); f.Capture("expanded-" + total);
                Require(page.Session.Game.Remaining == total && page.Session.Game.GetTiles().Max() == level.Kinds - 1, "Dense board contains every approved pattern");
                var original = new LinkMatchLayout(size.x, size.y, false, 6, 8);
                if (number == 5) Near((float)page.Layout.Cell, (float)original.Cell / 2, "Fifth level halves the actual reference cell edge");
                var pair = page.Session.Game.FindMatch(); var pressed = f.PressTile(page, pair.First);
                f.DragTile(page, pressed, pair.Second); f.DragTile(page, pressed, pair.First); f.Release(pressed); f.ClickTile(page, pair.Second);
                Require(page.Session.Game.Remaining == total, "Dense drag-out cannot select a stale first tile"); f.ClickTile(page, pair.Second);
                var a = f.PressTile(page, pair.First, 1); var b = f.PressTile(page, pair.Second, 2);
                f.Release(a); f.Release(b); f.ClickTile(page, pair.Second);
                Require(page.Session.Game.Remaining == total, "Dense second pointer cancels pending matching tiles"); f.ClickTile(page, pair.Second);
                f.ClickTile(page, pair.First); pressed = f.PressTile(page, pair.Second);
                var sizeDelta = f.Root.sizeDelta; f.Root.sizeDelta += new Vector2(0, 2); f.Release(pressed);
                Require(page.Session.Game.Remaining == total, "Dense resize before Update cancels the release");
                f.Root.sizeDelta = sizeDelta; page.LayoutNow();
                // Same fixed fixture as the prototype, with a valid outside-left route.
                int first = level.Columns, second = level.Columns * 4;
                f.ClickTile(page, first); f.Capture("expanded-selected-" + total); f.ClickTile(page, second);
                Require(page.Session.Game.Remaining == total - 2, "Dense actual raycast submits exactly one valid pair");
                var route = f.Find("LinkMatch path").GetComponent<LinkMatchGraphic>();
                Require(route.Points != null && route.Points.Length == 4, "Dense successful match displays its actual two-turn route");
                foreach (var point in route.Points) Require(point.x >= 0 && point.y >= 0 && point.x <= page.Surface.rect.width && point.y <= page.Surface.rect.height, "Dense outside route stays unclipped");
                f.Capture("expanded-outer-route-" + total); page.ShowPause(); RequireConcealed(f, page, "dense pause"); page.ResumeGame();
                page.SetHostSaveWarning("公共设置尚未保存"); CheckGeometry(f, page, true); f.Capture("expanded-warning-" + total);
                if (number == 5 && size.x == 360) Near((float)page.Layout.Cell, 24.125f, "Fifth-level warning keeps exact half-edge targets");
                Report.AppendLine("PASS expanded " + total + " tiles at " + size.x + "x" + size.y + ": all assets, real click cancellation, outside route, count width and warning layout");
            }

            using (var f = new Fixture(388, 784))
            {
                var profile = new LinkMatchProfile(); var attempts = new string[LinkMatchLevel.Count * 2]; var counts = new int[LinkMatchLevel.Count * 2];
                var page = f.MakePage(profile); page.Enter();
                for (int i = 0; i < LinkMatchLevel.Count * 2; i++)
                {
                    var mode = (LinkMatchMode)(i / LinkMatchLevel.Count); string id = LinkMatchLevel.Get(i % LinkMatchLevel.Count + 1).Id;
                    page.StartLevel(mode, id); var pair = page.Session.Game.FindMatch();
                    page.SelectTile(pair.First); page.SelectTile(pair.Second); attempts[i] = page.Session.AttemptId; counts[i] = page.Session.Game.Remaining;
                    page.ShowEntry();
                }
                Require(CountSlots(profile) == 10, "All ten mode by level slots are independent");
                for (int i = 0; i < LinkMatchLevel.Count * 2; i++)
                {
                    page.SelectMode((LinkMatchMode)(i / LinkMatchLevel.Count)); page.LayoutNow(); yield return null;
                    var entry = f.Find("Entry level " + LinkMatchLevel.Get(i % LinkMatchLevel.Count + 1).Id); f.RevealEntry(entry); f.Click(entry);
                    Require(page.Session.AttemptId == attempts[i] && page.Session.Game.Remaining == counts[i], "Entry level resumes the exact slot " + i);
                    page.ShowEntry();
                }
                Report.AppendLine("PASS ten-slot entry resume: mode, level, attempt identity and exact remaining state");
            }

            foreach (int number in new[] { 3, 4 })
            using (var f = new Fixture(388, 784))
            {
                var level = LinkMatchLevel.Get(number); var tiles = new int[level.Columns * level.Rows];
                for (int i = 0; i < tiles.Length; i++) tiles[i] = -1; tiles[0] = tiles[1] = 0;
                var profile = ProfileWith(Snapshot(level, LinkMatchMode.Relaxed, tiles, 12));
                var next = profile.Start(LinkMatchMode.Relaxed, LinkMatchLevel.Get(number + 1).Id, 700); string attempt = next.AttemptId; profile.Capture(next);
                var page = f.MakePage(profile); page.Enter(); page.StartLevel(LinkMatchMode.Relaxed, level.Id);
                f.ClickTile(page, 0); f.ClickTile(page, 1); yield return new WaitForSecondsRealtime(.3f); page.CheckEndState();
                Require(HasText(f, "继续下一关"), "Completed level " + number + " now offers the existing next attempt");
                f.Click(f.Find("Dialog primary"));
                Require(page.Session.Game.Level.Number == number + 1 && page.Session.AttemptId == attempt, "Next action preserves the larger level's existing slot");
            }

            foreach (bool timeout in new[] { false, true })
            using (var f = new Fixture(388, 784))
            {
                var level = LinkMatchLevel.Get(LinkMatchLevel.Count); var tiles = new int[level.Columns * level.Rows];
                for (int i = 0; i < tiles.Length; i++) tiles[i] = -1;
                tiles[0] = tiles[1] = 0;
                var state = Snapshot(level, LinkMatchMode.Challenge, tiles, timeout ? level.ChallengeSeconds - .05 : 12);
                var page = f.MakePage(ProfileWith(state)); page.Enter(); page.ContinueGame(); page.LayoutNow();
                Require(!page.Session.IsTerminal && page.CompletedCount == 0 && !page.HasDialog, "Two-tile last-level fixture starts as an unfinished game");
                if (timeout)
                {
                    // Production Update must expire the clock before any further tile submission.
                    yield return new WaitForSecondsRealtime(.15f);
                    page.SelectTile(0); page.SelectTile(1); page.CheckEndState();
                    Require(page.Session.Outcome == LinkMatchOutcome.TimedOut && page.Session.Game.Remaining == 2 && page.CompletedCount == 0, "Deadline wins over a late final pair");
                    Require(page.Profile.GetRecord(LinkMatchMode.Challenge, level.Id).Losses == 1, "Timeout records exactly one loss");
                }
                else
                {
                    f.ClickTile(page, 0); f.ClickTile(page, 1);
                    Require(page.Session.Outcome == LinkMatchOutcome.Won && page.CompletedCount == 1, "The actual final pair completes the last level");
                    Require(page.Profile.GetRecord(LinkMatchMode.Challenge, level.Id).Wins == 1, "Actual terminal match records exactly one win");
                }
                var disk = f.Store.Load(out var warning); var saved = disk.Slots[LinkMatchSaveData.SlotIndex(LinkMatchMode.Challenge, level.Id)];
                Require(warning == null && saved.Outcome == page.Session.Outcome && saved.ResultRecorded, "Terminal board, clock and record reach disk before feedback finishes");
                page.LayoutNow(); page.CheckEndState(); yield return new WaitForSecondsRealtime(.5f);
                Require(page.HasDialog && page.BoardConcealed, "Actual terminal result is displayed and conceals the board");
                if (!timeout) Require(!HasText(f, "下一关"), "Final-level result has no next-level action");
                f.Capture(timeout ? "timeout" : "last-level-completed"); page.Save(); page.Save(); page.CheckEndState();
                var record = page.Profile.GetRecord(LinkMatchMode.Challenge, level.Id);
                Require(timeout ? record.Losses == 1 : record.Wins == 1, "Repeated terminal saves never recount the result");
                Report.AppendLine("PASS real " + (timeout ? "deadline timeout" : "last-pair completion") + ": terminal-before-presentation save and idempotent record");
            }

            using (var f = new Fixture(360, 640))
            {
                string legacy = File.ReadAllText(Path.Combine(Application.dataPath, "../Tests/LinkMatchPersistence/Fixtures/legacy-v1-primary.json"));
                string oldPath = Path.Combine(Path.GetDirectoryName(f.Store.SavePath), "link-match-v1.json");
                Directory.CreateDirectory(Path.GetDirectoryName(oldPath)); File.WriteAllText(oldPath, legacy);
                Directory.CreateDirectory(f.Store.SavePath + ".tmp"); // Block only the migration write.
                var page = f.MakePage(null); page.Enter(); page.LayoutNow(); yield return null;
                Require(f.Store.MigrationPending && page.SaveError != null, "Failed migration is owned by the game and remains visible");
                Require(page.Profile.Export().Slots.Length == 10 && page.Profile.GetLatestActive() != null, "Failed migration retains the full playable ten-slot in-memory profile");
                f.Capture("migration-write-warning");
                Directory.Delete(f.Store.SavePath + ".tmp"); f.Click(f.Find("Retry save"));
                Require(!f.Store.MigrationPending && page.SaveError == null && File.Exists(f.Store.SavePath), "Actual retry saves migrated progress and clears its warning");
                Require(File.ReadAllText(oldPath) == legacy, "Successful migration and retry leave the original v1 file unchanged");
                Report.AppendLine("PASS actual migration failure and retry: preserved in-memory attempts, persistent warning and unchanged v1");
            }

            using (var f = new Fixture(360, 640, true))
            {
                var page = f.MakePage(new LinkMatchProfile()); page.Enter(); page.StartLevel(LinkMatchMode.Relaxed, "level-03");
                Require(!string.IsNullOrEmpty(page.SaveError), "Actual unwritable store reports a page save failure");
                page.SetHostSaveWarning("公共设置尚未保存"); page.LayoutNow(); yield return null;
                Require(HasText(f, "公共设置尚未保存") && !string.IsNullOrEmpty(page.SaveError), "Host and game save errors coexist without overwriting ownership");
                int retries = 0; page.RetryHostSave = () => { retries++; page.SetHostSaveWarning(null); };
                f.Click(f.Find("Retry save")); page.LayoutNow();
                Require(retries == 1 && !HasText(f, "公共设置尚未保存"), "Retry reaches the common-save callback even while game storage remains unavailable");
                Require(!string.IsNullOrEmpty(page.SaveError) && f.Find("LinkMatch warning").gameObject.activeInHierarchy, "Clearing host error cannot dismiss failed game persistence");
                CheckGeometry(f, page, true); f.Capture("persistent-save-failure");
                string attempt = page.Session.AttemptId; page.Save(); page.ShowEntry(); page.ContinueGame();
                Require(page.Session.AttemptId == attempt && !string.IsNullOrEmpty(page.SaveError), "Save retry failure and entry navigation preserve in-memory progress and warning");
                Report.AppendLine("PASS actual storage failure: persistent visible warning, host-error separation and in-memory continuation");
            }

            using (var f = new Fixture(720, 1150))
            {
                var inert = new GameObject("Isolated LinkMatch host"); inert.SetActive(false);
                try
                {
                    var host = inert.AddComponent<AppController>();
                    var safe = f.Root.gameObject.AddComponent<SafeDesignFrame>(); safe.Initialize(f.Root, (RectTransform)f.Root.parent);
                    Require(f.Root.rect.width > 800, "Short-screen host starts with the lobby minimum-height frame");
                    Set(host, "frame", f.Root); Set(host, "canvasRoot", (RectTransform)f.Root.parent); Set(host, "data", new SaveData());
                    Set(host, "feedback", inert.AddComponent<GameFeedback>());
                    var status = UiFactory.Label(f.Root.parent, "", 0, 0, 1, 1, 10, UiFactory.Muted); status.gameObject.SetActive(false); Set(host, "status", status);
                    Invoke(host, "BuildLinkMatch", new LinkMatchProfile(), f.Store);
                    Require(!safe.WidthOnly, "BuildLinkMatch restores the existing lobby width policy");
                    var page = Get<LinkMatchPage>(host, "linkMatchPage");
                    Require(CountSlots(page.Profile) == 0, "Host construction and initial Leave never create an attempt");
                    safe.WidthOnly = true;
                    Set(host, "activePage", Enum.Parse(typeof(AppController).GetNestedType("Page", BindingFlags.NonPublic), "LinkMatch"));
                    page.Enter(); page.StartLevel(LinkMatchMode.Challenge, "level-03"); page.LayoutNow(); yield return null;
                    Require(f.Bounds(f.Find("Settings")).xMax <= f.Width && f.Bounds(f.Find("Pause")).xMax <= f.Width, "Host-built page keeps right controls inside final safe width");
                    f.CheckHit(f.Find("Settings")); f.CheckHit(f.Find("Pause")); f.Capture("host-short-screen");
                    f.Click(f.Find("Settings"));
                    Require(Get<GameObject>(host, "modal") != null, "Real AppController creates shared settings"); RequireConcealed(f, page, "host settings"); f.Capture("host-settings");
                    Invoke(host, "OnApplicationPause", true);
                    Require(Get<GameObject>(host, "modal") == null && !page.Session.Foreground, "Host background closes settings and suspends its actual session");
                    Invoke(host, "OnApplicationPause", false); RequireConcealed(f, page, "host resume"); page.ResumeGame();
                    Require(page.Session.CanInteract, "Host resume permits play after explicit continuation");
                    f.Click(f.Find("Settings")); Get<Action>(host, "dismissModal")();
                    Require(Get<GameObject>(host, "modal") == null, "Host Android-back callback closes shared settings"); RequireConcealed(f, page, "host settings Back"); page.ResumeGame();
                    Invoke(host, "OnApplicationFocus", false); Invoke(host, "OnApplicationFocus", true);
                    RequireConcealed(f, page, "host focus return"); page.ResumeGame();
                    Set(host, "saveFailed", true); Set(host, "saveWarning", "公共设置尚未保存"); page.Save();
                    Require(HasText(f, "公共设置尚未保存"), "Host routes common save failure into the active LinkMatch page");
                    Set(host, "saveFailed", false); Set(host, "saveWarning", null); page.Save();
                    Require(!HasText(f, "公共设置尚未保存"), "Host recovery removes only its common save warning");
                    page.Leave(); Invoke(host, "SetStatus", "Other page status"); page.Save();
                    Require(status.text == "Other page status", "Inactive LinkMatch save preserves current host status");
                    Report.AppendLine("PASS actual AppController: construction frame policy, shared settings, pause/focus, explicit resume, save warnings and inactive ownership");
                }
                finally { Object.DestroyImmediate(inert); }
            }
        }

        private static void CheckEntry(Fixture f, LinkMatchPage page)
        {
            float scale = f.Width / (f.Height / (float)f.Width < 1.9f ? 360f : 388f);
            bool compact = f.Height / scale <= 680;
            Rect card = f.Bounds(f.Find("Continue game"));
            Near(card.xMin, (compact ? 14 : 19.5f) * scale, "Entry card has approved horizontal margin");
            Near(card.width, f.Width - card.xMin * 2, "Entry card fills available paper width");
            f.CheckHit(f.Find("Continue game")); f.CheckHit(f.Find("Mode relaxed")); f.CheckHit(f.Find("Mode challenge"));
            foreach (var level in LinkMatchLevel.All)
            {
                var rect = f.Find("Entry level " + level.Id); f.RevealEntry(rect); var bounds = f.Bounds(rect);
                Require(bounds.xMin >= 0 && bounds.xMax <= f.Width + 1 && bounds.yMin >= 0 && bounds.yMax <= f.Height, "Entry level card is fully inside safe content: " + level.Id);
                Near(bounds.height, (compact ? 58 : 64) * scale, "Entry level card matches approved height"); f.CheckHit(rect);
            }
            f.Find("LinkMatch entry viewport").GetComponent<ScrollRect>().verticalNormalizedPosition = 1; f.Flush();
        }
        private static void CheckGeometry(Fixture f, LinkMatchPage page, bool warning)
        {
            page.LayoutNow(); f.Flush(); var l = page.Layout; float s = (float)l.Scale;
            Rect board = f.Bounds(f.Find("LinkMatch board")), tools = f.Bounds(f.Find("LinkMatch operations"));
            Near(board.xMin, (float)l.Margin, "Board left edge matches prototype");
            Near(board.width, (float)l.BoardWidth, "Board width matches prototype");
            Near(f.Height - board.yMax, (l.Compact ? 120 : 150) * s + (warning ? 40 * s : 0), "Board top matches approved reference");
            Near(board.height, (float)l.BoardHeight, "Board height matches approved flex area");
            Near(tools.height, (l.Compact ? 48 : 52) * s, "Operations have approved height");
            Near(board.yMin - tools.yMax, (l.Compact ? 10 : 16) * s, "Board to tool gap matches prototype");
            Near(tools.yMin, (l.Compact ? 12 : 16) * s, "Tools retain safe bottom padding");
            Require(board.xMin >= 0 && board.xMax <= f.Width + 1 && tools.yMin >= 0 && board.yMax <= f.Height, "Full board and tools are inside safe content");
            Require(f.Find("LinkMatch remaining").GetComponent<Text>().preferredWidth <= f.Find("LinkMatch remaining").rect.width + 1, "Remaining count fits even with three digits");
            int renderedTiles = 0;
            Rect previous = new Rect();
            foreach (string name in new[] { "Hint", "Shuffle", "Pause" })
            {
                Rect button = f.Bounds(f.Find(name));
                Require(button.width >= 44 * s && button.height >= 44 * s && (previous.width == 0 || button.xMin > previous.xMax), "Operation targets are large and nonoverlapping: " + name); previous = button;
            }
            foreach (RectTransform tile in f.Root.GetComponentsInChildren<RectTransform>())
            {
                if (!tile.name.StartsWith("Tile ", StringComparison.Ordinal)) continue;
                renderedTiles++;
                Rect rect = f.Bounds(tile); Near(rect.width, rect.height, "Tile target is square");
                Require(rect.xMin >= board.xMin + 12 * s - 1 && rect.xMax <= board.xMax - 12 * s + 1 && rect.yMin >= board.yMin + 12 * s - 1 && rect.yMax <= board.yMax - 12 * s + 1, "Entire tile target fits board inset");
                foreach (var graphic in tile.GetComponentsInChildren<Graphic>()) Require(!graphic.raycastTarget, "Board alone owns tile gestures");
                var fruit = tile.Find("Fruit").GetComponent<RawImage>();
                Require(fruit.texture != null, "Visible tile has its actual approved fruit asset");
            }
            Require(renderedTiles == page.Session.Game.Remaining, "Every remaining cell has exactly one rendered tile");
            foreach (Text text in f.Root.GetComponentsInChildren<Text>()) Require(text.font == UiFactory.Font, "Production text uses shared local CJK font");
        }
        private static void RequireConcealed(Fixture f, LinkMatchPage page, string state)
        {
            Require(page.BoardConcealed && !page.Session.CanInteract, state + " conceals board and blocks the clock/input gate");
            Require(!f.Find("LinkMatch tiles", true).gameObject.activeInHierarchy, state + " removes all actual fruit and paths from render");
            int count = page.Session.Game.Remaining; page.SelectTile(0); page.SelectTile(1);
            Require(page.Session.Game.Remaining == count, state + " ignores tile submission");
        }
        private static bool HasText(Fixture f, string needle) { foreach (var text in f.Root.GetComponentsInChildren<Text>()) if (text.text.Contains(needle)) return true; return false; }
        private static int CountSlots(LinkMatchProfile profile) { int count = 0; foreach (var slot in profile.Export().Slots) if (slot != null) count++; return count; }
        private static int Remaining(LinkMatchSessionData state) { int count = 0; foreach (int tile in state.Tiles) if (tile >= 0) count++; return count; }
        private static LinkMatchSessionData Snapshot(LinkMatchLevel level, LinkMatchMode mode, int[] tiles, double seconds)
        {
            return new LinkMatchSessionData { AttemptId = Guid.NewGuid().ToString("D"), Mode = mode, LevelId = level.Id, Columns = level.Columns, Rows = level.Rows, Tiles = tiles, ElapsedSeconds = seconds, RemainingSeconds = mode == LinkMatchMode.Challenge ? level.ChallengeSeconds - seconds : 0, RandomState = 1709, ActivityOrder = 1 };
        }
        private static LinkMatchProfile ProfileWith(LinkMatchSessionData state)
        {
            var data = new LinkMatchSaveData { SelectedMode = state.Mode, SelectedLevelId = state.LevelId, ActivitySequence = 1 };
            data.Slots[LinkMatchSaveData.SlotIndex(state.Mode, state.LevelId)] = state;
            return new LinkMatchProfile(data);
        }
        private static int[] PrototypeFixture(LinkMatchLevel level)
        {
            var tiles = new int[level.Columns * level.Rows]; for (int i = 0; i < tiles.Length; i++) tiles[i] = i % level.Kinds;
            uint seed = (uint)(1709 + level.Number * 37);
            for (int i = tiles.Length - 1; i > 0; i--) { seed = unchecked(seed * 1664525u + 1013904223u); int j = (int)(seed % (uint)(i + 1)); int old = tiles[i]; tiles[i] = tiles[j]; tiles[j] = old; }
            var reserved = new HashSet<int>();
            foreach (int target in new[] { level.Columns, level.Columns * 4 })
            {
                int source = Array.FindIndex(tiles, value => value == 0);
                for (int i = 0; i < tiles.Length; i++) if (tiles[i] == 0 && !reserved.Contains(i)) { source = i; break; }
                int old = tiles[target]; tiles[target] = tiles[source]; tiles[source] = old; reserved.Add(target);
            }
            return tiles;
        }
        private static void Near(float actual, float expected, string reason) { Require(Math.Abs(actual - expected) < 1.1f, reason + ": " + actual + " vs " + expected); }
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); checks++; }
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static T Get<T>(object value, string field) { return (T)value.GetType().GetField(field, Members).GetValue(value); }
        private static void Set(object value, string field, object data) { value.GetType().GetField(field, Members).SetValue(value, data); }
        private static void Invoke(object value, string method, params object[] arguments)
        {
            foreach (var candidate in value.GetType().GetMethods(Members))
                if (candidate.Name == method && candidate.GetParameters().Length == arguments.Length) { candidate.Invoke(value, arguments); return; }
            throw new MissingMethodException(method);
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly RectTransform Root;
            internal readonly LinkMatchSaveStore Store;
            internal readonly int Width, Height;
            private readonly GameObject owner;
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly EventSystem events;
            private readonly string directory;
            internal Fixture(int width, int height, bool failedStore = false)
            {
                Width = width; Height = height; owner = new GameObject("LinkMatch UI fixture");
                directory = Path.Combine(Path.GetTempPath(), "xanadu-link-match-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
                string storeDirectory = directory;
                if (failedStore) { storeDirectory = Path.Combine(directory, "file-blocks-directory"); File.WriteAllText(storeDirectory, "intentional storage failure fixture"); }
                Store = new LinkMatchSaveStore(storeDirectory);
                var canvas = new GameObject("Fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
                canvas.SetParent(owner.transform, false); canvas.sizeDelta = new Vector2(width, height);
                camera = new GameObject("Fixture camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(owner.transform, false);
                camera.enabled = false; camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
                camera.orthographicSize = height * .5f; camera.aspect = (float)width / height; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UiFactory.Paper;
                target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; canvas.GetComponent<Canvas>().worldCamera = camera;
                Root = UiFactory.Rect(canvas, "LinkMatch fixture page", 0, 0, width, height); UiFactory.Stretch(Root);
                events = new GameObject("Fixture events", typeof(EventSystem)).GetComponent<EventSystem>(); events.transform.SetParent(owner.transform, false);
            }
            internal LinkMatchPage MakePage(LinkMatchProfile profile, Action lobby = null, Action<Action> settings = null, Func<bool> acceptsInput = null)
            {
                var page = Root.gameObject.AddComponent<LinkMatchPage>();
                page.Initialize(Root, lobby ?? (() => { }), settings ?? (done => done()), acceptsInput ?? (() => true), () => false, () => false, Root.gameObject.AddComponent<GameFeedback>(), _ => { }, initialProfile: profile, initialStore: Store);
                return page;
            }
            internal RectTransform Find(string name, bool includeInactive = false)
            {
                foreach (RectTransform rect in Root.GetComponentsInChildren<RectTransform>(includeInactive))
                    if (rect.name == name && (!includeInactive || rect.parent == null || rect.parent.gameObject.activeInHierarchy)) return rect;
                throw new InvalidOperationException("Missing production element: " + name);
            }
            internal void Flush() { Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); }
            internal Rect Bounds(RectTransform rect)
            {
                Flush(); var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]); var b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
            private PointerEventData Pointer(Vector2 point, RectTransform expected)
            {
                Flush(); var data = new PointerEventData(events) { position = point, pointerId = -1, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
                Require(hits.Count > 0 && (hits[0].gameObject.transform == expected || hits[0].gameObject.transform.IsChildOf(expected)), "Rendered raycast reaches " + expected.name);
                data.pointerCurrentRaycast = data.pointerPressRaycast = hits[0]; return data;
            }
            internal void RevealEntry(RectTransform rect)
            {
                var viewport = Find("LinkMatch entry viewport"); var scroll = viewport.GetComponent<ScrollRect>(); scroll.StopMovement();
                Rect view = Bounds(viewport), row = Bounds(rect); Vector2 position = scroll.content.anchoredPosition;
                if (row.yMin < view.yMin) position.y += view.yMin - row.yMin;
                else if (row.yMax > view.yMax) position.y -= row.yMax - view.yMax;
                scroll.content.anchoredPosition = position; Flush();
            }
            internal void CheckHit(RectTransform rect) { Pointer(RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)), rect); }
            internal void Click(RectTransform rect) { Dispatch(Pointer(RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)), rect)); }
            internal void ClickTile(LinkMatchPage page, int tile)
            {
                Dispatch(Pointer(TilePoint(page, tile), page.Surface));
            }
            private Vector2 TilePoint(LinkMatchPage page, int tile)
            {
                var l = page.Layout; var local = new Vector3(page.Surface.rect.xMin + (float)(l.GridLeft + (tile % l.Columns + .5) * l.Cell), page.Surface.rect.yMax - (float)(l.GridTop + (tile / l.Columns + .5) * l.Cell));
                return RectTransformUtility.WorldToScreenPoint(camera, page.Surface.TransformPoint(local));
            }
            internal PointerEventData PressTile(LinkMatchPage page, int tile, int pointerId = -1)
            {
                var data = Pointer(TilePoint(page, tile), page.Surface); data.pointerId = pointerId;
                data.pointerPress = ExecuteEvents.ExecuteHierarchy(data.pointerCurrentRaycast.gameObject, data, ExecuteEvents.pointerDownHandler);
                return data;
            }
            internal void DragTile(LinkMatchPage page, PointerEventData data, int tile)
            {
                data.position = TilePoint(page, tile); ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.dragHandler);
            }
            internal void Release(PointerEventData data)
            {
                if (data.pointerPress != null) ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
            }
            internal void ClickBackdrop()
            {
                Dispatch(Pointer(new Vector2(2, 2), Find("LinkMatch dialog shade")));
            }
            private static void Dispatch(PointerEventData data)
            {
                var hit = data.pointerCurrentRaycast.gameObject;
                var pressed = ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerDownHandler);
                var clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit); data.pointerPress = pressed != null ? pressed : clicked;
                if (data.pointerPress != null) ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
                if (clicked != null && data.pointerPress == clicked) ExecuteEvents.Execute(clicked, data, ExecuteEvents.pointerClickHandler);
            }
            internal void Capture(string name)
            {
                Flush(); var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                try { image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(output, name + "-" + Width + "x" + Height + ".png"), image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }
            public void Dispose()
            {
                camera.targetTexture = null; if (RenderTexture.active == target) RenderTexture.active = null;
                Object.DestroyImmediate(owner); target.Release(); Object.DestroyImmediate(target);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
