using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using XanaduYonder.Core.Idiom;

internal static class CatalogChecks
{
    internal static void Run(string path)
    {
        var commonPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), "../../Data/Idioms/common-idioms.txt"));
        var common = new HashSet<string>(File.ReadAllLines(commonPath).Select(s => s.Trim()).Where(s => s.Length > 0 && !s.StartsWith("#")), StringComparer.Ordinal);
        var load = Stopwatch.StartNew(); var entries = new List<IdiomEntry>();
        using (var document = JsonDocument.Parse(File.ReadAllBytes(path)))
        foreach (var row in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            string word = row.GetProperty("word").GetString();
            entries.Add(new IdiomEntry(row.GetProperty("id").GetString(), word,
                row.GetProperty("pinyin").GetProperty("display").GetString(), row.GetProperty("firstKey").GetString(),
                row.GetProperty("lastKey").GetString(), row.GetProperty("senseId").GetString(),
                row.GetProperty("detailFile").GetString(), common.Contains(word)));
        }
        var lex = new IdiomLexicon(entries, "full-catalog-test"); load.Stop();
        Require(lex.Count > 40000, "Expected the complete current game dictionary.");
        Console.WriteLine("CATALOG entries=" + lex.Count + " common=" + entries.Count(e => e.Common) + " load_ms=" + load.Elapsed.TotalMilliseconds.ToString("F2"));
        var starts = new List<double>(); var submissions = new List<double>(); var restores = new List<double>();
        int longest = 0, totalMessages = 0;
        foreach (IdiomMode mode in new[] { IdiomMode.SameCharacter, IdiomMode.SameSound })
        {
            var byFirst = lex.Entries.GroupBy(e => mode == IdiomMode.SameCharacter ? e.FirstCharacter : e.FirstKey)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            for (uint seed = 1; seed <= 8; seed++)
            {
                var game = new IdiomGame(lex, mode, seed); var clock = Stopwatch.StartNew(); game.Start(IdiomActor.App);
                clock.Stop(); starts.Add(clock.Elapsed.TotalMilliseconds);
                Require(game.LastEntry.Common, "Complete catalog openings must be curated common words.");
                var used = new HashSet<string>(StringComparer.Ordinal); var usedFirst = new Dictionary<string, int>(StringComparer.Ordinal);
                int accounted = 0;
                while (!game.IsEnded && game.History.Count < 1000)
                {
                    while (accounted < game.History.Count)
                    {
                        IdiomEntry accepted; lex.TryGetById(game.History[accounted++], out accepted);
                        Require(used.Add(accepted.Id), "A source word appeared twice in the game.");
                        string key = mode == IdiomMode.SameCharacter ? accepted.FirstCharacter : accepted.FirstKey;
                        int n; usedFirst.TryGetValue(key, out n); usedFirst[key] = n + 1;
                    }
                    string target = mode == IdiomMode.SameCharacter ? game.LastEntry.LastCharacter : game.LastEntry.LastKey;
                    List<IdiomEntry> candidates; Require(byFirst.TryGetValue(target, out candidates), "Live turn must have indexed moves.");
                    IdiomEntry selected = null; int score = -1;
                    foreach (var entry in candidates)
                    {
                        if (used.Contains(entry.Id)) continue;
                        string next = mode == IdiomMode.SameCharacter ? entry.LastCharacter : entry.LastKey;
                        List<IdiomEntry> future; int spent;
                        int remaining = (byFirst.TryGetValue(next, out future) ? future.Count : 0) - (usedFirst.TryGetValue(next, out spent) ? spent : 0);
                        if (remaining > score) { score = remaining; selected = entry; }
                    }
                    Require(selected != null, "A live player turn must have a legal move.");
                    clock.Restart(); Require(game.Submit(selected.Word).Success, "Selected legal player word was rejected.");
                    clock.Stop(); submissions.Add(clock.Elapsed.TotalMilliseconds);
                    if (game.History.Count % 100 == 1 || game.IsEnded)
                    {
                        var snapshot = game.GetSnapshot(); clock.Restart(); var restored = new IdiomGame(lex, snapshot); clock.Stop();
                        restores.Add(clock.Elapsed.TotalMilliseconds);
                        Require(restored.History.SequenceEqual(game.History) && restored.GetSnapshot().RandomState == snapshot.RandomState &&
                                restored.EndReason == game.EndReason && restored.PlayerCount == game.PlayerCount, "Long snapshot restores exactly.");
                        game = restored;
                    }
                }
                Require(game.History.Distinct().Count() == game.History.Count, "No repeated ids including last APP reply.");
                new IdiomGame(lex, game.GetSnapshot());
                longest = Math.Max(longest, game.History.Count); totalMessages += game.History.Count;
                Console.WriteLine("CATALOG run mode=" + mode + " seed=" + seed + " messages=" + game.History.Count + " end=" + game.EndReason);
            }
        }
        Require(longest >= 1000, "At least one real-dictionary game must exercise a 1,000-message history.");
        Print("start", starts); Print("submit", submissions); Print("restore", restores);
        Console.WriteLine("CATALOG total_messages=" + totalMessages + " longest=" + longest + " (desktop .NET timings, not device timings)");
    }

    private static void Print(string name, List<double> values)
    {
        values.Sort();
        Console.WriteLine("CATALOG " + name + " samples=" + values.Count + " median_ms=" + values[values.Count / 2].ToString("F3") +
                          " max_ms=" + values[values.Count - 1].ToString("F3"));
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
