using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Dino
{
    /// <summary>One immutable ruleset for the actual visible track, independent of orientation.</summary>
    public sealed class DinoRules
    {
        public const double StepSeconds = 1.0 / 60;
        public const double SpriteScale = 1.35;
        // High-speed mode deliberately trades the old response margin for a faster challenge.
        public const double ReactionSeconds = .15;
        private const double ViewportBaselineReactionSeconds = 1.2;
        private const double PeakSpeedMultiplier = 4;
        public const double InputBudgetSeconds = .1;
        public const double RecoverySeconds = .25;
        internal const int Substeps = 4;
        internal const double Gravity = 1500;
        internal const double JumpVelocity = 580;
        internal const double MinimumJumpHeight = 60;
        internal const double ReleasedVelocity = 180;
        internal const int MaximumPlanTicks = 2048;
        private const double InitialSpeed = 180;
        private const double Acceleration = 24;
        private const double SpeedPerTick = Acceleration * StepSeconds;
        private static readonly Dictionary<double, double> ViewportBaselineCaps = new Dictionary<double, double>();

        public double ViewportWidth { get; private set; }
        public double PlayerX { get; private set; }
        public double PlayerWidth { get { return 44 * SpriteScale; } }
        public double PlayerHeight { get { return 47 * SpriteScale; } }
        public double DuckWidth { get { return 59 * SpriteScale; } }
        public double DuckHeight { get { return 25 * SpriteScale; } }
        // A conservative display bound for a held jump, independent of integration rounding.
        public double MaximumJumpHeight { get { return JumpVelocity * JumpVelocity / (2 * Gravity); } }
        public double MaxSpeed { get; private set; }

        public DinoRules(double viewportWidth)
        {
            // A smaller or unbounded viewport cannot meet the legibility and response contract.
            if (double.IsNaN(viewportWidth) || double.IsInfinity(viewportWidth) || viewportWidth < 320 || viewportWidth > 4096)
                throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Track width must be finite and between 320 and 4096.");
            ViewportWidth = viewportWidth;
            PlayerX = viewportWidth * .12;
            lock (ViewportBaselineCaps) {
                double cap;
                if (!ViewportBaselineCaps.TryGetValue(viewportWidth, out cap)) {
                    cap = FindViewportBaselineCap();
                    // Layout widths are usually stable, but do not retain an unbounded resize history.
                    if (ViewportBaselineCaps.Count < 128) ViewportBaselineCaps.Add(viewportWidth, cap);
                }
                // Preserve the established width adaptation, then apply the requested real speed increase.
                // Generation separately proves physical passage and the high-speed response floor.
                MaxSpeed = cap * PeakSpeedMultiplier;
            }
        }

        private double FindViewportBaselineCap()
        {
            // Each candidate uses the actual jump/collision solver, including its entire width.
            // A geometrically impossible group is excluded at generation, never waved through.
            double low = 60, high = 360;
            if (BaselineSafeAt(high)) return high;
            for (int i = 0; i < 11; i++) {
                double middle = (low + high) * .5;
                if (BaselineSafeAt(middle)) low = middle; else high = middle;
            }
            // Round downward and re-prove the returned speed, rather than round up across a boundary.
            double result = Math.Floor(low * 4) / 4;
            while (result > 60 && !BaselineSafeAt(result)) result -= .25;
            return result;
        }

        private bool BaselineSafeAt(double speed)
        {
            bool singlePossible = false;
            for (int kind = 0; kind < 4; kind++) {
                int groups = kind < 2 ? 3 : 1;
                for (int group = 1; group <= groups; group++) {
                    DinoPlan plan;
                    if (!DinoTraversal.TryPlan(this, (DinoObstacleKind)kind, group, 0, speed, out plan)) continue;
                    if (kind == 0 && group == 1) singlePossible = true;
                    if (plan.ReactionWindowSeconds + 1e-9 < ViewportBaselineReactionSeconds) return false;
                }
            }
            return singlePossible;
        }

        internal double SpeedAt(long tick) { return Math.Min(MaxSpeed, InitialSpeed + tick * SpeedPerTick); }

        internal double Travel(long fromTick, long toTick)
        {
            if (toTick <= fromTick) return 0;
            // Exact sum of the same per-tick speeds used by Step, avoiding a horizon-sized allocation.
            long acceleratingLast = MaxSpeed <= InitialSpeed ? 0 : (long)Math.Floor((MaxSpeed - InitialSpeed) / SpeedPerTick);
            long end = Math.Min(toTick, acceleratingLast);
            long start = Math.Min(fromTick, acceleratingLast);
            double count = end - start;
            double accelerated = count * InitialSpeed + (start + 1.0 + end) * count * SpeedPerTick * .5;
            double capped = (toTick - fromTick - count) * MaxSpeed;
            return (accelerated + capped) * StepSeconds;
        }
    }
}
