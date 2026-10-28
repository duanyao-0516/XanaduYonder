using System;

namespace XanaduYonder.Core.HappyBall
{
    public enum HappyBallLayerState { Active, Passed, Broken }
    public enum HappyBallPower { Normal, Armed, Spent }
    public enum HappyBallEventKind { Bounce, LayerPassed, LayerBroken, Blocked, Armed, Died }

    [Serializable]
    public sealed class HappyBallSector
    {
        public double StartAngle;
        public double SweepAngle;
        public bool Dangerous;
        public HappyBallSector Copy() { return (HappyBallSector)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class HappyBallWall
    {
        public double Angle;
        public double WidthAngle;
        public double Height;
        public HappyBallWall Copy() { return (HappyBallWall)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class HappyBallLayer
    {
        public int Index;
        public double Y;
        public HappyBallSector[] Sectors;
        public HappyBallWall[] Walls;
        public HappyBallLayerState State;
        public HappyBallLayer Copy()
        {
            var copy = (HappyBallLayer)MemberwiseClone();
            if (Sectors != null)
            {
                copy.Sectors = new HappyBallSector[Sectors.Length];
                for (int i = 0; i < Sectors.Length; i++) copy.Sectors[i] = Sectors[i] == null ? null : Sectors[i].Copy();
            }
            if (Walls != null)
            {
                copy.Walls = new HappyBallWall[Walls.Length];
                for (int i = 0; i < Walls.Length; i++) copy.Walls[i] = Walls[i] == null ? null : Walls[i].Copy();
            }
            return copy;
        }
    }

    public sealed class HappyBallEvent
    {
        public HappyBallEventKind Kind;
        public int LayerIndex;
        public long ScoreDelta;
        // Collision or physical-passage pose, captured before subsequent fixed steps.
        public bool HasContact;
        public double ContactBallY;
        public double ContactTowerAngle;
        public int ContactOriginLayer;
    }

    public sealed class HappyBallStepResult
    {
        public HappyBallEvent[] Events = new HappyBallEvent[0];
    }

    [Serializable]
    public sealed class HappyBallSnapshot
    {
        public int RulesVersion = HappyBallRules.Version;
        public uint RandomState;
        public int NextLayerIndex;
        public int OriginLayer;
        public int PassedLayers;
        public int NaturalStreak;
        public double TowerAngle;
        public double BallY;
        public double BallVelocity;
        public long Score;
        public HappyBallPower Power;
        public int ImpactLayerIndex = -1;
        public bool GameOver;
        public HappyBallLayer[] Layers;

        public HappyBallSnapshot Copy()
        {
            var copy = (HappyBallSnapshot)MemberwiseClone();
            if (Layers != null)
            {
                copy.Layers = new HappyBallLayer[Layers.Length];
                for (int i = 0; i < Layers.Length; i++) copy.Layers[i] = Layers[i] == null ? null : Layers[i].Copy();
            }
            return copy;
        }

        public bool TryValidate(out string reason)
        {
            reason = null;
            if (RulesVersion != HappyBallRules.Version && RulesVersion != HappyBallRules.LegacyVersion) reason = "Unsupported rules version.";
            else if (RandomState == 0) reason = "Random state must be nonzero.";
            else if (OriginLayer < 0 || PassedLayers < 0 || NextLayerIndex <= 0 || NextLayerIndex > int.MaxValue - 64 ||
                PassedLayers >= NextLayerIndex || OriginLayer > PassedLayers) reason = "Invalid layer counters.";
            else if (NaturalStreak < 0 || NaturalStreak > PassedLayers || Score < PassedLayers) reason = "Invalid score or streak.";
            else if (!Finite(TowerAngle) || TowerAngle < 0 || TowerAngle >= Math.PI * 2 || !Finite(BallY) || Math.Abs(BallY) > 4096 ||
                !Finite(BallVelocity) || Math.Abs(BallVelocity) > 10000) reason = "Invalid motion.";
            else if (!Enum.IsDefined(typeof(HappyBallPower), Power)) reason = "Invalid power.";
            else if (Power == HappyBallPower.Armed && NaturalStreak < HappyBallRules.SmashThreshold) reason = "Armed without natural streak.";
            else if (Power == HappyBallPower.Normal && NaturalStreak >= HappyBallRules.SmashThreshold) reason = "Charged natural streak without armed power.";
            else if (Power == HappyBallPower.Spent && NaturalStreak != 0 &&
                (RulesVersion != HappyBallRules.LegacyVersion || NaturalStreak < HappyBallRules.SmashThreshold))
                reason = "Spent streak is not a recognized legacy impact.";
            else if (Power != HappyBallPower.Spent && ImpactLayerIndex != -1) reason = "Unexpected impact layer.";
            else if (Layers == null || Layers.Length == 0 || Layers.Length > HappyBallRules.MaximumLayers) reason = "Invalid layer window.";
            if (reason != null) return false;
            double layerSpacing = RulesVersion == HappyBallRules.LegacyVersion ? HappyBallRules.LegacyLayerSpacing : HappyBallRules.LayerSpacing;
            double maximumWallHeight = RulesVersion == HappyBallRules.LegacyVersion ? HappyBallRules.LegacyMaximumWallHeight : HappyBallRules.MaximumWallHeight;
            bool impactFound = false;
            for (int i = 0; i < Layers.Length; i++)
            {
                var layer = Layers[i];
                if (layer == null || layer.Index < 0 || layer.Index >= NextLayerIndex ||
                    (i > 0 && layer.Index != Layers[i - 1].Index + 1) || !Finite(layer.Y) ||
                    Math.Abs(layer.Y - (OriginLayer - layer.Index) * layerSpacing) > 0.00001 ||
                    !Enum.IsDefined(typeof(HappyBallLayerState), layer.State))
                { reason = "Invalid layer identity, height or order."; return false; }
                if (layer.Sectors == null || layer.Sectors.Length > 32 || layer.Walls == null || layer.Walls.Length > 8)
                { reason = "Invalid layer geometry collection."; return false; }
                foreach (var sector in layer.Sectors)
                {
                    if (sector == null || !Finite(sector.StartAngle) || sector.StartAngle < 0 || sector.StartAngle >= Math.PI * 2 ||
                        !Finite(sector.SweepAngle) || sector.SweepAngle <= 0 || sector.SweepAngle > Math.PI * 2)
                    { reason = "Invalid sector."; return false; }
                }
                foreach (var wall in layer.Walls)
                {
                    if (wall == null || !Finite(wall.Angle) || wall.Angle < 0 || wall.Angle >= Math.PI * 2 ||
                        !Finite(wall.WidthAngle) || wall.WidthAngle <= 0 || wall.WidthAngle > Math.PI ||
                        !Finite(wall.Height) || wall.Height <= 0 || wall.Height > maximumWallHeight)
                    { reason = "Invalid wall."; return false; }
                }
                if (layer.State == HappyBallLayerState.Active)
                {
                    foreach (var sector in layer.Sectors)
                        if (HappyBallGeometry.PrismDistance(-TowerAngle, BallY, sector.StartAngle, sector.SweepAngle,
                            layer.Y - HappyBallRules.PlateThickness, layer.Y) < HappyBallRules.BallRadius - HappyBallRules.ContactTolerance * 4)
                        { reason = "Ball penetrates an active plate."; return false; }
                    foreach (var wall in layer.Walls)
                        if (HappyBallGeometry.PrismDistance(-TowerAngle, BallY, wall.Angle - wall.WidthAngle / 2, wall.WidthAngle,
                            layer.Y, layer.Y + wall.Height) < HappyBallRules.BallRadius - HappyBallRules.ContactTolerance * 4)
                        { reason = "Ball penetrates an active wall."; return false; }
                }
                if (layer.State == HappyBallLayerState.Active && layer.Index < PassedLayers)
                { reason = "An already passed layer is active."; return false; }
                if (layer.State == HappyBallLayerState.Passed && layer.Index >= PassedLayers)
                { reason = "Uncounted passed layer."; return false; }
                if (layer.State == HappyBallLayerState.Broken && layer.Index >= PassedLayers &&
                    (Power != HappyBallPower.Spent || layer.Index != ImpactLayerIndex || layer.Index != PassedLayers))
                { reason = "Uncounted broken layer is not the current impact."; return false; }
                if (layer.Index == ImpactLayerIndex && layer.State == HappyBallLayerState.Broken)
                {
                    impactFound = true;
                    // A spent smash retains its broken layer throughout the upward
                    // rebound and return fall. Bound the whole ballistic arc, while
                    // retaining the underside boundary that triggers physical passage.
                    double apex = layer.Y + HappyBallRules.BallRadius +
                        HappyBallRules.BounceSpeed * HappyBallRules.BounceSpeed / (2 * HappyBallRules.Gravity);
                    double trajectoryApex = BallY + BallVelocity * BallVelocity / (2 * HappyBallRules.Gravity);
                    if (BallVelocity > HappyBallRules.BounceSpeed + HappyBallRules.ContactTolerance * 4 ||
                        trajectoryApex > apex + HappyBallRules.ContactTolerance * 4 ||
                        BallY < layer.Y - HappyBallRules.PlateThickness - HappyBallRules.BallRadius - HappyBallRules.ContactTolerance * 4)
                    { reason = "Spent motion lies outside the impact rebound."; return false; }
                }
            }
            if (Layers[Layers.Length - 1].Index != NextLayerIndex - 1)
            { reason = "Generator cursor does not follow the window."; return false; }
            if (Layers[0].Index > PassedLayers)
            { reason = "Window skips the current layer."; return false; }
            if (Power == HappyBallPower.Spent && (!impactFound || ImpactLayerIndex != PassedLayers))
            { reason = "Spent power must reference the next broken layer."; return false; }
            return true;
        }

        internal static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}
