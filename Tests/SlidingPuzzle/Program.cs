using System;
using System.Collections.Generic;
using XanaduYonder.Core.SlidingPuzzle;

internal static class Program
{
    private static int passed;
    private static readonly int[] First = { 1, 2, 3, 4, 0, 5, 7, 8, 6 };

    private static void Main()
    {
        Check("only adjacent single tiles move; illegal input has no effects", AdjacentMoves);
        Check("horizontal edges never wrap", NoRowWrapping);
        Check("move counts and undo retain full active trajectory", UndoAndCounters);
        Check("winning move locks move and undo", VictoryIsTerminal);
        Check("constructor and every exposed array have defensive copies", DefensiveCopies);
        Check("invalid sizes, permutations and unsolvable positions are rejected", RejectInvalidBoards);
        Check("even and odd boards use correct solvability parity", SolvabilityParity);
        Check("snapshot restores replayable states including wins and assistance", SnapshotRoundTrip);
        Check("restore rejects malformed snapshots and forged replay", RejectInvalidSnapshots);
        Check("restore cannot replay any move after reaching victory", RejectAfterWinHistory);
        Check("safety boundary retains complete history and locks further actions", ActionSafetyLimit);
        Check("seeded generator is stable, nonterminal, valid and diverse", GeneratedBoards);
        Check("catalog contains eighteen unique fixed levels and defensive arrays", CatalogShape);
        Check("every catalog witness legally reaches victory", ReplayAllCatalogSolutions);
        Check("independent exhaustive BFS proves first chapter optimal distances", OptimalFirstChapter);
        Console.WriteLine("Sliding puzzle: " + passed + " passed");
    }

    private static void AdjacentMoves()
    {
        var game = new SlidingPuzzleGame(3, First);
        foreach (int tile in new[] { -1, 0, 1, 3, 7, 9, int.MaxValue })
            Assert(!game.TryMove(tile), "Accepted invalid/nonadjacent tile " + tile);
        Equal(0, game.Moves); Same(First, game.GetCells()); Assert(!game.CanUndo);
        Assert(game.TryMove(5)); Equal(1, game.Moves);
        Same(new[] { 1, 2, 3, 4, 5, 0, 7, 8, 6 }, game.GetCells());
        Same(new[] { 5 }, game.GetSnapshot().History);
    }

    private static void NoRowWrapping()
    {
        var game = new SlidingPuzzleGame(3, new[] { 1, 2, 0, 4, 5, 3, 7, 8, 6 });
        Assert(!game.TryMove(4)); Equal(0, game.Moves);
        Assert(game.TryMove(3)); Equal(1, game.Moves);
    }

    private static void UndoAndCounters()
    {
        var game = new SlidingPuzzleGame(3, First);
        Assert(!game.Undo()); Assert(!game.Assisted);
        Assert(game.TryMove(4)); Assert(game.TryMove(1)); Equal(2, game.Moves);
        Assert(game.Undo()); Equal(3, game.Moves); Equal(1, game.UndoCount);
        Same(new[] { 4 }, game.GetSnapshot().History); Assert(game.Assisted);
        Assert(game.Undo()); Same(First, game.GetCells()); Equal(4, game.Moves);
        Assert(!game.CanUndo); Assert(!game.Undo()); Equal(4, game.Moves);
        Assert(game.TryMove(5)); Assert(game.TryMove(6)); Assert(game.Won);
        Equal(6, game.Moves); Equal(2, game.UndoCount);
        Same(new[] { 5, 6 }, game.GetSnapshot().History);
    }

    private static void VictoryIsTerminal()
    {
        var game = new SlidingPuzzleGame(3, First);
        Assert(game.TryMove(5)); Assert(game.TryMove(6)); Assert(game.Won);
        Assert(!game.CanUndo); Assert(!game.Undo()); Assert(!game.TryMove(6));
        Equal(2, game.Moves); Equal(0, game.UndoCount);
        Assert(!game.Assisted); Same(Goal(3), game.GetCells());
        var complete = new SlidingPuzzleGame(3, Goal(3));
        Assert(complete.Won); Assert(!complete.TryMove(8)); Assert(!complete.Undo());
        Assert(SlidingPuzzleGame.Restore(complete.GetSnapshot()).Won);
    }

    private static void DefensiveCopies()
    {
        var input = (int[])First.Clone(); var game = new SlidingPuzzleGame(3, input);
        input[0] = 0; game.GetInitial()[0] = 0; game.GetCells()[0] = 0;
        Same(First, game.GetInitial()); Same(First, game.GetCells());
        Assert(game.TryMove(4)); var snapshot = game.GetSnapshot(); var copy = snapshot.Copy();
        snapshot.InitialCells[0] = 0; snapshot.Cells[0] = 0; snapshot.History[0] = 0;
        Equal(1, copy.InitialCells[0]); Equal(1, copy.Cells[0]); Equal(4, copy.History[0]);
        var restored = SlidingPuzzleGame.Restore(copy);
        copy.InitialCells[0] = 0; copy.Cells[0] = 0; copy.History[0] = 0;
        Assert(restored.Undo()); Same(First, restored.GetCells());
        Same(new[] { 4 }, game.GetSnapshot().History);
        var missing = new SlidingPuzzleSnapshot(); Assert(missing.Copy().Cells == null);
    }

    private static void RejectInvalidBoards()
    {
        foreach (int size in new[] { int.MinValue, -1, 0, 1, 2, 6, 100, int.MaxValue })
        {
            Assert(!SlidingPuzzleGame.IsSolvable(size, First));
            Reject(() => new SlidingPuzzleGame(size, First));
        }
        foreach (var cells in new[] { null, new int[0], new[] { 0, 1 }, new int[9],
            new[] { 1, 2, 3, 4, 0, 5, 7, 8, 9 }, new[] { 1, 2, 3, 4, 0, 5, 7, 8, -1 },
            new[] { 1, 2, 3, 4, 0, 5, 7, 7, 6 }, new[] { 2, 1, 3, 4, 0, 5, 7, 8, 6 } })
        {
            Assert(!SlidingPuzzleGame.IsSolvable(3, cells));
            Reject(() => new SlidingPuzzleGame(3, cells));
        }
    }

    private static void SolvabilityParity()
    {
        for (int size = 3; size <= 5; size++)
        {
            var cells = Goal(size); Assert(SlidingPuzzleGame.IsSolvable(size, cells));
            Swap(cells, 0, 1); Assert(!SlidingPuzzleGame.IsSolvable(size, cells));
            Swap(cells, 0, 1); Swap(cells, cells.Length - 1, cells.Length - 1 - size);
            Assert(SlidingPuzzleGame.IsSolvable(size, cells));
            Swap(cells, 0, 1); Assert(!SlidingPuzzleGame.IsSolvable(size, cells));
        }
    }

    private static void SnapshotRoundTrip()
    {
        var game = new SlidingPuzzleGame(3, First);
        Assert(game.TryMove(4)); Assert(game.TryMove(1)); Assert(game.Undo());
        var restored = SlidingPuzzleGame.Restore(game.GetSnapshot());
        Same(game.GetCells(), restored.GetCells()); Same(game.GetInitial(), restored.GetInitial());
        Equal(game.Moves, restored.Moves); Equal(game.UndoCount, restored.UndoCount);
        Assert(restored.CanUndo && restored.Assisted && !restored.Won);
        Assert(restored.Undo()); Same(First, restored.GetCells());
        Assert(restored.TryMove(5)); Assert(restored.TryMove(6));
        var won = SlidingPuzzleGame.Restore(restored.GetSnapshot());
        Assert(won.Won && won.Assisted && !won.CanUndo); Equal(6, won.Moves);
    }

    private static void RejectInvalidSnapshots()
    {
        Reject(() => SlidingPuzzleGame.Restore(null));
        Reject(() => SlidingPuzzleGame.Restore(new SlidingPuzzleSnapshot()));
        var game = new SlidingPuzzleGame(3, First); Assert(game.TryMove(4));
        Action<Action<SlidingPuzzleSnapshot>> invalid = change => {
            var snapshot = game.GetSnapshot(); change(snapshot);
            Reject(() => SlidingPuzzleGame.Restore(snapshot));
        };
        invalid(s => s.Size = 4); invalid(s => s.InitialCells = null);
        invalid(s => s.Cells = null); invalid(s => s.History = null);
        invalid(s => s.InitialCells[1] = 1); invalid(s => s.Cells[1] = 1);
        invalid(s => s.InitialCells = Goal(3)); invalid(s => s.Cells = First);
        invalid(s => s.History = new[] { 1 }); invalid(s => s.History = new[] { 0 });
        invalid(s => s.History = new[] { 4, 2 }); invalid(s => s.Moves = -1);
        invalid(s => s.Moves = 2); invalid(s => s.UndoCount = -1);
        invalid(s => s.UndoCount = int.MaxValue); invalid(s => s.Moves = int.MaxValue);
        invalid(s => { s.Moves = 100001; s.UndoCount = 50000; });
        invalid(s => { s.InitialCells = Goal(3); s.Cells = Goal(3); s.History = new int[0]; s.Moves = 2; s.UndoCount = 1; });
    }

    private static void RejectAfterWinHistory()
    {
        var snapshot = new SlidingPuzzleSnapshot { Size = 3, InitialCells = (int[])First.Clone(),
            Cells = new[] { 1, 2, 3, 4, 5, 0, 7, 8, 6 }, History = new[] { 5, 6, 6 }, Moves = 3 };
        Reject(() => SlidingPuzzleGame.Restore(snapshot));
    }

    private static void ActionSafetyLimit()
    {
        Equal(100000, SlidingPuzzleGame.MaxActions);
        var game = new SlidingPuzzleGame(3, First);
        for (int i = 0; i < SlidingPuzzleGame.MaxActions - 1; i++) Assert(game.TryMove(4));
        Assert(!game.AtActionLimit); Assert(game.CanUndo); Assert(game.Undo());
        Assert(game.AtActionLimit); Assert(!game.CanUndo); Assert(!game.TryMove(4)); Assert(!game.Undo());
        Equal(SlidingPuzzleGame.MaxActions, game.Moves); Equal(1, game.UndoCount);
        Equal(SlidingPuzzleGame.MaxActions - 2, game.GetSnapshot().History.Length);
        var restored = SlidingPuzzleGame.Restore(game.GetSnapshot()); Assert(restored.AtActionLimit);
        Same(game.GetCells(), restored.GetCells());
    }

    private static void GeneratedBoards()
    {
        foreach (int size in new[] { 2, 6, int.MaxValue }) Reject(() => SlidingPuzzleGenerator.Generate(size, 0));
        for (int size = 3; size <= 5; size++)
        {
            var unique = new HashSet<string>();
            foreach (int seed in new[] { 0, 1, -1, 2, 42, int.MinValue, int.MaxValue })
            {
                var cells = SlidingPuzzleGenerator.Generate(size, seed);
                Same(cells, SlidingPuzzleGenerator.Generate(size, seed));
                Assert(SlidingPuzzleGame.IsSolvable(size, cells));
                Assert(!new SlidingPuzzleGame(size, cells).Won);
                unique.Add(Key(cells));
                cells[0] = -1;
                Assert(SlidingPuzzleGame.IsSolvable(size, SlidingPuzzleGenerator.Generate(size, seed)));
            }
            Assert(unique.Count >= 6, "Seeds should produce varied boards");
        }
    }

    private static void CatalogShape()
    {
        Equal(3, SlidingPuzzleCatalog.ChapterCount); Equal(6, SlidingPuzzleCatalog.LevelsPerChapter);
        Equal(18, SlidingPuzzleCatalog.Count); Assert(SlidingPuzzleCatalog.ContentVersion == "1");
        Reject(() => SlidingPuzzleCatalog.Get(-1)); Reject(() => SlidingPuzzleCatalog.Get(18));
        var unique = new HashSet<string>();
        for (int index = 0; index < 18; index++)
        {
            var level = SlidingPuzzleCatalog.Get(index);
            Equal(index, level.Index); Equal(index / 6, level.ChapterIndex); Equal(index % 6 + 1, level.Number);
            Equal(3 + index / 6, level.Size); Assert(!new SlidingPuzzleGame(level.Size, level.InitialCells).Won);
            Assert(unique.Add(level.Size + ":" + Key(level.InitialCells)));
            Assert(level.Solution.Length > 0);
            level.InitialCells[0] = -1; level.Solution[0] = -1;
            Assert(level.InitialCells[0] >= 0); Assert(level.Solution[0] > 0);
            if (index >= 6) Assert(!level.OptimalMoves.HasValue);
        }
        Same(First, SlidingPuzzleCatalog.Get(0).InitialCells);
        Same(new[] { 5, 6 }, SlidingPuzzleCatalog.Get(0).Solution);
    }

    private static void ReplayAllCatalogSolutions()
    {
        for (int index = 0; index < SlidingPuzzleCatalog.Count; index++)
        {
            var level = SlidingPuzzleCatalog.Get(index); var game = new SlidingPuzzleGame(level.Size, level.InitialCells);
            var solution = level.Solution;
            for (int step = 0; step < solution.Length; step++)
            {
                Assert(!game.Won, "Early victory in level " + index);
                Assert(game.TryMove(solution[step]), "Illegal witness in level " + index + " step " + step);
            }
            Assert(game.Won, "Unsolved level " + index); Equal(solution.Length, game.Moves);
            Same(game.GetCells(), SlidingPuzzleGame.Restore(game.GetSnapshot()).GetCells());
        }
    }

    // This search uses its own adjacency and string states, independent of production move/generator code.
    private static void OptimalFirstChapter()
    {
        var distance = new Dictionary<string, int>(); var queue = new Queue<string>();
        const string goal = "123456780"; distance.Add(goal, 0); queue.Enqueue(goal);
        while (queue.Count > 0)
        {
            string state = queue.Dequeue(); int blank = state.IndexOf('0'); int d = distance[state];
            for (int candidate = 0; candidate < 9; candidate++)
            {
                if (Math.Abs(blank / 3 - candidate / 3) + Math.Abs(blank % 3 - candidate % 3) != 1) continue;
                var chars = state.ToCharArray(); chars[blank] = chars[candidate]; chars[candidate] = '0';
                string next = new string(chars); if (distance.ContainsKey(next)) continue;
                distance.Add(next, d + 1); queue.Enqueue(next);
            }
        }
        Equal(181440, distance.Count);
        var expected = new[] { 2, 4, 6, 8, 12, 16 };
        for (int i = 0; i < 6; i++)
        {
            var level = SlidingPuzzleCatalog.Get(i); var chars = new char[9]; var cells = level.InitialCells;
            for (int j = 0; j < chars.Length; j++) chars[j] = (char)('0' + cells[j]);
            Equal(expected[i], distance[new string(chars)]);
            Assert(level.OptimalMoves.HasValue); Equal(expected[i], level.OptimalMoves.Value);
            Equal(expected[i], level.Solution.Length);
        }
    }

    private static int[] Goal(int size)
    {
        var cells = new int[size * size]; for (int i = 0; i < cells.Length - 1; i++) cells[i] = i + 1; return cells;
    }
    private static void Swap(int[] cells, int a, int b) { int t = cells[a]; cells[a] = cells[b]; cells[b] = t; }
    private static string Key(int[] cells) { return string.Join(",", cells); }
    private static void Check(string name, Action run) { run(); passed++; Console.WriteLine("PASS " + name); }
    private static void Equal(int expected, int actual) { Assert(expected == actual, "Expected " + expected + ", actual " + actual); }
    private static void Same(int[] expected, int[] actual) { Assert(expected != null && actual != null); Equal(expected.Length, actual.Length); for (int i = 0; i < expected.Length; i++) Equal(expected[i], actual[i]); }
    private static void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
    private static void Reject(Action run) { try { run(); } catch (ArgumentException) { return; } throw new Exception("Expected ArgumentException"); }
}
