using System;

namespace XanaduYonder.Core.LinkMatch
{
    // Every orthogonal path with <= 2 turns is a straight segment, an elbow,
    // or has a horizontal/vertical middle segment. Enumerating those segments
    // is bounded by the board dimensions and includes exactly one outer ring.
    internal static class LinkMatchPathFinder
    {
        internal static LinkMatchPath Find(LinkMatchLevel level, int[] tiles, int first, int second)
        {
            if (first > second) {
                var reverse = Find(level, tiles, second, first);
                return reverse == null ? null : reverse.Reversed();
            }
            var start = new LinkMatchPoint(first % level.Columns, first / level.Columns);
            var end = new LinkMatchPoint(second % level.Columns, second / level.Columns);
            LinkMatchPath best = null;
            if (start.X == end.X || start.Y == end.Y) Consider(level, tiles, start, end, ref best, start, end);
            if (best != null) return best;

            if (start.X != end.X && start.Y != end.Y) {
                Consider(level, tiles, start, end, ref best, start, new LinkMatchPoint(start.X, end.Y), end);
                Consider(level, tiles, start, end, ref best, start, new LinkMatchPoint(end.X, start.Y), end);
                if (best != null) return best;
            }
            if (start.Y != end.Y) for (int x = -1; x <= level.Columns; x++) {
                if (x == start.X || x == end.X) continue;
                Consider(level, tiles, start, end, ref best, start, new LinkMatchPoint(x, start.Y), new LinkMatchPoint(x, end.Y), end);
            }
            if (start.X != end.X) for (int y = -1; y <= level.Rows; y++) {
                if (y == start.Y || y == end.Y) continue;
                Consider(level, tiles, start, end, ref best, start, new LinkMatchPoint(start.X, y), new LinkMatchPoint(end.X, y), end);
            }
            return best;
        }
        private static void Consider(LinkMatchLevel level, int[] tiles, LinkMatchPoint start, LinkMatchPoint end, ref LinkMatchPath best, params LinkMatchPoint[] points)
        {
            int steps = 0;
            for (int i = 1; i < points.Length; i++) {
                if (!ClearSegment(level, tiles, points[i - 1], points[i], start, end)) return;
                steps += Math.Abs(points[i].X - points[i - 1].X) + Math.Abs(points[i].Y - points[i - 1].Y);
            }
            // Candidate iteration order breaks equal-cost ties deterministically.
            if (best == null || points.Length - 2 < best.Turns || (points.Length - 2 == best.Turns && steps < best.Steps)) best = new LinkMatchPath(points);
        }
        private static bool ClearSegment(LinkMatchLevel level, int[] tiles, LinkMatchPoint from, LinkMatchPoint to, LinkMatchPoint start, LinkMatchPoint end)
        {
            int dx = Math.Sign(to.X - from.X), dy = Math.Sign(to.Y - from.Y);
            int x = from.X, y = from.Y;
            while (true) {
                if (x >= 0 && x < level.Columns && y >= 0 && y < level.Rows && tiles[y * level.Columns + x] != -1 && !(x == start.X && y == start.Y) && !(x == end.X && y == end.Y)) return false;
                if (x == to.X && y == to.Y) return true;
                x += dx; y += dy;
            }
        }
    }
}
