using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.BoardGames
{
    internal static class ArmyBoard
    {
        internal const int Columns = 5;
        internal const int Rows = 12;
        internal const int PointCount = Columns * Rows;
        internal const int HighestKind = (int)ArmyPieceKind.Flag;
        internal const int PiecesPerSide = 25;
        internal const int MovableKinds = (1 << ((int)ArmyPieceKind.Bomb + 1)) - 2;

        internal static readonly int[] Inventory = { 0, 3, 3, 3, 2, 2, 2, 2, 1, 1, 2, 3, 1 };
        private static readonly ulong[] Roads = new ulong[PointCount];
        private static readonly ulong[] Rails = new ulong[PointCount];
        private static readonly int[,] RailSteps = new int[PointCount, 4];
        private static readonly int[] RailOffsets = { -Columns, -1, 1, Columns };

        static ArmyBoard()
        {
            for (int from = 0; from < PointCount; from++)
            {
                for (int to = 0; to < PointCount; to++)
                {
                    if (RailEdge(from, to))
                        Rails[from] |= 1UL << to;
                    else if (Adjacent(from, to))
                        Roads[from] |= 1UL << to;
                }

                for (int direction = 0; direction < RailOffsets.Length; direction++)
                {
                    int to = from + RailOffsets[direction];
                    RailSteps[from, direction] = IsRail(from, to) ? to : -1;
                }
            }
        }

        internal static bool IsPoint(int point) => point >= 0 && point < PointCount;

        internal static bool IsCamp(int point)
        {
            if (!IsPoint(point)) return false;
            int row = point / Columns;
            int col = point % Columns;
            return row == 3 || row == 8 ? col == 2 :
                (row == 2 || row == 4 || row == 7 || row == 9) && (col == 1 || col == 3);
        }

        internal static bool IsHeadquarters(int point)
        {
            if (!IsPoint(point)) return false;
            int row = point / Columns;
            int col = point % Columns;
            return (row == 0 || row == Rows - 1) && (col == 1 || col == 3);
        }

        internal static bool IsRoad(int from, int to) => IsPoint(from) && IsPoint(to) && (Roads[from] & (1UL << to)) != 0;

        internal static bool IsRail(int from, int to) => IsPoint(from) && IsPoint(to) && (Rails[from] & (1UL << to)) != 0;

        private static bool Adjacent(int from, int to)
        {
            int fromRow = from / Columns;
            int toRow = to / Columns;
            int rowDelta = Math.Abs(fromRow - toRow);
            int fromCol = from % Columns;
            int colDelta = Math.Abs(fromCol - to % Columns);
            if (rowDelta > 1 || colDelta > 1) return false;
            if (Math.Min(fromRow, toRow) == 5 && Math.Max(fromRow, toRow) == 6)
                return colDelta == 0 && fromCol % 2 == 0;
            return rowDelta + colDelta == 1 || rowDelta == 1 && colDelta == 1 && (IsCamp(from) || IsCamp(to));
        }

        private static bool RailEdge(int from, int to)
        {
            int row = from / Columns;
            int toRow = to / Columns;
            int col = from % Columns;
            int toCol = to % Columns;
            if (row == toRow && Math.Abs(col - toCol) == 1)
                return row == 1 || row == 5 || row == 6 || row == 10;
            if (col != toCol || Math.Abs(row - toRow) != 1) return false;
            if (col == 0 || col == Columns - 1)
                return Math.Min(row, toRow) >= 1 && Math.Max(row, toRow) <= 10;
            return col == 2 && Math.Min(row, toRow) == 5 && Math.Max(row, toRow) == 6;
        }

        internal static int FormationDomain(int slot)
        {
            if (IsCamp(ArmyGame.FormationPoint(0, slot))) return 0;
            int domain = (1 << ((int)ArmyPieceKind.Marshal + 1)) - 2;
            int row = slot / Columns;
            if (row > 0) domain |= KindBit(ArmyPieceKind.Bomb);
            if (row >= 4) domain |= KindBit(ArmyPieceKind.Mine);
            if (IsHeadquarters(ArmyGame.FormationPoint(0, slot))) domain |= KindBit(ArmyPieceKind.Flag);
            return domain;
        }

        internal static int KindBit(ArmyPieceKind kind) => 1 << (int)kind;

        internal static bool CanMove(int kind) => kind >= (int)ArmyPieceKind.Engineer && kind <= (int)ArmyPieceKind.Bomb;

        internal static bool RequiresEngineer(int from, int to, int[] sides, int movingSide)
        {
            return (Destinations(from, (int)ArmyPieceKind.Platoon, sides, movingSide) & (1UL << to)) == 0;
        }

        internal static ulong Destinations(int from, int kind, int[] sides, int movingSide)
        {
            if (!CanMove(kind) || IsHeadquarters(from)) return 0;

            ulong targets = 0;
            ulong roads = Roads[from];
            while (roads != 0)
            {
                int to = TakePoint(ref roads);
                if (CanLand(to, sides, movingSide)) targets |= 1UL << to;
            }

            if (kind == (int)ArmyPieceKind.Engineer)
                return targets | EngineerRailDestinations(from, sides, movingSide);

            for (int direction = 0; direction < RailOffsets.Length; direction++)
            {
                int to = RailSteps[from, direction];
                while (to >= 0)
                {
                    if (CanLand(to, sides, movingSide)) targets |= 1UL << to;
                    if (sides[to] >= 0) break;
                    to = RailSteps[to, direction];
                }
            }
            return targets;
        }

        private static ulong EngineerRailDestinations(int from, int[] sides, int movingSide)
        {
            ulong visited = 1UL << from;
            ulong frontier = Rails[from];
            ulong targets = 0;
            while (frontier != 0)
            {
                int to = TakePoint(ref frontier);
                visited |= 1UL << to;
                if (CanLand(to, sides, movingSide)) targets |= 1UL << to;
                if (sides[to] < 0) frontier |= Rails[to] & ~visited;
            }
            return targets;
        }

        private static bool CanLand(int to, int[] sides, int movingSide)
        {
            return sides[to] < 0 || sides[to] != movingSide && !IsCamp(to);
        }

        internal static List<BoardMove> LegalMoves(int[] kinds, int[] sides, int movingSide)
        {
            var moves = new List<BoardMove>();
            for (int from = 0; from < PointCount; from++)
            {
                if (sides[from] != movingSide) continue;
                ulong targets = Destinations(from, kinds[from], sides, movingSide);
                while (targets != 0) moves.Add(new BoardMove(from, TakePoint(ref targets)));
            }
            return moves;
        }

        internal static bool HasLegalMove(int[] kinds, int[] sides, int movingSide)
        {
            for (int from = 0; from < PointCount; from++)
                if (sides[from] == movingSide && Destinations(from, kinds[from], sides, movingSide) != 0) return true;
            return false;
        }

        private static int TakePoint(ref ulong points)
        {
            ulong bits = points & (~points + 1);
            points &= points - 1;
            int shift = 0;
            if (bits > uint.MaxValue) { bits >>= 32; shift += 32; }
            if (bits > ushort.MaxValue) { bits >>= 16; shift += 16; }
            if (bits > byte.MaxValue) { bits >>= 8; shift += 8; }
            if (bits > 15) { bits >>= 4; shift += 4; }
            if (bits > 3) { bits >>= 2; shift += 2; }
            return shift + (bits > 1 ? 1 : 0);
        }
    }
}
