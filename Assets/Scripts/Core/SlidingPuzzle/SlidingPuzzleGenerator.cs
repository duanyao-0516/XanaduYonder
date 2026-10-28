using System;

namespace XanaduYonder.Core.SlidingPuzzle
{
    public static class SlidingPuzzleGenerator
    {
        /// <summary>Stable legal scrambling. Shuffle length is not a difficulty rating.</summary>
        public static int[] Generate(int size, int seed)
        {
            if (size < 3 || size > 5) throw new ArgumentOutOfRangeException("size");
            var cells = new int[size * size];
            for (int i = 0; i < cells.Length - 1; i++) cells[i] = i + 1;
            int blank = cells.Length - 1;
            int previousBlank = -1;
            uint random = unchecked((uint)seed) ^ 0x9E3779B9u;
            if (random == 0) random = 0x6D2B79F5u;
            var candidates = new int[4];
            for (int step = 0; step < cells.Length * 64; step++)
            {
                int count = 0;
                int row = blank / size;
                int column = blank % size;
                if (row > 0 && blank - size != previousBlank) candidates[count++] = blank - size;
                if (column > 0 && blank - 1 != previousBlank) candidates[count++] = blank - 1;
                if (row < size - 1 && blank + size != previousBlank) candidates[count++] = blank + size;
                if (column < size - 1 && blank + 1 != previousBlank) candidates[count++] = blank + 1;
                random ^= random << 13;
                random ^= random >> 17;
                random ^= random << 5;
                int tile = candidates[(int)(random % (uint)count)];
                cells[blank] = cells[tile];
                cells[tile] = 0;
                previousBlank = blank;
                blank = tile;
            }
            if (SlidingPuzzleGame.IsComplete(cells))
            {
                cells[blank] = cells[blank - 1];
                cells[blank - 1] = 0;
            }
            return cells;
        }
    }
}
