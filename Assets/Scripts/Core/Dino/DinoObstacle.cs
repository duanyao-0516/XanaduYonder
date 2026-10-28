// Portions derived from Chromium, Copyright 2024 The Chromium Authors.
// Use of this source code is governed by the BSD-style license in docs/chromium-dino-reference/source/LICENSE.
using System;

namespace XanaduYonder.Core.Dino
{
    public enum DinoObstacleKind { SmallCactus, LargeCactus, LowBird, MidBird, HighBird }

    /// <summary>Immutable visible obstacle. Coordinates and Gap use world units; SpeedOffset uses source pixels/tick.</summary>
    public struct DinoObstacle
    {
        public long Id { get; private set; }
        public DinoObstacleKind Kind { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public int GroupSize { get; private set; }
        public long SpawnTick { get; private set; }
        public double Gap { get; private set; }
        public double SpeedOffset { get; private set; }
        internal bool FollowingObstacleCreated { get; private set; }

        internal DinoObstacle(long id, DinoObstacleKind kind, int group, double x, long spawnTick, double gap, double speedOffset)
        {
            Id = id; Kind = kind; GroupSize = group; X = x;
            Width = OriginalWidth(kind) * group * DinoRules.SpriteScale;
            Height = (kind == DinoObstacleKind.SmallCactus ? 35 : kind == DinoObstacleKind.LargeCactus ? 50 : 40) * DinoRules.SpriteScale;
            Y = (kind == DinoObstacleKind.HighBird ? 50 : kind == DinoObstacleKind.MidBird ? 25 : 0) * DinoRules.SpriteScale;
            SpawnTick = spawnTick; Gap = gap; SpeedOffset = speedOffset; FollowingObstacleCreated = false;
        }

        internal DinoObstacle(long id, DinoObstacleKind kind, int group, double x, DinoPlan plan)
            : this(id, kind, group, x, plan.SpawnTick, 0, 0) { }

        internal static int OriginalWidth(DinoObstacleKind kind) { return kind == DinoObstacleKind.SmallCactus ? 17 : kind == DinoObstacleKind.LargeCactus ? 25 : 46; }
        internal DinoObstacle WithFollower() { var changed = this; changed.FollowingObstacleCreated = true; return changed; }
        internal DinoObstacle At(double x) { var moved = this; moved.X = x; return moved; }
    }

    /// <summary>Source type/group/height/gap distribution. It never rejects a sample using a traversal solver.</summary>
    internal sealed class DinoObstacleGenerator
    {
        private uint random;
        private int previousType = -1, repeated;
        private long nextId = 1;
        internal DinoObstacleGenerator(uint seed) { random = seed == 0 ? 0x6D2B79F5u : seed; }

        internal DinoObstacle Create(DinoRules rules, long spawnTick, double speed)
        {
            // Sampling the eligible types directly is equivalent to Chromium's recursive uniform rejection.
            int typeCount = speed >= 8.5 ? 3 : 2;
            int eligible = typeCount - (repeated == 2 ? 1 : 0);
            int chosen = Random(0, eligible - 1);
            if (repeated == 2 && chosen >= previousType) chosen++;
            int group = Random(1, 3);
            if (speed < (chosen == 0 ? 4 : chosen == 1 ? 7 : 999)) group = 1;
            DinoObstacleKind kind = (DinoObstacleKind)chosen;
            double offset = 0;
            if (chosen == 2) {
                kind = Random(0, 1) == 0 ? DinoObstacleKind.LowBird : DinoObstacleKind.HighBird;
                offset = Random(0, 1) == 0 ? -.8 : .8;
            }
            int singleWidth = DinoObstacle.OriginalWidth(kind);
            int minimum = (int)DinoRules.Round(singleWidth * group * speed + (chosen == 2 ? 150 : 120) * .6);
            int maximum = (int)DinoRules.Round(minimum * 1.5);
            double gap = Random(minimum, maximum) * DinoRules.SpriteScale;
            var obstacle = new DinoObstacle(nextId, kind, group, rules.ViewportWidth + singleWidth * DinoRules.SpriteScale, spawnTick, gap, offset);
            if (nextId < long.MaxValue) nextId++;
            repeated = chosen == previousType ? repeated + 1 : 1; previousType = chosen;
            return obstacle;
        }

        private int Random(int minimum, int maximum)
        {
            uint value = random; value ^= value << 13; value ^= value >> 17; value ^= value << 5;
            random = value;
            return minimum + (int)(value % (uint)(maximum - minimum + 1));
        }
    }

    internal enum DinoAction { None, Jump, Duck }
    internal struct DinoPlan
    {
        internal long SpawnTick, EarliestActionTick, LatestActionTick, RecommendedActionTick, RecoveryTick;
        internal DinoAction Action;
    }
}
