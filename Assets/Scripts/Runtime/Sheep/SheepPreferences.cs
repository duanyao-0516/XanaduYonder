using XanaduYonder.Core.Sheep;

namespace XanaduYonder.Runtime.Sheep
{
    public sealed class SheepPreferences
    {
        public SheepDifficulty DefaultDifficulty = SheepDifficulty.Normal;
        public bool SkipDifficultyPrompt;
        public int RemoveLimit = 2;
        public int UndoLimit = 3;
        public int ShuffleLimit = 1;

        public bool IsStandardTools { get { return RemoveLimit == 2 && UndoLimit == 3 && ShuffleLimit == 1; } }

        public SheepPreferences Copy()
        {
            return new SheepPreferences {
                DefaultDifficulty = DefaultDifficulty, SkipDifficultyPrompt = SkipDifficultyPrompt,
                RemoveLimit = RemoveLimit, UndoLimit = UndoLimit, ShuffleLimit = ShuffleLimit
            };
        }

        public bool TryValidate(out string reason)
        {
            reason = null;
            if (DefaultDifficulty < SheepDifficulty.Easy || DefaultDifficulty > SheepDifficulty.Extreme)
                reason = "Unknown default sheep difficulty.";
            else if (RemoveLimit < 0 || RemoveLimit > 99 || UndoLimit < 0 || UndoLimit > 99 || ShuffleLimit < 0 || ShuffleLimit > 99)
                reason = "Sheep tool preferences must be between 0 and 99.";
            return reason == null;
        }
    }
}
