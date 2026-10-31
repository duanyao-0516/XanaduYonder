using System;

namespace XanaduYonder.Runtime.AircraftFire
{
    public sealed class AircraftFireSaveData
    {
        public long ClassicBestScore { get; set; }
        public long CrazyBestScore { get; set; }
        public long LevelBestScore { get; set; }
        // Zero means no level-mode run has been recorded yet.
        public int HighestStage { get; set; }
        public long BestScore => Math.Max(ClassicBestScore, Math.Max(CrazyBestScore, LevelBestScore));

        public AircraftFireSaveData Copy()
        {
            return new AircraftFireSaveData
            {
                ClassicBestScore = ClassicBestScore,
                CrazyBestScore = CrazyBestScore,
                LevelBestScore = LevelBestScore,
                HighestStage = HighestStage
            };
        }

        public bool Record(bool levelMode, bool crazy, long score, int reachedStage)
        {
            if (score < 0) throw new ArgumentOutOfRangeException(nameof(score));
            if (levelMode && (reachedStage < 1 || reachedStage > 7))
                throw new ArgumentOutOfRangeException(nameof(reachedStage));
            var changed = false;
            if (levelMode)
            {
                changed = score > LevelBestScore || reachedStage > HighestStage;
                LevelBestScore = Math.Max(LevelBestScore, score);
                HighestStage = Math.Max(HighestStage, reachedStage);
            }
            else if (crazy)
            {
                changed = score > CrazyBestScore;
                CrazyBestScore = Math.Max(CrazyBestScore, score);
            }
            else
            {
                changed = score > ClassicBestScore;
                ClassicBestScore = Math.Max(ClassicBestScore, score);
            }
            return changed;
        }

        internal void Merge(AircraftFireSaveData other)
        {
            if (other == null) return;
            ClassicBestScore = Math.Max(ClassicBestScore, other.ClassicBestScore);
            CrazyBestScore = Math.Max(CrazyBestScore, other.CrazyBestScore);
            LevelBestScore = Math.Max(LevelBestScore, other.LevelBestScore);
            HighestStage = Math.Max(HighestStage, other.HighestStage);
        }

        internal bool Contains(AircraftFireSaveData other)
        {
            return other == null || ClassicBestScore >= other.ClassicBestScore &&
                   CrazyBestScore >= other.CrazyBestScore && LevelBestScore >= other.LevelBestScore &&
                   HighestStage >= other.HighestStage;
        }
    }
}
