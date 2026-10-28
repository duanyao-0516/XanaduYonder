namespace XanaduYonder.Core.SlidingPuzzle
{
    /// <summary>Versioned by the owning save envelope. History records active tile values.</summary>
    public sealed class SlidingPuzzleSnapshot
    {
        public int Size;
        public int[] InitialCells;
        public int[] Cells;
        public int[] History;
        public int Moves;
        public int UndoCount;

        public SlidingPuzzleSnapshot Copy()
        {
            return new SlidingPuzzleSnapshot {
                Size = Size,
                InitialCells = InitialCells == null ? null : (int[])InitialCells.Clone(),
                Cells = Cells == null ? null : (int[])Cells.Clone(),
                History = History == null ? null : (int[])History.Clone(),
                Moves = Moves,
                UndoCount = UndoCount
            };
        }
    }
}
