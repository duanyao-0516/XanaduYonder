using System;
using XanaduYonder.Core.LinkMatch;

namespace XanaduYonder.Runtime.LinkMatch
{
    /// <summary>Owns the catalog-sized set of attempts and atomically records terminal results with their board and clock.</summary>
    public sealed class LinkMatchProfile
    {
        private LinkMatchSaveData data;
        public LinkMatchMode SelectedMode { get { return data.SelectedMode; } set { if (!LinkMatchSessionData.ValidMode(value)) throw new ArgumentOutOfRangeException(nameof(value)); data.SelectedMode = value; } }
        public string SelectedLevelId { get { return data.SelectedLevelId; } set { LinkMatchLevel.Get(value); data.SelectedLevelId = value; } }
        public int CompletedCount { get { int count = 0; foreach (var record in data.Records) if (record.Wins > 0) count++; return count; } }
        public LinkMatchProfile() { data = new LinkMatchSaveData(); }
        public LinkMatchProfile(LinkMatchSaveData saved)
        { if (saved == null) throw new ArgumentNullException(nameof(saved)); if (!saved.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(saved)); data = saved.Copy(); }
        public LinkMatchSessionData GetSlot(LinkMatchMode mode, string levelId) { var slot = data.Slots[LinkMatchSaveData.SlotIndex(mode, levelId)]; return slot == null ? null : slot.Copy(); }
        public LinkMatchSessionData GetLatestActive()
        { LinkMatchSessionData latest = null; foreach (var slot in data.Slots) if (slot != null && slot.Outcome == LinkMatchOutcome.Playing && (latest == null || slot.ActivityOrder > latest.ActivityOrder)) latest = slot; return latest == null ? null : latest.Copy(); }
        public LinkMatchRecord GetRecord(LinkMatchMode mode, string levelId) { return data.Records[LinkMatchSaveData.SlotIndex(mode, levelId)].Copy(); }
        public LinkMatchSaveData Export() { return data.Copy(); }
        public LinkMatchSession Start(LinkMatchMode mode, string levelId, uint seed, bool fresh = false)
        {
            int index = LinkMatchSaveData.SlotIndex(mode, levelId); var prior = data.Slots[index];
            var session = !fresh && prior != null && prior.Outcome == LinkMatchOutcome.Playing ? new LinkMatchSession(prior) : new LinkMatchSession(LinkMatchLevel.Get(levelId), mode, seed);
            // Replacing is authorized only here; Capture rejects callbacks from superseded attempts.
            var next = data.Copy(); next.Slots[index] = session.Export(); data = next; Capture(session); SelectedMode = mode; SelectedLevelId = levelId; return session;
        }
        public LinkMatchSaveData Capture(LinkMatchSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var slot = session.Export(); if (!slot.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(session));
            int index = LinkMatchSaveData.SlotIndex(slot.Mode, slot.LevelId); var prior = data.Slots[index];
            if (prior != null && prior.AttemptId != slot.AttemptId) throw new InvalidOperationException("Attempt was replaced; stale capture rejected.");
            if (prior != null && prior.ResultRecorded)
            {
                // A restored pre-result copy must not regress the authoritative terminal slot or recount its result.
                if (session.IsTerminal && !session.ResultRecorded) session.MarkResultRecorded(false);
                return data.Copy();
            }
            var next = data.Copy(); var record = next.Records[index]; bool newBest = false;
            if (slot.Outcome != LinkMatchOutcome.Playing)
            {
                // A recorded flag is meaningful only alongside this profile's already recorded slot.
                if (slot.ResultRecorded) throw new InvalidOperationException("Recorded attempt has no matching committed result.");
                if (slot.Outcome == LinkMatchOutcome.Won)
                {
                    record.Wins = LinkMatchSession.Increment(record.Wins);
                    newBest = !record.BestElapsedSeconds.HasValue || slot.ElapsedSeconds < record.BestElapsedSeconds.Value;
                    if (newBest) { record.BestElapsedSeconds = slot.ElapsedSeconds; record.BestHintsUsed = slot.HintsUsed; record.BestShufflesUsed = slot.ShufflesUsed; record.BestAttemptId = slot.AttemptId; }
                }
                else record.Losses = LinkMatchSession.Increment(record.Losses);
                slot.ResultRecorded = true;
            }
            if (next.ActivitySequence == long.MaxValue) CompactActivityOrders(next);
            slot.ActivityOrder = ++next.ActivitySequence; next.Slots[index] = slot;
            if (!next.TryValidate(out reason)) throw new InvalidOperationException(reason);
            data = next; session.SetActivityOrder(slot.ActivityOrder); if (slot.ResultRecorded) session.MarkResultRecorded(newBest); return data.Copy();
        }
        private static void CompactActivityOrders(LinkMatchSaveData save)
        {
            var sorted = (LinkMatchSessionData[])save.Slots.Clone(); Array.Sort(sorted, (a, b) => (a == null ? 0 : a.ActivityOrder).CompareTo(b == null ? 0 : b.ActivityOrder));
            save.ActivitySequence = 0; foreach (var slot in sorted) if (slot != null) slot.ActivityOrder = ++save.ActivitySequence;
        }
    }
}
