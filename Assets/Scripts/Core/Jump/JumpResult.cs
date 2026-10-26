namespace XanaduYonder.Core.Jump
{
    public enum JumpOutcome { Stayed, Landed, Perfect, Missed }

    /// <summary>Immutable animation data in the coordinates used before the jump rebased the game.</summary>
    public sealed class JumpResult
    {
        public JumpOutcome Outcome { get; private set; }
        public double StartX { get; private set; }
        public double StartZ { get; private set; }
        public double EndX { get; private set; }
        public double EndZ { get; private set; }
        public double TargetX { get; private set; }
        public double TargetZ { get; private set; }
        public double Distance { get; private set; }
        public long DeltaScore { get; private set; }

        internal JumpResult(JumpOutcome outcome, double startX, double startZ, double endX, double endZ,
            double targetX, double targetZ, double distance, long deltaScore)
        {
            Outcome = outcome; StartX = startX; StartZ = startZ; EndX = endX; EndZ = endZ;
            TargetX = targetX; TargetZ = targetZ; Distance = distance; DeltaScore = deltaScore;
        }
    }
}
