using System;
using XanaduYonder.Core.Zuma;

namespace XanaduYonder.Runtime.Zuma
{
    public sealed class ZumaRecord
    {
        public string MapId;
        public string DifficultyId;
        public long BestScore;
        public int BestTimeSeconds;
        public ZumaRecord Copy() { return (ZumaRecord)MemberwiseClone(); }
    }

    public sealed class ZumaProfile
    {
        public string LastMapId = "spiral";
        public string LastDifficultyId = "rabbit";
        public ZumaRecord[] Records;
        public ZumaProfile Copy()
        {
            var copy = (ZumaProfile)MemberwiseClone();
            if (Records != null)
            {
                copy.Records = new ZumaRecord[Records.Length];
                for (var i = 0; i < Records.Length; i++) copy.Records[i] = Records[i] == null ? null : Records[i].Copy();
            }
            return copy;
        }
    }

    public sealed class ZumaSaveData
    {
        public static readonly string[] MapIds =
        {
            "spiral", "claw", "riverbed", "targetglyph", "turnaround", "longrange", "tiltspiral", "underover", "warshak",
            "loopy", "groovefest", "spaceinvaders", "triangle", "coaster", "squaresville", "tunnellevel", "overunder", "inversespiral"
        };
        public static readonly string[] DifficultyIds = { "rabbit", "eagle", "jaguar", "sun_god" };
        public ZumaProfile Profile;
        public ZumaSnapshot Session;

        public static ZumaSaveData CreateNew()
        {
            var data = new ZumaSaveData { Profile = new ZumaProfile { Records = new ZumaRecord[72] } };
            var index = 0;
            foreach (var map in MapIds)
                foreach (var difficulty in DifficultyIds)
                    data.Profile.Records[index++] = new ZumaRecord { MapId = map, DifficultyId = difficulty };
            return data;
        }

        public ZumaRecord GetRecord(string mapId, string difficultyId)
        {
            if (Profile != null && Profile.Records != null)
                foreach (var record in Profile.Records)
                    if (record != null && record.MapId == mapId && record.DifficultyId == difficultyId) return record;
            throw new ArgumentException("Unknown Zuma map/difficulty record.");
        }

        public ZumaSaveData Copy()
        {
            return new ZumaSaveData { Profile = Profile == null ? null : Profile.Copy(), Session = Session == null ? null : Session.Copy() };
        }

        // This is the in-memory transaction. Save the returned state before claiming persistence.
        // Repeating the same finished session is idempotent, including after an I/O retry.
        public bool CommitResult(ZumaSnapshot result)
        {
            if (result == null) throw new ArgumentException("Zuma result is missing.", nameof(result));
            // Validate the exact detached result that will be committed, including the shared
            // MaximumPlayTicks bound, before touching records or narrowing integer seconds.
            var next = result.Copy();
            if (next.Phase != "won" && next.Phase != "lost")
                throw new ArgumentException("Zuma result is not final.", nameof(result));
            if (!next.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(result));
            if (Session != null && Session.SessionId == next.SessionId && Session.ResultCommitted)
            {
                if (Session.Phase != next.Phase || Session.Score != next.Score || Session.PlayTicks != next.PlayTicks ||
                    Session.MapId != next.MapId || Session.DifficultyId != next.DifficultyId)
                    throw new ArgumentException("A committed Zuma result cannot be changed.", nameof(result));
                return false;
            }
            var record = GetRecord(next.MapId, next.DifficultyId);
            if (next.Phase == "won")
            {
                var seconds = (int)(next.PlayTicks / ZumaGame.TickRate);
                record.BestScore = Math.Max(record.BestScore, next.Score);
                if (record.BestTimeSeconds == 0 || seconds < record.BestTimeSeconds) record.BestTimeSeconds = seconds;
            }
            next.ResultCommitted = true;
            next.ResultAcknowledged = false;
            Session = next;
            return true;
        }

        public bool AcknowledgeResult()
        {
            if (Session == null || !Session.ResultCommitted || (Session.Phase != "won" && Session.Phase != "lost"))
                throw new InvalidOperationException("Only a committed Zuma result can be acknowledged.");
            if (Session.ResultAcknowledged) return false;
            Session = Session.Copy();
            Session.ResultAcknowledged = true;
            return true;
        }

        internal bool TryValidate(Func<string, ZumaLevelDefinition> resolver, out string reason)
        {
            reason = null;
            if (Profile == null || Array.IndexOf(MapIds, Profile.LastMapId) < 0 || Array.IndexOf(DifficultyIds, Profile.LastDifficultyId) < 0 ||
                Profile.Records == null || Profile.Records.Length != 72)
            { reason = "Invalid Zuma profile or record count."; return false; }
            var present = new bool[72];
            foreach (var record in Profile.Records)
            {
                if (record == null) { reason = "Null Zuma record."; return false; }
                var map = Array.IndexOf(MapIds, record.MapId);
                var difficulty = Array.IndexOf(DifficultyIds, record.DifficultyId);
                if (map < 0 || difficulty < 0 || record.BestScore < 0 || record.BestTimeSeconds < 0 || present[map * 4 + difficulty])
                { reason = "Invalid or duplicate Zuma record."; return false; }
                present[map * 4 + difficulty] = true;
            }
            if (Session == null) return true;
            if (Array.IndexOf(MapIds, Session.MapId) < 0 || Array.IndexOf(DifficultyIds, Session.DifficultyId) < 0)
            { reason = "Unknown Zuma session map or difficulty."; return false; }
            var level = resolver == null ? null : resolver(Session.MapId);
            if (resolver != null && level == null) { reason = "Zuma level content is unavailable."; return false; }
            if (!Session.TryValidate(level, out reason)) return false;
            if (Session.ResultCommitted && Session.Phase == "won")
            {
                var record = GetRecord(Session.MapId, Session.DifficultyId);
                var seconds = Session.PlayTicks / ZumaGame.TickRate;
                if (seconds > int.MaxValue || record.BestScore < Session.Score || record.BestTimeSeconds > seconds ||
                    (seconds > 0 && record.BestTimeSeconds == 0))
                { reason = "Committed Zuma result disagrees with records."; return false; }
            }
            return true;
        }
    }
}
