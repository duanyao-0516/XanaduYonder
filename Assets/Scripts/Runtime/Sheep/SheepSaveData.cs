using System;
using XanaduYonder.Core.Sheep;

namespace XanaduYonder.Runtime.Sheep
{
    public sealed class SheepSaveData
    {
        public SheepSnapshot Challenge;
        public SheepSnapshot Endless;
        public SheepSnapshot Tutorial;
        public SheepMode ActiveMode = SheepMode.Challenge;
        public SheepPreferences Preferences = new SheepPreferences();
        public long CompletedRuns;
        public long EndlessBest;
        public bool WinRecorded;

        // A null slot represents a mode that has not been started yet.
        public SheepSnapshot Game
        {
            get
            {
                switch (ActiveMode)
                {
                    case SheepMode.Challenge: return Challenge;
                    case SheepMode.Endless: return Endless;
                    case SheepMode.Tutorial: return Tutorial;
                    default: throw new ArgumentOutOfRangeException(nameof(ActiveMode));
                }
            }
            set
            {
                switch (ActiveMode)
                {
                    case SheepMode.Challenge: Challenge = value; break;
                    case SheepMode.Endless: Endless = value; break;
                    case SheepMode.Tutorial: Tutorial = value; break;
                    default: throw new ArgumentOutOfRangeException(nameof(ActiveMode));
                }
            }
        }
    }
}
