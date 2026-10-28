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
        private static readonly string[] RootFields = {
            "schemaVersion", "gameId", "challenge", "endless", "tutorial", "activeMode",
            "preferences", "completedRuns", "endlessBest", "winRecorded"
        };
        private static readonly string[] PreferenceFields = {
            "defaultDifficulty", "skipDifficultyPrompt", "removeLimit", "undoLimit", "shuffleLimit"
        };
        private static readonly string[] GameFields = {
            "mode", "difficulty", "wave", "score", "level", "layoutId", "kinds", "locations", "tray", "reserve", "randomState",
            "removeLimit", "undoLimit", "shuffleLimit", "removeRemaining", "undoRemaining", "shuffleRemaining",
            "reviveUsed", "lastPickedId", "lastSource", "lastReserveIndex"
        };

        public static string Encode(SheepSaveData data)
        {
            if (!Validate(data, out var reason)) throw new ArgumentException(reason, nameof(data));
            var preferences = data.Preferences.Copy();
            var root = new JObject {
                ["schemaVersion"] = 2,
                ["gameId"] = "sheep",
                ["challenge"] = EncodeGame(data.Challenge),
                ["endless"] = EncodeGame(data.Endless),
                ["tutorial"] = EncodeGame(data.Tutorial),
                ["activeMode"] = (int)data.ActiveMode,
                ["preferences"] = new JObject {
                    ["defaultDifficulty"] = (int)preferences.DefaultDifficulty,
                    ["skipDifficultyPrompt"] = preferences.SkipDifficultyPrompt,
                    ["removeLimit"] = preferences.RemoveLimit,
                    ["undoLimit"] = preferences.UndoLimit,
                    ["shuffleLimit"] = preferences.ShuffleLimit
                },
                ["completedRuns"] = Decimal(data.CompletedRuns),
                ["endlessBest"] = Decimal(data.EndlessBest),
                ["winRecorded"] = data.WinRecorded
            };
            var json = root.ToString(Formatting.None);
            if (Encoding.UTF8.GetByteCount(json) > MaximumLength)
                throw new ArgumentException("Sheep save state is too large.", nameof(data));
            return json;
        }

        private static JToken EncodeGame(SheepSnapshot source)
        {
            if (source == null) return JValue.CreateNull();
            var snapshot = source.Copy();
            var locations = new JArray();
            foreach (var location in snapshot.Locations) locations.Add((int)location);
            return new JObject {
                ["mode"] = (int)snapshot.Mode,
                ["difficulty"] = (int)snapshot.Difficulty,
                ["wave"] = snapshot.Wave,
                ["score"] = Decimal(snapshot.Score),
                ["level"] = snapshot.Level,
                ["layoutId"] = snapshot.LayoutId,
                ["kinds"] = new JArray(snapshot.Kinds),
                ["locations"] = locations,
                ["tray"] = new JArray(snapshot.Tray),
                ["reserve"] = new JArray(snapshot.Reserve),
                ["randomState"] = snapshot.RandomState,
                ["removeLimit"] = snapshot.RemoveLimit,
                ["undoLimit"] = snapshot.UndoLimit,
                ["shuffleLimit"] = snapshot.ShuffleLimit,
                ["removeRemaining"] = snapshot.RemoveRemaining,
                ["undoRemaining"] = snapshot.UndoRemaining,
                ["shuffleRemaining"] = snapshot.ShuffleRemaining,
                ["reviveUsed"] = snapshot.ReviveUsed,
                ["lastPickedId"] = snapshot.LastPickedId,
                ["lastSource"] = snapshot.LastSource,
                ["lastReserveIndex"] = snapshot.LastReserveIndex
            };
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
                if (Integer(root["schemaVersion"]) != 2 || Text(root["gameId"]) != "sheep")
                    throw new FormatException("Unsupported sheep save schema.");
                var preferences = root["preferences"] as JObject;
                if (preferences == null) throw new FormatException("Missing sheep preferences.");
                RequireFields(preferences, PreferenceFields);
                var candidate = new SheepSaveData {
                    Challenge = DecodeGame(root["challenge"]),
                    Endless = DecodeGame(root["endless"]),
                    Tutorial = DecodeGame(root["tutorial"]),
                    ActiveMode = (SheepMode)Integer(root["activeMode"]),
                    Preferences = new SheepPreferences {
                        DefaultDifficulty = (SheepDifficulty)Integer(preferences["defaultDifficulty"]),
                        SkipDifficultyPrompt = Boolean(preferences["skipDifficultyPrompt"]),
                        RemoveLimit = Integer(preferences["removeLimit"]),
                        UndoLimit = Integer(preferences["undoLimit"]),
                        ShuffleLimit = Integer(preferences["shuffleLimit"])
                    },
                    CompletedRuns = NonnegativeLong(root["completedRuns"]),
                    EndlessBest = NonnegativeLong(root["endlessBest"]),
                    WinRecorded = Boolean(root["winRecorded"])
                };
                if (!Validate(candidate, out reason)) return false;
                data = candidate;
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException ||
                                              exception is OverflowException || exception is ArgumentException)
            {
                reason = exception.Message;
                return false;
            }
        }

        private static SheepSnapshot DecodeGame(JToken token)
        {
            if (token != null && token.Type == JTokenType.Null) return null;
            var game = token as JObject;
            if (game == null) throw new FormatException("Expected a sheep game object or null slot.");
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
            return new SheepSnapshot {
                Mode = (SheepMode)Integer(game["mode"]),
                Difficulty = (SheepDifficulty)Integer(game["difficulty"]),
                Wave = Integer(game["wave"]),
                Score = NonnegativeLong(game["score"]),
                Level = Integer(game["level"]),
                LayoutId = Integer(game["layoutId"]),
                Kinds = IntegerArray(game["kinds"], 144),
                Locations = locations,
                Tray = IntegerArray(game["tray"], 7),
                Reserve = IntegerArray(game["reserve"], 6),
                RandomState = UnsignedInteger(game["randomState"]),
                RemoveLimit = Integer(game["removeLimit"]),
                UndoLimit = Integer(game["undoLimit"]),
                ShuffleLimit = Integer(game["shuffleLimit"]),
                RemoveRemaining = Integer(game["removeRemaining"]),
                UndoRemaining = Integer(game["undoRemaining"]),
                ShuffleRemaining = Integer(game["shuffleRemaining"]),
                ReviveUsed = Boolean(game["reviveUsed"]),
                LastPickedId = Integer(game["lastPickedId"]),
                LastSource = Integer(game["lastSource"]),
                LastReserveIndex = Integer(game["lastReserveIndex"])
            };
        }

        private static bool Validate(SheepSaveData data, out string reason)
        {
            reason = null;
            if (data == null) return Invalid("Missing sheep save state.", out reason);
            if (data.ActiveMode < SheepMode.Challenge || data.ActiveMode > SheepMode.Tutorial)
                return Invalid("Unknown active sheep mode.", out reason);
            if (data.Preferences == null) return Invalid("Missing sheep preferences.", out reason);
            if (!data.Preferences.TryValidate(out reason) ||
                !ValidateSlot(data.Challenge, SheepMode.Challenge, out reason) ||
                !ValidateSlot(data.Endless, SheepMode.Endless, out reason) ||
                !ValidateSlot(data.Tutorial, SheepMode.Tutorial, out reason)) return false;
            if (data.CompletedRuns < 0 || data.EndlessBest < 0)
                return Invalid("Sheep records must not be negative.", out reason);
            var challengeWon = data.Challenge != null;
            if (data.Challenge != null)
                foreach (var location in data.Challenge.Locations)
                    if (location != SheepLocation.Removed) challengeWon = false;
            // Challenge accounting belongs to its slot even while another mode is active.
            if (data.WinRecorded != challengeWon || (data.WinRecorded && data.CompletedRuns == 0))
                return Invalid("Completion accounting does not match the challenge slot.", out reason);
            var endless = data.Endless;
            // Eligibility is frozen in the run; changing future preferences cannot change its rank.
            if (endless != null && endless.RemoveLimit == 2 && endless.UndoLimit == 3 && endless.ShuffleLimit == 1 &&
                data.EndlessBest < endless.Score)
                return Invalid("Endless best is below the current standard run score.", out reason);
            return true;
        }

        private static bool ValidateSlot(SheepSnapshot snapshot, SheepMode mode, out string reason)
        {
            reason = null;
            if (snapshot == null) return true;
            if (snapshot.Mode != mode) return Invalid("Sheep slot does not match its run mode.", out reason);
            return snapshot.TryValidate(out reason);
        }

        private static bool Invalid(string message, out string reason) { reason = message; return false; }
        private static string Decimal(long value) { return value.ToString(CultureInfo.InvariantCulture); }

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

        private static long NonnegativeLong(JToken token)
        {
            if (!long.TryParse(Text(token), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                throw new FormatException("Expected a nonnegative 64-bit decimal string.");
            return value;
        }
    }
}
