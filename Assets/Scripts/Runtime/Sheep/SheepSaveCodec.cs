using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Sheep;

namespace XanaduYonder.Runtime.Sheep
{
    public static class SheepSaveCodec
    {
        public const int MaximumLength = 128 * 1024;
        private static readonly string[] RootFields = { "schemaVersion", "gameId", "game", "completedRuns", "winRecorded" };
        private static readonly string[] GameFields =
        {
            "level", "layoutId", "kinds", "locations", "tray", "reserve", "randomState",
            "removeUsed", "undoUsed", "shuffleUsed", "reviveUsed", "lastPickedId", "lastSource", "lastReserveIndex"
        };

        public static string Encode(SheepSaveData data)
        {
            if (data == null || data.Game == null)
                throw new ArgumentException("Missing sheep save state.", nameof(data));
            if (!data.Game.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(data));
            var snapshot = data.Game.Copy();
            var completedRuns = data.CompletedRuns;
            var winRecorded = data.WinRecorded;
            if (!Validate(snapshot, completedRuns, winRecorded, out reason)) throw new ArgumentException(reason, nameof(data));
            var locations = new JArray();
            foreach (var location in snapshot.Locations) locations.Add((int)location);
            var game = new JObject
            {
                ["level"] = snapshot.Level,
                ["layoutId"] = snapshot.LayoutId,
                ["kinds"] = new JArray(snapshot.Kinds),
                ["locations"] = locations,
                ["tray"] = new JArray(snapshot.Tray),
                ["reserve"] = new JArray(snapshot.Reserve),
                ["randomState"] = snapshot.RandomState,
                ["removeUsed"] = snapshot.RemoveUsed,
                ["undoUsed"] = snapshot.UndoUsed,
                ["shuffleUsed"] = snapshot.ShuffleUsed,
                ["reviveUsed"] = snapshot.ReviveUsed,
                ["lastPickedId"] = snapshot.LastPickedId,
                ["lastSource"] = snapshot.LastSource,
                ["lastReserveIndex"] = snapshot.LastReserveIndex
            };
            return new JObject
            {
                ["schemaVersion"] = 1,
                ["gameId"] = "sheep",
                ["game"] = game,
                ["completedRuns"] = completedRuns.ToString(CultureInfo.InvariantCulture),
                ["winRecorded"] = winRecorded
            }.ToString(Formatting.None);
        }

        public static bool TryDecode(string json, out SheepSaveData data, out string reason)
        {
            data = null;
            reason = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength)
            {
                reason = "Missing or oversized sheep save file.";
                return false;
            }
            try
            {
                JObject root;
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { MaxDepth = 8, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException("Trailing data after sheep save object.");
                }
                RequireFields(root, RootFields);
                if (Integer(root["schemaVersion"]) != 1 || Text(root["gameId"]) != "sheep")
                    throw new FormatException("Unsupported sheep save schema.");
                var game = root["game"] as JObject;
                if (game == null) throw new FormatException("Missing sheep game state.");
                RequireFields(game, GameFields);
                var locationValues = IntegerArray(game["locations"], 144);
                var locations = new SheepLocation[locationValues.Length];
                for (var index = 0; index < locations.Length; index++)
                {
                    var value = locationValues[index];
                    if (value < (int)SheepLocation.Board || value > (int)SheepLocation.Removed)
                        throw new FormatException("Unknown sheep card location.");
                    locations[index] = (SheepLocation)value;
                }
                var snapshot = new SheepSnapshot
                {
                    Level = Integer(game["level"]),
                    LayoutId = Integer(game["layoutId"]),
                    Kinds = IntegerArray(game["kinds"], 144),
                    Locations = locations,
                    Tray = IntegerArray(game["tray"], 7),
                    Reserve = IntegerArray(game["reserve"], 6),
                    RandomState = UnsignedInteger(game["randomState"]),
                    RemoveUsed = Boolean(game["removeUsed"]),
                    UndoUsed = Boolean(game["undoUsed"]),
                    ShuffleUsed = Boolean(game["shuffleUsed"]),
                    ReviveUsed = Boolean(game["reviveUsed"]),
                    LastPickedId = Integer(game["lastPickedId"]),
                    LastSource = Integer(game["lastSource"]),
                    LastReserveIndex = Integer(game["lastReserveIndex"])
                };
                var completedRuns = CompletionCount(root["completedRuns"]);
                var winRecorded = Boolean(root["winRecorded"]);
                if (!Validate(snapshot, completedRuns, winRecorded, out reason)) return false;
                data = new SheepSaveData { Game = snapshot, CompletedRuns = completedRuns, WinRecorded = winRecorded };
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException ||
                                              exception is OverflowException || exception is ArgumentException)
            {
                reason = exception.Message;
                return false;
            }
        }

        private static bool Validate(SheepSnapshot snapshot, long completedRuns, bool winRecorded, out string reason)
        {
            if (!snapshot.TryValidate(out reason)) return false;
            if (completedRuns < 0)
            {
                reason = "Completion count must not be negative.";
                return false;
            }
            var challengeWon = snapshot.Level == 2;
            foreach (var location in snapshot.Locations)
                if (location != SheepLocation.Removed) challengeWon = false;
            // The page records a challenge win before saving, so a restore cannot count it twice.
            if (winRecorded != challengeWon || (winRecorded && completedRuns == 0))
            {
                reason = "Completion accounting does not match the saved game.";
                return false;
            }
            return true;
        }

        private static void RequireFields(JObject value, string[] fields)
        {
            if (value.Count != fields.Length) throw new FormatException("Unexpected sheep save fields.");
            foreach (var field in fields)
                if (value.Property(field) == null) throw new FormatException("Missing sheep save field: " + field);
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

        private static int[] IntegerArray(JToken token, int maximumCount)
        {
            var array = token as JArray;
            if (array == null || array.Count > maximumCount) throw new FormatException("Expected a bounded integer array.");
            var values = new int[array.Count];
            for (var index = 0; index < values.Length; index++) values[index] = Integer(array[index]);
            return values;
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

        private static long CompletionCount(JToken token)
        {
            if (!long.TryParse(Text(token), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                throw new FormatException("Expected a nonnegative 64-bit completion count string.");
            return value;
        }
    }
}
