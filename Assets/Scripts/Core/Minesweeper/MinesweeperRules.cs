using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Minesweeper
{
    public enum MinesweeperState { Ready, Active, Won, Lost }
    public enum MinesweeperMoveReason { None, Invalid, Flagged, FlagMismatch, Terminal, Blocked }

    public sealed class MinesweeperDifficulty
    {
        public int Index { get; private set; }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int MineCount { get; private set; }
        internal MinesweeperDifficulty(int index, string id, string name, int width, int height, int mines)
        { Index = index; Id = id; Name = name; Width = width; Height = height; MineCount = mines; }
    }

    public static class MinesweeperRules
    {
        private static readonly MinesweeperDifficulty[] Difficulties = {
            new MinesweeperDifficulty(0, "beginner", "初级", 9, 9, 10),
            new MinesweeperDifficulty(1, "intermediate", "中级", 16, 16, 40),
            new MinesweeperDifficulty(2, "expert", "高级", 30, 16, 99)
        };
        public static MinesweeperDifficulty Get(int difficulty)
        {
            if (difficulty < 0 || difficulty >= Difficulties.Length) throw new ArgumentOutOfRangeException(nameof(difficulty));
            return Difficulties[difficulty];
        }
        public static int ParseDifficulty(string id)
        {
            for (int i = 0; i < Difficulties.Length; i++) if (Difficulties[i].Id == id) return i;
            throw new ArgumentException("Unknown minesweeper difficulty.", nameof(id));
        }
        public static int[] Neighbors(int index, int width, int height)
        {
            if (width <= 0 || height <= 0 || index < 0 || index >= width * height) return new int[0];
            var result = new List<int>(8);
            int column = index % width, row = index / width;
            for (int y = Math.Max(0, row - 1); y <= Math.Min(height - 1, row + 1); y++)
                for (int x = Math.Max(0, column - 1); x <= Math.Min(width - 1, column + 1); x++)
                    if (x != column || y != row) result.Add(y * width + x);
            return result.ToArray();
        }
    }

    public sealed class MinesweeperMoveResult
    {
        private readonly int[] changedIndices;
        public bool Changed { get { return changedIndices.Length != 0; } }
        public int[] ChangedIndices { get { return (int[])changedIndices.Clone(); } }
        public MinesweeperState State { get; private set; }
        public MinesweeperMoveReason Reason { get; private set; }
        public MinesweeperMoveResult(MinesweeperState state, MinesweeperMoveReason reason, int[] changed)
        { State = state; Reason = reason; changedIndices = changed == null ? new int[0] : (int[])changed.Clone(); }
    }
}
