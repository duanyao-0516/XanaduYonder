using System;
using System.Collections.Generic;
using XanaduYonder.Core.Snake;

namespace XanaduYonder.Runtime.Snake
{
    public sealed class SnakeRecord
    {
        public int BestScore;
        public long Wins, Losses;
        public double? BestElapsedSeconds, BestFullBoardElapsedSeconds;
        public SnakeRecord Copy() { return new SnakeRecord { BestScore = BestScore, Wins = Wins, Losses = Losses,
            BestElapsedSeconds = BestElapsedSeconds, BestFullBoardElapsedSeconds = BestFullBoardElapsedSeconds }; }
        internal bool Valid(bool classic)
        {
            if (BestScore < 0 || BestScore > 4760 || BestScore % 10 != 0 || Wins < 0 || Losses < 0 ||
                !ValidTime(BestElapsedSeconds) || !ValidTime(BestFullBoardElapsedSeconds)) return false;
            if (Wins == 0 && Losses == 0 && BestScore != 0) return false;
            if (classic && BestScore == 4760 && Wins == 0) return false;
            if (Wins == 0 && (BestElapsedSeconds.HasValue || BestFullBoardElapsedSeconds.HasValue)) return false;
            if (Wins > 0 && !BestElapsedSeconds.HasValue) return false;
            if (classic && Wins > 0 && (BestScore != 4760 || !BestFullBoardElapsedSeconds.HasValue || BestElapsedSeconds != BestFullBoardElapsedSeconds)) return false;
            if (BestFullBoardElapsedSeconds.HasValue && BestElapsedSeconds > BestFullBoardElapsedSeconds) return false;
            return true;
        }
        private static bool ValidTime(double? time) { return !time.HasValue || (!double.IsNaN(time.Value) && !double.IsInfinity(time.Value) && time.Value >= 0); }
    }

    /// <summary>Owns all result accounting. Call Capture before every persistence checkpoint.</summary>
    public sealed class SnakeProfile
    {
        internal readonly SnakeCatalog Catalog;
        internal readonly SnakeSessionData[] ClassicSlots = new SnakeSessionData[6];
        internal readonly SnakeRecord[] ClassicRecords = new SnakeRecord[6];
        internal readonly SnakeRecord[] CampaignRecords = new SnakeRecord[12];
        internal SnakeSessionData CampaignData;
        internal int CampaignIndex = -1;
        private readonly HashSet<string> recordedSessions = new HashSet<string>();
        private int selectedMode, selectedDifficulty = 1, selectedLevel;
        private SnakeBoundary selectedBoundary = SnakeBoundary.Wrap;
        public SnakeProfile() : this(null) { }
        public SnakeProfile(SnakeCatalog catalog)
        {
            Catalog = catalog;
            for (int i = 0; i < ClassicRecords.Length; i++) ClassicRecords[i] = new SnakeRecord();
            for (int i = 0; i < CampaignRecords.Length; i++) CampaignRecords[i] = new SnakeRecord();
        }
        public int SelectedMode { get { return selectedMode; } set { if (value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value)); selectedMode = value; } }
        public int SelectedDifficulty { get { return selectedDifficulty; } set { Index(value, SnakeBoundary.Wrap); selectedDifficulty = value; } }
        public SnakeBoundary SelectedBoundary { get { return selectedBoundary; } set { Index(0, value); selectedBoundary = value; } }
        public int SelectedLevel { get { return selectedLevel; } set { CheckLevel(value); selectedLevel = value; } }
        public int UnlockedLevel { get; internal set; }
        public int CampaignLevel { get { return CampaignIndex; } }
        public int CompletedCount { get { int count = 0; foreach (var record in CampaignRecords) if (record.Wins > 0) count++; return count; } }
        public SnakeSessionData CampaignSlot { get { return CampaignData == null ? null : CampaignData.Copy(); } }
        public SnakeSessionData GetClassic(int difficulty, SnakeBoundary boundary)
        { var value = ClassicSlots[Index(difficulty, boundary)]; return value == null ? null : value.Copy(); }
        public SnakeRecord GetClassicRecord(int difficulty, SnakeBoundary boundary) { return ClassicRecords[Index(difficulty, boundary)].Copy(); }
        public SnakeRecord GetCampaignRecord(int level) { CheckLevel(level); return CampaignRecords[level].Copy(); }
        public int GetBestScore(int difficulty, SnakeBoundary boundary) { return ClassicRecords[Index(difficulty, boundary)].BestScore; }
        public double? GetBestFullBoardElapsed(int difficulty, SnakeBoundary boundary) { return ClassicRecords[Index(difficulty, boundary)].BestFullBoardElapsedSeconds; }
        public void Capture(SnakeSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            SnakeRules actual = session.Game.Rules;
            int index = actual.Mode == SnakeMode.Classic ? Index(actual.Difficulty, actual.Boundary) : RequireCatalog().FindLevel(actual.LevelId);
            if (actual.Mode == SnakeMode.Campaign && index > UnlockedLevel) throw new ArgumentException("Cannot capture a locked campaign level.", nameof(session));
            SnakeRules expected = actual.Mode == SnakeMode.Classic ? SnakeRules.Classic(actual.Difficulty, actual.Boundary) : Catalog.Get(index);
            if (!SameRules(actual, expected)) throw new ArgumentException("Session rules do not match the installed snake content.", nameof(session));
            SnakeSessionData slot = session.Export();
            if (!slot.TryValidate(expected, out var reason)) throw new ArgumentException(reason, nameof(session));
            bool terminal = slot.Board.State != SnakeState.Playing;
            var previous = actual.Mode == SnakeMode.Classic ? ClassicSlots[index] : CampaignData;
            bool accounted = slot.ResultRecorded || recordedSessions.Contains(slot.SessionId) || (previous != null && previous.SessionId == slot.SessionId && previous.ResultRecorded);
            var record = (actual.Mode == SnakeMode.Classic ? ClassicRecords[index] : CampaignRecords[index]).Copy();
            if (terminal && !accounted)
            {
                record.BestScore = Math.Max(record.BestScore, slot.Board.Score);
                if (slot.Board.State == SnakeState.Lost) record.Losses = Increment(record.Losses);
                else
                {
                    record.Wins = Increment(record.Wins);
                    record.BestElapsedSeconds = Minimum(record.BestElapsedSeconds, slot.ElapsedSeconds);
                    if (slot.Board.State == SnakeState.WonFullBoard) record.BestFullBoardElapsedSeconds = Minimum(record.BestFullBoardElapsedSeconds, slot.ElapsedSeconds);
                }
            }
            if (terminal) slot.ResultRecorded = true;
            if (actual.Mode == SnakeMode.Classic) { ClassicSlots[index] = slot; ClassicRecords[index] = record; }
            else
            {
                CampaignData = slot; CampaignIndex = index; CampaignRecords[index] = record;
                if (record.Wins > 0) UnlockedLevel = Math.Max(UnlockedLevel, Math.Min(11, index + 1));
            }
            if (terminal) { recordedSessions.Add(slot.SessionId); session.MarkResultRecorded(); }
        }
        internal void RememberRecordedSlots()
        {
            foreach (var slot in ClassicSlots) if (slot != null && slot.ResultRecorded) recordedSessions.Add(slot.SessionId);
            if (CampaignData != null && CampaignData.ResultRecorded) recordedSessions.Add(CampaignData.SessionId);
        }
        internal SnakeCatalog RequireCatalog() { if (Catalog == null) throw new InvalidOperationException("Campaign persistence requires a SnakeCatalog."); return Catalog; }
        internal static int Index(int difficulty, SnakeBoundary boundary)
        {
            if (difficulty < 0 || difficulty > 2 || (boundary != SnakeBoundary.Wrap && boundary != SnakeBoundary.Solid)) throw new ArgumentOutOfRangeException("difficulty/boundary");
            return difficulty * 2 + (boundary == SnakeBoundary.Solid ? 1 : 0);
        }
        internal static void CheckLevel(int level) { if (level < 0 || level >= 12) throw new ArgumentOutOfRangeException(nameof(level)); }
        internal static bool SameRules(SnakeRules a, SnakeRules b)
        {
            if (a.Mode != b.Mode || a.Difficulty != b.Difficulty || a.Boundary != b.Boundary || a.LevelId != b.LevelId || a.Width != b.Width || a.Height != b.Height ||
                a.TargetFood != b.TargetFood || a.InitialTickMs != b.InitialTickMs || a.MinimumTickMs != b.MinimumTickMs) return false;
            return SameCells(a.InitialBody, b.InitialBody) && SameCells(a.Obstacles, b.Obstacles);
        }
        private static bool SameCells(int[] a, int[] b) { if (a == null || b == null || a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        private static long Increment(long value) { return value == long.MaxValue ? value : value + 1; }
        private static double Minimum(double? a, double b) { return a.HasValue ? Math.Min(a.Value, b) : b; }
    }
}
