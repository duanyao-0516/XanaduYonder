using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.BoardGames
{
    public sealed partial class XiangqiGame
    {
        private static readonly int[] StraightRows = { -1, 1, 0, 0 };
        private static readonly int[] StraightColumns = { 0, 0, -1, 1 };
        private static readonly int[] HorseRows = { -2, -2, 2, 2, -1, -1, 1, 1 };
        private static readonly int[] HorseColumns = { -1, 1, -1, 1, -2, 2, -2, 2 };

        private static int Index(int row, int column) => row * Columns + column;
        private static int SideOf(sbyte piece) => piece > 0 ? 0 : 1;
        private static bool OnBoard(int row, int column) => (uint)row < Rows && (uint)column < Columns;
        private static bool InPalace(int side, int row, int column) =>
            column >= 3 && column <= 5 && (side == 0 ? row >= 7 : row <= 2);
        private static bool CrossedRiver(int side, int row) => side == 0 ? row < 5 : row >= 5;
        private static int Forward(int side) => side == 0 ? -1 : 1;

        private List<BoardMove> GenerateMoves(int side, bool tacticalOnly)
        {
            var result = new List<BoardMove>(tacticalOnly ? 16 : 48);
            FindMoves(side, tacticalOnly, result);
            return result;
        }

        private bool HasLegalMove(int side) => FindMoves(side, false, null);

        // A null list stops at the first legal move without allocating a move list.
        private bool FindMoves(int side, bool tacticalOnly, List<BoardMove> result)
        {
            for (int from = 0; from < Squares; from++)
            {
                sbyte piece = board[from];
                if (piece == 0 || SideOf(piece) != side) continue;
                int row = from / Columns;
                int column = from % Columns;
                int kind = Math.Abs(piece);
                switch (kind)
                {
                    case King:
                        for (int direction = 0; direction < 4; direction++)
                        {
                            int toRow = row + StraightRows[direction];
                            int toColumn = column + StraightColumns[direction];
                            if (InPalace(side, toRow, toColumn) && AddMove(from, toRow, toColumn, side, tacticalOnly, result))
                                return true;
                        }
                        break;
                    case Advisor:
                    case Elephant:
                        int distance = kind == Advisor ? 1 : 2;
                        for (int dr = -1; dr <= 1; dr += 2)
                        for (int dc = -1; dc <= 1; dc += 2)
                        {
                            int toRow = row + dr * distance;
                            int toColumn = column + dc * distance;
                            if (!OnBoard(toRow, toColumn)) continue;
                            bool allowed = kind == Advisor ? InPalace(side, toRow, toColumn) :
                                !CrossedRiver(side, toRow) && board[Index(row + dr, column + dc)] == 0;
                            if (allowed && AddMove(from, toRow, toColumn, side, tacticalOnly, result)) return true;
                        }
                        break;
                    case Horse:
                        for (int direction = 0; direction < HorseRows.Length; direction++)
                        {
                            int dr = HorseRows[direction];
                            int dc = HorseColumns[direction];
                            if (CanHorseMove(row, column, dr, dc) && AddMove(from, row + dr, column + dc, side, tacticalOnly, result))
                                return true;
                        }
                        break;
                    case Rook:
                    case Cannon:
                        for (int direction = 0; direction < 4; direction++)
                        {
                            int dr = StraightRows[direction];
                            int dc = StraightColumns[direction];
                            bool screen = false;
                            for (int r = row + dr, c = column + dc; OnBoard(r, c); r += dr, c += dc)
                            {
                                bool occupied = board[Index(r, c)] != 0;
                                bool allowed = kind == Rook || screen == occupied;
                                if (allowed && AddMove(from, r, c, side, tacticalOnly, result)) return true;
                                if (!occupied) continue;
                                if (kind == Rook || screen) break;
                                screen = true;
                            }
                        }
                        break;
                    case Pawn:
                        if (AddMove(from, row + Forward(side), column, side, tacticalOnly, result)) return true;
                        if (CrossedRiver(side, row) && (AddMove(from, row, column - 1, side, tacticalOnly, result) ||
                            AddMove(from, row, column + 1, side, tacticalOnly, result))) return true;
                        break;
                }
            }
            return result != null && result.Count > 0;
        }

        private bool AddMove(int from, int row, int column, int side, bool tacticalOnly, List<BoardMove> result)
        {
            if (!OnBoard(row, column)) return false;
            int to = Index(row, column);
            sbyte captured = board[to];
            if (captured != 0 && (SideOf(captured) == side || Math.Abs(captured) == King)) return false;
            if (!LeavesKingSafe(from, to, side, tacticalOnly && captured == 0)) return false;
            if (result == null) return true;
            result.Add(new BoardMove(from, to));
            return false;
        }

        private bool IsLegalMove(int from, int to, int side)
        {
            sbyte piece = board[from];
            sbyte captured = board[to];
            return piece != 0 && SideOf(piece) == side &&
                (captured == 0 || (SideOf(captured) != side && Math.Abs(captured) != King)) &&
                Reaches(from, to, side) && LeavesKingSafe(from, to, side);
        }

        private bool Reaches(int from, int to, int side)
        {
            if (from == to) return false;
            int row = from / Columns;
            int column = from % Columns;
            int toRow = to / Columns;
            int toColumn = to % Columns;
            int dr = toRow - row;
            int dc = toColumn - column;
            switch (Math.Abs(board[from]))
            {
                case King:
                    return Math.Abs(dr) + Math.Abs(dc) == 1 && InPalace(side, toRow, toColumn);
                case Advisor:
                    return Math.Abs(dr) == 1 && Math.Abs(dc) == 1 && InPalace(side, toRow, toColumn);
                case Elephant:
                    return Math.Abs(dr) == 2 && Math.Abs(dc) == 2 && !CrossedRiver(side, toRow) &&
                        board[Index(row + dr / 2, column + dc / 2)] == 0;
                case Horse:
                    return Math.Abs(dr * dc) == 2 && CanHorseMove(row, column, dr, dc);
                case Rook:
                    return (dr == 0 || dc == 0) && Screens(from, to) == 0;
                case Cannon:
                    return (dr == 0 || dc == 0) && Screens(from, to) == (board[to] == 0 ? 0 : 1);
                case Pawn:
                    return dr == Forward(side) && dc == 0 || dr == 0 && Math.Abs(dc) == 1 && CrossedRiver(side, row);
                default:
                    return false;
            }
        }

        private bool CanHorseMove(int row, int column, int dr, int dc) =>
            OnBoard(row + dr, column + dc) && board[Index(row + dr / 2, column + dc / 2)] == 0;

        private int Screens(int from, int to)
        {
            int step = Columns * Math.Sign(to / Columns - from / Columns) + Math.Sign(to % Columns - from % Columns);
            int count = 0;
            for (int square = from + step; square != to; square += step)
                if (board[square] != 0) count++;
            return count;
        }

        private bool LeavesKingSafe(int from, int to, int side, bool mustCheck = false)
        {
            sbyte piece = board[from];
            sbyte captured = board[to];
            MovePiece(from, to, piece);
            bool legal = !KingInCheck(side) && (!mustCheck || KingInCheck(1 - side));
            RestorePiece(from, to, piece, captured);
            return legal;
        }

        private void MovePiece(int from, int to, sbyte piece)
        {
            board[to] = piece;
            board[from] = 0;
            if (Math.Abs(piece) == King) kings[SideOf(piece)] = to;
        }

        private void RestorePiece(int from, int to, sbyte piece, sbyte captured)
        {
            board[from] = piece;
            board[to] = captured;
            if (Math.Abs(piece) == King) kings[SideOf(piece)] = from;
        }

        private bool KingInCheck(int side)
        {
            int king = kings[side];
            int row = king / Columns;
            int column = king % Columns;
            int enemy = 1 - side;
            int enemySign = enemy == 0 ? 1 : -1;
            for (int direction = 0; direction < 4; direction++)
            {
                int dr = StraightRows[direction];
                int dc = StraightColumns[direction];
                bool screen = false;
                for (int r = row + dr, c = column + dc; OnBoard(r, c); r += dr, c += dc)
                {
                    int kind = board[Index(r, c)] * enemySign;
                    if (kind == 0) continue;
                    if (screen)
                    {
                        if (kind == Cannon) return true;
                        break;
                    }
                    if (kind == Rook || (kind == King && dc == 0)) return true;
                    screen = true;
                }
            }
            for (int direction = 0; direction < HorseRows.Length; direction++)
            {
                int dr = HorseRows[direction];
                int dc = HorseColumns[direction];
                int horseRow = row - dr;
                int horseColumn = column - dc;
                if (OnBoard(horseRow, horseColumn) && board[Index(horseRow, horseColumn)] * enemySign == Horse &&
                    CanHorseMove(horseRow, horseColumn, dr, dc)) return true;
            }
            int pawnRow = row - Forward(enemy);
            if (OnBoard(pawnRow, column) && board[Index(pawnRow, column)] * enemySign == Pawn) return true;
            return CrossedRiver(enemy, row) &&
                (board[Index(row, column - 1)] * enemySign == Pawn || board[Index(row, column + 1)] * enemySign == Pawn);
        }
    }
}
