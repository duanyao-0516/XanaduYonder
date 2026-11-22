using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
        private const float Margin = 26;
        private float ContentWidth => Width - 2 * Margin;

        private void Render()
        {
            if (!shown) return;
            layoutSize = root.rect.size;
            if (Width < 40 || Height < 80) return;
            DestroyView();
            view = UiFactory.Rect(root, "Board game content", 0, 0, Width, Height);
            UiFactory.Stretch(view);
            UiFactory.Panel(view, "Board game paper", 0, 0, Width, Height, UiFactory.Paper, false);
            if (handoff)
            {
                DrawHandoff();
                if (panel != PanelKind.None) DrawPanel();
                return;
            }
            Navigation();
            switch (screen)
            {
                case ScreenKind.Menu: DrawMenu(); break;
                case ScreenKind.Setup: DrawSetup(); break;
                case ScreenKind.Formation: DrawFormation(); break;
                case ScreenKind.Match: DrawMatch(); break;
                case ScreenKind.Tutorial: DrawTutorial(); break;
            }
            if (panel != PanelKind.None) DrawPanel();
        }

        private void DestroyView()
        {
            if (view == null) return;
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
            view = null;
        }

        private void Navigation()
        {
            UiFactory.IconButton(view, UiIconKind.Home, Margin, 14, 88, 88, RequestLeave);
            UiFactory.Label(view, BoardCatalog.Name(game), 120, 14, Width - 335, 88, 39, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.IconButton(view, UiIconKind.Book, Width - Margin - 178, 14, 88, 88, () => OpenPanel(PanelKind.Rules));
            UiFactory.IconButton(view, UiIconKind.Settings, Width - Margin - 88, 14, 88, 88, () => OpenPanel(PanelKind.Settings));
        }

        private void DrawMenu()
        {
            float y = 137;
            var card = UiFactory.Panel(view, "New board game", Margin, y, ContentWidth, 216, MutedCard);
            var art = UiFactory.Rect(card.transform, "Game logo", ContentWidth - 165, 36, 132, 132).gameObject.AddComponent<Image>();
            art.sprite = Resources.Load<Sprite>("Lobby/Icons/" + BoardCatalog.Id(game) + "-logo");
            art.raycastTarget = false;
            UiFactory.Label(card.transform, BoardCatalog.Name(game), 29, 18, ContentWidth - 215, 65, 45, UiFactory.Ink, style: FontStyle.Bold);
            GameButton(card.transform, "开始新一局", 29, 104, ContentWidth - 220, 88, OpenSetup, true);
            y += 237;
            GameButton(view, profile.TutorialSeen ? "重玩新手教程" : "新手教程", Margin, y, ContentWidth, 87, StartTutorial);
            y += 112;
            if (profile.Matches.Count > 0)
            {
                Label("继续对局", Margin, y, ContentWidth, 47, 28, UiFactory.Ink, TextAnchor.MiddleLeft);
                y += 54;
                float available = Mathf.Max(53, Mathf.Min(88, (Height - y - 82) / profile.Matches.Count - 7));
                foreach (var saved in profile.Matches.OrderBy(pair => pair.Key).Select(pair => pair.Value))
                {
                    var captured = saved;
                    string mode = Mode(saved.Options);
                    GameButton(view, mode + (saved.Terminal ? " · 查看结局" : " · 第 " + (saved.MoveCount + 1) + " 手"),
                        Margin, y, ContentWidth, available, () => ResumeSlot(captured));
                    y += available + 7;
                }
            }
            DrawSaveWarning(Height - 69);
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
            BottomPair("取消", () => { screen = setupOrigin; Render(); }, game == BoardGameId.Army ? "开始布阵" : "开始对弈", RequestNew);
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
            Label(trainingFormation ? "布阵练习" : "玩家 " + (forming + 1) + " · 布阵", Margin, 114, ContentWidth, 54, 32, UiFactory.Ink, TextAnchor.MiddleLeft);
            Label("选两枚己方棋子交换。另半盘在布阵结束后显示。", Margin, 169, ContentWidth, 46, 19, UiFactory.Muted, TextAnchor.MiddleLeft);
            DrawBoard(formationBoard, 217, Height - 242, forming, forming, true, true);
            Label(notice ?? "军旗在大本营 · 地雷在后两排 · 炸弹避开首排", Margin, Height - 234, ContentWidth, 55, 20, UiFactory.Ink, TextAnchor.MiddleLeft);
            float column = (ContentWidth - 10) / 2;
            GameButton(view, "推荐阵型", Margin, Height - 179, column, 75, () => SetFormation(false));
            GameButton(view, "随机布阵", Margin + column + 10, Height - 179, column, 75, () => SetFormation(true));
            BottomPair("取消布阵", CancelFormation, draft.Local && forming == 0 ? "确认并交接" : "确认布阵", ConfirmFormation);
        }

        private void DrawHandoff()
        {
            Color cover = UiFactory.Hex("173D35");
            var dark = UiFactory.Panel(view, "Army handoff opaque screen", 0, 0, Width, Height, cover, false);
            dark.raycastTarget = true;
            int side = screen == ScreenKind.Formation ? forming : screen == ScreenKind.Tutorial ? 1 : match.Position.CurrentSide;
            float y = Height * .36f;
            Label("交给玩家 " + (side + 1), Margin, y, ContentWidth, 87, 48, UiFactory.Paper, TextAnchor.MiddleCenter);
            Label("棋盘已遮挡", Margin, y + 94, ContentWidth, 53, 25, UiFactory.Hex("D7E5D7"), TextAnchor.MiddleCenter);
            GameButton(view, "我是玩家 " + (side + 1) + "，揭开棋盘", Margin + 62, y + 175, ContentWidth - 124, 94, Continue);
        }

        private void DrawMatch()
        {
            var position = match.Position;
            int player = position.CurrentSide;
            int viewer = match.Options.Local ? player : match.Options.Human;
            int orientation = game == BoardGameId.Army || match.Options.Flip ? viewer : match.Options.Local ? 0 : match.Options.Human;
            string turn = match.Terminal ? Outcome() : paused ? "对局已暂停" :
                aiError ? "电脑暂时无法落子" : match.Computer ? match.SideName(player) + " 思考中" : "轮到" + match.SideName(player);
            Label(turn, Margin, 110, Width * .60f, 54, 29, UiFactory.Ink, TextAnchor.MiddleLeft);
            GameButton(view, Mode(match.Options), Width - Margin - 270, 113, 270, 51, OpenSetup);
            var rule = UiFactory.Panel(view, "Status divider", Margin, 170, ContentWidth, 1, PaperLine, false);
            rule.raycastTarget = false;
            float boardTop = 185, boardBottom = Height - 242;
            if (game == BoardGameId.Checkers)
            {
                DrawCheckersSeats(player, 177);
                boardTop += 70;
            }
            if (game == BoardGameId.Chess || game == BoardGameId.Xiangqi)
            {
                Label(match.SideName(1 - orientation), Margin + 8, 177, ContentWidth - 16, 40, 20, UiFactory.Muted, TextAnchor.MiddleLeft);
                boardTop += 39;
                boardBottom -= 35;
                Label(match.SideName(orientation), Margin + 8, boardBottom, ContentWidth - 16, 35, 20, UiFactory.Muted, TextAnchor.MiddleLeft);
            }
            DrawBoard(position, boardTop, boardBottom, viewer, orientation, CanSelect, false);
            if (paused && !match.Terminal)
            {
                var dim = UiFactory.Panel(view, "Paused chessboard", Margin, boardTop, ContentWidth, boardBottom - boardTop, MutedCard);
                dim.raycastTarget = true;
                UiFactory.Label(dim.transform, "对局已暂停", 0, 0, ContentWidth, boardBottom - boardTop, 42, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            DrawMatchActions();
        }

        private void DrawCheckersSeats(int player, float y)
        {
            int count = match.Options.Seats;
            float span = ContentWidth / count;
            for (int i = 0; i < count; i++)
            {
                Color tint = BoardSurface.SeatColor(i);
                if (i == player) UiFactory.Panel(view, "Current seat", Margin + i * span + 3, y, span - 6, 66, MutedCard);
                var disc = UiFactory.CirclePanel(view, "Seat " + (i + 1), Margin + i * span + (span - 30) * .5f, y + 2, 30, tint);
                UiFactory.Label(disc.transform, BoardSurface.SeatSymbol(i), 0, 0, 30, 30, 20, Color.white, TextAnchor.MiddleCenter);
                string name = match.Options.Local ? "玩家" + (i + 1) : i == match.Options.Human ? "你" :
                    "电脑" + (i < match.Options.Human ? i + 1 : i);
                Label(name, Margin + i * span, y + 34, span, 29, 18, UiFactory.Ink, TextAnchor.MiddleCenter);
            }
        }

        private void DrawMatchActions()
        {
            bool ready = CanSelect;
            string status = notice ?? (aiError ? "可重试电脑落子" : match.Position.Status);
            if (string.IsNullOrEmpty(status) || status == "行棋")
                status = selected < 0 ? "点己方棋子查看合法落点" : route.Count > 0 ? "已预览 " + route.Count + (routeIsStep ? " 步" : " 跳") : "点高亮落点";
            Label(SaveError ?? status, Margin, Height - 234, ContentWidth, 52, 21, UiFactory.Ink, TextAnchor.MiddleLeft);
            float width = (ContentWidth - 16) / 3;
            GameButton(view, saveFailed ? "重试保存" : "棋谱", Margin, Height - 179, width, 74,
                saveFailed ? (Action)(() => { Save(); Render(); }) : () => OpenPanel(PanelKind.Record));
            GameButton(view, paused ? "已暂停" : "暂停", Margin + width + 8, Height - 179, width, 74, Pause, false, !paused && !match.Terminal);
            GameButton(view, "新一局", Margin + 2 * (width + 8), Height - 179, width, 74, OpenSetup);

            BoardMove chosen = ready ? Prepared() : BoardMove.None;
            bool claim = ready && route.Count == 0 && LegalMoves().Contains(BoardMove.ClaimDraw);
            bool pass = ready && LegalMoves().Contains(BoardMove.Pass);
            string secondary = route.Count != 0 ? (routeIsStep ? "取消预览" : "退一跳") : claim ? "申请和棋" :
                selected >= 0 ? "取消选择" : game == BoardGameId.Army ? "返回选局" : "悔棋";
            Action second = route.Count != 0 ? (Action)UndoRoute : claim ? () => Commit(BoardMove.ClaimDraw) :
                selected >= 0 ? () => { ClearDraft(); Render(); } : game == BoardGameId.Army ? (Action)ReturnToMenu : RequestUndo;
            bool secondEnabled = route.Count != 0 || claim || selected >= 0 || game == BoardGameId.Army || match.CanUndo;
            string primary;
            Action first;
            bool firstEnabled = true;
            if (match.Terminal) { primary = "查看结算"; first = () => OpenPanel(PanelKind.Result); }
            else if (paused || aiError) { primary = aiError ? "重试电脑落子" : "继续对局"; first = Continue; }
            else if (pass) { primary = "跳过本手"; first = () => Commit(BoardMove.Pass); }
            else
            {
                primary = match.Computer ? "电脑行棋中" : game == BoardGameId.Checkers && !routeIsStep ? "结束连跳" :
                    ArmyLocal && profile.ConfirmArmyMove ? "确认并交接" : "确认走棋";
                first = ConfirmMove;
                firstEnabled = chosen != BoardMove.None;
                if (game == BoardGameId.Army && !profile.ConfirmArmyMove) primary = "点合法落点直接走棋";
            }
            BottomPair(secondary, second, primary, first, secondEnabled, firstEnabled);
        }

        private void DrawTutorial()
        {
            var lesson = lessons[lessonIndex];
            Label(lesson.Title, Margin, 108, ContentWidth, 56, 34, UiFactory.Ink, TextAnchor.MiddleLeft);
            Label((lessonIndex + 1) + " / " + lessons.Length + " · " + lesson.Instruction, Margin, 163, ContentWidth, 85, 23, UiFactory.Muted, TextAnchor.UpperLeft);
            DrawBoard(lessonPosition, 253, Height - 232, lessonPosition.CurrentSide, 0, CanSelect, false);
            Label(notice ?? (lessonDone ? "已完成本节" : ""), Margin, Height - 227, ContentWidth, 54, 22, UiFactory.Ink, TextAnchor.MiddleLeft);
            float half = (ContentWidth - 10) / 2;
            GameButton(view, "退出教程", Margin, Height - 173, half, 73, ReturnToMenu);
            GameButton(view, "跳过本节", Margin + half + 10, Height - 173, half, 73, NextLesson);
            bool explainHandoff = game == BoardGameId.Army && lessonIndex + 1 == lessons.Length && lesson.From < 0 && !lessonDone;
            if (game == BoardGameId.Army && lessonIndex == 0 && !lessonDone)
            {
                BottomPair("上一步", PriorLesson, "试布阵", PracticeFormation, false);
                return;
            }
            if (explainHandoff)
            {
                BottomPair("上一步", PriorLesson, "演示遮挡", () => { lessonDone = true; handoff = true; Render(); }, lessonIndex > 0);
                return;
            }
            if (lessonDone || lesson.From < 0)
            {
                BottomPair("上一步", PriorLesson, lessonIndex + 1 == lessons.Length ? "完成教程" : "下一节", NextLesson, lessonIndex > 0);
                return;
            }
            BottomPair(game == BoardGameId.Checkers && route.Count != 0 ? "退一跳" : "重新练习",
                game == BoardGameId.Checkers && route.Count != 0 ? (Action)UndoRoute : StartLesson,
                game == BoardGameId.Checkers ? "结束本手" : "确认走棋", ConfirmMove, true, Prepared() != BoardMove.None);
        }

        private void PriorLesson() { if (lessonIndex > 0) { lessonIndex--; StartLesson(); } }

        private void DrawBoard(IBoardPosition position, float top, float bottom, int viewer, int orientation, bool input, bool formation)
        {
            float ratio = BoardSurface.Ratio(game);
            float width = Mathf.Min(ContentWidth, Mathf.Max(40, (bottom - top) / ratio));
            float height = width * ratio;
            float y = top + Mathf.Max(0, (bottom - top - height) / 2);
            var surface = UiFactory.Rect(view, "Board surface", (Width - width) / 2, y, width, height).gameObject.AddComponent<BoardSurface>();
            IEnumerable<int> targets = Targets();
            if (screen == ScreenKind.Tutorial && selected == -1 && lessons[lessonIndex].From >= 0 && !lessonDone)
                targets = new[] { lessons[lessonIndex].From };
            surface.Bind(game, position, viewer, orientation, selected, preview, route, targets,
                InMatch ? match.LastMove : BoardMove.None, input, Point, formation);
        }

        private void BottomPair(string left, Action leftAction, string right, Action rightAction, bool leftEnabled = true, bool rightEnabled = true)
        {
            float leftWidth = ContentWidth * .37f;
            GameButton(view, left, Margin, Height - 93, leftWidth, 81, leftAction, false, leftEnabled);
            GameButton(view, right, Margin + leftWidth + 9, Height - 93, ContentWidth - leftWidth - 9, 81, rightAction, true, rightEnabled);
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

        private void DrawSaveWarning(float y)
        {
            if (SaveError != null) Label(SaveError, Margin, y, ContentWidth, 55, 21, UiFactory.Muted, TextAnchor.MiddleLeft);
        }

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
