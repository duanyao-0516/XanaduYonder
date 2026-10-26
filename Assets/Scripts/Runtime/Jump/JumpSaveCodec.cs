using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Jump;

namespace XanaduYonder.Runtime.Jump
{
    // Disk input is checked token by token; serializer coercion must not repair malformed state.
    public static class JumpSaveCodec
    {
        public const int MaximumLength = 65536;
        private static readonly string[] RootFields = { "schemaVersion", "gameId", "game", "bestScore" };
        private static readonly string[] GameFields =
        {
            "currentHalfSize", "nextX", "nextZ", "nextHalfSize", "playerX", "playerZ",
            "score", "steps", "streak", "isGameOver", "randomState"
        };

        public static string Encode(JumpSaveData data)
        {
            if (data == null || data.Game == null)
                throw new ArgumentException("Missing jump save state.", nameof(data));
            var snapshot = data.Game.Copy();
            var best = data.BestScore;
            if (!snapshot.TryValidate(out var reason) || best < snapshot.Score)
                throw new ArgumentException(reason ?? "Best score is below the current score.", nameof(data));
            var game = new JObject
            {
                ["currentHalfSize"] = snapshot.CurrentHalfSize,
                ["nextX"] = snapshot.NextX,
                ["nextZ"] = snapshot.NextZ,
                ["nextHalfSize"] = snapshot.NextHalfSize,
                ["playerX"] = snapshot.PlayerX,
                ["playerZ"] = snapshot.PlayerZ,
                ["score"] = snapshot.Score.ToString(CultureInfo.InvariantCulture),
                ["steps"] = snapshot.Steps,
                ["streak"] = snapshot.Streak,
                ["isGameOver"] = snapshot.IsGameOver,
                ["randomState"] = snapshot.RandomState
            };
            return new JObject
            {
                ["schemaVersion"] = 1,
                ["gameId"] = "jump",
                ["game"] = game,
                ["bestScore"] = best.ToString(CultureInfo.InvariantCulture)
            }.ToString(Formatting.None);
        }

        public static bool TryDecode(string json, out JumpSaveData data, out string reason)
        {
            data = null;
            reason = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength)
            {
                reason = "Missing or oversized jump save file.";
                return false;
            }
            try
            {
                JObject root;
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { MaxDepth = 8, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException("Trailing data after jump save object.");
                }
                RequireFields(root, RootFields);
                if (Integer(root["schemaVersion"]) != 1 || Text(root["gameId"]) != "jump")
                    throw new FormatException("Unsupported jump save schema.");
                var game = root["game"] as JObject;
                if (game == null) throw new FormatException("Missing jump game state.");
                RequireFields(game, GameFields);
                var snapshot = new JumpSnapshot
                {
                    CurrentHalfSize = Number(game["currentHalfSize"]),
                    NextX = Number(game["nextX"]),
                    NextZ = Number(game["nextZ"]),
                    NextHalfSize = Number(game["nextHalfSize"]),
                    PlayerX = Number(game["playerX"]),
                    PlayerZ = Number(game["playerZ"]),
                    Score = Score(game["score"]),
                    Steps = Integer(game["steps"]),
                    Streak = Integer(game["streak"]),
                    IsGameOver = Boolean(game["isGameOver"]),
                    RandomState = UnsignedInteger(game["randomState"])
                };
                if (!snapshot.TryValidate(out reason)) return false;
                var best = Score(root["bestScore"]);
                if (best < snapshot.Score) throw new FormatException("Best score is below the current score.");
                data = new JumpSaveData { Game = snapshot, BestScore = best };
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException ||
                                              exception is OverflowException || exception is ArgumentException)
            {
                reason = exception.Message;
                return false;
            }
        }

        private static void RequireFields(JObject value, string[] fields)
        {
            if (value.Count != fields.Length) throw new FormatException("Unexpected jump save fields.");
            foreach (var field in fields)
                if (value.Property(field) == null) throw new FormatException("Missing jump save field: " + field);
        }

        private static int Integer(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected an integer field.");
            return checked((int)token);
        }

        private static uint UnsignedInteger(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected an unsigned integer field.");
            return checked((uint)token);
        }

        private static double Number(JToken token)
        {
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
                throw new FormatException("Expected a numeric field.");
            var value = (double)token;
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new FormatException("Expected a finite numeric field.");
            return value;
        }

        private static bool Boolean(JToken token)
        {
            if (token == null || token.Type != JTokenType.Boolean) throw new FormatException("Expected a boolean field.");
            return (bool)token;
        }

        private static string Text(JToken token)
        {
            if (token == null || token.Type != JTokenType.String) throw new FormatException("Expected a string field.");
            return (string)token;
        }

        private static long Score(JToken token)
        {
            if (!long.TryParse(Text(token), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                throw new FormatException("Expected a nonnegative 64-bit score string.");
            return value;
        }
    }
}
