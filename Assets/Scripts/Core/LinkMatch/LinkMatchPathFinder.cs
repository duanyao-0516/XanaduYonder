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
            if (start.X == end.X || start.Y == end.Y) Consider(level, tiles, start, end, ref best, 0);
            if (best != null) return best;

            if (start.X != end.X && start.Y != end.Y) {
                Consider(level, tiles, start, end, ref best, 1, new LinkMatchPoint(start.X, end.Y));
                Consider(level, tiles, start, end, ref best, 1, new LinkMatchPoint(end.X, start.Y));
                if (best != null) return best;
            }
            if (start.Y != end.Y) for (int x = -1; x <= level.Columns; x++) {
                if (x == start.X || x == end.X) continue;
                Consider(level, tiles, start, end, ref best, 2, new LinkMatchPoint(x, start.Y), new LinkMatchPoint(x, end.Y));
            }
            if (start.X != end.X) for (int y = -1; y <= level.Rows; y++) {
                if (y == start.Y || y == end.Y) continue;
                Consider(level, tiles, start, end, ref best, 2, new LinkMatchPoint(start.X, y), new LinkMatchPoint(end.X, y));
            }
            return best;
        }
        private static void Consider(LinkMatchLevel level, int[] tiles, LinkMatchPoint start, LinkMatchPoint end, ref LinkMatchPath best, int turns, LinkMatchPoint firstBend = default(LinkMatchPoint), LinkMatchPoint secondBend = default(LinkMatchPoint))
        {
            int steps = 0; var previous = start;
            // Dense boards reject many candidates. Keep candidate corners on the
            // stack, allocating a display path only after a route improves best.
            if (turns >= 1) {
                if (!ClearSegment(level, tiles, previous, firstBend, start, end)) return;
                steps += Math.Abs(firstBend.X - previous.X) + Math.Abs(firstBend.Y - previous.Y);
                previous = firstBend;
            }
            if (turns == 2) {
                if (!ClearSegment(level, tiles, previous, secondBend, start, end)) return;
                steps += Math.Abs(secondBend.X - previous.X) + Math.Abs(secondBend.Y - previous.Y);
                previous = secondBend;
            }
            if (!ClearSegment(level, tiles, previous, end, start, end)) return;
            steps += Math.Abs(end.X - previous.X) + Math.Abs(end.Y - previous.Y);
            // Candidate iteration order breaks equal-cost ties deterministically.
            if (best == null || turns < best.Turns || (turns == best.Turns && steps < best.Steps)) {
                var points = turns == 0 ? new[] { start, end } : turns == 1 ? new[] { start, firstBend, end } : new[] { start, firstBend, secondBend, end };
                best = new LinkMatchPath(points);
            }
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
