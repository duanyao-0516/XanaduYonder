using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XanaduYonder.Runtime.LinkMatch
{
    /// <summary>Strict, bounded, versioned Newtonsoft boundary. No DTO coercion or silent default fields.</summary>
    public static class LinkMatchSaveCodec
    {
        public const int MaximumLength = 128 * 1024;
        public const int MaximumDepth = 8;
        public static string Encode(LinkMatchSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!data.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(data));
            var slots = new JArray(); foreach (var slot in data.Slots) slots.Add(slot == null ? (JToken)JValue.CreateNull() : WriteSession(slot));
            var records = new JArray(); foreach (var r in data.Records) records.Add(new JObject { ["mode"] = r.Mode.ToString(), ["levelId"] = r.LevelId, ["wins"] = r.Wins, ["losses"] = r.Losses, ["bestElapsedSeconds"] = r.BestElapsedSeconds.HasValue ? new JValue(r.BestElapsedSeconds.Value) : JValue.CreateNull(), ["bestHintsUsed"] = r.BestHintsUsed, ["bestShufflesUsed"] = r.BestShufflesUsed, ["bestAttemptId"] = r.BestAttemptId == null ? JValue.CreateNull() : new JValue(r.BestAttemptId) });
            string json = new JObject { ["schemaVersion"] = 2, ["gameId"] = "link-match", ["contentVersion"] = data.ContentVersion, ["selectedMode"] = data.SelectedMode.ToString(), ["selectedLevelId"] = data.SelectedLevelId, ["activitySequence"] = data.ActivitySequence, ["slots"] = slots, ["records"] = records }.ToString(Formatting.None);
            if (Encoding.UTF8.GetByteCount(json) > MaximumLength) throw new ArgumentException("Link Match save exceeds 128 KiB.", nameof(data));
            return json;
        }
        private static JObject WriteSession(LinkMatchSessionData s)
        { return new JObject { ["attemptId"] = s.AttemptId, ["mode"] = s.Mode.ToString(), ["levelId"] = s.LevelId, ["columns"] = s.Columns, ["rows"] = s.Rows, ["tiles"] = new JArray(s.Tiles), ["elapsedSeconds"] = s.ElapsedSeconds, ["remainingSeconds"] = s.RemainingSeconds, ["hintsUsed"] = s.HintsUsed, ["shufflesUsed"] = s.ShufflesUsed, ["randomState"] = s.RandomState, ["outcome"] = s.Outcome.ToString(), ["resultRecorded"] = s.ResultRecorded, ["activityOrder"] = s.ActivityOrder }; }
        public static bool TryDecode(string json, out LinkMatchSaveData data, out string reason)
        { return TryDecodeVersion(json, false, out data, out reason); }
        /// <summary>Only the original six-slot schema is accepted; output is a fully validated current profile.</summary>
        public static bool TryDecodeLegacyV1(string json, out LinkMatchSaveData data, out string reason)
        { return TryDecodeVersion(json, true, out data, out reason); }
        private static bool TryDecodeVersion(string json, bool legacy, out LinkMatchSaveData data, out string reason)
        {
            data = null; reason = null;
            try
            {
                var root = Parse(json); Fields(root, "schemaVersion", "gameId", "contentVersion", "selectedMode", "selectedLevelId", "activitySequence", "slots", "records");
                if (Int(root["schemaVersion"]) != (legacy ? 1 : 2) || Text(root["gameId"]) != "link-match" || Text(root["contentVersion"]) != (legacy ? "1" : LinkMatchSaveData.CurrentContentVersion)) throw new FormatException("Unknown schema, content version or game identity.");
                var decoded = new LinkMatchSaveData { SelectedMode = Mode(root["selectedMode"]), SelectedLevelId = Text(root["selectedLevelId"]), ActivitySequence = Long(root["activitySequence"]) };
                if (legacy && !LegacyLevelId(decoded.SelectedLevelId)) throw new FormatException("Unknown legacy selection.");
                var slots = Array(root["slots"]); var records = Array(root["records"]);
                int expectedCount = legacy ? 6 : LinkMatchSaveData.SlotCount;
                if (slots.Count != expectedCount || records.Count != expectedCount) throw new FormatException("Unexpected slot or record count.");
                for (int i = 0; i < expectedCount; i++)
                {
                    var slot = slots[i].Type == JTokenType.Null ? null : ReadSession(slots[i], legacy ? 48 : Core.LinkMatch.LinkMatchLevel.MaximumTileCount);
                    var record = ReadRecord(records[i]); int destination = i;
                    if (legacy)
                    {
                        // Old challenge indices 3..5 must become 5..7. Validate the old identity
                        // before mapping; accepting a raw copy would silently attach progress to another level.
                        var expectedMode = (Core.LinkMatch.LinkMatchMode)(i / 3);
                        string expectedId = Core.LinkMatch.LinkMatchLevel.Get(i % 3 + 1).Id;
                        if (record.Mode != expectedMode || record.LevelId != expectedId || (slot != null && (slot.Mode != expectedMode || slot.LevelId != expectedId))) throw new FormatException("Legacy slot identity does not match its original index.");
                        destination = LinkMatchSaveData.SlotIndex(record.Mode, record.LevelId);
                    }
                    decoded.Slots[destination] = slot; decoded.Records[destination] = record;
                }
                if (!decoded.TryValidate(out reason)) return false;
                data = decoded; return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException || exception is ArgumentException || exception is OverflowException || exception is InvalidCastException)
            { reason = exception.Message; data = null; return false; }
        }
        private static bool LegacyLevelId(string id) { return id == "level-01" || id == "level-02" || id == "level-03"; }
        private static LinkMatchSessionData ReadSession(JToken token, int maximumTiles)
        {
            var o = Object(token); Fields(o, "attemptId", "mode", "levelId", "columns", "rows", "tiles", "elapsedSeconds", "remainingSeconds", "hintsUsed", "shufflesUsed", "randomState", "outcome", "resultRecorded", "activityOrder");
            var tiles = Array(o["tiles"]); if (tiles.Count > maximumTiles) throw new FormatException("Too many tiles."); var values = new int[tiles.Count]; for (int i = 0; i < tiles.Count; i++) values[i] = Int(tiles[i]);
            long seed = Long(o["randomState"]); if (seed < 0 || seed > uint.MaxValue) throw new FormatException("Invalid random state.");
            return new LinkMatchSessionData { AttemptId = Text(o["attemptId"]), Mode = Mode(o["mode"]), LevelId = Text(o["levelId"]), Columns = Int(o["columns"]), Rows = Int(o["rows"]), Tiles = values, ElapsedSeconds = Number(o["elapsedSeconds"]), RemainingSeconds = Number(o["remainingSeconds"]), HintsUsed = Int(o["hintsUsed"]), ShufflesUsed = Int(o["shufflesUsed"]), RandomState = (uint)seed, Outcome = Outcome(o["outcome"]), ResultRecorded = Boolean(o["resultRecorded"]), ActivityOrder = Long(o["activityOrder"]) };
        }
        private static LinkMatchRecord ReadRecord(JToken token)
        {
            var o = Object(token); Fields(o, "mode", "levelId", "wins", "losses", "bestElapsedSeconds", "bestHintsUsed", "bestShufflesUsed", "bestAttemptId");
            return new LinkMatchRecord { Mode = Mode(o["mode"]), LevelId = Text(o["levelId"]), Wins = Int(o["wins"]), Losses = Int(o["losses"]), BestElapsedSeconds = o["bestElapsedSeconds"].Type == JTokenType.Null ? (double?)null : Number(o["bestElapsedSeconds"]), BestHintsUsed = Int(o["bestHintsUsed"]), BestShufflesUsed = Int(o["bestShufflesUsed"]), BestAttemptId = o["bestAttemptId"].Type == JTokenType.Null ? null : Text(o["bestAttemptId"]) };
        }
        private static Core.LinkMatch.LinkMatchMode Mode(JToken token)
        { string value = Text(token); if (value == "Relaxed") return Core.LinkMatch.LinkMatchMode.Relaxed; if (value == "Challenge") return Core.LinkMatch.LinkMatchMode.Challenge; throw new FormatException("Unknown mode."); }
        private static LinkMatchOutcome Outcome(JToken token)
        { string value = Text(token); if (value == "Playing") return LinkMatchOutcome.Playing; if (value == "Won") return LinkMatchOutcome.Won; if (value == "TimedOut") return LinkMatchOutcome.TimedOut; throw new FormatException("Unknown outcome."); }
        private static JObject Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength) throw new FormatException("Missing or oversized JSON.");
            new StrictSyntax(json).Validate();
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { MaxDepth = MaximumDepth, DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double })
            {
                var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new FormatException("Trailing JSON content."); return root;
            }
        }
        private static JObject Object(JToken token) { if (token == null || token.Type != JTokenType.Object) throw new FormatException("Expected object."); return (JObject)token; }
        private static JArray Array(JToken token) { if (token == null || token.Type != JTokenType.Array) throw new FormatException("Expected array."); return (JArray)token; }
        private static void Fields(JObject obj, params string[] names) { if (obj.Count != names.Length || names.Any(name => obj.Property(name) == null)) throw new FormatException("Missing or unknown field."); }
        private static string Text(JToken token) { if (token == null || token.Type != JTokenType.String) throw new FormatException("Expected text."); return (string)token; }
        private static long Long(JToken token) { if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected integer."); return checked((long)token); }
        private static int Int(JToken token) { return checked((int)Long(token)); }
        private static bool Boolean(JToken token) { if (token == null || token.Type != JTokenType.Boolean) throw new FormatException("Expected boolean."); return (bool)token; }
        private static double Number(JToken token) { if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) throw new FormatException("Expected number."); double value = (double)token; if (!LinkMatchSessionData.Finite(value)) throw new FormatException("Expected finite number."); return value; }
        // JsonTextReader intentionally accepts JavaScript extensions. This grammar excludes comments,
        // single quotes, NaN, trailing commas, leading zeros and unquoted names before token parsing.
        private sealed class StrictSyntax
        {
            private readonly string text; private int position;
            internal StrictSyntax(string text) { this.text = text; }
            internal void Validate() { Value(0); Space(); if (position != text.Length) Invalid(); }
            private void Value(int depth)
            {
                Space(); if (position >= text.Length) Invalid(); char c = text[position];
                if (c == '{' || c == '[')
                {
                    if (++depth > MaximumDepth) Invalid(); bool obj = c == '{'; position++; Space(); char close = obj ? '}' : ']';
                    if (Take(close)) return;
                    while (true) { if (obj) { String(); Space(); if (!Take(':')) Invalid(); } Value(depth); Space(); if (Take(close)) return; if (!Take(',')) Invalid(); Space(); }
                }
                if (c == '"') { String(); return; } if (c == 't') { Literal("true"); return; } if (c == 'f') { Literal("false"); return; } if (c == 'n') { Literal("null"); return; } Number();
            }
            private void String()
            {
                if (!Take('"')) Invalid();
                while (position < text.Length)
                {
                    char c = text[position++]; if (c == '"') return; if (c < 32) Invalid(); if (c != '\\') continue;
                    if (position >= text.Length) Invalid(); c = text[position++];
                    if (c == 'u') { for (int i = 0; i < 4; i++) { if (position >= text.Length) Invalid(); c = text[position++]; if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) Invalid(); } }
                    else if (c != '"' && c != '\\' && c != '/' && c != 'b' && c != 'f' && c != 'n' && c != 'r' && c != 't') Invalid();
                }
                Invalid();
            }
            private void Number() { Take('-'); if (!Take('0')) { if (position >= text.Length || text[position] < '1' || text[position] > '9') Invalid(); Digits(); } if (Take('.')) { if (!Digit()) Invalid(); Digits(); } if (Take('e') || Take('E')) { if (!Take('+')) Take('-'); if (!Digit()) Invalid(); Digits(); } }
            private bool Digit() { return position < text.Length && text[position] >= '0' && text[position] <= '9'; }
            private void Digits() { while (Digit()) position++; }
            private void Literal(string value) { foreach (char c in value) if (!Take(c)) Invalid(); }
            private bool Take(char c) { if (position >= text.Length || text[position] != c) return false; position++; return true; }
            private void Space() { while (position < text.Length && (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' || text[position] == '\n')) position++; }
            private static void Invalid() { throw new FormatException("Save is not strict JSON or exceeds its depth limit."); }
        }
    }
}
