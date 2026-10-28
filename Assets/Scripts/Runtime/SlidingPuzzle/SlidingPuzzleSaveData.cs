using System;
using XanaduYonder.Core.SlidingPuzzle;
namespace XanaduYonder.Runtime.SlidingPuzzle
{
    public sealed class SlidingPuzzleScore
    {
        public int Moves;
        public double Seconds;
        public SlidingPuzzleScore Copy() { return new SlidingPuzzleScore { Moves = Moves, Seconds = Seconds }; }
        internal bool Valid { get { return Moves > 0 && Moves <= SlidingPuzzleGame.MaxActions && !double.IsNaN(Seconds) && !double.IsInfinity(Seconds) && Seconds >= 0 && Seconds <= SlidingPuzzleSessionData.MaximumSeconds; } }
    }
    public sealed class SlidingPuzzleRecord
    {
        public SlidingPuzzleScore BestMoves, BestTime;
        public SlidingPuzzleRecord Copy() { return new SlidingPuzzleRecord { BestMoves = BestMoves == null ? null : BestMoves.Copy(), BestTime = BestTime == null ? null : BestTime.Copy() }; }
        public bool Consider(int moves, double seconds)
        {
            var score = new SlidingPuzzleScore { Moves = moves, Seconds = seconds };
            if (!score.Valid) throw new ArgumentException("Invalid result tuple.");
            bool byMoves = BestMoves == null || CompareMoves(score, BestMoves) < 0;
            bool byTime = BestTime == null || CompareTime(score, BestTime) < 0;
            if (byMoves) BestMoves = score.Copy(); if (byTime) BestTime = score.Copy(); return byMoves || byTime;
        }
        internal bool Valid { get { return BestMoves != null && BestTime != null && BestMoves.Valid && BestTime.Valid && CompareMoves(BestMoves, BestTime) <= 0 && CompareTime(BestTime, BestMoves) <= 0; } }
        internal static int CompareMoves(SlidingPuzzleScore a, SlidingPuzzleScore b) { int first = a.Moves.CompareTo(b.Moves); return first != 0 ? first : a.Seconds.CompareTo(b.Seconds); }
        internal static int CompareTime(SlidingPuzzleScore a, SlidingPuzzleScore b) { int first = a.Seconds.CompareTo(b.Seconds); return first != 0 ? first : a.Moves.CompareTo(b.Moves); }
    }
}namespace XanaduYonder.Runtime.SlidingPuzzle
{
    public sealed class SlidingPuzzleSessionData
    {
        public const double MaximumSeconds = 315576000;
        public int LevelIndex = -1;
        public SlidingPuzzleSnapshot Snapshot;
        public double ElapsedSeconds;
        public bool ResultRecorded;
        public SlidingPuzzleSessionData Copy()
        { return new SlidingPuzzleSessionData { LevelIndex = LevelIndex, Snapshot = Snapshot == null ? null : Snapshot.Copy(), ElapsedSeconds = ElapsedSeconds, ResultRecorded = ResultRecorded }; }
        internal static bool Finite(double number) { return !double.IsNaN(number) && !double.IsInfinity(number); }
        internal SlidingPuzzleGame ValidateAndRestore(int expectedSize, int completedCount)
        {
            if (Snapshot == null || !Finite(ElapsedSeconds) || ElapsedSeconds < 0 || ElapsedSeconds > MaximumSeconds) throw new ArgumentException("Invalid session or elapsed time.");
            var game = SlidingPuzzleGame.Restore(Snapshot);
            if (new SlidingPuzzleGame(game.Size, game.GetInitial()).Won || (game.Moves == 0 && ElapsedSeconds != 0)) throw new ArgumentException("Attempt must begin from an unfinished puzzle and start timing after a move.");
            if (game.Won != ResultRecorded) throw new ArgumentException("Completion accounting disagrees with the board.");
            if (expectedSize != 0)
            {
                if (LevelIndex != -1 || game.Size != expectedSize) throw new ArgumentException("Free slot identity does not match its board.");
            }
            else
            {
                if (LevelIndex < 0 || LevelIndex >= SlidingPuzzleCatalog.Count || LevelIndex > completedCount) throw new ArgumentException("Campaign attempt is unknown or locked.");
                var level = SlidingPuzzleCatalog.Get(LevelIndex);
                if (level.Size != game.Size || !Same(level.InitialCells, game.GetInitial())) throw new ArgumentException("Campaign initial board disagrees with the catalog.");
            }
            return game;
        }
        private static bool Same(int[] a, int[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    }

    /// <summary>Disk boundary DTO. Owners copy it rather than retaining externally mutable arrays.</summary>
    public sealed class SlidingPuzzleSaveData
    {
        public string ContentVersion = SlidingPuzzleCatalog.ContentVersion;
        public int SelectedMode, SelectedSize = 4, SelectedChapter;
        public SlidingPuzzleSessionData[] FreeSessions = new SlidingPuzzleSessionData[3];
        public SlidingPuzzleSessionData CampaignSession;
        public int[] CompletedLevels = new int[0];
        public SlidingPuzzleRecord[] FreeRecords = new SlidingPuzzleRecord[3];
        public SlidingPuzzleRecord[] LevelRecords = new SlidingPuzzleRecord[SlidingPuzzleCatalog.Count];
        public SlidingPuzzleSaveData Copy()
        {
            var copy = new SlidingPuzzleSaveData { ContentVersion = ContentVersion, SelectedMode = SelectedMode, SelectedSize = SelectedSize, SelectedChapter = SelectedChapter, CampaignSession = CampaignSession == null ? null : CampaignSession.Copy(), CompletedLevels = CompletedLevels == null ? null : (int[])CompletedLevels.Clone(), FreeSessions = FreeSessions == null ? null : new SlidingPuzzleSessionData[FreeSessions.Length], FreeRecords = CopyRecords(FreeRecords), LevelRecords = CopyRecords(LevelRecords) };
            if (FreeSessions != null) for (int i = 0; i < FreeSessions.Length; i++) copy.FreeSessions[i] = FreeSessions[i] == null ? null : FreeSessions[i].Copy();
            return copy;
        }
        private static SlidingPuzzleRecord[] CopyRecords(SlidingPuzzleRecord[] records)
        { if (records == null) return null; var copy = new SlidingPuzzleRecord[records.Length]; for (int i = 0; i < records.Length; i++) copy[i] = records[i] == null ? null : records[i].Copy(); return copy; }
        public bool TryValidate(out string reason)
        {
            reason = null;
            try
            {
                if (ContentVersion != SlidingPuzzleCatalog.ContentVersion || SelectedMode < 0 || SelectedMode > 1 || SelectedSize < 3 || SelectedSize > 5) throw new ArgumentException("Unknown content version or selection.");
                if (FreeSessions == null || FreeSessions.Length != 3 || FreeRecords == null || FreeRecords.Length != 3 || LevelRecords == null || LevelRecords.Length != SlidingPuzzleCatalog.Count || CompletedLevels == null || CompletedLevels.Length > SlidingPuzzleCatalog.Count) throw new ArgumentException("Invalid slot or record array length.");
                for (int i = 0; i < CompletedLevels.Length; i++) if (CompletedLevels[i] != i) throw new ArgumentException("Completed levels must be a continuous prefix.");
                if (SelectedChapter < 0 || SelectedChapter >= SlidingPuzzleCatalog.ChapterCount || SelectedChapter * SlidingPuzzleCatalog.LevelsPerChapter > CompletedLevels.Length) throw new ArgumentException("Selected chapter is locked.");
                if (SelectedMode == 0 && FreeSessions[SelectedSize - 3] == null) throw new ArgumentException("Selected free game is missing.");
                for (int i = 0; i < FreeRecords.Length; i++) ValidateRecord(FreeRecords[i]);
                for (int i = 0; i < LevelRecords.Length; i++)
                {
                    ValidateRecord(LevelRecords[i]);
                    if (LevelRecords[i] != null && i >= CompletedLevels.Length) throw new ArgumentException("An unfinished level has a result record.");
                    var optimum = SlidingPuzzleCatalog.Get(i).OptimalMoves;
                    if (LevelRecords[i] != null && optimum.HasValue && (LevelRecords[i].BestMoves.Moves < optimum.Value || LevelRecords[i].BestTime.Moves < optimum.Value)) throw new ArgumentException("Record is below the proven level optimum.");
                }
                for (int i = 0; i < FreeSessions.Length; i++) if (FreeSessions[i] != null)
                { var game = FreeSessions[i].ValidateAndRestore(i + 3, CompletedLevels.Length); ValidateResult(FreeSessions[i], game, FreeRecords[i]); }
                if (CampaignSession != null)
                {
                    var game = CampaignSession.ValidateAndRestore(0, CompletedLevels.Length);
                    if (game.Won && CampaignSession.LevelIndex >= CompletedLevels.Length) throw new ArgumentException("Won campaign attempt has not unlocked its successor.");
                    ValidateResult(CampaignSession, game, LevelRecords[CampaignSession.LevelIndex]);
                }
                return true;
            }
            catch (ArgumentException exception) { reason = exception.Message; return false; }
        }
        private static void ValidateRecord(SlidingPuzzleRecord record)
        { if (record != null && !record.Valid) throw new ArgumentException("Invalid or contradictory best result tuples."); }
        private static void ValidateResult(SlidingPuzzleSessionData session, SlidingPuzzleGame game, SlidingPuzzleRecord record)
        {
            if (!game.Won || game.Assisted) return;
            var score = new SlidingPuzzleScore { Moves = game.Moves, Seconds = session.ElapsedSeconds };
            if (record == null || SlidingPuzzleRecord.CompareMoves(record.BestMoves, score) > 0 || SlidingPuzzleRecord.CompareTime(record.BestTime, score) > 0) throw new ArgumentException("Recorded best omits an unassisted completion.");
        }
    }
}
