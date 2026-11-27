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
        private int recordStart;

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
            SaveSettings();
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

        private void DrawInlineRecord(DialogSheetSections rows, RectTransform sheet)
        {
            const int batch = 8;
            rows.Heading("本局棋谱");
            if (recordLines.Count == 0)
            {
                rows.Body("还没有已提交的走棋", 19);
                return;
            }
            int end = Mathf.Min(recordStart + batch, recordLines.Count);
            for (int index = recordStart; index < end; index++)
                rows.Body((index + 1) + ".  " + recordLines[index], 20);
            if (recordLines.Count <= batch) return;
            rows.Body("第 " + (recordStart + 1) + "–" + end + " 手，共 " + recordLines.Count + " 手", 19);
            float y = rows.Reserve(66), half = (rows.Width - 8) * .5f;
            DialogUi.ActionButton(sheet, "前一段棋谱", rows.Inset, y, half, 66, () =>
            {
                recordStart = Mathf.Max(0, recordStart - batch);
                DrawSettings();
            }).interactable = recordStart > 0;
            DialogUi.ActionButton(sheet, "后一段棋谱", rows.Inset + half + 8, y, half, 66, () =>
            {
                recordStart += batch;
                DrawSettings();
            }).interactable = end < recordLines.Count;
        }
    }
}
