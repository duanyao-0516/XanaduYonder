using System;

namespace XanaduYonder.Core.Minesweeper
{
    /// <summary>An immutable board; all public arrays are defensive copies.</summary>
    public sealed class MinesweeperSnapshot
    {
        private readonly bool[] mines, revealed, flags;
        public int Difficulty { get; private set; }
        public int Width { get { return MinesweeperRules.Get(Difficulty).Width; } }
        public int Height { get { return MinesweeperRules.Get(Difficulty).Height; } }
        public int MineCount { get { return MinesweeperRules.Get(Difficulty).MineCount; } }
        public bool Generated { get; private set; }
        public int? FirstRevealIndex { get; private set; }
        public int? DetonatedIndex { get; private set; }
        public MinesweeperState State { get; private set; }
        public bool[] Mines { get { return Clone(mines); } }
        public bool[] Revealed { get { return Clone(revealed); } }
        public bool[] Flags { get { return Clone(flags); } }
        public MinesweeperSnapshot(int difficulty, bool generated, int? firstRevealIndex, MinesweeperState state,
            int? detonatedIndex, bool[] mines, bool[] revealed, bool[] flags)
        {
            Difficulty = difficulty; Generated = generated; FirstRevealIndex = firstRevealIndex; State = state;
            DetonatedIndex = detonatedIndex; this.mines = Clone(mines); this.revealed = Clone(revealed); this.flags = Clone(flags);
        }
        private static bool[] Clone(bool[] values) { return values == null ? null : (bool[])values.Clone(); }
        public bool IsMine(int index) { return mines[index]; }
        public bool IsRevealed(int index) { return revealed[index]; }
        public bool IsFlagged(int index) { return flags[index]; }
        public int AdjacentMines(int index)
        {
            int count = 0;
            foreach (int neighbor in MinesweeperRules.Neighbors(index, Width, Height)) if (mines[neighbor]) count++;
            return count;
        }
        public MinesweeperSnapshot Copy() { return new MinesweeperSnapshot(Difficulty, Generated, FirstRevealIndex, State, DetonatedIndex, mines, revealed, flags); }
        public bool TryValidate(out string reason)
        {
            reason = null;
            if (Difficulty < 0 || Difficulty > 2 || !Enum.IsDefined(typeof(MinesweeperState), State)) return Fail("Unknown board difficulty or state.", out reason);
            int count = Width * Height;
            if (mines == null || revealed == null || flags == null || mines.Length != count || revealed.Length != count || flags.Length != count)
                return Fail("Board arrays must match its fixed dimensions.", out reason);
            int mineCount = 0, safeOpened = 0, openedMines = 0;
            for (int i = 0; i < count; i++)
            {
                if (flags[i] && revealed[i]) return Fail("Flags and reveals must be disjoint.", out reason);
                if (mines[i]) { mineCount++; if (revealed[i]) openedMines++; }
                else if (revealed[i]) safeOpened++;
            }
            if (Generated != (State != MinesweeperState.Ready)) return Fail("Generated board and state disagree.", out reason);
            if (!Generated)
            {
                if (mineCount != 0 || safeOpened != 0 || FirstRevealIndex.HasValue || DetonatedIndex.HasValue)
                    return Fail("Ready board must be empty and ungenerated.", out reason);
                return true;
            }
            if (mineCount != MineCount || !FirstRevealIndex.HasValue || FirstRevealIndex.Value < 0 || FirstRevealIndex.Value >= count)
                return Fail("Invalid generated mine count or first reveal.", out reason);
            int first = FirstRevealIndex.Value;
            if (!revealed[first] || mines[first] || AdjacentMines(first) != 0) return Fail("First reveal must be an open safe zero.", out reason);
            if (State == MinesweeperState.Lost)
            {
                if (!DetonatedIndex.HasValue || DetonatedIndex.Value < 0 || DetonatedIndex.Value >= count ||
                    !mines[DetonatedIndex.Value] || !revealed[DetonatedIndex.Value] || openedMines != 1 || safeOpened >= count - MineCount)
                    return Fail("Lost board requires one detonated mine and an unopened safe cell.", out reason);
            }
            else
            {
                if (DetonatedIndex.HasValue || openedMines != 0) return Fail("Only lost boards may reveal a mine.", out reason);
                if (State == MinesweeperState.Won && safeOpened != count - MineCount) return Fail("Won board must reveal every safe cell.", out reason);
                if (State == MinesweeperState.Active && (safeOpened == 0 || safeOpened >= count - MineCount)) return Fail("Active board must have incomplete safe reveals.", out reason);
            }
            return true;
        }
        private static bool Fail(string message, out string reason) { reason = message; return false; }
    }
}
