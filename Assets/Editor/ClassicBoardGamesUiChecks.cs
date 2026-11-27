using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core;
using XanaduYonder.Core.BoardGames;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.BoardGames;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    [InitializeOnLoad]
    public static class ClassicBoardGamesUiChecks
    {
        private const string Pending = "Xanadu.ClassicBoardGamesUiChecks.Pending";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static int assertions;

        static ClassicBoardGamesUiChecks() { EditorApplication.playModeStateChanged += StateChanged; }

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
            new GameObject("Classic board UI checks runner").AddComponent<Runner>().StartCoroutine(AfterFrame());
        }

        private static IEnumerator AfterFrame()
        {
            yield return null;
            var oldFont = UiFactory.Font;
            int exit = 0;
            using (var checks = CheckBoards())
            {
                while (true)
                {
                    try { if (!checks.MoveNext()) break; }
                    catch (Exception error)
                    {
                        exit = 1;
                        Debug.LogError("CLASSIC_BOARD_GAMES_UI_CHECKS_FAIL after " + assertions + " assertions");
                        Debug.LogException(error);
                        break;
                    }
                    yield return checks.Current;
                }
            }
            if (exit == 0) Debug.Log("CLASSIC_BOARD_GAMES_UI_CHECKS_PASS " + assertions + " assertions");
            UiFactory.Font = oldFont;
            EditorApplication.Exit(exit);
        }

        private static IEnumerator<object> CheckBoards()
        {
            UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            Require(UiFactory.Font != null, "The production Chinese font loads.");
            CheckXiangqiRiverLayer();
            using (var fixture = new Fixture(new Vector2Int(360, 720)))
                fixture.CheckModalFocusAndFeedbackRetry();
            using (var fixture = new Fixture(new Vector2Int(360, 720)))
            {
                fixture.Enter(BoardGameId.Chess, "EnterChess");
                string blocked = fixture.SavePath(BoardGameId.Chess) + ".tmp";
                Directory.CreateDirectory(blocked);
                fixture.Click(fixture.Navigation(UiIconKind.Settings));
                fixture.Click("显示棋盘坐标");
                Require(fixture.HasLabel("重试棋局保存"), "An unwritable slot offers an inline save retry.");
                fixture.Click("完成");
                fixture.Click(fixture.Navigation(UiIconKind.Home));
                Require(fixture.HasLabel("棋局尚未保存"), "A real failed write requires an explicit choice before leaving the match.");
                fixture.Capture("chess-unsaved-exit");
                DialogScreenshotAudit.CaptureCurrent(fixture.NamedRect("Board modal backdrop"));
                fixture.Back();
                Require(fixture.Page.gameObject.activeInHierarchy && !fixture.HasLabel("棋局尚未保存"), "Back preserves the previous match.");
                Directory.Delete(blocked);
            }
            Require(BoardTutorial.Create(BoardGameId.Checkers).Length >= 6, "Checkers teaches transit, stopping, and the endgame in addition to opening moves.");
            CheckCompleteTutorials();
            foreach (Vector2Int viewport in new[] { new Vector2Int(360, 720), new Vector2Int(320, 568) })
            {
                foreach (var game in new[] { BoardGameId.Army, BoardGameId.Xiangqi, BoardGameId.Chess, BoardGameId.Checkers })
                    using (var menus = new Fixture(viewport)) menus.CheckSettingsDirectory(game);
                using (var fixture = new Fixture(viewport))
                {
                    CheckArmy(fixture);
                    CheckStandardGame(fixture, BoardGameId.Xiangqi, "EnterXiangqi");
                    CheckStandardGame(fixture, BoardGameId.Chess, "EnterChess");
                    CheckCheckers(fixture);
                }
                using (var fixture = new Fixture(viewport))
                {
                    CheckDamagedSave(fixture, BoardGameId.Xiangqi, "EnterXiangqi");
                    CheckDamagedSave(fixture, BoardGameId.Army, "EnterArmyChess");
                }
                Debug.Log("PASS " + viewport.x + "×" + viewport.y + " board navigation, setup, board layout, and Army handoff");
            }
            CheckChessDrawFromSettings();
            CheckFinishedArmyReplay();
            using (var motion = CheckXiangqiMotionAndRecord())
                while (motion.MoveNext()) yield return motion.Current;
            using (var review = CheckHintsAndReplay())
                while (review.MoveNext()) yield return review.Current;
            foreach (var viewport in new[] { new Vector2Int(360, 720), new Vector2Int(320, 568) })
                using (var review = CheckCheckersReplay(viewport))
                    while (review.MoveNext()) yield return review.Current;
            using (var hint = CheckCheckersHints())
                while (hint.MoveNext()) yield return hint.Current;
            using (var fixture = new Fixture(new Vector2Int(360, 720)))
            {
                fixture.Enter(BoardGameId.Checkers, "EnterChineseCheckers");
                fixture.Click(fixture.Navigation(UiIconKind.Settings));
                fixture.Click("新一局");
                fixture.Click("5 台");
                fixture.Click("开始对弈");
                var match = fixture.Page.Match;
                var move = match.Position.LegalMoves().First(candidate => candidate.From >= 0 && candidate.PathLength == 0);
                fixture.Tap(move.From);
                fixture.Tap(move.To);
                fixture.Tap(move.To);
                float previous = 0;
                for (int seat = 1; seat <= 5; seat++)
                {
                    float deadline = Time.unscaledTime + 5;
                    while (match.MoveCount < seat + 1 && Time.unscaledTime < deadline) yield return null;
                    Require(match.MoveCount == seat + 1 && match.Position.CurrentSide == (seat + 1) % 6,
                        "Each computer plays exactly one legal turn in seat order.");
                    float now = Time.unscaledTime;
                    if (seat > 1)
                    {
                        Require(now - previous >= .96f, "Successive computer turns leave about a second to read the previous move.");
                        Debug.Log("Checkers computer interval: " + Mathf.RoundToInt((now - previous) * 1000) + " ms");
                    }
                    previous = now;
                }
            }
        }

        private static void CheckCompleteTutorials()
        {
            string[] routes = { "EnterArmyChess", "EnterXiangqi", "EnterChess", "EnterChineseCheckers" };
            for (int gameIndex = 0; gameIndex < routes.Length; gameIndex++)
            using (var fixture = new Fixture(new Vector2Int(360, 720)))
            {
                var game = (BoardGameId)gameIndex;
                var course = BoardTutorial.Create(game);
                fixture.Enter(game, routes[gameIndex]);
                var liveMatch = fixture.Page.Match;
                int liveMoves = liveMatch?.MoveCount ?? 0;
                fixture.Click(fixture.Navigation(UiIconKind.Settings));
                fixture.Click("新手教程");
                fixture.OpenTutorialLesson(course[course.Length - 1].Title);
                Require(fixture.HasLabel(course[course.Length - 1].Title), "The last lesson is reachable through the scrollable directory.");
                fixture.OpenTutorialLesson(course[0].Title);
                for (int index = 0; index < course.Length; index++)
                {
                    var lesson = course[index];
                    Require(fixture.HasLabel(lesson.Title), game + " tutorial presents " + lesson.Title);
                    switch (lesson.Kind)
                    {
                        case BoardLessonKind.Formation:
                            fixture.Click("试布阵");
                            fixture.Click("推荐阵型");
                            fixture.Click("随机布阵");
                            fixture.Click("确认布阵");
                            break;
                        case BoardLessonKind.FormationRestriction:
                            fixture.Click("试布阵");
                            Require(!fixture.Button("确认布阵").interactable, "A placement restriction must be tried before completing.");
                            fixture.Tap(lesson.From);
                            fixture.Tap(lesson.To);
                            Require(fixture.HasLabel(lesson.Outcome), "The actual formation validator refused the illegal exchange.");
                            fixture.Click("确认布阵");
                            break;
                        case BoardLessonKind.Handoff:
                            fixture.Click("演示遮挡");
                            fixture.CheckHandoff(1);
                            fixture.Reveal(1);
                            break;
                        case BoardLessonKind.Inspect: fixture.Click("查看结果"); break;
                        case BoardLessonKind.ClaimDraw: fixture.Click("申请和棋"); break;
                        case BoardLessonKind.Pass: fixture.Click("跳过本手"); break;
                        case BoardLessonKind.Blocked:
                            fixture.Tap(lesson.From);
                            fixture.Tap(lesson.To);
                            break;
                        case BoardLessonKind.Move:
                            fixture.Tap(lesson.From);
                            if (lesson.Route.Length == 0) fixture.Tap(lesson.To);
                            else foreach (int landing in lesson.Route) fixture.Tap(landing);
                            fixture.Tap(lesson.To);
                            if (lesson.Promotion != 0)
                            {
                                fixture.Click(BoardCatalog.PieceName(game, lesson.Promotion));
                                Require(Visible(fixture.Surface(), lesson.To).Kind == lesson.Promotion, "Each promotion produces the chosen piece.");
                            }
                            break;
                    }
                    Require(fixture.HasLabel(lesson.Outcome ?? "已完成本节"), game + " tutorial finishes " + lesson.Title);
                    fixture.Click(index == course.Length - 1 ? "完成教程" : "下一节");
                }
                Require(fixture.Page.Match == liveMatch && (liveMatch?.MoveCount ?? 0) == liveMoves, "The tutorial leaves the live board and its move history intact.");
                Debug.Log(game + " tutorials: " + course.Length + " lessons completed through production input.");
            }
        }

        private static IEnumerator<object> CheckHintsAndReplay()
        {
            var routes = new[] { "EnterArmyChess", "EnterXiangqi", "EnterChess" };
            for (int i = 0; i < routes.Length; i++)
            using (var fixture = new Fixture(new Vector2Int(360, 720)))
            {
                var game = (BoardGameId)i;
                fixture.Enter(game, routes[i]);
                if (game == BoardGameId.Army) fixture.Click("确认布阵");
                var match = fixture.Page.Match;
                fixture.Click(fixture.Navigation(UiIconKind.Settings));
                fixture.Click("显示提示按钮");
                fixture.Click("完成");
                fixture.Click("提示 3");
                float deadline = Time.unscaledTime + 6;
                while (match.HintsUsed == 0 && Time.unscaledTime < deadline) yield return null;
                var suggestion = fixture.Surface().Suggestion;
                Require(match.HintsUsed == 1 && match.MoveCount == 0 && suggestion.HasValue && match.Position.LegalMoves().Contains(suggestion.Value),
                    game + " presents a legal suggestion and consumes exactly one hint without playing.");
                Require(!fixture.Button("提示 2").interactable, "A visible suggestion cannot consume another hint in the same position.");
                fixture.Capture(BoardCatalog.Id(game) + "-hint");
                int from = suggestion.Value.From, to = suggestion.Value.To;
                fixture.Tap(from);
                fixture.Tap(to);
                fixture.Tap(to);
                Require(match.MoveCount == 1, game + " allows the suggested move to be played normally.");
                float humanPlayed = Time.unscaledTime;
                deadline = humanPlayed + 6;
                while (match.MoveCount < 2 && Time.unscaledTime < deadline) yield return null;
                Require(match.MoveCount == 2 && Time.unscaledTime - humanPlayed >= .47f, game + " waits before the computer responds.");
                Debug.Log(game + " human-to-computer interval: " + Mathf.RoundToInt((Time.unscaledTime - humanPlayed) * 1000) + " ms");
                fixture.Click(fixture.Navigation(UiIconKind.Settings));
                if (game == BoardGameId.Army)
                {
                    Require(!fixture.Button("终局后复盘").interactable, "Live Army cannot enter open-rank replay.");
                    var lines = match.DescribeHistory(match.Options.Human);
                    Require(fixture.HasLabel("1.  " + lines[0]) && !lines[0].Contains("暗棋") && fixture.HasLabel("2.  " + lines[1]) && lines[1].Contains("暗棋"),
                        "Live Army history names the viewer's piece and conceals the opponent's rank.");
                    continue;
                }
                Require(fixture.HasLabel("1.  " + match.DescribeHistory()[0]), "The record displays the piece and the move coordinates.");
                fixture.Click("复盘本局");
                Require(fixture.HasLabel("0 / 2") && !fixture.Surface().raycastTarget, "Replay starts at the untouchable opening board.");
                fixture.Click("下一手");
                Require(fixture.HasLabel("1 / 2") && fixture.HasLabel(match.DescribeHistory()[0]), "Manual replay advances and labels a single move.");
                fixture.Click("上一手");
                Require(fixture.HasLabel("0 / 2"), "Replay can return to the opening.");
                fixture.Click("自动播放");
                deadline = Time.unscaledTime + 4;
                while (!fixture.HasLabel("2 / 2") && Time.unscaledTime < deadline) yield return null;
                Require(fixture.HasLabel("2 / 2") && match.MoveCount == 2 && match.HintsUsed == 1,
                    "Autoplay reaches the end without touching the live match or its hints.");
                fixture.Capture(BoardCatalog.Id(game) + "-replay");
                fixture.Click(fixture.Navigation(UiIconKind.Back));
                Require(fixture.HasLabel("本局棋谱"), "Exiting replay restores the move list.");
            }
        }

        private static IEnumerator<object> CheckCheckersReplay(Vector2Int viewport)
        {
            using (var fixture = new Fixture(viewport))
            {
                var options = new BoardMatchOptions(false, BoardDifficulty.Normal, 6, human: 2);
                var sample = new BoardMatch(BoardGameId.Checkers, options);
                BoardMove chain = BoardMove.None;
                for (int ply = 0; ply < 120; ply++)
                {
                    if (sample.Computer)
                        chain = sample.Position.LegalMoves().Where(move => move.PathLength == 3).DefaultIfEmpty(BoardMove.None).First();
                    var move = chain != BoardMove.None ? chain : sample.Position.CaptureSearch()(BoardDifficulty.Easy, ply, CancellationToken.None);
                    Require(sample.Play(move), "The six-seat recording uses legal turns.");
                    if (chain != BoardMove.None) break;
                }
                Require(chain != BoardMove.None, "The six-seat recording contains a computer's three-hop turn.");
                fixture.WriteMatch(sample);
                fixture.Enter(BoardGameId.Checkers, "EnterChineseCheckers");
                fixture.Click(fixture.Navigation(UiIconKind.Settings));
                fixture.Click("复盘本局");
                var replay = (BoardReplay)Field(typeof(BoardGamePage), "replay").GetValue(fixture.Page);
                int turn = sample.MoveCount;
                Require(fixture.HasLabel("0 / " + turn), "Six-seat replay opens at the initial position.");
                while (replay.MoveIndex < turn - 1) replay.Advance();
                Method(typeof(BoardGamePage), "Render").Invoke(fixture.Page, null);
                int actor = replay.Position.CurrentSide;
                string actorName = sample.SideName(actor);
                Require(actorName.Contains("电脑"), "The replayed turn identifies a computer by number.");

                for (int playback = 0; playback < 2; playback++)
                {
                    fixture.Click(playback == 0 ? "下一手" : "自动播放");
                    for (int hop = 1; hop <= chain.PathLength; hop++)
                    {
                        string progress = "第 " + turn + " 手 · " + hop + " / " + chain.PathLength + " 跳";
                        float deadline = Time.unscaledTime + 3;
                        while (!(hop == chain.PathLength ? replay.MoveIndex == turn : fixture.HasLabel(progress)) && Time.unscaledTime < deadline)
                            yield return null;
                        var surface = fixture.Surface();
                        var marble = Visible(surface, chain.PathAt(hop - 1));
                        Require(marble.Kind > 0 && marble.Side == actor && fixture.HasLabel(actorName),
                            "Replay shows the named mover's marble at each recorded landing in order.");
                        Require(fixture.Bounds(fixture.NamedRect("Current seat")).Contains(fixture.Bounds(fixture.NamedRect("Seat " + (actor + 1))).center),
                            "The highlighted seat follows the actual mover during and after the turn.");
                        if (hop < chain.PathLength)
                            Require(replay.MoveIndex == turn - 1 && fixture.HasLabel(progress), "A preview landing does not commit a partial turn.");
                        if (playback == 0 && hop == 2) fixture.Capture("checkers-replay-hop");
                        if (playback == 1 && hop == 1)
                        {
                            fixture.Click("暂停自动");
                            float until = Time.unscaledTime + .4f;
                            while (Time.unscaledTime < until) yield return null;
                            Require(fixture.HasLabel(progress), "Pausing autoplay keeps the current landing on screen.");
                            fixture.Click("自动播放");
                        }
                    }
                    Require(replay.MoveIndex == turn && replay.LastMove == chain && fixture.Page.Match.MoveCount == turn,
                        "The final landing recreates the complete turn without modifying the saved match.");
                    if (playback == 0) fixture.Click("上一手");
                }
            }
        }

        private static IEnumerator<object> CheckCheckersHints()
        {
            using (var fixture = new Fixture(new Vector2Int(360, 720)))
            {
                fixture.Enter(BoardGameId.Checkers, "EnterChineseCheckers");
                fixture.Click(fixture.Navigation(UiIconKind.Settings));
                fixture.Click("显示提示按钮");
                fixture.Click("新一局");
                fixture.Click("本地双人");
                fixture.Click("开始对弈");
                var match = fixture.Page.Match;
                for (int use = 0; use < BoardMatch.HintLimit; use++)
                {
                    bool singleTap = use == BoardMatch.HintLimit - 1;
                    if (singleTap)
                    {
                        fixture.Click(fixture.Navigation(UiIconKind.Settings));
                        fixture.Click("单击落子");
                        fixture.Click("完成");
                    }
                    fixture.Click("提示 " + (BoardMatch.HintLimit - use));
                    float deadline = Time.unscaledTime + 5;
                    while (match.HintsUsed == use && Time.unscaledTime < deadline) yield return null;
                    var suggested = fixture.Surface().Suggestion;
                    Require(match.HintsUsed == use + 1 && match.MoveCount == use && suggested.HasValue &&
                        match.Position.LegalMoves().Contains(suggested.Value), "Checkers hints return a legal full move without playing it.");
                    var move = suggested.Value;
                    if (use == 0) fixture.Capture("checkers-hint");
                    fixture.Tap(move.From);
                    if (move.PathLength == 0) fixture.Tap(move.To);
                    else foreach (int landing in move.Path) fixture.Tap(landing);
                    if (!singleTap) fixture.Tap(move.To);
                    Require(match.LastMove == move, "The entire suggested route can be played.");
                }
                Require(!fixture.Button("提示 0").interactable, "The fourth hint is unavailable.");
                fixture.Click(fixture.Navigation(UiIconKind.Home));
                fixture.Enter(BoardGameId.Checkers, "EnterChineseCheckers");
                fixture.Click("继续对局");
                Require(fixture.Page.Match.HintsUsed == 3 && !fixture.Button("提示 0").interactable,
                    "The exhausted Checkers hint budget persists after reopening.");
            }
        }

        private static void CheckFinishedArmyReplay()
        {
            using (var fixture = new Fixture(new Vector2Int(360, 720)))
            {
                var formation = ArmyGame.CreateFormation(9);
                var options = new BoardMatchOptions(true, BoardDifficulty.Normal, 2);
                var match = new BoardMatch(BoardGameId.Army, options, formation, formation);
                var cycle = new[] { new BoardMove(31, 36), new BoardMove(26, 21), new BoardMove(36, 31), new BoardMove(21, 26) };
                for (int ply = 0; ply < 60; ply++) Require(match.Play(cycle[ply % cycle.Length]), "Army replay sample uses legal turns.");
                fixture.WriteMatch(match);
                fixture.Enter(BoardGameId.Army, "EnterArmyChess");
                fixture.Click(fixture.Navigation(UiIconKind.Settings));
                fixture.Click("复盘本局");
                var board = fixture.Surface();
                Require(Visible(board, ArmyGame.FormationPoint(0, 0)).Kind > 0 && Visible(board, ArmyGame.FormationPoint(1, 0)).Kind > 0,
                    "Ended Army replay shows both sides' ranks from the initial formations.");
                fixture.Click("下一手");
                Require(fixture.HasLabel("1 / 60"), "Army replays the first move independently of the ended board.");
                fixture.Capture("army-replay");
            }
        }

        private static void CheckChessDrawFromSettings()
        {
            using (var fixture = new Fixture(new Vector2Int(360, 720)))
            {
                fixture.Enter(BoardGameId.Chess, "EnterChess");
                fixture.Click(fixture.Navigation(UiIconKind.Settings));
                fixture.Click("新一局");
                fixture.Click("本地双人");
                fixture.Click("开始对弈");
                var cycle = new[] { (6, 21), (62, 45), (21, 6), (45, 62) };
                for (int repeat = 0; repeat < 2; repeat++)
                    foreach (var (source, destination) in cycle)
                    {
                        fixture.Tap(source);
                        fixture.Tap(destination);
                        fixture.Tap(destination);
                    }
                fixture.CheckNoMatchFooter();
                fixture.Click(fixture.Navigation(UiIconKind.Settings));
                fixture.Click("申请和棋");
                Require(fixture.Page.Match.Terminal, "A legal draw can still be claimed from Settings.");
                fixture.Click("回看棋盘");
                fixture.CheckNoMatchFooter();
                fixture.Click(fixture.Navigation(UiIconKind.Settings));
                fixture.Click("查看结算");
                Require(fixture.HasLabel("再来一局"), "The completed match reopens its result from Settings.");
            }
        }

        private static void CheckNavigation(Fixture fixture, BoardGameId game, string route)
        {
            fixture.Enter(game, route);
            fixture.Surface();
            var initialMatch = fixture.Page.Match;
            Require(!fixture.HasLabel("开始新一局") && !fixture.HasLabel("新手教程"), game + " lobby entry opens the board directly.");
            var icons = new[] { UiIconKind.Home, UiIconKind.Book, UiIconKind.Settings };
            var navigation = icons.Select(kind => fixture.Navigation(kind)).ToArray();
            float[] sharedX = { 56, 536, 606 };
            Rect pageBounds = fixture.Bounds(fixture.Page.transform);
            float scale = pageBounds.width / UiFactory.DesignWidth;
            for (int i = 0; i < navigation.Length; i++)
            {
                Rect bounds = fixture.Bounds(navigation[i].transform);
                Require(fixture.InViewport(bounds), game + " navigation is in the viewport: " + icons[i]);
                Require(Mathf.Abs(bounds.xMin - pageBounds.xMin - sharedX[i] * scale) < .5f &&
                    Mathf.Abs(pageBounds.yMax - bounds.yMax - 34 * scale) < .5f &&
                    Mathf.Abs(bounds.width - 58 * scale) < .5f && Mathf.Abs(bounds.height - 58 * scale) < .5f,
                    game + " uses the established game navigation geometry: " + icons[i]);
                for (int j = 0; j < i; j++)
                    Require(!bounds.Overlaps(fixture.Bounds(navigation[j].transform)), game + " navigation controls do not overlap.");
            }
            fixture.Click(navigation[1]);
            Require(fixture.HasLabel("玩法说明"), game + " Book opens the rules dialog.");
            if (game == BoardGameId.Xiangqi)
                Require(fixture.HasLabel("行路与棋谱") && fixture.HasLabel("十行") && fixture.HasLabel("2路") && fixture.HasLabel("2路九行"),
                    "Xiangqi explains the same Chinese ranks and numeric files using a coordinate diagram.");
            if (game != BoardGameId.Checkers)
            {
                Require(fixture.HasLabel("如何开始") && fixture.HasLabel("胜负与和棋"), game + " includes structured beginner rules.");
                var scroll = fixture.NamedRect("Board rules scroll").GetComponent<ScrollRect>();
                Require(scroll.vertical && scroll.content.rect.height > scroll.viewport.rect.height,
                    "All beginner sections are reachable by scrolling.");
                fixture.Capture(BoardCatalog.Id(game) + "-rules");
                if (game == BoardGameId.Xiangqi)
                {
                    float y = -fixture.NamedRect("Xiangqi coordinate diagram").anchoredPosition.y;
                    scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01((y - 54) / (scroll.content.rect.height - scroll.viewport.rect.height));
                    fixture.Capture("xiangqi-coordinate-diagram");
                }
            }
            Require(!fixture.HasLabel("新手教程"), game + " tutorial is available only in Settings.");
            fixture.Click("返回");
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            Require(fixture.HasLabel("对局设置"), game + " Settings opens the game settings dialog.");
            fixture.CheckSettingsOutside();
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            Require(fixture.HasLabel("显示提示按钮") && fixture.HasLabel("对局管理"), "All settings live in one scrollable sheet.");
            Require(fixture.HasLabel("显示提示按钮") && fixture.HasLabel("单击落子"), game + " exposes hint and move switches.");
            Require(fixture.HasLabel("显示棋盘坐标") && fixture.HasLabel("显示棋子汉字") == (game == BoardGameId.Chess),
                game + " has its coordinate preference and only chess exposes the piece name switch.");
            fixture.Click("声音");
            Require(fixture.SoundOn && !fixture.HapticsOn && fixture.HasLabel("声音与触感"), "Sound updates the common preference in place.");
            fixture.Click("触感");
            Require(fixture.SoundOn && fixture.HapticsOn && fixture.HasLabel("声音与触感"), "Haptics update independently on the same page.");
            fixture.Click("声音");
            fixture.Click("触感");
            Require(!fixture.SoundOn && !fixture.HapticsOn, "Both switches can turn off inline.");
            if (game == BoardGameId.Army) fixture.Capture("army-settings");
            fixture.Click("新手教程");
            Require(fixture.HasLabel("退出教程"), game + " opens the interactive tutorial from Settings.");
            if (game == BoardGameId.Army)
            {
                fixture.Click("试布阵");
                Require(fixture.HasLabel("布阵练习"), "Army can practice formations in the tutorial.");
                fixture.Click("随机布阵");
                fixture.Click("确认布阵");
                Require(fixture.HasLabel("完成教程") || fixture.HasLabel("下一节"), "Army formation exercise returns to the tutorial.");
            }
            if (game == BoardGameId.Chess)
            {
                fixture.OpenTutorialLesson("兵的升变");
                fixture.Tap(48);
                fixture.Tap(56);
                Require(!fixture.HasLabel("选择升变棋子") && !fixture.HasLabel("选择升变"),
                    "The first promotion tap previews the destination without a second footer action.");
                fixture.Tap(56);
                Require(fixture.HasLabel("选择升变棋子"), "Confirming the preview opens all promotion choices.");
                fixture.Click("后");
                Require(Visible(fixture.Surface(), 56).Kind == 5 && fixture.HasLabel("下一节"), "The selected promotion piece completes the move.");
            }
            fixture.Click("退出教程");
            fixture.Surface();
            Require(fixture.Page.Match == initialMatch && !fixture.HasLabel("退出教程"), game + " tutorial returns to the original board.");
            fixture.Back();
            Require(fixture.Page.gameObject.activeInHierarchy, game + " system Back uses the shared exit confirmation.");
            fixture.Click(fixture.Navigation(UiIconKind.Home));
            Require(!fixture.Page.gameObject.activeInHierarchy, game + " Home leaves the active game page.");
            fixture.Enter(game, route);
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            fixture.Click("新一局");
            Require(fixture.HasLabel("开局设置"), game + " Settings reaches the opening setup.");
        }

        private static void CheckStandardGame(Fixture fixture, BoardGameId game, string route)
        {
            CheckNavigation(fixture, game, route);
            fixture.Click("本地双人");
            fixture.Click("开始对弈");
            var match = fixture.Page.Match;
            Require(match != null && match.Options.Local && match.Position.CurrentSide == 0, game + " starts local play with the first side.");
            var surface = fixture.Surface();
            CheckCoordinateSwitch(fixture, game, game == BoardGameId.Chess ? "a" : "1", "1");
            surface = fixture.Surface();
            if (game == BoardGameId.Xiangqi)
                Require(surface.mainTexture.name == "XiangqiArtwork", "The real Xiangqi board renders its wood and piece artwork atlas.");
            if (game == BoardGameId.Chess)
                Require(surface.GetComponentsInChildren<Text>().Count(text => text.name.StartsWith("Chess piece label ", StringComparison.Ordinal)) == 32,
                    "Every chess piece has a name in the opening position.");
            fixture.CheckBoardLayout(surface, fixture.Navigation(UiIconKind.Settings));
            Require(!fixture.HasLabel("棋谱") && !fixture.HasLabel("确认走棋"), game + " keeps the board free of the record and confirmation buttons.");
            int whiteOrRedKing = game == BoardGameId.Chess ? 4 : 85;
            int blackKing = game == BoardGameId.Chess ? 60 : 4;
            Require(Center(surface, whiteOrRedKing).y < Center(surface, blackKing).y, game + " first side is at the bottom.");
            string first = game == BoardGameId.Chess ? "白方" : "红方";
            Require(fixture.HasLabel(first + " · 玩家 1") && fixture.HasLabel("黑方 · 玩家 2"), game + " both seat labels are present.");
            var board = fixture.Bounds(surface.transform);
            Require(fixture.Bounds(fixture.NamedRect("黑方 · 玩家 2")).yMin >= board.yMax &&
                fixture.Bounds(fixture.NamedRect(first + " · 玩家 1")).yMax <= board.yMin,
                game + " opposing seat labels stay outside the board.");
            fixture.Capture(BoardCatalog.Id(game));
            if (game == BoardGameId.Chess)
            {
                fixture.Click(fixture.Navigation(UiIconKind.Settings));
                fixture.Click("显示棋子汉字");
                fixture.Click("完成");
                Require(!fixture.Surface().GetComponentsInChildren<Text>().Any(text => text.name.StartsWith("Chess piece label ", StringComparison.Ordinal)),
                    "Turning off the Chinese names removes every chess caption.");
                fixture.Capture("chess-without-piece-labels");
            }
            BoardMove move = game == BoardGameId.Chess ? match.Position.LegalMoves().Single(candidate => candidate.From == 12 && candidate.To == 28) :
                match.Position.LegalMoves().First(candidate => candidate.From >= 0);
            fixture.Tap(move.From);
            fixture.Tap(move.From);
            Require(match.MoveCount == 0, game + " tapping the source twice only selects the piece.");
            fixture.Tap(move.To);
            Require(match.MoveCount == 0, game + " board taps preview rather than commit.");
            Require(!fixture.HasLabel("确认走棋"), game + " the selected destination does not add a confirmation button.");
            fixture.CheckNoMatchFooter();
            int otherPiece = match.Position.LegalMoves().First(candidate => candidate.From >= 0 && candidate.From != move.From).From;
            fixture.Tap(otherPiece);
            Require((int)Field(typeof(BoardGamePage), "selected").GetValue(fixture.Page) == otherPiece &&
                (int)Field(typeof(BoardGamePage), "preview").GetValue(fixture.Page) == -1,
                game + " tapping another own piece clears the former preview.");
            fixture.Tap(move.From);
            fixture.Tap(move.To);
            fixture.Tap(move.To);
            Require(match.MoveCount == 1 && match.Position.CurrentSide == 1, game + " tapping the same destination again commits exactly one move.");
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            fixture.Click("暂停");
            fixture.CheckPauseCover();
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            Require(!fixture.HasLabel("对局已暂停") && fixture.HasLabel("对局设置"),
                "A full-page settings scrim replaces the board-only pause shade.");
            fixture.Click("完成");
            fixture.CheckPauseCover();
            fixture.Click("继续对局");
            Require(fixture.Surface().raycastTarget && match.MoveCount == 1, game + " resuming from the cover restores play.");
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            Require(fixture.HasLabel("本局棋谱"), game + " reads move history directly within Settings.");
            fixture.Click("新手教程");
            fixture.Click("退出教程");
            Require(fixture.Page.Match == match && match.MoveCount == 1 && match.LastMove == move,
                game + " exiting the tutorial preserves the in-progress match.");
            fixture.Click(fixture.Navigation(UiIconKind.Home));
            fixture.Enter(game, route);
            fixture.Surface();
            Require(fixture.Page.Match.MoveCount == 1 && fixture.Page.Match.LastMove == move,
                game + " lobby entry returns directly to the saved board.");
            fixture.CheckPauseCover();
            fixture.Capture(BoardCatalog.Id(game) + "-paused");
            fixture.DismissPauseCover();
            Require(!fixture.HasLabel("对局已暂停") && !fixture.Surface().raycastTarget && match.MoveCount == 1,
                "An outside tap uncovers the same paused board without taking a turn.");
            fixture.Click("继续对局");
            if (game == BoardGameId.Chess)
            {
                fixture.Tap(51);
                fixture.Tap(35);
                fixture.Tap(35);
                fixture.Tap(28);
                fixture.Capture("chess-legal-targets");
                fixture.Tap(35);
                Require(fixture.Page.Match.MoveCount == 2, "A capture waits on the destination preview.");
                fixture.Capture("chess-capture-preview");
                fixture.Tap(35);
                Require(fixture.Page.Match.MoveCount == 3 && fixture.Page.Match.Position.PieceAt(35, 0).Side == 0,
                    "Tapping the same capture destination commits the capture.");
            }
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            fixture.Click("单击落子");
            fixture.Capture(BoardCatalog.Id(game) + "-settings");
            fixture.Click("完成");
            match = fixture.Page.Match;
            int before = match.MoveCount;
            move = match.Position.LegalMoves().First(candidate => candidate.From >= 0);
            fixture.Tap(move.From);
            Require(match.MoveCount == before, game + " direct mode still selects the piece first.");
            fixture.Tap(move.To);
            Require(match.MoveCount == before + 1 && match.LastMove == move, game + " direct mode commits on the first legal destination tap.");
            fixture.Click(fixture.Navigation(UiIconKind.Home));
            fixture.Enter(game, route);
            fixture.CheckPauseCover();
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            Require(fixture.Button("单击落子").GetComponent<DialogSwitch>().Value, game + " remembers direct placement when reopening the saved match.");
            fixture.Click("完成");
            fixture.Click(fixture.Navigation(UiIconKind.Home));
        }

        private static void CheckCoordinateSwitch(Fixture fixture, BoardGameId game, string file, string rank)
        {
            bool HasAxes() => fixture.Surface().GetComponentsInChildren<Text>().Any(text => text.name.StartsWith("Board rank ", StringComparison.Ordinal));
            Require(!HasAxes(), game + " hides coordinates by default.");
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            fixture.Click("显示棋盘坐标");
            fixture.Click("完成");
            Require(fixture.InViewport(fixture.Bounds(fixture.NamedRect("Board file " + file))) &&
                fixture.InViewport(fixture.Bounds(fixture.NamedRect("Board rank " + rank))), game + " draws both coordinate axes within the viewport.");
            if (game == BoardGameId.Xiangqi)
                Require(fixture.NamedRect("Board rank 1").GetComponent<Text>().text == "一", "Xiangqi ranks use Chinese numerals.");
            fixture.Capture(BoardCatalog.Id(game) + "-coordinates");
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            fixture.Click("显示棋盘坐标");
            fixture.Click("完成");
            Require(!HasAxes(), game + " can remove both coordinate axes.");
        }

        private static IEnumerator<object> CheckXiangqiMotionAndRecord()
        {
            using (var fixture = new Fixture(new Vector2Int(360, 720)))
            {
                var options = new BoardMatchOptions(true, BoardDifficulty.Normal, 2, 0, true);
                var match = new BoardMatch(BoardGameId.Xiangqi, options);
                var random = new System.Random(7104);
                for (int index = 0; index < 14; index++)
                {
                    var legal = match.Position.LegalMoves();
                    Require(legal.Count > 0 && match.Play(legal[random.Next(legal.Count)]), "Build a legal scrollable Xiangqi record.");
                }
                fixture.WriteMatch(match);
                fixture.Enter(BoardGameId.Xiangqi, "EnterXiangqi");
                fixture.Click("继续对局");
                var live = fixture.Page.Match;
                var move = live.Position.LegalMoves().First();
                Vector2 from = Center(fixture.Surface(), move.From);
                Vector2 to = Center(fixture.Surface(), move.To);
                fixture.Tap(move.From);
                fixture.Tap(move.To);
                // Release the heavy synchronous layout checks before starting a real-time motion.
                yield return null;
                yield return null;
                fixture.Tap(move.To);
                var moving = fixture.Surface();
                var frame = Field(typeof(BoardSurface), "xiangqiMotionProgress");
                Require((BoardMove)Field(typeof(BoardSurface), "xiangqiMotionMove").GetValue(moving) == move &&
                    !moving.raycastTarget && live.MoveCount == 15, "Xiangqi commits once and binds the artwork motion before accepting the next turn.");
                Require(Center(moving, move.From) == from && Center(moving, move.To) == to,
                    "A flip-mode board holds its orientation while the previous player's piece moves.");
                fixture.Capture("xiangqi-motion-source");
                float latest = Time.unscaledTime + 3;
                while (moving != null && (float)frame.GetValue(moving) < .28f && Time.unscaledTime < latest) yield return null;
                Require(moving != null && (float)frame.GetValue(moving) > .20f && (float)frame.GetValue(moving) < .90f,
                    "Unity renders at least one in-flight animation frame. Progress: " + (moving == null ? "destroyed" : frame.GetValue(moving).ToString()));
                fixture.Capture("xiangqi-motion-in-flight");
                while (fixture.Surface() == moving && Time.unscaledTime < latest) yield return null;
                Require(fixture.Surface().raycastTarget && Center(fixture.Surface(), move.To) != to, "After landing, the board flips and accepts the next turn.");
                fixture.Capture("xiangqi-motion-arrival");

                fixture.Click(fixture.Navigation(UiIconKind.Settings));
                var captions = live.DescribeHistory();
                var original = fixture.NamedRect("Board modal backdrop");
                Require(fixture.HasLabel("1.  " + captions[0]) && original.GetComponentsInChildren<ScrollRect>().Length == 1,
                    "The first moves are inline in the same settings viewport.");
                fixture.Click("后一段棋谱");
                Require(original == fixture.NamedRect("Board modal backdrop"), "Browsing more record lines keeps the same settings window.");
                yield return null;
                yield return null;
                string last = captions.Count + ".  " + captions[captions.Count - 1];
                Require(fixture.HasLabel(last) && last.Contains(BoardCatalog.Coordinate(BoardGameId.Xiangqi, move.To)),
                    "Scrolling reaches the last move with the same rank and file notation as the rule diagram.");
                fixture.Capture("xiangqi-record-scrolled");
                fixture.Click("复盘本局");
                fixture.Click("下一手");
                Require((BoardMove)Field(typeof(BoardSurface), "xiangqiMotionMove").GetValue(fixture.Surface()) == live.History.First(),
                    "Xiangqi replay animates a recorded move.");
                fixture.Click(fixture.Navigation(UiIconKind.Back));
                Require(fixture.HasLabel(last), "Returning from replay preserves the record's scroll position.");
            }
        }

        private static void CheckXiangqiRiverLayer()
        {
            using (var fixture = new Fixture(new Vector2Int(360, 720)))
            {
                fixture.Enter(BoardGameId.Xiangqi, "EnterXiangqi");
                var surface = fixture.Surface();
                var position = XiangqiGame.FromFen("4k4/9/9/9/2R6/9/9/9/4P4/4K4 w");
                var crossing = new BoardMove(38, 47);
                Require(position.TryPlay(crossing), "A rook can pass across the river inscription.");
                surface.Bind(BoardGameId.Xiangqi, position, 0, 0, -1, -1, Array.Empty<int>(), Array.Empty<int>(), crossing, false, null);
                surface.BindXiangqiMotion(crossing, Time.unscaledTime - BoardSurface.XiangqiMotionSeconds * .49f);
                fixture.Capture("xiangqi-river-crossing");
                var pieces = surface.GetComponentsInChildren<Graphic>().FirstOrDefault(graphic => graphic.name == "Xiangqi pieces") ?? surface;
                var inscriptions = surface.GetComponentsInChildren<Text>().Where(label => label.text == "楚 河" || label.text == "漢 界").ToArray();
                Require(inscriptions.Length == 2 && inscriptions.All(label => label.depth < pieces.depth),
                    "Xiangqi draws both inscriptions below a crossing piece (pieces " + pieces.depth + ", inscriptions " +
                    string.Join(", ", inscriptions.Select(label => label.depth)) + ").");
            }
        }

        private static void CheckCheckers(Fixture fixture)
        {
            CheckNavigation(fixture, BoardGameId.Checkers, "EnterChineseCheckers");
            fixture.Click("5 台");
            fixture.Click("开始对弈");
            var match = fixture.Page.Match;
            Require(match != null && !match.Options.Local && match.Options.Seats == 6, "The five-computer option builds a six-seat match.");
            var checkers = match.Position as ChineseCheckersGame;
            Require(checkers != null && checkers.Seats == 6 && checkers.PointCount == 121, "The live board is the full six-seat star.");
            CheckCoordinateSwitch(fixture, BoardGameId.Checkers, "25", "17");
            var surface = fixture.Surface();
            fixture.CheckNoMatchFooter();
            Rect board = fixture.Bounds(surface.transform);
            var seats = new List<Rect>();
            for (int i = 1; i <= 6; i++)
            {
                RectTransform badge = fixture.NamedRect("Seat " + i);
                Rect bounds = fixture.Bounds(badge);
                Require(fixture.InViewport(bounds) && bounds.yMin >= board.yMax - .5f, "Seat badge sits above the board.");
                Require(seats.All(other => !other.Overlaps(bounds)), "Six seats have independent indicators.");
                Require(fixture.HasLabel(i == 1 ? "你" : "电脑" + (i - 1)), "The human and each computer are labeled separately.");
                seats.Add(bounds);
            }
            for (int point = 0; point < checkers.PointCount; point++)
                Require(surface.rectTransform.rect.Contains(Center(surface, point)), "Every star hole fits its board.");

            BoardMove chain = BoardMove.None;
            for (int hand = 0; hand < 120 && chain == BoardMove.None; hand++)
            {
                if (checkers.CurrentSide == match.Options.Human)
                    chain = checkers.LegalMoves().Where(candidate => candidate.PathLength >= 3)
                        .OrderBy(candidate => candidate.PathLength).DefaultIfEmpty(BoardMove.None).First();
                if (chain == BoardMove.None)
                    Require(match.Play(checkers.CaptureSearch()(BoardDifficulty.Easy, hand, CancellationToken.None)), "Legal six-seat setup turn.");
            }
            Require(chain != BoardMove.None, "A live six-seat opening can reach a three-hop turn.");
            Method(typeof(BoardGamePage), "Render").Invoke(fixture.Page, null);
            int before = match.MoveCount;
            fixture.Tap(chain.From);
            foreach (int landing in chain.Path) fixture.Tap(landing);
            Require(match.MoveCount == before && fixture.HasLabel("已跳 " + chain.PathLength + " 次"), "Preview keeps the turn and displays the hop count.");
            fixture.Capture("checkers-six-seat-chain");
            fixture.CheckNoMatchFooter();
            int other = Enumerable.Range(0, checkers.PointCount).First(point => point != chain.From && checkers.PieceAt(point, 0).Kind > 0 && checkers.PieceAt(point, 0).Side == 0);
            fixture.Tap(other);
            Require(!fixture.HasLabel("已跳 " + chain.PathLength + " 次") && match.MoveCount == before,
                "Selecting a different marble discards the entire uncommitted chain.");
            fixture.Tap(chain.From);
            foreach (int landing in chain.Path) fixture.Tap(landing);
            fixture.Tap(chain.To);
            Require(match.MoveCount == before + 1 && checkers.CurrentSide == 1 && match.LastMove == chain,
                "Tapping the same landing again commits the entire route and advances to the first computer seat.");
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            Require(fixture.Button("悔棋").interactable, "Checkers keeps whole-turn undo in Settings.");
            fixture.Click("新一局");
            fixture.Click("本地双人");
            fixture.Click("开始对弈");
            var twoPlayer = fixture.Page.Match;
            BoardMove step = twoPlayer.Position.LegalMoves().First(candidate => candidate.From >= 0 && candidate.PathLength == 0);
            fixture.Tap(step.From);
            fixture.Tap(step.To);
            Require(twoPlayer.MoveCount == 0, "An adjacent Checkers step previews the empty hole first.");
            fixture.Tap(step.To);
            Require(twoPlayer.MoveCount == 1 && twoPlayer.LastMove == step, "Tapping the same step destination commits the single step.");
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            var originalSettings = fixture.NamedRect("Board modal backdrop");
            fixture.Click("悔棋");
            Require(originalSettings == fixture.NamedRect("Board modal backdrop") && fixture.HasLabel("同意撤回") &&
                twoPlayer.MoveCount == 1, "Local undo asks the opposing player inline before changing the board.");
            fixture.Click("保留棋局");
            Require(originalSettings == fixture.NamedRect("Board modal backdrop") && twoPlayer.MoveCount == 1,
                "Cancelling local undo leaves the saved turn intact inside the same settings instance.");
            fixture.Click("单击落子");
            fixture.Click("完成");
            BoardMove jump = twoPlayer.Position.LegalMoves().First(candidate => candidate.From >= 0 && candidate.PathLength == 1);
            fixture.Tap(jump.From);
            fixture.Tap(jump.To);
            Require(twoPlayer.MoveCount == 2 && twoPlayer.LastMove == jump, "Checkers direct mode commits a legal single-hop landing immediately.");
            fixture.Click(fixture.Navigation(UiIconKind.Home));
        }

        private static void CheckArmy(Fixture fixture)
        {
            CheckNavigation(fixture, BoardGameId.Army, "EnterArmyChess");
            fixture.Click("本地双人");
            fixture.Click("开始布阵");
            CheckFormation(fixture, 0, "确认并交接");
            fixture.Click("确认并交接");
            fixture.CheckHandoff(1);
            fixture.Reveal(1);

            CheckFormation(fixture, 1, "确认布阵");
            fixture.Click("确认布阵");
            fixture.CheckHandoff(0);
            fixture.Reveal(0);

            var match = fixture.Page.Match;
            Require(match != null && match.Options.Local && match.MoveCount == 0, "Both private formations start one local match.");
            CheckCoordinateSwitch(fixture, BoardGameId.Army, "5", "12");
            var surface = fixture.Surface();
            fixture.CheckNoMatchFooter();
            int greenFront = ArmyGame.FormationPoint(1, 0);
            Require(Visible(surface, greenFront).Kind == -1, "The opponent rank remains hidden on the board.");
            BoardMove move = match.Position.LegalMoves().First(candidate => match.Position.PieceAt(candidate.To, 0).Kind == 0);
            fixture.Tap(move.From);
            fixture.Tap(move.To);
            Require(match.MoveCount == 0 && match.Position.CurrentSide == 0, "Army destination selection does not advance the turn.");
            Require(Visible(fixture.Surface(), greenFront).Kind == -1, "Army preview does not reveal an opponent rank.");
            fixture.CheckNoMatchFooter();
            fixture.Tap(move.To);
            Require(match.MoveCount == 1 && match.Position.CurrentSide == 1, "Tapping the same Army destination advances exactly one turn.");
            fixture.CheckHandoff(1);
            fixture.Capture("army-handoff");
            fixture.Reveal(1);
            Require(Visible(fixture.Surface(), move.To).Kind == -1, "The next player cannot see the just-moved rank.");
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            fixture.Click("暂停");
            fixture.CheckArmyPause();
            fixture.Capture("army-paused");
            fixture.Click("继续对局");
            Require(Visible(fixture.Surface(), greenFront).Kind > 0, "Continuing the pause restores the current player's ranks.");
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            fixture.Click("新手教程");
            fixture.OpenTutorialLesson("同机交接");
            fixture.Click("演示遮挡");
            fixture.CheckHandoff(1);
            fixture.Reveal(1);
            Require(Visible(fixture.Surface(), ArmyGame.FormationPoint(0, 0)).Kind == -1 &&
                Visible(fixture.Surface(), ArmyGame.FormationPoint(1, 0)).Kind > 0, "Tutorial handoff reveals only the next example player's pieces.");
            fixture.Click("完成教程");
            fixture.CheckHandoff(1);
            fixture.Reveal(1);
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            fixture.Click("单击落子");
            fixture.Click("完成");
            BoardMove direct = match.Position.LegalMoves().First(candidate => match.Position.PieceAt(candidate.To, 1).Kind == 0);
            fixture.Tap(direct.From);
            Require(match.MoveCount == 1, "Selecting a rank in direct mode never commits a turn.");
            fixture.Tap(direct.To);
            Require(match.MoveCount == 2 && match.Position.CurrentSide == 0, "Direct target selection commits one legal Army move.");
            fixture.CheckHandoff(0);
            fixture.Page.SetForeground(false);
            fixture.CheckHandoff(0);
            fixture.Page.SetForeground(true);
            fixture.Reveal(0);
            fixture.Page.SetForeground(false);
            fixture.CheckArmyPause();
            fixture.Page.SetForeground(true);
            fixture.CheckArmyPause();
            fixture.Click("继续对局");
            fixture.Click(fixture.Navigation(UiIconKind.Home));
            fixture.Enter(BoardGameId.Army, "EnterArmyChess");
            fixture.CheckArmyPause();
            fixture.Click("继续对局");
            Require(fixture.Page.Match.MoveCount == 2, "Army resumes the saved match with the two legal moves intact.");
            fixture.Click(fixture.Navigation(UiIconKind.Home));
        }

        private static void CheckFormation(Fixture fixture, int side, string action)
        {
            var surface = fixture.Surface();
            fixture.CheckBoardLayout(surface, fixture.Button(action));
            Require(Visible(surface, ArmyGame.FormationPoint(side, 0)).Kind > 0, "Formation renders the active player's own rank.");
            Require(Visible(surface, ArmyGame.FormationPoint(1 - side, 0)).Kind == 0, "Formation omits the other player's pieces.");
        }

        private static void CheckDamagedSave(Fixture fixture, BoardGameId game, string route)
        {
            string path = fixture.SavePath(game);
            const string damaged = "damaged";
            File.WriteAllText(path, damaged);
            fixture.Enter(game, route);
            bool army = game == BoardGameId.Army;
            Require(army || !fixture.Surface().raycastTarget, game + " does not allow unsavable moves over a damaged slot.");
            if (!army)
            {
                fixture.CheckNoMatchFooter();
                fixture.Click(fixture.Navigation(UiIconKind.Settings));
            }
            fixture.Click("新一局");
            fixture.Click(army ? "开始布阵" : "开始对弈");
            Require(fixture.HasLabel("开始新一局？") && File.ReadAllText(path) == damaged, game + " asks before replacing a damaged save.");
            fixture.Click("确认新局");
            if (army)
            {
                Require(File.ReadAllText(path) == damaged, "Army retains the old file until formation is confirmed.");
                fixture.Click("确认布阵");
            }
            Require(fixture.Page.Match != null && File.ReadAllText(path) != damaged, game + " writes only the explicitly approved new game.");
            fixture.Click(fixture.Navigation(UiIconKind.Home));
        }

        private static BoardPiece Visible(BoardSurface surface, int point) =>
            (BoardPiece)Method(typeof(BoardSurface), "VisiblePiece").Invoke(surface, new object[] { point });

        private static Vector2 Center(BoardSurface surface, int point) =>
            (Vector2)Method(typeof(BoardSurface), "Center").Invoke(surface, new object[] { point });

        private static MethodInfo Method(Type type, string name) => type.GetMethod(name, Members)
            ?? throw new InvalidOperationException(type.Name + " is missing method " + name);

        private static FieldInfo Field(Type type, string name) => type.GetField(name, Members)
            ?? throw new InvalidOperationException(type.Name + " is missing field " + name);

        private static void Require(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class Runner : MonoBehaviour { }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject owner;
            private readonly AppController shell;
            private readonly RectTransform canvasRoot;
            private readonly GameObject lobby;
            private readonly Camera camera;
            private readonly EventSystem events;
            private readonly RenderTexture renderTarget;
            private readonly string directory;
            private readonly SaveData preferences = new SaveData { SoundEnabled = false, HapticsEnabled = false };
            internal BoardGamePage Page { get; }
            internal bool SoundOn => preferences.SoundEnabled;
            internal bool HapticsOn => preferences.HapticsEnabled;

            internal Fixture(Vector2Int pixels)
            {
                directory = Path.Combine(Path.GetTempPath(), "xanadu-board-ui-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                owner = new GameObject("Classic board UI fixture");
                var canvasObject = new GameObject("Fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(owner.transform, false);
                canvasRoot = (RectTransform)canvasObject.transform;
                float height = UiFactory.DesignWidth * pixels.y / (float)pixels.x;
                canvasRoot.sizeDelta = new Vector2(UiFactory.DesignWidth, height);
                var canvas = canvasObject.GetComponent<Canvas>();
                camera = new GameObject("Fixture camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(owner.transform, false);
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = height * .5f;
                camera.aspect = pixels.x / (float)pixels.y;
                renderTarget = new RenderTexture(pixels.x, pixels.y, 24);
                renderTarget.Create();
                camera.targetTexture = renderTarget;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;

                events = new GameObject("Fixture EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
                events.transform.SetParent(owner.transform, false);
                var inactiveHost = new GameObject("Inert AppController");
                inactiveHost.transform.SetParent(owner.transform, false);
                inactiveHost.SetActive(false);
                shell = inactiveHost.AddComponent<AppController>();
                SetHost("frame", canvasRoot);
                SetHost("canvasRoot", canvasRoot);
                canvasObject.AddComponent<SafeDesignFrame>();
                lobby = new GameObject("Fixture lobby");
                lobby.transform.SetParent(owner.transform, false);
                var otherGame = new GameObject("Fixture other game");
                otherGame.transform.SetParent(owner.transform, false);
                otherGame.SetActive(false);
                SetHost("lobbyPage", lobby);
                SetHost("gamePage", otherGame);
                SetHost("data", preferences);

                var root = UiFactory.Rect(canvasRoot, "Board game test page", 0, 0, canvasRoot.rect.width, height);
                UiFactory.Stretch(root);
                Page = root.gameObject.AddComponent<BoardGamePage>();
                var setFeedback = (Func<bool, bool, bool>)Method(typeof(AppController), "SetFeedbackSetting")
                    .CreateDelegate(typeof(Func<bool, bool, bool>), shell);
                Page.Initialize(root, directory, () => { Page.Leave(); lobby.SetActive(true); },
                    () => true, () => preferences.SoundEnabled, () => preferences.HapticsEnabled, setFeedback, null);
                SetHost("boardPage", Page);
            }

            private void SetHost(string name, object value) => Field(typeof(AppController), name).SetValue(shell, value);

            internal void CheckModalFocusAndFeedbackRetry()
            {
                Enter(BoardGameId.Chess, "EnterChess");
                var settings = Navigation(UiIconKind.Settings);
                events.SetSelectedGameObject(settings.gameObject);
                ExecuteEvents.Execute(settings.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
                var home = Navigation(UiIconKind.Home);
                Require(!home.IsInteractable(), "Opening board settings disables the rebuilt background navigation.");
                ExecuteEvents.Execute(home.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
                Require(HasLabel("对局设置") && Page.gameObject.activeInHierarchy, "A background Home submit cannot leave the board.");

                var first = Button("单击落子");
                events.SetSelectedGameObject(first.gameObject);
                ExecuteEvents.Execute(first.gameObject, new AxisEventData(events) { moveDir = MoveDirection.Down }, ExecuteEvents.moveHandler);
                var next = events.currentSelectedGameObject;
                Require(next != null && next != first.gameObject && next.transform.IsChildOf(NamedRect("Board modal backdrop")),
                    "Directional navigation moves between visible settings actions inside the modal.");
                Require(HasLabel("暂无其他可继续的对局。"), "A game with no other save shows an inline empty state.");
                var sheet = NamedRect("Board modal backdrop");

                string blockedPath = Path.Combine(directory, "shared-preferences");
                File.WriteAllText(blockedPath, "occupied");
                var store = new LocalSaveStore(blockedPath);
                SetHost("store", store);
                SetHost("game", new Game2048((IRandomSource)Field(typeof(AppController), "random").GetValue(shell)));
                Click("触感");
                Require(HapticsOn && !SoundOn && HasLabel("声音和触感设置暂未保存") && !File.Exists(store.SavePath),
                    "A filesystem failure leaves the chosen haptics value active and exposes a retry.");
                File.Delete(blockedPath);
                Click("重试保存");
                var saved = store.Load(out var warning);
                Require(saved != null && saved.HapticsEnabled && !saved.SoundEnabled && warning == null &&
                    !HasLabel("声音和触感设置暂未保存"), "A single retry writes the same settings without flipping either switch.");
                Require(NamedRect("Board modal backdrop") == sheet, "Saving and retrying retain the original settings window.");
                Back();
                Require(Navigation(UiIconKind.Home).IsInteractable() && events.currentSelectedGameObject == Navigation(UiIconKind.Settings).gameObject,
                    "Closing settings unlocks the board and restores the original settings opener.");
            }

            internal void Enter(BoardGameId game, string route)
            {
                Method(typeof(AppController), route).Invoke(shell, null);
                Flush();
                Require(Page.gameObject.activeInHierarchy && (BoardGameId)Field(typeof(BoardGamePage), "game").GetValue(Page) == game,
                    route + " presents the matching real board-game page.");
                Require(!lobby.activeSelf, route + " hides the lobby.");
            }

            internal bool HasLabel(string text) => Page.GetComponentsInChildren<Text>().Any(label => label.text == text);

            internal void OpenTutorialLesson(string title)
            {
                Click("教程目录");
                var lessons = (BoardLesson[])Field(typeof(BoardGamePage), "lessons").GetValue(Page);
                int index = Array.FindIndex(lessons, lesson => lesson.Title == title);
                Require(index >= 0, "Tutorial chapter is in the directory: " + title);
                var scroll = NamedRect("Tutorial lessons").GetComponent<ScrollRect>();
                float travel = scroll.content.rect.height - scroll.viewport.rect.height;
                if (travel > 0) scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01(index * 72 / travel);
                Click((index + 1) + ". " + title);
            }

            internal string SavePath(BoardGameId game) => Path.Combine(directory, BoardCatalog.Id(game) + "-v1.json");

            internal void WriteMatch(BoardMatch match)
            {
                var profile = new BoardProfile(match.Game) { Options = match.Options };
                profile.Matches.Add(match.Options.Slot, match);
                Require(new BoardSaveStore(directory, match.Game).Save(profile), "The replay fixture writes a valid save.");
            }

            internal void CheckSettingsDirectory(BoardGameId game)
            {
                var profile = new BoardProfile(game);
                foreach (bool local in new[] { true, false })
                {
                    var options = new BoardMatchOptions(local, BoardDifficulty.Normal, 2);
                    var match = game == BoardGameId.Army
                        ? new BoardMatch(game, options, ArmyGame.CreateFormation(201), ArmyGame.CreateFormation(315))
                        : new BoardMatch(game, options);
                    profile.Options = options;
                    profile.Matches.Add(options.Slot, match);
                }
                Require(new BoardSaveStore(directory, game).Save(profile), "The menu fixture has two independently resumable matches.");
                string route = game == BoardGameId.Checkers ? "EnterChineseCheckers" : game == BoardGameId.Army ? "EnterArmyChess" :
                    game == BoardGameId.Xiangqi ? "EnterXiangqi" : "EnterChess";
                Enter(game, route);
                Click(Navigation(UiIconKind.Settings));
                var sheet = NamedRect("Board modal backdrop");
                var layout = sheet.GetComponentInChildren<DialogSheetLayout>();
                var scroll = sheet.GetComponentInChildren<ScrollRect>();
                DialogTypographyChecks.CheckProductionSheet(Button("完成"), "对局设置");
                Require(scroll != null && scroll.enabled && sheet.GetComponentsInChildren<ScrollRect>().Length == 1,
                    "Grouped settings use one scrolling viewport.");
                var sections = new[] { ("落子与显示", "display"), ("声音与触感", "feedback"), ("对局管理", "manage"), ("继续其他对局", "saved"), ("本局棋谱", "record") };
                float previousOffset = -1;
                for (int i = 0; i < sections.Length; i++)
                {
                    var (title, slug) = sections[i];
                    var heading = sheet.GetComponentsInChildren<Text>().Single(item => item.text == title);
                    Require(heading.GetComponentInParent<Button>() == null, "The section heading is readable without opening a second page: " + title);
                    scroll.StopMovement();
                    scroll.verticalNormalizedPosition = 1 - i / (float)(sections.Length - 1);
                    ScrollIntoView(heading.rectTransform);
                    float offset = scroll.content.anchoredPosition.y;
                    Require(offset > previousOffset + .1f && Bounds(scroll.viewport).Contains(Bounds(heading.transform).center),
                        "The real sheet scrolls forward to each visible section: " + title);
                    previousOffset = offset;
                    Capture(BoardCatalog.Id(game) + "-settings-" + slug);
                    DialogScreenshotAudit.CaptureSection(sheet, title);
                    Require(sheet == NamedRect("Board modal backdrop") && layout == sheet.GetComponentInChildren<DialogSheetLayout>(),
                        "All settings positions are rendered inside the same window.");
                }
                Require(!HasLabel("暂无其他可继续的对局。") && sheet.GetComponentsInChildren<Button>().Any(button =>
                    button.GetComponentInChildren<Text>().text.Contains(" · ")), "Resumable saved games are offered directly in settings.");
                scroll.verticalNormalizedPosition = 0;
                var done = Button("完成");
                Require(Bounds(scroll.viewport).Contains(Bounds(done.transform).center), "The last action is reachable at the actual bottom.");
                ScrollIntoView((RectTransform)Button("声音").transform, true);
                float before = scroll.verticalNormalizedPosition;
                Click("声音");
                Require(sheet == NamedRect("Board modal backdrop") && Mathf.Abs(before - scroll.verticalNormalizedPosition) < .025f,
                    "Changing an option preserves both the settings instance and its scroll position.");
                Click("声音");
                CheckSettingsOutside();
                var previous = Page.Match;
                Click(Navigation(UiIconKind.Settings));
                var savedAction = NamedRect("Board modal backdrop").GetComponentsInChildren<Button>()
                    .Single(button => button.GetComponentInChildren<Text>().text.Contains(" · "));
                Click(savedAction);
                Require(!HasLabel("对局设置") && Page.Match != previous && Page.Match.Options.Slot != previous.Options.Slot,
                    "Choosing the other saved game explicitly leaves settings and activates that slot.");
            }

            internal Button Navigation(UiIconKind kind)
            {
                var icon = Page.GetComponentsInChildren<UiIconGraphic>().SingleOrDefault(item => item.Kind == kind);
                Require(icon != null, "Shared navigation icon is present: " + kind);
                return icon.GetComponentInParent<Button>();
            }

            internal Button Button(string caption)
            {
                var button = Page.GetComponentsInChildren<Button>()
                    .SingleOrDefault(item => item.GetComponentsInChildren<Text>().Any(label => label.text == caption));
                Require(button != null, "Visible button: " + caption);
                return button;
            }

            internal BoardSurface Surface()
            {
                var surface = Page.GetComponentsInChildren<BoardSurface>().SingleOrDefault();
                Require(surface != null, "The active view contains one production board surface.");
                return surface;
            }

            internal RectTransform NamedRect(string name)
            {
                var rect = Page.GetComponentsInChildren<RectTransform>().SingleOrDefault(item => item.name == name);
                Require(rect != null, "Visible object: " + name);
                return rect;
            }

            internal void CheckBoardLayout(BoardSurface surface, Button footer)
            {
                Rect board = Bounds(surface.transform);
                Rect controls = Bounds(footer.transform);
                Require(board.width > 0 && board.height > 0 && InViewport(board), "The complete board fits the viewport.");
                Require(InViewport(controls) && !controls.Overlaps(board), "Footer controls fit without covering the board.");
            }

            internal void CheckPauseCover()
            {
                RectTransform cover = NamedRect("Paused chessboard");
                var image = cover.GetComponent<Image>();
                var resume = Button("继续对局");
                Require(image.color.a > 0 && image.color.a < 1 && image.raycastTarget && !Surface().raycastTarget,
                    "The semitransparent pause cover shows the board and blocks play.");
                Require(resume.transform.IsChildOf(cover) && InViewport(Bounds(resume.transform)),
                    "Continue is displayed inside the pause cover.");
            }

            internal void DismissPauseCover()
            {
                var cover = NamedRect("Paused chessboard");
                Dispatch(cover, new Vector2(cover.rect.xMin + 8, cover.rect.yMax - 8));
            }

            internal void CheckArmyPause()
            {
                CheckPauseCover();
                var surface = Surface();
                for (int point = 0; point < 60; point++)
                    Require(Visible(surface, point).Kind <= 0, "The translucent Army pause never exposes either side's ranks.");
            }

            internal void CheckNoMatchFooter()
            {
                Rect board = Bounds(Surface().transform);
                Require(InViewport(board), "The complete board fits the viewport.");
                foreach (var button in Page.GetComponentsInChildren<Button>())
                    Require(Bounds(button.transform).yMax >= board.yMin, "A match does not place a button below the board: " + button.name);
            }

            internal void CheckHandoff(int side)
            {
                Require(!Page.GetComponentsInChildren<BoardSurface>().Any(), "No visible board exists during opaque Army handoff.");
                var cover = NamedRect("Army handoff opaque screen");
                var image = cover.GetComponent<Image>();
                Require(image != null && image.raycastTarget && image.color.a == 1, "Handoff is opaque and intercepts pointer input.");
                Rect bounds = Bounds(cover);
                Rect page = Bounds(Page.transform);
                Require(Mathf.Abs(bounds.xMin - page.xMin) < .5f && Mathf.Abs(bounds.xMax - page.xMax) < .5f &&
                    Mathf.Abs(bounds.yMin - page.yMin) < .5f && Mathf.Abs(bounds.yMax - page.yMax) < .5f,
                    "The opaque cover spans the whole page.");
                Require(HasLabel("交给玩家 " + (side + 1)), "The handoff identifies the correct next player.");
            }

            internal void Reveal(int side) => Click("我是玩家 " + (side + 1) + "，揭开棋盘");

            internal bool InViewport(Rect rect) =>
                rect.xMin >= -.5f && rect.yMin >= -.5f && rect.xMax <= renderTarget.width + .5f && rect.yMax <= renderTarget.height + .5f;

            internal Rect Bounds(Transform transform)
            {
                Flush();
                var corners = new Vector3[4];
                ((RectTransform)transform).GetWorldCorners(corners);
                var a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                var b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }

            internal void Back()
            {
                Page.Back(() => false);
                FinishSheet();
            }

            private void FinishSheet()
            {
                var sheet = canvasRoot.GetComponentInChildren<DialogSheetLayout>();
                if (sheet != null && sheet.IsDismissing) sheet.AdvanceDismissal(1f);
            }

            internal void CheckSettingsOutside()
            {
                TapOutsideSheet();
                Require(!HasLabel("对局设置"), "An outside tap dismisses settings.");
            }

            private void TapOutsideSheet()
            {
                var shade = NamedRect("Board modal backdrop");
                var surface = shade.GetComponentInChildren<DialogSheetLayout>();
                Require(surface != null, "Board settings uses the shared bottom surface.");
                var canvas = Bounds(canvasRoot);
                var panel = Bounds(surface.transform);
                Require(Mathf.Abs(canvas.yMin - panel.yMin) < 1 && Mathf.Abs(canvas.width - panel.width) < 1,
                    "The background of board settings reaches both bottom corners.");
                Dispatch(shade, new Vector2(shade.rect.center.x, shade.rect.yMax - 8));
                Require(surface.IsDismissing, "An outside tap starts the sheet exit.");
                FinishSheet();
            }

            private void ScrollIntoView(RectTransform target, bool align = false)
            {
                var layout = target.GetComponentInParent<DialogSheetLayout>();
                var scroll = target.GetComponentInParent<ScrollRect>();
                if (layout == null || scroll == null || !scroll.enabled) return;
                Flush();
                var visible = Bounds(scroll.viewport);
                var control = Bounds(target);
                if (!align && visible.yMin <= control.yMin && visible.yMax >= control.yMax) return;
                float range = scroll.content.rect.height - scroll.viewport.rect.height;
                if (range <= 0) return;
                float center = scroll.content.InverseTransformPoint(target.TransformPoint(target.rect.center)).y;
                float fromTop = scroll.content.rect.yMax - center;
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01((fromTop - scroll.viewport.rect.height * .45f) / range);
                Flush();
            }
            internal void Click(string caption) => Click(Button(caption));

            internal void Click(Button button)
            {
                Require(button.IsInteractable(), "The requested button is interactable.");
                var rect = (RectTransform)button.transform;
                ScrollIntoView(rect);
                Dispatch(rect, rect.rect.center);
                FinishSheet();
            }

            internal void Tap(int boardPoint)
            {
                var surface = Surface();
                Dispatch(surface.rectTransform, Center(surface, boardPoint));
            }

            private void Dispatch(RectTransform expected, Vector2 local)
            {
                Flush();
                var data = new PointerEventData(events)
                {
                    position = RectTransformUtility.WorldToScreenPoint(camera, expected.TransformPoint(local)),
                    pointerId = -1,
                    button = PointerEventData.InputButton.Left,
                    eligibleForClick = true
                };
                var hits = new List<RaycastResult>();
                events.RaycastAll(data, hits);
                Require(hits.Count > 0 && (hits[0].gameObject.transform == expected || hits[0].gameObject.transform.IsChildOf(expected)),
                    "Top raycast reaches " + expected.name + (hits.Count == 0 ? "." : " (hit " + hits[0].gameObject.name + ")."));
                data.pointerPressRaycast = data.pointerCurrentRaycast = hits[0];
                var target = hits[0].gameObject;
                var pressed = ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerDownHandler);
                var clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
                data.pointerPress = pressed != null ? pressed : clicked;
                if (data.pointerPress != null) ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
                if (clicked != null && data.pointerPress == clicked) ExecuteEvents.Execute(clicked, data, ExecuteEvents.pointerClickHandler);
                Flush();
            }

            private void Flush()
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
                Canvas.ForceUpdateCanvases();
            }

            internal void Capture(string name)
            {
                Flush();
                var previous = RenderTexture.active;
                var snapshot = new Texture2D(renderTarget.width, renderTarget.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = renderTarget;
                    snapshot.ReadPixels(new Rect(0, 0, snapshot.width, snapshot.height), 0, 0);
                    var output = Path.Combine("artifacts", "validation", "classic-board-games");
                    Directory.CreateDirectory(output);
                    File.WriteAllBytes(Path.Combine(output, name + "-" + snapshot.width + "x" + snapshot.height + ".png"), snapshot.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    Object.DestroyImmediate(snapshot);
                }
            }

            public void Dispose()
            {
                Page.Leave();
                camera.targetTexture = null;
                Object.DestroyImmediate(owner);
                renderTarget.Release();
                Object.DestroyImmediate(renderTarget);
                Directory.Delete(directory, true);
            }
        }
    }
}
