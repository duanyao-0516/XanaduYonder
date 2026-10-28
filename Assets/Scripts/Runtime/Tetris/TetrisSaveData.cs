using XanaduYonder.Core.Tetris;

namespace XanaduYonder.Runtime.Tetris
{
    public sealed class TetrisSaveData
    {
        public int SchemaVersion = 1;
        public string GameId = "tetris";
        // Null is the intentional preview state: browsing never creates a run.
        public TetrisSnapshot Game;
        public long BestScore;
        public bool ResultRecorded;

        public TetrisSaveData Copy()
        {
            return new TetrisSaveData {
                SchemaVersion = SchemaVersion, GameId = GameId,
                Game = Game == null ? null : Game.Copy(), BestScore = BestScore,
                ResultRecorded = ResultRecorded
            };
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (SchemaVersion != 1 || GameId != "tetris") error = "Unsupported Tetris save identity or schema.";
            else if (BestScore < 0) error = "Best score must be nonnegative.";
            else if (Game != null && !Game.TryValidate(out error)) return false;
            else if (Game != null && BestScore < Game.Score) error = "Best score is below the current score.";
            else if (ResultRecorded && (Game == null || !Game.GameOver)) error = "Only a terminal game can have a recorded result.";
            return error == null;
        }
    }
}
