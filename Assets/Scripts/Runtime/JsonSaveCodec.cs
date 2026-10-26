using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core;

namespace XanaduYonder.Runtime
{
    // Explicit token validation prevents the serializer's usual string/number coercion at the disk boundary.
    public static class JsonSaveCodec
    {
        public const int MaximumLength = 65536;

        public static string Encode(SaveData data)
        {
            string reason = null;
            if (data == null || !GameSnapshot.TryValidate(data.Game, out reason) || data.BestScore < data.Game.score)
                throw new ArgumentException(reason ?? "Invalid saved game or best score.", nameof(data));
            if (!HasPlayableTileCount(data.Game.cells))
                throw new ArgumentException("A saved game must contain at least two tiles.", nameof(data));
            var game = new JObject
            {
                ["schemaVersion"] = data.Game.schemaVersion,
                ["gameId"] = data.Game.gameId,
                ["cells"] = new JArray(data.Game.cells),
                ["score"] = data.Game.score.ToString(CultureInfo.InvariantCulture),
                ["winState"] = data.Game.winAcknowledged ? 2 : 1
            };
            return new JObject
            {
                ["schemaVersion"] = 1,
                ["saveKind"] = "xanaduyonder",
                ["game"] = game,
                ["bestScore"] = data.BestScore.ToString(CultureInfo.InvariantCulture),
                ["soundState"] = data.SoundEnabled ? 1 : 2,
                ["hapticsState"] = data.HapticsEnabled ? 1 : 2
            }.ToString(Formatting.None);
        }

        public static bool TryDecode(string json, out SaveData data, out string reason)
        {
            data = null;
            reason = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength)
            {
                reason = "Missing or oversized save file.";
                return false;
            }
            try
            {
                JObject root;
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { MaxDepth = 8, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException("Trailing data after save object.");
                }
                if (Integer(root["schemaVersion"]) != 1 || Text(root["saveKind"]) != "xanaduyonder")
                    throw new FormatException("Unsupported save schema.");
                var game = root["game"] as JObject;
                if (game == null) throw new FormatException("Missing game state.");
                var cells = game["cells"] as JArray;
                if (cells == null || cells.Count != 16) throw new FormatException("Expected a 4 by 4 board.");
                var snapshot = new GameSnapshot
                {
                    schemaVersion = Integer(game["schemaVersion"]),
                    gameId = Text(game["gameId"]),
                    cells = new int[16],
                    score = Score(game["score"]),
                    winAcknowledged = Flag(game["winState"]) == 2
                };
                for (var i = 0; i < 16; i++) snapshot.cells[i] = Integer(cells[i]);
                if (!GameSnapshot.TryValidate(snapshot, out reason)) return false;
                if (!HasPlayableTileCount(snapshot.cells)) throw new FormatException("A saved game must contain at least two tiles.");
                var best = Score(root["bestScore"]);
                if (best < snapshot.score) throw new FormatException("Best score is below the current score.");
                var sound = Flag(root["soundState"]) == 1;
                var haptics = Flag(root["hapticsState"]) == 1;
                data = new SaveData { Game = snapshot, BestScore = best, SoundEnabled = sound, HapticsEnabled = haptics };
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException ||
                                              exception is OverflowException || exception is ArgumentException)
            {
                reason = exception.Message;
                return false;
            }
        }

        private static int Integer(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected an integer field.");
            return checked((int)token);
        }

        private static bool HasPlayableTileCount(int[] cells)
        {
            var occupied = 0;
            foreach (var cell in cells) if (cell != 0) occupied++;
            return occupied >= 2;
        }

        private static string Text(JToken token)
        {
            if (token == null || token.Type != JTokenType.String) throw new FormatException("Expected a string field.");
            return (string)token;
        }

        private static long Score(JToken token)
        {
            long value;
            if (!long.TryParse(Text(token), NumberStyles.None, CultureInfo.InvariantCulture, out value))
                throw new FormatException("Expected a nonnegative 64-bit score string.");
            return value;
        }

        private static int Flag(JToken token)
        {
            var value = Integer(token);
            if (value != 1 && value != 2) throw new FormatException("Expected an explicit settings flag.");
            return value;
        }
    }
}
