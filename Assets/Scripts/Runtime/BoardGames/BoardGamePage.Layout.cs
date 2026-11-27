using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    internal sealed partial class BoardGamePage
    {
        private static readonly Color MutedCard = UiFactory.Hex("E7EDE4");
        private static readonly Color PaperLine = UiFactory.Hex("D6DFD2");
        private float Width => layoutSize.x;
        private float Height => layoutSize.y;
        private const float Margin = 24;
        private float ContentWidth => Width - 2 * Margin;
        private PanelKind renderedPanel;
        private readonly Dictionary<PanelKind, string> dialogFocus = new Dictionary<PanelKind, string>();

        private void Render()
        {
            if (!shown) return;
            if (panel == PanelKind.Settings && renderedPanel == panel && settingsBody != null && layoutSize == root.rect.size)
            {
                DrawSettings();
                return;
            }
            layoutSize = root.rect.size;
            if (Width < 40 || Height < 80) return;
            bool changingDialog = panel != PanelKind.None || renderedPanel != PanelKind.None;
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            if (changingDialog && view != null && selected != null && selected.transform.IsChildOf(view))
                dialogFocus[renderedPanel] = selected.name;
            DestroyView();
            view = UiFactory.Rect(root, "Board game content", 0, 0, Width, Height);
            UiFactory.Stretch(view);
            UiFactory.Panel(view, "Board game paper", 0, 0, Width, Height, UiFactory.Paper, false);
            if (handoff) DrawHandoff();
            else
            {
                Navigation();
                switch (screen)
                {
                    case ScreenKind.Setup: DrawSetup(); break;
                    case ScreenKind.Formation: DrawFormation(); break;
                    case ScreenKind.Match: DrawMatch(); break;
                    case ScreenKind.Tutorial: DrawTutorial(); break;
                    case ScreenKind.Replay: DrawReplay(); break;
                }
            }
            renderedPanel = panel;
            if (panel != PanelKind.None) DrawPanel();
            // Views are rebuilt on every change. Recover the corresponding live control after a switch or return.
            if (changingDialog && events != null && !events.alreadySelecting && dialogFocus.TryGetValue(panel, out var name))
            {
                var button = view.GetComponentsInChildren<Button>().FirstOrDefault(item => item.name == name && item.IsInteractable());
                if (button != null) events.SetSelectedGameObject(button.gameObject);
            }
        }

        private void DestroyView()
        {
            settingsBody = null;
            settingsBack = null;
            if (view == null) return;
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
            view = null;
        }

        private void Navigation()
        {
            var nav = UiFactory.Band(view, "Board navigation", 0, 92, 0);
            string title = BoardCatalog.Name(game) + (screen == ScreenKind.Replay ? "复盘" : "");
            UiFactory.Label(nav, title, 226, 34, 268, 58, 36, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (screen == ScreenKind.Setup || screen == ScreenKind.Tutorial || screen == ScreenKind.Replay || trainingFormation)
            {
                UiFactory.NavigationButton(nav, UiIconKind.Back, () => Back(() => true));
                if (screen == ScreenKind.Tutorial)
                    GameButton(nav, "教程目录", 536, 34, 128, 58, () => OpenPanel(PanelKind.Lessons));
                return;
            }
            UiFactory.NavigationButton(nav, UiIconKind.Home, RequestLeave);
            UiFactory.NavigationButton(nav, UiIconKind.Book, () => OpenPanel(PanelKind.Rules));
            UiFactory.NavigationButton(nav, UiIconKind.Settings, () => OpenPanel(PanelKind.Settings));
        }

        private void DrawSetup()
        {
            float y = 116;
            Section("开局设置", ref y);
            GridOptions(new[] { "人机对战", "本地双人" }, draft.Local ? 1 : 0, y, choice =>
            {
                bool local = choice == 1;
                draft = new BoardMatchOptions(local, draft.Difficulty, local || game != BoardGameId.Checkers ? 2 : profile.Options.Seats, 0);
                Render();
            });
            y += 98;
            if (!draft.Local)
            {
                Section("电脑难度", ref y);
                GridOptions(new[] { "▂  简单", "▂▅  普通", "▂▅▇  困难" }, (int)draft.Difficulty, y, choice =>
                { draft = new BoardMatchOptions(false, (BoardDifficulty)choice, draft.Seats, draft.Human); Render(); });
                y += 91;
                Label(DifficultyHint(game, draft.Difficulty), Margin, y, ContentWidth, 55, 21, UiFactory.Muted, TextAnchor.MiddleLeft);
                y += 63;
                if (game == BoardGameId.Checkers)
                {
                    Section("电脑人数", ref y);
                    GridOptions(new[] { "1 台", "2 台", "3 台", "5 台" }, Array.IndexOf(BoardCatalog.CheckersSeats, draft.Seats), y, choice =>
                    { draft = new BoardMatchOptions(false, draft.Difficulty, BoardCatalog.CheckersSeats[choice]); Render(); });
                    y += 93;
                }
                Section(game == BoardGameId.Checkers ? "你的席位" : "你的执棋", ref y);
                string[] labels = game == BoardGameId.Checkers ? Enumerable.Range(1, draft.Seats).Select(i => i.ToString()).ToArray() :
                    game == BoardGameId.Chess ? new[] { "白方", "黑方" } : game == BoardGameId.Army ? new[] { "红方", "绿方" } : new[] { "红方", "黑方" };
                GridOptions(labels, draft.Human, y, choice =>
                { draft = new BoardMatchOptions(false, draft.Difficulty, draft.Seats, choice); Render(); });
            }
            else if (game == BoardGameId.Chess || game == BoardGameId.Xiangqi)
            {
                Section("双人棋盘方向", ref y);
                GridOptions(new[] { "固定方向", "随回合翻转" }, draft.Flip ? 1 : 0, y, choice =>
                { draft = new BoardMatchOptions(true, draft.Difficulty, 2, 0, choice == 1); Render(); });
            }
            else
            {
                Label(game == BoardGameId.Army ? "两位玩家依次秘密布阵，换手时遮住整盘。" : "两位玩家从对角营出发。",
                    Margin, y, ContentWidth, 70, 24, UiFactory.Muted, TextAnchor.MiddleLeft);
            }
            BottomPair("取消", CancelSetup, game == BoardGameId.Army ? "开始布阵" : "开始对弈", RequestNew);
        }

        private void Section(string title, ref float y)
        {
            Label(title, Margin, y, ContentWidth, 46, 25, UiFactory.Ink, TextAnchor.MiddleLeft);
            y += 48;
        }

        private void GridOptions(string[] labels, int active, float y, Action<int> select)
        {
            float gap = 8, width = (ContentWidth - gap * (labels.Length - 1)) / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                int chosen = i;
                GameButton(view, labels[i], Margin + i * (width + gap), y, width, 82, () => select(chosen), i == active);
            }
        }

        private void DrawFormation()
        {
            if (PracticingFormationRestriction)
            {
                var lesson = lessons[lessonIndex];
                Label(lesson.Title, Margin, 114, ContentWidth, 54, 32, UiFactory.Ink, TextAnchor.MiddleLeft);
                Label(lesson.Instruction, Margin, 169, ContentWidth, 46, 21, UiFactory.Muted, TextAnchor.MiddleLeft);
                DrawBoard(formationBoard, 217, Height - 166, forming, forming, true, true);
                Label(notice ?? "先点亮起的棋子，再点金框位置。", Margin, Height - 162, ContentWidth, 60, 22, UiFactory.Ink, TextAnchor.MiddleLeft);
                BottomPair("返回教程", CancelFormation, "确认布阵", ConfirmFormation, true, formationEdited);
                return;
            }
            Label(trainingFormation ? "布阵练习" : "玩家 " + (forming + 1) + " · 布阵", Margin, 114, ContentWidth, 54, 32, UiFactory.Ink, TextAnchor.MiddleLeft);
            Label("选两枚己方棋子交换。另半盘在布阵结束后显示。", Margin, 169, ContentWidth, 46, 19, UiFactory.Muted, TextAnchor.MiddleLeft);
            DrawBoard(formationBoard, 217, Height - 242, forming, forming, true, true);
            Label(notice ?? SaveError ?? "军旗在大本营 · 地雷在后两排 · 炸弹避开首排", Margin, Height - 234, ContentWidth, 55, 20, UiFactory.Ink, TextAnchor.MiddleLeft);
            float column = (ContentWidth - 10) / 2;
            GameButton(view, "推荐阵型", Margin, Height - 179, column, 75, () => SetFormation(false));
            GameButton(view, "随机布阵", Margin + column + 10, Height - 179, column, 75, () => SetFormation(true));
            if (FormationNeedsReplacement) BottomPair("取消布阵", CancelFormation, "新一局", OpenSetup);
            else BottomPair("取消布阵", CancelFormation, formationOptions.Local && forming == 0 ? "确认并交接" : "确认布阵", ConfirmFormation);
        }

        private void DrawHandoff()
        {
            Color cover = UiFactory.Hex("173D35");
            var dark = UiFactory.Panel(view, "Army handoff opaque screen", 0, 0, Width, Height, cover, false);
            dark.raycastTarget = true;
            int side = screen == ScreenKind.Formation ? forming : screen == ScreenKind.Tutorial ? 1 : match.Position.CurrentSide;
            float y = Height * .45f;
            var symbol = UiFactory.Rect(view, "Handoff lock", (Width - 120) * .5f, y - 139, 120, 120).gameObject.AddComponent<UiIconGraphic>();
            symbol.Kind = UiIconKind.Lock;
            symbol.color = UiFactory.Hex("D7E5D7");
            symbol.raycastTarget = false;
            Label("交给玩家 " + (side + 1), Margin, y, ContentWidth, 87, 48, UiFactory.Paper, TextAnchor.MiddleCenter);
            Label("棋盘已遮挡", Margin, y + 94, ContentWidth, 53, 25, UiFactory.Hex("D7E5D7"), TextAnchor.MiddleCenter);
            GameButton(view, "我是玩家 " + (side + 1) + "，揭开棋盘", (Width - 352) * .5f, y + 167, 352, 92, Continue);
        }

        private void DrawMatch()
        {
            var position = match.Position;
            int player = position.CurrentSide;
            int viewer = match.Options.Local ? player : match.Options.Human;
            int orientation = game == BoardGameId.Army || match.Options.Flip ? viewer : match.Options.Local ? 0 : match.Options.Human;
            if (XiangqiAnimating && match.Options.Local && match.Options.Flip) orientation = xiangqiMotionActor;
            bool checkers = game == BoardGameId.Checkers;
            bool checkersAgainstAi = checkers && !match.Options.Local;
            string current = checkersAgainstAi ? (match.Computer ? "电脑 " + (player < match.Options.Human ? player + 1 : player) : "你") : match.SideName(player);
            string turn = match.Terminal ? Outcome() : match.Computer && !paused && !aiError ? current + " 思考中" : "轮到" + current;
            Label(turn, Margin + 20, 128, Width - 302, 40, 30, UiFactory.Ink, TextAnchor.MiddleLeft);
            string round = checkers ? "第 " + (match.MoveCount / match.Options.Seats + 1) + " 轮" : "第 " + (match.MoveCount + 1) + " 手";
            Label(round, Margin + 20, 169, Width - 302, 33, 22, UiFactory.Muted, TextAnchor.MiddleLeft);
            bool showHint = profile.HintsEnabled;
            float controlsX = Width - Margin - 240;
            GameButton(view, Mode(match.Options), controlsX, showHint ? 110 : 139, 220, 49, OpenSetup);
            if (showHint)
            {
                string caption = searchingHint ? "正在分析…" : "提示 " + (BoardMatch.HintLimit - match.HintsUsed);
                GameButton(view, caption, controlsX + 52, 160, 168, 52, RequestHint, CanHint, CanHint);
            }
            UiFactory.Panel(view, "Status divider", Margin, 214, ContentWidth, 1, PaperLine, false);
            float boardTop = checkers ? 314 : HasOpposingSeatLabels ? 278 : 229;
            float boardBottom = Mathf.Max(Height - (HasOpposingSeatLabels ? 306 : 260),
                Mathf.Min(boardTop + ContentWidth * BoardSurface.Ratio(game), Height - (HasOpposingSeatLabels ? 154 : 110)));
            bool canPass = CanSelect && checkers && LegalMoves().Contains(BoardMove.Pass);
            if (ArmyLocal && paused) viewer = -1;
            Rect board = DrawBoard(position, boardTop, boardBottom, viewer, orientation, CanSelect && !canPass, false);
            if (checkers) DrawCheckersSeats(player, board.y - 84);
            if (HasOpposingSeatLabels)
            {
                DrawOpponentSeat(board, board.y - 47, 1 - orientation);
                DrawOpponentSeat(board, board.yMax + 4, orientation);
            }
            if (panel == PanelKind.None && paused && !match.Terminal)
                DrawBoardOverlay(board, "Paused chessboard", "对局已暂停", "继续对局", Continue);
            else if (panel == PanelKind.None && aiError)
                DrawBoardOverlay(board, "Board action overlay", "电脑暂时无法落子", "重试电脑落子", Continue);
            else if (canPass)
                DrawBoardOverlay(board, "Board action overlay", "当前没有可走的棋子", "跳过本手", () => Commit(BoardMove.Pass));
            DrawMatchStatus(board.yMax + (HasOpposingSeatLabels ? 54 : 12), canPass);
        }

        private void DrawBoardOverlay(Rect board, string name, string title, string button, Action action)
        {
            if (boardPromptCollapsed)
            {
                GameButton(view, button, board.center.x - 112, board.yMax - 62, 224, 54, action, true);
                return;
            }
            var dim = UiFactory.Panel(view, name, board.x, board.y, board.width, board.height, DialogUi.Scrim);
            DialogOutsideTap.Bind(dim, () => { boardPromptCollapsed = true; Render(); });
            float width = Mathf.Min(392, board.width - 42);
            var card = UiFactory.Panel(dim.transform, "Pause controls", (board.width - width) / 2, (board.height - 188) / 2, width, 188, UiFactory.Paper);
            card.raycastTarget = true;
            UiFactory.Label(card.transform, title, 16, 17, width - 32, 60, 32, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            GameButton(card.transform, button, 23, 92, width - 46, 73, action, true);
        }

        private void DrawOpponentSeat(Rect board, float y, int side)
        {
            bool xiangqi = game == BoardGameId.Xiangqi;
            Color color = side == 1 ? UiFactory.Ink : xiangqi ? UiFactory.Hex("A53B32") : UiFactory.Hex("E1CF9F");
            UiFactory.CirclePanel(view, "Side marker", board.x + 8, y + 15, 12, color);
            Label(match.SideName(side), board.x + 30, y, board.width - 40, 42, 22, UiFactory.Ink, TextAnchor.MiddleLeft);
            if (!xiangqi) return;
            int remaining = 0;
            for (int point = 0; point < match.Position.PointCount; point++)
            {
                var piece = match.Position.PieceAt(point, side);
                if (piece.Kind > 0 && piece.Side == 1 - side) remaining++;
            }
            Label("吃子 " + (16 - remaining), board.x + board.width - 120, y, 106, 42, 20, UiFactory.Muted, TextAnchor.MiddleRight);
        }

        private void DrawCheckersSeats(int player, float y)
        {
            int count = match.Options.Seats;
            float span = ContentWidth / count;
            for (int i = 0; i < count; i++)
            {
                Color tint = BoardSurface.SeatColor(i);
                if (i == player) UiFactory.Panel(view, "Current seat", Margin + i * span + 3, y, span - 6, 72, MutedCard);
                var disc = UiFactory.CirclePanel(view, "Seat " + (i + 1), Margin + i * span + (span - 36) * .5f, y + 4, 36, tint);
                UiFactory.Label(disc.transform, BoardSurface.SeatSymbol(i), 0, 0, 36, 36, 22, Color.white, TextAnchor.MiddleCenter);
                string name = match.Options.Local ? "玩家" + (i + 1) : i == match.Options.Human ? "你" :
                    "电脑" + (i < match.Options.Human ? i + 1 : i);
                Label(name, Margin + i * span, y + 40, span, 29, 20, UiFactory.Ink, TextAnchor.MiddleCenter);
            }
        }

        private void DrawMatchStatus(float hintTop, bool canPass)
        {
            string status = SaveError;
            if (status == null && !paused && !aiError && !canPass)
            {
                status = notice ?? (CanSelect ? MovePreviewHint() ?? SuggestionText() : null) ?? match.Position.Status;
                if (CanSelect && (string.IsNullOrEmpty(status) || status == "行棋"))
                    status = selected < 0 ? "点己方棋子查看合法落点" : DirectMoves ? "点高亮落点走棋" : "点高亮落点预览";
            }
            if (route.Count > 0)
                Label(routeIsStep ? "单步预览" : "已跳 " + route.Count + " 次", Width - Margin - 140, hintTop, 140, 54, 22,
                    UiFactory.Muted, TextAnchor.MiddleRight);
            if (!string.IsNullOrEmpty(status))
                Label(status, Margin, hintTop, ContentWidth - (route.Count > 0 ? 152 : 0), 54, 22, UiFactory.Ink, TextAnchor.MiddleLeft);
        }

        private string MovePreviewHint()
        {
            if (route.Count > 0)
            {
                if (Prepared() == BoardMove.None) return "此处只能经过，请继续跳";
                return Targets().Any() ? "可继续跳，再点当前落点结束" : "再点当前落点确认";
            }
            if (preview < 0) return null;
            return promotions.Length > 0 ? "再点落点选择升变" : "再点落点确认走棋";
        }

        private Rect DrawBoard(IBoardPosition position, float top, float bottom, int viewer, int orientation, bool input, bool formation,
            BoardMove? replayMove = null, bool revealArmy = false, IReadOnlyList<int> replayLandings = null)
        {
            float ratio = BoardSurface.Ratio(game);
            float width = Mathf.Min(ContentWidth, Mathf.Max(40, (bottom - top) / ratio));
            float height = width * ratio;
            float y = top + Mathf.Max(0, (bottom - top - height) / 2);
            var board = new Rect((Width - width) / 2, y, width, height);
            bool squareGrid = game == BoardGameId.Chess;
            float inset = squareGrid ? 14 : 0;
            var frame = UiFactory.Panel(view, "Rounded board frame", board.x, board.y, board.width, board.height, UiFactory.Hex("293F33"));
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = squareGrid;
            var surface = UiFactory.Rect(frame.transform, "Board surface", inset, inset, board.width - 2 * inset, board.height - 2 * inset)
                .gameObject.AddComponent<BoardSurface>();
            IEnumerable<int> targets = Targets();
            BoardMove? highlight = InMatch && input && suggestion != BoardMove.None ? (BoardMove?)suggestion : null;
            if (PracticingFormationRestriction && !formationEdited)
            {
                var lesson = lessons[lessonIndex];
                if (selected < 0) targets = new[] { lesson.From };
                else highlight = new BoardMove(lesson.From, lesson.To);
            }
            else if (screen == ScreenKind.Tutorial && !lessonDone && lessons[lessonIndex].UsesBoard)
            {
                var lesson = lessons[lessonIndex];
                if (selected < 0) targets = new[] { lesson.From };
                else if (lesson.Kind == BoardLessonKind.Blocked)
                {
                    targets = Array.Empty<int>();
                    highlight = new BoardMove(lesson.From, lesson.To);
                }
                else
                {
                    int[] path = lesson.Route;
                    int target = route.Count < path.Length ? path[route.Count] : lesson.To;
                    targets = targets.Where(point => point == target);
                }
            }
            int destination = replayLandings != null ? replayLandings[replayLandings.Count - 1] : replayMove?.To ?? preview;
            BoardMove lastMove = replayLandings != null ? BoardMove.None : replayMove ?? (InMatch ? match.LastMove : BoardMove.None);
            surface.Bind(game, position, viewer, orientation, replayMove?.From ?? selected, destination, replayLandings ?? route, targets,
                lastMove, input, Point, formation, revealArmy, highlight, profile.ShowCoordinates, profile.ShowChessNames);
            if (XiangqiAnimating) surface.BindXiangqiMotion(xiangqiMotion, xiangqiMotionStartedAt);
            return board;
        }

        private void BottomPair(string left, Action leftAction, string right, Action rightAction, bool leftEnabled = true, bool rightEnabled = true)
        {
            float leftWidth = ContentWidth * .37f;
            float y = Height - 93;
            const float height = 81;
            GameButton(view, left, Margin, y, leftWidth, height, leftAction, false, leftEnabled);
            GameButton(view, right, Margin + leftWidth + 9, y, ContentWidth - leftWidth - 9, height, rightAction, true, rightEnabled);
        }

        private static Button GameButton(Transform parent, string text, float x, float y, float width, float height, Action action,
            bool primary = false, bool enabled = true)
        {
            var button = UiFactory.Button(parent, text, x, y, width, height, action, primary);
            button.interactable = enabled;
            button.GetComponentInChildren<Text>().fontSize = Mathf.Min(25, Mathf.FloorToInt(width / Mathf.Max(3, text.Length) * 1.1f));
            return button;
        }

        private void Label(string text, float x, float y, float width, float height, int size, Color color, TextAnchor alignment) =>
            UiFactory.Label(view, text, x, y, width, height, size, color, alignment);

        private string Mode(BoardMatchOptions options) => options.Local ? "本地双人" :
            (game == BoardGameId.Checkers ? "1 人 / " + (options.Seats - 1) + " 电脑" : "人机") + " · " + BoardCatalog.Difficulty(options.Difficulty);

        private string Outcome() => match.Position.Winner == BoardResult.Draw ? "和棋" : match.SideName(match.Position.Winner) + "获胜";

        private static string DifficultyHint(BoardGameId game, BoardDifficulty difficulty)
        {
            switch (game)
            {
                case BoardGameId.Army: return difficulty == BoardDifficulty.Easy ? "看营区与走法，按公开信息评估风险" :
                    difficulty == BoardDifficulty.Normal ? "根据公开战报推断暗子身份" : "比较符合公开信息的多种可能布阵";
                case BoardGameId.Checkers: return difficulty == BoardDifficulty.Easy ? "偏好单子直接向前，兼顾短跳" :
                    difficulty == BoardDifficulty.Normal ? "考虑全队推进、后排与跳板" : "预判其他席位走棋后的跳板和领先势头";
                default: return difficulty == BoardDifficulty.Easy ? "浅搜几着合理走法" :
                    difficulty == BoardDifficulty.Normal ? "兼顾子力、局面和短战术" : "加深搜索，追踪强制战术与残局";
            }
        }
    }
}
