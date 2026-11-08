using System;

namespace XanaduYonder.Core.GoldMiner
{
    // Values are source pixels / 100. Source metadata is deliberately per entity:
    // the original maps contain differing sprite scales, weights and bag rewards.
    [Serializable]
    public sealed class GoldMinerSourceEntity
    {
        public string Key, Reward;
        public int Type, MotionTick;
        public long Value, Award;
        public int AwardPlayer;
        public double Weight, Radius, ScaleX, ScaleY, InitialX, InitialY, MotionEndX;
        public bool Flip;
        public GoldMinerSourceEntity Copy()
        {
            return (GoldMinerSourceEntity)MemberwiseClone();
        }
    }
    [Serializable]
    public sealed class GoldMinerSourceHook
    {
        public double X, Y;
        public int SwingTick, RewardTicks, BombTicks, ResetTicks;
        public int TravelDirection = 1;
        public int? PendingEntityId;
        public GoldMinerSourceHook Copy()
        {
            return (GoldMinerSourceHook)MemberwiseClone();
        }
    }
    [Serializable]
    public sealed class GoldMinerSourceState
    {
        public int MapIndex, SharedBombs;
        public double Strength;
        public GoldMinerSourceState Copy()
        {
            return (GoldMinerSourceState)MemberwiseClone();
        }
    }
    public static class GoldMinerSourceRules
    {
        public const int MaximumSoloLevel = 100000;
        public static bool IsSource(GoldMinerBalanceProfile profile)
        {
            return profile == GoldMinerBalanceProfile.Source;
        }
        public static int MaximumLevel(GoldMinerMode mode)
        {
            return mode == GoldMinerMode.Solo ? MaximumSoloLevel : 30;
        }
        public static bool Completed(GoldMinerMode mode, int level)
        {
            return mode == GoldMinerMode.Coop && level == 30;
        }
        public static long Target(GoldMinerMode mode, int level)
        {
            if (level < 1 || level > MaximumLevel(mode))
                throw new ArgumentOutOfRangeException("level");
            return mode == GoldMinerMode.Solo ? checked(5L * (27L * level * level + 28L * level + 75)) : 500 + 150L * level;
        }
        public static int DurationTicks(GoldMinerMode mode)
        {
            return mode == GoldMinerMode.Solo ? 3720 : 2400;
        }
        public static double Width(GoldMinerMode mode)
        {
            return mode == GoldMinerMode.Solo ? 6.4 : 5.5;
        }
        public static double Height(GoldMinerMode mode)
        {
            return mode == GoldMinerMode.Solo ? 4.34 : 3.45;
        }
        public static double OriginX(GoldMinerMode mode, int player)
        {
            return mode == GoldMinerMode.Solo ? 3.2 : player == 0 ? 1.85 : 2.85;
        }
        public static void HookPoint(GoldMinerMode mode, GoldMinerHook hook, out double x, out double y)
        {
            double tip = mode == GoldMinerMode.Solo ? .4 : .25;
            x = hook.Source.X + Math.Sin(hook.AngleRadians) * tip;
            y = hook.Source.Y + Math.Cos(hook.AngleRadians) * tip + (mode == GoldMinerMode.Coop ? .05 : 0);
        }
        public static GoldMinerHook[] Hooks(GoldMinerMode mode)
        {
            var result = new GoldMinerHook[GoldMinerRules.PlayerCount(mode)];
            for (int i = 0; i < result.Length; i++)
                result[i] = new GoldMinerHook
                {
                    PlayerId = i,
                    State = GoldMinerHookState.Swinging,
                    SwingDirection = i == 0 ? 1 : -1,
                    AngleRadians = mode == GoldMinerMode.Solo ? Math.PI / 3 : 0,
                    Length = mode == GoldMinerMode.Solo ? .4 : .3,
                    Source = new GoldMinerSourceHook { X = OriginX(mode, i) }
                };
            return result;
        }
        public static int Random(ref uint seed, int maximum)
        {
            return (int)(GoldMinerRules.NextUInt(ref seed) % (uint)maximum);
        }
        public static void DrawBag(GoldMinerSourceEntity entity, ref uint seed, bool clover, int bombs)
        {
            int choice = Random(ref seed, 6) + 1;
            entity.Reward = "money";
            if (clover)
            {
                if (choice <= 2)
                    entity.Reward = "power";
                else if (choice <= 4)
                {
                    if (bombs <= 3)
                        entity.Reward = "dynamite";
                    else
                        entity.Value = Random(ref seed, 300) + 300;
                }
                else
                    entity.Value = 700;
            }
            else
            {
                if (choice <= 3)
                    entity.Value = Random(ref seed, 600) + 1;
                else if (choice == 4)
                    entity.Reward = "power";
                else if (choice == 5)
                {
                    if (bombs <= 3)
                        entity.Reward = "dynamite";
                    else
                        entity.Value = Random(ref seed, 100) + 100;
                }
                else
                    entity.Value = 800;
            }
            int weight = Random(ref seed, 4) + 1;
            entity.Weight = weight == 1 ? Random(ref seed, 9) + 1 : weight == 2 ? -(Random(ref seed, 5) + 1) : weight == 3 ? 9 : -1;
        }
        public static long Value(GoldMinerEntity e, GoldMinerBuffs buffs, GoldMinerMode mode)
        {
            long result = e.Source.Value;
            if (mode == GoldMinerMode.Solo)
            {
                if (buffs.RockBook && (e.Source.Type == 8 || e.Source.Type == 14))
                    result *= 3;
                if (buffs.DiamondOil && (e.Source.Type == 10 || e.Source.Type == 11))
                    result += 300;
            }
            return result;
        }
        public static int MapGroup(int level)
        {
            return level <= 10 ? level - 1 : (level - 6) % 10;
        }
        public static double RetractPerTick(GoldMinerMode mode, GoldMinerState state, GoldMinerEntity entity)
        {
            if (mode == GoldMinerMode.Solo)
                return (10 + state.Source.Strength - (entity == null ? 0 : entity.Source.Weight)) * .01;
            return .04 * (entity == null ? 1 : Math.Min(1 - Math.Max(entity.Source.Weight - state.Source.Strength, 20) / 100, 1));
        }
    }
}
