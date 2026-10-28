using System;
using XanaduYonder.Core.Sudoku;

internal static class Program
{
    private static int passed, failed;

    private static int Main()
    {
        Run("fresh game derives givens, counts and phase", Fresh);
        Run("given cells and invalid commands never change state", ReadOnly);
        Run("formal mistakes use the answer even without visible conflicts", Mistakes);
        Run("third mistake immediately freezes every mutation and survives restore", ThirdMistake);
        Run("candidate addition uses local rules rather than solution", Notes);
        Run("red formal digits reject candidate additions with unique conflicts", NoteConflicts);
        Run("existing candidate can be deleted despite a new red conflict", RemoveConflictingNote);
        Run("correct input clears only the matching peer candidates atomically", CorrectCleanup);
        Run("wrong input clears target candidates but preserves peer candidates", IncorrectCleanup);
        Run("erase never recreates peer notes but undo restores complete transaction", EraseAndUndo);
        Run("undo does not refund mistakes or paid hints", CumulativeCounters);
        Run("hint application checks revision, answer, paid use and terminal phase", Hints);
        Run("completed digits count only given and correct player values", DigitCounts);
        Run("only all-correct completion wins and rejects every later mutation", Win);
        Run("a full board containing a red value is still incomplete", FullIncorrectBoard);
        Run("snapshots defensively own puzzle, board and all history arrays", Copies);
        Run("snapshot round trip retains cumulative counters and atomic undo", RoundTrip);
        Run("strict snapshot validation rejects corrupt shapes and counters", InvalidSnapshots);
        Run("strict history validation rejects injected moves and terminal ancestors", InvalidHistory);
        Run("ordinary input supports editable correct and incorrect player values", OrdinaryControls);
        Run("quick input chooses digit before writing and never covers formal values", QuickControls);
        Run("quick pencil toggles candidates without modifying formal values", QuickPencil);
        Run("finishing a digit clears quick selection but zero remaining can highlight", ExhaustedDigit);
        Run("control normalization rejects invalid selections and modes stay independent", NormalizeControls);
        Run("terminal controls cannot change board or selected quick source", TerminalControls);
        Run("revision exhaustion does not wrap or mutate the board", RevisionBoundary);
        Run("deterministic mixed actions always produce restorable snapshots", MixedSequences);
        Console.WriteLine("Sudoku checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static SudokuPuzzle Puzzle()
    {
        return new SudokuPuzzle {
            Id = "core-test-001", Difficulty = 0, Level = 1,
            Givens = Digits("530070000600195000098000060800060003400803001700020006060000280000419005000080079"),
            Solution = Digits("534678912672195348198342567859761423426853791713924856961537284287419635345286179")
        };
    }

    private static int[] Digits(string s) { var a = new int[s.Length]; for (int i = 0; i < a.Length; i++) a[i] = s[i] - '0'; return a; }
    private static int Bit(int digit) { return 1 << (digit - 1); }

    private static void Fresh()
    {
        var g = new SudokuGame(Puzzle());
        Assert(g.Phase == SudokuPhase.Playing && g.RemainingCount == 51, "51 missing cells");
        Assert(g.Errors == 0 && g.HintsRemaining == 3 && g.Revision == 0 && !g.CanUndo, "fresh cumulative state");
        Assert(g.IsGiven(0) && g.IsCorrect(0) && g.ValueAt(0) == 5 && g.NotesAt(0) == 0, "given view");
        Assert(!g.IsGiven(2) && !g.IsCorrect(2) && g.ValueAt(2) == 0, "empty view");
        Valid(g.GetSnapshot());
        Throws<ArgumentNullException>(delegate { new SudokuGame((SudokuPuzzle)null); });
        Throws<ArgumentNullException>(delegate { new SudokuGame((SudokuSnapshot)null); });
    }

    private static void ReadOnly()
    {
        var g = new SudokuGame(Puzzle()); var before = g.GetSnapshot();
        Assert(!g.EnterValue(0, 1).Changed && !g.ToggleNote(0, 1).Changed && !g.Erase(0).Changed, "givens read only");
        foreach (int cell in new[] { -1, 81, int.MaxValue })
            Assert(!g.EnterValue(cell, 1).Changed && !g.ToggleNote(cell, 1).Changed && !g.Erase(cell).Changed, "invalid cell ignored");
        foreach (int digit in new[] { -1, 0, 10, int.MaxValue })
            Assert(!g.EnterValue(2, digit).Changed && !g.ToggleNote(2, digit).Changed, "invalid digit ignored");
        Assert(!g.Erase(2).Changed && !g.Undo().Changed, "empty operations ignored");
        Same(before, g.GetSnapshot(), "invalid commands atomic");
    }

    private static void Mistakes()
    {
        var g = new SudokuGame(Puzzle());
        var move = g.EnterValue(2, 1);
        Assert(move.Changed && move.Mistake && move.Conflicts.Length == 0 && move.Cell == 2 && move.Digit == 1, "wrong without duplicate");
        Assert(g.Errors == 1 && !g.IsCorrect(2) && g.RemainingCount == 51, "red value not complete");
        int revision = g.Revision;
        Assert(!g.EnterValue(2, 1).Changed && g.Errors == 1 && g.Revision == revision, "same red value no new action");
        Assert(g.EnterValue(2, 2).Mistake && g.Errors == 2, "replace red with distinct red counts");
        Assert(g.EnterValue(2, 4).Changed && g.IsCorrect(2) && g.Errors == 2 && g.RemainingCount == 50, "correcting retains mistakes");
        Valid(g.GetSnapshot());
    }

    private static void ThirdMistake()
    {
        var g = new SudokuGame(Puzzle());
        g.EnterValue(2, 1); g.Erase(2); g.EnterValue(2, 2); g.Undo();
        var final = g.EnterValue(2, 3);
        Assert(final.Mistake && final.Phase == SudokuPhase.Lost && g.Errors == 3 && !g.CanUndo, "third wrong loses immediately");
        Frozen(g);
        var restored = new SudokuGame(g.GetSnapshot());
        Assert(restored.Phase == SudokuPhase.Lost && restored.Errors == 3, "lost survives load"); Frozen(restored);
    }

    private static void Notes()
    {
        var g = new SudokuGame(Puzzle());
        var added = g.ToggleNote(2, 1);
        Assert(added.Changed && !added.Mistake && !added.RejectedNote && g.NotesAt(2) == Bit(1), "non-answer legal note");
        g.ToggleNote(2, 2); Assert(g.NotesAt(2) == (Bit(1) | Bit(2)), "fixed bit positions");
        g.ToggleNote(2, 1); Assert(g.NotesAt(2) == Bit(2), "toggle only selected note");
        Assert(g.ValueAt(2) == 0 && g.Errors == 0, "notes never formal or mistake");
    }

    private static void NoteConflicts()
    {
        var g = new SudokuGame(Puzzle());
        var before = g.GetSnapshot(); var rejected = g.ToggleNote(2, 5);
        Assert(!rejected.Changed && rejected.RejectedNote && !rejected.Mistake, "reject conflicting note");
        Equal(new[] { 0 }, rejected.Conflicts, "row-box duplicate conflict listed once");
        Same(before, g.GetSnapshot(), "rejection has no history");
        g.EnterValue(2, 1); int revision = g.Revision;
        rejected = g.ToggleNote(6, 1);
        Assert(!rejected.Changed && rejected.RejectedNote && Array.IndexOf(rejected.Conflicts, 2) >= 0, "red number is conflict");
        Assert(g.Errors == 1 && g.Revision == revision, "red conflict adds no mistakes");
    }

    private static void RemoveConflictingNote()
    {
        var g = new SudokuGame(Puzzle()); g.ToggleNote(6, 1); g.EnterValue(2, 1);
        Assert(g.NotesAt(6) == Bit(1), "wrong does not clean notes");
        var remove = g.ToggleNote(6, 1);
        Assert(remove.Changed && !remove.RejectedNote && g.NotesAt(6) == 0 && g.Errors == 1, "delete before conflict test");
    }

    private static void CorrectCleanup()
    {
        var g = new SudokuGame(Puzzle());
        g.ToggleNote(2, 2); g.ToggleNote(6, 1); g.ToggleNote(6, 4); g.ToggleNote(11, 4); g.ToggleNote(32, 4);
        Assert(g.NotesAt(32) == Bit(4), "non-peer fixture valid");
        var before = g.GetSnapshot(); var move = g.EnterValue(2, 4);
        Assert(move.Changed && !move.Mistake && g.NotesAt(2) == 0, "target candidates cleared");
        Assert(g.NotesAt(6) == Bit(1) && g.NotesAt(11) == 0 && g.NotesAt(32) == Bit(4), "only peer same digit removed");
        int revision = g.Revision; Assert(g.Undo().Changed, "undo commit");
        Equal(before.Values, g.GetSnapshot().Values, "undo target formal"); Equal(before.Notes, g.GetSnapshot().Notes, "undo all peer candidates");
        Assert(g.Revision == revision + 1, "undo creates new revision"); Valid(g.GetSnapshot());
    }

    private static void IncorrectCleanup()
    {
        var g = new SudokuGame(Puzzle()); g.ToggleNote(2, 2); g.ToggleNote(6, 1);
        var move = g.EnterValue(2, 1);
        Assert(move.Mistake && g.NotesAt(2) == 0 && g.NotesAt(6) == Bit(1), "wrong clears only target");
        g.Undo(); Assert(g.NotesAt(2) == Bit(2) && g.Errors == 1, "undo wrong recovers notes not counter");
    }

    private static void EraseAndUndo()
    {
        var g = new SudokuGame(Puzzle()); g.ToggleNote(6, 4); g.EnterValue(2, 4); g.Erase(2);
        Assert(g.ValueAt(2) == 0 && g.NotesAt(6) == 0, "erase does not restore notes");
        g.Undo(); Assert(g.ValueAt(2) == 4 && g.NotesAt(6) == 0, "undo erase restores correct formal");
        g.Undo(); Assert(g.ValueAt(2) == 0 && g.NotesAt(6) == Bit(4), "undo correct fill restores peer notes");
        g.ToggleNote(2, 1); g.ToggleNote(2, 2); g.Erase(2);
        Assert(g.NotesAt(2) == 0, "erase entire note set"); g.Undo(); Assert(g.NotesAt(2) == (Bit(1) | Bit(2)), "undo erase note set");
    }

    private static void CumulativeCounters()
    {
        var g = new SudokuGame(Puzzle()); g.EnterValue(2, 1); g.SpendHint(); g.Undo();
        Assert(g.ValueAt(2) == 0 && g.Errors == 1 && g.HintsRemaining == 2 && !g.CanUndo, "undo no refunds");
        g.EnterValue(2, 1); g.EnterValue(2, 4); g.Undo();
        Assert(g.ValueAt(2) == 1 && g.Errors == 2, "restoring red does not recount"); Valid(g.GetSnapshot());
    }

    private static void Hints()
    {
        var g = new SudokuGame(Puzzle());
        Assert(!g.ApplyHint(2, 4, 0).Changed, "unpaid cannot apply");
        Assert(g.SpendHint() && g.Revision == 0 && !g.CanUndo, "spend separate from board revision");
        Assert(!g.ApplyHint(2, 1, 0).Changed && g.Errors == 0, "incorrect hint rejected without mistake");
        g.ToggleNote(2, 1);
        Assert(!g.ApplyHint(2, 4, 0).Changed, "stale hint cannot apply");
        Assert(g.ApplyHint(2, 4, g.Revision).Changed && g.HintsRemaining == 2, "valid hint no extra charge");
        g.Undo(); Assert(g.ValueAt(2) == 0 && g.NotesAt(2) == Bit(1) && g.HintsRemaining == 2, "hint undo atomic no refund");
        Assert(g.SpendHint() && g.SpendHint() && !g.SpendHint() && g.HintsRemaining == 0, "exactly three uses"); Valid(g.GetSnapshot());
    }

    private static void DigitCounts()
    {
        var g = new SudokuGame(Puzzle()); int before = g.RemainingDigit(1);
        g.EnterValue(2, 1); Assert(g.RemainingDigit(1) == before, "wrong formal never counts");
        g.EnterValue(7, 1); Assert(g.RemainingDigit(1) == before - 1, "correct formal counts");
        g.Erase(7); Assert(g.RemainingDigit(1) == before, "erase recomputes");
        g.ToggleNote(7, 1); Assert(g.RemainingDigit(1) == before, "notes never count");
    }

    private static void Win()
    {
        var g = new SudokuGame(Puzzle()); var p = Puzzle();
        for (int cell = 0; cell < 81; cell++) if (!g.IsGiven(cell)) g.EnterValue(cell, p.Solution[cell]);
        Assert(g.Phase == SudokuPhase.Won && g.RemainingCount == 0 && !g.CanUndo, "all answers wins");
        for (int d = 1; d <= 9; d++) Assert(g.RemainingDigit(d) == 0, "all digits done");
        Valid(g.GetSnapshot()); Frozen(g); Frozen(new SudokuGame(g.GetSnapshot()));
    }

    private static void Frozen(SudokuGame g)
    {
        var s = g.GetSnapshot();
        Assert(!g.EnterValue(2, 1).Changed && !g.ToggleNote(3, 2).Changed && !g.Erase(2).Changed && !g.Undo().Changed, "terminal ignores board writes");
        Assert(!g.SpendHint() && !g.ApplyHint(2, 4, g.Revision).Changed, "terminal ignores hints"); Same(s, g.GetSnapshot(), "terminal snapshot unchanged");
    }

    private static void FullIncorrectBoard()
    {
        var p = Puzzle(); var g = new SudokuGame(p); g.EnterValue(2, 1);
        for (int cell = 0; cell < 81; cell++) if (!g.IsGiven(cell) && cell != 2) g.EnterValue(cell, p.Solution[cell]);
        Assert(g.Phase == SudokuPhase.Playing && g.RemainingCount == 1 && g.RemainingDigit(4) == 1, "full red board is not won");
        Assert(g.Errors == 1, "correct later values do not recount red"); Valid(g.GetSnapshot());
        Assert(g.EnterValue(2, 4).Changed && g.Phase == SudokuPhase.Won, "fix last red wins");
    }

    private static void Copies()
    {
        var p = Puzzle(); var g = new SudokuGame(p); p.Givens[0] = 1; p.Solution[2] = 1; p.Id = "changed";
        Assert(g.ValueAt(0) == 5 && g.Puzzle.Id == "core-test-001", "constructor owns puzzle");
        g.ToggleNote(2, 1); g.EnterValue(2, 4); var baseline = g.GetSnapshot(); var source = g.GetSnapshot();
        var restored = new SudokuGame(source); source.Values[2] = 9; source.Notes[3] = 511; source.History[0].Values[0] = 0;
        source.History[1].Notes[2] = 0; source.Puzzle.Solution[2] = 9;
        Same(baseline, restored.GetSnapshot(), "snapshot import defensive");
        var exposed = restored.Puzzle; exposed.Givens[0] = 0; exposed.Solution[2] = 9;
        Same(baseline, restored.GetSnapshot(), "puzzle access defensive");
        var copy = baseline.Copy(); copy.Values[2] = 9; copy.History[0].Notes[2] = 511; copy.Puzzle.Givens[0] = 0;
        Assert(baseline.Values[2] == 4 && baseline.History[0].Notes[2] == 0 && baseline.Puzzle.Givens[0] == 5, "Copy fully deep");
    }

    private static void RoundTrip()
    {
        var g = new SudokuGame(Puzzle()); g.ToggleNote(6, 1); g.ToggleNote(6, 4); g.EnterValue(2, 1); g.SpendHint(); g.EnterValue(2, 4);
        var restored = new SudokuGame(g.GetSnapshot()); Same(g.GetSnapshot(), restored.GetSnapshot(), "round trip identical");
        g.Undo(); restored.Undo(); Same(g.GetSnapshot(), restored.GetSnapshot(), "same undo continuation");
        Assert(restored.ValueAt(2) == 1 && restored.Errors == 1 && restored.HintsRemaining == 2, "red restored legally");
    }

    private static void InvalidSnapshots()
    {
        Invalid(delegate(SudokuSnapshot s) { s.Puzzle = null; });
        Invalid(delegate(SudokuSnapshot s) { s.Values = null; }); Invalid(delegate(SudokuSnapshot s) { s.Notes = null; });
        Invalid(delegate(SudokuSnapshot s) { s.Values = new int[80]; }); Invalid(delegate(SudokuSnapshot s) { s.Notes = new int[82]; });
        Invalid(delegate(SudokuSnapshot s) { s.Values[0] = 0; }); Invalid(delegate(SudokuSnapshot s) { s.Values[2] = 10; });
        Invalid(delegate(SudokuSnapshot s) { s.Values[2] = -1; }); Invalid(delegate(SudokuSnapshot s) { s.Notes[2] = 512; });
        Invalid(delegate(SudokuSnapshot s) { s.Notes[2] = -1; }); Invalid(delegate(SudokuSnapshot s) { s.Notes[0] = 1; });
        Invalid(delegate(SudokuSnapshot s) { s.Mistakes = -1; }); Invalid(delegate(SudokuSnapshot s) { s.Mistakes = 4; });
        Invalid(delegate(SudokuSnapshot s) { s.HintsUsed = -1; }); Invalid(delegate(SudokuSnapshot s) { s.HintsUsed = 4; });
        Invalid(delegate(SudokuSnapshot s) { s.Revision = -1; }); Invalid(delegate(SudokuSnapshot s) { s.History = null; });
        Invalid(delegate(SudokuSnapshot s) { s.Values[2] = 1; });
        var g = new SudokuGame(Puzzle()); g.EnterValue(2, 1); var red = g.GetSnapshot(); Valid(red);
        red.Mistakes = 0; Invalid(red);
        var fresh = new SudokuGame(Puzzle()).GetSnapshot(); fresh.Mistakes = 3; Invalid(fresh);
    }

    private static void InvalidHistory()
    {
        var g = new SudokuGame(Puzzle()); g.ToggleNote(6, 4); g.EnterValue(2, 4);
        var s = g.GetSnapshot(); s.History[0] = null; Invalid(s);
        s = g.GetSnapshot(); s.History[0].Values = new int[80]; Invalid(s);
        s = g.GetSnapshot(); s.History[0].Values[0] = 0; Invalid(s);
        s = g.GetSnapshot(); s.History[0].Notes[2] = Bit(5); Invalid(s);
        s = g.GetSnapshot(); s.Notes[32] = Bit(4); Invalid(s);
        s = g.GetSnapshot(); s.History[1].Values[2] = 1; Invalid(s);
        s = g.GetSnapshot(); s.Revision = 0; Invalid(s);
        var done = new SudokuGame(Puzzle()); for (int c = 0; c < 81; c++) if (!done.IsGiven(c)) done.EnterValue(c, Puzzle().Solution[c]);
        s = done.GetSnapshot(); s.Mistakes = 3; Invalid(s);
        s = done.GetSnapshot(); s.History[s.History.Length - 1].Values = (int[])s.Values.Clone(); Invalid(s);
    }

    private static void OrdinaryControls()
    {
        var g = new SudokuGame(Puzzle()); var c = new SudokuControls();
        Assert(c.SelectedCell == -1 && c.QuickDigit == 0 && !c.Quick && !c.Pencil, "default input");
        Assert(!c.SelectDigit(g, 4).Changed && !c.SelectCell(g, 2).Changed, "select first");
        Assert(c.SelectDigit(g, 4).Changed && g.IsCorrect(2), "ordinary writes selected");
        Assert(c.SelectDigit(g, 1).Mistake && g.ValueAt(2) == 1, "ordinary replaces correct");
        Assert(c.SelectDigit(g, 4).Changed && g.ValueAt(2) == 4, "ordinary fixes wrong");
        c.TogglePencil(); Assert(!c.SelectDigit(g, 1).Changed, "pencil cannot cover formal");
        c.SelectCell(g, 6); Assert(c.SelectDigit(g, 1).Changed && g.NotesAt(6) == Bit(1), "ordinary notes selected");
    }

    private static void QuickControls()
    {
        var g = new SudokuGame(Puzzle()); var c = new SudokuControls(); c.SetQuick(true);
        Assert(!c.SelectCell(g, 2).Changed && c.SelectedCell == 2, "no quick digit only selection");
        Assert(!c.SelectDigit(g, 4).Changed && c.QuickDigit == 4, "quick digit picks without writing");
        Assert(c.SelectCell(g, 2).Changed && g.ValueAt(2) == 4, "quick empty writes");
        c.SelectDigit(g, 1); Assert(!c.SelectCell(g, 2).Changed && c.QuickDigit == 4, "correct is quick digit source");
        Assert(!c.SelectCell(g, 0).Changed && c.QuickDigit == 5, "given is quick digit source");
        c.SelectDigit(g, 1); c.SelectCell(g, 3); Assert(g.ValueAt(3) == 1 && g.Errors == 1, "quick formal errors count");
        c.SelectDigit(g, 2); Assert(!c.SelectCell(g, 3).Changed && c.QuickDigit == 2 && c.SelectedCell == 3, "red selects no overwrite or digit replacement");
        c.SetQuick(false); Assert(c.QuickDigit == 0, "leaving quick clears digit");
    }

    private static void QuickPencil()
    {
        var g = new SudokuGame(Puzzle()); var c = new SudokuControls(); c.SetQuick(true); c.TogglePencil();
        c.SelectDigit(g, 1); Assert(c.SelectCell(g, 2).Changed && g.NotesAt(2) == Bit(1), "quick pencil adds");
        Assert(c.SelectCell(g, 2).Changed && g.NotesAt(2) == 0, "quick pencil toggles");
        c.SelectCell(g, 0); Assert(c.QuickDigit == 5 && g.ValueAt(0) == 5 && g.NotesAt(0) == 0, "quick pencil given only picks");
        Assert(!c.SelectCell(g, 2).Changed && g.Errors == 0, "quick pencil conflict not formal error");
    }

    private static void ExhaustedDigit()
    {
        var g = new SudokuGame(Puzzle()); var p = Puzzle(); int last = -1;
        for (int cell = 0; cell < 81; cell++) if (p.Solution[cell] == 1 && !g.IsGiven(cell)) last = cell;
        for (int cell = 0; cell < 81; cell++) if (p.Solution[cell] == 1 && !g.IsGiven(cell) && cell != last) g.EnterValue(cell, 1);
        var c = new SudokuControls(); c.SetQuick(true); c.SelectDigit(g, 1); Assert(c.SelectCell(g, last).Changed && c.QuickDigit == 0, "last correct clears quick");
        Assert(!c.SelectDigit(g, 1).Changed && c.QuickDigit == 1, "completed digit selectable for highlight");
        Assert(!c.SelectCell(g, 2).Changed && g.Errors == 0 && g.ValueAt(2) == 0, "completed digit cannot write");
        c.SetQuick(false); c.SelectCell(g, 2); Assert(!c.SelectDigit(g, 1).Changed, "ordinary completed digit cannot write");
    }

    private static void NormalizeControls()
    {
        var g = new SudokuGame(Puzzle()); var c = new SudokuControls { SelectedCell = 100, QuickDigit = 12, Quick = true, Pencil = true };
        c.Normalize(g); Assert(c.SelectedCell == -1 && c.QuickDigit == 0 && c.Quick && c.Pencil, "invalid selections cleared");
        c.SetQuick(false); Assert(c.Pencil && !c.Quick, "two modes independent"); c.TogglePencil(); Assert(!c.Pencil, "pencil toggles");
        c.SelectCell(g, 2); c.SelectCell(g, -1); Assert(c.SelectedCell == 2, "invalid command keeps selection");
        c.SelectDigit(g, 10); Assert(g.Revision == 0, "invalid digit ignored");
        c.QuickDigit = 4; c.Normalize(g); Assert(c.QuickDigit == 0, "normal mode has no active quick digit");
    }

    private static void TerminalControls()
    {
        var g = new SudokuGame(Puzzle()); g.EnterValue(2, 1); g.EnterValue(2, 2); g.EnterValue(2, 3);
        var c = new SudokuControls { SelectedCell = 2, QuickDigit = 4, Quick = true };
        Assert(!c.SelectCell(g, 0).Changed && !c.SelectDigit(g, 5).Changed, "terminal input rejected");
        Assert(c.SelectedCell == 2 && c.QuickDigit == 4 && g.ValueAt(2) == 3, "queued input cannot change terminal selection");
    }

    private static void RevisionBoundary()
    {
        var g = new SudokuGame(Puzzle()); g.ToggleNote(2, 1); var s = g.GetSnapshot(); s.Revision = int.MaxValue;
        Valid(s); g = new SudokuGame(s); var before = g.GetSnapshot();
        Assert(!g.EnterValue(2, 4).Changed && !g.ToggleNote(2, 1).Changed && !g.Erase(2).Changed && !g.Undo().Changed, "revision limit rejects writes");
        Same(before, g.GetSnapshot(), "revision never wraps");
    }

    private static void MixedSequences()
    {
        var random = new Random(260526);
        for (int run = 0; run < 16; run++)
        {
            var g = new SudokuGame(Puzzle());
            for (int turn = 0; turn < 70; turn++)
            {
                int cell = random.Next(81), digit = random.Next(1, 10);
                switch (random.Next(7))
                {
                    case 0: g.EnterValue(cell, Puzzle().Solution[cell]); break;
                    case 1: g.ToggleNote(cell, digit); break;
                    case 2: g.Erase(cell); break;
                    case 3: g.Undo(); break;
                    case 4: g.SpendHint(); break;
                    case 5: g.EnterValue(cell, digit); break;
                    default: g.ApplyHint(cell, Puzzle().Solution[cell], g.Revision); break;
                }
                var snapshot = g.GetSnapshot(); Valid(snapshot); var restored = new SudokuGame(snapshot); Same(snapshot, restored.GetSnapshot(), "mixed continuation");
                g = restored;
            }
        }
    }

    private static void Invalid(Action<SudokuSnapshot> mutate) { var s = new SudokuGame(Puzzle()).GetSnapshot(); mutate(s); Invalid(s); }
    private static void Invalid(SudokuSnapshot s) { string e; Assert(!s.TryValidate(out e) && !string.IsNullOrEmpty(e), "invalid snapshot rejected with reason"); Throws<ArgumentException>(delegate { new SudokuGame(s); }); }
    private static void Valid(SudokuSnapshot s) { string e; Assert(s.TryValidate(out e), e ?? "snapshot valid"); }
    private static void Same(SudokuSnapshot a, SudokuSnapshot b, string label)
    {
        Assert(a.Puzzle.Id == b.Puzzle.Id && a.Puzzle.Difficulty == b.Puzzle.Difficulty && a.Puzzle.Level == b.Puzzle.Level, label + " puzzle");
        Equal(a.Puzzle.Givens, b.Puzzle.Givens, label); Equal(a.Puzzle.Solution, b.Puzzle.Solution, label); Equal(a.Values, b.Values, label); Equal(a.Notes, b.Notes, label);
        Assert(a.Mistakes == b.Mistakes && a.HintsUsed == b.HintsUsed && a.Revision == b.Revision && a.History.Length == b.History.Length, label + " counters");
        for (int i = 0; i < a.History.Length; i++) { Equal(a.History[i].Values, b.History[i].Values, label); Equal(a.History[i].Notes, b.History[i].Notes, label); }
    }
    private static void Equal(int[] a, int[] b, string label) { Assert(a.Length == b.Length, label + " length"); for (int i = 0; i < a.Length; i++) Assert(a[i] == b[i], label + " at " + i); }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    private static void Run(string name, Action test) { try { test(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + e.Message); } }
}
