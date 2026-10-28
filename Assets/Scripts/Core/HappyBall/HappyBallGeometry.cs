using System;

namespace XanaduYonder.Core.HappyBall
{
    public static class HappyBallGeometry
    {
        public const double Tau = Math.PI * 2;
        public static double NormalizeAngle(double angle)
        {
            double result = angle % Tau;
            if (result < 0) result += Tau;
            return result >= Tau ? 0 : result;
        }

        // Exact distance from the ball's horizontal center to the solid annular sector.
        // Angles follow (sin(a) * r, -cos(a) * r); Angle is the wall's centre.
        public static double SectorDistance(double angle, double start, double sweep)
        {
            double relative = NormalizeAngle(angle - start);
            if (relative <= sweep || sweep >= Tau - 1e-12) return 0;
            return Math.Min(EdgeDistance(relative), EdgeDistance(relative - sweep));
        }

        private static double EdgeDistance(double delta)
        {
            double projected = HappyBallRules.OrbitRadius * Math.Cos(delta);
            double radius = Math.Max(HappyBallRules.InnerRadius, Math.Min(HappyBallRules.OuterRadius, projected));
            double radial = radius - HappyBallRules.OrbitRadius * Math.Cos(delta);
            double tangent = HappyBallRules.OrbitRadius * Math.Sin(delta);
            return Math.Sqrt(radial * radial + tangent * tangent);
        }

        // Derivative of half the squared horizontal distance with respect to local
        // angle. The closest radius is clamped to the radial edge segment; its own
        // derivative vanishes at the minimum (including each clamped endpoint).
        internal static double SectorSquaredDistanceSlope(double angle, double start, double sweep)
        {
            double relative = NormalizeAngle(angle - start);
            if (relative <= sweep || sweep >= Tau - 1e-12) return 0;
            double delta = EdgeDistance(relative) <= EdgeDistance(relative - sweep) ? relative : relative - sweep;
            double radius = Math.Max(HappyBallRules.InnerRadius,
                Math.Min(HappyBallRules.OuterRadius, HappyBallRules.OrbitRadius * Math.Cos(delta)));
            return HappyBallRules.OrbitRadius * radius * Math.Sin(delta);
        }

        internal static double PrismDistance(double angle, double y, double start, double sweep, double bottom, double top)
        {
            double horizontal = SectorDistance(angle, start, sweep);
            double vertical = y > top ? y - top : y < bottom ? bottom - y : 0;
            return Math.Sqrt(horizontal * horizontal + vertical * vertical);
        }
    }
}
