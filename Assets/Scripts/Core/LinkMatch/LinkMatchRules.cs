using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.LinkMatch
{
    public enum LinkMatchMode { Relaxed, Challenge }
    public sealed class LinkMatchLevel
    {
        private static readonly IReadOnlyList<LinkMatchLevel> Catalog = Array.AsReadOnly(new[] {
            new LinkMatchLevel(1, "level-01", 4, 6, 6, 90),
            new LinkMatchLevel(2, "level-02", 6, 6, 9, 120),
            new LinkMatchLevel(3, "level-03", 6, 8, 12, 150),
            new LinkMatchLevel(4, "level-04", 8, 12, 24, 300),
            new LinkMatchLevel(5, "level-05", 12, 16, 48, 600)
        });
        public int Number { get; private set; }
        public string Id { get; private set; }
        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public int Kinds { get; private set; }
        public int ChallengeSeconds { get; private set; }
        public static IReadOnlyList<LinkMatchLevel> All { get { return Catalog; } }
        public static int Count { get { return Catalog.Count; } }
        public static int MaximumTileCount { get; private set; }
        public static int MaximumKinds { get; private set; }
        static LinkMatchLevel()
        {
            foreach (var level in Catalog) {
                MaximumTileCount = Math.Max(MaximumTileCount, level.Columns * level.Rows);
                MaximumKinds = Math.Max(MaximumKinds, level.Kinds);
            }
        }
        private LinkMatchLevel(int number, string id, int columns, int rows, int kinds, int challengeSeconds)
        { Number = number; Id = id; Columns = columns; Rows = rows; Kinds = kinds; ChallengeSeconds = challengeSeconds; }
        public static LinkMatchLevel Get(int number)
        {
            if (number < 1 || number > Catalog.Count) throw new ArgumentOutOfRangeException(nameof(number));
            return Catalog[number - 1];
        }
        public static LinkMatchLevel Get(string id)
        {
            for (int i = 0; i < Catalog.Count; i++) if (Catalog[i].Id == id) return Catalog[i];
            throw new ArgumentException("Unknown link-match level.", nameof(id));
        }
    }
    public struct LinkMatchPoint : IEquatable<LinkMatchPoint>
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public LinkMatchPoint(int x, int y) { X = x; Y = y; }
        public bool Equals(LinkMatchPoint other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is LinkMatchPoint && Equals((LinkMatchPoint)obj); }
        public override int GetHashCode() { unchecked { return X * 397 ^ Y; } }
    }
    public sealed class LinkMatchPath
    {
        private readonly IReadOnlyList<LinkMatchPoint> points;
        public IReadOnlyList<LinkMatchPoint> Points { get { return points; } }
        public int Turns { get { return points.Count - 2; } }
        public int Steps { get; private set; }
        internal LinkMatchPath(LinkMatchPoint[] corners)
        {
            points = Array.AsReadOnly((LinkMatchPoint[])corners.Clone());
            for (int i = 1; i < points.Count; i++) Steps += Math.Abs(points[i].X - points[i - 1].X) + Math.Abs(points[i].Y - points[i - 1].Y);
        }
        internal LinkMatchPath Reversed()
        {
            var reversed = new LinkMatchPoint[points.Count];
            for (int i = 0; i < reversed.Length; i++) reversed[i] = points[reversed.Length - i - 1];
            return new LinkMatchPath(reversed);
        }
    }
    public sealed class LinkMatchPair
    {
        public int First { get; private set; }
        public int Second { get; private set; }
        public LinkMatchPath Path { get; private set; }
        internal LinkMatchPair(int first, int second, LinkMatchPath path) { First = first; Second = second; Path = path; }
    }
    public sealed class LinkMatchDeal
    {
        private readonly int[] tiles;
        private readonly IReadOnlyList<LinkMatchPair> solution;
        public int[] Tiles { get { return (int[])tiles.Clone(); } }
        public IReadOnlyList<LinkMatchPair> Solution { get { return solution; } }
        internal LinkMatchDeal(int[] tiles, LinkMatchPair[] solution)
        { this.tiles = (int[])tiles.Clone(); this.solution = Array.AsReadOnly((LinkMatchPair[])solution.Clone()); }
    }
}
