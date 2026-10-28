using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core;
using XanaduYonder.Core.Gomoku;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.Gomoku;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Real page, graphics and EventSystem hit testing. Every store is either
    // explicitly absent or confined to this run's temporary validation directory.
    [InitializeOnLoad]
    public static class GomokuUiChecks
    {
        private const string Pending = "Xanadu.GomokuUiChecks.Pending";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly StringBuilder report = new StringBuilder();
        private static string output;
        private static int checks;

        static GomokuUiChecks() { EditorApplication.playModeStateChanged += StateChanged; }

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
            new GameObject("Gomoku UI checks runner").AddComponent<Runner>().StartCoroutine(AfterFrame());
        }

        private static IEnumerator AfterFrame() { yield return null; Execute(); }

        private static void Execute()
        {
            var oldFont = UiFactory.Font;
            checks = 0; report.Clear();
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/validation/gomoku-ui"));
            Directory.CreateDirectory(output);
            foreach (string file in Directory.GetFiles(output, "*.png")) File.Delete(file);
            report.AppendLine("Unity " + Application.unityVersion + "; production GomokuPage; Camera.Render; real UGUI raycasts; isolated stores");
            report.AppendLine("Mouse events are dispatched through UGUI and the production pointer LateUpdate. OS touch phases are covered by console input tests; device input is a separate validation layer.");
            try
            {
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Require(UiFactory.Font != null, "Bundled Noto CJK font loads");
                Require(Input.touchCount == 0 && !Input.GetMouseButton(0), "Batch input begins without a physical held contact");
                foreach (int height in new[] { 1320, 1582 })
                using (var fixture = new Fixture(height))
                {
                    CheckBoardAndLocalPlay(fixture);
                    CheckSetup(fixture);
                    CheckIndependentRestore(fixture);
                    CheckLifecycle(fixture);
                    CheckSharedHostLifecycle(fixture);
                    CheckSharedHostSaveStatus(fixture);
                    CheckAiWaiting(fixture);
                    CheckTerminal(fixture, false);
                    CheckTerminal(fixture, true);
                    CheckSaveFailure(fixture);
                    CheckInvalidSave(fixture);
                    report.AppendLine("PASS 720x" + height + ": navigation, rendered board, confirmation, local consent, setup, independent resume, lifecycle, AI cancellation, terminal, isolated storage failure/recovery");
                }
                new GameObject("Gomoku asynchronous checks runner").AddComponent<Runner>().StartCoroutine(AsynchronousChecks(oldFont));
            }
            catch (Exception error)
            {
                Fail(error); Finish(1, oldFont);
            }
        }

        private static IEnumerator AsynchronousChecks(Font oldFont)
        {
            // Keep yields outside the catch: failures from any resumed iterator
            // step still reach a report and nonzero batch exit.
            var sequence = CheckWorkerCompletion();
            int exit = 0;
            while (true)
            {
                bool next = false; object current = null;
                try { next = sequence.MoveNext(); if (next) current = sequence.Current; }
                catch (Exception error) { exit = 1; Fail(error); }
                if (!next || exit != 0) break;
                yield return current;
            }
            (sequence as IDisposable)?.Dispose();
            Finish(exit, oldFont);
        }

        private static IEnumerator CheckWorkerCompletion()
        {
            foreach (int height in new[] { 1320, 1582 })
            using (var f = new Fixture(height))
            {
                using (var g = new PageFixture(f, Data(GomokuMode.HumanVsAi)))
                {
                    g.Click("简单"); g.Click("确认开始"); g.TapCell(112); g.ClickField("confirmButton");
                    double deadline = Time.realtimeSinceStartupAsDouble + 10;
                    while (g.Session.Game.MoveCount < 2)
                    {
                        Require(Time.realtimeSinceStartupAsDouble < deadline, "Real AI worker replies within the bounded UI integration deadline");
                        yield return null;
                    }
                    var game = g.Session.Game;
                    Require(game.MoveCount == 2 && game.GetCell(game.LastMove) == GomokuStone.White && game.LastMove != 112,
                        "Actual Task.Run result reaches GomokuPage.Update and commits one legal computer reply");
                    Require(g.Session.CanSelect && !g.Session.AiPending && Get<object>(g.Page, "search") == null,
                        "Accepted computer reply restores the human turn and clears request ownership");
                    Require(Get<GomokuSaveData>(g.Page, "data").Slots[(int)GomokuMode.HumanVsAi].Moves.Length == 2,
                        "Accepted asynchronous AI reply is captured by the real page save path");
                    RequireMeshColor(f, g, UiFactory.Hex("A76837"), "Accepted white reply paints its last-move dot");
                    f.Capture("ai-reply"); g.Page.Suspend();
                }
                using (var g = new PageFixture(f, Data(GomokuMode.HumanVsAi)))
                {
                    g.Click("简单"); g.Click("白棋 · 后手"); g.Click("确认开始");
                    var worker = Get<Task<GomokuAiResult>>(g.Page, "search");
                    Require(worker != null && worker.Wait(2000) && worker.Status == TaskStatus.RanToCompletion,
                        "Opening worker completes before a main-thread Update, without an unbounded wait");
                    Require(g.Session.Game.MoveCount == 0, "Worker completion alone cannot mutate the page-owned game");
                    g.Page.Suspend(); Invoke(g.Page, "Update");
                    Require(g.Session.Game.MoveCount == 0 && g.Session.Paused && !g.Session.AiPending && Get<object>(g.Page, "search") == null,
                        "Suspending after worker completion but before Update rejects the completed result");
                    yield return null;
                    Require(g.Session.Game.MoveCount == 0, "A later real frame cannot revive the canceled completed result");
                }
                report.AppendLine("PASS 720x" + height + ": real asynchronous AI completion and completed-before-Update cancellation");
            }
        }

        private static void Fail(Exception error)
        {
            report.AppendLine("GOMOKU_UI_CHECKS_FAIL after " + checks + " assertions\n" + error);
            Debug.LogException(error);
        }

        private static void Finish(int exit, Font oldFont)
        {
            if (exit == 0) report.AppendLine("GOMOKU_UI_CHECKS_PASS: " + checks + " assertions");
            UiFactory.Font = oldFont;
            File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
            Debug.Log(report.ToString()); EditorApplication.Exit(exit);
        }

        private static void CheckBoardAndLocalPlay(Fixture f)
        {
            using (var g = new PageFixture(f))
            {
                g.Click("确认开始");
                CheckNavigation(f, g);
                var surface = g.Surface;
                Require(surface.GetComponent<GomokuPointer>() != null, "One production pointer owns the board");
                Require(surface.GetComponentsInChildren<Button>(true).Length == 0, "225 intersections do not create competing cell Buttons");
                Require(surface.GetComponentsInChildren<GomokuBoardGraphic>(true).Length == 1, "One procedural graphic paints grid and stones");
                int axes = 0;
                foreach (var label in surface.GetComponentsInChildren<Text>(true))
                    if (label.name.StartsWith("Column ", StringComparison.Ordinal) || label.name.StartsWith("Row ", StringComparison.Ordinal))
                    {
                        axes++; Require(label.font == UiFactory.Font && label.fontSize == 22 && !label.raycastTarget,
                            "Coordinate is legible Noto text without a competing raycast: " + label.name);
                    }
                Require(axes == 60, "All four sides contain 15 coordinate labels");
                Require(Find(surface, "Column A top").GetComponent<Text>().text == "A" &&
                    Find(surface, "Column O bottom").GetComponent<Text>().text == "O" &&
                    Find(surface, "Row 15 left").GetComponent<Text>().text == "15" &&
                    Find(surface, "Row 1 right").GetComponent<Text>().text == "1", "Four corner coordinate labels have the expected orientation");
                var bounds = InPage(g.Root, surface);
                Near(bounds.width, 648, "Board width remains 648 design units");
                Near(bounds.height, 648, "Board height remains 648 design units");
                foreach (var button in g.Root.GetComponentsInChildren<Button>())
                {
                    var area = InPage(g.Root, button.GetComponent<RectTransform>());
                    Require(area.xMin >= 0 && area.xMax <= 720.1 && area.yMin >= 0 && area.yMax <= f.Height + .1,
                        "Visible action stays inside the page at height " + f.Height + ": " + button.name);
                }
                g.TapAt(new Vector2(22, 324));
                Require(!g.Session.SelectedIndex.HasValue && g.Session.Game.MoveCount == 0, "Coordinate gutter click does not select or place a stone");
                g.TapCell(82);
                Require(g.Session.SelectedIndex == 82 && g.Session.Game.MoveCount == 0, "Real H10 tap previews without committing");
                Require(Get<Text>(g.Page, "confirmText").text.Contains("H10"), "Confirmation names the selected H10 coordinate");
                Require(Find(surface, "Column H top").GetComponent<Text>().fontStyle == FontStyle.Bold &&
                    Find(surface, "Row 10 right").GetComponent<Text>().fontStyle == FontStyle.Bold, "Preview highlights its row and column axes");
                RequireMeshColor(f, g, new Color(41f / 255, 58f / 255, 49f / 255, .5f), "Black turn paints a translucent black preview");
                f.Capture("local-black-preview");
                g.ClickField("confirmButton");
                Require(g.Session.Game.GetCell(82) == GomokuStone.Black && g.Session.Game.MoveCount == 1 && !g.Session.SelectedIndex.HasValue,
                    "Only the real confirmation button commits the black stone");
                Require(Get<Text>(g.Page, "turnText").text.Contains("玩家 2") && g.Session.Game.ToPlay == GomokuStone.White,
                    "Local play transfers the next turn to player 2 as white");
                RequireMeshColor(f, g, UiFactory.Hex("E8BC80"), "Last black stone carries the warm last-move dot");
                g.TapCell(82);
                Require(!g.Session.SelectedIndex.HasValue && g.Session.Game.MoveCount == 1, "Occupied point cannot become a pending move");
                g.TapCell(112);
                Require(g.Session.SelectedIndex == 112, "Board center maps to H8");
                RequireMeshColor(f, g, new Color(1, 253f / 255, 245f / 255, .7f), "White turn paints a translucent white preview");
                f.Capture("local-white-preview");
                g.ClickField("clearButton");
                Require(!g.Session.SelectedIndex.HasValue && g.Session.Game.MoveCount == 1, "Cancel selection does not place the preview");
                g.ClickField("undoButton");
                Require(HasText(g.Dialog, "请玩家 2 确认"), "Local undo asks the other player to confirm");
                f.Capture("local-undo-consent");
                g.Click("不同意");
                Require(g.Session.Game.MoveCount == 1 && !g.Session.Paused, "Rejecting undo preserves the board and resumes the same turn");
                g.ClickField("undoButton"); g.Click("同意悔棋");
                Require(g.Session.Game.MoveCount == 0 && g.Session.Game.ToPlay == GomokuStone.Black, "Agreeing to local undo removes exactly the last move");
                g.Click("新一局"); g.Click("玩家 2 执黑"); g.Click("取消");
                Require(g.Session.BlackPlayer == 1, "Canceling local setup does not apply draft player assignments");
                g.Click("新一局"); g.Click("玩家 2 执黑"); g.Click("确认开始");
                Require(g.Session.BlackPlayer == 2 && Get<Text>(g.Page, "turnText").text.Contains("玩家 2"), "Confirmed local setup lets player 2 play black first");
                g.TapCell(112); g.ClickField("confirmButton"); g.ClickField("undoButton");
                Require(HasText(g.Dialog, "请玩家 1 确认"), "Undo consent follows swapped player ownership");
                g.Click("不同意");
            }
        }

        private static void CheckNavigation(Fixture f, PageFixture g)
        {
            var icons = new[] { UiIconKind.Home, UiIconKind.Book, UiIconKind.Settings };
            var left = new[] { 56f, 536f, 606f };
            for (int i = 0; i < icons.Length; i++)
            {
                var control = SharedIcon(g.Root, icons[i]);
                var area = InPage(g.Root, control);
                Near(area.xMin, left[i], "Shared navigation x: " + icons[i]);
                Near(area.yMin, 34, "Shared navigation y: " + icons[i]);
                Near(area.width, 58, "Shared navigation width: " + icons[i]);
                Near(area.height, 58, "Shared navigation height: " + icons[i]);
            }
            f.Click(SharedIcon(g.Root, UiIconKind.Book));
            Require(g.Dialog != null && HasText(g.Dialog, "从下到上 1–15"), "Shared Book opens the full coordinate rules");
            f.Capture("rules"); g.Click("知道了");
            f.Click(SharedIcon(g.Root, UiIconKind.Settings));
            Require(g.SettingsOpened == 1 && g.Session.Paused && !Get<GomokuBoardView>(g.Page, "board").Interactable,
                "Shared Settings pauses and blocks the board before host callback");
            g.CloseSettings();
            Require(!g.Session.Paused, "Settings completion restores the prior active turn");
        }

        private static void CheckSetup(Fixture f)
        {
            using (var g = new PageFixture(f, Data(GomokuMode.HumanVsAi)))
            {
                f.Capture("ai-setup"); g.Click("取消");
                Require(g.Session == null && Get<Button>(g.Page, "confirmButton").interactable, "Empty page has an actionable new-game confirmation");
                g.ClickField("confirmButton"); g.Click("确认开始");
                string id = g.Session.SessionId;
                g.Click("新一局"); g.Click("困难"); g.Click("白棋 · 后手"); g.Click("取消");
                Require(g.Session.SessionId == id && g.Session.Difficulty == GomokuDifficulty.Normal && g.Session.HumanStone == GomokuStone.Black,
                    "Canceling AI setup preserves the current game and options");
                var data = Get<GomokuSaveData>(g.Page, "data");
                Require(data.PreferredDifficulty == GomokuDifficulty.Normal && data.PreferredHumanStone == GomokuStone.Black,
                    "Canceled drafts do not change saved preferences");
            }
            string[] names = { "简单", "普通", "困难" };
            for (int difficulty = 0; difficulty < 3; difficulty++)
            foreach (var side in new[] { GomokuStone.Black, GomokuStone.White })
            using (var g = new PageFixture(f, Data(GomokuMode.HumanVsAi)))
            {
                g.Click(names[difficulty]); g.Click(side == GomokuStone.Black ? "黑棋 · 先手" : "白棋 · 后手"); g.Click("确认开始");
                Require(g.Session.Difficulty == (GomokuDifficulty)difficulty && g.Session.HumanStone == side,
                    "Real setup applies difficulty and side: " + names[difficulty] + " " + side);
                Require(side == GomokuStone.Black ? g.Session.CanSelect : g.Session.NeedsAi && g.Session.AiPending,
                    "Black starts as human; choosing white schedules the computer opening");
                g.Page.Suspend();
                Require(!g.Session.AiPending && Get<object>(g.Page, "search") == null, "Setup fixture suspends and cancels any opening worker");
            }
        }

        private static void CheckIndependentRestore(Fixture f)
        {
            var initial = Data(GomokuMode.Local);
            initial.Slots[(int)GomokuMode.Local] = Slot(GomokuMode.Local, new[] { 112, 113 }, GomokuStone.Black, 2);
            initial.Slots[(int)GomokuMode.HumanVsAi] = Slot(GomokuMode.HumanVsAi, new[] { 112 }, GomokuStone.White);
            using (var g = new PageFixture(f, initial))
            {
                string localId = g.Session.SessionId;
                Require(g.Session.Paused && !g.Session.CanSelect, "Restored local slot requires explicit resume");
                f.Capture("restored-paused"); g.ClickField("confirmButton"); g.TapCell(82);
                g.ClickField("aiMode");
                Require(g.Session.Mode == GomokuMode.HumanVsAi && g.Session.Paused && g.Session.Game.MoveCount == 1 && !g.Session.AiPending,
                    "Switching modes restores the independent AI slot behind an explicit resume gate");
                string aiId = g.Session.SessionId;
                g.ClickField("confirmButton");
                Require(g.Session.CanSelect && g.Session.HumanStone == GomokuStone.White, "AI slot resumes the saved human white turn");
                g.TapCell(82); g.ClickField("confirmButton"); g.Page.Suspend(); g.ClickField("localMode");
                Require(g.Session.SessionId == localId && g.Session.Game.MoveCount == 2 && g.Session.BlackPlayer == 2 && g.Session.Paused && !g.Session.SelectedIndex.HasValue,
                    "Returning to local preserves its own board, ownership and identity, while discarding previews");
                var data = Get<GomokuSaveData>(g.Page, "data");
                Require(data.Slots[(int)GomokuMode.HumanVsAi].SessionId == aiId && data.Slots[(int)GomokuMode.HumanVsAi].Moves.Length == 2,
                    "The AI slot retains its own committed moves independently");
                g.Page.Leave(); g.Page.Enter();
                Require(g.Session.Paused && g.Session.SessionId == localId, "Leaving and reentering never auto-resumes a saved game");
            }
        }

        private static void CheckLifecycle(Fixture f)
        {
            using (var g = new PageFixture(f))
            {
                g.Click("确认开始"); g.TapCell(82); g.ClickField("pauseButton");
                Require(g.Session.Paused && !g.Session.SelectedIndex.HasValue && Get<GameObject>(g.Page, "pauseCover").activeSelf,
                    "Pause clears preview and displays an opaque cover");
                var cover = Get<GameObject>(g.Page, "pauseCover").GetComponent<Image>();
                Near(cover.color.a, 1, "Pause cover is opaque");
                g.TapCell(112, false);
                Require(!g.Session.SelectedIndex.HasValue && g.Session.Game.MoveCount == 0, "Covered board clicks cannot select or move");
                f.Capture("paused"); g.ClickField("confirmButton"); g.TapCell(82);
                g.Page.SetForeground(false); g.Page.SetForeground(true);
                Require(g.Session.Paused && !g.Session.SelectedIndex.HasValue && !g.Session.CanSelect, "Foreground return preserves the explicit resume gate and clears preview");
                g.ClickField("confirmButton");
                g.AcceptsInput = false; g.TapCell(112);
                Require(!g.Session.SelectedIndex.HasValue, "Host input gating blocks real board selection even before a repaint");
                g.AcceptsInput = true;
                var pointer = g.Surface.GetComponent<GomokuPointer>();
                Invoke(pointer, "LateUpdate");
                var down = f.Down(g.Surface, Cell(112), true);
                g.Page.Suspend(); f.Up(down); Invoke(pointer, "LateUpdate");
                Require(!g.Session.SelectedIndex.HasValue, "Suspension between DOWN and UP rejects the held pointer");
                g.ClickField("confirmButton"); g.TapCell(112);
                Require(g.Session.SelectedIndex == 112 && g.Session.Game.MoveCount == 0, "A fresh down works after explicit resume");
            }
        }

        private static void CheckAiWaiting(Fixture f)
        {
            using (var g = new PageFixture(f, Data(GomokuMode.HumanVsAi)))
            {
                g.Click("困难"); g.Click("确认开始"); g.TapCell(112); g.ClickField("confirmButton");
                Require(g.Session.Game.MoveCount == 1 && g.Session.AiPending && !Get<Button>(g.Page, "confirmButton").interactable,
                    "A confirmed human move enters visible AI waiting with confirmation disabled");
                g.TapCell(82);
                Require(!g.Session.SelectedIndex.HasValue && g.Session.Game.MoveCount == 1, "Board click during AI waiting cannot preselect or commit");
                f.Capture("ai-waiting");
                g.ClickField("undoButton");
                Require(g.Session.Game.MoveCount == 0 && !g.Session.AiPending && Get<object>(g.Page, "search") == null,
                    "Undo before the AI reply cancels its worker and withdraws the human move");
                g.TapCell(112); g.ClickField("confirmButton"); g.Page.SetForeground(false);
                Require(g.Session.Paused && !g.Session.AiPending && Get<object>(g.Page, "search") == null,
                    "Losing foreground cancels the pending AI worker");
                g.Page.SetForeground(true);
                Require(g.Session.Paused && Get<object>(g.Page, "search") == null, "Returning foreground does not silently restart AI");
                g.ClickField("confirmButton");
                Require(g.Session.AiPending, "Explicit resume schedules a new AI request");
                g.Page.Suspend();
            }
        }

        private static void CheckSharedHostLifecycle(Fixture f)
        {
            using (var host = new HostFixture(f))
            {
                var g = host.Game;
                g.Click("确认开始"); g.TapCell(82);
                f.Click(SharedIcon(g.Root, UiIconKind.Settings));
                var modal = Get<GameObject>(host.Shell, "modal");
                Require(modal != null && HasText(modal, "设置在所有游戏中共用"), "Gomoku Settings invokes the actual AppController settings modal");
                Require(g.Session.Paused && !g.Session.SelectedIndex.HasValue && !g.Session.CanSelect &&
                    Get<bool>(g.Page, "settingsOpen") && !Get<GomokuBoardView>(g.Page, "board").Interactable,
                    "Real host CreateModal/StopAnimation suspends the selected game before showing shared settings");
                f.Capture("host-settings"); f.Click(Find(modal.transform, "完成"));
                Require(g.SettingsCompleted == 1 && Get<GameObject>(host.Shell, "modal") == null &&
                    !Get<bool>(g.Page, "settingsOpen") && !g.Session.Paused && g.Session.CanSelect,
                    "Actual shared Done callback restores the previously active Gomoku turn");

                f.Click(SharedIcon(g.Root, UiIconKind.Settings));
                var oldFocusFinish = Get<Action>(host.Shell, "dismissModal");
                Invoke(host.Shell, "OnApplicationFocus", false); Invoke(host.Shell, "OnApplicationFocus", false);
                Require(Get<GameObject>(host.Shell, "modal") == null && Get<Action>(host.Shell, "dismissModal") == null && g.SettingsCompleted == 1,
                    "Repeated host focus loss directly closes settings without running user completion");
                Invoke(host.Shell, "OnApplicationFocus", true);
                Require(g.Session.Paused && !g.Session.CanSelect && !Get<bool>(g.Page, "settingsOpen"),
                    "Actual host focus return requires explicit Gomoku resume");
                oldFocusFinish();
                Require(g.Session.Paused && !g.Session.CanSelect && !Get<GomokuBoardView>(g.Page, "board").Interactable,
                    "Late old host settings completion cannot revive the paused game after focus return");
                f.Capture("host-focus-resume"); g.ClickField("confirmButton");
                Require(g.Session.CanSelect, "A real Continue button resumes after the host focus lifecycle");

                f.Click(SharedIcon(g.Root, UiIconKind.Settings));
                var oldPauseFinish = Get<Action>(host.Shell, "dismissModal");
                int completed = g.SettingsCompleted;
                Invoke(host.Shell, "OnApplicationPause", true); Invoke(host.Shell, "OnApplicationFocus", false);
                Require(Get<GameObject>(host.Shell, "modal") == null && g.SettingsCompleted == completed && g.Session.Paused,
                    "Actual host backgrounding closes shared settings without invoking its Done action");
                Invoke(host.Shell, "OnApplicationPause", false);
                Require(g.Session.Paused && !g.Session.CanSelect && !Get<bool>(g.Page, "foreground"),
                    "Pause(false) while the host remains unfocused does not restore input");
                Invoke(host.Shell, "OnApplicationFocus", true);
                Require(Get<bool>(g.Page, "foreground") && g.Session.Paused && !g.Session.CanSelect,
                    "Combined host Pause/Focus return remains behind an explicit resume gate");
                oldPauseFinish();
                Require(g.Session.Paused && !g.Session.CanSelect, "Late background settings completion remains inert after foreground returns");
                g.ClickField("confirmButton"); g.TapCell(112);
                Require(g.Session.SelectedIndex == 112 && g.Session.Game.MoveCount == 0,
                    "Fresh real board input works after explicit resume through the actual host lifecycle");
                Require(Get<object>(host.Shell, "store") == null && Get<object>(host.Shell, "game") == null && !host.Shell.gameObject.activeInHierarchy,
                    "Inert host never runs Awake or opens the normal player save store");
            }
        }

        private static void CheckSharedHostSaveStatus(Fixture f)
        {
            string directory = Path.Combine(output, "temporary-host-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string sharedDirectory = Path.Combine(directory, "shared");
            File.WriteAllText(sharedDirectory, "Fixture file blocks only shared storage.");
            try
            {
                var gameStore = new GomokuSaveStore(Path.Combine(directory, "gomoku"));
                using (var host = new HostFixture(f, gameStore))
                {
                    Set(host.Shell, "store", new LocalSaveStore(sharedDirectory));
                    Set(host.Shell, "game", new Game2048(new SeededRandom(2718)));
                    host.Game.Click("确认开始"); Invoke(host.Shell, "Save");
                    string warning = Get<string>(host.Shell, "saveWarning");
                    Require(Get<bool>(host.Shell, "saveFailed") && !string.IsNullOrEmpty(warning) && host.Game.Page.SaveError == null && File.Exists(gameStore.SavePath),
                        "Actual isolated shared-store failure occurs while Gomoku saves successfully");
                    Require(host.Status.text == warning, "AppController SetStatus prioritizes a shared save error over successful Gomoku status");
                    host.Game.Page.Save();
                    Require(host.Status.text == warning, "A later successful page Save cannot erase the host save failure");
                    host.SetPage("Lobby"); Invoke(host.Shell, "SetStatus", "ordinary lobby text");
                    Require(host.LobbyStatus.text == warning, "Lobby status also retains the shared save failure");
                    host.SetPage("Gomoku"); f.Capture("host-shared-save-error");
                    File.Delete(sharedDirectory); Invoke(host.Shell, "Save");
                    Require(!Get<bool>(host.Shell, "saveFailed") && host.Game.Page.SaveError == null && host.Status.text != warning,
                        "Repairing the actual shared store clears the aggregated failure on retry");
                    host.Game.Page.Save();
                    Require(string.IsNullOrEmpty(host.Status.text), "A healthy Gomoku refresh clears the ordinary host footer");
                }

                string pageDirectory = Path.Combine(directory, "blocked-gomoku");
                File.WriteAllText(pageDirectory, "Fixture file blocks only Gomoku storage.");
                using (var host = new HostFixture(f, new GomokuSaveStore(pageDirectory)))
                {
                    host.Game.Click("确认开始");
                    string pageError = host.Game.Page.SaveError;
                    Invoke(host.Shell, "SetStatus", "ordinary game text");
                    Require(pageError != null && host.Status.text == pageError && !Get<bool>(host.Shell, "saveFailed"),
                        "AppController aggregates the Gomoku-specific storage failure when shared storage has no failure");
                    host.SetPage("Lobby"); Invoke(host.Shell, "SetStatus", "ordinary lobby text");
                    Require(host.LobbyStatus.text == pageError, "Lobby aggregation includes the Gomoku page failure");
                    host.SetPage("Gomoku"); File.Delete(pageDirectory); host.Game.ClickField("retryButton");
                    Require(host.Game.Page.SaveError == null && string.IsNullOrEmpty(host.Status.text),
                        "Real page retry clears its error from the actual host footer");
                    host.SetPage("Lobby"); Invoke(host.Shell, "SetStatus", "healthy lobby");
                    Require(host.LobbyStatus.text == "healthy lobby", "Lobby aggregation releases a resolved Gomoku failure");
                    host.SetPage("Gomoku");
                }
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        private static void CheckTerminal(Fixture f, bool draw)
        {
            var initial = Data(GomokuMode.Local);
            initial.Slots[(int)GomokuMode.Local] = Slot(GomokuMode.Local,
                draw ? DrawHistory() : new[] { 110, 95, 111, 96, 112, 97, 113, 98, 114 });
            using (var g = new PageFixture(f, initial))
            {
                int count = g.Session.Game.MoveCount;
                Require(g.Session.Game.State == (draw ? GomokuState.Draw : GomokuState.BlackWon), "Injected history reconstructs the expected legal terminal state");
                Require(!Get<GomokuBoardView>(g.Page, "board").Interactable && !Get<Button>(g.Page, "undoButton").interactable &&
                    !Get<Button>(g.Page, "pauseButton").interactable && !Get<GameObject>(g.Page, "pauseCover").activeSelf,
                    "Terminal board is visible and read-only with undo/pause disabled");
                g.TapCell(82);
                Require(g.Session.Game.MoveCount == count && !g.Session.SelectedIndex.HasValue, "Terminal board clicks cannot select or place another move");
                if (!draw) RequireMeshColor(f, g, UiFactory.Hex("BD8450"), "Winning line appears in the production board mesh");
                f.Capture(draw ? "draw-board" : "won-board"); g.ClickField("confirmButton");
                Require(g.Dialog != null && HasText(g.Dialog, draw ? "和棋" : "玩家 1 获胜"), "Result dialog names the actual terminal result");
                f.Capture(draw ? "draw-result" : "won-result");
                g.Click("查看棋盘"); g.Page.SetForeground(false); g.Page.SetForeground(true);
                Require(g.Session.Game.MoveCount == count && !Get<GameObject>(g.Page, "pauseCover").activeSelf,
                    "Terminal foreground cycle preserves the visible completed board");
                g.ClickField("confirmButton"); g.Click("再来一局");
                var choice = Find(g.Dialog.transform, "玩家 2 执黑").GetComponent<Image>();
                Require(choice.color == UiFactory.Accent, "Local rematch defaults to exchanging black and white");
                g.Click("取消");
                Require(g.Session.Game.MoveCount == count && g.Session.Game.State != GomokuState.Playing, "Canceling rematch preserves the completed game");
            }
        }

        private static void CheckSaveFailure(Fixture f)
        {
            string directory = Path.Combine(output, "temporary-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string blocked = Path.Combine(directory, "blocked"); File.WriteAllText(blocked, "regular file blocks directory creation");
            try
            {
                var store = new GomokuSaveStore(blocked);
                using (var g = new PageFixture(f, store: store))
                {
                    g.Click("确认开始"); g.TapCell(112); g.ClickField("confirmButton");
                    Require(g.Page.SaveError != null && Get<Button>(g.Page, "retryButton").gameObject.activeSelf && g.Status.Contains("未保存"),
                        "Storage failure exposes retry and host status without losing the committed game");
                    f.Click(SharedIcon(g.Root, UiIconKind.Home));
                    Require(g.LobbyRequests == 0 && g.Dialog != null && HasText(g.Dialog, "进度尚未保存"), "Home blocks on a real save failure and offers a concrete decision");
                    f.Capture("save-failure-return"); g.Click("留在棋盘");
                    Require(g.Session.Game.MoveCount == 1 && g.LobbyRequests == 0, "Staying after a failed return retains the board");
                    File.Delete(blocked);
                    g.ClickField("retryButton");
                    var saved = store.Load(out var warning);
                    Require(saved != null && warning == null && saved.Slots[(int)GomokuMode.Local].Moves.Length == 1 && g.Page.SaveError == null && string.IsNullOrEmpty(g.Status),
                        "Retry writes the real isolated store and clears page plus host failure state");
                    f.Click(SharedIcon(g.Root, UiIconKind.Home));
                    Require(g.LobbyRequests == 1 && !g.Root.gameObject.activeSelf, "Successful Home save reaches the host and leaves the game");
                }
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        private static void CheckInvalidSave(Fixture f)
        {
            string directory = Path.Combine(output, "temporary-invalid-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var store = new GomokuSaveStore(directory);
            const string broken = "{invalid-gomoku-save";
            File.WriteAllText(store.SavePath, broken);
            try
            {
                using (var g = new PageFixture(f, store: store, loadStore: true))
                {
                    Require(g.Dialog != null && HasText(g.Dialog, "存档无法恢复"), "Invalid real store enters the explicit recovery gate");
                    f.Capture("invalid-save"); g.Page.Save();
                    Require(File.ReadAllText(store.SavePath) == broken, "Entering and saving cannot overwrite an unconfirmed invalid file");
                    g.Click("设置新局"); g.Click("取消"); g.Page.Save();
                    Require(File.ReadAllText(store.SavePath) == broken && g.Session == null, "Canceling recovery setup preserves the invalid source and creates no session");
                    g.Click("新一局"); g.Click("确认开始"); g.Page.Suspend();
                    Require(store.Load(out var warning) != null && warning == null && g.Page.SaveError == null,
                        "Only explicit new-game confirmation replaces the invalid file with validated state");
                }
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        private static GomokuSaveData Data(GomokuMode mode)
        { return new GomokuSaveData { SelectedMode = mode }; }

        private static GomokuSessionData Slot(GomokuMode mode, int[] moves, GomokuStone human = GomokuStone.Black, int blackPlayer = 1)
        { return new GomokuSessionData { SessionId = Guid.NewGuid().ToString("D"), Mode = mode, Difficulty = GomokuDifficulty.Normal,
            HumanStone = human, BlackPlayer = blackPlayer, Moves = moves }; }

        private static int[] DrawHistory()
        {
            var black = new List<int>(); var white = new List<int>();
            for (int i = 0; i < 225; i++) { if ((i / 15 * 2 + i % 15) % 4 < 2) black.Add(i); else white.Add(i); }
            var result = new int[225];
            for (int i = 0; i < result.Length; i++) result[i] = i % 2 == 0 ? black[i / 2] : white[i / 2];
            return result;
        }

        private static Vector2 Cell(int index)
        { return new Vector2((float)GomokuBoardGeometry.XAt(index), (float)GomokuBoardGeometry.YAt(index)); }

        private static void RequireMeshColor(Fixture f, PageFixture g, Color expected, string message)
        {
            f.Flush();
            var mesh = g.Surface.GetComponentInChildren<GomokuBoardGraphic>().canvasRenderer.GetMesh();
            Require(mesh != null, "Rendered board has a CanvasRenderer mesh");
            bool found = false;
            foreach (var color in mesh.colors)
                if (Mathf.Abs(color.r - expected.r) < .01f && Mathf.Abs(color.g - expected.g) < .01f &&
                    Mathf.Abs(color.b - expected.b) < .01f && Mathf.Abs(color.a - expected.a) < .01f) { found = true; break; }
            Require(found && mesh.vertexCount > 0, message);
        }

        private static RectTransform SharedIcon(Transform root, UiIconKind kind)
        {
            foreach (var icon in root.GetComponentsInChildren<UiIconGraphic>())
                if (icon.Kind == kind) return icon.GetComponentInParent<Button>().GetComponent<RectTransform>();
            throw new InvalidOperationException("Missing shared icon " + kind);
        }

        private static RectTransform Find(Transform root, string name)
        {
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name && rect.gameObject.activeInHierarchy) return rect;
            throw new InvalidOperationException("Missing active UI object " + name);
        }

        private static bool HasText(GameObject root, string value)
        {
            foreach (var label in root.GetComponentsInChildren<Text>()) if (label.text.Contains(value)) return true;
            return false;
        }

        private static Rect InPage(RectTransform root, RectTransform rect)
        {
            Canvas.ForceUpdateCanvases(); var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var a = root.InverseTransformPoint(corners[0]); var b = root.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(a.x - root.rect.xMin, root.rect.yMax - b.y, b.x - root.rect.xMin, root.rect.yMax - a.y);
        }

        private static T Get<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, Members);
            if (field == null) throw new InvalidOperationException(target.GetType().Name + " has no field " + name);
            return (T)field.GetValue(target);
        }

        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, Members);
            if (field == null) throw new InvalidOperationException(target.GetType().Name + " has no field " + name);
            field.SetValue(target, value);
        }

        private static object Invoke(object target, string name, params object[] args)
        {
            var method = target.GetType().GetMethod(name, Members);
            if (method == null) throw new InvalidOperationException(target.GetType().Name + " has no method " + name);
            return method.Invoke(target, args);
        }

        private static void Near(float actual, float expected, string message)
        { Require(Mathf.Abs(actual - expected) < .15f, message + " (" + actual + " vs " + expected + ")"); }
        private static void Require(bool condition, string message)
        { checks++; if (!condition) throw new InvalidOperationException(message); }
        private sealed class Runner : MonoBehaviour { }

        private sealed class SeededRandom : IRandomSource
        {
            private readonly System.Random random;
            public SeededRandom(int seed) { random = new System.Random(seed); }
            public int Next(int maximum) { return random.Next(maximum); }
        }

        private sealed class PageFixture : IDisposable
        {
            public readonly GomokuPage Page;
            public readonly RectTransform Root;
            public int LobbyRequests, SettingsOpened, SettingsCompleted;
            public string Status = "";
            public bool AcceptsInput = true;
            private Action settingsClosed;
            private readonly Fixture fixture;
            public GomokuSession Session { get { return Get<GomokuSession>(Page, "session"); } }
            public GameObject Dialog { get { return Get<GameObject>(Page, "dialog"); } }
            public RectTransform Surface { get { return Find(Root, "Gomoku board"); } }

            public PageFixture(Fixture fixture, GomokuSaveData data = null, GomokuSaveStore store = null, bool loadStore = false, AppController host = null)
            {
                this.fixture = fixture; Root = fixture.Page(); Page = Root.gameObject.AddComponent<GomokuPage>();
                Require(!loadStore || store != null, "Disk-loading fixtures always provide an isolated store");
                if (host != null) Set(host, "gomokuPage", Page);
                Page.Initialize(Root, () => { LobbyRequests++; Page.Leave(); }, after =>
                    {
                        SettingsOpened++; settingsClosed = () => { SettingsCompleted++; after?.Invoke(); };
                        if (host != null) typeof(AppController).GetMethod("OpenSettings", Members, null, new[] { typeof(Action) }, null)
                            .Invoke(host, new object[] { settingsClosed });
                    },
                    () => AcceptsInput && (host == null || Get<GameObject>(host, "modal") == null && !Get<bool>(host, "paused") && Get<bool>(host, "focused")),
                    () => false, () => false, null, status => { Status = status; if (host != null) Invoke(host, "SetStatus", status); },
                    loadStore ? null : data ?? Data(GomokuMode.Local), store);
                Require(ReferenceEquals(Get<object>(Page, "store"), store), "Fixture never opens the normal persistent-data store");
                Page.Enter();
            }

            public void Click(string name) { fixture.Click(Find(Root, name)); }
            public void ClickField(string field) { fixture.Click(Get<Button>(Page, field).GetComponent<RectTransform>()); }
            public void CloseSettings() { var close = settingsClosed; settingsClosed = null; close?.Invoke(); }
            public void TapCell(int index, bool requireBoard = true) { TapAt(Cell(index), requireBoard); }
            public void TapAt(Vector2 position, bool requireBoard = true)
            {
                var pointer = Surface.GetComponent<GomokuPointer>();
                Invoke(pointer, "LateUpdate");
                int moves = Session == null ? 0 : Session.Game.MoveCount;
                var down = fixture.Down(Surface, position, requireBoard);
                Require(Session == null || Session.Game.MoveCount == moves, "Pointer DOWN never commits a Gomoku move");
                fixture.Up(down); Invoke(pointer, "LateUpdate"); fixture.Flush();
                Require(Session == null || Session.Game.MoveCount == moves, "Pointer UP only preselects; confirmation remains required");
            }

            public void Dispose() { Page.Leave(); Object.DestroyImmediate(Root.gameObject); }
        }

        private sealed class HostFixture : IDisposable
        {
            public readonly AppController Shell;
            public readonly PageFixture Game;
            public readonly Text Status, LobbyStatus;
            private readonly GameObject inert;
            private readonly RectTransform statusBand;

            public HostFixture(Fixture fixture, GomokuSaveStore store = null)
            {
                inert = new GameObject("Inert Gomoku AppController fixture"); inert.SetActive(false);
                Shell = inert.AddComponent<AppController>();
                Set(Shell, "frame", fixture.Root); Set(Shell, "canvasRoot", fixture.Root);
                Set(Shell, "data", new SaveData { SoundEnabled = false, HapticsEnabled = false });
                SetPage("Gomoku");
                statusBand = UiFactory.Band(fixture.Root, "Host fixture status", 1278, 34, 1);
                Status = UiFactory.Label(statusBand, "", 56, 0, 608, 34, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
                LobbyStatus = UiFactory.Label(statusBand, "", 56, 0, 608, 34, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
                LobbyStatus.gameObject.SetActive(false);
                Set(Shell, "status", Status); Set(Shell, "lobbyStatus", LobbyStatus);
                Game = new PageFixture(fixture, store: store, host: Shell); statusBand.SetAsLastSibling();
            }

            public void SetPage(string page)
            { Set(Shell, "activePage", Enum.Parse(typeof(AppController).GetField("activePage", Members).FieldType, page)); }

            public void Dispose()
            {
                Invoke(Shell, "CloseModal"); Game.Dispose(); Set(Shell, "gomokuPage", null);
                Object.DestroyImmediate(inert); Object.DestroyImmediate(statusBand.gameObject);
            }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly int Height;
            public RectTransform Root { get { return root; } }
            private readonly GameObject owner;
            private readonly RectTransform root;
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly EventSystem events;

            public Fixture(int height)
            {
                Height = height; owner = new GameObject("Gomoku UI fixture");
                root = new GameObject("Fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
                root.SetParent(owner.transform, false); root.sizeDelta = new Vector2(720, height);
                camera = new GameObject("Fixture camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(owner.transform, false); camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
                camera.orthographicSize = height * .5f; camera.aspect = 720f / height;
                camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UiFactory.Paper;
                target = new RenderTexture(720, height, 24); target.Create(); camera.targetTexture = target;
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                events = new GameObject("Fixture events", typeof(EventSystem)).GetComponent<EventSystem>();
                events.transform.SetParent(owner.transform, false);
            }

            public RectTransform Page()
            { var page = UiFactory.Rect(root, "Gomoku fixture page", 0, 0, 720, Height); UiFactory.Stretch(page); return page; }
            public void Flush() { Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); }

            public PointerEventData Down(RectTransform rect, Vector2 position, bool requireTarget)
            {
                Flush();
                var data = new PointerEventData(events)
                {
                    position = RectTransformUtility.WorldToScreenPoint(camera,
                        rect.TransformPoint(new Vector2(rect.rect.xMin + position.x, rect.rect.yMax - position.y))),
                    pointerId = -1, button = PointerEventData.InputButton.Left
                };
                var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
                Require(hits.Count > 0, "Rendered control has a real raycast hit: " + rect.name);
                if (requireTarget)
                    Require(hits[0].gameObject.transform == rect || hits[0].gameObject.transform.IsChildOf(rect),
                        "Top rendered raycast reaches " + rect.name + ", hit " + hits[0].gameObject.name);
                data.pointerCurrentRaycast = data.pointerPressRaycast = hits[0];
                var pressed = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
                var clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                data.pointerPress = pressed != null ? pressed : clicked;
                return data;
            }

            public void Up(PointerEventData data)
            {
                if (data.pointerPress != null) ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
                var clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(data.pointerCurrentRaycast.gameObject);
                if (clicked != null && data.pointerPress == clicked) ExecuteEvents.Execute(clicked, data, ExecuteEvents.pointerClickHandler);
            }

            public void Click(RectTransform rect)
            { Up(Down(rect, new Vector2(rect.rect.width / 2, rect.rect.height / 2), true)); }

            public void Capture(string name)
            {
                Flush(); var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(720, Height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, 720, Height), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + "-720x" + Height + ".png"), image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }

            public void Dispose()
            {
                camera.targetTexture = null; if (RenderTexture.active == target) RenderTexture.active = null;
                Object.DestroyImmediate(owner); target.Release(); Object.DestroyImmediate(target);
            }
        }
    }
}
