using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XanaduYonder.Runtime.Dino
{
    public static class DinoSaveCodec
    {
        public const int MaximumLength = 4096;
        private static readonly string[] Fields =
        {
            "schemaVersion", "gameId", "bestPortraitScore", "bestLandscapeScore", "preferredOrientation"
        };

        public static string Encode(DinoSaveData data)
        {
            if (data == null) throw new ArgumentException("Missing dino save data.", nameof(data));
            var portrait = data.BestPortraitScore;
            var landscape = data.BestLandscapeScore;
            var orientation = data.PreferredOrientation;
            if (portrait < 0 || landscape < 0 || !IsOrientation(orientation))
                throw new ArgumentException("Invalid dino record or orientation preference.", nameof(data));
            return new JObject
            {
                ["schemaVersion"] = 1,
                ["gameId"] = "dino",
                ["bestPortraitScore"] = portrait.ToString(CultureInfo.InvariantCulture),
                ["bestLandscapeScore"] = landscape.ToString(CultureInfo.InvariantCulture),
                ["preferredOrientation"] = orientation
            }.ToString(Formatting.None);
        }

        public static bool TryDecode(string json, out DinoSaveData data, out string reason)
        {
            data = null;
            reason = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength)
            {
                reason = "Missing or oversized dino save file.";
                return false;
            }
            try
            {
                JObject root;
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { MaxDepth = 4, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException("Trailing data after dino save object.");
                }
                if (root.Count != Fields.Length) throw new FormatException("Unexpected dino save fields.");
                foreach (var field in Fields)
                    if (root.Property(field) == null) throw new FormatException("Missing dino save field: " + field);
                if (root["schemaVersion"].Type != JTokenType.Integer || checked((int)root["schemaVersion"]) != 1 || Text(root["gameId"]) != "dino")
                    throw new FormatException("Unsupported dino save schema.");
                var portrait = Score(root["bestPortraitScore"]);
                var landscape = Score(root["bestLandscapeScore"]);
                var orientation = Text(root["preferredOrientation"]);
                if (!IsOrientation(orientation)) throw new FormatException("Unknown dino orientation preference.");
                // Construct only after every disk field has passed validation.
                data = new DinoSaveData
                {
                    BestPortraitScore = portrait,
                    BestLandscapeScore = landscape,
                    PreferredOrientation = orientation
                };
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

        private static long Score(JToken token)
        {
            var text = Text(token);
            // Int64 parsing tolerates a trailing NUL; disk score strings must contain digits only.
            foreach (var character in text)
                if (character < '0' || character > '9') throw new FormatException("Expected decimal score digits only.");
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 0)
                throw new FormatException("Expected a nonnegative 64-bit decimal score string.");
            return value;
        }

        private static bool IsOrientation(string orientation)
        {
            return orientation == "portrait" || orientation == "landscape";
        }
    }
}
