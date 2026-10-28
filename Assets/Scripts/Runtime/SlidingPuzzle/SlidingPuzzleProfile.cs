using System;
using XanaduYonder.Core.SlidingPuzzle;

namespace XanaduYonder.Runtime.SlidingPuzzle
{
    /// <summary>Owns three independent free attempts, one campaign attempt, and sequential progress.</summary>
    public sealed class SlidingPuzzleProfile
    {
        private readonly SlidingPuzzleSession[] free = new SlidingPuzzleSession[3];
        private SlidingPuzzleSession campaign;
        private SlidingPuzzleSaveData data;
        public int SelectedMode { get { return data.SelectedMode; } }
        public int SelectedSize { get { return data.SelectedSize; } }
        public int SelectedChapter { get { return data.SelectedChapter; } }
        public int ActiveLevelIndex { get; private set; }
        public int CompletedCount { get { return data.CompletedLevels.Length; } }
        public SlidingPuzzleSession ActiveSession { get { return SelectedMode == 0 ? free[SelectedSize - 3] : campaign; } }
        public SlidingPuzzleProfile()
        { data = new SlidingPuzzleSaveData(); ActiveLevelIndex = -1; StartFree(4); }
        public SlidingPuzzleProfile(SlidingPuzzleSaveData saved)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            if (!saved.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(saved));
            data = saved.Copy(); ActiveLevelIndex = data.CampaignSession == null ? -1 : data.CampaignSession.LevelIndex;
            for (int i = 0; i < free.Length; i++) if (data.FreeSessions[i] != null) { free[i] = new SlidingPuzzleSession(data.FreeSessions[i]); free[i].Leave(); }
            if (data.CampaignSession != null) { campaign = new SlidingPuzzleSession(data.CampaignSession); campaign.Leave(); }
            if (ActiveSession != null) ActiveSession.Enter();
        }
        public SlidingPuzzleSession StartFree(int size, bool newPuzzle = false)
        {
            if (size < 3 || size > 5) throw new ArgumentOutOfRangeException(nameof(size));
            CaptureCurrent(); LeaveCurrent(); data.SelectedMode = 0; data.SelectedSize = size;
            if (free[size - 3] == null || newPuzzle) free[size - 3] = new SlidingPuzzleSession(new SlidingPuzzleGame(size, SlidingPuzzleGenerator.Generate(size, Guid.NewGuid().GetHashCode())));
            ActiveSession.Enter(); return ActiveSession;
        }
        public SlidingPuzzleSession StartLevel(int index, bool restart = false)
        {
            CaptureCurrent();
            if (!IsLevelUnlocked(index)) throw new ArgumentOutOfRangeException(nameof(index), "Level is locked or unknown.");
            LeaveCurrent(); data.SelectedMode = 1; data.SelectedChapter = index / SlidingPuzzleCatalog.LevelsPerChapter;
            if (campaign == null || ActiveLevelIndex != index || restart)
            { var level = SlidingPuzzleCatalog.Get(index); campaign = new SlidingPuzzleSession(new SlidingPuzzleGame(level.Size, level.InitialCells)); ActiveLevelIndex = index; }
            campaign.Enter(); return campaign;
        }
        public void SelectMode(int mode)
        {
            if (mode < 0 || mode > 1) throw new ArgumentOutOfRangeException(nameof(mode));
            CaptureCurrent(); LeaveCurrent(); data.SelectedMode = mode;
            if (mode == 0 && free[SelectedSize - 3] == null) StartFree(SelectedSize);
            else if (ActiveSession != null) ActiveSession.Enter();
        }
        public bool SelectChapter(int index) { if (!IsChapterUnlocked(index)) return false; data.SelectedChapter = index; return true; }
        public bool IsLevelUnlocked(int index) { return index >= 0 && index < SlidingPuzzleCatalog.Count && index <= CompletedCount; }
        public bool IsChapterUnlocked(int index) { return index >= 0 && index < SlidingPuzzleCatalog.ChapterCount && index * SlidingPuzzleCatalog.LevelsPerChapter <= CompletedCount; }
        public bool IsCompleted(int index) { return index >= 0 && index < CompletedCount; }
        public SlidingPuzzleRecord GetFreeRecord(int size) { if (size < 3 || size > 5) throw new ArgumentOutOfRangeException(nameof(size)); return Copy(data.FreeRecords[size - 3]); }
        public SlidingPuzzleRecord GetLevelRecord(int index) { if (index < 0 || index >= SlidingPuzzleCatalog.Count) throw new ArgumentOutOfRangeException(nameof(index)); return Copy(data.LevelRecords[index]); }
        private static SlidingPuzzleRecord Copy(SlidingPuzzleRecord record) { return record == null ? null : record.Copy(); }
        public bool RecordCompleted()
        {
            var session = ActiveSession; if (session == null || !session.Game.Won || session.ResultRecorded) return false;
            bool best = false;
            if (!session.Game.Assisted)
            {
                var records = SelectedMode == 0 ? data.FreeRecords : data.LevelRecords;
                int index = SelectedMode == 0 ? SelectedSize - 3 : ActiveLevelIndex;
                if (records[index] == null) records[index] = new SlidingPuzzleRecord();
                best = records[index].Consider(session.Game.Moves, session.ElapsedSeconds);
            }
            if (SelectedMode == 1 && ActiveLevelIndex == CompletedCount)
            { int count = CompletedCount; var completed = new int[count + 1]; Array.Copy(data.CompletedLevels, completed, count); completed[count] = count; data.CompletedLevels = completed; }
            session.MarkResultRecorded(best); return true;
        }
        public SlidingPuzzleSaveData Capture()
        {
            CaptureCurrent();
            for (int i = 0; i < free.Length; i++) data.FreeSessions[i] = free[i] == null ? null : free[i].Export(-1);
            data.CampaignSession = campaign == null ? null : campaign.Export(ActiveLevelIndex);
            return data.Copy();
        }
        public SlidingPuzzleSaveData Export() { return Capture(); }
        private void CaptureCurrent() { RecordCompleted(); }
        private void LeaveCurrent() { if (ActiveSession != null) ActiveSession.Leave(); }
    }
}
