using System;

namespace XanaduYonder.Core.GoldMiner
{
    public static class GoldMinerRules
    {
        public const int Version = 3, LevelCount = 10, TicksPerSecond = 60, LevelTicks = 3600, MaxEntities = 128, MaxBombs = 9;
        // Speeds are world units per second. Heavy loads should consume a meaningful part of
        // the one-minute round; strength and dynamite trade shop cash for that lost time.
        public const double TickSeconds = 1.0 / 60, IdleLength = .2, HookRadius = .09, ExtendSpeed = 7.2, EmptyRetractSpeed = 5, LoadedRetractSpeed = 2.4;
        public const double MaxAngle = 80 * Math.PI / 180, SwingSpeed = Math.PI / 3;
        // Targets remain cumulative wallet balances. Each mode has its own measured curve.
        static readonly long[] targets = { 1200, 3000, 5100, 7600, 10400, 13600, 17200, 21200, 25600, 30400 };
        static readonly long[] coopTargets = { 2400, 6000, 10200, 15200, 20800, 27200, 34400, 42400, 51200, 60800 };
        static readonly long[] legacyTargets =
        {
            650,
            1500,
            2550,
            3900,
            5550,
            7500,
            9750,
            12300,
            15150,
            18300
        };
        static readonly int[, ] counts =
        {
            {
                4,
                3,
                1,
                3,
                1,
                0,
                2,
                0,
                0
            },
            {
                4,
                4,
                2,
                3,
                2,
                1,
                2,
                0,
                0
            },
            {
                4,
                4,
                2,
                3,
                2,
                1,
                2,
                2,
                0
            },
            {
                4,
                4,
                2,
                3,
                2,
                2,
                2,
                2,
                0
            },
            {
                3,
                4,
                3,
                3,
                2,
                2,
                2,
                2,
                1
            },
            {
                3,
                4,
                3,
                3,
                2,
                3,
                2,
                2,
                1
            },
            {
                3,
                4,
                3,
                3,
                2,
                4,
                2,
                2,
                1
            },
            {
                3,
                4,
                3,
                3,
                2,
                4,
                2,
                2,
                2
            },
            {
                2,
                4,
                3,
                3,
                2,
                5,
                2,
                2,
                2
            },
            {
                2,
                4,
                4,
                3,
                2,
                5,
                2,
                2,
                2
            }
        };
        static readonly long[] values =
        {
            100,
            250,
            500,
            20,
            50,
            600,
            100,
            30,
            630
        };
        static readonly double[] weights =
        {
            1,
            2,
            4,
            3,
            5,
            .6,
            1.2,
            .8,
            .8
        }, radii =
        {
            .18,
            .29,
            .42,
            .21,
            .40,
            .16,
            .22,
            .20,
            .22
        };
        public static bool ValidMode(GoldMinerMode mode)
        {
            return mode == GoldMinerMode.Solo || mode == GoldMinerMode.Coop;
        }

        public static int PlayerCount(GoldMinerMode mode)
        {
            if (!ValidMode(mode))
                throw new ArgumentOutOfRangeException("mode");
            return mode == GoldMinerMode.Solo ? 1 : 2;
        }

        public static double Width(GoldMinerMode mode)
        {
            PlayerCount(mode);
            return mode == GoldMinerMode.Solo ? 6 : 12;
        }

        public static double Height(GoldMinerMode mode)
        {
            PlayerCount(mode);
            return mode == GoldMinerMode.Solo ? 7.2 : 5.2;
        }

        public static double SlowestRetractSpeed
        {
            get
            {
                double maximumWeight = .5;
                foreach (double weight in weights)
                    maximumWeight = Math.Max(maximumWeight, weight);
                return Math.Min(EmptyRetractSpeed, LoadedRetractSpeed / maximumWeight);
            }
        }

        // A hook already launched at the deadline may finish one full extension and return.
        // The world diagonal bounds every pivot-to-boundary distance in either mode.
        public static int MaximumDrainTicks(GoldMinerMode mode)
        {
            double width = Width(mode), height = Height(mode);
            double diagonal = Math.Sqrt(width * width + height * height);
            int extensionTicks = (int)Math.Ceiling(diagonal / ExtendSpeed * TicksPerSecond);
            int returnTicks = (int)Math.Ceiling(diagonal / SlowestRetractSpeed * TicksPerSecond);
            return extensionTicks + returnTicks + 2; // Separate rounding and floating-point margin.
        }

        public static int MaximumSimulationTicks(GoldMinerMode mode)
        {
            return LevelTicks + MaximumDrainTicks(mode);
        }

        public static string LayoutProfileId(GoldMinerMode mode)
        {
            PlayerCount(mode);
            return mode == GoldMinerMode.Solo ? "solo-v1" : "coop-v1";
        }

        public static string ModeId(GoldMinerMode mode)
        {
            PlayerCount(mode);
            return mode == GoldMinerMode.Solo ? "solo" : "coop";
        }

        public static long Target(GoldMinerMode mode, int level)
        {
            ValidateLevel(level);
            PlayerCount(mode);
            return (mode == GoldMinerMode.Coop ? coopTargets : targets)[level - 1];
        }

        internal static long LegacyTarget(GoldMinerMode mode, int level)
        {
            ValidateLevel(level);
            PlayerCount(mode);
            return legacyTargets[level - 1] * (mode == GoldMinerMode.Coop ? 8 : 5) / 5;
        }

        internal static bool IsSupportedTarget(GoldMinerMode mode, int level, long target)
        {
            return target == Target(mode, level) || target == LegacyTarget(mode, level);
        }

        public static int Count(GoldMinerMode mode, int level, GoldMinerKind kind)
        {
            ValidateLevel(level);
            ValidateKind(kind);
            PlayerCount(mode);
            int n = counts[level - 1, (int)kind];
            return mode == GoldMinerMode.Coop ? (n * 8 + 4) / 5 : n;
        }

        public static double Radius(GoldMinerKind kind)
        {
            ValidateKind(kind);
            return radii[(int)kind];
        }

        public static double Weight(GoldMinerKind kind)
        {
            ValidateKind(kind);
            return weights[(int)kind];
        }

        public static long BaseValue(GoldMinerKind kind)
        {
            ValidateKind(kind);
            return values[(int)kind];
        }

        public static bool IsAnimal(GoldMinerKind kind)
        {
            return kind == GoldMinerKind.Mole || kind == GoldMinerKind.DiamondMole;
        }

        public static double AnimalSpeed(int level)
        {
            ValidateLevel(level);
            return .65 + .05 * (level - 3);
        }

        public static long Value(GoldMinerEntity e, GoldMinerBuffs b)
        {
            if (e == null || b == null)
                throw new ArgumentNullException();
            long v = e.Kind == GoldMinerKind.MysteryBag ? e.BagValue.Value : BaseValue(e.Kind);
            if (b.RockBook && (e.Kind == GoldMinerKind.RockSmall || e.Kind == GoldMinerKind.RockLarge))
                v *= 3;
            if (b.DiamondOil && (e.Kind == GoldMinerKind.Diamond || e.Kind == GoldMinerKind.DiamondMole))
                v += 300;
            return v;
        }

        public static long Price(GoldMinerItem item, int completedLevel)
        {
            if (completedLevel < 1 || completedLevel >= LevelCount)
                throw new ArgumentOutOfRangeException("completedLevel");
            switch (item)
            {
                case GoldMinerItem.Dynamite:
                    return 60 + 10 * completedLevel;
                case GoldMinerItem.Strength:
                    return 120 + 20 * completedLevel;
                case GoldMinerItem.Clover:
                    return 80 + 10 * completedLevel;
                case GoldMinerItem.RockBook:
                    return 50 + 10 * completedLevel;
                case GoldMinerItem.DiamondOil:
                    return 150 + 20 * completedLevel;
                default:
                    throw new ArgumentOutOfRangeException("item");
            }
        }

        public static long ShopMaximum(int level)
        {
            return Price(GoldMinerItem.Dynamite, level) * 3 + Price(GoldMinerItem.Strength, level) + Price(GoldMinerItem.Clover, level) + Price(GoldMinerItem.RockBook, level) + Price(GoldMinerItem.DiamondOil, level);
        }

        public static long RequiredIncome(GoldMinerMode mode, int level)
        {
            return level == 1 ? Target(mode, 1) : Target(mode, level) - Target(mode, level - 1) + ShopMaximum(level - 1);
        }

        public static double OriginX(GoldMinerMode mode, int playerId)
        {
            if (playerId < 0 || playerId >= PlayerCount(mode))
                throw new ArgumentOutOfRangeException("playerId");
            return playerId == 0 ? 3 : 9;
        }

        public static GoldMinerHook[] InitialHooks(GoldMinerMode mode)
        {
            var a = new GoldMinerHook[PlayerCount(mode)];
            for (int i = 0; i < a.Length; i++)
                a[i] = new GoldMinerHook
                {
                    PlayerId = i,
                    State = GoldMinerHookState.Swinging,
                    AngleRadians = (i == 0 ? -35 : 35) * Math.PI / 180,
                    SwingDirection = i == 0 ? 1 : -1,
                    Length = IdleLength
                };
            return a;
        }

        public static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public static uint NextUInt(ref uint state)
        {
            if (state == 0)
                throw new ArgumentOutOfRangeException("state");
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        public static long DrawBag(ref uint state, bool clover)
        {
            uint n = NextUInt(ref state) % 100;
            return clover ? (n < 20 ? 100 : n < 60 ? 250 : 500) : (n < 60 ? 100 : n < 90 ? 250 : 500);
        }

        static void ValidateLevel(int level)
        {
            if (level < 1 || level > LevelCount)
                throw new ArgumentOutOfRangeException("level");
        }

        static void ValidateKind(GoldMinerKind kind)
        {
            if ((int)kind < 0 || (int)kind >= values.Length)
                throw new ArgumentOutOfRangeException("kind");
        }
    }
}
