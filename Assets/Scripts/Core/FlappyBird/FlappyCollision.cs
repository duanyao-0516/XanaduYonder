using System;
namespace XanaduYonder.Core.FlappyBird
{
    internal static class FlappyCollision
    {
        internal static bool SweepPipe(double x0, double y0, double x1, double y1, FlappyPipe pipe, out double time)
        {
            double lower = pipe.GapCenter - FlappyRules.GapHeight / 2;
            double upper = pipe.GapCenter + FlappyRules.GapHeight / 2;
            double left = pipe.X, right = left + FlappyRules.PipeWidth;
            double inset = FlappyRules.PipeStemInset;
            time = double.PositiveInfinity;
            AddRectangle(x0, y0, x1, y1, left, lower - FlappyRules.PipeCapHeight, right, lower, ref time);
            AddRectangle(x0, y0, x1, y1, left, upper, right, upper + FlappyRules.PipeCapHeight, ref time);
            AddRectangle(x0, y0, x1, y1, left + inset, 0, right - inset, lower - FlappyRules.PipeStemCapOffset, ref time);
            AddRectangle(x0, y0, x1, y1, left + inset, upper + FlappyRules.PipeStemCapOffset, right - inset, FlappyRules.PlayHeight, ref time);
            return time <= 1;
        }
        private static void AddRectangle(double x0, double y0, double x1, double y1, double left, double bottom,
            double right, double top, ref double time)
        {
            double contact;
            if (Sweep(x0, y0, x1, y1, left, bottom, right, top, FlappyRules.BirdRadius, out contact)) time = Math.Min(time, contact);
        }

        // Sweep a circle center against a rectangle. The Minkowski boundary is four faces
        // plus four corner disks; checking their union preserves the rounded corners.
        internal static bool Sweep(double x0, double y0, double x1, double y1,
            double left, double bottom, double right, double top, double radius, out double time)
        {
            double closestX = Math.Max(left, Math.Min(right, x0));
            double closestY = Math.Max(bottom, Math.Min(top, y0));
            double dx0 = x0 - closestX, dy0 = y0 - closestY;
            time = double.PositiveInfinity;
            if (dx0 * dx0 + dy0 * dy0 <= radius * radius + FlappyRules.Epsilon) { time = 0; return true; }
            double dx = x1 - x0, dy = y1 - y0;
            Face(x0, y0, dx, dy, left - radius, bottom, top, ref time);
            Face(x0, y0, dx, dy, right + radius, bottom, top, ref time);
            Face(y0, x0, dy, dx, bottom - radius, left, right, ref time);
            Face(y0, x0, dy, dx, top + radius, left, right, ref time);
            Corner(x0, y0, dx, dy, left, bottom, radius, ref time);
            Corner(x0, y0, dx, dy, left, top, radius, ref time);
            Corner(x0, y0, dx, dy, right, bottom, radius, ref time);
            Corner(x0, y0, dx, dy, right, top, radius, ref time);
            return time <= 1;
        }
        private static void Face(double a, double b, double da, double db, double face, double min, double max, ref double time)
        {
            if (Math.Abs(da) < FlappyRules.Epsilon) return;
            double t = (face - a) / da, other = b + db * t;
            if (t >= -FlappyRules.Epsilon && t <= 1 + FlappyRules.Epsilon && other >= min && other <= max)
                time = Math.Min(time, Math.Max(0, Math.Min(1, t)));
        }
        private static void Corner(double x, double y, double dx, double dy, double cx, double cy, double radius, ref double time)
        {
            double a = dx * dx + dy * dy;
            if (a < FlappyRules.Epsilon) return;
            double ox = x - cx, oy = y - cy;
            double b = 2 * (ox * dx + oy * dy), c = ox * ox + oy * oy - radius * radius;
            double discriminant = b * b - 4 * a * c;
            if (discriminant < -FlappyRules.Epsilon) return;
            double t = (-b - Math.Sqrt(Math.Max(0, discriminant))) / (2 * a);
            if (t >= -FlappyRules.Epsilon && t <= 1 + FlappyRules.Epsilon)
                time = Math.Min(time, Math.Max(0, Math.Min(1, t)));
        }
    }
}
