using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Sokoban;

internal static class Program
{
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    private static void Reject(string json, string label)
    {
        try { SokobanCatalog.Parse(json); }
        catch (ArgumentException) { return; }
        catch (FormatException) { return; }
        catch (JsonException) { return; }
        throw new Exception("Catalog accepted invalid content: " + label);
    }
    private static int StrictCatalogChecks(JObject original)
    {
        var changes = new Action<JObject>[] {
            root => root.Remove("schemaVersion"),
            root => root["schemaVersion"] = 2,
            root => root["schemaVersion"] = "1",
            root => root["levels"] = JValue.CreateNull(),
            root => root["unknown"] = 1,
            root => root["levels"][0]["source"] = JValue.CreateNull(),
            root => root["levels"][0]["source"]["sourceId"] = 1,
            root => root["levels"][0]["source"]["collection"] = "",
            root => root["levels"][0]["rows"][0] = JValue.CreateNull(),
            root => root["levels"][0]["solution"] = "LRX",
            root => root["levels"][0]["solution"] = "",
            root => root["levels"][0]["contentVersion"] = "different",
            root => root["levels"][1]["levelId"] = root["levels"][0]["levelId"].DeepClone(),
            root => root["levels"][1]["source"] = root["levels"][0]["source"].DeepClone(),
            root => root["levels"][1]["order"] = root["levels"][0]["order"].DeepClone(),
            root => root["levels"][1]["rows"] = root["levels"][0]["rows"].DeepClone(),
            root => root["levels"][8]["rows"] = root["levels"][0]["rows"].DeepClone()
        };
        for (int i = 0; i < changes.Length; i++)
        {
            var changed = (JObject)original.DeepClone(); changes[i](changed);
            Reject(changed.ToString(Formatting.None), "mutation " + (i + 1));
        }
        string valid = original.ToString(Formatting.None);
        Reject(valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1"), "duplicate key");
        Reject(valid + "{}", "trailing second JSON value");
        return changes.Length + 2;
    }
    private static int Main(string[] args)
    {
        try
        {
            string path = "Assets/Resources/Sokoban/campaign.json", reportPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--catalog" && i + 1 < args.Length) path = args[++i];
                else if (args[i] == "--report" && i + 1 < args.Length) reportPath = args[++i];
                else throw new ArgumentException("Expected --catalog path or --report path.");
            }
            byte[] bytes = File.ReadAllBytes(path);
            var catalog = SokobanCatalog.Parse(Encoding.UTF8.GetString(bytes));
            var raw = JObject.Parse(Encoding.UTF8.GetString(bytes));
            int malformedChecks = StrictCatalogChecks(raw);
            Require(catalog.Count == 108, "Exactly 108 release levels are required.");
            Require(catalog.ContentVersion == "sokoban-campaign-v1", "Unexpected campaign version.");
            var chapters = new Dictionary<string, int>();
            var report = new JArray(); int totalMoves = 0, totalPushes = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                var level = catalog.At(i); var game = new SokobanGame(level);
                Require(level.Order == i + 1, "Campaign order must be consecutive: " + level.LevelId);
                Require(level.LevelId == (string)raw["levels"][i]["levelId"], "Catalog changed source order.");
                if (!chapters.ContainsKey(level.Chapter)) chapters.Add(level.Chapter, 0);
                chapters[level.Chapter]++;
                for (int step = 0; step < level.Solution.Length; step++)
                {
                    var move = game.Move(SokobanGame.FromChar(level.Solution[step]));
                    Require(move.Changed, level.LevelId + " blocked at step " + (step + 1));
                    Require(game.Moves == step + 1, "Move count drift: " + level.LevelId);
                    Require(!game.Won || step == level.Solution.Length - 1, "Solution contains actions after victory: " + level.LevelId);
                }
                Require(game.Won && game.GoalsFilled == level.BoxCount, "Witness did not solve " + level.LevelId);
                Require(game.Actions == level.Solution, "Replay history drift: " + level.LevelId);
                var snapshot = game.GetSnapshot();
                Require(snapshot.Boxes.All(level.IsGoal), "Final box outside goal: " + level.LevelId);
                string rows = string.Join("\n", ((JArray)raw["levels"][i]["rows"]).Select(r => (string)r)) + "\n";
                report.Add(new JObject { ["levelId"] = level.LevelId, ["mapSha256"] = Hash(Encoding.UTF8.GetBytes(rows)), ["moves"] = game.Moves, ["pushes"] = game.Pushes, ["finalPlayer"] = game.Player, ["finalBoxes"] = new JArray(snapshot.Boxes) });
                totalMoves += game.Moves; totalPushes += game.Pushes;
                // Every nonterminal prefix remains undoable, including all pushes.
                game.Restart();
                foreach (char direction in level.Solution.Take(level.Solution.Length - 1)) Require(game.Move(SokobanGame.FromChar(direction)).Changed, "Prefix replay failed.");
                while (game.CanUndo) Require(game.Undo(), "Undo failed: " + level.LevelId);
                Require(game.Player == level.InitialPlayer && game.Moves == 0 && game.Pushes == 0 && game.GetSnapshot().Boxes.SequenceEqual(level.InitialBoxes), "Full prefix undo did not restore " + level.LevelId);
            }
            Require(chapters.Count == 6 && chapters["tutorial"] == 8, "Expected tutorial plus five chapters.");
            for (int i = 1; i <= 5; i++) Require(chapters["warehouse-" + i.ToString("00")] == 20, "Expected 20 levels in each warehouse.");
            Require(catalog.Get("tutorial-001").Width == 8 && catalog.Get("tutorial-001").Height == 8, "Compact prototype dimensions changed.");
            Require(catalog.Get("tutorial-008").Width == 8 && catalog.Get("tutorial-008").Height == 13, "Tall prototype dimensions changed.");
            var root = new JObject { ["schemaVersion"] = 1, ["contentVersion"] = catalog.ContentVersion, ["validator"] = "production-csharp-SokobanGame", ["catalogSha256"] = Hash(bytes), ["levelCount"] = catalog.Count, ["totalMoves"] = totalMoves, ["totalPushes"] = totalPushes, ["levels"] = report };
            if (reportPath != null) File.WriteAllText(reportPath, root.ToString(Formatting.Indented) + "\n", new UTF8Encoding(false));
            Console.WriteLine("PASS: 108 production SokobanGame witnesses and full nonterminal undo histories; " + totalMoves + " moves / " + totalPushes + " pushes.");
            Console.WriteLine("PASS: " + malformedChecks + " strict production catalog rejection checks.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
