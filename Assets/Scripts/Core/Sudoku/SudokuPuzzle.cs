using System;

namespace XanaduYonder.Core.Sudoku
{
    [Serializable]
    public sealed class SudokuPuzzle
    {
        public string Id;
        public int Difficulty;
        public int Level;
        public int[] Givens;
        public int[] Solution;

        public SudokuPuzzle Copy()
        {
            return new SudokuPuzzle { Id = Id, Difficulty = Difficulty, Level = Level,
                Givens = Givens == null ? null : (int[])Givens.Clone(),
                Solution = Solution == null ? null : (int[])Solution.Clone() };
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(Id) || Id.Length > 100) error = "题目 ID 无效";
            else if (Difficulty < 0 || Difficulty > 3 || Level < 1 || Level > 500) error = "难度或关卡号无效";
            else if (Givens == null || Solution == null || Givens.Length != 81 || Solution.Length != 81) error = "题面和答案必须各有 81 格";
            if (error != null) return false;
            for (int i = 0; i < 81; i++)
            {
                if (Givens[i] < 0 || Givens[i] > 9 || Solution[i] < 1 || Solution[i] > 9 || (Givens[i] != 0 && Givens[i] != Solution[i]))
                { error = "题面或答案数字不合法"; return false; }
            }
            // Runtime checks are bounded: uniqueness is verified once by the offline importer.
            for (int unit = 0; unit < 27; unit++)
            {
                int mask = 0;
                for (int offset = 0; offset < 9; offset++)
                {
                    int cell = unit < 9 ? unit * 9 + offset : unit < 18 ? offset * 9 + unit - 9 :
                        (unit - 18) / 3 * 27 + (unit - 18) % 3 * 3 + offset / 3 * 9 + offset % 3;
                    mask |= 1 << Solution[cell];
                }
                if (mask != 1022) { error = "答案不满足标准数独的行、列、宫约束"; return false; }
            }
            return true;
        }
    }
}
