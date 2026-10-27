using System;
using System.Collections.Generic;
using XanaduYonder.Core.Sudoku;

namespace XanaduYonder.Runtime.Sudoku
{
    // Platform independent session state. Modals do not stop time; a hidden board does.
    public sealed class SudokuSession
    {
        private readonly SudokuSaveData data;
        private int hintRequestNumber;
        public SudokuGame Game { get; private set; }
        public SudokuControls Controls { get; private set; }
        public bool Paused { get; private set; }
        public bool Foreground { get; private set; } = true;
        public double ElapsedSeconds { get { return data.ElapsedSeconds; } }
        public int CompletedCount { get { return data.CompletedIds.Length; } }
        public bool CanPlay { get { return Foreground && !Paused && Game.Phase == SudokuPhase.Playing; } }
        public SudokuSavedHint CurrentHint { get { return ValidHint ? data.Hint.Copy() : null; } }
        private bool ValidHint { get { return data.Hint != null && data.Hint.Revision == Game.Revision && !Game.IsCorrect(data.Hint.Cell); } }

        public SudokuSession(SudokuSaveData saved, bool restoring)
        {
            if (saved == null || saved.Game == null) throw new ArgumentException("A session needs a game.");
            data = saved.Copy();
            Game = new SudokuGame(data.Game);
            Controls = new SudokuControls { SelectedCell = data.SelectedCell, QuickDigit = data.QuickDigit, Quick = data.Quick, Pencil = data.Pencil };
            Controls.Normalize(Game);
            Paused = restoring;
            RecordCompletion();
        }

        public void Tick(double seconds)
        {
            if (!CanPlay || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) return;
            data.ElapsedSeconds = Math.Min(315360000, data.ElapsedSeconds + seconds);
        }
        public void Pause() { Paused = true; CancelHint(); }
        public void Resume() { if (Foreground) Paused = false; }
        public void SetForeground(bool foreground) { Foreground = foreground; if (!foreground) Pause(); }

        public SudokuMove SelectCell(int cell) { return CanPlay ? After(Controls.SelectCell(Game, cell)) : SudokuMove.NoOp(Game.Phase); }
        public SudokuMove SelectDigit(int digit) { return CanPlay ? After(Controls.SelectDigit(Game, digit)) : SudokuMove.NoOp(Game.Phase); }
        public SudokuMove Erase() { return CanPlay ? After(Game.Erase(Controls.SelectedCell)) : SudokuMove.NoOp(Game.Phase); }
        public SudokuMove Undo() { return CanPlay ? After(Game.Undo()) : SudokuMove.NoOp(Game.Phase); }

        private SudokuMove After(SudokuMove move)
        {
            if (move.Changed) { data.Hint = null; CancelHint(); }
            Controls.Normalize(Game);
            RecordCompletion();
            return move;
        }

        public int FirstError()
        {
            for (int i = 0; i < 81; i++) if (Game.ValueAt(i) != 0 && !Game.IsCorrect(i)) return i;
            return -1;
        }

        public SudokuHintRequest BeginHint(int preferredCell)
        {
            if (!CanPlay || FirstError() >= 0 || ValidHint || Game.HintsRemaining == 0) return null;
            var values = new int[81]; for (int i = 0; i < 81; i++) values[i] = Game.ValueAt(i);
            return new SudokuHintRequest(this, ++hintRequestNumber, Game.Revision, Game.Puzzle, values, preferredCell);
        }

        public void CancelHint() { hintRequestNumber++; }

        // Called only on the UI thread, immediately before presenting the paid help.
        public bool AcceptHint(SudokuHintRequest request, SudokuHint hint)
        {
            if (request == null || request.Owner != this || request.Number != hintRequestNumber ||
                request.Revision != Game.Revision || !CanPlay || FirstError() >= 0 || hint == null ||
                hint.Cell < 0 || hint.Cell >= 81 || Game.ValueAt(hint.Cell) != 0 ||
                hint.Digit != Game.Puzzle.Solution[hint.Cell] || string.IsNullOrWhiteSpace(hint.Explanation) || hint.Explanation.Length > 8192 ||
                string.IsNullOrWhiteSpace(hint.Technique) || hint.Technique.Length > 128 || hint.Evidence == null || hint.Evidence.Length > 81) return false;
            foreach (int cell in hint.Evidence) if (cell < 0 || cell >= 81) return false;
            if (!Game.SpendHint()) return false;
            hintRequestNumber++;
            data.Hint = new SudokuSavedHint { Cell = hint.Cell, Digit = hint.Digit, Revision = Game.Revision,
                Technique = hint.Technique, Explanation = hint.Explanation, Evidence = (int[])hint.Evidence.Clone(),
                IsDirect = hint.IsDirect, Revealed = false };
            return true;
        }

        public void RevealHint() { if (CanPlay && ValidHint) data.Hint.Revealed = true; }
        public SudokuMove ApplyHint()
        {
            if (!CanPlay || !ValidHint || (data.Hint.IsDirect && !data.Hint.Revealed)) return SudokuMove.NoOp(Game.Phase);
            var hint = data.Hint;
            Controls.SelectedCell = hint.Cell;
            return After(Game.ApplyHint(hint.Cell, hint.Digit, hint.Revision));
        }

        private void RecordCompletion()
        {
            if (Game.Phase != SudokuPhase.Won) return;
            data.CompletedIds = AddOnce(data.CompletedIds, Game.Puzzle.Id);
            if (Game.HintsRemaining == 3) data.UnassistedIds = AddOnce(data.UnassistedIds, Game.Puzzle.Id);
        }
        private static string[] AddOnce(string[] ids, string id)
        {
            if (Array.IndexOf(ids, id) >= 0) return ids;
            var list = new List<string>(ids) { id }; return list.ToArray();
        }

        public SudokuSaveData Export()
        {
            RecordCompletion();
            data.Game = Game.GetSnapshot();
            data.Pencil = Controls.Pencil; data.Quick = Controls.Quick;
            data.SelectedCell = Controls.SelectedCell; data.QuickDigit = Controls.QuickDigit;
            if (!ValidHint) data.Hint = null;
            return data.Copy();
        }
    }

    public sealed class SudokuHintRequest
    {
        internal readonly SudokuSession Owner;
        internal readonly int Number;
        public readonly int Revision, PreferredCell;
        public readonly SudokuPuzzle Puzzle;
        public readonly int[] Values;
        internal SudokuHintRequest(SudokuSession owner, int number, int revision, SudokuPuzzle puzzle, int[] values, int preferredCell)
        {
            Owner = owner; Number = number; Revision = revision; Puzzle = puzzle; Values = values; PreferredCell = preferredCell;
        }
    }
}
