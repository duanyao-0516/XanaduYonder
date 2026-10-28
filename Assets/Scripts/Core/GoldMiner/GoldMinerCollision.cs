using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.GoldMiner
{
    public static class GoldMinerCollision
    {
        // Both trajectories use the same normalized interval [0, 1].
        public static bool TrySweptCircle(double ax0, double ay0, double ax1, double ay1, double bx0, double by0, double bx1, double by1, double radius, out double time)
        {
            time = 0;
            if (!GoldMinerRules.IsFinite(ax0) || !GoldMinerRules.IsFinite(ay0) || !GoldMinerRules.IsFinite(ax1) || !GoldMinerRules.IsFinite(ay1) || !GoldMinerRules.IsFinite(bx0) || !GoldMinerRules.IsFinite(by0) || !GoldMinerRules.IsFinite(bx1) || !GoldMinerRules.IsFinite(by1) || !GoldMinerRules.IsFinite(radius) || radius < 0)
                return false;
            double x = ax0 - bx0, y = ay0 - by0, dx = (ax1 - ax0) - (bx1 - bx0), dy = (ay1 - ay0) - (by1 - by0);
            double c = x * x + y * y - radius * radius;
            if (!GoldMinerRules.IsFinite(x) || !GoldMinerRules.IsFinite(y) || !GoldMinerRules.IsFinite(dx) || !GoldMinerRules.IsFinite(dy) || !GoldMinerRules.IsFinite(c))
                return false;
            if (c <= 0)
                return true;
            double a = dx * dx + dy * dy;
            if (a == 0 || !GoldMinerRules.IsFinite(a))
                return false;
            double b = 2 * (x * dx + y * dy), discriminant = b * b - 4 * a * c;
            if (discriminant < 0 || !GoldMinerRules.IsFinite(b) || !GoldMinerRules.IsFinite(discriminant))
                return false;
            double root = (-b - Math.Sqrt(discriminant)) / (2 * a);
            if (!GoldMinerRules.IsFinite(root) || root < 0 || root > 1)
                return false;
            time = root;
            return true;
        }

        internal sealed class Segment
        {
            internal double T0, T1, X0, Y0, X1, Y1;
            internal int Direction;
        }

        internal static List<Segment> Trajectory(GoldMinerEntity e, GoldMinerMode mode, int level)
        {
            var result = new List<Segment>();
            if (!GoldMinerRules.IsAnimal(e.Kind))
            {
                result.Add(new Segment { T0 = 0, T1 = 1, X0 = e.X, X1 = e.X, Y0 = e.Y, Y1 = e.Y });
                return result;
            }

            double speed = GoldMinerRules.AnimalSpeed(level) * GoldMinerRules.TickSeconds;
            double min = GoldMinerRules.Radius(e.Kind), max = GoldMinerRules.Width(mode) - min, x = e.X, time = 0;
            int direction = e.MoveDirection;
            while (time < 1)
            {
                double wall = direction > 0 ? max : min;
                double duration = Math.Abs(wall - x) / speed;
                if (duration == 0)
                {
                    direction = -direction;
                    continue;
                }

                bool reachesWall = duration <= 1 - time;
                double end = reachesWall ? time + duration : 1;
                double next = reachesWall ? wall : x + direction * speed * (end - time);
                result.Add(new Segment { T0 = time, T1 = end, X0 = x, X1 = next, Y0 = e.Y, Y1 = e.Y, Direction = direction });
                x = next;
                time = end;
                if (reachesWall)
                    direction = -direction;
            }

            result[result.Count - 1].Direction = direction;
            return result;
        }

        internal static bool FirstContact(double originX, double angle, double length, double travel, double end, List<Segment> path, double radius, out double time)
        {
            time = 0;
            double dx = Math.Sin(angle), dy = Math.Cos(angle);
            foreach (var p in path)
            {
                if (p.T0 > end)
                    break;
                double stop = Math.Min(end, p.T1), span = p.T1 - p.T0;
                double bx = p.X0 + (p.X1 - p.X0) * (stop - p.T0) / span, by = p.Y0 + (p.Y1 - p.Y0) * (stop - p.T0) / span;
                double local;
                if (TrySweptCircle((originX - p.X0) + dx * (length + travel * p.T0), -p.Y0 + dy * (length + travel * p.T0), (originX - bx) + dx * (length + travel * stop), -by + dy * (length + travel * stop), 0, 0, 0, 0, radius, out local))
                {
                    time = p.T0 + (stop - p.T0) * local;
                    return true;
                }
            }

            return false;
        }
    }
}
