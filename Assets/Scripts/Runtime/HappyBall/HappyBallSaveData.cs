using XanaduYonder.Core.HappyBall;

namespace XanaduYonder.Runtime.HappyBall
{
    public sealed class HappyBallSaveData
    {
        public int SchemaVersion = 1;
        public string GameId = "happy-ball";
        public HappyBallSnapshot Game;
        public long BestScore;
        public bool HasStarted;
        public bool ResultRecorded;

        public HappyBallSaveData Copy()
        {
            return new HappyBallSaveData {
                SchemaVersion = SchemaVersion, GameId = GameId, Game = Game == null ? null : Game.Copy(),
                BestScore = BestScore, HasStarted = HasStarted, ResultRecorded = ResultRecorded
            };
        }

        public bool TryValidate(out string reason)
        {
            reason = null;
            if (SchemaVersion != 1 || GameId != "happy-ball") reason = "Unsupported happy-ball save schema.";
            else if (Game == null) reason = "Missing happy-ball game state.";
            if (reason != null) return false;
            if (!Game.TryValidate(out reason)) return false;
            if (BestScore < 0 || BestScore < Game.Score) reason = "Best score cannot be below the current score.";
            else if (ResultRecorded && !Game.GameOver) reason = "A live game cannot have a recorded result.";
            else if (!HasStarted && (Game.GameOver || ResultRecorded || Game.Score != 0 || Game.PassedLayers != 0 ||
                Game.NaturalStreak != 0 || Game.Power != HappyBallPower.Normal)) reason = "An unstarted game cannot have progress or a result.";
            return reason == null;
        }
    }
}
