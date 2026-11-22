using System;

namespace XanaduYonder.Core.Sudoku
{
    [Serializable]
    public sealed class SudokuControls
    {
        public int SelectedCell = -1;
        public int QuickDigit;
        public bool Pencil, Quick;

        public SudokuMove SelectCell(SudokuGame game, int cell)
        {
            if (game == null) throw new ArgumentNullException("game");
            if (!SudokuGame.ValidCell(cell) || game.Phase != SudokuPhase.Playing) return SudokuMove.NoOp(game.Phase, cell);
            SelectedCell = cell;
            if (!Quick) return SudokuMove.NoOp(game.Phase, cell);
            if (game.ValueAt(cell) != 0)
            {
                if (game.IsCorrect(cell)) QuickDigit = game.ValueAt(cell);
                return SudokuMove.NoOp(game.Phase, cell, QuickDigit);
            }
            if (!SudokuGame.ValidDigit(QuickDigit)) return SudokuMove.NoOp(game.Phase, cell);
            var move = Write(game, cell, QuickDigit);
            if (!Pencil && move.Changed && !move.Mistake && game.RemainingDigit(QuickDigit) == 0)
            {
                QuickDigit = NextUnfinishedDigit(game, QuickDigit);
                if (QuickDigit != 0) SelectedCell = -1;
            }
            return move;
        }

        public SudokuMove SelectDigit(SudokuGame game, int digit)
        {
            if (game == null) throw new ArgumentNullException("game");
            if (!SudokuGame.ValidDigit(digit) || game.Phase != SudokuPhase.Playing) return SudokuMove.NoOp(game.Phase, SelectedCell, digit);
            if (Quick)
            {
                QuickDigit = digit;
                SelectedCell = -1;
                return SudokuMove.NoOp(game.Phase, digit: digit);
            }
            return Write(game, SelectedCell, digit);
        }

        public void TogglePencil() { Pencil = !Pencil; }
        public void SetQuick(SudokuGame game, bool quick)
        {
            if (game == null) throw new ArgumentNullException("game");
            if (Quick == quick) return;
            Quick = quick;
            QuickDigit = quick && game.IsCorrect(SelectedCell) ? game.ValueAt(SelectedCell) : 0;
        }

        private static int NextUnfinishedDigit(SudokuGame game, int previous)
        {
            for (int offset = 1; offset < 9; offset++)
            {
                int digit = (previous - 1 + offset) % 9 + 1;
                if (game.RemainingDigit(digit) > 0) return digit;
            }
            return 0;
        }

        public void Normalize(SudokuGame game)
        {
            if (game == null) throw new ArgumentNullException("game");
            if (!SudokuGame.ValidCell(SelectedCell)) SelectedCell = -1;
            if (!Quick || !SudokuGame.ValidDigit(QuickDigit)) QuickDigit = 0;
        }

        private SudokuMove Write(SudokuGame game, int cell, int digit)
        {
            // Existing notes are deletable even after their digit becomes exhausted/conflicted.
            if (Pencil) return game.ToggleNote(cell, digit);
            return game.EnterValue(cell, digit);
        }
    }
}
