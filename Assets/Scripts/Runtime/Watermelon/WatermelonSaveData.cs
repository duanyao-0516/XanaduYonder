using XanaduYonder.Core.Watermelon;

namespace XanaduYonder.Runtime.Watermelon
{
    public sealed class WatermelonSaveData
    {
        public int Theme, BestScore;
        public bool GoalAcknowledged;
        public WatermelonSnapshot Game;
        public WatermelonSaveData Copy()
        {
            return new WatermelonSaveData {
                Theme = Theme, BestScore = BestScore, GoalAcknowledged = GoalAcknowledged,
                Game = Game == null ? null : Game.Copy()
            };
        }

        public bool TryValidate(out string reason)
        {
            reason = null;
            if (Game == null) { reason = "Missing watermelon game state."; return false; }
            if (!Game.TryValidate(out reason)) return false;
            if (Theme < 0 || Theme > 2) { reason = "Unknown watermelon theme."; return false; }
            if (BestScore < Game.Score) { reason = "Best score cannot be below the current score."; return false; }
            return true;
        }
    }
}
