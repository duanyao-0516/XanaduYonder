using System;

namespace XanaduYonder.Core.Sudoku
{
    public enum SudokuPhase { Playing, Won, Lost }

    public sealed class SudokuMove
    {
        public readonly bool Changed, Mistake, RejectedNote;
        public readonly int[] Conflicts;
        public readonly int Cell, Digit;
        public readonly SudokuPhase Phase;

        public SudokuMove(bool changed, bool mistake, bool rejectedNote, int[] conflicts, int cell, int digit, SudokuPhase phase)
        {
            Changed = changed; Mistake = mistake; RejectedNote = rejectedNote;
            Conflicts = conflicts == null ? new int[0] : (int[])conflicts.Clone();
            Cell = cell; Digit = digit; Phase = phase;
        }

        public static SudokuMove NoOp(SudokuPhase phase, int cell = -1, int digit = 0)
        {
            return new SudokuMove(false, false, false, null, cell, digit, phase);
        }
    }
}
