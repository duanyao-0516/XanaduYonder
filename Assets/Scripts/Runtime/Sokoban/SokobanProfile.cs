using System;
using System.Collections.Generic;
using XanaduYonder.Core.Sokoban;

namespace XanaduYonder.Runtime.Sokoban
{
    /// <summary>One active attempt and distinct completed levels; a captured win commits both actual best tuples.</summary>
    public sealed class SokobanProfile
    {
        private readonly SokobanCatalog catalog;
        private SokobanSaveData data;
        public int CompletedCount { get { return data.CompletedLevelIds.Length; } }
        public bool TutorialSeen { get { return data.TutorialSeen; } set { data.TutorialSeen = value; } }
        public string SelectedLevelId { get { return data.SelectedLevelId; } }
        public SokobanProfile(SokobanCatalog catalog)
        { this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); data = new SokobanSaveData { ContentVersion = catalog.ContentVersion, SelectedLevelId = catalog.At(0).LevelId }; }
        public SokobanProfile(SokobanCatalog catalog, SokobanSaveData saved)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            if (!saved.TryValidate(catalog, out var reason)) throw new ArgumentException(reason, nameof(saved)); data = saved.Copy();
        }
        public bool IsCompleted(string id) { return Array.IndexOf(data.CompletedLevelIds, id) >= 0; }
        public SokobanRecord GetRecord(string id) { foreach (var record in data.Records) if (record.LevelId == id) return record.Copy(); return null; }
        public SokobanSessionData GetActive() { return data.ActiveAttempt == null ? null : data.ActiveAttempt.Copy(); }
        public SokobanSaveData Export() { return data.Copy(); }
        public SokobanSession StartLevel(string id) { var session = new SokobanSession(catalog.Get(id)); Capture(session); return session; }
        public SokobanSaveData Capture(SokobanSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!ReferenceEquals(catalog.Get(session.Game.Level.LevelId), session.Game.Level)) throw new ArgumentException("Session terrain does not belong to this catalog.", nameof(session));
            var next = data.Copy(); var slot = session.Export(); bool newBest = false;
            if (slot.Won)
            {
                var records = new List<SokobanRecord>(next.Records); var record = records.Find(x => x.LevelId == slot.LevelId);
                if (record == null) { record = new SokobanRecord { LevelId = slot.LevelId }; records.Add(record); }
                newBest = record.Consider(slot.Moves, slot.Pushes);
                if (Array.IndexOf(next.CompletedLevelIds, slot.LevelId) < 0) { var completed = new List<string>(next.CompletedLevelIds); completed.Add(slot.LevelId); next.CompletedLevelIds = completed.ToArray(); }
                next.Records = records.ToArray(); slot.ResultRecorded = true;
            }
            next.ActiveAttempt = slot; next.SelectedLevelId = slot.LevelId;
            if (!next.TryValidate(catalog, out var reason, false)) throw new InvalidOperationException(reason);
            data = next; if (slot.Won && !session.ResultRecorded) session.MarkResultRecorded(newBest); return data.Copy();
        }
    }
}
