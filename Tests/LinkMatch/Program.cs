using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using XanaduYonder.Core.LinkMatch;

internal static class Program
{
    private static int passed, failed;
    private static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--legacy-fingerprints") {
            for (int n = 1; n <= 3; n++) Console.WriteLine("level-0" + n + " " + LegacyFingerprint(LinkMatchLevel.Get(n)));
            return;
        }
        if (args.Length == 1 && args[0] == "--benchmark") { Benchmark(); return; }
        Check("level catalog uses stable ids and approved settings", () => {
            Assert(LinkMatchLevel.All.Count == 5, "catalog must contain the approved five levels");
            var columns = new[] { 4, 6, 6, 8, 12 }; var rows = new[] { 6, 6, 8, 12, 16 };
            var kinds = new[] { 6, 9, 12, 24, 48 }; var seconds = new[] { 90, 120, 150, 300, 600 };
            for (int n = 1; n <= 5; n++) {
                var l = LinkMatchLevel.Get(n);
                Assert(l.Number == n && l.Id == "level-0" + n && ReferenceEquals(l, LinkMatchLevel.Get(l.Id)), "identity");
                Assert(l.Columns == columns[n - 1] && l.Rows == rows[n - 1], "dimensions");
                Assert(l.Kinds == kinds[n - 1] && l.ChallengeSeconds == seconds[n - 1], "rules");
                Assert(l.Columns * l.Rows == l.Kinds * 4, "exactly four copies per kind");
            }
            Throws<ArgumentOutOfRangeException>(() => LinkMatchLevel.Get(0));
            Throws<ArgumentOutOfRangeException>(() => LinkMatchLevel.Get(6));
            Throws<ArgumentException>(() => LinkMatchLevel.Get("LEVEL-01"));
            Throws<ArgumentException>(() => LinkMatchLevel.Get((string)null));
        });
        Check("catalog exposes count and actual maximums for shared consumers", () => {
            foreach (var expected in new[] { new KeyValuePair<string, int>("Count", 5), new KeyValuePair<string, int>("MaximumTileCount", 192), new KeyValuePair<string, int>("MaximumKinds", 48) }) {
                var property = typeof(LinkMatchLevel).GetProperty(expected.Key);
                Assert(property != null && property.PropertyType == typeof(int) && property.GetGetMethod().IsStatic, "missing public static int " + expected.Key);
                Assert((int)property.GetValue(null, null) == expected.Value, expected.Key + " value");
            }
        });
        Check("legacy seeded boards and complete path witnesses remain byte-for-byte stable", () => {
            var fingerprints = new[] { "2f88377da01eb2b411519602ddce6d55edb76676ebbaef40769d30400e9a320c", "d7b52eace46e394a717b9e9a3639eef3eed95a56a3beba5b04d4b1973b47c0d3", "6a864b807a080f1c36b466bfbfca6cdfcc6a7bd0cca89cb4339b64d8490b0149" };
            for (int n = 1; n <= 3; n++) Assert(LegacyFingerprint(LinkMatchLevel.Get(n)) == fingerprints[n - 1], "legacy generation or tie-break drift at level " + n);
        });
        Check("board validates length ids and exact pair counts", () => {
            var l = LinkMatchLevel.Get(1);
            Throws<ArgumentNullException>(() => new LinkMatchGame(null, new int[24]));
            Throws<ArgumentNullException>(() => new LinkMatchGame(l, null));
            Throws<ArgumentException>(() => new LinkMatchGame(l, new int[23]));
            var b = Empty(l); b[0] = -2; Throws<ArgumentException>(() => new LinkMatchGame(l, b));
            b[0] = 6; Throws<ArgumentException>(() => new LinkMatchGame(l, b));
            b[0] = 0; Throws<ArgumentException>(() => new LinkMatchGame(l, b));
            for (int i = 0; i < 6; i++) b[i] = 0;
            Throws<ArgumentException>(() => new LinkMatchGame(l, b));
        });
        Check("empty game is won and rejects interaction", () => {
            foreach (var l in LinkMatchLevel.All) {
            var g = new LinkMatchGame(l, Empty(l)); LinkMatchPath p;
            Assert(g.Won && g.Remaining == 0 && g.FindMatch() == null && !g.TryMatch(0, 1, out p) && p == null, "terminal");
            Assert(g.Shuffle(7).Solution.Count == 0 && g.Won, "empty shuffle");
            Throws<ArgumentOutOfRangeException>(() => g.TileAt(-1));
            Throws<ArgumentOutOfRangeException>(() => g.TileAt(l.Columns * l.Rows));
            }
        });
        Check("straight horizontal and vertical paths retain empty positions", () => {
            foreach (var pair in new[] { new[] { 5, 7 }, new[] { 5, 17 }, new[] { 5, 6 } }) {
                var l = LinkMatchLevel.Get(1); var b = Fixture(l, pair[0], pair[1]); var g = new LinkMatchGame(l, b); LinkMatchPath p;
                Assert(g.TryMatch(pair[0], pair[1], out p) && p.Turns == 0, "straight");
                ValidatePath(l, b, pair[0], pair[1], p);
                Assert(g.Won && g.Remaining == 0 && g.GetTiles().All(v => v == -1), "last pair and static holes");
            }
        });
        Check("one-turn corner has exact endpoints and Manhattan length", () => {
            var l = LinkMatchLevel.Get(1); var b = Fixture(l, 5, 14); LinkMatchPath p;
            Assert(new LinkMatchGame(l, b).TryMatch(5, 14, out p) && p.Turns == 1 && p.Steps == 3, "corner");
            ValidatePath(l, b, 5, 14, p);
        });
        Check("two-turn internal route avoids both blocked elbows", () => {
            var l = LinkMatchLevel.Get(2); var b = Fixture(l, 7, 28, 10, 25); LinkMatchPath p;
            Assert(new LinkMatchGame(l, b).TryMatch(7, 28, out p) && p.Turns == 2, "internal two bends");
            Assert(p.Points.All(v => v.X >= 0 && v.X < l.Columns && v.Y >= 0 && v.Y < l.Rows), "short route remains inside");
            ValidatePath(l, b, 7, 28, p);
        });
        Check("outer ring is used on all four edges without wrapping", () => {
            foreach (var l in LinkMatchLevel.All) {
            int w = l.Columns, bottom = (l.Rows - 1) * w;
            var cases = new[] { new[] { 0, 2, 1, w + 1 }, new[] { bottom, bottom + 2, bottom + 1, bottom - w + 1 }, new[] { 0, 2 * w, w, w + 1 }, new[] { w - 1, 3 * w - 1, 2 * w - 1, 2 * w - 2 } };
            foreach (var c in cases) {
                var b = Fixture(l, c[0], c[1], c[2], c[3]); LinkMatchPath p;
                Assert(new LinkMatchGame(l, b).TryMatch(c[0], c[1], out p) && p.Turns == 2 && p.Steps == 4, "edge");
                Assert(p.Points.Any(v => v.X == -1 || v.X == l.Columns || v.Y == -1 || v.Y == l.Rows), "outside");
                ValidatePath(l, b, c[0], c[1], p);
            }
            }
        });
        Check("three-turn corridor is rejected even when a longer route exists", () => {
            var l = LinkMatchLevel.Get(2); var holes = new HashSet<int>(new[] { 5, 13, 14, 15, 21, 27 });
            var blockers = Enumerable.Range(0, 36).Where(i => i != 7 && i != 28 && !holes.Contains(i)).ToArray();
            var b = Fixture(l, 7, 28, blockers); int turns, steps;
            Assert(Oracle(l, b, 7, 28, 3, out turns, out steps) && turns == 3, "fixture has precisely three turns");
            LinkMatchPath p; var g = new LinkMatchGame(l, b);
            Assert(!g.TryMatch(7, 28, out p) && p == null && g.GetTiles().SequenceEqual(b), "max two");
        });
        Check("surrounded tile cannot cross blockers", () => {
            var l = LinkMatchLevel.Get(2); var b = Fixture(l, 14, 28, 8, 13, 15, 20); LinkMatchPath p;
            Assert(!new LinkMatchGame(l, b).TryMatch(14, 28, out p) && p == null, "blocked");
        });
        Check("invalid indices same tile wrong fruit and holes do not mutate", () => {
            var l = LinkMatchLevel.Get(1); var b = Fixture(l, 0, 1, 2, 3); var g = new LinkMatchGame(l, b);
            foreach (var c in new[] { new[] { -1, 0 }, new[] { 0, 24 }, new[] { 0, 0 }, new[] { 0, 2 }, new[] { 0, 4 }, new[] { 4, 5 } }) {
                LinkMatchPath p; Assert(!g.TryMatch(c[0], c[1], out p) && p == null, "invalid pair");
                Assert(g.Remaining == 4 && g.GetTiles().SequenceEqual(b), "atomic rejection");
            }
        });
        Check("path first minimizes turns then steps with stable ties", () => {
            var l = LinkMatchLevel.Get(2); var b = Fixture(l, 7, 28, 10, 25); LinkMatchPath expected = null;
            int turns, steps; Assert(Oracle(l, b, 7, 28, 2, out turns, out steps), "oracle route");
            for (int run = 0; run < 20; run++) {
                LinkMatchPath p; Assert(new LinkMatchGame(l, b).TryMatch(7, 28, out p), "match");
                Assert(p.Turns == turns && p.Steps == steps, "optimum");
                if (expected != null) Assert(PathText(p) == PathText(expected), "tie stability");
                expected = p;
            }
            LinkMatchPath reversed; Assert(new LinkMatchGame(l, b).TryMatch(28, 7, out reversed), "reverse");
            Assert(reversed.Points.SequenceEqual(expected.Points.Reverse()), "canonical reverse");
        });
        Check("hint is a legal pair without mutating the game", () => {
            var l = LinkMatchLevel.Get(1); var b = Fixture(l, 0, 1, 2, 3); var g = new LinkMatchGame(l, b);
            var pair = g.FindMatch(); Assert(pair != null && pair.First == 0 && pair.Second == 1, "stable hint");
            Assert(g.GetTiles().SequenceEqual(b) && g.Remaining == 4, "hint read only");
            ValidatePath(l, b, pair.First, pair.Second, pair.Path);
        });
        Check("paired deadlock is distinct from winning and reshuffle produces a full rescue", () => {
            var l = LinkMatchLevel.Get(1); var b = Empty(l);
            b[0] = b[5] = 0; b[1] = b[4] = 1;
            var g = new LinkMatchGame(l, b);
            Assert(!g.Won && g.Remaining == 4 && g.FindMatch() == null, "even counts alone are not playable");
            var rescued = g.Shuffle(0); AssertPreserved(b, g.GetTiles());
            Assert(g.FindMatch() != null && !g.Won, "rescue has a legal first move"); Replay(l, rescued);
        });
        Check("board inputs and snapshots are defensively isolated", () => {
            var l = LinkMatchLevel.Get(1); var b = Fixture(l, 0, 1); var g = new LinkMatchGame(l, b);
            b[0] = -1; var copy = g.GetTiles(); copy[1] = -1;
            Assert(g.TileAt(0) == 0 && g.TileAt(1) == 0 && g.Remaining == 2, "defensive board");
            var p = g.FindMatch().Path;
            var list = p.Points as IList<LinkMatchPoint>;
            if (list != null) Throws<NotSupportedException>(() => list[0] = new LinkMatchPoint(99, 99));
        });
        Check("matcher agrees with an independent directional search", () => {
            var random = new Random(20261028); var l = LinkMatchLevel.Get(2);
            for (int run = 0; run < 400; run++) {
                var cells = Enumerable.Range(0, 36).OrderBy(i => random.Next()).ToArray();
                var b = Fixture(l, cells[0], cells[1], cells.Skip(2).Take(random.Next(17) * 2).ToArray());
                int turns, steps; bool found = Oracle(l, b, cells[0], cells[1], 2, out turns, out steps); LinkMatchPath p;
                Assert(new LinkMatchGame(l, b).TryMatch(cells[0], cells[1], out p) == found, "existence " + run);
                if (found) { Assert(p.Turns == turns && p.Steps == steps, "cost " + run); ValidatePath(l, b, cells[0], cells[1], p); }
            }
        });
        Check("dense paths agree with independent search and reject illegal submissions atomically", () => {
            var random = new Random(96192);
            foreach (int number in new[] { 4, 5 }) {
                var l = LinkMatchLevel.Get(number); int count = l.Columns * l.Rows;
                for (int run = 0; run < 160; run++) {
                    var cells = Enumerable.Range(0, count).OrderBy(i => random.Next()).ToArray();
                    var b = Fixture(l, cells[0], cells[1], cells.Skip(2).Take(random.Next((count - 4) / 2 + 1) * 2).ToArray());
                    var game = new LinkMatchGame(l, b); LinkMatchPath path;
                    Assert(!game.TryMatch(cells[0], cells[0], out path) && !game.TryMatch(-1, cells[1], out path) && !game.TryMatch(cells[0], count, out path), "invalid dense submission");
                    Assert(game.GetTiles().SequenceEqual(b), "invalid submission mutated dense board");
                    int turns, steps; bool found = Oracle(l, b, cells[0], cells[1], 2, out turns, out steps);
                    Assert(game.TryMatch(cells[0], cells[1], out path) == found, "dense route existence");
                    if (found) { Assert(path.Turns == turns && path.Steps == steps, "dense optimal route"); ValidatePath(l, b, cells[0], cells[1], path); }
                    else Assert(game.GetTiles().SequenceEqual(b), "blocked route mutated dense board");
                }
                var full = LinkMatchGenerator.Generate(l, uint.MaxValue); var input = full.Tiles; var live = new LinkMatchGame(l, input); input[0] = -1;
                var snapshot = live.GetTiles(); snapshot[count - 1] = -1;
                Assert(live.Remaining == count && live.TileAt(0) >= 0 && live.TileAt(count - 1) >= 0, "dense defensive copies");
                var illegal = Empty(l); illegal[0] = illegal[count - 1] = l.Kinds;
                Throws<ArgumentException>(() => new LinkMatchGame(l, illegal));
            }
        });
        Check("dense generation avoids per-candidate temporary arrays", () => {
            foreach (int number in new[] { 4, 5 }) {
                var level = LinkMatchLevel.Get(number);
                LinkMatchGenerator.Generate(level, 0);
                for (uint seed = 0; seed < 16; seed++) {
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    var deal = LinkMatchGenerator.Generate(level, seed);
                    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                    Assert(allocated <= level.Columns * level.Rows * 1536L, "dense generation allocated " + allocated + " bytes at " + level.Id + " seed " + seed);
                    GC.KeepAlive(deal);
                }
            }
        });
        Check("seeded generation is repeatable and fully replayable for every level", () => {
            foreach (var l in LinkMatchLevel.All) {
                var layouts = new HashSet<string>();
                for (uint seed = 0; seed < 100; seed++) {
                    var deal = LinkMatchGenerator.Generate(l, seed); var repeat = LinkMatchGenerator.Generate(l, seed);
                    Assert(deal.Tiles.SequenceEqual(repeat.Tiles), "deterministic tiles");
                    Assert(string.Join(";", deal.Solution.Select(p => p.First + "," + p.Second)) == string.Join(";", repeat.Solution.Select(p => p.First + "," + p.Second)), "deterministic solution");
                    Assert(deal.Tiles.All(v => v >= 0) && Enumerable.Range(0, l.Kinds).All(k => deal.Tiles.Count(v => v == k) == 4), "four of each");
                    Replay(l, deal); layouts.Add(string.Join(",", deal.Tiles));
                }
                Assert(layouts.Count > 90, "seed variety");
            }
        });
        Check("sparse reshuffles preserve every hole and exact remaining multiset", () => {
            var random = new Random(47);
            foreach (var l in LinkMatchLevel.All) for (int run = 0; run < 80; run++) {
                var occupied = Enumerable.Range(0, l.Columns * l.Rows).OrderBy(i => random.Next()).Take(2 * random.Next(l.Columns * l.Rows / 2 + 1)).ToArray();
                var b = Empty(l); for (int i = 0; i < occupied.Length; i++) b[occupied[i]] = i / 4;
                var original = (int[])b.Clone(); var deal = LinkMatchGenerator.Reshuffle(l, b, (uint)run);
                Assert(b.SequenceEqual(original), "input immutable"); AssertPreserved(b, deal.Tiles); Replay(l, deal);
                var g = new LinkMatchGame(l, b); var shuffled = g.Shuffle((uint)run);
                Assert(g.GetTiles().SequenceEqual(deal.Tiles) && shuffled.Tiles.SequenceEqual(deal.Tiles) && g.Remaining == occupied.Length, "game shuffle");
            }
        });
        Check("reshuffle handles separated singleton rows and last pair", () => {
            foreach (var l in LinkMatchLevel.All) {
            var b = Empty(l);
            for (int row = 0; row < l.Rows; row++) b[row * l.Columns + row % l.Columns] = row / 2;
            var deal = LinkMatchGenerator.Reshuffle(l, b, uint.MaxValue); AssertPreserved(b, deal.Tiles); Replay(l, deal);
            b = Fixture(l, 0, b.Length - 1); deal = LinkMatchGenerator.Reshuffle(l, b, 0); Replay(l, deal);
            }
        });
        Check("generated snapshots and solution collections cannot alter the deal", () => {
            var l = LinkMatchLevel.Get(1); var deal = LinkMatchGenerator.Generate(l, 3);
            var original = deal.Tiles; var changed = deal.Tiles; changed[0] = -1;
            Assert(deal.Tiles.SequenceEqual(original), "deal tiles");
            var list = deal.Solution as IList<LinkMatchPair>;
            if (list != null) Throws<NotSupportedException>(() => list[0] = null);
            Replay(l, deal);
        });
        Check("generation rejects invalid inputs before constructing a deal", () => {
            Throws<ArgumentNullException>(() => LinkMatchGenerator.Generate(null, 1));
            Throws<ArgumentNullException>(() => LinkMatchGenerator.Reshuffle(LinkMatchLevel.Get(1), null, 1));
            Throws<ArgumentException>(() => LinkMatchGenerator.Reshuffle(LinkMatchLevel.Get(1), new int[24], 1));
        });
        Console.WriteLine("LinkMatch: " + passed + " passed, " + failed + " failed");
        if (failed != 0) Environment.Exit(1);
    }

    private static string LegacyFingerprint(LinkMatchLevel level)
    {
        var content = new StringBuilder();
        for (uint seed = 0; seed < 100; seed++) {
            var deal = LinkMatchGenerator.Generate(level, seed);
            content.Append(string.Join(",", deal.Tiles)).Append('|');
            foreach (var pair in deal.Solution) content.Append(pair.First).Append(',').Append(pair.Second).Append(':').Append(PathText(pair.Path)).Append('|');
            content.Append('\n');
        }
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(content.ToString()))).Replace("-", "").ToLowerInvariant();
    }
    private static void Benchmark()
    {
        Console.WriteLine("LinkMatch .NET " + Environment.Version + " latency/allocation; 128 fixed seeds per case; no Unity/device claims.");
        foreach (int number in new[] { 4, 5 }) {
            var level = LinkMatchLevel.Get(number);
            var deals = Enumerable.Range(0, 128).Select(seed => LinkMatchGenerator.Generate(level, (uint)seed)).ToArray();
            var games = deals.Select(deal => new LinkMatchGame(level, deal.Tiles)).ToArray();
            var sparse = deals.Select(deal => {
                var game = new LinkMatchGame(level, deal.Tiles);
                foreach (var pair in deal.Solution.Take(deal.Solution.Count / 2)) { LinkMatchPath path; Assert(game.TryMatch(pair.First, pair.Second, out path), "benchmark sparse fixture"); }
                return game.GetTiles();
            }).ToArray();
            Measure(level, "Generate", i => LinkMatchGenerator.Generate(level, (uint)i));
            Measure(level, "FindMatch/full", i => games[i].FindMatch());
            Measure(level, "Reshuffle/full", i => LinkMatchGenerator.Reshuffle(level, deals[i].Tiles, (uint)i));
            Measure(level, "Reshuffle/half", i => LinkMatchGenerator.Reshuffle(level, sparse[i], (uint)i));
        }
    }
    private static void Measure(LinkMatchLevel level, string name, Func<int, object> operation)
    {
        for (int i = 0; i < 16; i++) GC.KeepAlive(operation(i));
        var milliseconds = new double[128]; var allocated = new long[128];
        for (int i = 0; i < milliseconds.Length; i++) {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
            object result = operation(i);
            milliseconds[i] = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            allocated[i] = GC.GetAllocatedBytesForCurrentThread() - bytes; GC.KeepAlive(result);
        }
        Array.Sort(milliseconds);
        Console.WriteLine(level.Id + " " + name + ": median=" + milliseconds[64].ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "ms p95=" + milliseconds[121].ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "ms max=" + milliseconds[127].ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "ms mean-bytes=" + ((long)allocated.Average()) + " max-bytes=" + allocated.Max());
    }

    private static int[] Empty(LinkMatchLevel l) { return Enumerable.Repeat(-1, l.Columns * l.Rows).ToArray(); }
    private static int[] Fixture(LinkMatchLevel l, int a, int b, params int[] blockers)
    {
        Assert(blockers.Length % 2 == 0, "fixture blockers need pairs");
        var result = Empty(l); result[a] = result[b] = 0;
        for (int i = 0; i < blockers.Length; i++) result[blockers[i]] = 1 + i / 4;
        return result;
    }
    private static void Replay(LinkMatchLevel l, LinkMatchDeal deal)
    {
        var g = new LinkMatchGame(l, deal.Tiles); Assert(deal.Solution.Count * 2 == g.Remaining, "witness length");
        foreach (var pair in deal.Solution) {
            var before = g.GetTiles(); LinkMatchPath p;
            Assert(g.TryMatch(pair.First, pair.Second, out p), "witness pair " + pair.First + "," + pair.Second);
            Assert(PathText(p) == PathText(pair.Path), "witness actual production route");
            ValidatePath(l, before, pair.First, pair.Second, p);
        }
        Assert(g.Won && g.Remaining == 0, "witness clears all");
    }
    private static void AssertPreserved(int[] before, int[] after)
    {
        Assert(before.OrderBy(v => v).SequenceEqual(after.OrderBy(v => v)), "multiset");
        Assert(before.Select(v => v == -1).SequenceEqual(after.Select(v => v == -1)), "static hole mask");
    }
    private static string PathText(LinkMatchPath path) { return string.Join(";", path.Points.Select(p => p.X + "," + p.Y)); }
    private static void ValidatePath(LinkMatchLevel l, int[] b, int a, int z, LinkMatchPath p)
    {
        Assert(p != null && p.Turns >= 0 && p.Turns <= 2 && p.Points.Count == p.Turns + 2, "corners");
        Assert(p.Points[0].Equals(new LinkMatchPoint(a % l.Columns, a / l.Columns)) && p.Points[p.Points.Count - 1].Equals(new LinkMatchPoint(z % l.Columns, z / l.Columns)), "endpoints");
        int steps = 0, lastDx = 0, lastDy = 0;
        for (int i = 1; i < p.Points.Count; i++) {
            var start = p.Points[i - 1]; var end = p.Points[i];
            int dx = Math.Sign(end.X - start.X), dy = Math.Sign(end.Y - start.Y);
            Assert(Math.Abs(dx) + Math.Abs(dy) == 1 && (i == 1 || dx != lastDx || dy != lastDy), "orthogonal bend");
            int x = start.X, y = start.Y;
            do {
                x += dx; y += dy; steps++;
                Assert(x >= -1 && x <= l.Columns && y >= -1 && y <= l.Rows, "one outer ring");
                if (x >= 0 && x < l.Columns && y >= 0 && y < l.Rows) {
                    int index = y * l.Columns + x;
                    Assert(index == z || b[index] == -1, "route cannot cross tile " + index);
                }
            } while (x != end.X || y != end.Y);
            lastDx = dx; lastDy = dy;
        }
        Assert(steps == p.Steps, "step length");
    }
    // Independent cell-by-cell directional BFS; production enumerates geometric bends.
    private static bool Oracle(LinkMatchLevel l, int[] b, int a, int z, int maxTurns, out int turns, out int steps)
    {
        int width = l.Columns + 2, height = l.Rows + 2; var distance = new int[width, height, 4, maxTurns + 1];
        var queue = new Queue<int[]>(); int[] dx = { 1, 0, -1, 0 }, dy = { 0, 1, 0, -1 };
        int sx = a % l.Columns + 1, sy = a / l.Columns + 1, tx = z % l.Columns + 1, ty = z / l.Columns + 1;
        for (int d = 0; d < 4; d++) { distance[sx, sy, d, 0] = 1; queue.Enqueue(new[] { sx, sy, d, 0 }); }
        while (queue.Count > 0) {
            var state = queue.Dequeue(); int x = state[0], y = state[1], direction = state[2], t = state[3];
            if (x == tx && y == ty) continue;
            for (int d = 0; d < 4; d++) {
                int nt = t + (d == direction ? 0 : 1), nx = x + dx[d], ny = y + dy[d];
                if (nt > maxTurns || nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                int index = (ny - 1) * l.Columns + nx - 1;
                if (nx > 0 && nx <= l.Columns && ny > 0 && ny <= l.Rows && index != z && b[index] != -1) continue;
                if (distance[nx, ny, d, nt] != 0) continue;
                distance[nx, ny, d, nt] = distance[x, y, direction, t] + 1; queue.Enqueue(new[] { nx, ny, d, nt });
            }
        }
        for (turns = 0; turns <= maxTurns; turns++) {
            steps = int.MaxValue;
            for (int d = 0; d < 4; d++) if (distance[tx, ty, d, turns] > 0) steps = Math.Min(steps, distance[tx, ty, d, turns] - 1);
            if (steps < int.MaxValue) return true;
        }
        steps = -1; return false;
    }
    private static void Check(string name, Action body) { try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Throws<T>(Action body) where T : Exception { try { body(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
}
