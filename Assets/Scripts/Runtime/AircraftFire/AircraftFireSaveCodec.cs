using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XanaduYonder.Runtime.AircraftFire
{
    public static class AircraftFireSaveCodec
    {
        public const int MaximumLength = 4096;
        private static readonly string[] Fields =
        {
            "schemaVersion", "gameId", "classicBestScore", "crazyBestScore", "levelBestScore", "highestStage"
        };

        public static string Encode(AircraftFireSaveData data)
        {
            if (data == null || data.ClassicBestScore < 0 || data.CrazyBestScore < 0 ||
                data.LevelBestScore < 0 || data.HighestStage < 0 || data.HighestStage > 7)
                throw new ArgumentException("Aircraft Fire record has invalid scores or stage.", nameof(data));
            return new JObject
            {
                ["schemaVersion"] = 1,
                ["gameId"] = "aircraft-fire",
                ["classicBestScore"] = data.ClassicBestScore.ToString(CultureInfo.InvariantCulture),
                ["crazyBestScore"] = data.CrazyBestScore.ToString(CultureInfo.InvariantCulture),
                ["levelBestScore"] = data.LevelBestScore.ToString(CultureInfo.InvariantCulture),
                ["highestStage"] = data.HighestStage
            }.ToString(Formatting.None);
        }

        public static bool TryDecode(string json, out AircraftFireSaveData data, out string reason)
        {
            data = null;
            reason = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength)
            {
                reason = "Missing or oversized Aircraft Fire save file.";
                return false;
            }
            try
            {
                JObject root;
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { MaxDepth = 4, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException("Trailing data after Aircraft Fire save object.");
                }
                if (root.Count != Fields.Length) throw new FormatException("Unexpected Aircraft Fire save fields.");
                foreach (var field in Fields)
                    if (root.Property(field) == null) throw new FormatException("Missing Aircraft Fire save field: " + field);
                if (root["schemaVersion"].Type != JTokenType.Integer || checked((int)root["schemaVersion"]) != 1 || Text(root["gameId"]) != "aircraft-fire")
                    throw new FormatException("Unsupported Aircraft Fire save schema.");
                if (root["highestStage"].Type != JTokenType.Integer)
                    throw new FormatException("Aircraft Fire stage must be an integer.");
                var stage = checked((int)root["highestStage"]);
                if (stage < 0 || stage > 7) throw new FormatException("Aircraft Fire stage is outside 0..7.");
                data = new AircraftFireSaveData
                {
                    ClassicBestScore = Score(root["classicBestScore"]),
                    CrazyBestScore = Score(root["crazyBestScore"]),
                    LevelBestScore = Score(root["levelBestScore"]),
                    HighestStage = stage
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

        private static long Score(JToken token)
        {
            var text = Text(token);
            if (text.Length < 1 || text.Length > 19) throw new FormatException("Expected a 64-bit decimal score string.");
            foreach (var character in text)
                if (character < '0' || character > '9') throw new FormatException("Expected decimal score digits only.");
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var score))
                throw new FormatException("Aircraft Fire score exceeds Int64.");
            return score;
        }

        private static string Text(JToken token)
        {
            if (token == null || token.Type != JTokenType.String) throw new FormatException("Expected a string field.");
            return (string)token;
        }
    }
}
