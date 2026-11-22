using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.BoardGames
{
    public sealed partial class XiangqiGame
    {
        private static readonly int[] PieceValue = { 0, 0, 120, 120, 405, 910, 430, 88 };

        public BoardSearch CaptureSearch()
        {
            var snapshot = new XiangqiGame(this);
            return (difficulty, seed, cancellation) =>
                BoardNegamax.Find(new SearchPosition(new XiangqiGame(snapshot)), difficulty, seed, cancellation);
        }

        private sealed class SearchPosition : IClassicSearchPosition
        {
            private readonly XiangqiGame position;
            private readonly Stack<int> results = new Stack<int>();
            private int winner;

            internal SearchPosition(XiangqiGame position)
            {
                this.position = position;
                winner = position.winner;
            }

            public int CurrentSide => position.CurrentSide;
            public int Winner => winner;
            public bool InCheck => position.history[position.history.Count - 1].Check;
            public int NoMoveScore => -BoardNegamax.MateValue;
            public ulong SearchKey
            {
                get
                {
                    Turn turn = position.history[position.history.Count - 1];
                    // Repetition and the natural draw depend on the path to the current board.
                    ulong context = ((ulong)position.naturalMoves << 32) |
                        ((ulong)position.redChecks << 16) | (uint)position.blackChecks;
                    return turn.ReversibleKey ^ NextHash(ref context);
                }
            }

            public int Evaluate() => position.Evaluate();
            public IReadOnlyList<BoardMove> SearchMoves(bool tacticalOnly) => winner == BoardResult.Ongoing ?
                position.GenerateMoves(CurrentSide, tacticalOnly) : NoMoves;

            public int Priority(BoardMove move)
            {
                int attacker = Math.Abs(position.board[move.From]);
                int target = Math.Abs(position.board[move.To]);
                int score = target == 0 ? 0 : 1200 + PieceValue[target] * 4 - PieceValue[attacker];
                if (attacker == King) score -= 10;
                return score + Math.Abs(move.From % Columns - 4) - Math.Abs(move.To % Columns - 4);
            }

            public void Push(BoardMove move)
            {
                results.Push(winner);
                position.PushMove(move);
                winner = position.RuleResult(out _);
                if (winner != BoardResult.Ongoing && !position.HasLegalMove(position.CurrentSide)) winner = 1 - position.CurrentSide;
            }

            public void Pop()
            {
                position.PopMove();
                winner = results.Pop();
            }
        }

        private int Evaluate()
        {
            int score = KingShelter(0) - KingShelter(1);
            for (int square = 0; square < Squares; square++)
            {
                sbyte piece = board[square];
                if (piece == 0) continue;
                int kind = Math.Abs(piece);
                int side = SideOf(piece);
                int row = square / Columns;
                int column = square % Columns;
                int advanced = side == 0 ? Rows - 1 - row : row;
                int centrality = 4 - Math.Abs(column - 4);
                int value = PieceValue[kind];
                switch (kind)
                {
                    case Pawn:
                        value += 6 * advanced;
                        if (CrossedRiver(side, row)) value += 36 + 5 * centrality;
                        if (advanced == Rows - 1) value -= 20;
                        break;
                    case Horse:
                        value += 8 * centrality + 3 * Math.Min(advanced, 7) + 5 * HorseActivity(row, column, side);
                        break;
                    case Rook:
                        value += 2 * centrality + 4 * Math.Min(advanced, 5) + 3 * RookSpace(row, column);
                        break;
                    case Cannon:
                        value += 4 * centrality + CannonPressure(row, column, side);
                        break;
                }
                score += side == 0 ? value : -value;
            }
            return (CurrentSide == 0 ? score : -score) + 6;
        }

        private int KingShelter(int side)
        {
            int king = kings[side];
            int row = king / Columns;
            int column = king % Columns;
            int back = side == 0 ? Rows - 1 : 0;
            int sign = side == 0 ? 1 : -1;
            int score = 6 - 6 * Math.Abs(row - back) - 5 * Math.Abs(column - 4);
            for (int direction = 0; direction < 4; direction++)
            {
                int r = row + StraightRows[direction];
                int c = column + StraightColumns[direction];
                if (!OnBoard(r, c) || !InPalace(side, r, c)) continue;
                int kind = board[Index(r, c)] * sign;
                if (kind == Advisor || kind == Elephant) score += 10;
                if (kind == 0) score += 3;
            }
            return score;
        }

        private int HorseActivity(int row, int column, int side)
        {
            int mobility = 0;
            for (int i = 0; i < HorseRows.Length; i++)
            {
                int dr = HorseRows[i];
                int dc = HorseColumns[i];
                if (!CanHorseMove(row, column, dr, dc)) continue;
                sbyte target = board[Index(row + dr, column + dc)];
                if (target == 0 || SideOf(target) != side) mobility++;
            }
            return mobility;
        }

        private int RookSpace(int row, int column)
        {
            int mobility = 0;
            for (int direction = 0; direction < 4; direction++)
            {
                int dr = StraightRows[direction];
                int dc = StraightColumns[direction];
                for (int r = row + dr, c = column + dc; OnBoard(r, c) && board[Index(r, c)] == 0; r += dr, c += dc)
                    mobility++;
            }
            return mobility;
        }

        private int CannonPressure(int row, int column, int side)
        {
            int score = 0;
            for (int direction = 0; direction < 4; direction++)
            {
                int dr = StraightRows[direction];
                int dc = StraightColumns[direction];
                sbyte screen = 0;
                for (int r = row + dr, c = column + dc; OnBoard(r, c); r += dr, c += dc)
                {
                    sbyte target = board[Index(r, c)];
                    if (target == 0) continue;
                    if (screen == 0)
                    {
                        screen = target;
                        continue;
                    }
                    if (SideOf(target) != side)
                    {
                        int kind = Math.Abs(target);
                        score += kind == King ? 28 : kind == Rook ? 13 : 5;
                        if (SideOf(screen) == side) score += 5;
                    }
                    break;
                }
            }
            return score;
        }
    }
}
