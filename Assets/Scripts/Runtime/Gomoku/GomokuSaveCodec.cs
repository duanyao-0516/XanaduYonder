using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Gomoku;

namespace XanaduYonder.Runtime.Gomoku
{
    public static class GomokuSaveCodec
    {
        public const int MaximumLength = 32 * 1024;
        public const int MaximumDepth = 8;
        private static readonly string[] RootFields = { "schemaVersion", "gameId", "selectedMode", "preferredDifficulty", "preferredHumanStone", "preferredBlackPlayer", "slots" };
        private static readonly string[] SlotFields = { "sessionId", "mode", "difficulty", "humanStone", "blackPlayer", "moves" };

        public static string Encode(GomokuSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var copy = data.Copy();
            if (!copy.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(data));
            var slots = new JArray();
            foreach (var slot in copy.Slots)
                slots.Add(slot == null ? (JToken)JValue.CreateNull() : new JObject {
                    ["sessionId"] = slot.SessionId, ["mode"] = ModeName(slot.Mode), ["difficulty"] = DifficultyName(slot.Difficulty),
                    ["humanStone"] = StoneName(slot.HumanStone), ["blackPlayer"] = slot.BlackPlayer, ["moves"] = JArray.FromObject(slot.Moves)
                });
            string json = new JObject { ["schemaVersion"] = 1, ["gameId"] = "gomoku", ["selectedMode"] = ModeName(copy.SelectedMode),
                ["preferredDifficulty"] = DifficultyName(copy.PreferredDifficulty), ["preferredHumanStone"] = StoneName(copy.PreferredHumanStone),
                ["preferredBlackPlayer"] = copy.PreferredBlackPlayer, ["slots"] = slots }.ToString(Formatting.None);
            if (Encoding.UTF8.GetByteCount(json) > MaximumLength) throw new ArgumentException("Oversized Gomoku profile.", nameof(data));
            return json;
        }

        public static bool TryDecode(string json, out GomokuSaveData data, out string reason)
        {
            data = null; reason = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength)
            { reason = "Missing or oversized Gomoku save file."; return false; }
            try
            {
                // Json.NET accepts JavaScript extensions; disk files must first satisfy JSON syntax.
                new StrictJsonSyntax(json).Validate();
                JObject root;
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { MaxDepth = MaximumDepth, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException("Trailing data after save object.");
                }
                RequireFields(root, RootFields);
                if (Integer(root["schemaVersion"]) != 1 || Text(root["gameId"]) != "gomoku") throw new FormatException("Unsupported Gomoku save schema.");
                var candidate = new GomokuSaveData { SelectedMode = ParseMode(root["selectedMode"]), PreferredDifficulty = ParseDifficulty(root["preferredDifficulty"]),
                    PreferredHumanStone = ParseStone(root["preferredHumanStone"]), PreferredBlackPlayer = Integer(root["preferredBlackPlayer"]) };
                var slots = Array(root["slots"]);
                if (slots.Count != 2) throw new FormatException("Expected exactly two Gomoku mode slots.");
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].Type == JTokenType.Null) continue;
                    var slot = slots[i] as JObject;
                    if (slot == null) throw new FormatException("Expected a Gomoku session object.");
                    RequireFields(slot, SlotFields);
                    var moves = Array(slot["moves"]);
                    if (moves.Count > 225) throw new FormatException("Oversized Gomoku move history.");
                    var history = new int[moves.Count];
                    for (int j = 0; j < moves.Count; j++) history[j] = Integer(moves[j]);
                    candidate.Slots[i] = new GomokuSessionData { SessionId = Text(slot["sessionId"]), Mode = ParseMode(slot["mode"]),
                        Difficulty = ParseDifficulty(slot["difficulty"]), HumanStone = ParseStone(slot["humanStone"]), BlackPlayer = Integer(slot["blackPlayer"]), Moves = history };
                }
                if (!candidate.TryValidate(out reason)) return false;
                data = candidate;
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException || exception is OverflowException || exception is ArgumentException)
            { reason = exception.Message; return false; }
        }

        private static string ModeName(GomokuMode value) { return value == GomokuMode.HumanVsAi ? "human-vs-ai" : "local"; }
        private static string DifficultyName(GomokuDifficulty value) { return value == GomokuDifficulty.Easy ? "easy" : value == GomokuDifficulty.Normal ? "normal" : "hard"; }
        private static string StoneName(GomokuStone value) { return value == GomokuStone.Black ? "black" : "white"; }
        private static GomokuMode ParseMode(JToken token)
        {
            switch (Text(token)) { case "human-vs-ai": return GomokuMode.HumanVsAi; case "local": return GomokuMode.Local; default: throw new FormatException("Unknown Gomoku mode."); }
        }
        private static GomokuDifficulty ParseDifficulty(JToken token)
        {
            switch (Text(token)) { case "easy": return GomokuDifficulty.Easy; case "normal": return GomokuDifficulty.Normal; case "hard": return GomokuDifficulty.Hard; default: throw new FormatException("Unknown Gomoku difficulty."); }
        }
        private static GomokuStone ParseStone(JToken token)
        {
            switch (Text(token)) { case "black": return GomokuStone.Black; case "white": return GomokuStone.White; default: throw new FormatException("Unknown Gomoku stone."); }
        }
        private static void RequireFields(JObject value, string[] fields)
        {
            if (value.Count != fields.Length) throw new FormatException("Unexpected save fields.");
            foreach (string field in fields) if (value.Property(field) == null) throw new FormatException("Missing save field: " + field);
        }
        private static JArray Array(JToken token) { var value = token as JArray; if (value == null) throw new FormatException("Expected an array."); return value; }
        private static int Integer(JToken token) { if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected an integer token."); return checked((int)token); }
        private static string Text(JToken token) { if (token == null || token.Type != JTokenType.String) throw new FormatException("Expected a string token."); return (string)token; }

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
            private void Literal(string value) { foreach (char c in value) if (!Take(c)) Invalid(); }
            private bool Take(char c) { if (position >= text.Length || text[position] != c) return false; position++; return true; }
            private void Space() { while (position < text.Length && (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' || text[position] == '\n')) position++; }
            private static void Invalid() { throw new FormatException("Save file is not strict JSON or exceeds the depth limit."); }
        }
    }
}
