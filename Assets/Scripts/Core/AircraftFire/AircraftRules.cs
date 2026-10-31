using System;

namespace XanaduYonder.Core.AircraftFire
{
    // Values reconstructed from Aircraft Fire 2.5's native ClassicMode and StageMode tables.
    public sealed class AircraftStage
    {
        public int Duration { get; private set; }
        public int BossHp { get; private set; }
        public int BossBurst { get; private set; }
        public float BossFireRange { get; private set; }
        internal readonly float Speed, SmallInterval, MediumBase, MediumRange;
        internal readonly float DoubleBase, DoubleRange, BombBase, BombRange, RouteChance;
        internal AircraftStage(int duration, float speed, float small, float mediumBase, float mediumRange,
            float doubleBase, float doubleRange, float bombBase, float bombRange, int hp, int burst, float fire, float route)
        {
            Duration = duration; Speed = speed; SmallInterval = small; MediumBase = mediumBase; MediumRange = mediumRange;
            DoubleBase = doubleBase; DoubleRange = doubleRange; BombBase = bombBase; BombRange = bombRange;
            BossHp = hp; BossBurst = burst; BossFireRange = fire; RouteChance = route;
        }
    }
    public static class AircraftRules
    {
        public const double FixedStep = 1.0 / 120.0;
        public const float MaximumFrameTime = .25f;
        public const int StageCount = 7;
        public const int MaximumEnemies = 128, MaximumBullets = 256, MaximumPickups = 8, MaximumExplosions = 32;
        private static readonly AircraftStage[] stages = {
            new AircraftStage(60,250,2.5f,4,10,30,50,50,120,300,2,4.5f,.2f),
            new AircraftStage(60,300,2,3,10,30,50,50,110,400,3,4,.3f),
            new AircraftStage(90,350,2,2,8,30,45,40,100,500,3,3.5f,.3f),
            new AircraftStage(90,400,1.5f,1.5f,5,20,30,40,90,600,4,3,.3f),
            new AircraftStage(90,450,1.2f,1,4,20,30,30,70,700,4,2.5f,.4f),
            new AircraftStage(90,500,1,0,3,10,30,30,60,900,5,2,.5f),
            new AircraftStage(120,600,1,0,2,7,30,20,60,1000,5,1,.5f)
        };
        // Crazy changes the initial ranges; the subsequent time-based shift is unchanged.
        internal static float ClassicInterval(AircraftEnemyKind kind, bool crazy, double elapsed, float unit)
        {
            float f = (float)Math.Max(0, Math.Min(1, elapsed / 600));
            float min = kind == AircraftEnemyKind.Large ? 10 : 0;
            float range = kind == AircraftEnemyKind.Small ? 1.5f : kind == AircraftEnemyKind.Medium ? 5 : 20;
            float shift = kind == AircraftEnemyKind.Small ? .75f : kind == AircraftEnemyKind.Medium ? 3.5f : 16;
            if (crazy) { min *= .5f; range *= .5f; }
            min -= shift * f;
            if (min < 0) { range = Math.Max(.25f, range + min); min = 0; }
            return Math.Max((float)FixedStep, min + unit * range);
        }
        internal static float ClassicSpeed(AircraftEnemyKind kind, bool crazy, double elapsed, float unit)
        {
            float f = (float)Math.Max(0, Math.Min(1, elapsed / 600));
            float range = kind == AircraftEnemyKind.Small ? 500 : kind == AircraftEnemyKind.Medium ? 400 : 200;
            float min = kind == AircraftEnemyKind.Large ? 100 : 250;
            return min * (crazy ? 2 : 1) + range * f + unit * range;
        }
        public static AircraftStage GetStage(int stage)
        {
            if (stage < 1 || stage > StageCount) throw new ArgumentOutOfRangeException("stage");
            return stages[stage - 1];
        }
    }
}
