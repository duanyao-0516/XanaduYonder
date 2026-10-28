using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace XanaduYonder.Core.Sudoku
{
    /// <summary>Validated offline bank. Returned puzzles are copies, so callers cannot alter the catalog.</summary>
    public sealed class SudokuCatalog
    {
        public const string Version = "sudoku-exchange-d8c8eba-v1";
        private readonly List<SudokuPuzzle> puzzles = new List<SudokuPuzzle>();
        private readonly Dictionary<int, SudokuPuzzle> bySlot = new Dictionary<int, SudokuPuzzle>();
        public IReadOnlyList<SudokuPuzzle> Puzzles
        {
            get { var copies = new List<SudokuPuzzle>(puzzles.Count); foreach (var puzzle in puzzles) copies.Add(puzzle.Copy()); return copies.AsReadOnly(); }
        }

        public static SudokuCatalog Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException("数独题库为空");
            string[] lines = text.Replace("\r", "").TrimStart('\uFEFF').Split('\n');
            if (lines.Length < 2 || !lines[0].StartsWith("# " + Version + "|", StringComparison.Ordinal)) throw new FormatException("数独题库版本无效");
            string[] manifest = lines[0].Substring(2).Split('|');
            int expectedCount;
            if (manifest.Length != 3 || !int.TryParse(manifest[1], NumberStyles.None, CultureInfo.InvariantCulture, out expectedCount) || expectedCount < 1 || expectedCount > 2000 || manifest[2].Length != 64) throw new FormatException("数独题库清单无效");
            var payload = new StringBuilder();
            for (int i = 1; i < lines.Length; i++) if (lines[i].Trim().Length != 0 && !lines[i].Trim().StartsWith("#", StringComparison.Ordinal)) payload.Append(lines[i].Trim()).Append('\n');
            using (var hash = SHA256.Create())
            {
                string actual = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString()))).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(actual, manifest[2], StringComparison.Ordinal)) throw new FormatException("数独题库完整性校验失败");
            }
            var catalog = new SudokuCatalog();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var boards = new HashSet<string>(StringComparer.Ordinal);
            int lineNumber = 0;
            foreach (string raw in lines)
            {
                lineNumber++;
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var parts = line.Split('|');
                int difficulty, level;
                if (parts.Length != 5 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out difficulty) ||
                    !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out level)) throw Invalid(lineNumber);
                var puzzle = new SudokuPuzzle { Id = parts[0], Difficulty = difficulty, Level = level, Givens = ReadDigits(parts[3]), Solution = ReadDigits(parts[4]) };
                string error;
                if (!puzzle.TryValidate(out error) || !ids.Add(puzzle.Id) || !boards.Add(parts[3]) || catalog.bySlot.ContainsKey(difficulty * 501 + level)) throw Invalid(lineNumber);
                catalog.puzzles.Add(puzzle);
                catalog.bySlot.Add(difficulty * 501 + level, puzzle);
            }
            if (catalog.puzzles.Count != expectedCount) throw new FormatException("数独题库题目数与清单不符");
            return catalog;
        }

        public SudokuPuzzle Get(int difficulty, int level)
        {
            SudokuPuzzle puzzle;
            return difficulty >= 0 && difficulty < 4 && level >= 1 && level <= 500 && bySlot.TryGetValue(difficulty * 501 + level, out puzzle) ? puzzle.Copy() : null;
        }
        public int Count(int difficulty) { int count = 0; foreach (var puzzle in puzzles) if (puzzle.Difficulty == difficulty) count++; return count; }
        private static int[] ReadDigits(string digits)
        {
            if (digits.Length != 81) return null;
            var result = new int[81];
            for (int i = 0; i < result.Length; i++) { if (digits[i] < '0' || digits[i] > '9') return null; result[i] = digits[i] - '0'; }
            return result;
        }
        private static FormatException Invalid(int line) { return new FormatException("数独题库第 " + line + " 行无效或重复"); }
    }
}
