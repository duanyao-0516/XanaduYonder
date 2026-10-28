using System;

namespace XanaduYonder.Runtime.Dino
{
    // Only records and the next-entry preference survive a session; a running game is never saved.
    public sealed class DinoSaveData
    {
        public long BestPortraitScore { get; set; }
        public long BestLandscapeScore { get; set; }
        public string PreferredOrientation { get; set; } = "portrait";

        public long GetBest(bool landscape)
        {
            return landscape ? BestLandscapeScore : BestPortraitScore;
        }

        public bool Record(bool landscape, long score)
        {
            if (score < 0) throw new ArgumentOutOfRangeException(nameof(score), "A score cannot be negative.");
            if (score <= GetBest(landscape)) return false;
            if (landscape) BestLandscapeScore = score;
            else BestPortraitScore = score;
            return true;
        }
    }
}
