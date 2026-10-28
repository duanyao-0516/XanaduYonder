using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.SlidingPuzzle
{
    /// <summary>Single-tile sliding rules, independent of rendering and persistence.</summary>
    public sealed class SlidingPuzzleGame
    {
        // Resource protection, not a challenge target. No action is silently discarded.
        public const int MaxActions = 100000;
        private readonly int[] initial;
        private readonly int[] cells;
        private readonly List<int> history;
        private int blank;

        public SlidingPuzzleGame(int size, int[] initial)
        {
            if (!IsSolvable(size, initial))
                throw new ArgumentException("Expected a solvable permutation for a 3, 4 or 5 square board.", "initial");
            Size = size;
            this.initial = (int[])initial.Clone();
            cells = (int[])initial.Clone();
            history = new List<int>();
            blank = Array.IndexOf(cells, 0);
            Won = IsComplete(cells);
        }

        public int Size { get; private set; }
        public int Moves { get; private set; }
        public int UndoCount { get; private set; }
        public bool Won { get; private set; }
        public bool Assisted { get { return UndoCount > 0; } }
        public bool AtActionLimit { get { return Moves >= MaxActions; } }
        public bool CanUndo { get { return !Won && !AtActionLimit && history.Count > 0; } }

        public int[] GetCells() { return (int[])cells.Clone(); }
        public int[] GetInitial() { return (int[])initial.Clone(); }

        public bool TryMove(int tileValue)
        {
            if (Won || AtActionLimit || tileValue <= 0 || tileValue >= cells.Length) return false;
            int tile = Array.IndexOf(cells, tileValue);
            if (!IsAdjacent(blank, tile, Size)) return false;
            ExchangeWithBlank(tile);
            history.Add(tileValue);
            Moves++;
            Won = IsComplete(cells);
            return true;
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            int last = history.Count - 1;
            int tile = Array.IndexOf(cells, history[last]);
            ExchangeWithBlank(tile);
            history.RemoveAt(last);
            Moves++;
            UndoCount++;
            return true;
        }

        public SlidingPuzzleSnapshot GetSnapshot()
        {
            return new SlidingPuzzleSnapshot {
                Size = Size, InitialCells = GetInitial(), Cells = GetCells(),
                History = history.ToArray(), Moves = Moves, UndoCount = UndoCount
            };
        }

        /// <summary>Replays active moves; never trusts a persisted board or victory flag.</summary>
        public static SlidingPuzzleGame Restore(SlidingPuzzleSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            if (snapshot.Size < 3 || snapshot.Size > 5 || snapshot.InitialCells == null ||
                snapshot.Cells == null || snapshot.History == null ||
                snapshot.InitialCells.Length != snapshot.Size * snapshot.Size ||
                snapshot.Cells.Length != snapshot.Size * snapshot.Size ||
                snapshot.History.Length > MaxActions || snapshot.UndoCount < 0 ||
                snapshot.Moves < 0 || snapshot.Moves > MaxActions ||
                (long)snapshot.History.Length + 2L * snapshot.UndoCount != snapshot.Moves)
                throw new ArgumentException("Invalid sliding puzzle snapshot structure or counters.", "snapshot");

            var saved = snapshot.Copy();
            var game = new SlidingPuzzleGame(saved.Size, saved.InitialCells);
            if (game.Won && (saved.Moves != 0 || saved.UndoCount != 0))
                throw new ArgumentException("An initially completed board cannot have actions.", "snapshot");
            foreach (int tile in saved.History)
                if (!game.TryMove(tile))
                    throw new ArgumentException("Snapshot history contains an illegal or post-victory move.", "snapshot");
            for (int i = 0; i < saved.Cells.Length; i++)
                if (saved.Cells[i] != game.cells[i])
                    throw new ArgumentException("Snapshot board does not match its replayed history.", "snapshot");
            game.Moves = saved.Moves;
            game.UndoCount = saved.UndoCount;
            return game;
        }

        /// <summary>Rejects malformed arrays before applying the odd/even board parity rule.</summary>
        public static bool IsSolvable(int size, int[] cells)
        {
            if (size < 3 || size > 5 || cells == null || cells.Length != size * size) return false;
            var seen = new bool[cells.Length];
            int blankIndex = -1;
            for (int i = 0; i < cells.Length; i++)
            {
                int value = cells[i];
                if (value < 0 || value >= cells.Length || seen[value]) return false;
                seen[value] = true;
                if (value == 0) blankIndex = i;
            }
            int inversions = 0;
            for (int i = 0; i < cells.Length; i++)
                for (int j = i + 1; j < cells.Length; j++)
                    if (cells[i] != 0 && cells[j] != 0 && cells[i] > cells[j]) inversions++;
            if (size % 2 != 0) return inversions % 2 == 0;
            int blankRowFromBottom = size - blankIndex / size;
            return (inversions + blankRowFromBottom) % 2 == 1;
        }

        private void ExchangeWithBlank(int tile)
        {
            cells[blank] = cells[tile];
            cells[tile] = 0;
            blank = tile;
        }

        private static bool IsAdjacent(int a, int b, int size)
        {
            return Math.Abs(a / size - b / size) + Math.Abs(a % size - b % size) == 1;
        }

        internal static bool IsComplete(int[] cells)
        {
            for (int i = 0; i < cells.Length - 1; i++)
                if (cells[i] != i + 1) return false;
            return cells[cells.Length - 1] == 0;
        }
    }
}
