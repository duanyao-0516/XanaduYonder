using System;

namespace XanaduYonder.Core.Jump
{
    /// <summary>Version-independent game state; the current platform is always centered at (0, 0).</summary>
    [Serializable]
    public sealed class JumpSnapshot
    {
        internal const double EdgeTolerance = 1e-9;
        public double CurrentHalfSize;
        public double NextX;
        public double NextZ;
        public double NextHalfSize;
        public double PlayerX;
        public double PlayerZ;
        public long Score;
        public int Steps;
        public int Streak;
        public bool IsGameOver;
        public uint RandomState;

        public JumpSnapshot Copy()
        {
            return (JumpSnapshot)MemberwiseClone();
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (!IsFinite(CurrentHalfSize) || !IsFinite(NextHalfSize) ||
                !IsFinite(NextX) || !IsFinite(NextZ) || !IsFinite(PlayerX) || !IsFinite(PlayerZ))
                error = "Positions and sizes must be finite.";
            else if (CurrentHalfSize < .6 || CurrentHalfSize > 1 || NextHalfSize < .6 || NextHalfSize > 1)
                error = "Platform half sizes must be between 0.6 and 1.";
            else if (!((NextX == 0 && NextZ >= 2.5 && NextZ <= 4) ||
                       (NextZ == 0 && NextX >= 2.5 && NextX <= 4)))
                error = "The next platform must be 2.5 to 4 units along one positive axis.";
            else if (RandomState == 0)
                error = "The random state cannot be zero.";
            else if (Steps < 0 || Score < Steps || Streak < 0 || Streak > Steps)
                error = "Score, successful jumps and perfect streak are inconsistent.";
            else if (IsGameOver && Streak != 0)
                error = "A missed jump must reset the perfect streak.";
            else if (Score < (long)Streak * (Streak + 1L) + (Steps - Streak) ||
                     (Steps < int.MaxValue && Score > (long)Steps * (Steps + 1L)))
                error = "Score is outside the possible range for these counters.";
            else if (Math.Abs(PlayerX) > 6.21 || Math.Abs(PlayerZ) > 6.21)
                error = "The player position is outside the renderable jump range.";
            else if (!IsGameOver && !Contains(PlayerX, PlayerZ, 0, 0, CurrentHalfSize))
                error = "An active player must be on the current platform.";
            else if (IsGameOver && (Contains(PlayerX, PlayerZ, 0, 0, CurrentHalfSize) ||
                                   Contains(PlayerX, PlayerZ, NextX, NextZ, NextHalfSize)))
                error = "A finished jump must land outside both platforms.";
            return error == null;
        }

        internal static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        internal static bool Contains(double x, double z, double centerX, double centerZ, double halfSize)
        {
            return Math.Abs(x - centerX) <= halfSize + EdgeTolerance &&
                   Math.Abs(z - centerZ) <= halfSize + EdgeTolerance;
        }
    }
}
