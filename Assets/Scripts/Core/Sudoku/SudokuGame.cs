using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Sudoku
{
    // The board owns gameplay state; modal/lifecycle and paid-hint identity belong to the session.
    public sealed class SudokuGame
    {
        private readonly SudokuPuzzle puzzle;
        private int[] values, notes;
        private readonly List<SudokuUndoFrame> history;
        private int mistakes, hintsUsed, revision;

        public SudokuGame(SudokuPuzzle source)
        {
            if (source == null) throw new ArgumentNullException("source");
            string error;
            if (!source.TryValidate(out error)) throw new ArgumentException(error, "source");
            puzzle = source.Copy(); values = (int[])puzzle.Givens.Clone(); notes = new int[81];
            history = new List<SudokuUndoFrame>();
        }

        public SudokuGame(SudokuSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            string error;
            if (!snapshot.TryValidate(out error)) throw new ArgumentException(error, "snapshot");
            var owned = snapshot.Copy();
            puzzle = owned.Puzzle; values = owned.Values; notes = owned.Notes;
            mistakes = owned.Mistakes; hintsUsed = owned.HintsUsed; revision = owned.Revision;
            history = new List<SudokuUndoFrame>(owned.History);
        }

        public SudokuPuzzle Puzzle { get { return puzzle.Copy(); } }
        public int Errors { get { return mistakes; } }
        public int HintsRemaining { get { return 3 - hintsUsed; } }
        public int Revision { get { return revision; } }
        public SudokuPhase Phase { get { return mistakes == 3 ? SudokuPhase.Lost : RemainingCount == 0 ? SudokuPhase.Won : SudokuPhase.Playing; } }
        public bool CanUndo { get { return Phase == SudokuPhase.Playing && history.Count > 0 && revision < int.MaxValue; } }
        public int RemainingCount
        {
            get { int count = 0; for (int i = 0; i < 81; i++) if (values[i] != puzzle.Solution[i]) count++; return count; }
        }

        public int ValueAt(int cell) { return ValidCell(cell) ? values[cell] : 0; }
        public int NotesAt(int cell) { return ValidCell(cell) ? notes[cell] : 0; }
        public bool IsGiven(int cell) { return ValidCell(cell) && puzzle.Givens[cell] != 0; }
        public bool IsCorrect(int cell) { return ValidCell(cell) && values[cell] == puzzle.Solution[cell]; }

        public int RemainingDigit(int digit)
        {
            if (!ValidDigit(digit)) return 0;
            int remaining = 9;
            for (int cell = 0; cell < 81; cell++) if (values[cell] == digit && values[cell] == puzzle.Solution[cell]) remaining--;
            return remaining;
        }

        public SudokuSnapshot GetSnapshot()
        {
            var frames = new SudokuUndoFrame[history.Count];
            for (int i = 0; i < frames.Length; i++) frames[i] = history[i].Copy();
            return new SudokuSnapshot {
                Puzzle = puzzle.Copy(), Values = (int[])values.Clone(), Notes = (int[])notes.Clone(),
                Mistakes = mistakes, HintsUsed = hintsUsed, Revision = revision, History = frames
            };
        }

        public SudokuMove EnterValue(int cell, int digit)
        {
            if (!CanEdit(cell) || !ValidDigit(digit) || values[cell] == digit || RemainingDigit(digit) == 0)
                return SudokuMove.NoOp(Phase, cell, digit);
            bool mistake = puzzle.Solution[cell] != digit;
            int[] conflicts = mistake ? FindConflicts(cell, digit) : new int[0];
            Remember();
            values[cell] = digit; notes[cell] = 0;
            if (mistake) mistakes++;
            else
                for (int other = 0; other < 81; other++)
                    if (ArePeers(cell, other)) notes[other] &= ~(1 << (digit - 1));
            revision++;
            return new SudokuMove(true, mistake, false, conflicts, cell, digit, Phase);
        }

        public SudokuMove ToggleNote(int cell, int digit)
        {
            if (!CanEdit(cell) || !ValidDigit(digit) || values[cell] != 0) return SudokuMove.NoOp(Phase, cell, digit);
            int bit = 1 << (digit - 1);
            if ((notes[cell] & bit) == 0)
            {
                int[] conflicts = FindConflicts(cell, digit);
                if (conflicts.Length > 0) return new SudokuMove(false, false, true, conflicts, cell, digit, Phase);
                if (RemainingDigit(digit) == 0) return SudokuMove.NoOp(Phase, cell, digit);
            }
            Remember(); notes[cell] ^= bit; revision++;
            return new SudokuMove(true, false, false, null, cell, digit, Phase);
        }

        public SudokuMove Erase(int cell)
        {
            if (!CanEdit(cell) || (values[cell] == 0 && notes[cell] == 0)) return SudokuMove.NoOp(Phase, cell);
            Remember(); values[cell] = 0; notes[cell] = 0; revision++;
            return new SudokuMove(true, false, false, null, cell, 0, Phase);
        }

        public SudokuMove Undo()
        {
            if (!CanUndo) return SudokuMove.NoOp(Phase);
            var before = history[history.Count - 1];
            int cell = -1;
            for (int i = 0; i < 81; i++) if (values[i] != before.Values[i]) { cell = i; break; }
            if (cell < 0) for (int i = 0; i < 81; i++) if (notes[i] != before.Notes[i]) { cell = i; break; }
            values = before.Values; notes = before.Notes; history.RemoveAt(history.Count - 1); revision++;
            return new SudokuMove(true, false, false, null, cell, cell < 0 ? 0 : values[cell], Phase);
        }

        public bool SpendHint()
        {
            if (Phase != SudokuPhase.Playing || hintsUsed == 3) return false;
            hintsUsed++; return true;
        }

        public SudokuMove ApplyHint(int cell, int digit, int expectedRevision)
        {
            // The session additionally validates the paid hint's identity, game id and single use.
            if (!ValidCell(cell) || expectedRevision != revision || hintsUsed == 0 || puzzle.Solution[cell] != digit)
                return SudokuMove.NoOp(Phase, cell, digit);
            return EnterValue(cell, digit);
        }

        private void Remember()
        {
            history.Add(new SudokuUndoFrame { Values = (int[])values.Clone(), Notes = (int[])notes.Clone() });
        }

        private bool CanEdit(int cell)
        {
            return ValidCell(cell) && puzzle.Givens[cell] == 0 && Phase == SudokuPhase.Playing && revision < int.MaxValue;
        }

        private int[] FindConflicts(int cell, int digit)
        {
            var conflicts = new List<int>();
            for (int other = 0; other < 81; other++) if (ArePeers(cell, other) && values[other] == digit) conflicts.Add(other);
            return conflicts.ToArray();
        }

        internal static bool ValidCell(int cell) { return cell >= 0 && cell < 81; }
        internal static bool ValidDigit(int digit) { return digit >= 1 && digit <= 9; }
        internal static bool ArePeers(int a, int b)
        {
            return a != b && (a / 9 == b / 9 || a % 9 == b % 9 || (a / 27 == b / 27 && a % 9 / 3 == b % 9 / 3));
        }
    }
}
