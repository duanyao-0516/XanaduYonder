using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XanaduYonder.Runtime.FlappyBird
{
    public static class FlappySaveCodec
    {
        public const int MaximumLength = 4096;
        private static readonly string[] Fields = { "schemaVersion", "gameId", "bestScore" };

        public static string Encode(FlappySaveData data)
        {
            if (data == null || data.BestScore < 0)
                throw new ArgumentException("Flappy Bird record must be nonnegative.", nameof(data));
            return new JObject
            {
                ["schemaVersion"] = 1,
                ["gameId"] = "flappy-bird",
                ["bestScore"] = data.BestScore.ToString(CultureInfo.InvariantCulture)
            }.ToString(Formatting.None);
        }

        public static bool TryDecode(string json, out FlappySaveData data, out string reason)
        {
            data = null;
            reason = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength)
            {
                reason = "Missing or oversized Flappy Bird save file.";
                return false;
            }
            try
            {
                JObject root;
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { MaxDepth = 4, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException("Trailing data after Flappy Bird save object.");
                }
                if (root.Count != Fields.Length) throw new FormatException("Unexpected Flappy Bird save fields.");
                foreach (var field in Fields)
                    if (root.Property(field) == null) throw new FormatException("Missing Flappy Bird save field: " + field);
                if (root["schemaVersion"].Type != JTokenType.Integer || checked((int)root["schemaVersion"]) != 1 || Text(root["gameId"]) != "flappy-bird")
                    throw new FormatException("Unsupported Flappy Bird save schema.");
                var scoreText = Text(root["bestScore"]);
                // Int64 parsing accepts trailing NUL; disk records permit ASCII decimal digits only.
                foreach (var character in scoreText)
                    if (character < '0' || character > '9') throw new FormatException("Expected decimal score digits only.");
                if (!long.TryParse(scoreText, NumberStyles.None, CultureInfo.InvariantCulture, out var score) || score < 0)
                    throw new FormatException("Expected a nonnegative 64-bit decimal score string.");
                data = new FlappySaveData { BestScore = score };
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException ||
                                              exception is OverflowException || exception is ArgumentException)
            {
                reason = exception.Message;
                return false;
            }
        }

        private static string Text(JToken token)
        {
            if (token == null || token.Type != JTokenType.String) throw new FormatException("Expected a string field.");
            return (string)token;
        }
    }
}
