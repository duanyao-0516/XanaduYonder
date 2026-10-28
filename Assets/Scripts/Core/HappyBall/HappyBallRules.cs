namespace XanaduYonder.Core.HappyBall
{
    public static class HappyBallRules
    {
        public const int Version = 2;
        public const int LegacyVersion = 1;
        internal const double LegacyLayerSpacing = 1.65;
        internal const double LegacyMaximumWallHeight = 0.5;
        public const double FixedStep = 1.0 / 120.0;
        public const double BallRadius = 0.22;
        public const double OrbitRadius = 1.35;
        public const double InnerRadius = 1.0;
        public const double OuterRadius = 2.0;
        public const double LayerSpacing = 2.4;
        public const double PlateThickness = 0.24;
        // Leaves .22 units for the whole sphere to finish passing the preceding slab.
        public const double MaximumWallHeight = 1.50;
        public const double Gravity = 40.0;
        public const double BounceSpeed = 13.2;
        public const double TerminalFallSpeed = 24.0;
        public const int SmashThreshold = 3;
        public const int WindowLayers = 12;
        public const int MaximumLayers = 32;
        public const double ContactTolerance = 0.0000001;
    }
}
