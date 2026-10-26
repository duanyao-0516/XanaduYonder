using System;

namespace XanaduYonder.Core.Jump
{
    /// <summary>Deterministic rules independent of physics, frame rate and Unity objects.</summary>
    public sealed class JumpGame
    {
        public const double MinDistance = .35;
        public const double DistancePerSecond = 3.6;
        public const double MaxChargeSeconds = 1.35;
        public const double PerfectRadius = .16;
        public const double MaxDistance = MinDistance + DistancePerSecond * MaxChargeSeconds;

        private readonly JumpSnapshot state;

        public JumpGame(uint seed)
        {
            state = new JumpSnapshot {
                CurrentHalfSize = .95,
                NextX = 2.8,
                NextZ = 0,
                NextHalfSize = .85,
                RandomState = seed == 0 ? 0x6D2B79F5u : seed
            };
        }

        public JumpGame(JumpSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            string error;
            if (!snapshot.TryValidate(out error)) throw new ArgumentException(error, nameof(snapshot));
            state = snapshot.Copy();
        }

        public JumpSnapshot GetSnapshot() { return state.Copy(); }

        /// <summary>
        /// Commits the logical landing immediately. Animate using the returned pre-rebase coordinates,
        /// then render the new snapshot. A short hop on the same platform resets the perfect streak.
        /// </summary>
        public JumpResult Jump(double heldSeconds)
        {
            if (!JumpSnapshot.IsFinite(heldSeconds) || heldSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(heldSeconds), "Charge duration must be finite and nonnegative.");
            if (state.IsGameOver) throw new InvalidOperationException("The game has finished.");

            double startX = state.PlayerX;
            double startZ = state.PlayerZ;
            double targetX = state.NextX;
            double targetZ = state.NextZ;
            double dx = targetX - startX;
            double dz = targetZ - startZ;
            double centerDistance = Math.Sqrt(dx * dx + dz * dz);
            double distance = MinDistance + DistancePerSecond * Math.Min(heldSeconds, MaxChargeSeconds);
            double endX = startX + dx / centerDistance * distance;
            double endZ = startZ + dz / centerDistance * distance;
            long delta = 0;
            JumpOutcome outcome;

            if (JumpSnapshot.Contains(endX, endZ, targetX, targetZ, state.NextHalfSize))
            {
                double errorX = endX - targetX;
                double errorZ = endZ - targetZ;
                bool perfect = Math.Sqrt(errorX * errorX + errorZ * errorZ) <= PerfectRadius + JumpSnapshot.EdgeTolerance;
                state.Streak = perfect ? Increment(state.Streak) : 0;
                long award = perfect ? state.Streak * 2L : 1L;
                delta = Math.Min(award, long.MaxValue - state.Score);
                state.Score += delta;
                state.Steps = Increment(state.Steps);
                state.CurrentHalfSize = state.NextHalfSize;
                state.PlayerX = ClampToPlatform(errorX, state.CurrentHalfSize);
                state.PlayerZ = ClampToPlatform(errorZ, state.CurrentHalfSize);
                outcome = perfect ? JumpOutcome.Perfect : JumpOutcome.Landed;
                GenerateTarget();
            }
            else if (JumpSnapshot.Contains(endX, endZ, 0, 0, state.CurrentHalfSize))
            {
                state.PlayerX = ClampToPlatform(endX, state.CurrentHalfSize);
                state.PlayerZ = ClampToPlatform(endZ, state.CurrentHalfSize);
                state.Streak = 0;
                outcome = JumpOutcome.Stayed;
            }
            else
            {
                state.PlayerX = endX;
                state.PlayerZ = endZ;
                state.Streak = 0;
                state.IsGameOver = true;
                outcome = JumpOutcome.Missed;
            }
            return new JumpResult(outcome, startX, startZ, endX, endZ, targetX, targetZ, distance, delta);
        }

        private void GenerateTarget()
        {
            bool alongX = NextUnit() < .5;
            // Even the opposite corner of a half-size 1 platform is within the 5.21-unit charge limit.
            double distance = 2.5 + NextUnit() * 1.5;
            double maximumHalfSize = Math.Max(.7, 1 - Math.Min(state.Steps, 100) * .003);
            state.NextHalfSize = .6 + NextUnit() * (maximumHalfSize - .6);
            state.NextX = alongX ? distance : 0;
            state.NextZ = alongX ? 0 : distance;
        }

        private double NextUnit()
        {
            uint value = state.RandomState;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state.RandomState = value;
            return value / 4294967296.0;
        }

        private static int Increment(int value) { return value == int.MaxValue ? value : value + 1; }
        private static double ClampToPlatform(double value, double halfSize) { return Math.Max(-halfSize, Math.Min(halfSize, value)); }
    }
}
