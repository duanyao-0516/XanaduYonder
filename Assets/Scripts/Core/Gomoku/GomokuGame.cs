using System;

namespace XanaduYonder.Core.Gomoku
{
    /// <summary>15 x 15 freestyle Gomoku. History is the sole restoration format.</summary>
    public sealed class GomokuGame
    {
        public const int Size = 15, CellCount = Size * Size;
        private readonly GomokuStone[] cells = new GomokuStone[CellCount];
        private readonly int[] moves = new int[CellCount];
        private int[] winningLine = new int[0];
        public GomokuState State { get; private set; }
        public GomokuStone ToPlay { get { return MoveCount % 2 == 0 ? GomokuStone.Black : GomokuStone.White; } }
        public int MoveCount { get; private set; }
        public int LastMove { get { return MoveCount == 0 ? -1 : moves[MoveCount - 1]; } }
        public int[] WinningLine { get { return (int[])winningLine.Clone(); } }

        public static GomokuGame Restore(int[] history)
        {
            if (history == null) throw new ArgumentNullException(nameof(history));
            if (history.Length > CellCount) throw new ArgumentException("History is longer than the board.", nameof(history));
            var game = new GomokuGame();
            for (int i = 0; i < history.Length; i++)
                if (!game.TryPlay(history[i])) throw new ArgumentException("Illegal move at history position " + i + ".", nameof(history));
            return game;
        }

        public GomokuStone GetCell(int index)
        {
            if (index < 0 || index >= CellCount) throw new ArgumentOutOfRangeException(nameof(index));
            return cells[index];
        }

        public bool TryPlay(int index)
        {
            if (State != GomokuState.Playing || index < 0 || index >= CellCount || cells[index] != GomokuStone.Empty) return false;
            GomokuStone stone = ToPlay;
            cells[index] = stone; moves[MoveCount++] = index;
            winningLine = FindWinningLine(index, stone);
            if (winningLine.Length >= 5) State = stone == GomokuStone.Black ? GomokuState.BlackWon : GomokuState.WhiteWon;
            else if (MoveCount == CellCount) State = GomokuState.Draw;
            return true;
        }

        public bool Undo(int count)
        {
            if (count <= 0 || count > MoveCount) return false;
            for (int n = 0; n < count; n++) cells[moves[--MoveCount]] = GomokuStone.Empty;
            State = GomokuState.Playing; winningLine = new int[0];
            return true;
        }

        public int[] ExportMoves()
        {
            var result = new int[MoveCount]; Array.Copy(moves, result, MoveCount); return result;
        }

        public GomokuGame Copy()
        {
            var result = new GomokuGame();
            Array.Copy(cells, result.cells, CellCount); Array.Copy(moves, result.moves, MoveCount);
            result.MoveCount = MoveCount; result.State = State; result.winningLine = (int[])winningLine.Clone();
            return result;
        }

        private int[] FindWinningLine(int index, GomokuStone stone)
        {
            int row = index / Size, col = index % Size;
            for (int direction = 0; direction < 4; direction++)
            {
                int dr = direction == 0 ? 0 : 1, dc = direction == 1 ? 0 : direction == 3 ? -1 : 1;
                int before = Count(row, col, -dr, -dc, stone), after = Count(row, col, dr, dc, stone);
                if (before + after + 1 < 5) continue;
                var result = new int[before + after + 1];
                for (int i = 0; i < result.Length; i++) result[i] = (row + (i - before) * dr) * Size + col + (i - before) * dc;
                return result;
            }
            return new int[0];
        }

        private int Count(int row, int col, int dr, int dc, GomokuStone stone)
        {
            int count = 0; row += dr; col += dc;
            while (row >= 0 && row < Size && col >= 0 && col < Size && cells[row * Size + col] == stone)
            { count++; row += dr; col += dc; }
            return count;
        }
    }
}
