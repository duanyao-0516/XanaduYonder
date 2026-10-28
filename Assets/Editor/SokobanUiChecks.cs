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
                page.Enter(); page.LayoutNow();
                yield return null;
                Require(page.IsEntry, "Lobby route opens the level entry before play");
                Require(profile.GetActive() == null, "Viewing entry does not fabricate a started attempt");
                page.Move(Direction.Up); page.Save(); page.Suspend(); page.SetForeground(false); page.SetForeground(true);
                Require(page.Session.Game.Moves == 0 && profile.GetActive() == null && page.IsEntry, "Entry ignores movement and lifecycle saves keep fresh profile empty");
                page.Leave(); page.Save(); page.Enter();
                Require(profile.GetActive() == null && page.IsEntry, "Startup leave and reentry do not create an active save");
                f.Capture("entry-new");
                f.Click(f.Find("Continue game"));
                Require(!page.IsEntry && profile.GetActive() != null, "Start teaching explicitly enters and persists the first attempt");
                CheckGeometry(f, page, height);
                f.Capture("compact");
                foreach (string name in new[] { "Home", "Book", "Settings", "Restart", "Move Up", "Move Down", "Move Left", "Move Right" })
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
                Require(page.Viewport.Zoom == 1 && Math.Abs(page.Viewport.CenterX - .5) < .001, "Viewport always shows the whole centered map");
                page.Move(Direction.Up); yield return new WaitForSeconds(.15f);
                string attempt = page.Session.AttemptId;
                page.ShowEntry(); page.LayoutNow(); yield return null;
                Require(page.IsEntry && page.Session.Game.Moves == 1, "Returning to entry preserves the live board");
                f.Capture("entry-resume"); CheckEntryGeometry(f);
                foreach (string chapter in new[] { "tutorial", catalog.At(8).Chapter, catalog.At(28).Chapter, catalog.At(48).Chapter, catalog.At(68).Chapter, catalog.At(88).Chapter })
                {
                    page.SelectChapter(chapter); yield return null;
                    int expected = chapter == "tutorial" ? 8 : 20;
                    int found = 0;
                    foreach (var button in f.Root.GetComponentsInChildren<Button>()) if (button.name.StartsWith("Entry level ", StringComparison.Ordinal)) found++;
                    Require(found == expected, "Entry chapter exposes its actual number of selectable levels");
                }
                string lastId = catalog.At(catalog.Count - 1).LevelId;
                Require(page.DisplayLevelNumber(lastId) == 100 && page.DisplayLevelLabel(lastId) == "第 100 关", "Regular numbering is independent of eight teaching levels");
                f.Capture("entry-numbers-last");
                page.SelectChapter(catalog.At(8).Chapter); yield return null; f.Capture("entry-numbers");
                page.ContinueGame();
                Require(!page.IsEntry && page.Session.AttemptId == attempt && page.Session.Game.Moves == 1, "Continue returns to the same attempt and counters");
                page.ShowLevels(); yield return null;
                f.Click(f.Find("Level tutorial-002"));
                Require(page.HasDialog && page.Session.Game.Level.LevelId == "tutorial-001" && page.Session.Game.Moves == 1, "Switching a different unfinished level requires confirmation");
                f.Click(f.Find("Dialog secondary"));
                Require(page.HasDialog && page.Session.Game.Moves == 1, "Cancel switch retains current attempt and picker");
                f.Click(f.Find("Level tutorial-001"));
                Require(!page.HasDialog && page.Session.AttemptId == attempt && page.Session.Game.Moves == 1, "Selecting the current level directly resumes it");
                page.Undo(); Require(page.Session.Game.Moves == 0, "Continue preserves full undo history");
                page.ShowLevels(); yield return null; CheckPickerGeometry(f); f.Capture("picker-tutorial");
                page.Back(); Require(!page.HasDialog && !page.IsEntry && home == 0, "Picker Back dismisses the picker only");
                page.SetHostSaveWarning("设置尚未保存，请检查设备空间。"); page.LayoutNow();
                Require(Array.Exists(f.Root.GetComponentsInChildren<Text>(), t => t.text.Contains("设置尚未保存")), "Shared save failure remains visible");
                f.Capture("save-warning"); page.SetHostSaveWarning(null); page.LayoutNow();
                string solution = catalog.Get("tutorial-001").Solution;
                for (int step = 0; step < solution.Length; step++)
                {
                    page.Move(SokobanGame.FromChar(solution[step]));
                    if (step == solution.Length - 1)
                    {
                        if (height == 1455) page.LayoutNow();
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
                f.Click(f.Find("Return to entry")); Require(page.IsEntry && !page.HasDialog, "Picker link returns to entry with no hidden modal");
                page.StartLevel(lastId, true); page.ShowLevels(); yield return null;
                CheckPickerGeometry(f); f.Capture("picker-numbers");
                foreach (var text in f.Root.GetComponentsInChildren<Text>()) Require(!text.text.Contains("仓库 "), "Regular game and picker do not expose placeholder warehouse titles");
                page.Back();
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
                Require(page.IsEntry && page.Session.Game.Moves == 50000, "Leave and reentry show entry while retaining complete action history");
                page.ContinueGame(); Require(page.Session.Game.Moves == 50000, "Large history continues without reset");
                f.Click(f.Find("Home")); Require(home == 1, "Home invokes lobby callback");
                report.AppendLine("PASS 720x" + height + ": reference geometry, controls/raycast, undo, help/settings, lifecycle, auto-fit, entry, picker, records, tall map, max counter");
            }
            using (var f = new Fixture(1320, 640))
            {
                var page = f.Root.gameObject.AddComponent<SokobanPage>();
                page.Initialize(f.Root, () => { }, done => done(), () => true, () => false, () => false,
                    f.Root.gameObject.AddComponent<GameFeedback>(), _ => { }, initialCatalog: catalog, initialProfile: new SokobanProfile(catalog));
                page.Enter(); page.LayoutNow(); yield return null;
                Rect first = f.Bounds(f.Find("Entry level tutorial-001"));
                Near(first.y, f.Bounds(f.Find("Entry level tutorial-004")).y, "Narrow tutorial grid has four columns");
                Require(f.Bounds(f.Find("Entry level tutorial-005")).yMax < first.yMin, "Fifth tutorial wraps to a separate row");
                f.CheckHit(f.Find("Entry level tutorial-008")); f.Capture("entry-narrow-tutorial");
                page.SelectChapter(catalog.At(8).Chapter); yield return null;
                first = f.Bounds(f.Find("Entry level " + catalog.At(8).LevelId));
                Near(first.y, f.Bounds(f.Find("Entry level " + catalog.At(12).LevelId)).y, "Narrow numbered grid has five columns");
                Require(f.Bounds(f.Find("Entry level " + catalog.At(13).LevelId)).yMax < first.yMin, "Sixth regular number wraps to a separate row");
                f.CheckHit(f.Find("Entry level " + catalog.At(27).LevelId)); f.Capture("entry-narrow-numbers");
                report.AppendLine("PASS 640x1320 narrow entry: four tutorial columns, five numbered columns, last-row raycasts");
            }
            using (var f = new Fixture(1455))
            {
                var data = new SokobanProfile(catalog).Export();
                data.SelectedLevelId = catalog.At(catalog.Count - 1).LevelId;
                var migrated = new SokobanProfile(catalog, data);
                var page = f.Root.gameObject.AddComponent<SokobanPage>();
                page.Initialize(f.Root, () => { }, done => done(), () => true, () => false, () => false,
                    f.Root.gameObject.AddComponent<GameFeedback>(), _ => { }, initialCatalog: catalog, initialProfile: migrated);
                page.Leave(); page.Save(); page.Enter(); yield return null;
                Require(page.IsEntry && page.Session.Game.Level.LevelId == "tutorial-001", "Migrated selection without an active attempt previews the actual starting tutorial");
                Require(migrated.GetActive() == null && migrated.SelectedLevelId == data.SelectedLevelId, "Opening a migrated profile preserves its selection and does not fabricate progress");
                f.Click(f.Find("Continue game"));
                Require(!page.IsEntry && page.Session.Game.Level.LevelId == "tutorial-001" && migrated.GetActive().LevelId == "tutorial-001", "Migrated entry action starts the same level that it previews");
                report.AppendLine("PASS migrated profile: preview/action agree without creating a save during entry");
            }
            using (var f = new Fixture(1455))
            {
                var profile = new SokobanProfile(catalog);
                var saved = profile.StartLevel("tutorial-001"); saved.Move(Direction.Up); saved.SetView(2, .8, .2); profile.Capture(saved);
                string attempt = saved.AttemptId;
                var restored = new SokobanProfile(catalog, profile.Export());
                Action finish = null; int home = 0;
                var page = f.Root.gameObject.AddComponent<SokobanPage>();
                page.Initialize(f.Root, () => home++, done => finish = done, () => true, () => false, () => false,
                    f.Root.gameObject.AddComponent<GameFeedback>(), _ => { }, initialCatalog: catalog, initialProfile: restored);
                page.Enter(); yield return null;
                Require(page.IsEntry && page.Session.Game.Moves == 1, "Loaded saved attempt is previewed at entry");
                f.Click(f.Find("Settings")); page.Move(Direction.Up);
                Require(page.IsEntry && page.Session.Game.Moves == 1, "Entry settings cannot move the saved attempt");
                finish(); page.SetForeground(false); page.SetForeground(true);
                Require(page.IsEntry, "Settings and background preserve entry view");
                page.ContinueGame();
                Require(page.Session.AttemptId == attempt && page.Session.Game.Moves == 1, "Old saved attempt continues without replacing identity");
                Require(page.Viewport.Zoom == 1 && restored.GetActive().Zoom == 1 && restored.GetActive().CenterX == .5 && restored.GetActive().CenterY == .5, "Legacy zoom is normalized without discarding moves");
                page.SetForeground(false); page.SetForeground(true);
                Require(!page.IsEntry, "Background resumes gameplay without an extra entry step");
                page.Undo(); Require(page.Session.Game.Moves == 0, "Old save trajectory remains undoable after fit normalization");
                page.ShowEntry(); page.Back(); Require(home == 1, "Entry Back returns to lobby");
                var last = catalog.At(catalog.Count - 1); page.StartLevel(last.LevelId, true);
                foreach (char step in last.Solution) page.Session.Move(SokobanGame.FromChar(step));
                page.Save(); Invoke(page, "Refresh"); page.CheckEndState();
                Require(page.Session.Game.Won && page.HasDialog && page.DisplayLevelNumber(last.LevelId) == 100, "Last regular level resolves completion using catalog identity");
                page.ShowEntry(); page.ContinueGame();
                Require(!page.IsEntry && page.Session.Game.Level.LevelId == last.LevelId && page.Session.Game.Moves == 0 && !page.Session.Game.Won, "Final entry action replays the existing last level without out-of-range access");
                report.AppendLine("PASS saved profile: old zoom, identity, history, entry/game lifecycle and Back");
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
                    page.Enter(); page.ContinueGame(); page.LayoutNow(); yield return null;
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
                    var page = Get<SokobanPage>(host, "sokobanPage"); page.Enter(); page.ContinueGame(); page.LayoutNow(); yield return null;
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
            Rect undo = f.Bounds(f.Find("Undo")), restart = f.Bounds(f.Find("Restart"));
            Rect up = f.Bounds(f.Find("Move Up")), down = f.Bounds(f.Find("Move Down"));
            Near(undo.yMin, up.yMin, "Undo aligns with Up"); Near(restart.yMin, down.yMin, "Restart aligns with Down");
            Near(undo.width, 166 * s, "Undo uses the approved full left column");
            Near(operations.yMax - up.yMax, 24 * s, "Up moved half a grid toward center");
            Near(operations.yMax - down.yMax, 80 * s, "Down moved half a grid toward center");
            Near(operations.yMax - f.Bounds(f.Find("Move Left")).yMax, 52 * s, "Left retains original position");
            foreach (Transform child in f.Root.GetComponentsInChildren<Transform>(true))
                Require(child.name != "Direction center" && child.name != "Fit" && child.name != "Zoom in" && child.name != "Zoom out", "Removed zoom and center controls do not exist");
            Rect map = f.Bounds(f.Find("Sokoban map"));
            Require(map.xMin >= board.xMin + 12 * s - 1 && map.xMax <= board.xMax - 12 * s + 1 && map.yMin >= board.yMin + 12 * s - 1 && map.yMax <= board.yMax - 12 * s + 1, "Complete board fits inside the approved 12-pixel inset");
        }
        private static void CheckEntryGeometry(Fixture f)
        {
            float s = 720f / 388;
            Rect entry = f.Bounds(f.Find("Sokoban entry viewport"));
            Rect card = f.Bounds(f.Find("Continue game"));
            Near(card.width, 349 * s, "Continue card width matches prototype");
            Near(card.height, 183 * s, "Continue card height matches prototype");
            Rect grid = f.Bounds(f.Find("Sokoban entry level grid"));
            Near(entry.yMax - card.yMax, (154.015625f - 110.421875f) * s, "Continue card offset matches prototype");
            Near(entry.yMax - grid.yMax, (497.515625f - 110.421875f) * s, "Tutorial grid vertical position matches prototype");
            foreach (var button in f.Root.GetComponentsInChildren<Button>())
                if (button.name.StartsWith("Entry level ", StringComparison.Ordinal)) Near(f.Bounds((RectTransform)button.transform).height, 64 * s, "Tutorial retains descriptive 64-pixel cards");
        }
        private static void CheckPickerGeometry(Fixture f)
        {
            Rect panel = f.Bounds(f.Find("Sokoban dialog"));
            Near(panel.width, 320 * 720f / 388, "Level dialog is exactly the approved narrower width");
            foreach (var text in f.Root.GetComponentsInChildren<Text>()) Require(text.text != "进入", "No meaningless enter label remains");
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
            private readonly int height, width;
            public Fixture(int height, int width = 720)
            {
                this.height = height; this.width = width; owner = new GameObject("Sokoban UI fixture");
                var canvas = new GameObject("Fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
                canvas.SetParent(owner.transform, false); canvas.sizeDelta = new Vector2(width, height);
                camera = new GameObject("Fixture camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(owner.transform, false);
                camera.enabled = false; camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
                camera.orthographicSize = height * .5f; camera.aspect = (float)width / height; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UiFactory.Paper;
                target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; canvas.GetComponent<Canvas>().worldCamera = camera;
                Root = UiFactory.Rect(canvas, "Sokoban fixture page", 0, 0, width, height); UiFactory.Stretch(Root);
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
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try { image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(output, name + "-" + width + "x" + height + ".png"), image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }
            public void Dispose() { camera.targetTexture = null; if (RenderTexture.active == target) RenderTexture.active = null; Object.DestroyImmediate(owner); target.Release(); Object.DestroyImmediate(target); }
        }
    }
}
