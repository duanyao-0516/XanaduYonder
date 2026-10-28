using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Sokoban
{
    /// <summary>Validated, immutable terrain. The initial positions never share mutable storage with callers.</summary>
    public sealed class SokobanLevel
    {
        private readonly bool[] walls, goals;
        private readonly int[] boxes;
        public string LevelId { get; private set; }
        public string ContentVersion { get; private set; }
        public string Title { get; private set; }
        public string Chapter { get; private set; }
        public int Order { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int InitialPlayer { get; private set; }
        public int[] InitialBoxes { get { return (int[])boxes.Clone(); } }
        public int BoxCount { get { return boxes.Length; } }
        public string Solution { get; private set; }
        public string SourceCollection { get; private set; }
        public string SourceId { get; private set; }
        public SokobanLevel(string id, string version, string title, string chapter, int order, string[] rows, string solution = "", string sourceCollection = "xanadu-original", string sourceId = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(chapter) || string.IsNullOrWhiteSpace(sourceCollection) || order < 1)
                throw new ArgumentException("Missing level identity, title, source or order.");
            if (rows == null || rows.Length < 3 || rows.Length > 14 || rows[0] == null || rows[0].Length < 3 || rows[0].Length > 10)
                throw new ArgumentException("Level dimensions exceed the published bounds.");
            LevelId = id; ContentVersion = version; Title = title; Chapter = chapter; Order = order; SourceCollection = sourceCollection; SourceId = sourceId ?? id;
            if (string.IsNullOrWhiteSpace(SourceId)) throw new ArgumentException("Missing source ID.");
            Width = rows[0].Length; Height = rows.Length; walls = new bool[Width * Height]; goals = new bool[walls.Length];
            var initialBoxes = new List<int>(); int players = 0, goalCount = 0, covered = 0;
            for (int y = 0; y < Height; y++)
            {
                if (rows[y] == null || rows[y].Length != Width) throw new ArgumentException("A level must be rectangular without trimming its rows.");
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x; char c = rows[y][x];
                    if ("# .$@*+".IndexOf(c) < 0) throw new ArgumentException("Unknown terrain character.");
                    walls[i] = c == '#'; goals[i] = c == '.' || c == '*' || c == '+';
                    if ((x == 0 || y == 0 || x == Width - 1 || y == Height - 1) && !walls[i]) throw new ArgumentException("The published map must have a closed outer wall.");
                    if (goals[i]) goalCount++;
                    if (c == '$' || c == '*') { initialBoxes.Add(i); if (goals[i]) covered++; }
                    if (c == '@' || c == '+') { InitialPlayer = i; players++; }
                }
            }
            if (players != 1 || goalCount == 0 || initialBoxes.Count != goalCount || covered == goalCount) throw new ArgumentException("A level needs one player, matching nonzero boxes/goals and an unfinished initial board.");
            boxes = initialBoxes.ToArray();
            if (solution == null || solution.Length > SokobanGame.MaximumMoves) throw new ArgumentException("Invalid solution length.");
            foreach (char c in solution) if ("LURD".IndexOf(c) < 0) throw new ArgumentException("Invalid solution direction.");
            Solution = solution;
        }
        public bool IsWall(int index) { return index < 0 || index >= walls.Length || walls[index]; }
        public bool IsGoal(int index) { return index >= 0 && index < goals.Length && goals[index]; }
        internal int Neighbor(int index, Direction direction)
        {
            int x = index % Width, y = index / Width;
            switch (direction) { case Direction.Up: y--; break; case Direction.Down: y++; break; case Direction.Left: x--; break; case Direction.Right: x++; break; default: return -1; }
            return x < 0 || x >= Width || y < 0 || y >= Height ? -1 : y * Width + x;
        }
        internal bool IsStaticCorner(int index)
        { return !IsGoal(index) && (IsWall(Neighbor(index, Direction.Up)) || IsWall(Neighbor(index, Direction.Down))) && (IsWall(Neighbor(index, Direction.Left)) || IsWall(Neighbor(index, Direction.Right))); }
    }
}
