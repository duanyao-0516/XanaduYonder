namespace XanaduYonder.Core.SlidingPuzzle
{
    /// <summary>Immutable fixed content; only the first chapter claims proved optimal counts.</summary>
    public sealed class SlidingPuzzleLevel
    {
        private readonly int[] initialCells;
        private readonly int[] solution;

        internal SlidingPuzzleLevel(int index, int size, int[] initialCells, int[] solution, int? optimalMoves)
        {
            Index = index;
            Size = size;
            this.initialCells = (int[])initialCells.Clone();
            this.solution = (int[])solution.Clone();
            OptimalMoves = optimalMoves;
        }

        public int Index { get; private set; }
        public int ChapterIndex { get { return Index / SlidingPuzzleCatalog.LevelsPerChapter; } }
        public int Number { get { return Index % SlidingPuzzleCatalog.LevelsPerChapter + 1; } }
        public int Size { get; private set; }
        public int[] InitialCells { get { return (int[])initialCells.Clone(); } }
        public int[] Solution { get { return (int[])solution.Clone(); } }
        public int? OptimalMoves { get; private set; }
    }
}
