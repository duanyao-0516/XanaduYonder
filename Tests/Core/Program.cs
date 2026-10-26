using System;
using System.Collections.Generic;
using XanaduYonder.Core;

internal static class Program
{
    private static int passed;
    private static int failed;

    private static int Main()
    {
        Run("New game creates exactly two tiles", NewGameCreatesTwoTiles);
        Run("New game rejects null randomness", () => Throws<ArgumentNullException>(() => new Game2048(null)));
        Run("Restore rejects null randomness", () => Throws<ArgumentNullException>(() => new Game2048(Snapshot(), null)));
        Run("Restoring uses no random numbers", RestoreUsesNoRandomness);
        Run("Snapshot exposes the stable serialization contract", SnapshotContract);
        Run("Left move compresses and merges", () => DirectionMerges(Direction.Left, new[] { 0, 3 }, 0));
        Run("Right move compresses and merges", () => DirectionMerges(Direction.Right, new[] { 0, 3 }, 3));
        Run("Up move compresses and merges", () => DirectionMerges(Direction.Up, new[] { 0, 12 }, 0));
        Run("Down move compresses and merges", () => DirectionMerges(Direction.Down, new[] { 0, 12 }, 12));
        Run("Four equal tiles merge into two pairs", FourEqualTilesMergeIntoTwoPairs);
        Run("A newly merged tile cannot merge again", NewlyMergedTileCannotMergeAgain);
        Run("Odd runs merge from the movement edge", OddRunsMergeFromMovementEdge);
        Run("Tiles compress across gaps in their original order", CompressAcrossGaps);
        Run("Effective move spawns exactly one tile", EffectiveMoveSpawnsExactlyOneTile);
        Run("No-op move does not consume random numbers", NoOpDoesNotConsumeRandomness);
        Run("Spawn roll 0 creates two", () => SpawnRoll(0, 2));
        Run("Spawn roll 89 creates two", () => SpawnRoll(89, 2));
        Run("Spawn roll 90 creates four", () => SpawnRoll(90, 4));
        Run("Spawn roll 99 creates four", () => SpawnRoll(99, 4));
        Run("Spawn position is selected among all empty cells", SpawnSelectsEmptyCell);
        Run("Score supports values above int.MaxValue", ScoreUsesLong);
        Run("Score saturates safely at long.MaxValue", ScoreSaturates);
        Run("Invalid directions reject without mutating state", InvalidDirectionsReject);
        Run("Largest tiles do not merge or overflow", MaximumTilesDoNotMerge);
        Run("Winning is derived when a 2048 tile is created", WinningIsDerived);
        Run("Winner may continue and acknowledge once", WinnerMayContinue);
        Run("Acknowledging before winning does not set a false flag", EarlyAcknowledgementDoesNothing);
        Run("Restoring an acknowledged winner preserves its flag", RestoreAcknowledgedWinner);
        Run("Full board without pairs is game over", DeadBoardIsGameOver);
        Run("An empty cell keeps the game alive", EmptyCellKeepsGameAlive);
        Run("Horizontal merge keeps a full board alive", HorizontalMergeKeepsGameAlive);
        Run("Vertical merge keeps a full board alive", VerticalMergeKeepsGameAlive);
        Run("Capped adjacent tiles do not keep a full board alive", CappedTilesAreNotMergeable);
        Run("Valid snapshot accepts all supported tile powers", SupportedPowersAreValid);
        Run("Invalid snapshots are rejected with a reason", InvalidSnapshotsRejected);
        Run("Invalid snapshot cannot be restored", InvalidSnapshotCannotBeRestored);
        Run("Restoration defensively copies the caller's cells", RestoreCopiesCells);
        Run("Snapshot reads defensively copy the internal cells", SnapshotCopiesCells);
        Run("Cell indices are range checked", CellIndicesAreRangeChecked);
        Run("Motions describe every original tile and merge partner", MotionsDescribeEveryTile);
        Run("No-op motions include stationary tiles", NoOpMotionsIncludeStationaryTiles);
        Run("Up and down motions use row-major indices", VerticalMotionsUseRowMajorIndices);
        Run("Repeated play preserves valid snapshot invariants", RepeatedPlayPreservesInvariants);

        Console.WriteLine("\n" + passed + " passed, " + failed + " failed, " + (passed + failed) + " total.");
        return failed == 0 ? 0 : 1;
    }

    private static void NewGameCreatesTwoTiles()
    {
        var random = new SequenceRandom(0, 0, 14, 90);
        var game = new Game2048(random);
        Equal(2, CountTiles(game.GetSnapshot().cells));
        Equal(2, game.GetCell(0));
        Equal(4, game.GetCell(15));
        Equal(0L, game.Score);
        Equal(false, game.HasWon);
        Equal(false, game.HasAcknowledgedWin);
        Equal(false, game.IsGameOver);
        SequenceEqual(new[] { 16, 100, 15, 100 }, random.RequestedBounds);
    }

    private static void RestoreUsesNoRandomness()
    {
        var random = new SequenceRandom();
        var game = new Game2048(Snapshot(2, 4, 8, 16), random);
        Equal(0, random.Calls);
        Equal(16, game.GetCell(3));
    }

    private static void SnapshotContract()
    {
        var snapshot = new GameSnapshot();
        Equal(1, snapshot.schemaVersion);
        Equal("2048", snapshot.gameId);
        Equal(true, Attribute.IsDefined(typeof(GameSnapshot), typeof(SerializableAttribute)));
        foreach (var name in new[] { "schemaVersion", "gameId", "cells", "score", "winAcknowledged" })
            Equal(true, typeof(GameSnapshot).GetField(name) != null, "Missing public DTO field " + name);
        string reason;
        Equal(true, GameSnapshot.TryValidate(Snapshot(), out reason));
        Equal(true, string.IsNullOrEmpty(reason));
    }

    private static void DirectionMerges(Direction direction, int[] sourceIndices, int destination)
    {
        var snapshot = Snapshot();
        foreach (var index in sourceIndices) snapshot.cells[index] = 2;
        var game = new Game2048(snapshot, new SequenceRandom(0, 0));
        var result = game.Move(direction);
        var expected = new int[16];
        expected[destination] = 4;
        AssertMove(game, result, expected, 4);
        Equal(2, result.Motions.Count);
        foreach (var motion in result.Motions)
        {
            Equal(destination, motion.ToIndex);
            Equal(true, motion.Merged);
        }
    }

    private static void FourEqualTilesMergeIntoTwoPairs()
    {
        var game = Restore(2, 2, 2, 2);
        var result = game.Move(Direction.Left);
        AssertMove(game, result, Cells(4, 4), 8);
        Equal(8L, game.Score);
    }

    private static void NewlyMergedTileCannotMergeAgain()
    {
        var game = Restore(2, 2, 4);
        AssertMove(game, game.Move(Direction.Left), Cells(4, 4), 4);
        Equal(4L, game.Score);
    }

    private static void OddRunsMergeFromMovementEdge()
    {
        var left = Restore(2, 2, 2);
        AssertMove(left, left.Move(Direction.Left), Cells(4, 2), 4);
        var right = Restore(2, 2, 2);
        AssertMove(right, right.Move(Direction.Right), Cells(0, 0, 2, 4), 4);
    }

    private static void CompressAcrossGaps()
    {
        var game = Restore(2, 0, 4, 2);
        AssertMove(game, game.Move(Direction.Left), Cells(2, 4, 2), 0);
    }

    private static void EffectiveMoveSpawnsExactlyOneTile()
    {
        var random = new SequenceRandom(0, 0);
        var game = new Game2048(Snapshot(0, 2, 4), random);
        var result = game.Move(Direction.Left);
        AssertMove(game, result, Cells(2, 4), 0);
        Equal(3, CountTiles(game.GetSnapshot().cells));
        Equal(2, random.Calls);
    }

    private static void NoOpDoesNotConsumeRandomness()
    {
        var random = new SequenceRandom();
        var snapshot = Snapshot(2, 4, 8, 16);
        snapshot.score = 28;
        var game = new Game2048(snapshot, random);
        var result = game.Move(Direction.Left);
        Equal(false, result.Changed);
        Equal(0L, result.ScoreDelta);
        Equal(-1, result.SpawnIndex);
        Equal(0, result.SpawnValue);
        Equal(0, random.Calls);
        Equal(28L, game.Score);
        SequenceEqual(snapshot.cells, game.GetSnapshot().cells);
    }

    private static void SpawnRoll(int roll, int value)
    {
        var random = new SequenceRandom(0, roll);
        var game = new Game2048(Snapshot(0, 2), random);
        var result = game.Move(Direction.Left);
        Equal(value, result.SpawnValue);
        Equal(value, game.GetCell(result.SpawnIndex));
        SequenceEqual(new[] { 15, 100 }, random.RequestedBounds);
    }

    private static void SpawnSelectsEmptyCell()
    {
        var game = new Game2048(Snapshot(0, 2), new SequenceRandom(14, 0));
        var result = game.Move(Direction.Left);
        Equal(15, result.SpawnIndex);
        Equal(2, game.GetCell(15));
    }

    private static void ScoreUsesLong()
    {
        var snapshot = Snapshot(2, 2);
        snapshot.score = int.MaxValue;
        var game = new Game2048(snapshot, new SequenceRandom(0, 0));
        Equal(4L, game.Move(Direction.Left).ScoreDelta);
        Equal((long)int.MaxValue + 4, game.Score);
    }

    private static void ScoreSaturates()
    {
        var snapshot = Snapshot(2, 2);
        snapshot.score = long.MaxValue - 1;
        var game = new Game2048(snapshot, new SequenceRandom(0, 0));
        Equal(4L, game.Move(Direction.Left).ScoreDelta);
        Equal(long.MaxValue, game.Score);
        string reason;
        Equal(true, GameSnapshot.TryValidate(game.GetSnapshot(), out reason));
    }

    private static void InvalidDirectionsReject()
    {
        var random = new SequenceRandom();
        var game = new Game2048(Snapshot(2, 2), random);
        Throws<ArgumentOutOfRangeException>(() => game.Move((Direction)(-1)));
        Throws<ArgumentOutOfRangeException>(() => game.Move((Direction)4));
        Equal(0, random.Calls);
        SequenceEqual(Cells(2, 2), game.GetSnapshot().cells);
    }

    private static void MaximumTilesDoNotMerge()
    {
        var random = new SequenceRandom();
        var game = new Game2048(Snapshot(1073741824, 1073741824), random);
        Equal(false, game.Move(Direction.Left).Changed);
        Equal(0L, game.Score);
        Equal(0, random.Calls);
        var belowCap = Restore(536870912, 536870912);
        Equal(1073741824L, belowCap.Move(Direction.Left).ScoreDelta);
        Equal(1073741824, belowCap.GetCell(0));
    }

    private static void WinningIsDerived()
    {
        var game = Restore(1024, 1024);
        Equal(false, game.HasWon);
        Equal(2048L, game.Move(Direction.Left).ScoreDelta);
        Equal(true, game.HasWon);
        Equal(false, game.HasAcknowledgedWin);
    }

    private static void WinnerMayContinue()
    {
        var game = Restore(2048, 2048);
        Equal(true, game.HasWon);
        var result = game.Move(Direction.Left);
        Equal(true, result.Changed);
        Equal(4096, game.GetCell(0));
        game.AcknowledgeWin();
        game.AcknowledgeWin();
        Equal(true, game.HasAcknowledgedWin);
        Equal(true, game.GetSnapshot().winAcknowledged);
    }

    private static void EarlyAcknowledgementDoesNothing()
    {
        var game = Restore(2);
        game.AcknowledgeWin();
        Equal(false, game.HasAcknowledgedWin);
        Equal(false, game.GetSnapshot().winAcknowledged);
    }

    private static void RestoreAcknowledgedWinner()
    {
        var snapshot = Snapshot(4096);
        snapshot.winAcknowledged = true;
        var game = new Game2048(snapshot, new SequenceRandom());
        Equal(true, game.HasWon);
        Equal(true, game.HasAcknowledgedWin);
    }

    private static void DeadBoardIsGameOver()
    {
        var random = new SequenceRandom();
        var snapshot = DeadBoard();
        var game = new Game2048(snapshot, random);
        Equal(true, game.IsGameOver);
        foreach (Direction direction in Enum.GetValues(typeof(Direction)))
            Equal(false, game.Move(direction).Changed);
        Equal(0, random.Calls);
        SequenceEqual(snapshot.cells, game.GetSnapshot().cells);
    }

    private static void EmptyCellKeepsGameAlive()
    {
        var snapshot = DeadBoard();
        snapshot.cells[15] = 0;
        Equal(false, new Game2048(snapshot, new SequenceRandom()).IsGameOver);
        Equal(false, Restore().IsGameOver);
    }

    private static void HorizontalMergeKeepsGameAlive()
    {
        var snapshot = DeadBoard();
        snapshot.cells[15] = snapshot.cells[14];
        Equal(false, new Game2048(snapshot, new SequenceRandom()).IsGameOver);
    }

    private static void VerticalMergeKeepsGameAlive()
    {
        var snapshot = DeadBoard();
        snapshot.cells[15] = snapshot.cells[11];
        Equal(false, new Game2048(snapshot, new SequenceRandom()).IsGameOver);
    }

    private static void CappedTilesAreNotMergeable()
    {
        var snapshot = Snapshot();
        for (var i = 0; i < 16; i++) snapshot.cells[i] = 1073741824;
        var game = new Game2048(snapshot, new SequenceRandom());
        Equal(true, game.IsGameOver);
        Equal(false, game.Move(Direction.Down).Changed);
    }

    private static void SupportedPowersAreValid()
    {
        for (var exponent = 1; exponent <= 30; exponent++)
        {
            string reason;
            Equal(true, GameSnapshot.TryValidate(Snapshot(1 << exponent), out reason), reason);
        }
    }

    private static void InvalidSnapshotsRejected()
    {
        Invalid(null);
        Invalid(Change(s => s.schemaVersion = 0));
        Invalid(Change(s => s.schemaVersion = 2));
        Invalid(Change(s => s.gameId = null));
        Invalid(Change(s => s.gameId = "other"));
        Invalid(Change(s => s.cells = null));
        Invalid(Change(s => s.cells = new int[15]));
        Invalid(Change(s => s.cells = new int[17]));
        Invalid(Change(s => s.score = -1));
        Invalid(Change(s => s.winAcknowledged = true));
        foreach (var tile in new[] { -2, 1, 3, 6, int.MinValue, int.MaxValue })
            Invalid(Snapshot(tile));
    }

    private static void InvalidSnapshotCannotBeRestored()
    {
        Throws<ArgumentException>(() => new Game2048(Snapshot(3), new SequenceRandom()));
        Throws<ArgumentException>(() => new Game2048(null, new SequenceRandom()));
    }

    private static void RestoreCopiesCells()
    {
        var snapshot = Snapshot(2, 4);
        snapshot.score = 8;
        var game = new Game2048(snapshot, new SequenceRandom());
        snapshot.cells[0] = 2048;
        snapshot.score = 100;
        snapshot.winAcknowledged = true;
        Equal(2, game.GetCell(0));
        Equal(8L, game.Score);
        Equal(false, game.HasWon);
        Equal(false, game.HasAcknowledgedWin);
    }

    private static void SnapshotCopiesCells()
    {
        var game = Restore(2, 4);
        var first = game.GetSnapshot();
        var second = game.GetSnapshot();
        Equal(false, ReferenceEquals(first, second));
        Equal(false, ReferenceEquals(first.cells, second.cells));
        first.cells[0] = 1024;
        first.score = 100;
        first.winAcknowledged = true;
        Equal(2, game.GetCell(0));
        Equal(2, second.cells[0]);
        Equal(0L, game.Score);
        Equal(false, game.HasAcknowledgedWin);
    }

    private static void CellIndicesAreRangeChecked()
    {
        var game = Restore();
        Throws<ArgumentOutOfRangeException>(() => game.GetCell(-1));
        Throws<ArgumentOutOfRangeException>(() => game.GetCell(16));
    }

    private static void MotionsDescribeEveryTile()
    {
        var game = Restore(2, 0, 2, 4, 0, 0, 0, 8);
        var result = game.Move(Direction.Left);
        Equal(4, result.Motions.Count);
        AssertMotion(result, 0, 0, 2, true);
        AssertMotion(result, 2, 0, 2, true);
        AssertMotion(result, 3, 1, 4, false);
        AssertMotion(result, 7, 4, 8, false);
        var seen = new HashSet<int>();
        foreach (var motion in result.Motions) Equal(true, seen.Add(motion.FromIndex));
    }

    private static void NoOpMotionsIncludeStationaryTiles()
    {
        var game = new Game2048(Snapshot(2, 4, 8), new SequenceRandom());
        var result = game.Move(Direction.Left);
        Equal(3, result.Motions.Count);
        AssertMotion(result, 0, 0, 2, false);
        AssertMotion(result, 1, 1, 4, false);
        AssertMotion(result, 2, 2, 8, false);
    }

    private static void VerticalMotionsUseRowMajorIndices()
    {
        var snapshot = Snapshot();
        snapshot.cells[1] = 2;
        snapshot.cells[9] = 2;
        snapshot.cells[14] = 4;
        var up = new Game2048(snapshot, new SequenceRandom(0, 0));
        var upResult = up.Move(Direction.Up);
        AssertMotion(upResult, 1, 1, 2, true);
        AssertMotion(upResult, 9, 1, 2, true);
        AssertMotion(upResult, 14, 2, 4, false);
        var down = new Game2048(snapshot, new SequenceRandom(0, 0));
        var downResult = down.Move(Direction.Down);
        AssertMotion(downResult, 1, 13, 2, true);
        AssertMotion(downResult, 9, 13, 2, true);
        AssertMotion(downResult, 14, 14, 4, false);
    }

    private static void RepeatedPlayPreservesInvariants()
    {
        var random = new SeededRandom(2048);
        var game = new Game2048(random);
        var actions = new Random(41);
        for (var turn = 0; turn < 1000 && !game.IsGameOver; turn++)
        {
            var previous = game.GetSnapshot();
            var result = game.Move((Direction)actions.Next(4));
            var current = game.GetSnapshot();
            string reason;
            Equal(true, GameSnapshot.TryValidate(current, out reason), reason);
            Equal(Sum(previous.cells) + result.SpawnValue, Sum(current.cells));
            Equal(previous.score + result.ScoreDelta, current.score);
            Equal(CountTiles(previous.cells), result.Motions.Count);
            Equal(result.Changed, result.SpawnIndex >= 0);
            if (!result.Changed) SequenceEqual(previous.cells, current.cells);
        }
    }

    private static void AssertMove(Game2048 game, MoveResult result, int[] beforeSpawn, long scoreDelta)
    {
        Equal(true, result.Changed);
        Equal(scoreDelta, result.ScoreDelta);
        Equal(true, result.SpawnIndex >= 0 && result.SpawnIndex < 16);
        Equal(0, beforeSpawn[result.SpawnIndex], "Spawn must use a vacant cell");
        Equal(true, result.SpawnValue == 2 || result.SpawnValue == 4);
        beforeSpawn[result.SpawnIndex] = result.SpawnValue;
        SequenceEqual(beforeSpawn, game.GetSnapshot().cells);
    }

    private static void AssertMotion(MoveResult result, int from, int to, int value, bool merged)
    {
        TileMotion found = null;
        foreach (var motion in result.Motions)
            if (motion.FromIndex == from) { found = motion; break; }
        Equal(true, found != null, "Missing motion from " + from);
        Equal(to, found.ToIndex);
        Equal(value, found.Value);
        Equal(merged, found.Merged);
    }

    private static Game2048 Restore(params int[] cells)
    {
        return new Game2048(Snapshot(cells), new SequenceRandom(0, 0));
    }

    private static GameSnapshot Snapshot(params int[] cells)
    {
        return new GameSnapshot { cells = Cells(cells) };
    }

    private static int[] Cells(params int[] cells)
    {
        var board = new int[16];
        Array.Copy(cells, board, cells.Length);
        return board;
    }

    private static GameSnapshot Change(Action<GameSnapshot> change)
    {
        var snapshot = Snapshot();
        change(snapshot);
        return snapshot;
    }

    private static GameSnapshot DeadBoard()
    {
        return Snapshot(2, 4, 2, 4, 4, 2, 4, 2, 2, 4, 2, 4, 4, 2, 4, 2);
    }

    private static void Invalid(GameSnapshot snapshot)
    {
        string reason;
        Equal(false, GameSnapshot.TryValidate(snapshot, out reason));
        Equal(false, string.IsNullOrWhiteSpace(reason), "Invalid snapshots need a rejection reason");
    }

    private static int CountTiles(int[] cells)
    {
        var count = 0;
        foreach (var value in cells) if (value != 0) count++;
        return count;
    }

    private static long Sum(int[] cells)
    {
        long sum = 0;
        foreach (var value in cells) sum += value;
        return sum;
    }

    private static void Equal<T>(T expected, T actual, string detail = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException("Expected " + expected + ", got " + actual + (detail == null ? "" : ": " + detail));
    }

    private static void SequenceEqual(IList<int> expected, IList<int> actual)
    {
        Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++) Equal(expected[i], actual[i], "Index " + i);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected exception " + typeof(T).Name);
    }

    private static void Run(string name, Action action)
    {
        try
        {
            action();
            passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception exception)
        {
            failed++;
            Console.WriteLine("FAIL " + name + ": " + exception.Message);
        }
    }

    private sealed class SequenceRandom : IRandomSource
    {
        private readonly int[] values;
        private int position;
        public readonly List<int> RequestedBounds = new List<int>();
        public int Calls { get { return position; } }

        public SequenceRandom(params int[] values) { this.values = values; }

        public int Next(int maxExclusive)
        {
            RequestedBounds.Add(maxExclusive);
            if (position >= values.Length) throw new InvalidOperationException("Unexpected random-number request");
            var value = values[position++];
            if (value < 0 || value >= maxExclusive) throw new InvalidOperationException("Test random value is out of range");
            return value;
        }
    }

    private sealed class SeededRandom : IRandomSource
    {
        private readonly Random random;
        public SeededRandom(int seed) { random = new Random(seed); }
        public int Next(int maxExclusive) { return random.Next(maxExclusive); }
    }
}
