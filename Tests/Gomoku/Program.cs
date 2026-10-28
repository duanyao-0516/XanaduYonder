using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using XanaduYonder.Core.Gomoku;

internal static class Program
{
    private static int passed, failed;
    private static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--benchmark") { Benchmark(); return; }
        Check("empty board and legal turn contract", () => {
            var g = new GomokuGame();
            Assert(g.State == GomokuState.Playing && g.ToPlay == GomokuStone.Black && g.MoveCount == 0 && g.LastMove == -1, "empty");
            Assert(!g.TryPlay(-1) && !g.TryPlay(225) && g.TryPlay(112) && !g.TryPlay(112), "legality");
            Assert(g.GetCell(112) == GomokuStone.Black && g.ToPlay == GomokuStone.White && g.MoveCount == 1, "black first");
            Throws<ArgumentOutOfRangeException>(() => g.GetCell(225));
        });
        Check("wins in all four directions stop further play", () => {
            foreach (int step in new[] { 1, 15, 16, 14 }) {
                int start = step == 14 ? 14 : 0;
                var g = new GomokuGame();
                for (int n = 0; n < 5; n++) { Assert(g.TryPlay(start + n * step), "black"); if (n < 4) Assert(g.TryPlay(200 + n * 2), "white"); }
                Assert(g.State == GomokuState.BlackWon && g.WinningLine.Length == 5 && !g.TryPlay(100), "terminal");
                Assert(g.WinningLine.SequenceEqual(Enumerable.Range(0, 5).Select(n => start + n * step)), "line");
            }
        });
        Check("white win and boundary cannot wrap", () => {
            var g = GomokuGame.Restore(new[] { 13, 75, 14, 76, 15, 77, 16, 78, 17, 79 });
            Assert(g.State == GomokuState.WhiteWon, "row wrap must not win for black");
        });
        Check("overline wins and returns full six stone line", () => {
            var g = GomokuGame.Restore(new[] { 105, 0, 106, 2, 107, 4, 109, 6, 110, 8, 108 });
            Assert(g.State == GomokuState.BlackWon && g.WinningLine.Length == 6, "freestyle overline");
        });
        Check("draw only when board is full", () => {
            var b = new List<int>(); var w = new List<int>();
            for (int i = 0; i < 225; i++) { if ((i / 15 * 2 + i % 15) % 4 < 2) b.Add(i); else w.Add(i); }
            var g = new GomokuGame();
            for (int i = 0; i < 225; i++) { Assert(g.State == GomokuState.Playing, "premature terminal " + i); Assert(g.TryPlay(i % 2 == 0 ? b[i / 2] : w[i / 2]), "draw move"); }
            Assert(g.State == GomokuState.Draw && g.WinningLine.Length == 0, "full draw");
        });
        Check("replay rejects malformed or post-terminal history", () => {
            Throws<ArgumentNullException>(() => GomokuGame.Restore(null));
            foreach (var moves in new[] { new[] { -1 }, new[] { 225 }, new[] { 112, 112 }, new[] { 0, 200, 1, 202, 2, 204, 3, 206, 4, 208 } })
                Throws<ArgumentException>(() => GomokuGame.Restore(moves));
        });
        Check("snapshots and copies cannot mutate their source", () => {
            var input = new[] { 0, 200, 1, 202, 2, 204, 3, 206, 4 };
            var g = GomokuGame.Restore(input); input[0] = 20;
            var history = g.ExportMoves(); history[0] = 21;
            var line = g.WinningLine; line[0] = 22;
            var copy = g.Copy(); Assert(copy.Undo(1) && copy.TryPlay(50), "copy mutable");
            Assert(g.GetCell(0) == GomokuStone.Black && g.ExportMoves()[0] == 0 && g.WinningLine[0] == 0 && g.State == GomokuState.BlackWon, "defensive");
        });
        Check("undo is atomic and restores turn and terminal state", () => {
            var g = GomokuGame.Restore(new[] { 0, 200, 1, 202, 2, 204, 3, 206, 4 });
            Assert(!g.Undo(0) && !g.Undo(-1) && !g.Undo(10) && g.MoveCount == 9, "invalid undo");
            Assert(g.Undo(1) && g.State == GomokuState.Playing && g.ToPlay == GomokuStone.Black && g.LastMove == 206 && g.WinningLine.Length == 0, "undo terminal");
            Assert(g.Undo(8) && g.LastMove == -1 && g.ToPlay == GomokuStone.Black, "undo all");
        });
        Check("coordinates roundtrip all cells and validate syntax", () => {
            for (int i = 0; i < 225; i++) { int parsed; Assert(GomokuCoordinates.TryParse(GomokuCoordinates.Format(i), out parsed) && parsed == i, "roundtrip"); }
            int n; Assert(GomokuCoordinates.Format(82) == "H10" && GomokuCoordinates.TryParse("h10", out n) && n == 82, "H10");
            foreach (string invalid in new[] { null, "", "A0", "A16", "P1", "A01", "A+1", "AA1", "A 1", "A1x" }) Assert(!GomokuCoordinates.TryParse(invalid, out n) && n == -1, "invalid " + invalid);
            Throws<ArgumentOutOfRangeException>(() => GomokuCoordinates.Format(-1));
        });
        Check("AI takes own win before opponent threat at zero search budget", () => {
            var g = GomokuGame.Restore(new[] { 105, 0, 106, 1, 107, 2, 108, 3 });
            foreach (GomokuDifficulty level in Enum.GetValues(typeof(GomokuDifficulty))) {
                var r = GomokuAi.FindMove(g, level, CancellationToken.None, Budget(0));
                Assert(r.Index == 109 && r.CompletedDepth == 0, "win first " + level);
            }
        });
        Check("AI blocks sole jump-four gap including board edge", () => {
            foreach (var moves in new[] { new[] { 200, 0, 202, 1, 204, 3, 206, 4 }, new[] { 200, 14, 202, 29, 204, 59, 206, 74 } }) {
                var g = GomokuGame.Restore(moves); int gap = moves[1] == 0 ? 2 : 44;
                var r = GomokuAi.FindMove(g, GomokuDifficulty.Easy, CancellationToken.None, Budget(0));
                Assert(r.Index == gap, "global mandatory defense");
            }
        });
        Check("hard proves a multi-move forcing-four and mirrored tactic", () => {
            int[] moves = { 108, 107, 109, 0, 110, 2, 66, 4, 81, 6 };
            var g = GomokuGame.Restore(moves);
            var options = Budget(200000); options.MaxDepth = 1; options.VcfMaxDepth = 5;
            var r = GomokuAi.FindMove(g, GomokuDifficulty.Hard, CancellationToken.None, options);
            Assert(r.Index == 111, "closed four forces block then open four; got " + r.Index);
            Assert(g.TryPlay(111) && g.TryPlay(112) && g.TryPlay(96), "forcing continuation");
            foreach (int defense in Enumerable.Range(0, 225).Where(i => g.GetCell(i) == GomokuStone.Empty)) {
                var copy = g.Copy(); Assert(copy.TryPlay(defense), "defender legal");
                bool won = false;
                for (int i = 0; i < 225 && !won; i++) { var leaf = copy.Copy(); if (leaf.TryPlay(i) && leaf.State == GomokuState.BlackWon) won = true; }
                Assert(won, "all final defenses lose");
            }
            var mirrored = GomokuGame.Restore(moves.Select(Mirror).ToArray());
            Assert(GomokuAi.FindMove(mirrored, GomokuDifficulty.Hard, CancellationToken.None, options).Index == Mirror(111), "mirror VCF");
        });
        Check("AI fallback is legal and source history never changes", () => {
            var g = GomokuGame.Restore(new[] { 112, 111, 97, 127 }); var before = g.ExportMoves();
            var r = GomokuAi.FindMove(g, GomokuDifficulty.Normal, CancellationToken.None, Budget(0));
            Assert(r.Index >= 0 && g.GetCell(r.Index) == GomokuStone.Empty && r.CompletedDepth == 0 && r.Nodes == 0, "legal zero-node fallback");
            Assert(g.ExportMoves().SequenceEqual(before), "input unchanged");
            var timed = Budget(100000); timed.TimeLimitMilliseconds = 0;
            Assert(g.GetCell(GomokuAi.FindMove(g, GomokuDifficulty.Hard, CancellationToken.None, timed).Index) == GomokuStone.Empty, "zero-time fallback");
        });
        Check("AI pre-cancellation throws and does not mutate board", () => {
            var g = GomokuGame.Restore(new[] { 112, 111 }); var before = g.ExportMoves();
            var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            Throws<OperationCanceledException>(() => GomokuAi.FindMove(g, GomokuDifficulty.Hard, cancellation.Token));
            Assert(g.ExportMoves().SequenceEqual(before), "cancel unchanged");
        });
        Check("bounded-node search is deterministic and completes a depth", () => {
            var g = GomokuGame.Restore(new[] { 112, 111, 97, 127, 98, 128 });
            var options = Budget(300); options.MaxDepth = 5;
            var a = GomokuAi.FindMove(g, GomokuDifficulty.Normal, CancellationToken.None, options);
            var b = GomokuAi.FindMove(g, GomokuDifficulty.Normal, CancellationToken.None, options);
            Assert(a.Index == b.Index && a.Nodes == b.Nodes && a.CompletedDepth == b.CompletedDepth, "deterministic");
            Assert(a.Nodes <= 300 && a.CompletedDepth >= 1 && g.GetCell(a.Index) == GomokuStone.Empty, "bounded completed iteration");
        });
        Check("search creates and defends a crossing double-three fork", () => {
            var attack = GomokuGame.Restore(new[] { 111, 0, 113, 2, 97, 30, 127, 32 });
            var options = Budget(12000); options.MaxDepth = 3;
            var attacked = GomokuAi.FindMove(attack, GomokuDifficulty.Normal, CancellationToken.None, options);
            Assert(attacked.Index == 112, "create fork got " + attacked.Index + " depth " + attacked.CompletedDepth + " nodes " + attacked.Nodes);
            var defense = GomokuGame.Restore(new[] { 0, 111, 2, 113, 30, 97, 32, 127 });
            Assert(GomokuAi.FindMove(defense, GomokuDifficulty.Normal, CancellationToken.None, options).Index == 112, "defend fork");
        });
        Check("AI prioritizes a remote jump-three counterattack over its own slow fork", () => {
            var g = GomokuGame.Restore(new[] { 111, 0, 113, 2, 97, 4, 127, 6 });
            var options = Budget(12000); options.MaxDepth = 3;
            Assert(GomokuAi.FindMove(g, GomokuDifficulty.Normal, CancellationToken.None, options).Index == 3, "remote double winning hole threat");
        });
        Check("all difficulties open at the center without wasting a search", () => {
            foreach (GomokuDifficulty level in Enum.GetValues(typeof(GomokuDifficulty))) {
                var result = GomokuAi.FindMove(new GomokuGame(), level, CancellationToken.None, Budget(50000));
                Assert(result.Index == 112 && result.Nodes == 0, "center opening " + level + " got " + result.Index);
            }
        });
        Check("interrupted iteration returns exactly the last completed result", () => {
            var g = GomokuGame.Restore(new[] { 112, 111, 97, 127, 98, 128 });
            var small = Budget(300); small.MaxDepth = 6;
            var interrupted = GomokuAi.FindMove(g, GomokuDifficulty.Normal, CancellationToken.None, small);
            Assert(interrupted.CompletedDepth >= 1 && interrupted.CompletedDepth < 6 && interrupted.Nodes <= 300, "interrupted");
            var complete = Budget(100000); complete.MaxDepth = interrupted.CompletedDepth;
            var comparison = GomokuAi.FindMove(g, GomokuDifficulty.Normal, CancellationToken.None, complete);
            Assert(interrupted.Index == comparison.Index, "partial iteration cannot replace completed result");
        });
        Check("cancellation while searching throws and preserves source", () => {
            var g = GomokuGame.Restore(new[] { 112, 111, 97, 127, 98, 128 }); var before = g.ExportMoves();
            using (var cancel = new CancellationTokenSource()) {
                var options = Budget(10000000); options.MaxDepth = 12;
                cancel.CancelAfter(20);
                Throws<OperationCanceledException>(() => GomokuAi.FindMove(g, GomokuDifficulty.Hard, cancel.Token, options));
                Assert(g.ExportMoves().SequenceEqual(before), "active cancel unchanged");
            }
        });
        Check("random legal replays and undo match independent snapshots", () => {
            var random = new Random(2037);
            for (int run = 0; run < 20; run++) {
                var g = new GomokuGame();
                for (int n = 0; n < 100 && g.State == GomokuState.Playing; n++) {
                    int index; do { index = random.Next(225); } while (g.GetCell(index) != GomokuStone.Empty);
                    var before = g.Copy(); Assert(g.TryPlay(index), "random legal");
                    var restored = GomokuGame.Restore(g.ExportMoves());
                    Assert(restored.State == g.State && restored.ToPlay == g.ToPlay && restored.WinningLine.SequenceEqual(g.WinningLine), "replay state");
                    Assert(restored.Undo(1) && restored.ExportMoves().SequenceEqual(before.ExportMoves()), "undo random");
                    for (int cell = 0; cell < 225; cell++) Assert(restored.GetCell(cell) == before.GetCell(cell), "snapshot cell");
                }
            }
        });
        Check("AI rejects invalid options and reports terminal without move", () => {
            Throws<ArgumentNullException>(() => GomokuAi.FindMove(null, GomokuDifficulty.Easy, CancellationToken.None));
            Throws<ArgumentOutOfRangeException>(() => GomokuAi.FindMove(new GomokuGame(), (GomokuDifficulty)7, CancellationToken.None));
            var options = Budget(-1);
            Throws<ArgumentOutOfRangeException>(() => GomokuAi.FindMove(new GomokuGame(), GomokuDifficulty.Easy, CancellationToken.None, options));
            var terminal = GomokuGame.Restore(new[] { 0, 200, 1, 202, 2, 204, 3, 206, 4 });
            Assert(GomokuAi.FindMove(terminal, GomokuDifficulty.Hard, CancellationToken.None).Index == -1, "terminal");
        });
        Console.WriteLine("Gomoku: " + passed + " passed, " + failed + " failed");
        if (failed != 0) Environment.Exit(1);
    }
    private static void Benchmark()
    {
        var fixtures = new[] {
            new GomokuGame(),
            GomokuGame.Restore(new[] { 112, 111, 97, 127, 98, 128 }),
            GomokuGame.Restore(new[] { 108, 107, 109, 0, 110, 2, 66, 4, 81, 6 }),
            GomokuGame.Restore(new[] { 111, 0, 113, 2, 97, 4, 127, 6 })
        };
        // Warm the JIT before recording host .NET timings. These are not Android/IL2CPP measurements.
        GomokuAi.FindMove(fixtures[1], GomokuDifficulty.Easy, CancellationToken.None);
        Console.WriteLine("fixture,difficulty,index,depth,nodes,milliseconds,allocatedBytes");
        for (int f = 0; f < fixtures.Length; f++) foreach (GomokuDifficulty difficulty in Enum.GetValues(typeof(GomokuDifficulty))) {
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            var r = GomokuAi.FindMove(fixtures[f], difficulty, CancellationToken.None);
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Assert(r.Index >= 0 && fixtures[f].GetCell(r.Index) == GomokuStone.Empty, "benchmark legal");
            Console.WriteLine(f + "," + difficulty + "," + r.Index + "," + r.CompletedDepth + "," + r.Nodes + "," + r.ElapsedMilliseconds + "," + allocated);
        }
    }
    private static int Mirror(int i) { return i / 15 * 15 + 14 - i % 15; }
    private static GomokuSearchOptions Budget(long nodes) { return new GomokuSearchOptions { MaxNodes = nodes, TimeLimitMilliseconds = int.MaxValue, MaxDepth = 6, CandidateLimit = 12, TranspositionCapacity = 4096, VcfMaxDepth = 5 }; }
    private static void Check(string name, Action test) { try { test(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); } }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
}
