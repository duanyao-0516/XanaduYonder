using System;
using System.Collections.Generic;
using XanaduYonder.Core.Sudoku;

// Deliberately independent of the engine's unit tables, peer helpers and subset search.
internal static class ProofChecker
{
    internal static int[] Candidates(int[] board)
    {
        var result = new int[81];
        for (int cell = 0; cell < 81; cell++)
        {
            if (board[cell] != 0) continue;
            for (int digit = 1; digit <= 9; digit++)
            {
                bool blocked = false;
                for (int other = 0; other < 81; other++)
                    if (board[other] == digit && (cell / 9 == other / 9 || cell % 9 == other % 9 || cell / 27 == other / 27 && cell % 9 / 3 == other % 9 / 3)) { blocked = true; break; }
                if (!blocked) result[cell] |= 1 << digit;
            }
        }
        return result;
    }

    internal static void CheckHint(int[] board, SudokuHint hint)
    {
        Need(hint != null && !hint.IsDirect && hint.Steps != null && hint.Steps.Length > 0, "Expected logical proof");
        var candidates = Candidates(board);
        foreach (var step in hint.Steps) Apply(candidates, step);
        var final = hint.Steps[hint.Steps.Length - 1];
        Need(final.Cell == hint.Cell && final.Digit == hint.Digit && final.RemovedCells.Length == 0, "Final proof must justify the offered placement");
    }

    internal static void Apply(int[] candidates, SudokuReasoningStep step)
    {
        Need(candidates != null && candidates.Length == 81 && step != null && step.PatternCells != null && step.RemovedCells != null && step.RemovedMasks != null, "Proof shape");
        var pattern = new HashSet<int>(step.PatternCells);
        Need(pattern.Count == step.PatternCells.Length && pattern.Count > 0 && step.RemovedCells.Length == step.RemovedMasks.Length, "Unique pattern cells");
        foreach (int cell in pattern) Need(cell >= 0 && cell < 81, "Pattern cell range");
        int digits = step.DigitMask;
        Need(digits > 0 && (digits & ~1022) == 0, "Digit mask range");
        var allowed = new int[81];
        if (step.Technique == "裸单")
        {
            Need(step.Cell >= 0 && step.Cell < 81 && step.Digit >= 1 && step.Digit <= 9 && candidates[step.Cell] == (1 << step.Digit), "Naked single must have exactly one candidate");
            Need(pattern.Count == 1 && pattern.Contains(step.Cell) && digits == (1 << step.Digit) && step.RemovedCells.Length == 0, "Naked single proof");
            return;
        }
        if (step.Technique == "隐藏单")
        {
            Need(pattern.Count == 1 && pattern.Contains(step.Cell) && step.Digit >= 1 && step.Digit <= 9 && digits == (1 << step.Digit), "Hidden single proof");
            var positions = Positions(candidates, step.SourceUnit, digits);
            Need(positions.SetEquals(pattern) && step.RemovedCells.Length == 0, "Hidden single is the only position in its unit");
            return;
        }
        if (step.Technique == "区块排除")
        {
            Need(Count(digits) == 1 && ((step.SourceUnit < 18) != (step.TargetUnit < 18)), "Locked candidate crosses a box and a line");
            var positions = Positions(candidates, step.SourceUnit, digits);
            Need(positions.Count >= 2 && positions.SetEquals(pattern), "Locked proof includes every source position");
            foreach (int cell in pattern) Need(InUnit(cell, step.TargetUnit), "Source positions confined to target");
            for (int cell = 0; cell < 81; cell++) if (InUnit(cell, step.TargetUnit) && !InUnit(cell, step.SourceUnit)) allowed[cell] = digits;
        }
        else if (step.Technique == "裸数对" || step.Technique == "裸三数组" || step.Technique == "隐藏数对" || step.Technique == "隐藏三数组")
        {
            bool hidden = step.Technique.StartsWith("隐藏", StringComparison.Ordinal);
            int size = step.Technique.EndsWith("数对", StringComparison.Ordinal) ? 2 : 3;
            Need(pattern.Count == size && Count(digits) == size, "Subset has n cells and n digits");
            int union = 0;
            foreach (int cell in pattern)
            {
                Need(InUnit(cell, step.SourceUnit) && candidates[cell] != 0, "Subset cells are empty in one unit");
                union |= candidates[cell];
                if (!hidden) Need((candidates[cell] & ~digits) == 0, "Naked subset contains no outside digit");
            }
            if (hidden)
            {
                var positions = new HashSet<int>();
                for (int digit = 1; digit <= 9; digit++) if ((digits & (1 << digit)) != 0)
                {
                    var single = Positions(candidates, step.SourceUnit, 1 << digit);
                    Need(single.Count > 0, "Each hidden digit appears"); positions.UnionWith(single);
                }
                Need(positions.SetEquals(pattern), "Hidden digits have no positions outside subset");
                foreach (int cell in pattern) allowed[cell] = 1022 & ~digits;
            }
            else
            {
                Need(union == digits, "Naked subset digit union");
                for (int cell = 0; cell < 81; cell++) if (InUnit(cell, step.SourceUnit) && !pattern.Contains(cell)) allowed[cell] = digits;
            }
        }
        else if (step.Technique == "X-Wing")
        {
            Need(Count(digits) == 1 && step.SourceUnit >= 0 && step.SourceUnit < 18 && step.TargetUnit >= 0 && step.TargetUnit < 18 && step.SourceUnit != step.TargetUnit && step.SourceUnit / 9 == step.TargetUnit / 9, "X-Wing has two distinct parallel lines");
            var first = Positions(candidates, step.SourceUnit, digits); var second = Positions(candidates, step.TargetUnit, digits);
            Need(first.Count == 2 && second.Count == 2, "Each X-Wing line has exactly two positions");
            var corners = new HashSet<int>(first); corners.UnionWith(second); Need(corners.SetEquals(pattern), "X-Wing corners");
            var cross1 = new HashSet<int>(); var cross2 = new HashSet<int>();
            foreach (int cell in first) cross1.Add(step.SourceUnit < 9 ? cell % 9 : cell / 9);
            foreach (int cell in second) cross2.Add(step.SourceUnit < 9 ? cell % 9 : cell / 9);
            Need(cross1.SetEquals(cross2), "X-Wing cross-lines align");
            for (int cell = 0; cell < 81; cell++) if (!InUnit(cell, step.SourceUnit) && !InUnit(cell, step.TargetUnit) && cross1.Contains(step.SourceUnit < 9 ? cell % 9 : cell / 9)) allowed[cell] = digits;
        }
        else throw new Exception("Unsupported proof technique: " + step.Technique);
        Need(step.RemovedCells.Length > 0, "Elimination must remove a candidate");
        var seen = new HashSet<int>();
        for (int i = 0; i < step.RemovedCells.Length; i++)
        {
            int cell = step.RemovedCells[i], mask = step.RemovedMasks[i];
            Need(cell >= 0 && cell < 81 && seen.Add(cell) && mask != 0 && (mask & ~allowed[cell]) == 0 && (mask & candidates[cell]) == mask, "Removed candidates must follow from the pattern");
            Need((candidates[cell] & ~mask) != 0, "Elimination cannot empty a cell");
            candidates[cell] &= ~mask;
        }
    }

    private static HashSet<int> Positions(int[] candidates, int unit, int digits)
    {
        Need(unit >= 0 && unit < 27, "Unit range");
        var cells = new HashSet<int>();
        for (int cell = 0; cell < 81; cell++) if (InUnit(cell, unit) && (candidates[cell] & digits) != 0) cells.Add(cell);
        return cells;
    }
    private static bool InUnit(int cell, int unit) { Need(unit >= 0 && unit < 27, "Unit range"); return unit < 9 ? cell / 9 == unit : unit < 18 ? cell % 9 == unit - 9 : cell / 27 * 3 + cell % 9 / 3 == unit - 18; }
    private static int Count(int bits) { int n = 0; for (int d = 1; d <= 9; d++) if ((bits & (1 << d)) != 0) n++; return n; }
    private static void Need(bool condition, string message) { if (!condition) throw new Exception("Invalid Sudoku proof: " + message); }
}
