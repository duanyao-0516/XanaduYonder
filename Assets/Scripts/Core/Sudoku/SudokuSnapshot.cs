using System;

namespace XanaduYonder.Core.Sudoku
{
    [Serializable]
    public sealed class SudokuUndoFrame
    {
        public int[] Values, Notes;
        public SudokuUndoFrame Copy()
        {
            return new SudokuUndoFrame { Values = Values == null ? null : (int[])Values.Clone(), Notes = Notes == null ? null : (int[])Notes.Clone() };
        }
    }

    [Serializable]
    public sealed class SudokuSnapshot
    {
        public SudokuPuzzle Puzzle;
        public int[] Values, Notes;
        public int Mistakes, HintsUsed, Revision;
        // Oldest first. Each frame owns the entire board immediately before one committed edit.
        public SudokuUndoFrame[] History;

        public SudokuSnapshot Copy()
        {
            SudokuUndoFrame[] frames = History == null ? null : new SudokuUndoFrame[History.Length];
            if (frames != null) for (int i = 0; i < frames.Length; i++) frames[i] = History[i] == null ? null : History[i].Copy();
            return new SudokuSnapshot { Puzzle = Puzzle == null ? null : Puzzle.Copy(),
                Values = Values == null ? null : (int[])Values.Clone(), Notes = Notes == null ? null : (int[])Notes.Clone(),
                Mistakes = Mistakes, HintsUsed = HintsUsed, Revision = Revision, History = frames };
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (Puzzle == null) return Invalid("Puzzle is required.", out error);
            if (!Puzzle.TryValidate(out error)) return false;
            if (Mistakes < 0 || Mistakes > 3 || HintsUsed < 0 || HintsUsed > 3 || Revision < 0)
                return Invalid("Cumulative counters are outside their ranges.", out error);
            if (History == null || History.Length > Revision || ((Revision - History.Length) & 1) != 0)
                return Invalid("Undo history disagrees with the board revision.", out error);
            if (!ValidateBoard(Values, Notes, Mistakes, out error)) return false;
            if (Mistakes == 3 && (History.Length == 0 || Complete(Values)))
                return Invalid("A lost game must retain its final mistaken input.", out error);
            for (int i = 0; i < History.Length; i++)
            {
                var frame = History[i];
                if (frame == null) return Invalid("Undo frame is required.", out error);
                if (!ValidateBoard(frame.Values, frame.Notes, Math.Min(Mistakes, 2), out error)) return false;
                if (Complete(frame.Values)) return Invalid("Undo history cannot contain an already completed game.", out error);
            }
            int[] firstValues = History.Length == 0 ? Values : History[0].Values;
            int[] firstNotes = History.Length == 0 ? Notes : History[0].Notes;
            for (int cell = 0; cell < 81; cell++)
                if (firstValues[cell] != Puzzle.Givens[cell] || firstNotes[cell] != 0)
                    return Invalid("Undo history must begin at the original puzzle.", out error);
            int visibleMistakes = 0;
            bool finalMistake = false;
            for (int i = 0; i < History.Length; i++)
            {
                int[] nextValues = i + 1 == History.Length ? Values : History[i + 1].Values;
                int[] nextNotes = i + 1 == History.Length ? Notes : History[i + 1].Notes;
                bool mistake;
                if (!ValidateTransition(History[i].Values, History[i].Notes, nextValues, nextNotes, out mistake))
                    return Invalid("Undo history contains an invalid board edit.", out error);
                if (mistake) visibleMistakes++;
                if (i + 1 == History.Length) finalMistake = mistake;
                else if (visibleMistakes >= 3) return Invalid("An edit occurred after the third mistake.", out error);
            }
            // Every removed history entry requires one edit and one undo in Revision.
            int undoneEdits = (Revision - History.Length) / 2;
            if (visibleMistakes > Mistakes || Mistakes - visibleMistakes > undoneEdits || (Mistakes == 3 && !finalMistake))
                return Invalid("Mistake count disagrees with the undo history.", out error);
            return true;
        }

        private bool ValidateBoard(int[] values, int[] notes, int maxWrong, out string error)
        {
            error = null;
            if (values == null || notes == null || values.Length != 81 || notes.Length != 81)
                return Invalid("Board arrays must contain 81 cells.", out error);
            int wrong = 0;
            for (int cell = 0; cell < 81; cell++)
            {
                if (values[cell] < 0 || values[cell] > 9 || notes[cell] < 0 || notes[cell] > 511)
                    return Invalid("Board value or candidate mask is out of range.", out error);
                if (Puzzle.Givens[cell] != 0 && values[cell] != Puzzle.Givens[cell])
                    return Invalid("A given digit has been changed.", out error);
                if (values[cell] != 0 && notes[cell] != 0)
                    return Invalid("Formal values and notes cannot share a cell.", out error);
                if (values[cell] != 0 && values[cell] != Puzzle.Solution[cell]) wrong++;
            }
            if (wrong > maxWrong) return Invalid("Red values exceed the cumulative mistake count.", out error);
            return true;
        }

        private bool Complete(int[] values)
        {
            for (int i = 0; i < 81; i++) if (values[i] != Puzzle.Solution[i]) return false;
            return true;
        }

        private bool ValidateTransition(int[] previous, int[] oldNotes, int[] next, int[] newNotes, out bool mistake)
        {
            mistake = false;
            int target = -1;
            for (int i = 0; i < 81; i++)
                if (previous[i] != next[i]) { if (target != -1) return false; target = i; }
            if (target != -1)
            {
                int digit = next[target];
                mistake = digit != 0 && digit != Puzzle.Solution[target];
                if (digit != 0)
                {
                    int correctCount = 0;
                    for (int i = 0; i < 81; i++) if (previous[i] == digit && previous[i] == Puzzle.Solution[i]) correctCount++;
                    if (correctCount == 9) return false;
                }
                for (int i = 0; i < 81; i++)
                {
                    int expected = oldNotes[i];
                    if (i == target) expected = 0;
                    else if (digit != 0 && !mistake && SudokuGame.ArePeers(target, i)) expected &= ~(1 << (digit - 1));
                    if (newNotes[i] != expected) return false;
                }
                return true;
            }
            for (int i = 0; i < 81; i++)
                if (oldNotes[i] != newNotes[i]) { if (target != -1) return false; target = i; }
            if (target == -1 || previous[target] != 0) return false;
            if (newNotes[target] == 0) return true; // The eraser clears all notes in this cell.
            int changedBit = oldNotes[target] ^ newNotes[target];
            if ((changedBit & (changedBit - 1)) != 0) return false;
            if ((newNotes[target] & changedBit) == 0) return true; // Removing a note precedes conflict checks.
            for (int other = 0; other < 81; other++)
                if (SudokuGame.ArePeers(target, other) && previous[other] != 0 && (1 << (previous[other] - 1)) == changedBit) return false;
            return true;
        }

        private static bool Invalid(string reason, out string error) { error = reason; return false; }
    }
}
