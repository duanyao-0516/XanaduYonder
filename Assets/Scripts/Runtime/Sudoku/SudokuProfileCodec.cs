using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XanaduYonder.Runtime.Sudoku
{
    public static class SudokuProfileCodec
    {
        public const int MaximumLength = 64 * 1024 * 1024;
        private static readonly string[] RootFields = { "schemaVersion", "gameId", "selectedDifficulty", "pages", "completedIds", "unassistedIds", "slots" };

        public static string Encode(SudokuProfile profile)
        {
            if (profile == null) throw new ArgumentException("Missing sudoku profile.", nameof(profile));
            if (!profile.TryValidate(out var error)) throw new ArgumentException(error, nameof(profile));
            var saved = profile.Copy();
            var slots = new JArray();
            foreach (var slot in saved.Slots)
                slots.Add(slot == null ? (JToken)JValue.CreateNull() : JObject.Parse(SudokuSaveCodec.Encode(slot)));
            var root = new JObject
            {
                ["schemaVersion"] = 2, ["gameId"] = "sudoku",
                ["selectedDifficulty"] = saved.SelectedDifficulty, ["pages"] = new JArray(saved.Pages),
                ["completedIds"] = new JArray(saved.CompletedIds), ["unassistedIds"] = new JArray(saved.UnassistedIds),
                ["slots"] = slots
            };
            var json = root.ToString(Formatting.None);
            if (json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength)
                throw new ArgumentException("Sudoku profile exceeds the size limit; no session history was discarded.", nameof(profile));
            return json;
        }

        public static bool TryDecode(string json, out SudokuProfile profile, out string error)
        {
            profile = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength)
            {
                error = "Missing or oversized sudoku profile file.";
                return false;
            }
            try
            {
                JObject root;
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { MaxDepth = 10, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException("Trailing data after sudoku profile object.");
                }
                if (root.Count != RootFields.Length) throw new FormatException("Unexpected sudoku profile fields.");
                foreach (var field in RootFields)
                    if (root.Property(field) == null) throw new FormatException("Missing sudoku profile field: " + field);
                if (Integer(root["schemaVersion"]) != 2 || Text(root["gameId"]) != "sudoku")
                    throw new FormatException("Unsupported sudoku profile schema.");
                var pages = Array(root["pages"], 4, true);
                var slotTokens = Array(root["slots"], 4, true);
                var decoded = new SudokuProfile
                {
                    SelectedDifficulty = Integer(root["selectedDifficulty"]),
                    CompletedIds = TextArray(root["completedIds"]), UnassistedIds = TextArray(root["unassistedIds"])
                };
                for (var difficulty = 0; difficulty < 4; difficulty++)
                {
                    decoded.Pages[difficulty] = Integer(pages[difficulty]);
                    var slot = slotTokens[difficulty];
                    if (slot.Type == JTokenType.Null) continue;
                    if (!(slot is JObject)) throw new FormatException("Expected a sudoku session object or null slot.");
                    if (!SudokuSaveCodec.TryDecode(slot.ToString(Formatting.None), out var session, out error)) return false;
                    decoded.Slots[difficulty] = session;
                }
                if (!decoded.TryValidate(out error)) return false;
                profile = decoded;
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException || exception is OverflowException || exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        private static int Integer(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected an integer profile field.");
            return checked((int)token);
        }

        private static string Text(JToken token)
        {
            if (token == null || token.Type != JTokenType.String) throw new FormatException("Expected a string profile field.");
            return (string)token;
        }

        private static JArray Array(JToken token, int count, bool exact)
        {
            var array = token as JArray;
            if (array == null || array.Count > count || (exact && array.Count != count))
                throw new FormatException("Invalid sudoku profile array length.");
            return array;
        }

        private static string[] TextArray(JToken token)
        {
            var array = Array(token, 2000, false);
            var result = new string[array.Count];
            for (var index = 0; index < result.Length; index++) result[index] = Text(array[index]);
            return result;
        }
    }
}
