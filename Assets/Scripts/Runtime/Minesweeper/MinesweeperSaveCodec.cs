using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Minesweeper;

namespace XanaduYonder.Runtime.Minesweeper
{
    public static class MinesweeperSaveCodec
    {
        public const int MaximumLength = 256 * 1024;
        public const int MaximumDepth = 12;
        private static readonly string[] RootFields = { "schemaVersion", "gameId", "selectedDifficulty", "tutorialSeen", "slots", "records" };
        private static readonly string[] RecordFields = { "wins", "losses", "bestMilliseconds" };
        private static readonly string[] SlotFields = { "sessionId", "rulesVersion", "difficulty", "width", "height", "mineCount", "generated", "firstRevealIndex",
            "mines", "revealed", "flags", "state", "detonatedIndex", "resultRecorded", "elapsedMilliseconds", "inputMode", "fullscreen", "zoomScale", "centerColumn", "centerRow" };

        public static string Encode(MinesweeperSaveData data)
        {
            if (data == null) throw new ArgumentException("Missing minesweeper profile.", nameof(data));
            if (!data.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(data));
            var copy = data.Copy(); var slots = new JArray(); var records = new JArray();
            for (int i = 0; i < 3; i++)
            {
                var slot = copy.Slots[i];
                slots.Add(slot == null ? (JToken)JValue.CreateNull() : EncodeSlot(slot));
                var record = copy.Records[i];
                records.Add(new JObject { ["wins"] = Decimal(record.Wins), ["losses"] = Decimal(record.Losses),
                    ["bestMilliseconds"] = record.BestMilliseconds.HasValue ? new JValue(Decimal(record.BestMilliseconds.Value)) : JValue.CreateNull() });
            }
            string json = new JObject { ["schemaVersion"] = 1, ["gameId"] = "minesweeper", ["selectedDifficulty"] = MinesweeperRules.Get(copy.SelectedDifficulty).Id,
                ["tutorialSeen"] = copy.TutorialSeen, ["slots"] = slots, ["records"] = records }.ToString(Formatting.None);
            if (Encoding.UTF8.GetByteCount(json) > MaximumLength) throw new ArgumentException("Oversized minesweeper profile.", nameof(data));
            return json;
        }
        private static JObject EncodeSlot(MinesweeperSessionData slot)
        {
            var board = slot.Board;
            return new JObject {
                ["sessionId"] = slot.SessionId, ["rulesVersion"] = 1, ["difficulty"] = MinesweeperRules.Get(board.Difficulty).Id,
                ["width"] = board.Width, ["height"] = board.Height, ["mineCount"] = board.MineCount, ["generated"] = board.Generated,
                ["firstRevealIndex"] = board.FirstRevealIndex.HasValue ? new JValue(board.FirstRevealIndex.Value) : JValue.CreateNull(),
                ["mines"] = JArray.FromObject(board.Mines), ["revealed"] = JArray.FromObject(board.Revealed), ["flags"] = JArray.FromObject(board.Flags),
                ["state"] = board.State.ToString(), ["detonatedIndex"] = board.DetonatedIndex.HasValue ? new JValue(board.DetonatedIndex.Value) : JValue.CreateNull(),
                ["resultRecorded"] = slot.ResultRecorded, ["elapsedMilliseconds"] = Decimal(slot.ElapsedMilliseconds),
                ["inputMode"] = slot.InputMode == MinesweeperInputMode.Reveal ? "reveal" : "flag", ["fullscreen"] = slot.Fullscreen,
                ["zoomScale"] = slot.ZoomScale, ["centerColumn"] = slot.CenterColumn, ["centerRow"] = slot.CenterRow
            };
        }
        public static bool TryDecode(string json, out MinesweeperSaveData data, out string reason)
        {
            data = null; reason = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength)
            { reason = "Missing or oversized minesweeper save file."; return false; }
            try
            {
                // Json.NET deliberately accepts JavaScript extensions; disk saves require standard JSON.
                new StrictJsonSyntax(json).Validate();
                JObject root;
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { MaxDepth = MaximumDepth, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException("Trailing data after save object.");
                }
                RequireFields(root, RootFields);
                if (Integer(root["schemaVersion"]) != 1 || Text(root["gameId"]) != "minesweeper") throw new FormatException("Unsupported minesweeper save schema.");
                var candidate = new MinesweeperSaveData { SelectedDifficulty = MinesweeperRules.ParseDifficulty(Text(root["selectedDifficulty"])), TutorialSeen = Boolean(root["tutorialSeen"]) };
                var slots = Array(root["slots"], 3); var records = Array(root["records"], 3);
                for (int i = 0; i < 3; i++)
                {
                    if (slots[i].Type != JTokenType.Null) candidate.Slots[i] = DecodeSlot(Object(slots[i]));
                    var record = Object(records[i]); RequireFields(record, RecordFields);
                    candidate.Records[i] = new MinesweeperRecord { Wins = DecimalInteger(record["wins"]), Losses = DecimalInteger(record["losses"]),
                        BestMilliseconds = record["bestMilliseconds"].Type == JTokenType.Null ? (long?)null : DecimalInteger(record["bestMilliseconds"]) };
                }
                if (!candidate.TryValidate(out reason)) return false;
                data = candidate; return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException || exception is OverflowException || exception is ArgumentException)
            { reason = exception.Message; return false; }
        }
        private static MinesweeperSessionData DecodeSlot(JObject slot)
        {
            RequireFields(slot, SlotFields);
            int difficulty = MinesweeperRules.ParseDifficulty(Text(slot["difficulty"])); var spec = MinesweeperRules.Get(difficulty);
            if (Integer(slot["rulesVersion"]) != 1 || Integer(slot["width"]) != spec.Width || Integer(slot["height"]) != spec.Height || Integer(slot["mineCount"]) != spec.MineCount)
                throw new FormatException("Unsupported rules version or mismatched board dimensions.");
            MinesweeperState state;
            switch (Text(slot["state"]))
            {
                case "Ready": state = MinesweeperState.Ready; break;
                case "Active": state = MinesweeperState.Active; break;
                case "Won": state = MinesweeperState.Won; break;
                case "Lost": state = MinesweeperState.Lost; break;
                default: throw new FormatException("Unknown minesweeper state.");
            }
            string mode = Text(slot["inputMode"]);
            if (mode != "reveal" && mode != "flag") throw new FormatException("Unknown minesweeper input mode.");
            int length = spec.Width * spec.Height;
            return new MinesweeperSessionData {
                SessionId = Text(slot["sessionId"]), Board = new MinesweeperSnapshot(difficulty, Boolean(slot["generated"]), NullableInteger(slot["firstRevealIndex"]),
                    state, NullableInteger(slot["detonatedIndex"]), Booleans(slot["mines"], length), Booleans(slot["revealed"], length), Booleans(slot["flags"], length)),
                ResultRecorded = Boolean(slot["resultRecorded"]), ElapsedMilliseconds = DecimalInteger(slot["elapsedMilliseconds"]),
                InputMode = mode == "reveal" ? MinesweeperInputMode.Reveal : MinesweeperInputMode.Flag, Fullscreen = Boolean(slot["fullscreen"]),
                ZoomScale = Number(slot["zoomScale"]), CenterColumn = Number(slot["centerColumn"]), CenterRow = Number(slot["centerRow"])
            };
        }
        private static void RequireFields(JObject value, string[] fields)
        {
            if (value.Count != fields.Length) throw new FormatException("Unexpected save fields.");
            foreach (string field in fields) if (value.Property(field) == null) throw new FormatException("Missing save field: " + field);
        }
        private static JObject Object(JToken token)
        { var value = token as JObject; if (value == null) throw new FormatException("Expected an object."); return value; }
        private static JArray Array(JToken token, int length)
        { var value = token as JArray; if (value == null || value.Count != length) throw new FormatException("Invalid array length."); return value; }
        private static bool[] Booleans(JToken token, int length)
        { var array = Array(token, length); var values = new bool[length]; for (int i = 0; i < length; i++) values[i] = Boolean(array[i]); return values; }
        private static int Integer(JToken token)
        { if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected an integer token."); return checked((int)token); }
        private static int? NullableInteger(JToken token) { return token.Type == JTokenType.Null ? (int?)null : Integer(token); }
        private static string Text(JToken token)
        { if (token == null || token.Type != JTokenType.String) throw new FormatException("Expected a string token."); return (string)token; }
        private static bool Boolean(JToken token)
        { if (token == null || token.Type != JTokenType.Boolean) throw new FormatException("Expected a boolean token."); return (bool)token; }
        private static double Number(JToken token)
        {
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) throw new FormatException("Expected a finite numeric token.");
            double value = (double)token;
            if (!MinesweeperSessionData.Finite(value)) throw new FormatException("Expected a finite number.");
            return value;
        }
        private static string Decimal(long value) { return value.ToString(CultureInfo.InvariantCulture); }
        private static long DecimalInteger(JToken token)
        {
            string text = Text(token);
            if (text.Length == 0) throw new FormatException("Empty decimal string.");
            foreach (char c in text) if (c < '0' || c > '9') throw new FormatException("Expected unsigned decimal digits.");
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) throw new FormatException("Decimal value exceeds Int64.");
            return value;
        }

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
