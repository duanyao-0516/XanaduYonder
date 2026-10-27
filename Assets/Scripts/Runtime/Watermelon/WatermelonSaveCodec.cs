using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Watermelon;

namespace XanaduYonder.Runtime.Watermelon
{
    public static class WatermelonSaveCodec
    {
        public const int MaximumLength = 128 * 1024;
        private static readonly string[] RootFields = { "schemaVersion", "gameId", "theme", "bestScore", "goalAcknowledged", "game" };
        private static readonly string[] GameFields = {
            "balls", "score", "currentLevel", "nextLevel", "randomState", "nextId", "dropCooldown", "dangerTime", "gameOver"
        };
        private static readonly string[] BallFields = {
            "id", "level", "x", "y", "velocityX", "velocityY", "angle", "angularVelocity", "age", "contacted"
        };

        public static string Encode(WatermelonSaveData data)
        {
            if (data == null) throw new ArgumentException("Missing watermelon save state.", nameof(data));
            if (!data.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(data));
            var copy = data.Copy();
            var snapshot = copy.Game;
            var balls = new JArray();
            foreach (var ball in snapshot.Balls)
            {
                balls.Add(new JObject {
                    ["id"] = ball.Id, ["level"] = ball.Level,
                    ["x"] = ball.X, ["y"] = ball.Y,
                    ["velocityX"] = ball.VelocityX, ["velocityY"] = ball.VelocityY,
                    ["angle"] = ball.Angle, ["angularVelocity"] = ball.AngularVelocity,
                    ["age"] = ball.Age, ["contacted"] = ball.Contacted
                });
            }
            return new JObject {
                ["schemaVersion"] = 1, ["gameId"] = "watermelon", ["theme"] = copy.Theme,
                ["bestScore"] = copy.BestScore, ["goalAcknowledged"] = copy.GoalAcknowledged,
                ["game"] = new JObject {
                    ["balls"] = balls, ["score"] = snapshot.Score,
                    ["currentLevel"] = snapshot.CurrentLevel, ["nextLevel"] = snapshot.NextLevel,
                    ["randomState"] = snapshot.RandomState, ["nextId"] = snapshot.NextId,
                    ["dropCooldown"] = snapshot.DropCooldown, ["dangerTime"] = snapshot.DangerTime,
                    ["gameOver"] = snapshot.GameOver
                }
            }.ToString(Formatting.None);
        }

        public static bool TryDecode(string json, out WatermelonSaveData data, out string reason)
        {
            data = null;
            reason = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength)
            {
                reason = "Missing or oversized watermelon save file.";
                return false;
            }
            try
            {
                JObject root;
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { MaxDepth = 8, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException("Trailing data after watermelon save object.");
                }
                RequireFields(root, RootFields);
                if (Integer(root["schemaVersion"]) != 1 || Text(root["gameId"]) != "watermelon")
                    throw new FormatException("Unsupported watermelon save schema.");
                var game = root["game"] as JObject;
                if (game == null) throw new FormatException("Missing watermelon game object.");
                RequireFields(game, GameFields);
                var array = game["balls"] as JArray;
                if (array == null || array.Count > WatermelonRules.MaxBalls) throw new FormatException("Invalid watermelon ball array.");
                var balls = new WatermelonBallState[array.Count];
                for (var index = 0; index < balls.Length; index++)
                {
                    var ball = array[index] as JObject;
                    if (ball == null) throw new FormatException("Missing watermelon ball object.");
                    RequireFields(ball, BallFields);
                    balls[index] = new WatermelonBallState {
                        Id = Integer(ball["id"]), Level = Integer(ball["level"]),
                        X = Number(ball["x"]), Y = Number(ball["y"]),
                        VelocityX = Number(ball["velocityX"]), VelocityY = Number(ball["velocityY"]),
                        Angle = Number(ball["angle"]), AngularVelocity = Number(ball["angularVelocity"]),
                        Age = Number(ball["age"]), Contacted = Boolean(ball["contacted"])
                    };
                }
                var candidate = new WatermelonSaveData {
                    Theme = Integer(root["theme"]), BestScore = Integer(root["bestScore"]),
                    GoalAcknowledged = Boolean(root["goalAcknowledged"]),
                    Game = new WatermelonSnapshot {
                        Balls = balls, Score = Integer(game["score"]),
                        CurrentLevel = Integer(game["currentLevel"]), NextLevel = Integer(game["nextLevel"]),
                        RandomState = UnsignedInteger(game["randomState"]), NextId = Integer(game["nextId"]),
                        DropCooldown = Number(game["dropCooldown"]), DangerTime = Number(game["dangerTime"]),
                        GameOver = Boolean(game["gameOver"])
                    }
                };
                if (!candidate.TryValidate(out reason)) return false;
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

        private static void RequireFields(JObject value, string[] fields)
        {
            if (value.Count != fields.Length) throw new FormatException("Unexpected watermelon save fields.");
            foreach (var field in fields)
                if (value.Property(field) == null) throw new FormatException("Missing watermelon save field: " + field);
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
        private static float Number(JToken token)
        {
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
                throw new FormatException("Expected a numeric field.");
            var number = (double)token;
            if (double.IsNaN(number) || double.IsInfinity(number) || number < -float.MaxValue || number > float.MaxValue)
                throw new FormatException("Expected a finite single precision number.");
            return (float)number;
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
    }
}
