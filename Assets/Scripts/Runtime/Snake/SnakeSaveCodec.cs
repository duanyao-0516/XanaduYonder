using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Snake;

namespace XanaduYonder.Runtime.Snake
{
    public static class SnakeSaveCodec
    {
        public const int MaximumLength = 256 * 1024;
        private static readonly string[] RootFields = { "schemaVersion", "gameId", "rulesVersion", "levelPackVersion", "selectedMode", "selectedDifficulty", "selectedBoundary", "selectedLevel", "unlockedLevel", "classicSlots", "campaignSlot", "classicRecords", "campaignRecords" };
        private static readonly string[] SlotFields = { "rulesVersion", "levelPackVersion", "mode", "difficulty", "boundary", "levelId", "width", "height", "targetFood", "initialTickMs", "minimumTickMs", "sessionId", "body", "food", "direction", "state", "foodEaten", "score", "tickCount", "randomState", "elapsedSeconds", "resultRecorded" };
        private static readonly string[] RecordFields = { "difficulty", "boundary", "levelId", "bestScore", "wins", "losses", "bestElapsedSeconds", "bestFullBoardElapsedSeconds" };

        public static string Encode(SnakeProfile data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            ValidateProfile(data);
            var slots = new JArray(); var classicRecords = new JArray(); var campaignRecords = new JArray();
            for (int i = 0; i < 6; i++)
            {
                SnakeRules rules = SnakeRules.Classic(i / 2, (SnakeBoundary)(i % 2));
                slots.Add(data.ClassicSlots[i] == null ? (JToken)JValue.CreateNull() : EncodeSlot(data.ClassicSlots[i], rules));
                classicRecords.Add(EncodeRecord(data.ClassicRecords[i], i / 2, rules.Boundary, null));
            }
            for (int i = 0; i < 12; i++) campaignRecords.Add(EncodeRecord(data.CampaignRecords[i], 0, null, "snake-" + (i + 1).ToString("00")));
            var root = new JObject { ["schemaVersion"] = 1, ["gameId"] = "snake", ["rulesVersion"] = SnakeCatalog.SupportedRulesVersion,
                ["levelPackVersion"] = SnakeCatalog.SupportedPackVersion, ["selectedMode"] = data.SelectedMode, ["selectedDifficulty"] = data.SelectedDifficulty,
                ["selectedBoundary"] = data.SelectedBoundary.ToString(), ["selectedLevel"] = data.SelectedLevel, ["unlockedLevel"] = data.UnlockedLevel,
                ["classicSlots"] = slots, ["campaignSlot"] = data.CampaignData == null ? (JToken)JValue.CreateNull() : EncodeSlot(data.CampaignData, data.RequireCatalog().Get(data.CampaignIndex)),
                ["classicRecords"] = classicRecords, ["campaignRecords"] = campaignRecords };
            string json = root.ToString(Formatting.None);
            if (Encoding.UTF8.GetByteCount(json) > MaximumLength) throw new ArgumentException("Oversized snake save.", nameof(data));
            return json;
        }
        private static JObject EncodeSlot(SnakeSessionData slot, SnakeRules rules)
        {
            SnakeSnapshot board = slot.Board;
            return new JObject { ["rulesVersion"] = SnakeCatalog.SupportedRulesVersion, ["levelPackVersion"] = SnakeCatalog.SupportedPackVersion,
                ["mode"] = rules.Mode.ToString(), ["difficulty"] = rules.Difficulty, ["boundary"] = rules.Boundary.ToString(), ["levelId"] = rules.LevelId,
                ["width"] = rules.Width, ["height"] = rules.Height, ["targetFood"] = rules.TargetFood, ["initialTickMs"] = rules.InitialTickMs, ["minimumTickMs"] = rules.MinimumTickMs,
                ["sessionId"] = slot.SessionId, ["body"] = JArray.FromObject(board.Body), ["food"] = board.Food < 0 ? JValue.CreateNull() : new JValue(board.Food),
                ["direction"] = board.Direction.ToString(), ["state"] = board.State.ToString(), ["foodEaten"] = board.FoodEaten, ["score"] = board.Score,
                ["tickCount"] = board.TickCount, ["randomState"] = board.RandomState, ["elapsedSeconds"] = slot.ElapsedSeconds, ["resultRecorded"] = slot.ResultRecorded };
        }
        private static JObject EncodeRecord(SnakeRecord record, int difficulty, SnakeBoundary? boundary, string levelId)
        {
            return new JObject { ["difficulty"] = difficulty, ["boundary"] = boundary.HasValue ? new JValue(boundary.Value.ToString()) : JValue.CreateNull(),
                ["levelId"] = levelId, ["bestScore"] = record.BestScore, ["wins"] = record.Wins, ["losses"] = record.Losses,
                ["bestElapsedSeconds"] = record.BestElapsedSeconds.HasValue ? new JValue(record.BestElapsedSeconds.Value) : JValue.CreateNull(),
                ["bestFullBoardElapsedSeconds"] = record.BestFullBoardElapsedSeconds.HasValue ? new JValue(record.BestFullBoardElapsedSeconds.Value) : JValue.CreateNull() };
        }
        public static bool TryDecode(string json, SnakeCatalog catalog, out SnakeProfile data, out string reason)
        {
            data = null; reason = null;
            try
            {
                if (catalog == null) throw new ArgumentNullException(nameof(catalog));
                JObject root = SnakeJson.Parse(json); SnakeJson.Fields(root, RootFields);
                if (SnakeJson.Int(root["schemaVersion"]) != 1 || SnakeJson.Text(root["gameId"]) != "snake") throw new FormatException("Unsupported snake save schema.");
                RequireCurrentVersions(root, catalog);
                var candidate = new SnakeProfile(catalog) { SelectedMode = SnakeJson.Int(root["selectedMode"]), SelectedDifficulty = SnakeJson.Int(root["selectedDifficulty"]),
                    SelectedBoundary = Boundary(root["selectedBoundary"]), SelectedLevel = SnakeJson.Int(root["selectedLevel"]), UnlockedLevel = SnakeJson.Int(root["unlockedLevel"]) };
                var slots = SnakeJson.Array(root["classicSlots"], 6); var classicRecords = SnakeJson.Array(root["classicRecords"], 6); var campaignRecords = SnakeJson.Array(root["campaignRecords"], 12);
                for (int i = 0; i < 6; i++)
                {
                    if (slots[i].Type != JTokenType.Null) candidate.ClassicSlots[i] = DecodeSlot(SnakeJson.Object(slots[i]), SnakeRules.Classic(i / 2, (SnakeBoundary)(i % 2)), catalog);
                    candidate.ClassicRecords[i] = DecodeRecord(classicRecords[i], i, true);
                }
                if (root["campaignSlot"].Type != JTokenType.Null)
                {
                    var slot = SnakeJson.Object(root["campaignSlot"]); int index = catalog.FindLevel(SnakeJson.Text(slot["levelId"]));
                    candidate.CampaignData = DecodeSlot(slot, catalog.Get(index), catalog); candidate.CampaignIndex = index;
                }
                for (int i = 0; i < 12; i++) candidate.CampaignRecords[i] = DecodeRecord(campaignRecords[i], i, false);
                ValidateProfile(candidate); candidate.RememberRecordedSlots(); data = candidate; return true;
            }
            catch (Exception exception) when (SnakeJson.IsInvalid(exception)) { reason = exception.Message; return false; }
        }
        private static SnakeSessionData DecodeSlot(JObject value, SnakeRules rules, SnakeCatalog catalog)
        {
            SnakeJson.Fields(value, SlotFields); RequireCurrentVersions(value, catalog);
            bool levelMatches = rules.Mode == SnakeMode.Classic ? value["levelId"].Type == JTokenType.Null : SnakeJson.Text(value["levelId"]) == rules.LevelId;
            if (SnakeJson.Text(value["mode"]) != rules.Mode.ToString() || SnakeJson.Int(value["difficulty"]) != rules.Difficulty || Boundary(value["boundary"]) != rules.Boundary || !levelMatches ||
                SnakeJson.Int(value["width"]) != rules.Width || SnakeJson.Int(value["height"]) != rules.Height || SnakeJson.Int(value["targetFood"]) != rules.TargetFood ||
                SnakeJson.Int(value["initialTickMs"]) != rules.InitialTickMs || SnakeJson.Int(value["minimumTickMs"]) != rules.MinimumTickMs) throw new FormatException("Slot rules do not match their key and installed content.");
            var source = SnakeJson.Array(value["body"]); if (source.Count < 4 || source.Count > rules.Capacity) throw new FormatException("Invalid snake length.");
            int[] body = new int[source.Count]; for (int i = 0; i < body.Length; i++) body[i] = SnakeJson.Int(source[i]);
            var board = new SnakeSnapshot { Body = body, Food = value["food"].Type == JTokenType.Null ? -1 : SnakeJson.Int(value["food"]),
                Direction = Direction(value["direction"]), State = State(value["state"]), FoodEaten = SnakeJson.Int(value["foodEaten"]), Score = SnakeJson.Int(value["score"]),
                TickCount = SnakeJson.Long(value["tickCount"]), RandomState = SnakeJson.UInt(value["randomState"]) };
            var slot = new SnakeSessionData { SessionId = SnakeJson.Text(value["sessionId"]), Board = board, ElapsedSeconds = SnakeJson.Number(value["elapsedSeconds"]), ResultRecorded = SnakeJson.Bool(value["resultRecorded"]) };
            if (!slot.TryValidate(rules, out var reason)) throw new FormatException(reason);
            return slot;
        }
        private static SnakeRecord DecodeRecord(JToken token, int index, bool classic)
        {
            var value = SnakeJson.Object(token); SnakeJson.Fields(value, RecordFields);
            if (classic)
            {
                if (SnakeJson.Int(value["difficulty"]) != index / 2 || Boundary(value["boundary"]) != (SnakeBoundary)(index % 2) || value["levelId"].Type != JTokenType.Null)
                    throw new FormatException("Classic record key mismatch.");
            }
            else if (SnakeJson.Int(value["difficulty"]) != 0 || value["boundary"].Type != JTokenType.Null || SnakeJson.Text(value["levelId"]) != "snake-" + (index + 1).ToString("00"))
                throw new FormatException("Campaign record key mismatch.");
            var record = new SnakeRecord { BestScore = SnakeJson.Int(value["bestScore"]), Wins = SnakeJson.Long(value["wins"]), Losses = SnakeJson.Long(value["losses"]),
                BestElapsedSeconds = SnakeJson.NullableNumber(value["bestElapsedSeconds"]), BestFullBoardElapsedSeconds = SnakeJson.NullableNumber(value["bestFullBoardElapsedSeconds"]) };
            if (!record.Valid(classic)) throw new FormatException("Invalid snake record.");
            return record;
        }
        private static void ValidateProfile(SnakeProfile profile)
        {
            if (profile.UnlockedLevel < 0 || profile.UnlockedLevel > 11) throw new ArgumentException("Invalid unlocked level.");
            var sessions = new HashSet<string>(); int unlocked = 0;
            for (int i = 0; i < 6; i++)
            {
                if (profile.ClassicRecords[i] == null || !profile.ClassicRecords[i].Valid(true)) throw new ArgumentException("Invalid classic record.");
                ValidateSlot(profile.ClassicSlots[i], SnakeRules.Classic(i / 2, (SnakeBoundary)(i % 2)), profile.ClassicRecords[i], sessions);
            }
            for (int i = 0; i < 12; i++)
            {
                if (profile.CampaignRecords[i] == null || !profile.CampaignRecords[i].Valid(false)) throw new ArgumentException("Invalid campaign record.");
                if (profile.CampaignRecords[i].Wins > 0) unlocked = Math.Max(unlocked, Math.Min(11, i + 1));
            }
            if (profile.UnlockedLevel != unlocked) throw new ArgumentException("Unlock state does not agree with completion records.");
            if (profile.CampaignData != null)
            {
                if (profile.CampaignIndex < 0 || profile.CampaignIndex > profile.UnlockedLevel) throw new ArgumentException("Campaign slot refers to a locked level.");
                ValidateSlot(profile.CampaignData, profile.RequireCatalog().Get(profile.CampaignIndex), profile.CampaignRecords[profile.CampaignIndex], sessions);
            }
            else if (profile.CampaignIndex != -1) throw new ArgumentException("Campaign index without a slot.");
        }
        private static void ValidateSlot(SnakeSessionData slot, SnakeRules rules, SnakeRecord record, HashSet<string> sessions)
        {
            if (slot == null) return;
            if (!slot.TryValidate(rules, out var reason)) throw new ArgumentException(reason);
            if (!sessions.Add(slot.SessionId)) throw new ArgumentException("A session is duplicated across rules.");
            if (!slot.ResultRecorded)
            {
                if (slot.Board.State != SnakeState.Playing) throw new ArgumentException("A persisted terminal session must already be accounted for.");
                return;
            }
            bool lost = slot.Board.State == SnakeState.Lost;
            if (record.BestScore < slot.Board.Score || (lost ? record.Losses == 0 : record.Wins == 0) ||
                (!lost && (!record.BestElapsedSeconds.HasValue || record.BestElapsedSeconds.Value > slot.ElapsedSeconds)) ||
                (slot.Board.State == SnakeState.WonFullBoard && (!record.BestFullBoardElapsedSeconds.HasValue || record.BestFullBoardElapsedSeconds.Value > slot.ElapsedSeconds)))
                throw new ArgumentException("Recorded terminal slot disagrees with its record.");
        }
        private static void RequireCurrentVersions(JObject root, SnakeCatalog catalog)
        {
            if (SnakeJson.Text(root["rulesVersion"]) != catalog.RulesVersion || SnakeJson.Text(root["levelPackVersion"]) != catalog.PackVersion)
                throw new FormatException("Snake rules or level pack version is incompatible.");
        }
        internal static bool IsIncompatible(string json, SnakeCatalog catalog)
        {
            try
            {
                JObject root = SnakeJson.Parse(json);
                if (SnakeJson.Text(root["gameId"]) != "snake") return false;
                if (SnakeJson.Int(root["schemaVersion"]) != 1 || SnakeJson.Text(root["rulesVersion"]) != catalog.RulesVersion || SnakeJson.Text(root["levelPackVersion"]) != catalog.PackVersion) return true;
                var slots = root["classicSlots"] as JArray;
                if (slots != null) foreach (var token in slots) if (SlotHasUnknownVersion(token as JObject, catalog)) return true;
                return SlotHasUnknownVersion(root["campaignSlot"] as JObject, catalog);
            }
            catch (Exception exception) when (SnakeJson.IsInvalid(exception)) { return false; }
        }
        private static bool SlotHasUnknownVersion(JObject slot, SnakeCatalog catalog)
        {
            return slot != null && ((slot["rulesVersion"] != null && slot["rulesVersion"].Type == JTokenType.String && (string)slot["rulesVersion"] != catalog.RulesVersion) ||
                (slot["levelPackVersion"] != null && slot["levelPackVersion"].Type == JTokenType.String && (string)slot["levelPackVersion"] != catalog.PackVersion));
        }
        internal static bool TryRecoverRecords(string json, SnakeCatalog catalog, out SnakeProfile profile)
        {
            profile = null;
            try
            {
                JObject root = SnakeJson.Parse(json);
                if (SnakeJson.Int(root["schemaVersion"]) != 1 || SnakeJson.Text(root["gameId"]) != "snake") return false;
                if (!IsIncompatible(json, catalog)) return false;
                // Only the independently validated record section survives an incompatible rules/content version.
                var result = new SnakeProfile(catalog);
                RecoverRecordArray(root["classicRecords"], result.ClassicRecords, true);
                RecoverRecordArray(root["campaignRecords"], result.CampaignRecords, false);
                for (int i = 0; i < 12; i++) if (result.CampaignRecords[i].Wins > 0) result.UnlockedLevel = Math.Max(result.UnlockedLevel, Math.Min(11, i + 1));
                profile = result; return true;
            }
            catch (Exception exception) when (SnakeJson.IsInvalid(exception)) { return false; }
        }
        private static void RecoverRecordArray(JToken token, SnakeRecord[] target, bool classic)
        {
            var array = token as JArray; if (array == null || array.Count != target.Length) return;
            for (int i = 0; i < target.Length; i++)
            { try { target[i] = DecodeRecord(array[i], i, classic); } catch (Exception exception) when (SnakeJson.IsInvalid(exception)) { } }
        }
        private static SnakeBoundary Boundary(JToken token)
        { switch (SnakeJson.Text(token)) { case "Wrap": return SnakeBoundary.Wrap; case "Solid": return SnakeBoundary.Solid; default: throw new FormatException("Unknown snake boundary."); } }
        private static SnakeDirection Direction(JToken token)
        { switch (SnakeJson.Text(token)) { case "Up": return SnakeDirection.Up; case "Right": return SnakeDirection.Right; case "Down": return SnakeDirection.Down; case "Left": return SnakeDirection.Left; default: throw new FormatException("Unknown snake direction."); } }
        private static SnakeState State(JToken token)
        { switch (SnakeJson.Text(token)) { case "Playing": return SnakeState.Playing; case "Lost": return SnakeState.Lost; case "WonTarget": return SnakeState.WonTarget; case "WonFullBoard": return SnakeState.WonFullBoard; default: throw new FormatException("Unknown snake state."); } }
    }

    internal static class SnakeJson
    {
        internal const int MaximumDepth = 16;
        internal static JObject Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > SnakeSaveCodec.MaximumLength || Encoding.UTF8.GetByteCount(json) > SnakeSaveCodec.MaximumLength)
                throw new FormatException("Missing or oversized snake JSON.");
            new StrictJsonSyntax(json).Validate();
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { MaxDepth = MaximumDepth, DateParseHandling = DateParseHandling.None })
            {
                var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new FormatException("Trailing JSON data.");
                return root;
            }
        }
        internal static void Fields(JObject value, params string[] fields)
        {
            if (value.Count != fields.Length) throw new FormatException("Unexpected or missing snake fields.");
            foreach (string name in fields) if (value.Property(name) == null) throw new FormatException("Missing field: " + name);
        }
        internal static JObject Object(JToken token) { var value = token as JObject; if (value == null) throw new FormatException("Expected object."); return value; }
        internal static JArray Array(JToken token, int count = -1)
        { var value = token as JArray; if (value == null || (count >= 0 && value.Count != count)) throw new FormatException("Invalid array."); return value; }
        internal static string Text(JToken token) { if (token == null || token.Type != JTokenType.String) throw new FormatException("Expected string token."); return (string)token; }
        internal static bool Bool(JToken token) { if (token == null || token.Type != JTokenType.Boolean) throw new FormatException("Expected boolean token."); return (bool)token; }
        internal static long Long(JToken token) { if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected integer token."); return checked((long)token); }
        internal static int Int(JToken token) { return checked((int)Long(token)); }
        internal static uint UInt(JToken token) { return checked((uint)Long(token)); }
        internal static double Number(JToken token)
        {
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) throw new FormatException("Expected numeric token.");
            double value = (double)token; if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) throw new FormatException("Expected finite nonnegative number."); return value;
        }
        internal static double? NullableNumber(JToken token) { if (token == null) throw new FormatException("Missing number."); return token.Type == JTokenType.Null ? (double?)null : Number(token); }
        internal static bool IsInvalid(Exception exception)
        { return exception is JsonException || exception is FormatException || exception is OverflowException || exception is ArgumentException; }
        private sealed class StrictJsonSyntax
        {
            private readonly string text;
            private int position;
            internal StrictJsonSyntax(string text) { this.text = text; }
            internal void Validate() { Value(0); Space(); if (position != text.Length) Invalid(); }
            private void Value(int depth)
            {
                Space(); if (position >= text.Length) Invalid(); char c = text[position];
                if (c == '{' || c == '[')
                {
                    if (++depth > MaximumDepth) Invalid(); bool obj = c == '{'; position++; Space(); char close = obj ? '}' : ']';
                    if (Take(close)) return;
                    while (true)
                    {
                        if (obj) { String(); Space(); if (!Take(':')) Invalid(); }
                        Value(depth); Space(); if (Take(close)) return; if (!Take(',')) Invalid(); Space();
                    }
                }
                if (c == '"') { String(); return; }
                if (c == 't') { Literal("true"); return; }
                if (c == 'f') { Literal("false"); return; }
                if (c == 'n') { Literal("null"); return; }
                Number();
            }
            private void String()
            {
                if (!Take('"')) Invalid();
                while (position < text.Length)
                {
                    char c = text[position++]; if (c == '"') return; if (c < 32) Invalid();
                    if (c != '\\') continue;
                    if (position >= text.Length) Invalid(); c = text[position++];
                    if (c == 'u')
                    {
                        for (int i = 0; i < 4; i++)
                        { if (position >= text.Length) Invalid(); c = text[position++]; if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) Invalid(); }
                    }
                    else if (c != '"' && c != '\\' && c != '/' && c != 'b' && c != 'f' && c != 'n' && c != 'r' && c != 't') Invalid();
                }
                Invalid();
            }
            private void Number()
            {
                Take('-');
                if (!Take('0'))
                { if (position >= text.Length || text[position] < '1' || text[position] > '9') Invalid(); Digits(); }
                if (Take('.')) { if (!Digit()) Invalid(); Digits(); }
                if (Take('e') || Take('E')) { if (!Take('+')) Take('-'); if (!Digit()) Invalid(); Digits(); }
            }
            private bool Digit() { return position < text.Length && text[position] >= '0' && text[position] <= '9'; }
            private void Digits() { while (Digit()) position++; }
            private void Literal(string value)
            { foreach (char c in value) if (!Take(c)) Invalid(); }
            private bool Take(char c) { if (position >= text.Length || text[position] != c) return false; position++; return true; }
            private void Space() { while (position < text.Length && (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' || text[position] == '\n')) position++; }
            private static void Invalid() { throw new FormatException("Save file is not strict JSON or exceeds the depth limit."); }
        }
    }
}
