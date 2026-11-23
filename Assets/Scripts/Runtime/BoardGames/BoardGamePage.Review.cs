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
        private const float ReplayStepSeconds = .8f;
        private const float ReplayHopSeconds = .32f;
        private BoardReplay replay;
        private PanelKind replayReturnPanel;
        private bool autoReplay, manualReplay;
        private float nextReplayTime;
        private readonly List<int> replayLandings = new List<int>();
        private IReadOnlyList<string> recordLines = Array.Empty<string>();
        private float recordScrollOffset;

        private void RequestHint()
        {
            if (!CanHint) return;
            ClearDraft();
            BeginSearch(true);
            Render();
        }

        private void ReceiveHint(BoardMove move)
        {
            if (!CanHint) return;
            if (move.From >= 0 && LegalMoves().Contains(move) && match.TryUseHint())
            {
                suggestion = move;
                profile.Matches[match.Options.Slot] = match;
                Save();
            }
            else notice = "这一步暂时无法给出走棋建议";
            Render();
        }

        private string SuggestionText()
        {
            if (!CanSelect || suggestion == BoardMove.None) return null;
            var piece = match.Position.PieceAt(suggestion.From, match.Position.CurrentSide);
            string choice = game == BoardGameId.Chess && suggestion.Choice > 0
                ? " · 升变为" + BoardCatalog.PieceName(game, suggestion.Choice, piece.Side)
                : "";
            if (game == BoardGameId.Checkers && suggestion.PathLength > 0) choice = " · " + suggestion.PathLength + " 跳";
            return "建议 " + BoardCatalog.PieceName(game, piece.Kind, piece.Side) + " "
                + BoardCatalog.Coordinate(game, suggestion.From) + " → " + BoardCatalog.Coordinate(game, suggestion.To) + choice;
        }

        private void ToggleHints()
        {
            profile.HintsEnabled = !profile.HintsEnabled;
            if (!profile.HintsEnabled) suggestion = BoardMove.None;
            Save();
            Render();
        }

        private void StartReplay()
        {
            if (!CanReplay) return;
            replay = match.CreateReplay();
            if (replay == null) return;
            CancelSearch();
            xiangqiMotion = BoardMove.None;
            ClearDraft();
            replayReturnPanel = panel;
            panel = PanelKind.None;
            autoReplay = manualReplay = false;
            replayLandings.Clear();
            screen = ScreenKind.Replay;
            Render();
        }

        private void ExitReplay()
        {
            xiangqiMotion = BoardMove.None;
            replay = null;
            autoReplay = manualReplay = false;
            replayLandings.Clear();
            screen = ScreenKind.Match;
            panel = replayReturnPanel;
            Render();
        }

        private void StepReplay(bool forward)
        {
            xiangqiMotion = BoardMove.None;
            autoReplay = manualReplay = false;
            if (forward)
            {
                manualReplay = true;
                AdvanceReplayFrame();
                return;
            }
            if (replayLandings.Count > 0) replayLandings.Clear();
            else replay.Back();
            Render();
        }

        private void RestartReplay()
        {
            xiangqiMotion = BoardMove.None;
            replay.Reset();
            replayLandings.Clear();
            autoReplay = manualReplay = false;
            Render();
        }

        private void ToggleAutoReplay()
        {
            autoReplay = !autoReplay;
            manualReplay = false;
            if (autoReplay)
            {
                if (replay.MoveIndex == replay.TotalMoves)
                {
                    replay.Reset();
                    xiangqiMotion = BoardMove.None;
                }
                nextReplayTime = Time.unscaledTime + (replayLandings.Count > 0 ? ReplayHopSeconds : ReplayStepSeconds);
            }
            Render();
        }

        private void UpdateReplay()
        {
            if (!Allowed || !(autoReplay || manualReplay) || Time.unscaledTime < nextReplayTime) return;
            AdvanceReplayFrame();
        }

        private void AdvanceReplayFrame()
        {
            BoardMove next = replay.NextMove;
            if (game == BoardGameId.Checkers && replayLandings.Count + 1 < next.PathLength)
            {
                // Preview the prefix against the unmodified board. Only the final landing applies the turn and any swap.
                replayLandings.Add(next.PathAt(replayLandings.Count));
                nextReplayTime = Time.unscaledTime + ReplayHopSeconds;
            }
            else
            {
                if (replay.Advance()) StartXiangqiMotion(next);
                replayLandings.Clear();
                manualReplay = false;
                if (replay.MoveIndex == replay.TotalMoves) autoReplay = false;
                nextReplayTime = Time.unscaledTime + ReplayStepSeconds;
            }
            Render();
        }

        private void DrawReplay()
        {
            bool checkers = game == BoardGameId.Checkers;
            bool showingHop = replayLandings.Count > 0;
            int turn = replay.MoveIndex + (showingHop ? 1 : 0);
            int actor = showingHop ? replay.Position.CurrentSide : replay.LastActor;
            string title = turn == 0 ? "初始布局" : checkers ? match.SideName(actor) : "第 " + turn + " 手";
            Label(title, Margin, 112, ContentWidth - 200, 56, 29, UiFactory.Ink, TextAnchor.MiddleLeft);
            Label(turn + " / " + replay.TotalMoves,
                Width - Margin - 200, 112, 200, 56, 23, UiFactory.Muted, TextAnchor.MiddleRight);
            float gap = 8;
            float buttonWidth = (ContentWidth - gap * 3) / 4;
            float step = buttonWidth + gap;
            GameButton(view, "回到开局", Margin, 178, buttonWidth, 72, RestartReplay, false, turn > 0);
            GameButton(view, "上一手", Margin + step, 178, buttonWidth, 72, () => StepReplay(false), false, turn > 0);
            GameButton(view, "下一手", Margin + step * 2, 178, buttonWidth, 72, () => StepReplay(true), false,
                replay.MoveIndex < replay.TotalMoves && !(showingHop && (autoReplay || manualReplay)));
            GameButton(view, autoReplay ? "暂停自动" : "自动播放", Margin + step * 3, 178, buttonWidth, 72, ToggleAutoReplay, autoReplay);
            var move = showingHop ? replay.NextMove : replay.LastMove;
            Rect board = DrawBoard(replay.Position, checkers ? 340 : 268, Height - 169, 0, 0, false, false,
                move, game == BoardGameId.Army, showingHop ? replayLandings : null);
            if (checkers) DrawCheckersSeats(actor, board.y - 83);
            string description = turn == 0 ? "点「下一手」逐手查看，或开启自动播放" : showingHop
                ? "第 " + turn + " 手 · " + replayLandings.Count + " / " + move.PathLength + " 跳"
                : replay.LastDescription;
            Label(description, Margin, board.yMax + 14, ContentWidth, 80, 23, UiFactory.Ink, TextAnchor.UpperLeft);
        }

        private void DrawRecord(Transform sheet, float width, float height)
        {
            DialogTitle(sheet, width, "本局棋谱");
            if (recordLines.Count == 0) DialogText(sheet, width, 108, 80, "还没有已提交的走棋");
            var content = recordLines.Count > 0 ? DrawRecordList(sheet, width, height) : null;
            if (CanReplay) DialogPair(sheet, width, height, "返回设置", ClosePanel, "复盘本局", () =>
            {
                recordScrollOffset = content.anchoredPosition.y;
                StartReplay();
            });
            else GameButton(sheet, "返回设置", 26, height - 102, width - 52, 76, ClosePanel);
        }

        private RectTransform DrawRecordList(Transform sheet, float width, float height)
        {
            const float top = 103;
            const float side = 26;
            const float rowHeight = 72;
            float listHeight = height - top - 118;
            float listWidth = width - 2 * side;
            float rowsHeight = recordLines.Count * rowHeight;
            float maxOffset = Mathf.Max(0, rowsHeight - listHeight);
            float viewportWidth = listWidth - (maxOffset > 0 ? 24 : 0);
            var area = UiFactory.Rect(sheet, "Board record scroll", side, top, listWidth, listHeight);
            var viewport = UiFactory.Panel(area, "Board record viewport", 0, 0, viewportWidth, listHeight, Color.clear, false);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiFactory.Rect(viewport.transform, "Board record content", 0, 0, viewportWidth, Mathf.Max(listHeight, rowsHeight));
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = rowHeight;
            if (maxOffset > 0)
            {
                var scrollbar = DrawRulesScrollbar(area, viewportWidth + 13, listHeight);
                scrollbar.name = "Board record scrollbar";
                scroll.verticalScrollbar = scrollbar;
            }

            var lines = recordLines;
            var rows = new Text[Mathf.Min(lines.Count, Mathf.CeilToInt(listHeight / rowHeight) + 1)];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = UiFactory.Label(content, "", 2, 0, viewportWidth - 4, rowHeight, 23, UiFactory.Ink);
                rows[i].name = "Board record row";
            }

            int firstVisible = -1;
            void BindVisibleRows()
            {
                recordScrollOffset = Mathf.Clamp(content.anchoredPosition.y, 0, maxOffset);
                int first = Mathf.FloorToInt(recordScrollOffset / rowHeight);
                if (first == firstVisible) return;
                firstVisible = first;
                for (int i = 0; i < rows.Length; i++)
                {
                    int index = first + i;
                    var row = rows[i];
                    row.gameObject.SetActive(index < lines.Count);
                    if (index >= lines.Count) continue;
                    row.rectTransform.anchoredPosition = new Vector2(2, -index * rowHeight);
                    row.text = (index + 1) + ".  " + lines[index];
                }
            }

            content.anchoredPosition = new Vector2(0, Mathf.Clamp(recordScrollOffset, 0, maxOffset));
            scroll.onValueChanged.AddListener(_ => BindVisibleRows());
            BindVisibleRows();
            return content;
        }
    }
}
