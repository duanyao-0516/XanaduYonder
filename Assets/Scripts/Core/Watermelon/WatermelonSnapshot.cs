using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Watermelon
{
    [Serializable]
    public sealed class WatermelonSnapshot
    {
        public WatermelonBallState[] Balls;
        public int Score, CurrentLevel, NextLevel, NextId;
        public uint RandomState;
        public float DropCooldown, DangerTime;
        public bool GameOver;
        public WatermelonSnapshot Copy()
        {
            WatermelonBallState[] copied = null;
            if (Balls != null)
            {
                copied = new WatermelonBallState[Balls.Length];
                for (int i = 0; i < Balls.Length; i++) copied[i] = Balls[i] == null ? null : Balls[i].Copy();
            }
            return new WatermelonSnapshot {
                Balls = copied, Score = Score, CurrentLevel = CurrentLevel, NextLevel = NextLevel,
                RandomState = RandomState, NextId = NextId, DropCooldown = DropCooldown,
                DangerTime = DangerTime, GameOver = GameOver
            };
        }

        public bool TryValidate(out string reason)
        {
            reason = null;
            if (Balls == null || Balls.Length > WatermelonRules.MaxBalls) return Invalid("Invalid ball array or capacity.", out reason);
            if (Score < 0 || NextId < 1 || RandomState == 0) return Invalid("Invalid score, next id or random state.", out reason);
            if (CurrentLevel < 0 || CurrentLevel >= WatermelonRules.StartingLevelCount
                || NextLevel < 0 || NextLevel >= WatermelonRules.StartingLevelCount) return Invalid("Invalid drop queue level.", out reason);
            if (!WatermelonRules.IsFinite(DropCooldown) || DropCooldown < 0 || DropCooldown > WatermelonRules.DropInterval)
                return Invalid("Invalid drop cooldown.", out reason);
            if (!WatermelonRules.IsFinite(DangerTime) || DangerTime < 0 || DangerTime > WatermelonRules.DangerDuration
                || GameOver != (DangerTime >= WatermelonRules.DangerDuration)) return Invalid("Invalid overflow timer or game-over state.", out reason);
            if (Balls.Length == 0 && DangerTime > 0) return Invalid("An empty board cannot overflow.", out reason);

            var ids = new HashSet<int>();
            foreach (var ball in Balls)
            {
                if (ball == null || ball.Id < 1 || ball.Id >= NextId || !ids.Add(ball.Id)) return Invalid("Ball ids must be unique and precede next id.", out reason);
                if (ball.Level < 0 || ball.Level >= WatermelonRules.LevelCount) return Invalid("Invalid ball level.", out reason);
                if (!WatermelonRules.HasValidMotion(ball)) return Invalid("Invalid ball position or motion.", out reason);
                if (!WatermelonRules.IsFinite(ball.Age) || ball.Age < 0 || ball.Age > WatermelonRules.MaximumAge)
                    return Invalid("Invalid ball age.", out reason);
            }
            return true;
        }

        private static bool Invalid(string message, out string reason) { reason = message; return false; }
    }
}
