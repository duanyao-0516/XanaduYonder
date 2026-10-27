using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using XanaduYonder.Core.Sudoku;

internal static class Importer
{
    private sealed class Entry { internal SudokuPuzzle Puzzle; internal string Source; internal int Line; internal string Hash; internal string Rating; }
    internal static void Run(string[] args)
    {
        if (args.Length < 2) throw new ArgumentException("--import source-file ... (name.txt or selected.tsv)");
        var buckets = new[] { new List<Entry>(), new List<Entry>(), new List<Entry>(), new List<Entry>() };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var classified = new int[5]; int scanned = 0;
        foreach (string path in Slice(args))
        {
            int sourceLine = 0; string sourceName = Path.GetFileNameWithoutExtension(path);
            foreach (string raw in File.ReadLines(path))
            {
                sourceLine++;
                if (raw.StartsWith("#", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(raw)) continue;
                string hash, digits, rating, source; int originLine;
                if (path.EndsWith(".tsv", StringComparison.Ordinal))
                {
                    var fields = raw.Split('\t'); if (fields.Length != 6) throw new FormatException("Selected source line shape");
                    source = fields[1]; originLine = int.Parse(fields[2], CultureInfo.InvariantCulture); hash = fields[3]; rating = fields[4]; digits = fields[5];
                }
                else
                {
                    var fields = raw.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length != 3 || fields[0].Length != 12 || fields[1].Length != 81) throw new FormatException("Upstream source line shape: " + path + ":" + sourceLine);
                    source = sourceName; originLine = sourceLine; hash = fields[0]; digits = fields[1]; rating = fields[2];
                }
                if (!seen.Add(digits)) continue;
                scanned++;
                var givens = Program.Digits(digits); int[] solution;
                if (SudokuSolver.CountSolutions(givens, out solution) != 1) throw new Exception("Upstream puzzle is not uniquely solvable: " + hash);
                var puzzle = new SudokuPuzzle { Id = "candidate-" + hash, Difficulty = 0, Level = 1, Givens = givens, Solution = solution };
                int difficulty, steps;
                bool logical = SudokuHintEngine.TrySolveLogically(puzzle, out difficulty, out steps);
                classified[logical ? difficulty : 4]++;
                if (logical && buckets[difficulty].Count < 500)
                {
                    puzzle.Difficulty = difficulty; puzzle.Level = buckets[difficulty].Count + 1;
                    puzzle.Id = "se-v1-d" + difficulty + "-l" + puzzle.Level.ToString("000", CultureInfo.InvariantCulture);
                    buckets[difficulty].Add(new Entry { Puzzle = puzzle, Source = source, Line = originLine, Hash = hash, Rating = rating });
                }
                if (scanned % 500 == 0) Console.WriteLine("IMPORT scanned=" + scanned + " selected=" + Counts(buckets) + " classes=" + string.Join(",", classified));
                if (Full(buckets)) break;
            }
            Console.WriteLine("IMPORT source=" + sourceName + " scanned=" + scanned + " selected=" + Counts(buckets));
            if (Full(buckets)) break;
        }
        if (!Full(buckets)) throw new Exception("Insufficient eligible puzzles; no output written. selected=" + Counts(buckets) + ", classes=" + string.Join(",", classified));
        var body = new StringBuilder(); var provenance = new StringBuilder("# id\tsource-file\tline\tupstream-digest\tupstream-rating\tgivens\n");
        foreach (var bucket in buckets) foreach (var entry in bucket)
        {
            var p = entry.Puzzle;
            body.Append(p.Id).Append('|').Append(p.Difficulty).Append('|').Append(p.Level).Append('|').Append(string.Join("", p.Givens)).Append('|').Append(string.Join("", p.Solution)).Append('\n');
            provenance.Append(p.Id).Append('\t').Append(entry.Source).Append('\t').Append(entry.Line).Append('\t').Append(entry.Hash).Append('\t').Append(entry.Rating).Append('\t').Append(string.Join("", p.Givens)).Append('\n');
        }
        Directory.CreateDirectory("Assets/Resources/Sudoku"); Directory.CreateDirectory("ThirdParty/Sudoku");
        string bank = Program.Envelope(body.ToString());
        const string bankPath = "Assets/Resources/Sudoku/puzzles.txt";
        if (File.Exists(bankPath) && File.ReadAllText(bankPath) != bank) throw new InvalidOperationException("Refusing to change stable v1 puzzle IDs. A changed selection requires a new catalog version and save migration.");
        File.WriteAllText(bankPath, bank, new UTF8Encoding(false));
        File.WriteAllText("ThirdParty/Sudoku/selected.tsv", provenance.ToString(), new UTF8Encoding(false));
        if (!(args.Length == 2 && args[1].EndsWith(".tsv", StringComparison.Ordinal))) File.WriteAllText("ThirdParty/Sudoku/import-report.txt", "version=" + SudokuCatalog.Version + "\nscanned=" + scanned + "\nselected=" + Counts(buckets) + "\nclassifications=" + string.Join(",", classified) + "\n", new UTF8Encoding(false));
        Console.WriteLine("IMPORT complete: 2,000 unique, logically solvable puzzles; bank and provenance written.");
    }
    private static string[] Slice(string[] args) { var paths = new string[args.Length - 1]; Array.Copy(args, 1, paths, 0, paths.Length); return paths; }
    private static bool Full(List<Entry>[] buckets) { foreach (var b in buckets) if (b.Count != 500) return false; return true; }
    private static string Counts(List<Entry>[] buckets) { return buckets[0].Count + "," + buckets[1].Count + "," + buckets[2].Count + "," + buckets[3].Count; }
}
