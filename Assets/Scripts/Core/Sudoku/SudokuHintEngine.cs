using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace XanaduYonder.Core.Sudoku
{
    /// <summary>Machine-checkable proof. Units 0..8 are rows, 9..17 columns, 18..26 boxes.</summary>
    [Serializable]
    public sealed class SudokuReasoningStep
    {
        public string Technique;
        public int SourceUnit = -1;
        public int TargetUnit = -1;
        public int[] PatternCells = new int[0];
        public int DigitMask;
        public int[] RemovedCells = new int[0];
        public int[] RemovedMasks = new int[0];
        public int Cell = -1;
        public int Digit;
        public SudokuReasoningStep Copy()
        {
            return new SudokuReasoningStep { Technique = Technique, SourceUnit = SourceUnit, TargetUnit = TargetUnit,
                PatternCells = (int[])PatternCells.Clone(), DigitMask = DigitMask, RemovedCells = (int[])RemovedCells.Clone(),
                RemovedMasks = (int[])RemovedMasks.Clone(), Cell = Cell, Digit = Digit };
        }
    }

    [Serializable]
    public sealed class SudokuHint
    {
        public int Cell;
        public int Digit;
        public string Technique;
        public string Explanation;
        public int[] Evidence;
        public bool IsDirect;
        public int Difficulty;
        public SudokuReasoningStep[] Steps = new SudokuReasoningStep[0];
        public SudokuHint Copy()
        {
            var steps = new SudokuReasoningStep[Steps == null ? 0 : Steps.Length];
            for (int i = 0; i < steps.Length; i++) steps[i] = Steps[i].Copy();
            return new SudokuHint { Steps = steps, Cell = Cell, Digit = Digit, Technique = Technique, Explanation = Explanation,
                Evidence = Evidence == null ? null : (int[])Evidence.Clone(), IsDirect = IsDirect, Difficulty = Difficulty };
        }
    }

    /// <summary>Pure standard Sudoku reasoning. Pencil notes never enter this engine.</summary>
    public static class SudokuHintEngine
    {
        private static readonly int[][] Units = BuildUnits();

        public static SudokuHint Find(SudokuPuzzle puzzle, int[] values, int preferredCell, CancellationToken cancellation, int budgetMilliseconds)
        {
            var budget = new Budget(cancellation, budgetMilliseconds);
            cancellation.ThrowIfCancellationRequested();
            try { return FindCore(puzzle, values, preferredCell, budget.Check); }
            catch (BudgetExceededException)
            {
                cancellation.ThrowIfCancellationRequested();
                return Direct(puzzle, values, preferredCell, "本次提示计算已达到时间预算。可选择直接揭晓推荐格；这不是逻辑推理步骤。");
            }
        }

        public static SudokuHint Find(SudokuPuzzle puzzle, int[] values, int preferredCell)
        { return Find(puzzle, values, preferredCell, CancellationToken.None, -1); }

        private static SudokuHint FindCore(SudokuPuzzle puzzle, int[] values, int preferredCell, Action checkpoint)
        {
            checkpoint();
            if (puzzle == null || puzzle.Givens == null || puzzle.Solution == null || values == null ||
                puzzle.Givens.Length != 81 || puzzle.Solution.Length != 81 || values.Length != 81) return null;
            string validationError;
            if (!puzzle.TryValidate(out validationError)) return null;
            var board = new int[81];
            var candidates = new int[81];
            int firstEmpty = -1;
            for (int i = 0; i < 81; i++)
            {
                checkpoint();
                if (values[i] < 0 || values[i] > 9 || (values[i] != 0 && values[i] != puzzle.Solution[i]) ||
                    (puzzle.Givens[i] != 0 && values[i] != puzzle.Givens[i])) return null;
                board[i] = values[i];
                if (board[i] == 0 && firstEmpty < 0) firstEmpty = i;
            }
            if (firstEmpty < 0) return null;
            for (int cell = 0; cell < 81; cell++)
            {
                checkpoint();
                if (board[cell] != 0) continue;
                int mask = 1022;
                for (int peer = 0; peer < 81; peer++) if (board[peer] != 0 && SharesUnit(cell, peer)) mask &= ~(1 << board[peer]);
                if (mask == 0) return null;
                candidates[cell] = mask;
            }
            var proofSteps = new List<SudokuReasoningStep>();
            var explanations = new List<string>();
            var techniques = new List<string>();
            var evidence = new HashSet<int>();
            int difficulty = 0;
            // Every successful elimination removes at least one of at most 729 candidates.
            for (int pass = 0; pass < 730; pass++)
            {
                checkpoint();
                SudokuHint placement = FindSingle(board, candidates, preferredCell, checkpoint);
                if (placement != null)
                {
                    if (placement.Digit != puzzle.Solution[placement.Cell]) return null;
                    explanations.Add(placement.Explanation);
                    if (!techniques.Contains(placement.Technique)) techniques.Add(placement.Technique);
                    foreach (int cell in placement.Evidence) evidence.Add(cell);
                    placement.Explanation = string.Join("\n", explanations.ToArray());
                    placement.Technique = string.Join(" → ", techniques.ToArray());
                    placement.Difficulty = Math.Max(difficulty, placement.Difficulty);
                    placement.Evidence = Sorted(evidence);
                    proofSteps.AddRange(placement.Steps);
                    placement.Steps = proofSteps.ToArray();
                    checkpoint();
                    return placement;
                }
                Elimination step = Locked(candidates, checkpoint) ?? Subsets(candidates, 2, false, checkpoint) ?? Subsets(candidates, 2, true, checkpoint) ??
                    Subsets(candidates, 3, false, checkpoint) ?? Subsets(candidates, 3, true, checkpoint) ?? XWing(candidates, checkpoint);
                if (step == null) break;
                proofSteps.Add(step.Proof);
                explanations.Add(step.Explanation);
                if (!techniques.Contains(step.Technique)) techniques.Add(step.Technique);
                foreach (int cell in step.Evidence) evidence.Add(cell);
                difficulty = Math.Max(difficulty, step.Difficulty);
            }
            checkpoint();
            return Direct(puzzle, board, preferredCell, "当前支持的逻辑技巧尚不能推导出下一格。可选择直接揭晓推荐格；这不是逻辑推理步骤。");
        }

        private static SudokuHint Direct(SudokuPuzzle puzzle, int[] values, int preferredCell, string explanation)
        {
            if (puzzle == null || values == null || values.Length != 81) return null;
            string error; if (!puzzle.TryValidate(out error)) return null;
            int target = -1;
            for (int cell = 0; cell < 81; cell++)
            {
                if (values[cell] < 0 || values[cell] > 9 || (values[cell] != 0 && values[cell] != puzzle.Solution[cell]) ||
                    (puzzle.Givens[cell] != 0 && values[cell] != puzzle.Givens[cell])) return null;
                if (values[cell] == 0 && target < 0) target = cell;
            }
            if (target < 0) return null;
            if (preferredCell >= 0 && preferredCell < 81 && values[preferredCell] == 0) target = preferredCell;
            return new SudokuHint { Cell = target, Digit = puzzle.Solution[target], Technique = "直接揭晓",
                Explanation = explanation, Evidence = new[] { target }, IsDirect = true, Difficulty = 4 };
        }

        private sealed class BudgetExceededException : Exception { }
        private sealed class Budget
        {
            private readonly CancellationToken cancellation;
            private readonly long start = Stopwatch.GetTimestamp();
            private readonly int milliseconds;
            internal Budget(CancellationToken cancellation, int milliseconds) { this.cancellation = cancellation; this.milliseconds = milliseconds; }
            internal void Check()
            {
                cancellation.ThrowIfCancellationRequested();
                if (milliseconds >= 0 && (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency >= milliseconds) throw new BudgetExceededException();
            }
        }

        public static bool TrySolveLogically(SudokuPuzzle puzzle, out int difficulty, out int steps)
        {
            difficulty = 0; steps = 0;
            string error;
            if (puzzle == null || !puzzle.TryValidate(out error)) return false;
            var board = (int[])puzzle.Givens.Clone();
            for (int pass = 0; pass < 81; pass++)
            {
                bool complete = true;
                for (int i = 0; i < 81; i++) if (board[i] == 0) { complete = false; break; }
                if (complete) return true;
                var hint = Find(puzzle, board, -1);
                if (hint == null || hint.IsDirect) return false;
                board[hint.Cell] = hint.Digit;
                difficulty = Math.Max(difficulty, hint.Difficulty);
                steps++;
            }
            return false;
        }

        private static SudokuHint FindSingle(int[] board, int[] candidates, int preferred, Action checkpoint)
        {
            if (preferred >= 0 && preferred < 81 && board[preferred] == 0)
            {
                var preferredHint = NakedSingle(board, candidates, preferred) ?? HiddenSingle(candidates, preferred, checkpoint);
                if (preferredHint != null) return preferredHint;
            }
            for (int cell = 0; cell < 81; cell++) { var hint = NakedSingle(board, candidates, cell); if (hint != null) return hint; }
            for (int cell = 0; cell < 81; cell++) { var hint = HiddenSingle(candidates, cell, checkpoint); if (hint != null) return hint; }
            return null;
        }

        private static SudokuHint NakedSingle(int[] board, int[] candidates, int cell)
        {
            int mask = candidates[cell];
            if (SudokuSolver.PopCount(mask) != 1) return null;
            int digit = OnlyDigit(mask);
            var evidence = new HashSet<int> { cell };
            for (int peer = 0; peer < 81; peer++) if (board[peer] != 0 && SharesUnit(cell, peer)) evidence.Add(peer);
            return new SudokuHint { Cell = cell, Digit = digit, Technique = "裸单", Difficulty = 0,
                Steps = new[] { new SudokuReasoningStep { Technique = "裸单", Cell = cell, Digit = digit, DigitMask = mask, PatternCells = new[] { cell } } },
                Explanation = Position(cell) + " 的行、列、宫及上述排除后，只剩候选 " + digit + "，因此填入 " + digit + "。", Evidence = Sorted(evidence) };
        }

        private static SudokuHint HiddenSingle(int[] candidates, int cell, Action checkpoint)
        {
            if (candidates[cell] == 0) return null;
            for (int unit = 0; unit < 27; unit++)
            {
                checkpoint();
                if (Array.IndexOf(Units[unit], cell) < 0) continue;
                for (int digit = 1; digit <= 9; digit++)
                {
                    int bit = 1 << digit;
                    if ((candidates[cell] & bit) == 0) continue;
                    int count = 0;
                    foreach (int other in Units[unit]) if ((candidates[other] & bit) != 0) count++;
                    if (count != 1) continue;
                    return new SudokuHint { Cell = cell, Digit = digit, Technique = "隐藏单", Difficulty = 1,
                        Steps = new[] { new SudokuReasoningStep { Technique = "隐藏单", SourceUnit = unit, Cell = cell, Digit = digit, DigitMask = bit, PatternCells = new[] { cell } } },
                        Explanation = UnitName(unit) + " 中数字 " + digit + " 仅能出现在 " + Position(cell) + "，因此填入 " + digit + "。", Evidence = (int[])Units[unit].Clone() };
                }
            }
            return null;
        }

        private sealed class Elimination
        {
            internal string Technique;
            internal string Explanation;
            internal int[] Evidence;
            internal int Difficulty;
            internal SudokuReasoningStep Proof;
        }

        private static Elimination Locked(int[] candidates, Action checkpoint)
        {
            for (int source = 0; source < 27; source++)
            for (int digit = 1; digit <= 9; digit++)
            {
                checkpoint();
                int bit = 1 << digit;
                var positions = new List<int>();
                foreach (int cell in Units[source]) if ((candidates[cell] & bit) != 0) positions.Add(cell);
                if (positions.Count < 2 || positions.Count > 3) continue;
                for (int target = 0; target < 27; target++)
                {
                    if ((source < 18) == (target < 18)) continue; // box-line intersections only
                    bool allInTarget = true;
                    foreach (int cell in positions) if (Array.IndexOf(Units[target], cell) < 0) { allInTarget = false; break; }
                    if (!allInTarget) continue;
                    var removed = new List<int>();
                    foreach (int cell in Units[target]) if (Array.IndexOf(Units[source], cell) < 0 && (candidates[cell] & bit) != 0) removed.Add(cell);
                    if (removed.Count == 0) continue;
                    foreach (int cell in removed) candidates[cell] &= ~bit;
                    var proof = new HashSet<int>(positions); foreach (int cell in removed) proof.Add(cell);
                    return new Elimination { Technique = "区块排除", Difficulty = 2, Evidence = Sorted(proof),
                        Proof = Proof("区块排除", source, target, positions, bit, removed, null),
                        Explanation = UnitName(source) + " 的候选 " + digit + " 全部落在 " + UnitName(target) + "（" + Positions(positions) + "），所以 " + Positions(removed) + " 可排除 " + digit + "。" };
                }
            }
            return null;
        }

        private static Elimination Subsets(int[] candidates, int size, bool hidden, Action checkpoint)
        {
            for (int unit = 0; unit < 27; unit++)
            {
                checkpoint();
                // At most 84 triples per unit. For hidden subsets a/b/c are digit indices; otherwise cell offsets.
                for (int a = 0; a < 9; a++)
                for (int b = a + 1; b < 9; b++)
                for (int c = size == 2 ? 9 : b + 1; c < (size == 2 ? 10 : 9); c++)
                {
                    checkpoint();
                    int digits = 0, locations = 0;
                    bool valid = true;
                    if (hidden)
                    {
                        for (int selectedIndex = 0; selectedIndex < size; selectedIndex++)
                        {
                            int index = selectedIndex == 0 ? a : selectedIndex == 1 ? b : c;
                            int bit = 1 << (index + 1), found = 0;
                            digits |= bit;
                            for (int p = 0; p < 9; p++) if ((candidates[Units[unit][p]] & bit) != 0) found |= 1 << p;
                            if (found == 0) { valid = false; break; }
                            locations |= found;
                        }
                        if (!valid || SudokuSolver.PopCount(locations) != size) continue;
                    }
                    else
                    {
                        for (int selectedIndex = 0; selectedIndex < size; selectedIndex++)
                        {
                            int index = selectedIndex == 0 ? a : selectedIndex == 1 ? b : c;
                            int mask = candidates[Units[unit][index]];
                            if (mask == 0 || SudokuSolver.PopCount(mask) > size) { valid = false; break; }
                            locations |= 1 << index; digits |= mask;
                        }
                        if (!valid || SudokuSolver.PopCount(digits) != size) continue;
                    }
                    var source = new List<int>(); var removed = new List<int>(); var removalMasks = new List<int>(); var details = new List<string>();
                    for (int p = 0; p < 9; p++)
                    {
                        int cell = Units[unit][p];
                        bool selected = (locations & (1 << p)) != 0;
                        if (selected) source.Add(cell);
                        int remove = hidden ? (selected ? candidates[cell] & ~digits : 0) : (!selected ? candidates[cell] & digits : 0);
                        if (remove == 0) continue;
                        removed.Add(cell); removalMasks.Add(remove); details.Add(Position(cell) + " 的 " + Digits(remove));
                        candidates[cell] &= ~remove;
                    }
                    if (removed.Count == 0) continue;
                    var evidence = new HashSet<int>(source); foreach (int cell in removed) evidence.Add(cell);
                    string technique = (hidden ? "隐藏" : "裸") + (size == 2 ? "数对" : "三数组");
                    return new Elimination { Technique = technique, Difficulty = size == 2 ? 2 : 3, Evidence = Sorted(evidence),
                        Proof = Proof(technique, unit, -1, source, digits, removed, removalMasks),
                        Explanation = UnitName(unit) + " 中 " + Positions(source) + (hidden ? " 是数字 " : " 的候选仅限 ") + Digits(digits) +
                            (hidden ? " 的全部位置" : "，这 " + size + " 个数字占据这 " + size + " 格") + "（" + technique + "），可排除 " + string.Join("、", details.ToArray()) + "。" };
                }
            }
            return null;
        }

        private static Elimination XWing(int[] candidates, Action checkpoint)
        {
            for (int orientation = 0; orientation < 2; orientation++)
            for (int digit = 1; digit <= 9; digit++)
            for (int a = 0; a < 8; a++)
            {
                checkpoint();
                int bit = 1 << digit, pattern = 0;
                for (int p = 0; p < 9; p++) if ((candidates[At(orientation, a, p)] & bit) != 0) pattern |= 1 << p;
                if (SudokuSolver.PopCount(pattern) != 2) continue;
                for (int b = a + 1; b < 9; b++)
                {
                    int second = 0;
                    for (int p = 0; p < 9; p++) if ((candidates[At(orientation, b, p)] & bit) != 0) second |= 1 << p;
                    if (second != pattern) continue;
                    var corners = new List<int>(); var removed = new List<int>();
                    for (int p = 0; p < 9; p++)
                    {
                        if ((pattern & (1 << p)) == 0) continue;
                        corners.Add(At(orientation, a, p)); corners.Add(At(orientation, b, p));
                        for (int line = 0; line < 9; line++)
                        {
                            int cell = At(orientation, line, p);
                            if (line != a && line != b && (candidates[cell] & bit) != 0) removed.Add(cell);
                        }
                    }
                    if (removed.Count == 0) continue;
                    foreach (int cell in removed) candidates[cell] &= ~bit;
                    var evidence = new HashSet<int>(corners); foreach (int cell in removed) evidence.Add(cell);
                    return new Elimination { Technique = "X-Wing", Difficulty = 3, Evidence = Sorted(evidence),
                        Proof = Proof("X-Wing", a + orientation * 9, b + orientation * 9, corners, bit, removed, null),
                        Explanation = "数字 " + digit + " 在两" + (orientation == 0 ? "行" : "列") + "中都仅有 " + Positions(corners) +
                            " 这四个角的位置，构成 X-Wing，因此同" + (orientation == 0 ? "列" : "行") + "的 " + Positions(removed) + " 可排除 " + digit + "。" };
                }
            }
            return null;
        }

        private static SudokuReasoningStep Proof(string technique, int source, int target, List<int> pattern, int digits, List<int> removed, List<int> masks)
        {
            int[] removalMasks = masks == null ? new int[removed.Count] : masks.ToArray();
            if (masks == null) for (int i = 0; i < removalMasks.Length; i++) removalMasks[i] = digits;
            return new SudokuReasoningStep { Technique = technique, SourceUnit = source, TargetUnit = target,
                PatternCells = pattern.ToArray(), DigitMask = digits, RemovedCells = removed.ToArray(), RemovedMasks = removalMasks };
        }

        private static int At(int orientation, int line, int position) { return orientation == 0 ? line * 9 + position : position * 9 + line; }
        private static bool SharesUnit(int a, int b) { return a / 9 == b / 9 || a % 9 == b % 9 || (a / 27 == b / 27 && a % 9 / 3 == b % 9 / 3); }
        private static int OnlyDigit(int mask) { for (int digit = 1; digit <= 9; digit++) if ((mask & (1 << digit)) != 0) return digit; return 0; }
        private static string Position(int cell) { return "第 " + (cell / 9 + 1) + " 行第 " + (cell % 9 + 1) + " 列"; }
        private static string UnitName(int unit) { return "第 " + (unit % 9 + 1) + (unit < 9 ? " 行" : unit < 18 ? " 列" : " 宫"); }
        private static string Positions(List<int> cells) { var names = new string[cells.Count]; for (int i = 0; i < cells.Count; i++) names[i] = Position(cells[i]); return string.Join("、", names); }
        private static string Digits(int mask) { var result = new StringBuilder(); for (int d = 1; d <= 9; d++) if ((mask & (1 << d)) != 0) { if (result.Length > 0) result.Append('/'); result.Append(d); } return result.ToString(); }
        private static int[] Sorted(HashSet<int> cells) { var result = new int[cells.Count]; cells.CopyTo(result); Array.Sort(result); return result; }
        private static int[][] BuildUnits()
        {
            var units = new int[27][];
            for (int unit = 0; unit < 27; unit++)
            {
                units[unit] = new int[9];
                for (int i = 0; i < 9; i++) units[unit][i] = unit < 9 ? unit * 9 + i : unit < 18 ? i * 9 + unit - 9 : (unit - 18) / 3 * 27 + (unit - 18) % 3 * 3 + i / 3 * 9 + i % 3;
            }
            return units;
        }
    }
}
