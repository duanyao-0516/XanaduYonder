using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Dino
{
    /// <summary>One immutable ruleset for the actual visible track, independent of orientation.</summary>
    public sealed class DinoRules
    {
        public const double StepSeconds = 1.0 / 60;
        public const double SpriteScale = 1.35;
        public const double ReactionSeconds = 1.2;
        public const double InputBudgetSeconds = .1;
        public const double RecoverySeconds = .25;
        internal const int Substeps = 4;
        internal const double Gravity = 1500;
        internal const double JumpVelocity = 580;
        internal const double MinimumJumpHeight = 60;
        internal const double ReleasedVelocity = 180;
        internal const int MaximumPlanTicks = 2048;
        private static readonly Dictionary<double, double> SpeedCaps = new Dictionary<double, double>();

        public double ViewportWidth { get; private set; }
        public double PlayerX { get; private set; }
        public double PlayerWidth { get { return 44 * SpriteScale; } }
        public double PlayerHeight { get { return 47 * SpriteScale; } }
        public double DuckWidth { get { return 59 * SpriteScale; } }
        public double DuckHeight { get { return 25 * SpriteScale; } }
        public double MaxSpeed { get; private set; }

        public DinoRules(double viewportWidth)
        {
            // A smaller or unbounded viewport cannot meet the legibility and response contract.
            if (double.IsNaN(viewportWidth) || double.IsInfinity(viewportWidth) || viewportWidth < 320 || viewportWidth > 4096)
                throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Track width must be finite and between 320 and 4096.");
            ViewportWidth = viewportWidth;
            PlayerX = viewportWidth * .12;
            lock (SpeedCaps) {
                double cap;
                if (!SpeedCaps.TryGetValue(viewportWidth, out cap)) {
                    cap = FindSpeedCap();
                    // Layout widths are usually stable, but do not retain an unbounded resize history.
                    if (SpeedCaps.Count < 128) SpeedCaps.Add(viewportWidth, cap);
                }
                MaxSpeed = cap;
            }
        }

        private double FindSpeedCap()
        {
            // Each candidate uses the actual jump/collision solver, including its entire width.
            // A geometrically impossible group is excluded at generation, never waved through.
            double low = 60, high = 360;
            if (SafeAt(high)) return high;
            for (int i = 0; i < 11; i++) {
                double middle = (low + high) * .5;
                if (SafeAt(middle)) low = middle; else high = middle;
            }
            // Round downward and re-prove the returned speed, rather than round up across a boundary.
            double result = Math.Floor(low * 4) / 4;
            while (result > 60 && !SafeAt(result)) result -= .25;
            return result;
        }

        private bool SafeAt(double speed)
        {
            bool singlePossible = false;
            for (int kind = 0; kind < 4; kind++) {
                int groups = kind < 2 ? 3 : 1;
                for (int group = 1; group <= groups; group++) {
                    DinoPlan plan;
                    if (!DinoTraversal.TryPlan(this, (DinoObstacleKind)kind, group, 0, speed, out plan)) continue;
                    if (kind == 0 && group == 1) singlePossible = true;
                    if (plan.ReactionWindowSeconds + 1e-9 < ReactionSeconds) return false;
                }
            }
            return singlePossible;
        }

        internal double SpeedAt(long tick) { return Math.Min(MaxSpeed, 180 + Math.Min(tick, 5400) * StepSeconds * 2); }

        internal double Travel(long fromTick, long toTick)
        {
            if (toTick <= fromTick) return 0;
            // Exact sum of the same per-tick speeds used by Step, avoiding a horizon-sized allocation.
            long acceleratingLast = MaxSpeed <= 180 ? 0 : (long)Math.Floor((MaxSpeed - 180) * 30);
            long end = Math.Min(toTick, acceleratingLast);
            long start = Math.Min(fromTick, acceleratingLast);
            double count = end - start;
            double accelerated = count * 180 + (start + 1.0 + end) * count / 60;
            double capped = (toTick - fromTick - count) * MaxSpeed;
            return (accelerated + capped) * StepSeconds;
        }
    }
}
