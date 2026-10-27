using System;
using XanaduYonder.Core.Sheep;

namespace XanaduYonder.Runtime.Sheep
{
    // Owns run selection and accounting; presentation never edits a frozen run's rules.
    public sealed class SheepSession
    {
        public SheepSaveData Data { get; private set; }
        public SheepGame Game { get; private set; }
        public bool IsCustomRun => Game != null && !Standard(Game.GetSnapshot());

        public SheepSession(SheepSaveData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            if (data.Game != null) Game = new SheepGame(data.Game);
        }

        public void SetPreferences(SheepPreferences preferences)
        {
            if (preferences == null || !preferences.TryValidate(out _)) throw new ArgumentException("Invalid sheep preferences.");
            Data.Preferences = preferences.Copy();
        }

        public bool Resume(SheepMode mode)
        {
            if (mode < SheepMode.Challenge || mode > SheepMode.Tutorial) return false;
            Capture();
            var saved = mode == SheepMode.Challenge ? Data.Challenge : mode == SheepMode.Endless ? Data.Endless : Data.Tutorial;
            if (saved == null) return false;
            Data.ActiveMode = mode;
            Game = new SheepGame(saved);
            return true;
        }

        public void StartChallenge(SheepDifficulty difficulty, bool remember, uint seed)
        {
            var preferences = Data.Preferences.Copy();
            preferences.DefaultDifficulty = difficulty;
            preferences.SkipDifficultyPrompt = remember;
            SetPreferences(preferences);
            Start(SheepMode.Challenge, difficulty, seed);
        }

        public void Start(SheepMode mode, SheepDifficulty difficulty, uint seed)
        {
            Capture();
            var previous = mode == SheepMode.Challenge ? Data.Challenge : mode == SheepMode.Endless ? Data.Endless : Data.Tutorial;
            var p = Data.Preferences;
            var next = SheepLevelGenerator.Generate(mode, difficulty, seed, p.RemoveLimit, p.UndoLimit, p.ShuffleLimit,
                previous == null ? -1 : previous.LayoutId).Snapshot;
            Data.ActiveMode = mode;
            Game = new SheepGame(next);
            if (mode == SheepMode.Challenge) Data.WinRecorded = false;
            Capture();
        }

        public bool AdvanceWave()
        {
            if (Game == null || !Game.AdvanceWave()) return false;
            Capture();
            return true;
        }

        public void Capture()
        {
            if (Game == null) return;
            var snapshot = Game.GetSnapshot();
            Data.Game = snapshot;
            if (snapshot.Mode == SheepMode.Challenge && Game.Phase == SheepPhase.Won && !Data.WinRecorded)
            {
                if (Data.CompletedRuns < long.MaxValue) Data.CompletedRuns++;
                Data.WinRecorded = true;
            }
            if (snapshot.Mode == SheepMode.Endless && Standard(snapshot)) Data.EndlessBest = Math.Max(Data.EndlessBest, snapshot.Score);
        }

        private static bool Standard(SheepSnapshot snapshot) => snapshot.RemoveLimit == 2 && snapshot.UndoLimit == 3 && snapshot.ShuffleLimit == 1;
    }
}
