using System;
using System.Collections.Generic;

namespace XanaduYonder.Core
{
    public sealed class Game2048
    {
        internal const int MaximumTile = 1073741824;
        private const int Size = 4;
        private readonly int[] cells;
        private readonly IRandomSource random;

        public long Score { get; private set; }
        public bool HasAcknowledgedWin { get; private set; }

        public bool HasWon
        {
            get
            {
                foreach (var value in cells) if (value >= 2048) return true;
                return false;
            }
        }

        public bool IsGameOver
        {
            get
            {
                for (var index = 0; index < cells.Length; index++)
                {
                    if (cells[index] == 0) return false;
                    if (index % Size < Size - 1 && CanMerge(cells[index], cells[index + 1])) return false;
                    if (index / Size < Size - 1 && CanMerge(cells[index], cells[index + Size])) return false;
                }
                return true;
            }
        }

        public Game2048(IRandomSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            this.random = random;
            cells = new int[Size * Size];
            SpawnTile(out _, out _);
            SpawnTile(out _, out _);
        }

        public Game2048(GameSnapshot snapshot, IRandomSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            string reason;
            if (!GameSnapshot.TryValidate(snapshot, out reason)) throw new ArgumentException(reason, nameof(snapshot));
            this.random = random;
            cells = (int[])snapshot.cells.Clone();
            Score = snapshot.score;
            HasAcknowledgedWin = snapshot.winAcknowledged;
        }

        public int GetCell(int index)
        {
            if (index < 0 || index >= cells.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return cells[index];
        }

        public GameSnapshot GetSnapshot()
        {
            return new GameSnapshot
            {
                cells = (int[])cells.Clone(),
                score = Score,
                winAcknowledged = HasAcknowledgedWin
            };
        }

        public void AcknowledgeWin()
        {
            if (HasWon) HasAcknowledgedWin = true;
        }

        public MoveResult Move(Direction direction)
        {
            if (direction < Direction.Up || direction > Direction.Right)
                throw new ArgumentOutOfRangeException(nameof(direction));

            var next = new int[cells.Length];
            var motions = new List<TileMotion>(cells.Length);
            var sources = new int[Size];
            long scoreDelta = 0;

            // Visit each line from its destination edge, so a consumed pair can never merge again.
            for (var line = 0; line < Size; line++)
            {
                var count = 0;
                for (var position = 0; position < Size; position++)
                {
                    var index = IndexAt(direction, line, position);
                    if (cells[index] != 0) sources[count++] = index;
                }

                var outputPosition = 0;
                for (var source = 0; source < count; source++)
                {
                    var fromIndex = sources[source];
                    var toIndex = IndexAt(direction, line, outputPosition++);
                    var value = cells[fromIndex];
                    var merged = source + 1 < count && CanMerge(value, cells[sources[source + 1]]);
                    motions.Add(new TileMotion(fromIndex, toIndex, value, merged));
                    if (merged)
                    {
                        var secondIndex = sources[++source];
                        motions.Add(new TileMotion(secondIndex, toIndex, cells[secondIndex], true));
                        next[toIndex] = value * 2;
                        scoreDelta += next[toIndex];
                    }
                    else next[toIndex] = value;
                }
            }

            var changed = false;
            for (var index = 0; index < cells.Length; index++)
                if (cells[index] != next[index]) { changed = true; break; }
            if (!changed) return new MoveResult(false, 0, motions, -1, 0);

            Array.Copy(next, cells, cells.Length);
            // A valid external snapshot may already have the maximum representable score.
            Score = Score > long.MaxValue - scoreDelta ? long.MaxValue : Score + scoreDelta;
            int spawnIndex;
            int spawnValue;
            SpawnTile(out spawnIndex, out spawnValue);
            return new MoveResult(true, scoreDelta, motions, spawnIndex, spawnValue);
        }

        private void SpawnTile(out int index, out int value)
        {
            var emptyCells = new List<int>(cells.Length);
            for (var cell = 0; cell < cells.Length; cell++)
                if (cells[cell] == 0) emptyCells.Add(cell);
            index = emptyCells[random.Next(emptyCells.Count)];
            value = random.Next(100) < 90 ? 2 : 4;
            cells[index] = value;
        }

        private static bool CanMerge(int first, int second)
        {
            return first > 0 && first < MaximumTile && first == second;
        }

        private static int IndexAt(Direction direction, int line, int position)
        {
            switch (direction)
            {
                case Direction.Left: return line * Size + position;
                case Direction.Right: return line * Size + Size - 1 - position;
                case Direction.Up: return position * Size + line;
                case Direction.Down: return (Size - 1 - position) * Size + line;
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }
    }
}
