using System;
using System.Collections.Generic;
using XanaduYonder.Core.LinkMatch;

namespace XanaduYonder.Runtime.LinkMatch
{
    public enum LinkMatchOutcome { Playing, Won, TimedOut }

    /// <summary>Disk DTO. Only validated snapshots may cross into a live session.</summary>
    public sealed class LinkMatchSessionData
    {
        public string AttemptId;
        public LinkMatchMode Mode;
        public string LevelId;
        public int Columns, Rows;
        public int[] Tiles;
        public double ElapsedSeconds, RemainingSeconds;
        public int HintsUsed, ShufflesUsed;
        public uint RandomState;
        public LinkMatchOutcome Outcome;
        public bool ResultRecorded;
        public long ActivityOrder;
        public LinkMatchSessionData Copy()
        { var copy = (LinkMatchSessionData)MemberwiseClone(); copy.Tiles = Tiles == null ? null : (int[])Tiles.Clone(); return copy; }
        public bool TryValidate(out string reason)
        {
            reason = null;
            if (!ValidAttempt(AttemptId) || !ValidMode(Mode) || !KnownLevel(LevelId)) return Fail("Unknown attempt, mode or level identity.", out reason);
            var level = LinkMatchLevel.Get(LevelId);
            if (Columns != level.Columns || Rows != level.Rows || Tiles == null || Tiles.Length != Columns * Rows) return Fail("Board dimensions do not match its level.", out reason);
            int remaining = 0; var counts = new int[level.Kinds];
            foreach (int tile in Tiles) { if (tile == -1) continue; if (tile < 0 || tile >= counts.Length) return Fail("Unknown fruit ID.", out reason); counts[tile]++; remaining++; }
            foreach (int count in counts) if (count > 4 || count % 2 != 0) return Fail("Fruit counts must be even and at most four.", out reason);
            if (!Finite(ElapsedSeconds) || ElapsedSeconds < 0 || ElapsedSeconds > LinkMatchSession.MaximumElapsedSeconds || !Finite(RemainingSeconds) || HintsUsed < 0 || ShufflesUsed < 0 || RandomState == 0 || ActivityOrder < 0) return Fail("Invalid clock, assistance counter or random state.", out reason);
            if (Mode == LinkMatchMode.Challenge)
            {
                if (HintsUsed > 3 || ShufflesUsed > 2 || ElapsedSeconds > level.ChallengeSeconds || RemainingSeconds != level.ChallengeSeconds - ElapsedSeconds) return Fail("Challenge clock or assistance quota disagrees.", out reason);
            }
            else if (RemainingSeconds != 0 || Outcome == LinkMatchOutcome.TimedOut) return Fail("Relaxed mode cannot time out.", out reason);
            if (Outcome != LinkMatchOutcome.Playing && Outcome != LinkMatchOutcome.Won && Outcome != LinkMatchOutcome.TimedOut) return Fail("Unknown outcome.", out reason);
            if ((Outcome == LinkMatchOutcome.Won) != (remaining == 0)) return Fail("Outcome disagrees with remaining tiles.", out reason);
            if (Outcome == LinkMatchOutcome.TimedOut && (Mode != LinkMatchMode.Challenge || RemainingSeconds != 0)) return Fail("Timeout requires an expired challenge clock.", out reason);
            if (Mode == LinkMatchMode.Challenge && Outcome != LinkMatchOutcome.TimedOut && RemainingSeconds <= 0) return Fail("Live or won challenge must have time remaining.", out reason);
            if (Outcome == LinkMatchOutcome.Playing && ResultRecorded) return Fail("An unfinished attempt cannot be recorded.", out reason);
            return true;
        }
        internal static bool ValidAttempt(string id) { return Guid.TryParseExact(id, "D", out var value) && value != Guid.Empty && value.ToString("D") == id; }
        internal static bool ValidMode(LinkMatchMode mode) { return mode == LinkMatchMode.Relaxed || mode == LinkMatchMode.Challenge; }
        internal static bool KnownLevel(string id) { return id == "level-01" || id == "level-02" || id == "level-03"; }
        internal static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        internal static bool Fail(string message, out string reason) { reason = message; return false; }
    }

    /// <summary>The assistance tuple belongs to BestAttemptId, never to another run.</summary>
    public sealed class LinkMatchRecord
    {
        public LinkMatchMode Mode;
        public string LevelId;
        public int Wins, Losses;
        public double? BestElapsedSeconds;
        public int BestHintsUsed, BestShufflesUsed;
        public string BestAttemptId;
        public LinkMatchRecord Copy() { return (LinkMatchRecord)MemberwiseClone(); }
        internal bool TryValidate(out string reason)
        {
            reason = null;
            if (!LinkMatchSessionData.ValidMode(Mode) || !LinkMatchSessionData.KnownLevel(LevelId) || Wins < 0 || Losses < 0 || (Mode == LinkMatchMode.Relaxed && Losses != 0)) return LinkMatchSessionData.Fail("Invalid record identity or totals.", out reason);
            if (Wins == 0)
            { if (BestElapsedSeconds.HasValue || BestAttemptId != null || BestHintsUsed != 0 || BestShufflesUsed != 0) return LinkMatchSessionData.Fail("An uncompleted level cannot have a best run.", out reason); return true; }
            if (!BestElapsedSeconds.HasValue || !LinkMatchSessionData.Finite(BestElapsedSeconds.Value) || BestElapsedSeconds < 0 || BestElapsedSeconds > LinkMatchSession.MaximumElapsedSeconds || !LinkMatchSessionData.ValidAttempt(BestAttemptId) || BestHintsUsed < 0 || BestShufflesUsed < 0) return LinkMatchSessionData.Fail("Missing or invalid best run.", out reason);
            if (Mode == LinkMatchMode.Challenge && (BestElapsedSeconds >= LinkMatchLevel.Get(LevelId).ChallengeSeconds || BestHintsUsed > 3 || BestShufflesUsed > 2)) return LinkMatchSessionData.Fail("Challenge best run exceeds its limits.", out reason);
            return true;
        }
    }

    public sealed class LinkMatchSaveData
    {
        public const string CurrentContentVersion = "1";
        public string ContentVersion = CurrentContentVersion;
        public LinkMatchMode SelectedMode;
        public string SelectedLevelId = "level-01";
        public long ActivitySequence;
        // Fixed index: mode * 3 + level.Number - 1. Null means never started.
        public LinkMatchSessionData[] Slots = new LinkMatchSessionData[6];
        public LinkMatchRecord[] Records = CreateRecords();
        public static int SlotIndex(LinkMatchMode mode, string levelId)
        { if (!LinkMatchSessionData.ValidMode(mode)) throw new ArgumentOutOfRangeException(nameof(mode)); return (int)mode * 3 + LinkMatchLevel.Get(levelId).Number - 1; }
        private static LinkMatchRecord[] CreateRecords()
        { var result = new LinkMatchRecord[6]; for (int i = 0; i < result.Length; i++) result[i] = new LinkMatchRecord { Mode = (LinkMatchMode)(i / 3), LevelId = LinkMatchLevel.Get(i % 3 + 1).Id }; return result; }
        public LinkMatchSaveData Copy()
        {
            var copy = (LinkMatchSaveData)MemberwiseClone();
            if (Slots != null) { copy.Slots = new LinkMatchSessionData[Slots.Length]; for (int i = 0; i < Slots.Length; i++) copy.Slots[i] = Slots[i] == null ? null : Slots[i].Copy(); }
            if (Records != null) { copy.Records = new LinkMatchRecord[Records.Length]; for (int i = 0; i < Records.Length; i++) copy.Records[i] = Records[i] == null ? null : Records[i].Copy(); }
            return copy;
        }
        public bool TryValidate(out string reason)
        {
            reason = null;
            if (ContentVersion != CurrentContentVersion || !LinkMatchSessionData.ValidMode(SelectedMode) || !LinkMatchSessionData.KnownLevel(SelectedLevelId) || ActivitySequence < 0 || Slots == null || Slots.Length != 6 || Records == null || Records.Length != 6) return LinkMatchSessionData.Fail("Invalid profile schema or selection.", out reason);
            var attempts = new HashSet<string>(StringComparer.Ordinal); var orders = new HashSet<long>(); var bestAttempts = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < 6; i++)
            {
                var record = Records[i];
                if (record == null) return LinkMatchSessionData.Fail("Missing record.", out reason);
                if (!record.TryValidate(out reason)) return false;
                if (SlotIndex(record.Mode, record.LevelId) != i || (record.BestAttemptId != null && !bestAttempts.Add(record.BestAttemptId))) return LinkMatchSessionData.Fail("Duplicate or misfiled record.", out reason);
                var slot = Slots[i]; if (slot == null) continue;
                if (!slot.TryValidate(out reason)) return false;
                if (SlotIndex(slot.Mode, slot.LevelId) != i || !attempts.Add(slot.AttemptId) || slot.ActivityOrder <= 0 || slot.ActivityOrder > ActivitySequence || !orders.Add(slot.ActivityOrder)) return LinkMatchSessionData.Fail("Duplicate or misfiled attempt/activity order.", out reason);
                if (slot.Outcome == LinkMatchOutcome.Playing) { if (slot.AttemptId == record.BestAttemptId) return LinkMatchSessionData.Fail("A best attempt cannot still be playing.", out reason); continue; }
                if (!slot.ResultRecorded) return LinkMatchSessionData.Fail("Terminal slot must be recorded in the same transaction.", out reason);
                if (slot.Outcome == LinkMatchOutcome.TimedOut)
                { if (record.Losses == 0 || slot.AttemptId == record.BestAttemptId) return LinkMatchSessionData.Fail("Timeout has no matching loss record.", out reason); continue; }
                if (record.Wins == 0 || record.BestElapsedSeconds > slot.ElapsedSeconds) return LinkMatchSessionData.Fail("Best record is worse than its completed slot.", out reason);
                if (record.Wins == 1 && record.BestAttemptId != slot.AttemptId) return LinkMatchSessionData.Fail("The only win must be its best attempt.", out reason);
                if (record.BestAttemptId == slot.AttemptId && (record.BestElapsedSeconds != slot.ElapsedSeconds || record.BestHintsUsed != slot.HintsUsed || record.BestShufflesUsed != slot.ShufflesUsed)) return LinkMatchSessionData.Fail("Best tuple disagrees with its winning attempt.", out reason);
            }
            // A best ID belonging to another current slot cannot be a real same-run tuple.
            for (int i = 0; i < 6; i++) for (int j = 0; j < 6; j++) if (i != j && Slots[j] != null && Records[i].BestAttemptId == Slots[j].AttemptId) return LinkMatchSessionData.Fail("A best attempt belongs to a different slot.", out reason);
            return true;
        }
    }
}
