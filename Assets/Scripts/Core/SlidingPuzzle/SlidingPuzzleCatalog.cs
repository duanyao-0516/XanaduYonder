using System;

namespace XanaduYonder.Core.SlidingPuzzle
{
    public static class SlidingPuzzleCatalog
    {
        public const int ChapterCount = 3;
        public const int LevelsPerChapter = 6;
        public const int Count = ChapterCount * LevelsPerChapter;
        public const string ContentVersion = "1";

        // Embedded boards keep saved level identities stable across sessions and runtimes.
        // Chapter one distances are exhaustively proved by the independent test BFS.
        // Later witnesses prove solvability only; their lengths are not optimality claims.
        private static readonly SlidingPuzzleLevel[] Levels = {
            new SlidingPuzzleLevel(0, 3,
                new[] { 1, 2, 3, 4, 0, 5, 7, 8, 6 },
                new[] { 5, 6 }, 2),
            new SlidingPuzzleLevel(1, 3,
                new[] { 0, 2, 3, 1, 4, 5, 7, 8, 6 },
                new[] { 1, 4, 5, 6 }, 4),
            new SlidingPuzzleLevel(2, 3,
                new[] { 0, 2, 3, 1, 8, 5, 4, 7, 6 },
                new[] { 1, 4, 7, 8, 5, 6 }, 6),
            new SlidingPuzzleLevel(3, 3,
                new[] { 4, 1, 3, 7, 0, 6, 5, 2, 8 },
                new[] { 2, 5, 7, 4, 1, 2, 5, 8 }, 8),
            new SlidingPuzzleLevel(4, 3,
                new[] { 7, 1, 2, 5, 6, 3, 0, 4, 8 },
                new[] { 5, 7, 1, 2, 3, 6, 4, 5, 7, 4, 5, 8 }, 12),
            new SlidingPuzzleLevel(5, 3,
                new[] { 0, 1, 6, 3, 2, 8, 4, 5, 7 },
                new[] { 1, 2, 3, 1, 2, 3, 5, 7, 8, 6, 3, 2, 1, 4, 7, 8 }, 16),
            new SlidingPuzzleLevel(6, 4,
                new[] { 1, 2, 3, 4, 5, 0, 7, 8, 11, 6, 12, 15, 9, 13, 10, 14 },
                new[] { 6, 11, 9, 13, 10, 14, 15, 12, 11, 10, 14, 15 }, null),
            new SlidingPuzzleLevel(7, 4,
                new[] { 0, 1, 2, 3, 5, 6, 8, 4, 13, 11, 7, 12, 14, 10, 9, 15 },
                new[] { 1, 2, 3, 4, 8, 7, 9, 10, 11, 9, 10, 11, 14, 13, 9, 10, 11, 15 }, null),
            new SlidingPuzzleLevel(8, 4,
                new[] { 5, 1, 3, 4, 2, 0, 6, 7, 13, 9, 15, 8, 10, 14, 12, 11 },
                new[] { 2, 5, 1, 2, 6, 7, 8, 15, 12, 14, 9, 13, 10, 9, 13, 10, 9, 13, 14, 11, 15, 12, 11, 15 }, null),
            new SlidingPuzzleLevel(9, 4,
                new[] { 5, 1, 2, 4, 9, 3, 12, 7, 10, 6, 0, 8, 13, 14, 11, 15 },
                new[] { 12, 7, 8, 15, 11, 12, 7, 3, 6, 10, 9, 5, 1, 2, 4, 8, 3, 7, 15, 11, 12, 15, 7, 3, 8, 4, 3, 7, 11, 12 }, null),
            new SlidingPuzzleLevel(10, 4,
                new[] { 5, 8, 0, 3, 10, 9, 1, 2, 13, 7, 6, 4, 14, 15, 11, 12 },
                new[] { 1, 2, 4, 6, 11, 15, 14, 13, 10, 9, 2, 1, 3, 4, 6, 11, 7, 10, 9, 2, 1, 6, 4, 3, 8, 5, 2, 1, 5, 2, 1, 5, 6, 8, 3, 4, 8, 7, 11, 12 }, null),
            new SlidingPuzzleLevel(11, 4,
                new[] { 1, 10, 2, 4, 5, 6, 14, 7, 12, 13, 0, 15, 9, 11, 3, 8 },
                new[] { 3, 11, 9, 12, 13, 3, 14, 6, 3, 9, 12, 13, 9, 14, 11, 8, 15, 7, 4, 2, 6, 11, 7, 15, 8, 12, 14, 3, 11, 7, 15, 8, 12, 15, 3, 11, 10, 6, 2, 4, 7, 3, 11, 10, 6, 2, 3, 7, 8, 12 }, null),
            new SlidingPuzzleLevel(12, 5,
                new[] { 1, 2, 3, 4, 5, 17, 6, 8, 9, 10, 0, 7, 13, 14, 15, 12, 21, 18, 19, 20, 11, 16, 22, 23, 24 },
                new[] { 17, 6, 7, 17, 12, 11, 16, 21, 17, 12, 11, 16, 21, 22, 23, 24 }, null),
            new SlidingPuzzleLevel(13, 5,
                new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 16, 12, 14, 15, 17, 21, 13, 24, 23, 0, 22, 19, 18, 20 },
                new[] { 17, 21, 16, 12, 13, 19, 22, 17, 21, 16, 17, 22, 18, 20, 23, 24, 20, 23, 24, 20, 19, 18, 23, 24 }, null),
            new SlidingPuzzleLevel(14, 5,
                new[] { 2, 8, 0, 3, 9, 1, 7, 13, 5, 4, 6, 12, 20, 14, 10, 11, 22, 17, 24, 15, 16, 21, 18, 23, 19 },
                new[] { 3, 5, 4, 9, 5, 3, 8, 2, 1, 6, 11, 16, 21, 22, 17, 20, 13, 8, 3, 4, 9, 10, 15, 24, 20, 18, 23, 19, 24, 20, 19, 24 }, null),
            new SlidingPuzzleLevel(15, 5,
                new[] { 2, 4, 5, 19, 0, 1, 3, 7, 10, 14, 12, 13, 8, 9, 15, 6, 16, 17, 18, 20, 11, 21, 22, 23, 24 },
                new[] { 19, 10, 14, 19, 10, 5, 4, 3, 7, 8, 13, 12, 6, 11, 21, 22, 23, 24, 20, 15, 19, 14, 9, 19, 14, 10, 5, 4, 3, 2, 1, 6, 11, 16, 17, 18, 19, 14, 15, 20 }, null),
            new SlidingPuzzleLevel(16, 5,
                new[] { 12, 4, 5, 3, 10, 7, 2, 1, 14, 9, 6, 11, 8, 20, 19, 18, 0, 17, 13, 15, 16, 22, 21, 23, 24 },
                new[] { 17, 13, 20, 19, 15, 20, 19, 14, 3, 5, 4, 2, 1, 3, 9, 10, 5, 4, 2, 12, 7, 1, 12, 7, 1, 6, 11, 17, 22, 21, 13, 22, 18, 16, 21, 18, 22, 13, 18, 22, 17, 12, 7, 2, 3, 8, 13, 18, 23, 24 }, null),
            new SlidingPuzzleLevel(17, 5,
                new[] { 6, 1, 3, 5, 10, 11, 2, 15, 8, 4, 7, 12, 13, 9, 20, 21, 16, 18, 0, 14, 22, 23, 17, 19, 24 },
                new[] { 18, 16, 23, 17, 19, 24, 14, 18, 9, 8, 4, 10, 5, 4, 8, 9, 18, 14, 24, 19, 16, 13, 15, 8, 9, 15, 13, 18, 14, 20, 15, 13, 12, 7, 11, 6, 1, 2, 7, 12, 13, 14, 19, 24, 20, 19, 18, 16, 17, 23, 16, 17, 23, 22, 21, 16, 17, 18, 19, 20 }, null),
        };

        public static SlidingPuzzleLevel Get(int globalIndex)
        {
            if (globalIndex < 0 || globalIndex >= Count) throw new ArgumentOutOfRangeException("globalIndex");
            return Levels[globalIndex];
        }
    }
}
