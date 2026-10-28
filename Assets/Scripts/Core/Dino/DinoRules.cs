// Portions derived from Chromium, Copyright 2024 The Chromium Authors.
// Use of this source code is governed by the BSD-style license in docs/chromium-dino-reference/source/LICENSE.
using System;

namespace XanaduYonder.Core.Dino
{
    /// <summary>Chromium normal mode at a fixed 60 Hz; public geometry and speed use scaled world units.</summary>
    public sealed class DinoRules
    {
        public const double StepSeconds = 1.0 / 60;
        public const double SpriteScale = 1.35;
        internal const int MaximumPlanTicks = 2048;
        internal const double OriginalMaximumSpeed = 13;
        internal const double OriginalAcceleration = .001;
        internal const long ClearTicks = 180;
        private readonly double initialSpeed;

        public double ViewportWidth { get; private set; }
        public double PlayerX { get { return 50 * SpriteScale; } }
        public double PlayerWidth { get { return 44 * SpriteScale; } }
        public double PlayerHeight { get { return 47 * SpriteScale; } }
        public double DuckWidth { get { return 59 * SpriteScale; } }
        public double DuckHeight { get { return 25 * SpriteScale; } }
        // The absolute-y cutoff, one frame of overshoot and remaining -5 flight fit within 100 source pixels.
        public double MaximumJumpHeight { get { return 100 * SpriteScale; } }
        public double MaxSpeed { get { return OriginalMaximumSpeed * 60 * SpriteScale; } }

        public DinoRules(double viewportWidth)
        {
            if (double.IsNaN(viewportWidth) || double.IsInfinity(viewportWidth) || viewportWidth < 320 || viewportWidth > 4096)
                throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Track width must be finite and between 320 and 4096.");
            ViewportWidth = viewportWidth;
            double canvasWidth = viewportWidth / SpriteScale;
            initialSpeed = canvasWidth < 600 ? Math.Min(6, 6 * canvasWidth / 600 * 1.2) : 6;
        }

        internal double OriginalSpeedAt(long tick) { return Math.Min(OriginalMaximumSpeed, initialSpeed + tick * OriginalAcceleration); }
        internal double SpeedAt(long tick) { return OriginalSpeedAt(tick) * 60 * SpriteScale; }
        internal static double Round(double value) { return Math.Floor(value + .5); }
        internal static double ObstacleStep(double speed, double speedOffset) { return Math.Floor(speed + speedOffset) * SpriteScale; }

        internal double Travel(long fromTick, long toTick)
        {
            if (toTick <= fromTick) return 0;
            // Chrome moves with the current speed, then increments it. This sum uses [fromTick,toTick).
            long capTick = (long)Math.Ceiling((OriginalMaximumSpeed - initialSpeed) / OriginalAcceleration);
            long end = Math.Min(toTick, capTick), start = Math.Min(fromTick, capTick);
            double count = end - start;
            double accelerated = count * initialSpeed + (start + (double)end - 1) * count * OriginalAcceleration * .5;
            double capped = (toTick - fromTick - count) * OriginalMaximumSpeed;
            return (accelerated + capped) * SpriteScale;
        }
    }
}
