using System;

namespace XanaduYonder.Core.Watermelon
{
    public static class WatermelonRules
    {
        public const int LevelCount = 11, MaxBalls = 128;
        public const float Width = 6, Height = 7.4f, SpawnY = 6.95f, DangerY = 6.25f, DropInterval = .38f, DangerDuration = 2;
        public const int StartingLevelCount = 5;
        public const float DangerMinimumAge = 1.2f, DangerMaximumSpeed = .5f, MaximumStep = .25f;
        public const float MaximumAge = 86400;

        private static readonly float[] Radii = { .18f, .25f, .32f, .38f, .45f, .54f, .64f, .75f, .87f, 1f, 1.16f };

        public static float GetRadius(int level)
        {
            if (level < 0 || level >= LevelCount) throw new ArgumentOutOfRangeException(nameof(level));
            return Radii[level];
        }

        internal static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        // A save may capture solver penetration or an upward impulse. These bounds reject corrupt
        // external data without forcing moving bodies back inside their walls during restoration.
        internal static bool HasValidMotion(WatermelonBallState ball)
        {
            return ball != null && IsFinite(ball.X) && ball.X >= -4 && ball.X <= Width + 4
                && IsFinite(ball.Y) && ball.Y >= -4 && ball.Y <= Height + 6
                && IsFinite(ball.VelocityX) && Math.Abs(ball.VelocityX) <= 100
                && IsFinite(ball.VelocityY) && Math.Abs(ball.VelocityY) <= 100
                && IsFinite(ball.Angle) && IsFinite(ball.AngularVelocity) && Math.Abs(ball.AngularVelocity) <= 20000;
        }

        internal static float Clamp(float value, float minimum, float maximum) { return Math.Max(minimum, Math.Min(maximum, value)); }
    }
}
