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
using XanaduYonder.Core.Minesweeper;
using XanaduYonder.Core.Sudoku;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.Minesweeper;
using XanaduYonder.Runtime.Sudoku;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Uses rendered production controls and isolated in-memory page state. Never
    // initialize a page with its default Application.persistentDataPath store.
    [InitializeOnLoad]
    public static class MinesweeperUiChecks
    {
        private const string Pending = "Xanadu.MinesweeperUiChecks.Pending";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly StringBuilder report = new StringBuilder();
        private static string output;
        private static int checks;

        static MinesweeperUiChecks() { EditorApplication.playModeStateChanged += StateChanged; }

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
            new GameObject("Minesweeper checks runner").AddComponent<Runner>().StartCoroutine(RunAfterFrame());
        }

        private static IEnumerator RunAfterFrame()
        {
            yield return null;
            Execute();
        }

        private static void Execute()
        {
            var oldFont = UiFactory.Font;
            int exit = 0;
            checks = 0;
            report.Clear();
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/validation/minesweeper-ui"));
            Directory.CreateDirectory(output);
            foreach (string oldImage in Directory.GetFiles(output, "*.png")) File.Delete(oldImage);
            report.AppendLine("Unity " + Application.unityVersion + "; production UGUI + Camera.Render; injected in-memory profiles");
            try
            {
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Require(UiFactory.Font != null, "Bundled Noto CJK font loads");
                var pageType = typeof(AppController).Assembly.GetType("XanaduYonder.Runtime.Minesweeper.MinesweeperPage");
                Require(pageType != null, "Production MinesweeperPage exists");
                foreach (int height in new[] { 1320, 1582 })
                using (var fixture = new Fixture(height))
                {
                    CheckNavigationWorldAlignment(fixture);
                    CheckImmediateLongPress(fixture);
                    CheckExpandedViewAvailability(fixture);
                    CheckLegacySmallBoardFullscreen(fixture);
                    CheckSelectionAndSmallCells(fixture);
                    CheckReadyLifecycle(fixture);
                    CheckFocusOverviewAndPause(fixture);
                    CheckTerminal(fixture, false);
                    CheckTerminal(fixture, true);
                    CheckSharedHostLifecycle(fixture);
                    CheckSaveRetryStatus(fixture);
                    CheckTutorial(fixture);
                    CheckTouchTargetLayout(fixture);
                    report.AppendLine("PASS 720x" + height + ": selection, shared chrome, rendered board input, small cells, focus, overview, opaque pause, terminal/lifecycle");
                }
                report.AppendLine("MINESWEEPER_UI_CHECKS_PASS: " + checks + " assertions");
            }
            catch (Exception error)
            {
                exit = 1;
                report.AppendLine("MINESWEEPER_UI_CHECKS_FAIL after " + checks + " assertions\n" + error);
                Debug.LogException(error);
            }
            finally
            {
                UiFactory.Font = oldFont;
                File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
                Debug.Log(report.ToString());
                EditorApplication.Exit(exit);
            }
        }

        private static void CheckNavigationWorldAlignment(Fixture f)
        {
            var referenceRoot = f.Page(); referenceRoot.name = "Production Sudoku navigation reference";
            var reference = referenceRoot.gameObject.AddComponent<SudokuPage>();
            var catalog = SudokuCatalog.Parse(Resources.Load<TextAsset>("Sudoku/puzzles").text);
            var profile = new SudokuProfile();
            Set(reference, "root", referenceRoot); Set(reference, "catalog", catalog); Set(reference, "profile", profile);
            Set(reference, "session", new SudokuSession(profile.CreateGame(catalog.Get(0, 1)), false));
            Set(reference, "returnLobby", (Action)(() => { })); Set(reference, "openSettings", (Action)(() => { }));
            Set(reference, "acceptsInput", (Func<bool>)(() => true)); Set(reference, "setStatus", (Action<string>)(_ => { }));
            Invoke(reference, "BuildGame"); Invoke(reference, "BuildSelection");
            try
            {
                using (var game = new PageFixture(f))
                {
                    var referenceSelection = Get<GameObject>(reference, "selectionRoot");
                    var referenceGame = Get<GameObject>(reference, "gameRoot");
                    foreach (float width in new[] { 720f, 844.8f })
                    {
                        referenceRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                        game.Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                        referenceGame.SetActive(false); referenceSelection.SetActive(true);
                        CompareNavigation(f, Get<GameObject>(game.Page, "selectionRoot").transform, referenceSelection.transform,
                            new[] { UiIconKind.Home, UiIconKind.Book, UiIconKind.Settings }, "selection width " + width);
                        // Screenshot the actual reference hierarchy and the real Minesweeper
                        // hierarchy separately. Neither fixture opens a player save store.
                        if (width == 720)
                        {
                            game.Root.gameObject.SetActive(false); f.Capture("navigation-reference-selection");
                            referenceRoot.gameObject.SetActive(false); game.Root.gameObject.SetActive(true);
                            f.Capture("navigation-minesweeper-selection"); referenceRoot.gameObject.SetActive(true);
                        }
                        game.Page.StartGame(1, true);
                        referenceSelection.SetActive(false); referenceGame.SetActive(true);
                        CompareNavigation(f, Get<GameObject>(game.Page, "gameRoot").transform, referenceGame.transform,
                            new[] { UiIconKind.Back, UiIconKind.Restart, UiIconKind.Book, UiIconKind.Settings }, "game width " + width);
                        Invoke(game.Page, "ToggleFullscreen"); game.Page.Back();
                        CompareNavigation(f, Get<GameObject>(game.Page, "gameRoot").transform, referenceGame.transform,
                            new[] { UiIconKind.Back, UiIconKind.Restart, UiIconKind.Book, UiIconKind.Settings }, "game after focus width " + width);
                        if (width == 720)
                        {
                            game.Root.gameObject.SetActive(false); f.Capture("navigation-reference-game");
                            referenceRoot.gameObject.SetActive(false); game.Root.gameObject.SetActive(true);
                            f.Capture("navigation-minesweeper-game"); referenceRoot.gameObject.SetActive(true);
                        }
                        game.Page.Back();
                        referenceGame.SetActive(false); referenceSelection.SetActive(true);
                        CompareNavigation(f, Get<GameObject>(game.Page, "selectionRoot").transform, referenceSelection.transform,
                            new[] { UiIconKind.Home, UiIconKind.Book, UiIconKind.Settings }, "selection after game width " + width);
                    }
                }
            }
            finally { Object.DestroyImmediate(referenceRoot.gameObject); }
        }

        private static void CompareNavigation(Fixture f, Transform actual, Transform reference, UiIconKind[] kinds, string scenario)
        {
            foreach (var kind in kinds)
            {
                var actualRect = SharedIcon(actual, kind).GetComponent<RectTransform>();
                var referenceRect = SharedIcon(reference, kind).GetComponent<RectTransform>();
                var actualBounds = f.Bounds(actualRect); var expectedBounds = f.Bounds(referenceRect);
                Near(actualBounds.xMin, expectedBounds.xMin, "Rendered navigation left aligns with production Sudoku: " + scenario + " " + kind);
                Near(actualBounds.yMin, expectedBounds.yMin, "Rendered navigation bottom aligns with production Sudoku: " + scenario + " " + kind);
                Near(actualBounds.xMax, expectedBounds.xMax, "Rendered navigation right aligns with production Sudoku: " + scenario + " " + kind);
                Near(actualBounds.yMax, expectedBounds.yMax, "Rendered navigation top aligns with production Sudoku: " + scenario + " " + kind);
                report.AppendLine("NAV " + scenario + " " + kind + ": Minesweeper=" + actualBounds + " Sudoku=" + expectedBounds);
            }
        }

        private static void CheckImmediateLongPress(Fixture f)
        {
            string directory = Path.Combine(output, "temporary-hold-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new MinesweeperSaveStore(directory);
                using (var game = new PageFixture(f, store: store))
                {
                    game.Start(0);
                    var diskFlagsAtFeedback = new List<bool>();
                    Set(game.Page, "hapticsEnabled", (Func<bool>)(() => {
                        // Observe the real feedback call without vibrating hardware. Reading
                        // the isolated store here proves its ordering against synchronous Save.
                        var atFeedback = store.Load(out var feedbackWarning);
                        Require(atFeedback != null && feedbackWarning == null && atFeedback.Slots[0] != null,
                            "Threshold feedback observes a valid isolated save");
                        diskFlagsAtFeedback.Add(atFeedback.Slots[0].Board.IsFlagged(0));
                        return false;
                    }));
                    var pointer = Get<MinesweeperPointer>(game.Page, "pointer");
                    var position = game.Viewport.BoardToViewport(new MinesweeperInputPosition(.5, .5));
                    var surface = Find(Get<GameObject>(game.Page, "gameRoot").transform, "Minesweeper viewport");
                    f.RequireTopHit(surface, new Vector2((float)position.X, (float)position.Y));
                    long start = (long)(Time.realtimeSinceStartupAsDouble * 1000) + 5000;
                    HoldFrame(game, pointer, start, new MinesweeperInputContact[0]);
                    for (int round = 0; round < 2; round++)
                    {
                        bool previousFlag = round == 1;
                        long began = start + 1 + round * 1000;
                        HoldFrame(game, pointer, began, new[] { new MinesweeperInputContact(17, position.X, position.Y, MinesweeperInputPhase.Began) });
                        HoldFrame(game, pointer, began + 399, new[] { new MinesweeperInputContact(17, position.X, position.Y, MinesweeperInputPhase.Stationary) });
                        Require(game.Session.Game.GetSnapshot().IsFlagged(0) == previousFlag, "399 ms stationary does not commit a long press");
                        Require(diskFlagsAtFeedback.Count == round, "Before the threshold there is no flag feedback");
                        HoldFrame(game, pointer, began + 400, new[] { new MinesweeperInputContact(17, position.X, position.Y, MinesweeperInputPhase.Stationary) });
                        Require(game.Session.Game.GetSnapshot().IsFlagged(0) != previousFlag, "400 ms stationary immediately toggles the flag through the production pointer/page");
                        Require(diskFlagsAtFeedback.Count == round + 1 && diskFlagsAtFeedback[round] == previousFlag,
                            "Threshold feedback starts once before the synchronous flag save");
                        var saved = store.Load(out var warning);
                        Require(saved != null && warning == null && saved.Slots[0].Board.IsFlagged(0) != previousFlag,
                            "The threshold-time flag change is on disk before pointer release");
                        HoldFrame(game, pointer, began + 650, new[] { new MinesweeperInputContact(17, position.X, position.Y, MinesweeperInputPhase.Stationary) });
                        Require(game.Session.Game.GetSnapshot().IsFlagged(0) != previousFlag, "Continuing to hold does not repeat the threshold action");
                        Require(diskFlagsAtFeedback.Count == round + 1, "Continuing to hold does not repeat flag feedback");
                        HoldFrame(game, pointer, began + 800, new[] { new MinesweeperInputContact(17, position.X, position.Y, MinesweeperInputPhase.Ended) });
                        HoldFrame(game, pointer, began + 801, new MinesweeperInputContact[0]);
                        Require(game.Session.Game.GetSnapshot().IsFlagged(0) != previousFlag && !game.Session.Game.GetSnapshot().Generated,
                            "Release after a committed long press neither toggles again nor reveals");
                        Require(diskFlagsAtFeedback.Count == round + 1, "Release after a committed long press does not repeat flag feedback");
                        Require(game.Profile.GetSlot(0).Board.IsFlagged(0) != previousFlag && game.Session.ElapsedMilliseconds == 0,
                            "Long press remains saved and does not start a Ready timer");
                        Get<MinesweeperBoardView>(game.Page, "board").Tick(.2f);
                        f.Capture(round == 0 ? "hold-threshold-flagged" : "hold-threshold-unflagged");
                    }
                }
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        private static void HoldFrame(PageFixture game, MinesweeperPointer pointer, long time, MinesweeperInputContact[] contacts)
        {
            pointer.ProcessFrame(new MinesweeperInputFrame(time, game.Session.SessionId, game.Session.CanView,
                game.Session.CanInteract, game.Session.InputMode == MinesweeperInputMode.Flag, contacts));
        }

        private static void CheckExpandedViewAvailability(Fixture f)
        {
            using (var game = new PageFixture(f))
            {
                // Reuse one production page so disabled visuals must recover when
                // changing to a larger difficulty, and disable again on returning.
                foreach (int difficulty in new[] { 0, 1, 2, 0 })
                {
                    game.Page.StartGame(difficulty, true);
                    var root = Get<GameObject>(game.Page, "gameRoot").transform;
                    var overview = Find(root, "Overview");
                    var fullscreen = Find(root, "Enter fullscreen");
                    bool expanded = difficulty != 0;
                    Require(overview.GetComponent<Button>().interactable == expanded && fullscreen.GetComponent<Button>().interactable == expanded,
                        "Expanded-view actions match board size for difficulty " + difficulty);
                    string before = BoardFingerprint(game.Session.Game.GetSnapshot());
                    string id = game.Session.SessionId;
                    if (!expanded)
                    {
                        foreach (var control in new[] { overview, fullscreen })
                        {
                            Require(control.GetComponentInChildren<MinesweeperIconGraphic>().color == UiFactory.Muted &&
                                control.GetComponentInChildren<Text>().color == UiFactory.Muted,
                                "Small-board disabled action has visibly muted icon and label: " + control.name);
                            f.Click(control);
                        }
                        Invoke(game.Page, "ToggleOverview");
                        Invoke(game.Page, "ToggleFullscreen");
                        Require(!game.Viewport.IsOverview && !game.Session.Overview && !Get<bool>(game.Page, "fullscreen"),
                            "Small-board real clicks and direct callbacks cannot enter overview or fullscreen");
                        f.Click(Find(root, "Zoom in"));
                        Near(game.Viewport.ZoomScale, 1.25, "Small boards retain normal zoom interaction");
                        Require(!overview.GetComponent<Button>().interactable && !fullscreen.GetComponent<Button>().interactable,
                            "Zooming a small board does not unlock expanded-view actions");
                        f.Click(Find(root, "Zoom value"));
                        f.Capture("small-board-disabled-view-actions");
                    }
                    else
                    {
                        Require(overview.GetComponentInChildren<MinesweeperIconGraphic>().color == UiFactory.Accent &&
                            overview.GetComponentInChildren<Text>().color == UiFactory.Accent,
                            "Enabled overview returns to the shared green accent after changing difficulty");
                        f.RequireInkInFourQuadrants(overview.GetComponentInChildren<MinesweeperIconGraphic>().rectTransform);
                        f.Click(overview);
                        Require(game.Viewport.IsOverview && game.Session.Overview, "Larger board can enter overview: " + difficulty);
                        game.Page.Back();
                        f.Click(fullscreen);
                        Require(Get<bool>(game.Page, "fullscreen"), "Larger board can enter fullscreen: " + difficulty);
                        game.Page.Back();
                        Require(!Get<bool>(game.Page, "fullscreen") && !Get<bool>(game.Page, "choosing"),
                            "Larger-board fullscreen Back returns to its ordinary game page");
                    }
                    Require(game.Session.SessionId == id && BoardFingerprint(game.Session.Game.GetSnapshot()) == before,
                        "View-action availability does not replace or mutate the board: " + difficulty);
                }
            }
        }

        private static void CheckLegacySmallBoardFullscreen(Fixture f)
        {
            foreach (bool active in new[] { false, true })
            {
                var legacy = new MinesweeperSession(0, new SeededRandom(2718), () => 0);
                legacy.Execute(legacy.SessionId, 0, true);
                if (active)
                {
                    legacy.Execute(legacy.SessionId, 40);
                    Require(legacy.Game.State == MinesweeperState.Active, "Legacy fixture has an unfinished generated board");
                }
                legacy.SetInputMode(MinesweeperInputMode.Flag);
                legacy.SetView(true, 1.25, 4.5, 4.5);
                var profile = new MinesweeperProfile { TutorialSeen = true };
                var data = profile.Capture(legacy);
                if (active) data.Slots[0].ElapsedMilliseconds = 12345;
                Require(MinesweeperSaveCodec.TryDecode(MinesweeperSaveCodec.Encode(data), out var decoded, out var reason),
                    "Previously valid small-board fullscreen save remains readable: " + reason);
                var saved = decoded.Slots[0];
                using (var game = new PageFixture(f, initialProfile: new MinesweeperProfile(decoded)))
                {
                    game.Page.StartGame(0, false);
                    Require(!Get<bool>(game.Page, "fullscreen") && !game.Session.Fullscreen && !game.Profile.GetSlot(0).Fullscreen,
                        "Restoring an old small-board fullscreen save normalizes page, session and saved slot to ordinary layout");
                    Require(game.Session.SessionId == saved.SessionId && BoardFingerprint(game.Session.Game.GetSnapshot()) == BoardFingerprint(saved.Board),
                        "Small-board fullscreen normalization preserves the saved game and board");
                    Require(game.Session.InputMode == saved.InputMode && game.Session.ElapsedMilliseconds == saved.ElapsedMilliseconds && game.Session.Paused == active,
                        "Small-board fullscreen normalization preserves mode, elapsed time and the active-game resume gate");
                    Near(game.Viewport.ZoomScale, saved.ZoomScale, "Small-board fullscreen normalization retains zoom");
                    Near(game.Viewport.CenterColumn, saved.CenterColumn, "Small-board fullscreen normalization retains a legal column center");
                    Near(game.Viewport.CenterRow, saved.CenterRow, "Small-board fullscreen normalization retains a legal row center");
                    var root = Get<GameObject>(game.Page, "gameRoot").transform;
                    Require(Find(root, "Minesweeper title").gameObject.activeInHierarchy && !Find(root, "Enter fullscreen").GetComponent<Button>().interactable,
                        "Restored small board displays ordinary chrome with fullscreen disabled");
                    if (active) CheckOpaqueCover(f, root);
                }
            }
        }

        private static void CheckSelectionAndSmallCells(Fixture f)
        {
            using (var game = new PageFixture(f))
            {
                var page = game.Page;
                var selection = Get<GameObject>(page, "selectionRoot");
                Require(selection.activeSelf, "Entering Minesweeper opens difficulty selection");
                var selectionTitle = TextWithValue(selection.transform, "扫雷");
                Require(selectionTitle.font == UiFactory.Font && selectionTitle.fontSize == 52 && selectionTitle.fontStyle == FontStyle.Bold && selectionTitle.color == UiFactory.Ink,
                    "Selection heading follows the existing selection-page typography");
                for (int i = 0; i < 3; i++)
                {
                    Require(Find(selection.transform, "Difficulty " + i) != null, "Difficulty card exists: " + i);
                    var start = Find(selection.transform, "Start " + i);
                    Require(start.GetComponent<Button>().interactable, "Fresh difficulty has a start action: " + i);
                    var card = f.Bounds((RectTransform)start.parent);
                    var action = f.Bounds(start);
                    Near(action.xMin - card.xMin, card.xMax - action.xMax,
                        "A fresh card's sole action fills its row with balanced horizontal padding: " + i);
                }
                f.Capture("selection");
                f.Click(SharedIcon(selection.transform, UiIconKind.Settings).transform as RectTransform);
                Require(game.SettingsOpened == 1, "Selection shared settings icon invokes host settings");
                game.CloseSettings();
                f.Click(SharedIcon(selection.transform, UiIconKind.Home).transform as RectTransform);
                Require(game.LobbyRequests == 1, "Selection shared home icon invokes lobby navigation");
                f.Click(Find(selection.transform, "Start 0"));
                game.SeedReadySession(0);
                var session = game.Session;
                var root = Get<GameObject>(page, "gameRoot");
                Require(root.activeSelf && !selection.activeSelf, "Start action enters the actual game page");
                CheckChrome(f, root.transform);
                var viewport = Find(root.transform, "Minesweeper viewport");
                Require(viewport.GetComponent<RectMask2D>() != null, "Board viewport clips the rendered board");
                Require(viewport.GetComponentsInChildren<Button>(true).Length <= 1, "Cells do not contain competing button handlers");
                Require(Find(root.transform, "Minesweeper board").GetComponentsInChildren<Button>(true).Length == 0, "Rendered board has no per-cell Buttons");
                Require(viewport.GetComponent<MinesweeperPointer>() != null, "One production pointer owns the viewport");
                CheckLayout(f, root.transform);
                f.Capture("normal-ready");

                f.Click(Find(root.transform, "Flag mode"));
                game.MouseCell(0);
                Require(session.Game.GetSnapshot().IsFlagged(0) && session.Game.State == MinesweeperState.Ready && !session.Game.GetSnapshot().Generated,
                    "Real flag mode and board pointer mark without generating mines");
                Require(session.ElapsedMilliseconds == 0, "Pre-reveal flagging does not start the timer");
                game.MouseCell(0);
                Require(!session.Game.GetSnapshot().IsFlagged(0), "A second board press removes the flag");
                f.Click(Find(root.transform, "Zoom out"));
                Near(game.Viewport.ZoomScale, .75, "Zoom button reaches 75 percent");
                game.PressCell(2);
                Require(session.Game.GetSnapshot().IsFlagged(2), "Actual 75 percent cell press retains flag mode");
                game.PressCell(2);
                f.Capture("normal-75");
                f.Click(Find(root.transform, "Zoom out"));
                Near(game.Viewport.ZoomScale, .5, "Zoom button reaches 50 percent");
                Require(!Find(root.transform, "Zoom out").GetComponent<Button>().interactable, "Zoom-out is disabled at 50 percent");
                game.PressCell(1);
                Require(session.Game.GetSnapshot().IsFlagged(1), "Actual 50 percent cell press remains a flag operation");
                game.PressCell(1);
                f.Click(Find(root.transform, "Reveal mode"));
                game.PressCell(40);
                Require(session.Game.GetSnapshot().IsRevealed(40) && session.Game.GetSnapshot().Generated, "Actual 50 percent press reveals its target without auto zoom");
                Near(game.Viewport.ZoomScale, .5, "Small-cell reveal preserves the selected 50 percent scale");
                Require(session.Game.State == MinesweeperState.Active, "Seeded real board remains active after first reveal");
                var opened = session.Game.GetSnapshot();
                bool checkedNumber = false;
                for (int i = 0; i < opened.Width * opened.Height; i++)
                {
                    if (!opened.IsRevealed(i) || opened.AdjacentMines(i) == 0) continue;
                    var number = Find(root.transform, "Cell " + i).GetComponentInChildren<Text>();
                    Require(number.text == opened.AdjacentMines(i).ToString() && number.font == UiFactory.Font,
                        "Revealed production cell renders its actual adjacent-mine number in the shared font");
                    checkedNumber = true; break;
                }
                Require(checkedNumber, "Seeded board supplies a visible numbered-cell rendering check");
                f.Capture("normal-50-active");
                string beforeWhitespace = BoardFingerprint(session.Game.GetSnapshot());
                game.PressAt(new MinesweeperInputPosition(2, 2), 100);
                Require(BoardFingerprint(session.Game.GetSnapshot()) == beforeWhitespace, "Whitespace around the centered small board does not target an edge cell");
                f.Click(Find(root.transform, "Zoom value"));
                Near(game.Viewport.ZoomScale, 1, "Clicking the displayed zoom restores 100 percent");
                Require(Find(root.transform, "Zoom value").GetComponentInChildren<Text>().text.Contains("100"), "Displayed scale follows the reset");
                f.Capture("normal-active");

                // A modal must hide the board before the host displays settings.
                f.Click(SharedIcon(root.transform, UiIconKind.Settings).transform as RectTransform);
                Require(game.SettingsOpened == 2 && session.Paused, "Active settings pauses before invoking the host");
                CheckOpaqueCover(f, root.transform);
                game.CloseSettings();
                Require(session.Paused, "Closing active settings requires explicit resume");
                f.Click(Find(root.transform, "Resume"));
                Require(!session.Paused, "Explicit resume restores active play");
            }
        }

        private static void CheckReadyLifecycle(Fixture f)
        {
            using (var game = new PageFixture(f))
            {
                game.Start(0);
                var root = Get<GameObject>(game.Page, "gameRoot").transform;
                var session = game.Session;
                game.Page.SetForeground(false); game.Page.SetForeground(true);
                Require(session.Game.State == MinesweeperState.Ready && !session.Paused && session.ElapsedMilliseconds == 0,
                    "Unpaused Ready foreground return preserves a zero-time ready board");
                f.Click(SharedIcon(root, UiIconKind.Book).transform as RectTransform);
                Require(Get<GameObject>(game.Page, "dialog") != null && session.Blocked, "Rules opens through the shared book icon and blocks the ready board");
                CheckOpaqueCover(f, root, false);
                f.Capture("rules");
                game.Page.Back();
                Require(Get<GameObject>(game.Page, "dialog") == null && !session.Paused && session.ElapsedMilliseconds == 0,
                    "Closing rules on unpaused Ready restores Ready without a resume gate");
                f.Click(Find(root, "Pause"));
                Require(session.Paused, "Ready can be manually paused");
                game.Page.SetForeground(false); game.Page.SetForeground(true);
                Require(session.Paused && session.Game.State == MinesweeperState.Ready, "Manual Ready pause survives foreground changes");
                CheckOpaqueCover(f, root);
                f.Click(Find(root, "Resume"));
                Require(!session.Paused && session.Game.State == MinesweeperState.Ready && session.ElapsedMilliseconds == 0,
                    "Resuming Ready does not generate a board or start time");
            }
        }

        private static void CheckFocusOverviewAndPause(Fixture f)
        {
            using (var game = new PageFixture(f))
            {
                game.Start(2);
                var page = game.Page;
                var root = Get<GameObject>(page, "gameRoot").transform;
                var session = game.Session;
                game.PressCell(0);
                Require(session.Game.State == MinesweeperState.Active, "Expert fixture begins through the actual board pointer");
                game.Viewport.RestoreNormal(.75, 15, 8);
                Invoke(page, "Refresh");
                var before = BoardFingerprint(session.Game.GetSnapshot());
                string id = session.SessionId;
                double centerX = game.Viewport.CenterColumn, centerY = game.Viewport.CenterRow;
                float normalHeight = Find(root, "Minesweeper viewport").rect.height;
                f.Click(Find(root, "Flag mode"));
                f.Click(Find(root, "Enter fullscreen"));
                Require(Get<bool>(page, "fullscreen"), "Enter fullscreen switches the application layout");
                Require(Find(root, "Minesweeper viewport").rect.height > normalHeight, "Focus layout gives additional height to the board");
                Require(!Find(root, "Minesweeper title", false).gameObject.activeInHierarchy, "Focus layout hides the large heading");
                Near(game.Viewport.ZoomScale, .75, "Fullscreen preserves selected zoom");
                Near(game.Viewport.CenterColumn, centerX, "Fullscreen preserves desired column center");
                Near(game.Viewport.CenterRow, centerY, "Fullscreen preserves desired row center");
                Require(session.SessionId == id && session.InputMode == MinesweeperInputMode.Flag && BoardFingerprint(session.Game.GetSnapshot()) == before,
                    "Fullscreen retains game identity, input mode and all board data");
                CheckLayout(f, root);
                f.Capture("focus-active");
                f.Click(Find(root, "Overview"));
                Require(game.Viewport.IsOverview && session.Overview, "Overview is an explicit view and session state");
                Require(!Find(root, "Flag mode").GetComponent<Button>().interactable && !Find(root, "Zoom in").GetComponent<Button>().interactable,
                    "Overview disables mode and ordinary zoom controls");
                double elapsed = session.ElapsedMilliseconds;
                game.Clock += 2500;
                Require(session.ElapsedMilliseconds >= elapsed + 2500, "Active overview continues accumulating foreground time");
                Invoke(page, "Refresh");
                f.Capture("focus-overview");
                game.PressAt(game.Viewport.BoardToViewport(new MinesweeperInputPosition(15, 8)), 500);
                Require(game.Viewport.IsOverview && BoardFingerprint(session.Game.GetSnapshot()) == before,
                    "Overview long press neither locates nor changes cells");
                game.PressAt(game.Viewport.BoardToViewport(new MinesweeperInputPosition(15, 8)), 100);
                Require(!game.Viewport.IsOverview && !session.Overview && BoardFingerprint(session.Game.GetSnapshot()) == before,
                    "Overview short press only returns to a localized normal view");
                Near(game.Viewport.ZoomScale, .75, "Overview location restores the prior actionable scale");
                Require(Get<bool>(page, "fullscreen"), "Overview location preserves focus layout");
                f.Click(Find(root, "Overview"));
                page.Back();
                Require(!game.Viewport.IsOverview && Get<bool>(page, "fullscreen"), "Hardware Back consumes overview before focus layout");
                page.Back();
                Require(!Get<bool>(page, "fullscreen") && !Get<bool>(page, "choosing"), "Next hardware Back leaves focus before the game page");
                Near(game.Viewport.ZoomScale, .75, "Focus exit preserves zoom");
                f.Click(Find(root, "Overview"));
                f.Capture("normal-overview");
                f.Click(Find(root, "Pause"));
                Require(session.Paused && !game.Viewport.IsOverview, "Pausing overview returns to the normal view and pauses");
                CheckOpaqueCover(f, root);
                elapsed = session.ElapsedMilliseconds;
                game.Clock += 5000;
                Require(session.ElapsedMilliseconds == elapsed, "Paused time is frozen");
                Require(!Find(root, "Overview").GetComponent<Button>().interactable && !Find(root, "Zoom in").GetComponent<Button>().interactable,
                    "Paused view disables overview and zoom");
                f.Capture("normal-paused");
                f.Click(Find(root, "Enter fullscreen"));
                Require(Get<bool>(page, "fullscreen") && session.Paused, "Layout changes retain the existing paused state");
                CheckOpaqueCover(f, root);
                f.Capture("focus-paused");
                f.Click(Find(root, "Resume"));
                Require(!session.Paused, "Pause cover has an effective resume action");
                f.Click(Find(root, "Overview"));
                page.SetForeground(false);
                Require(session.Paused && !game.Viewport.IsOverview && Get<GameObject>(page, "dialog") == null,
                    "Background closes overview and transient dialogs and pauses the active board");
                page.SetForeground(true);
                Require(session.Paused, "Foreground does not automatically resume an active board");
                CheckOpaqueCover(f, root);
                f.Capture("focus-returned-from-background");
                f.Click(Find(root, "Resume"));
                page.Back();
                f.Click(SharedIcon(root, UiIconKind.Restart).transform as RectTransform);
                var restart = Get<GameObject>(page, "dialog");
                Require(restart != null, "Restarting an active expert board requests confirmation");
                f.Click(ButtonWithText(restart.transform, "开始新局").transform as RectTransform);
                Require(game.Session.Game.State == MinesweeperState.Ready && game.Session.SessionId != id,
                    "Confirmed restart creates a distinct Ready expert game");
                Near(game.Viewport.ZoomScale, 1, "New game restores the default 100 percent scale");
                Near(game.Viewport.OffsetX, 0, "New expert game starts at the left edge even when replacing a saved slot");
                Near(game.Viewport.OffsetY, 0, "New expert game starts at the top edge even when replacing a saved slot");
            }
        }

        private static void CheckTerminal(Fixture f, bool won)
        {
            using (var game = new PageFixture(f))
            {
                game.Start(1);
                var state = new MinesweeperGame(1, new SeededRandom(157));
                state.Reveal(40);
                var snapshot = state.GetSnapshot();
                int target = -1;
                var revealed = snapshot.Revealed;
                var flags = snapshot.Flags;
                if (won)
                {
                    for (int i = 0; i < revealed.Length; i++)
                        if (!snapshot.IsMine(i) && i != 40 && snapshot.AdjacentMines(i) > 0) { target = i; break; }
                    for (int i = 0; i < revealed.Length; i++) revealed[i] = !snapshot.IsMine(i) && i != target;
                }
                else
                {
                    for (int i = 0; i < revealed.Length; i++) if (snapshot.IsMine(i)) { target = i; break; }
                    for (int i = 0; i < revealed.Length; i++)
                        if (!snapshot.IsMine(i) && !snapshot.IsRevealed(i)) { flags[i] = true; break; }
                }
                Require(target >= 0, "Terminal fixture has an actionable final cell");
                var board = new MinesweeperSnapshot(1, true, 40, MinesweeperState.Active, null, snapshot.Mines, revealed, flags);
                Require(board.TryValidate(out var reason), "Terminal fixture is a valid production snapshot: " + reason);
                game.ReplaceBoard(board, 12345);
                game.Viewport.RestoreNormal(.5, 8, 8);
                Invoke(game.Page, "Refresh");
                game.PressCell(target);
                var session = game.Session;
                var page = game.Page;
                var root = Get<GameObject>(page, "gameRoot").transform;
                var expected = won ? MinesweeperState.Won : MinesweeperState.Lost;
                Require(session.Game.State == expected, "Real final cell press reaches " + expected);
                var dialog = Get<GameObject>(page, "dialog");
                Require(dialog != null && ContainsText(dialog.transform, won ? "排雷成功" : "触雷了"), "Real result dialog describes " + expected);
                Require(ContainsText(dialog.transform, "再来一局") && ContainsText(dialog.transform, "选择难度") && ContainsText(dialog.transform, "查看棋盘"),
                    "Result dialog exposes the three approved actions");
                f.Capture(won ? "won-dialog" : "lost-dialog");
                f.Click(ButtonWithText(dialog.transform, "查看棋盘").transform as RectTransform);
                Require(Get<GameObject>(page, "dialog") == null && !session.Paused, "Viewing a terminal board closes the result without a resume gate");
                Require(!Find(root, "Pause").GetComponent<Button>().interactable, "Terminal pause control is disabled");
                string before = BoardFingerprint(session.Game.GetSnapshot());
                long elapsed = session.ElapsedMilliseconds;
                game.Clock += 4000;
                game.PressCell(target);
                Require(BoardFingerprint(session.Game.GetSnapshot()) == before && session.ElapsedMilliseconds == elapsed, "Terminal board presses cannot change rules or restart time");
                f.Click(Find(root, "Enter fullscreen"));
                f.Click(Find(root, "Overview"));
                Require(Get<bool>(page, "fullscreen") && game.Viewport.IsOverview, "Terminal larger board retains fullscreen and overview actions");
                f.Capture(won ? "won-overview" : "lost-overview");
                game.PressAt(game.Viewport.BoardToViewport(new MinesweeperInputPosition(8, 8)), 100);
                Require(!game.Viewport.IsOverview && session.Game.State == expected && session.ElapsedMilliseconds == elapsed,
                    "Terminal overview location cannot revive the game");
                Require(Get<GameObject>(page, "dialog") == null, "Terminal overview does not reopen the result");
                page.SetForeground(false);
                page.SetForeground(true);
                Require(session.Game.State == expected && !session.Paused && Get<GameObject>(page, "dialog") == null,
                    "Terminal foreground return is direct board viewing");
                page.Save(); page.CheckEndState(); page.Save();
                Require(Get<GameObject>(page, "dialog") == null, "Repeated terminal checks do not reopen dismissed results");
                var record = game.Profile.GetRecord(1);
                Require(won ? record.Wins == 1 && record.Losses == 0 : record.Losses == 1 && record.Wins == 0,
                    "Repeated result viewing and saves account for exactly one outcome");
                f.Capture(won ? "won-board" : "lost-board");
            }
        }

        private static void CheckChrome(Fixture f, Transform root)
        {
            var title = Find(root, "Minesweeper title").GetComponent<Text>();
            Require(title.text == "扫雷" && title.font == UiFactory.Font && title.fontSize == 49 && title.fontStyle == FontStyle.Bold && title.color == UiFactory.Ink,
                "Game title matches the established Sudoku font, size, weight and ink");
            Near(title.rectTransform.anchoredPosition.x, 36, "Game title retains the existing left inset");
            foreach (var item in new[] { UiIconKind.Back, UiIconKind.Restart, UiIconKind.Book, UiIconKind.Settings })
            {
                var button = SharedIcon(root, item);
                var rect = button.GetComponent<RectTransform>();
                Near(rect.rect.width, 58, "Shared navigation icon width: " + item);
                Near(rect.rect.height, 58, "Shared navigation icon height: " + item);
                Near(rect.anchoredPosition.x, item == UiIconKind.Back ? 56 : item == UiIconKind.Restart ? 126 : item == UiIconKind.Book ? 536 : 606,
                    "Shared navigation icon retains the existing horizontal position: " + item);
                Near(rect.anchoredPosition.y, -34, "Shared navigation icon retains the existing top position: " + item);
                Require(button.GetComponentInChildren<UiIconGraphic>().color == UiFactory.Ink, "Navigation icon uses shared ink: " + item);
                Require(button.targetGraphic.color == UiFactory.Hex("E7EDE4"), "Navigation button uses the shared surface: " + item);
            }
            var brand = TextWithValue(root, "XANADU / YONDER");
            Require(brand.fontSize == 19 && brand.color == UiFactory.Muted && brand.font == UiFactory.Font, "Brand typography matches existing game headers");
            Near(brand.rectTransform.anchoredPosition.x, 210, "Brand retains the existing header position");
            f.Flush();
            foreach (var icon in root.GetComponentsInChildren<UiIconGraphic>())
                Require(icon.canvasRenderer.GetMesh() != null && icon.canvasRenderer.GetMesh().vertexCount > 0, "Shared icon submits a real mesh: " + icon.Kind);
            var overview = Find(root, "Overview");
            var localIcons = overview.GetComponentsInChildren<MinesweeperIconGraphic>();
            Require(localIcons.Length == 1, "Overview uses one module-owned procedural icon");
            Require(localIcons[0].canvasRenderer.GetMesh() != null && localIcons[0].canvasRenderer.GetMesh().vertexCount > 0, "Overview icon submits a visible render mesh");
            CheckLocalButtonContents(f, root);
        }

        private static void CheckSharedHostLifecycle(Fixture f)
        {
            using (var host = new HostFixture(f))
            {
                var game = host.Game;
                game.Start(0); game.PressCell(0);
                var root = Get<GameObject>(game.Page, "gameRoot").transform;
                var session = game.Session;
                f.Click(SharedIcon(root, UiIconKind.Settings).transform as RectTransform);
                var modal = Get<GameObject>(host.Shell, "modal");
                Require(modal != null && ContainsText(modal.transform, "设置在所有游戏中共用"), "Page settings opens the actual shared host modal");
                Require(session.Blocked && session.Paused, "Actual shared settings blocks and pauses Active before display");
                CheckOpaqueCover(f, root, false);
                f.Capture("host-settings-active");
                f.Click(ButtonWithText(modal.transform, "完成").transform as RectTransform);
                Require(game.SettingsCompleted == 1 && Get<GameObject>(host.Shell, "modal") == null && session.Paused && !session.Blocked,
                    "User completion runs the real host dismiss callback but requires explicit Active resume");
                f.Click(Find(root, "Resume"));

                f.Click(SharedIcon(root, UiIconKind.Settings).transform as RectTransform);
                Invoke(host.Shell, "OnApplicationFocus", false);
                Invoke(host.Shell, "OnApplicationFocus", false);
                Require(Get<GameObject>(host.Shell, "modal") == null && Get<Action>(host.Shell, "dismissModal") == null && game.SettingsCompleted == 1,
                    "Focus loss closes shared settings directly without executing user completion");
                Invoke(host.Shell, "OnApplicationFocus", true);
                Require(session.Paused && !session.Blocked && session.Foreground, "Active host focus return stays covered and requires resume");
                CheckOpaqueCover(f, root);
                f.Capture("host-settings-focus-return");
                f.Click(Find(root, "Resume"));

                f.Click(SharedIcon(root, UiIconKind.Settings).transform as RectTransform);
                Invoke(host.Shell, "OnApplicationPause", true);
                Invoke(host.Shell, "OnApplicationFocus", false);
                Require(Get<GameObject>(host.Shell, "modal") == null && game.SettingsCompleted == 1, "Application pause also closes host settings without a dismiss callback");
                Invoke(host.Shell, "OnApplicationPause", false);
                Require(!session.Foreground && session.Paused, "Pause(false) while still unfocused does not expose the board");
                Invoke(host.Shell, "OnApplicationFocus", true);
                Require(session.Foreground && session.Paused && game.SettingsCompleted == 1, "Combined Pause/Focus return remains explicit-resume and idempotent");
                CheckOpaqueCover(f, root);
                f.Click(Find(root, "Resume"));

                var board = session.Game.GetSnapshot();
                int mine = -1;
                for (int i = 0; i < board.Width * board.Height; i++) if (board.IsMine(i)) { mine = i; break; }
                game.PressCell(mine);
                Require(session.Game.State == MinesweeperState.Lost, "Shared-host fixture reaches Lost through the real board");
                f.Click(ButtonWithText(Get<GameObject>(game.Page, "dialog").transform, "查看棋盘").transform as RectTransform);
                long elapsed = session.ElapsedMilliseconds;
                f.Click(SharedIcon(root, UiIconKind.Settings).transform as RectTransform);
                Invoke(host.Shell, "OnApplicationPause", true);
                Invoke(host.Shell, "OnApplicationPause", false);
                Invoke(host.Shell, "OnApplicationFocus", false);
                Invoke(host.Shell, "OnApplicationFocus", true);
                game.Clock += 4000;
                Require(Get<GameObject>(host.Shell, "modal") == null && Get<GameObject>(game.Page, "dialog") == null && game.SettingsCompleted == 1,
                    "Terminal host lifecycle neither restores settings nor reopens the dismissed result");
                Require(session.Game.State == MinesweeperState.Lost && !session.Paused && session.ElapsedMilliseconds == elapsed,
                    "Terminal shared-settings return never revives the game or timer");
                Require(!Find(root, "Pause").GetComponent<Button>().interactable && !Find(root, "Resume", false).gameObject.activeInHierarchy,
                    "Terminal shared-settings return has no playable resume action");
                Require(Get<object>(host.Shell, "store") == null && Get<object>(host.Shell, "game") == null,
                    "Inert host remains uninitialized and never opens normal player storage");
                f.Capture("host-terminal-focus-return");
            }
        }

        private static void CheckSaveRetryStatus(Fixture f)
        {
            string directory = Path.Combine(output, "temporary-save-retry-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(directory, "Intentional fixture file blocks directory creation.");
            try
            {
                var store = new MinesweeperSaveStore(directory);
                using (var host = new HostFixture(f, store))
                {
                    var game = host.Game;
                    game.Start(1);
                    game.Page.Save();
                    Require(game.Page.SaveError != null && host.Status.text.Contains("扫雷进度暂未保存"),
                        "Real storage failure is visible in the normal host status");
                    f.Capture("save-error-normal");
                    var root = Get<GameObject>(game.Page, "gameRoot").transform;
                    f.Click(Find(root, "Enter fullscreen"));
                    var compact = Get<Text>(game.Page, "compactStatus");
                    Require(compact.gameObject.activeInHierarchy && compact.text.Contains("扫雷进度暂未保存"),
                        "Focus layout preserves the real storage error in its compact status");
                    f.Capture("save-error-focus");
                    File.Delete(directory);
                    Directory.CreateDirectory(directory);
                    game.Page.Save();
                    Require(File.Exists(store.SavePath) && game.Page.SaveError == null, "A successful retry actually writes and clears SaveError");
                    Require(!compact.text.Contains("扫雷进度暂未保存"), "Successful retry clears the compact error status");
                    Require(!host.Status.text.Contains("扫雷进度暂未保存"), "Successful retry clears stale save failure from the normal host status");
                    f.Click(Find(root, "Exit fullscreen"));
                    Require(host.Status.gameObject.activeInHierarchy && !host.Status.text.Contains("扫雷进度暂未保存"),
                        "Returning to normal layout cannot resurrect the stale save warning");
                    f.Capture("save-retry-recovered");
                }
            }
            finally
            {
                if (File.Exists(directory)) File.Delete(directory);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void CheckTutorial(Fixture f)
        {
            using (var game = new PageFixture(f, tutorialSeen: false))
            {
                Require(game.Session == null, "Opening the first-use tutorial does not create a session");
                for (int i = 0; i < 3; i++) Require(game.Profile.GetSlot(i) == null, "Tutorial leaves difficulty slot empty: " + i);
                var dialog = Get<GameObject>(game.Page, "dialog");
                Require(dialog != null, "First entry opens the real tutorial dialog");
                var diagram = Find(dialog.transform, "Tutorial diagram");
                for (int i = 0; i < 9; i++) Require(Find(diagram, "Tutorial cell " + i) != null, "Tutorial has static example cell " + i);
                Require(diagram.GetComponentsInChildren<Button>().Length == 0 && diagram.GetComponentInChildren<MinesweeperPointer>() == null,
                    "Tutorial diagram cannot execute a real board operation");
                f.Capture("tutorial");
                f.Click(ButtonWithText(dialog.transform, "跳过").transform as RectTransform);
                Require(game.Profile.TutorialSeen && game.Session == null && Get<GameObject>(game.Page, "dialog") == null,
                    "Skipping tutorial records completion without starting a game");
                for (int i = 0; i < 3; i++) Require(game.Profile.GetSlot(i) == null, "Skipping tutorial leaves difficulty slot empty: " + i);
            }
        }

        private static void CheckTouchTargetLayout(Fixture f)
        {
            var widths = new[] { 720f, 844.8f };
            var minimums = new[] { 99f, 117f };
            for (int scenario = 0; scenario < widths.Length; scenario++)
            using (var game = new PageFixture(f))
            {
                game.Start(1);
                float minimum = minimums[scenario];
                game.Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, widths[scenario]);
                // Editor uses one pixel per design unit. Inject an equivalent logical
                // density so the real production Layout performs the full reflow.
                Set(game.Page, "cachedDensityDpi", minimum * 160 / 44);
                Invoke(game.Page, "Layout", true);
                CheckTargetRegions(game, minimum);
                Invoke(game.Page, "Pause");
                var root = Get<GameObject>(game.Page, "gameRoot").transform;
                var resume = Find(root, "Resume");
                Require(resume.rect.width + .01f >= minimum && resume.rect.height + .01f >= minimum,
                    "Paused resume meets the simulated logical touch target " + minimum);
                var resumeArea = InPage(game.Root, resume);
                var coverArea = InPage(game.Root, Find(root, "Pause cover"));
                Require(Encloses(coverArea, resumeArea), "Enlarged resume remains inside the opaque cover");
                Invoke(game.Page, "ToggleFullscreen");
                CheckTargetRegions(game, minimum);
                Require(game.Session.Paused, "Touch-target reflow into focus preserves pause state");
                Invoke(game.Page, "ToggleFullscreen");
                CheckTargetRegions(game, minimum);
            }
            report.AppendLine("PASS logical touch-target simulations: 99 units at width 720; 117 units at width 844.8 (layout geometry, not physical-device dp evidence)");
        }

        private static void CheckTargetRegions(PageFixture game, float minimum)
        {
            var root = Get<GameObject>(game.Page, "gameRoot").transform;
            bool fullscreen = Get<bool>(game.Page, "fullscreen");
            var viewportArea = InPage(game.Root, Find(root, "Minesweeper viewport"));
            var pageArea = new Rect(0, 0, game.Root.rect.width, game.Root.rect.height);
            var areas = new List<Rect>();
            foreach (string name in new[] { "Reveal mode", "Flag mode", "Zoom out", "Zoom value", "Zoom in", "Overview",
                fullscreen ? "Focus pause" : "Pause", fullscreen ? "Exit fullscreen" : "Enter fullscreen" })
            {
                var rect = Find(root, name);
                Require(rect.rect.width + .01f >= minimum && rect.rect.height + .01f >= minimum,
                    name + " meets simulated minimum target " + minimum);
                var area = InPage(game.Root, rect);
                Require(Encloses(pageArea, area), "Enlarged control remains within page: " + name);
                Require(!area.Overlaps(viewportArea), "Enlarged control does not cover the board: " + name);
                foreach (var previous in areas) Require(!previous.Overlaps(area), "Enlarged control does not overlap another control: " + name);
                areas.Add(area);
            }
            Require(Encloses(pageArea, viewportArea), "Touch-target reflow keeps a usable board viewport inside the page");
        }

        private static Rect InPage(RectTransform root, RectTransform child)
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var low = root.InverseTransformPoint(corners[0]);
            var high = root.InverseTransformPoint(corners[2]);
            return new Rect(low.x - root.rect.xMin, root.rect.yMax - high.y, high.x - low.x, high.y - low.y);
        }
        private static bool Encloses(Rect outer, Rect inner)
        { return inner.xMin >= outer.xMin - .5f && inner.yMin >= outer.yMin - .5f && inner.xMax <= outer.xMax + .5f && inner.yMax <= outer.yMax + .5f; }

        private static void CheckLayout(Fixture f, Transform root)
        {
            f.Flush();
            var viewport = Find(root, "Minesweeper viewport");
            var area = f.Bounds(viewport);
            Require(area.width > 0 && area.height > 0 && area.xMin >= 0 && area.xMax <= 720.5f && area.yMin >= 0 && area.yMax <= f.Height + .5f,
                "Board viewport stays inside the available page");
            var controls = new List<Rect>();
            string pause = Find(root, "Pause", false).gameObject.activeInHierarchy ? "Pause" : "Focus pause";
            foreach (string name in new[] { "Reveal mode", "Flag mode", "Zoom out", "Zoom value", "Zoom in", "Overview", pause })
            {
                var control = Find(root, name);
                var bounds = f.Bounds(control);
                Require(bounds.xMin >= 0 && bounds.xMax <= 720.5f && bounds.yMin >= 0 && bounds.yMax <= f.Height + .5f,
                    "Control fits the page at " + f.Height + ": " + name);
                Require(!area.Overlaps(bounds), "Controls occupy independent space outside the board: " + name);
                foreach (var previous in controls) Require(!previous.Overlaps(bounds), "Major control hit regions do not overlap: " + name);
                controls.Add(bounds);
            }
            CheckLocalButtonContents(f, root);
        }

        private static void CheckLocalButtonContents(Fixture f, Transform root)
        {
            foreach (var button in root.GetComponentsInChildren<Button>())
            {
                var icons = button.GetComponentsInChildren<MinesweeperIconGraphic>();
                if (icons.Length == 0) continue;
                var buttonArea = f.Bounds(button.GetComponent<RectTransform>());
                foreach (var label in button.GetComponentsInChildren<Text>())
                {
                    if (string.IsNullOrEmpty(label.text)) continue;
                    var labelArea = f.Bounds(label.rectTransform);
                    Require(labelArea.xMin >= buttonArea.xMin - .5f && labelArea.xMax <= buttonArea.xMax + .5f &&
                        labelArea.yMin >= buttonArea.yMin - .5f && labelArea.yMax <= buttonArea.yMax + .5f,
                        "Local button text fits its control: " + button.name);
                    foreach (var icon in icons)
                        Require(!labelArea.Overlaps(f.Bounds(icon.rectTransform)),
                            "Local button text and symbol use separate regions: " + button.name);
                }
            }
        }

        private static void CheckOpaqueCover(Fixture f, Transform root, bool checkPointer = true)
        {
            var cover = Find(root, "Pause cover");
            var image = cover.GetComponent<Image>();
            Require(image != null && image.color.a >= .999f && image.raycastTarget, "Pause cover is opaque and intercepts pointer input");
            var viewport = f.Bounds(Find(root, "Minesweeper viewport"));
            var bounds = f.Bounds(cover);
            Require(bounds.xMin <= viewport.xMin + .5f && bounds.xMax >= viewport.xMax - .5f && bounds.yMin <= viewport.yMin + .5f && bounds.yMax >= viewport.yMax - .5f,
                "Opaque pause cover encloses the entire viewport");
            if (checkPointer) f.RequireTopHit(cover, new Vector2(8, 8));
        }

        private static RectTransform Find(Transform root, string name, bool activeOnly = true)
        {
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name && (!activeOnly || rect.gameObject.activeInHierarchy)) return rect;
            throw new InvalidOperationException("Missing " + (activeOnly ? "active " : "") + name + " under " + root.name);
        }
        private static bool ContainsText(Transform root, string text)
        { foreach (var label in root.GetComponentsInChildren<Text>()) if (label.text.Contains(text)) return true; return false; }
        private static Text TextWithValue(Transform root, string text)
        { foreach (var label in root.GetComponentsInChildren<Text>()) if (label.text == text) return label; throw new InvalidOperationException("Missing text: " + text); }
        private static Button ButtonWithText(Transform root, string text)
        { foreach (var button in root.GetComponentsInChildren<Button>()) if (ContainsText(button.transform, text)) return button; throw new InvalidOperationException("Missing button: " + text); }
        private static Button SharedIcon(Transform root, UiIconKind kind)
        { foreach (var icon in root.GetComponentsInChildren<UiIconGraphic>()) if (icon.Kind == kind) return icon.GetComponentInParent<Button>(); throw new InvalidOperationException("Missing shared icon: " + kind); }
        private static void Near(double actual, double expected, string reason) { Require(Math.Abs(actual - expected) < .001, reason + " (" + actual + " vs " + expected + ")"); }
        private static string BoardFingerprint(MinesweeperSnapshot board)
        {
            var result = new StringBuilder(board.State.ToString());
            for (int i = 0; i < board.Width * board.Height; i++) result.Append(board.IsMine(i) ? 'M' : '.').Append(board.IsRevealed(i) ? 'R' : '.').Append(board.IsFlagged(i) ? 'F' : '.');
            return result.ToString();
        }

        private sealed class SeededRandom : IRandomSource
        {
            private readonly System.Random random;
            public SeededRandom(int seed) { random = new System.Random(seed); }
            public int Next(int exclusiveMax) { return random.Next(exclusiveMax); }
        }

        private sealed class PageFixture : IDisposable
        {
            public readonly MinesweeperPage Page;
            public readonly MinesweeperProfile Profile;
            public readonly RectTransform Root;
            public int LobbyRequests, SettingsOpened, SettingsCompleted;
            public long Clock;
            private long pointerTime;
            private readonly Fixture fixture;
            private Action settingsClosed;
            public MinesweeperSession Session { get { return Get<MinesweeperSession>(Page, "session"); } }
            public MinesweeperViewport Viewport { get { return Get<MinesweeperViewport>(Page, "viewport"); } }

            public PageFixture(Fixture fixture, AppController host = null, MinesweeperSaveStore store = null, bool tutorialSeen = true, MinesweeperProfile initialProfile = null)
            {
                this.fixture = fixture;
                Root = fixture.Page();
                Page = Root.gameObject.AddComponent<MinesweeperPage>();
                Profile = initialProfile ?? new MinesweeperProfile { TutorialSeen = tutorialSeen };
                var feedback = Root.gameObject.AddComponent<GameFeedback>();
                if (host != null) { Set(host, "minesweeperPage", Page); Set(host, "feedback", feedback); }
                Page.Initialize(Root, () => LobbyRequests++, after => {
                    SettingsOpened++; settingsClosed = () => { SettingsCompleted++; after?.Invoke(); };
                    if (host != null) typeof(AppController).GetMethod("OpenSettings", Members, null, new[] { typeof(Action) }, null).Invoke(host, new object[] { settingsClosed });
                },
                    () => host == null || Get<GameObject>(host, "modal") == null && !Get<bool>(host, "paused") && Get<bool>(host, "focused"),
                    () => false, () => false, feedback, text => { if (host != null) Invoke(host, "SetStatus", text); },
                    full => { if (host != null) Get<Text>(host, "status").gameObject.SetActive(!full); }, Profile, store);
                Require(ReferenceEquals(Get<object>(Page, "store"), store), "Injected profile uses only its explicitly supplied memory or temporary store");
                Page.Enter();
            }
            public void CloseSettings() { var close = settingsClosed; settingsClosed = null; close?.Invoke(); }
            public void Start(int difficulty)
            {
                fixture.Click(Find(Get<GameObject>(Page, "selectionRoot").transform, "Start " + difficulty));
                SeedReadySession(difficulty);
            }
            public void SeedReadySession(int difficulty)
            {
                var view = Viewport;
                var session = new MinesweeperSession(difficulty, new SeededRandom(2718), () => Clock);
                session.SetView(Get<bool>(Page, "fullscreen"), view.ZoomScale, view.CenterColumn, view.CenterRow);
                Set(Page, "session", session);
                Invoke(Page, "Refresh");
            }
            public void ReplaceBoard(MinesweeperSnapshot board, long elapsed)
            {
                var data = Session.Export();
                data.Board = board;
                data.ElapsedMilliseconds = elapsed;
                var session = new MinesweeperSession(data, new SeededRandom(2718), () => Clock);
                session.Enter(); session.Resume();
                Set(Page, "session", session);
                Invoke(Page, "Refresh");
            }
            public void PressCell(int index)
            {
                var board = Session.Game.GetSnapshot();
                PressAt(Viewport.BoardToViewport(new MinesweeperInputPosition(index % board.Width + .5, index / board.Width + .5)), 100);
            }
            public void MouseCell(int index)
            {
                var board = Session.Game.GetSnapshot();
                var position = Viewport.BoardToViewport(new MinesweeperInputPosition(index % board.Width + .5, index / board.Width + .5));
                var pointer = Get<MinesweeperPointer>(Page, "pointer");
                var makeFrame = Get<Func<MinesweeperInputContact[], MinesweeperInputFrame>>(pointer, "frame");
                var idle = makeFrame(new MinesweeperInputContact[0]);
                pointer.ProcessFrame(idle);
                var rect = Find(Get<GameObject>(Page, "gameRoot").transform, "Minesweeper viewport");
                fixture.RequireTopHit(rect, new Vector2((float)position.X, (float)position.Y));
                fixture.ClickAt(rect, new Vector2((float)position.X, (float)position.Y));
                Get<MinesweeperBoardView>(Page, "board").Tick(.2f);
                pointerTime = Math.Max(pointerTime, makeFrame(new MinesweeperInputContact[0]).TimeMilliseconds + 1000);
            }
            public void PressAt(MinesweeperInputPosition position, long duration)
            {
                var rect = Find(Get<GameObject>(Page, "gameRoot").transform, "Minesweeper viewport");
                fixture.RequireTopHit(rect, new Vector2((float)position.X, (float)position.Y));
                var pointer = Get<MinesweeperPointer>(Page, "pointer");
                Send(pointer, new MinesweeperInputContact[0]);
                Send(pointer, new[] { new MinesweeperInputContact(7, position.X, position.Y, MinesweeperInputPhase.Began) });
                pointerTime += duration;
                Send(pointer, new[] { new MinesweeperInputContact(7, position.X, position.Y, MinesweeperInputPhase.Ended) });
                Send(pointer, new MinesweeperInputContact[0]);
                Get<MinesweeperBoardView>(Page, "board").Tick(.2f);
                fixture.Flush();
            }
            private void Send(MinesweeperPointer pointer, MinesweeperInputContact[] contacts)
            {
                pointerTime++;
                Invoke(pointer, "ProcessFrame", new MinesweeperInputFrame(pointerTime, Session.SessionId, Session.CanView, Session.CanInteract,
                    Session.InputMode == MinesweeperInputMode.Flag, contacts));
            }
            public void Dispose() { Page.Leave(); Object.DestroyImmediate(Root.gameObject); }
        }

        private sealed class HostFixture : IDisposable
        {
            public readonly AppController Shell;
            public readonly PageFixture Game;
            public readonly Text Status;
            private readonly GameObject inert;
            private readonly RectTransform statusBand;
            public HostFixture(Fixture fixture, MinesweeperSaveStore store = null)
            {
                inert = new GameObject("Inert Minesweeper host fixture");
                inert.SetActive(false);
                Shell = inert.AddComponent<AppController>();
                Set(Shell, "frame", fixture.Root); Set(Shell, "canvasRoot", fixture.Root);
                Set(Shell, "data", new SaveData { SoundEnabled = false, HapticsEnabled = false });
                var pageField = typeof(AppController).GetField("activePage", Members);
                Set(Shell, "activePage", Enum.Parse(pageField.FieldType, "Minesweeper"));
                statusBand = UiFactory.Band(fixture.Root, "Host fixture status", 1278, 34, 1);
                Status = UiFactory.Label(statusBand, "", 56, 0, 608, 34, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
                Set(Shell, "status", Status);
                Game = new PageFixture(fixture, Shell, store);
                statusBand.SetAsLastSibling();
            }
            public void Dispose()
            {
                Invoke(Shell, "CloseModal");
                Game.Dispose(); Set(Shell, "minesweeperPage", null);
                Object.DestroyImmediate(inert); Object.DestroyImmediate(statusBand.gameObject);
            }
        }

        private static object Invoke(object target, string name, params object[] args)
        {
            var method = target.GetType().GetMethod(name, Members);
            Require(method != null, target.GetType().Name + " exposes expected method " + name);
            return method.Invoke(target, args);
        }

        private static T Get<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, Members);
            Require(field != null, target.GetType().Name + " exposes expected field " + name);
            return (T)field.GetValue(target);
        }

        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, Members);
            Require(field != null, target.GetType().Name + " exposes expected field " + name);
            field.SetValue(target, value);
        }

        private static void Require(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class Runner : MonoBehaviour { }

        private sealed class Fixture : IDisposable
        {
            public readonly int Height;
            public readonly RectTransform Root;
            private readonly GameObject owner;
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly EventSystem events;

            public Fixture(int height)
            {
                Height = height;
                owner = new GameObject("Minesweeper UI fixture");
                Root = new GameObject("Fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
                Root.SetParent(owner.transform, false);
                Root.sizeDelta = new Vector2(720, height);
                camera = new GameObject("Fixture camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(owner.transform, false);
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = height * .5f;
                camera.aspect = 720f / height;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = UiFactory.Paper;
                camera.cullingMask = ~(1 << 31);
                target = new RenderTexture(720, height, 24);
                target.Create();
                camera.targetTexture = target;
                var canvas = Root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                events = new GameObject("Fixture events", typeof(EventSystem)).GetComponent<EventSystem>();
                events.transform.SetParent(owner.transform, false);
            }

            public RectTransform Page()
            {
                var rect = UiFactory.Rect(Root, "Minesweeper fixture page", 0, 0, 720, Height);
                UiFactory.Stretch(rect);
                return rect;
            }

            public void Flush()
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
                Canvas.ForceUpdateCanvases();
            }

            public Rect Bounds(RectTransform rect)
            {
                Flush();
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                var low = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                var high = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
            }

            public void RequireTopHit(RectTransform rect, Vector2 topLeftPosition)
            {
                Flush();
                var world = rect.TransformPoint(new Vector2(rect.rect.xMin + topLeftPosition.x, rect.rect.yMax - topLeftPosition.y));
                var pointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(camera, world) };
                var hits = new List<RaycastResult>();
                events.RaycastAll(pointer, hits);
                Require(hits.Count > 0 && (hits[0].gameObject.transform == rect || hits[0].gameObject.transform.IsChildOf(rect)),
                    "Actual rendered raycast reaches " + rect.name + " at " + topLeftPosition +
                    (hits.Count == 0 ? " (no hit)" : " (hit " + hits[0].gameObject.name + ")"));
            }

            public void RequireInkInFourQuadrants(RectTransform rect)
            {
                var bounds = Bounds(rect);
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(720, Height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, 720, Height), 0, 0);
                    image.Apply();
                    var counts = new int[4];
                    for (int y = Mathf.Max(0, Mathf.CeilToInt(bounds.yMin)); y < Mathf.Min(Height, Mathf.FloorToInt(bounds.yMax)); y++)
                    for (int x = Mathf.Max(0, Mathf.CeilToInt(bounds.xMin)); x < Mathf.Min(720, Mathf.FloorToInt(bounds.xMax)); x++)
                    {
                        var pixel = image.GetPixel(x, y);
                        if (pixel.r < .35f && pixel.g < .5f && pixel.b < .45f)
                            counts[(x < bounds.center.x ? 0 : 1) + (y < bounds.center.y ? 0 : 2)]++;
                    }
                    foreach (int count in counts) Require(count > 3, "Overview icon paints visible ink in each of its four arrow quadrants");
                    // Arrowheads must be near the inner ends of all four shafts.
                    // This samples the rendered glyph, independent of its mesh generator.
                    float size = Mathf.Min(bounds.width, bounds.height);
                    for (int xSign = -1; xSign <= 1; xSign += 2)
                    for (int ySign = -1; ySign <= 1; ySign += 2)
                    {
                        Require(HasInk(image, bounds.center + new Vector2(.12f * xSign, .25f * ySign) * size, size * .045f) &&
                            HasInk(image, bounds.center + new Vector2(.25f * xSign, .12f * ySign) * size, size * .045f),
                            "Overview arrowhead points inward in quadrant " + xSign + "," + ySign);
                    }
                }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }

            private static bool HasInk(Texture2D image, Vector2 point, float radius)
            {
                for (int y = Mathf.Max(0, Mathf.FloorToInt(point.y - radius)); y <= Mathf.Min(image.height - 1, Mathf.CeilToInt(point.y + radius)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(point.x - radius)); x <= Mathf.Min(image.width - 1, Mathf.CeilToInt(point.x + radius)); x++)
                {
                    var pixel = image.GetPixel(x, y);
                    if (pixel.r < .35f && pixel.g < .5f && pixel.b < .45f) return true;
                }
                return false;
            }

            public void Capture(string name)
            {
                Flush();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(720, Height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, 720, Height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + "-720x" + Height + ".png"), image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }

            public void Click(RectTransform rect)
            {
                ClickAt(rect, new Vector2(rect.rect.width / 2, rect.rect.height / 2));
            }

            public void ClickAt(RectTransform rect, Vector2 topLeftPosition)
            {
                Flush();
                var pointer = new PointerEventData(events)
                {
                    position = RectTransformUtility.WorldToScreenPoint(camera,
                        rect.TransformPoint(new Vector2(rect.rect.xMin + topLeftPosition.x, rect.rect.yMax - topLeftPosition.y))),
                    pointerId = -1,
                    button = PointerEventData.InputButton.Left
                };
                var hits = new List<RaycastResult>();
                events.RaycastAll(pointer, hits);
                Require(hits.Count > 0, "Rendered control has a raycast hit: " + rect.name);
                pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
                var pressed = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
                var clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                pointer.pointerPress = pressed != null ? pressed : clicked;
                if (pointer.pointerPress != null) ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);
                if (clicked != null && pointer.pointerPress == clicked) ExecuteEvents.Execute(clicked, pointer, ExecuteEvents.pointerClickHandler);
            }

            public void Dispose()
            {
                camera.targetTexture = null;
                if (RenderTexture.active == target) RenderTexture.active = null;
                Object.DestroyImmediate(owner);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
