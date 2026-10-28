using System;
using XanaduYonder.Core.Sudoku;
using XanaduYonder.Runtime.Sudoku;

internal static class Program
{
    private static int passed;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static SudokuPuzzle Puzzle()
    {
        const string solved = "534678912672195348198342567859761423426853791713924856961537284287419635345286179";
        var values = new int[81]; for (int i = 0; i < 81; i++) values[i] = solved[i] - '0';
        var givens = (int[])values.Clone(); givens[0] = givens[1] = givens[2] = givens[3] = 0;
        return new SudokuPuzzle { Id = "se-v1-d0-l001", Difficulty = 0, Level = 1, Givens = givens, Solution = values };
    }
    private static SudokuSession Fresh() { return new SudokuSession(new SudokuSaveData { Game = new SudokuGame(Puzzle()).GetSnapshot() }, false); }
    private static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    private static bool Request(SudokuSession s, int preferred)
    {
        if (s.CurrentHint != null && s.CanPlay) return true;
        var request = s.BeginHint(preferred);
        return request != null && s.AcceptHint(request, SudokuHintEngine.Find(request.Puzzle, request.Values, preferred));
    }
    private static void Main()
    {
        Run("timer stops through background and requires explicit resume", () => {
            var s = Fresh(); s.Tick(5); s.SetForeground(false); s.Tick(9); s.SetForeground(true); s.Tick(10);
            Check(s.ElapsedSeconds == 5 && s.Paused, "background counted or auto-resumed"); s.Resume(); s.Tick(2); Check(s.ElapsedSeconds == 7, "resume");
        });
        Run("restored game starts paused and retains modes", () => {
            var s = Fresh(); s.Controls.SetQuick(true); s.Controls.TogglePencil(); s.Tick(7);
            var restored = new SudokuSession(s.Export(), true); restored.Tick(10);
            Check(restored.Paused && restored.ElapsedSeconds == 7 && restored.Controls.Quick && restored.Controls.Pencil, "restore state");
        });
        Run("hint charge and cached review are single use with stale invalidation", () => {
            var s = Fresh(); Check(Request(s, 0), "hint failed"); Check(s.Game.HintsRemaining == 2, "charge");
            Check(Request(s, 0) && s.Game.HintsRemaining == 2, "re-review charged");
            int cell = s.CurrentHint.Cell; s.RevealHint(); s.ApplyHint(); Check(s.Game.IsCorrect(cell), "not applied");
            Check(s.CurrentHint == null && s.Game.HintsRemaining == 2, "hint not consumed");
            s.Undo(); Check(s.Game.ValueAt(cell) == 0 && s.Game.HintsRemaining == 2, "undo refunded hint");
        });
        Run("wrong board and pause never consume hint", () => {
            var s = Fresh(); s.SelectCell(0); s.SelectDigit(3); Check(!Request(s, 0) && s.Game.HintsRemaining == 3, "red board hint");
            s.Erase(); s.Pause(); Check(!Request(s, 0), "paused hint");
        });
        Run("third mistake freezes play and time across restore", () => {
            var s = Fresh(); s.SelectCell(0); s.SelectDigit(3); s.SelectDigit(4); s.Tick(8); s.SelectDigit(6); s.Tick(5);
            Check(s.Game.Phase == SudokuPhase.Lost && s.ElapsedSeconds == 8, "third error");
            Check(!s.Undo().Changed && !s.Erase().Changed, "terminal editable");
            var restored = new SudokuSession(s.Export(), true); restored.Resume(); Check(restored.Game.Phase == SudokuPhase.Lost, "revived loss");
        });
        Run("win records completion once and freezes time", () => {
            var s = Fresh(); for (int i = 0; i < 4; i++) { s.SelectCell(i); s.SelectDigit(Puzzle().Solution[i]); }
            s.Tick(20); var a = s.Export(); var b = new SudokuSession(a, true).Export();
            Check(s.Game.Phase == SudokuPhase.Won && a.CompletedIds.Length == 1 && b.CompletedIds.Length == 1 && b.UnassistedIds.Length == 1, "win record");
            Check(s.ElapsedSeconds == 0, "terminal clock");
        });
        Run("three hint limit persists and used hint remains reviewable", () => {
            var s = Fresh(); for (int i = 0; i < 3; i++) { Check(Request(s, -1), "hint unavailable"); if (i < 2) { s.RevealHint(); s.ApplyHint(); } }
            var restored = new SudokuSession(s.Export(), true); restored.Resume();
            Check(restored.Game.HintsRemaining == 0 && Request(restored, -1), "cached last hint lost");
            restored.RevealHint(); restored.ApplyHint(); Check(!Request(restored, -1), "fourth hint allowed");
        });
        Run("late async hints cannot charge paused changed or replaced sessions", () => {
            var s = Fresh(); var request = s.BeginHint(0); var hint = SudokuHintEngine.Find(request.Puzzle, request.Values, 0);
            s.Pause(); s.Resume(); Check(!s.AcceptHint(request, hint) && s.Game.HintsRemaining == 3, "cancelled result accepted");
            request = s.BeginHint(0); s.SelectCell(1); s.SelectDigit(3); Check(!s.AcceptHint(request, hint), "old revision accepted");
            var other = Fresh(); request = s.BeginHint(0); Check(!other.AcceptHint(request, hint), "different session accepted");
            request = other.BeginHint(0); Check(other.AcceptHint(request, hint), "current result rejected");
            Check(!other.AcceptHint(request, hint) && other.Game.HintsRemaining == 2, "duplicate charge");
        });
        Console.WriteLine("Sudoku session checks: " + passed + " passed, 0 failed.");
    }
}
