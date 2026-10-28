using System;

namespace XanaduYonder.Core.Sudoku
{
    /// <summary>Exact backtracking verifier; counts at most two solutions and never mutates the input.</summary>
    public static class SudokuSolver
    {
        public static int CountSolutions(int[] givens, out int[] solution)
        {
            solution = null;
            if (givens == null || givens.Length != 81) return 0;
            var board = (int[])givens.Clone();
            var rows = new int[9]; var columns = new int[9]; var boxes = new int[9];
            for (int cell = 0; cell < 81; cell++)
            {
                int digit = board[cell];
                if (digit < 0 || digit > 9) return 0;
                if (digit == 0) continue;
                int row = cell / 9, col = cell % 9, box = row / 3 * 3 + col / 3, bit = 1 << digit;
                if (((rows[row] | columns[col] | boxes[box]) & bit) != 0) return 0;
                rows[row] |= bit; columns[col] |= bit; boxes[box] |= bit;
            }
            int count = 0;
            Search(board, rows, columns, boxes, ref count, ref solution);
            return count;
        }

        private static void Search(int[] board, int[] rows, int[] columns, int[] boxes, ref int count, ref int[] solution)
        {
            if (count >= 2) return;
            int target = -1, mask = 0, best = 10;
            for (int cell = 0; cell < 81; cell++)
            {
                if (board[cell] != 0) continue;
                int candidates = 1022 & ~(rows[cell / 9] | columns[cell % 9] | boxes[cell / 27 * 3 + cell % 9 / 3]);
                int size = PopCount(candidates);
                if (size == 0) return;
                if (size < best) { target = cell; mask = candidates; best = size; if (size == 1) break; }
            }
            if (target < 0) { count++; if (solution == null) solution = (int[])board.Clone(); return; }
            int row = target / 9, col = target % 9, box = row / 3 * 3 + col / 3;
            for (int digit = 1; digit <= 9 && count < 2; digit++)
            {
                int bit = 1 << digit;
                if ((mask & bit) == 0) continue;
                board[target] = digit; rows[row] |= bit; columns[col] |= bit; boxes[box] |= bit;
                Search(board, rows, columns, boxes, ref count, ref solution);
                board[target] = 0; rows[row] &= ~bit; columns[col] &= ~bit; boxes[box] &= ~bit;
            }
        }

        internal static int PopCount(int bits) { int count = 0; while (bits != 0) { bits &= bits - 1; count++; } return count; }
    }
}
