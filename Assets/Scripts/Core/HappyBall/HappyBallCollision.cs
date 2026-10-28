using System;

namespace XanaduYonder.Core.HappyBall
{
    internal sealed class HappyBallContact
    {
        internal double Time;
        internal double Top;
        internal double Bottom;
        internal double HorizontalDistance;
        internal double HorizontalApproach;
        internal bool Dangerous;
        internal bool Wall;
        internal HappyBallLayer Layer;
    }

    internal static class HappyBallCollision
    {
        internal static double Y(double y, double velocity, double time)
        {
            double terminalTime = Math.Max(0, (velocity + HappyBallRules.TerminalFallSpeed) / HappyBallRules.Gravity);
            if (time <= terminalTime) return y + velocity * time - HappyBallRules.Gravity * time * time / 2;
            return y + velocity * terminalTime - HappyBallRules.Gravity * terminalTime * terminalTime / 2 -
                HappyBallRules.TerminalFallSpeed * (time - terminalTime);
        }

        internal static double Velocity(double velocity, double time)
        { return Math.Max(-HappyBallRules.TerminalFallSpeed, velocity - HappyBallRules.Gravity * time); }

        internal static HappyBallContact First(HappyBallSnapshot state, double duration, double angularSpeed)
        {
            HappyBallContact earliest = null;
            foreach (var layer in state.Layers)
            {
                if (layer.State != HappyBallLayerState.Active) continue;
                foreach (var sector in layer.Sectors)
                    Consider(state, duration, angularSpeed, layer, sector.StartAngle, sector.SweepAngle,
                        layer.Y - HappyBallRules.PlateThickness, layer.Y, sector.Dangerous, false, ref earliest);
                foreach (var wall in layer.Walls)
                    Consider(state, duration, angularSpeed, layer, wall.Angle - wall.WidthAngle / 2, wall.WidthAngle,
                        layer.Y, layer.Y + wall.Height, false, true, ref earliest);
            }
            return earliest;
        }

        private static void Consider(HappyBallSnapshot state, double duration, double angularSpeed, HappyBallLayer layer,
            double start, double sweep, double bottom, double top, bool dangerous, bool wall, ref HappyBallContact earliest)
        {
            double limit = earliest == null ? duration : Math.Min(duration, earliest.Time + 1e-7);
            double endY = Y(state.BallY, state.BallVelocity, limit);
            double apexTime = Math.Max(0, Math.Min(limit, state.BallVelocity / HappyBallRules.Gravity));
            double maxY = Math.Max(Math.Max(state.BallY, endY), Y(state.BallY, state.BallVelocity, apexTime));
            if (maxY < bottom - HappyBallRules.BallRadius - HappyBallRules.ContactTolerance ||
                Math.Min(state.BallY, endY) > top + HappyBallRules.BallRadius + HappyBallRules.ContactTolerance) return;
            double speedBound = Math.Abs(angularSpeed) * HappyBallRules.OrbitRadius +
                Math.Max(Math.Abs(state.BallVelocity), Math.Abs(Velocity(state.BallVelocity, limit)));
            if (speedBound < 1e-12) speedBound = HappyBallRules.Gravity * limit + 1e-9;
            double t = 0;
            // Distance to a closed solid is 1-Lipschitz. Each advance is smaller than
            // clearance / maximum point speed, so it cannot jump through any thin face.
            while (t <= limit)
            {
                double angle = -state.TowerAngle - angularSpeed * t;
                double y = Y(state.BallY, state.BallVelocity, t);
                double clearance = HappyBallGeometry.PrismDistance(angle, y, start, sweep, bottom, top) - HappyBallRules.BallRadius;
                if (clearance <= HappyBallRules.ContactTolerance)
                {
                    double probe = t + 1e-6;
                    double nextDistance = HappyBallGeometry.PrismDistance(-state.TowerAngle - angularSpeed * probe,
                        Y(state.BallY, state.BallVelocity, probe), start, sweep, bottom, top) - HappyBallRules.BallRadius;
                    if (clearance >= -HappyBallRules.ContactTolerance && nextDistance > clearance + 1e-12)
                    { t = probe; continue; }
                    if (t > limit) return;
                    var hit = new HappyBallContact { Time = t, Top = top, Bottom = bottom, Dangerous = dangerous, Wall = wall,
                        Layer = layer, HorizontalDistance = HappyBallGeometry.SectorDistance(angle, start, sweep),
                        HorizontalApproach = Math.Max(0, angularSpeed * HappyBallGeometry.SectorSquaredDistanceSlope(angle, start, sweep)) };
                    if (earliest == null || t < earliest.Time - 1e-7 ||
                        (Math.Abs(t - earliest.Time) <= 1e-7 && dangerous && !earliest.Dangerous)) earliest = hit;
                    return;
                }
                double advance = Math.Max(1e-10, clearance / speedBound * 0.9);
                if (t + advance > limit)
                {
                    if (limit - t < 1e-12) return;
                    t = limit;
                }
                else t += advance;
            }
        }

        internal static double PassTime(HappyBallSnapshot state, HappyBallLayer layer, double duration)
        {
            double target = layer.Y - HappyBallRules.PlateThickness - HappyBallRules.BallRadius - HappyBallRules.ContactTolerance;
            if (state.BallY <= target && state.BallVelocity <= 0) return 0;
            if (Velocity(state.BallVelocity, duration) >= 0) return double.PositiveInfinity;
            if (Y(state.BallY, state.BallVelocity, duration) > target) return double.PositiveInfinity;
            double lo = Math.Max(0, Math.Min(duration, state.BallVelocity / HappyBallRules.Gravity));
            double hi = duration;
            for (int i = 0; i < 45; i++)
            {
                double middle = (lo + hi) / 2;
                if (Y(state.BallY, state.BallVelocity, middle) > target) lo = middle; else hi = middle;
            }
            return hi;
        }
    }
}
