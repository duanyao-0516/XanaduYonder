using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using XanaduYonder.Core.Sheep;

internal static class Program
{
    private static int passed, failed;

    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--export") { ExportGeometry(); return 0; }
        Run("legacy six layouts retain exact save geometry and id ordering", LegacyGeometry);
        Run("layout support distinguishes tutorials and new challenges", SupportedLayouts);
        Run("twelve new layouts have bounded exact card geometry", Geometry);
        Run("main piles form two three and four separated islands", Islands);
        Run("all new layouts retain shallow side reserves and independent blind stacks", ReserveGeometry);
        Run("all twelve layouts have distinct geometry and staggered layers", Variety);
        Run("new challenge generation avoids legacy layouts and samples every new layout", NewGeneration);
        Run("repeat generation excludes previous layout without changing tutorial selection", NoImmediateRepeat);
        Run("one thousand generated witnesses clear through the real game core", SolutionWitnesses);
        Run("restore preserves layout and future legal selections after each move", RestoreContinuation);
        Console.WriteLine("Sheep layout checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void ExportGeometry()
    {
        var text = new StringBuilder("[");
        for (int id = 3; id < 15; id++)
        {
            if (id != 3) text.Append(',');
            text.Append("{\"layoutId\":").Append(id).Append(",\"positions\":[");
            var positions = SheepBoardLayout.GetPositions(2, id);
            for (int i = 0; i < positions.Length; i++)
            {
                if (i != 0) text.Append(',');
                var p = positions[i];
                text.Append("{\"id\":").Append(p.Id).Append(",\"x\":").Append(p.X).Append(",\"y\":").Append(p.Y)
                    .Append(",\"layer\":").Append(p.Layer).Append(",\"blindStack\":").Append(p.BlindStack).Append('}');
            }
            text.Append("]}");
        }
        Console.WriteLine(text.Append(']').ToString());
    }

    private static void Run(string title, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + title); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + title + ": " + e.Message); }
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new Exception(reason);
    }

    private static string Fingerprint(SheepPosition[] positions)
    {
        var text = new StringBuilder();
        foreach (var p in positions) text.Append(p.Id).Append(',').Append(p.X).Append(',').Append(p.Y).Append(',').Append(p.Layer).Append(',').Append(p.BlindStack).Append(';');
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
    }

    private static void LegacyGeometry()
    {
        var expected = new[] {
            "a9e98023564a7e827f56f8865644e75db4ed973e8c2c33972551489929c85190", "d6f013ffb84d31bb5ec2688e83343bc7586f5cd3e62acabe36867f3b31570ce3", "ef2ec4dc9d72a9b58bf2185be072d7aac62a51b476c5d1b6ca9381facfb6e4d3",
            "bee7d235cccad86226d4136ed9c1419a588b7d2fb697ea6bbb67bc052b3c7a6a", "dc7623961d81264e6864b15017c90d675bea06cc00add536da35231561755677", "a2308cd4b085a4ccca085d6c8a4aa60047e341fd0da199e8d05f3eaf8988a12e"
        };
        for (int level = 1; level <= 2; level++)
        for (int id = 0; id < 3; id++) Check(Fingerprint(SheepBoardLayout.GetPositions(level, id)) == expected[(level - 1) * 3 + id], "Legacy geometry changed for " + level + "/" + id);
    }

    private static void SupportedLayouts()
    {
        for (int id = -1; id <= SheepBoardLayout.ChallengeLayoutCount; id++)
        {
            Check(SheepBoardLayout.IsSupported(1, id) == (id >= 0 && id < 3), "Tutorial support range");
            Check(SheepBoardLayout.IsSupported(2, id) == (id >= 0 && id < 15), "Challenge support range");
            Check(!SheepBoardLayout.IsSupported(0, id) && !SheepBoardLayout.IsSupported(3, id), "Invalid level accepted");
        }
    }

    private static void Geometry()
    {
        for (int layout = 3; layout < 15; layout++)
        {
            var p = SheepBoardLayout.GetPositions(2, layout);
            Check(p.Length == 144, "Count " + layout);
            for (int i = 0; i < p.Length; i++)
            {
                Check(p[i].Id == i && p[i].X >= 0 && p[i].X <= 14 && p[i].Y >= 0 && p[i].Y <= 14, "Bounds " + layout + "/" + i);
                for (int j = i + 1; j < p.Length; j++)
                    Check(p[i].Layer != p[j].Layer || !SheepBoardLayout.Overlaps(p[i], p[j]), "Same-layer collision " + layout + "/" + i + "/" + j);
            }
        }
    }

    private static void Islands()
    {
        var expected = new[] { 2, 3, 4, 3 };
        for (int layout = 3; layout < 15; layout++)
        {
            var cards = SheepBoardLayout.GetPositions(2, layout).Where(p => p.BlindStack == 0 && p.Layer >= 2).ToArray();
            var reached = new bool[cards.Length]; int islands = 0;
            for (int i = 0; i < cards.Length; i++)
            {
                if (reached[i]) continue;
                islands++; reached[i] = true; var queue = new Queue<int>(); queue.Enqueue(i);
                while (queue.Count > 0)
                {
                    var a = cards[queue.Dequeue()];
                    for (int j = 0; j < cards.Length; j++)
                        if (!reached[j] && Math.Abs(a.X - cards[j].X) <= 2 && Math.Abs(a.Y - cards[j].Y) <= 2)
                        { reached[j] = true; queue.Enqueue(j); }
                }
            }
            Check(islands == expected[(layout - 3) / 3], "Expected separated islands, layout=" + layout + " got=" + islands);
        }
    }

    private static void ReserveGeometry()
    {
        for (int layout = 3; layout < 15; layout++)
        {
            var p = SheepBoardLayout.GetPositions(2, layout); var locations = new SheepLocation[144];
            Check(p.Count(x => x.BlindStack == 0) == 120, "Normal count");
            Check(p.Count(x => x.BlindStack == 0 && (x.X == 0 || x.X == 14) && x.Layer <= 1) >= 12, "Missing shallow reserves");
            for (int side = 1; side <= 2; side++)
            {
                var stack = p.Where(x => x.BlindStack == side).ToArray();
                Check(stack.Length == 12 && stack.All(x => x.Y <= 12), "Blind geometry");
                Check(stack.Count(x => SheepBoardLayout.IsExposed(p, locations, x.Id)) == 1, "Blind stack exposes more than top");
                Check(stack.Select(x => x.X + "," + x.Y).Distinct().Count() == 1, "Blind stack lost common face");
                foreach (var card in stack)
                    Check(!p.Any(x => x.BlindStack == 0 && SheepBoardLayout.Overlaps(card, x)), "Blind and main faces intersect");
            }
        }
    }

    private static void Variety()
    {
        var signatures = new HashSet<string>();
        for (int id = 3; id < 15; id++)
        {
            var p = SheepBoardLayout.GetPositions(2, id);
            Check(signatures.Add(Fingerprint(p)), "Repeated geometry");
            Check(p.Any(x => x.BlindStack == 0 && x.X % 2 != x.Y % 2), "Missing staggered rows");
            Check(p.Where(x => x.BlindStack == 0).Select(x => x.Layer).Distinct().Count() >= 6, "Deep piles missing");
            var other = SheepBoardLayout.GetPositions(2, id); p[0].X = 99;
            Check(other[0].X != 99, "Layout shared mutable array");
        }
    }

    private static void NewGeneration()
    {
        var sampled = new HashSet<int>();
        for (uint seed = 1; seed <= 300; seed++)
        {
            var board = SheepLevelGenerator.Generate(2, seed);
            Check(board.Snapshot.LayoutId >= 3 && board.Snapshot.LayoutId < 15, "New challenge used legacy");
            sampled.Add(board.Snapshot.LayoutId);
            var again = SheepLevelGenerator.Generate(2, seed);
            Check(board.Snapshot.LayoutId == again.Snapshot.LayoutId && board.SolutionIds.SequenceEqual(again.SolutionIds), "Generation nondeterministic");
        }
        Check(sampled.Count == 12, "Did not sample all layouts");
    }

    private static void NoImmediateRepeat()
    {
        for (uint seed = 0; seed < 80; seed++)
        for (int previous = 3; previous < 15; previous++)
        {
            var board = SheepLevelGenerator.Generate(2, seed, previous);
            Check(board.Snapshot.LayoutId >= 3 && board.Snapshot.LayoutId < 15 && board.Snapshot.LayoutId != previous, "Previous layout repeated");
            var tutorial = SheepLevelGenerator.Generate(1, seed, previous);
            Check(tutorial.Snapshot.LayoutId == SheepLevelGenerator.Generate(1, seed).Snapshot.LayoutId, "Tutorial selection changed");
        }
    }

    private static void SolutionWitnesses()
    {
        var visited = new HashSet<int>();
        for (uint seed = 1; seed <= 1000; seed++)
        {
            var board = SheepLevelGenerator.Generate(2, seed, (int)(seed % 12) + 3); visited.Add(board.Snapshot.LayoutId);
            var game = new SheepGame(board.Snapshot);
            foreach (int id in board.SolutionIds)
            {
                Check(game.CanPick(id), "Witness blocked at seed " + seed + " id " + id);
                Check(game.Pick(id).Changed && game.Phase != SheepPhase.Lost, "Witness lost at seed " + seed);
            }
            Check(game.Phase == SheepPhase.Won, "Witness did not clear");
        }
        Check(visited.Count == 12, "Missing generated family witness");
    }

    private static void RestoreContinuation()
    {
        for (uint seed = 1; seed <= 24; seed++)
        {
            var board = SheepLevelGenerator.Generate(2, seed); var game = new SheepGame(board.Snapshot);
            var geometry = Fingerprint(SheepBoardLayout.GetPositions(2, board.Snapshot.LayoutId));
            foreach (int id in board.SolutionIds)
            {
                game = new SheepGame(game.GetSnapshot());
                Check(Fingerprint(SheepBoardLayout.GetPositions(2, game.GetSnapshot().LayoutId)) == geometry, "Restored geometry changed");
                Check(game.Pick(id).Changed, "Restore changed future picks");
            }
            Check(game.Phase == SheepPhase.Won, "Restored path did not win");
        }
    }
}
