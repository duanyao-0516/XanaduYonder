namespace XanaduYonder.Core.FlappyBird
{
    public static class FlappyRules
    {
        public const double Width = 360, Height = 600, GroundHeight = 32.4;
        public const double PlayHeight = Height - GroundHeight;
        public const double BirdX = .28 * Width, BirdRadius = 21;
        public const double FixedStep = 1.0 / 60, PipeWidth = .16 * Width;
        public const double PipeCapHeight = PipeWidth / 97 * 36;
        public const double PipeStemWidth = 46.7, PipeStemInset = (PipeWidth - PipeStemWidth) / 2;
        public const double PipeStemCapOffset = PipeCapHeight * .55;
        public const double GapHeight = .32 * PlayHeight, Speed = .30 * Width;
        public const double Gravity = 2.2 * PlayHeight, FlapVelocity = .80 * PlayHeight;
        public const double TerminalVelocity = .95 * PlayHeight;
        public const int PipeIntervalTicks = 132, FirstPassTick = 192, MaximumPipes = 4;
        public const double DyingSeconds = .45, RestartGuardSeconds = .35;
        internal const double Epsilon = 1e-9;
    }

    public struct FlappyStepResult
    {
        public bool Flapped, Died;
        public int Points;
    }
}
