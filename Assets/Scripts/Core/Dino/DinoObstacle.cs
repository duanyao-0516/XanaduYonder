namespace XanaduYonder.Core.Dino
{
    public enum DinoObstacleKind { SmallCactus, LargeCactus, LowBird, MidBird }

    /// <summary>Immutable geometry and the generation-time traversal certificate. Y is the sprite bottom.</summary>
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
        public long FullVisibleTick { get; private set; }
        public long EarliestActionTick { get; private set; }
        public long LatestActionTick { get; private set; }
        public long RecommendedActionTick { get; private set; }
        public long RecoveryTick { get; private set; }
        public double ReactionWindowSeconds { get { return (LatestActionTick - FullVisibleTick) * DinoRules.StepSeconds - DinoRules.InputBudgetSeconds; } }

        internal DinoObstacle(long id, DinoObstacleKind kind, int group, double x, DinoPlan plan)
        {
            Id = id; Kind = kind; GroupSize = group; X = x;
            Width = (kind == DinoObstacleKind.SmallCactus ? 17 * group : kind == DinoObstacleKind.LargeCactus ? 25 * group : 46) * DinoRules.SpriteScale;
            Height = (kind == DinoObstacleKind.SmallCactus ? 35 : kind == DinoObstacleKind.LargeCactus ? 50 : 40) * DinoRules.SpriteScale;
            Y = kind == DinoObstacleKind.MidBird ? 28 * DinoRules.SpriteScale : 0;
            SpawnTick = plan.SpawnTick; FullVisibleTick = plan.FullVisibleTick;
            EarliestActionTick = plan.EarliestActionTick; LatestActionTick = plan.LatestActionTick;
            RecommendedActionTick = plan.RecommendedActionTick; RecoveryTick = plan.RecoveryTick;
        }

        internal DinoObstacle At(double x) { var moved = this; moved.X = x; return moved; }
    }

    internal struct DinoPlan
    {
        internal long SpawnTick, FullVisibleTick, EarliestActionTick, LatestActionTick, RecommendedActionTick, RecoveryTick;
        internal double ReactionWindowSeconds { get { return (LatestActionTick - FullVisibleTick) * DinoRules.StepSeconds - DinoRules.InputBudgetSeconds; } }
    }
}
