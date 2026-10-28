using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.HappyBall;

namespace XanaduYonder.Runtime.HappyBall
{
    public static class HappyBallSaveCodec
    {
        public const int MaximumLength = 256 * 1024;
        private const int MaximumDepth = 12;
        private static readonly string[] RootFields = { "schemaVersion", "gameId", "game", "bestScore", "hasStarted", "resultRecorded" };
        private static readonly string[] GameFields = { "rulesVersion", "randomState", "nextLayerIndex", "originLayer", "passedLayers", "naturalStreak", "towerAngle", "ballY", "ballVelocity", "score", "power", "impactLayerIndex", "gameOver", "layers" };
        private static readonly string[] LayerFields = { "index", "y", "state", "sectors", "walls" };
        private static readonly string[] SectorFields = { "startAngle", "sweepAngle", "dangerous" };
        private static readonly string[] WallFields = { "angle", "widthAngle", "height" };

        public static string Encode(HappyBallSaveData data)
        {
            if (data == null) throw new ArgumentException("Missing happy-ball save.", nameof(data));
            if (!data.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(data));
            var copy = data.Copy();
            var game = copy.Game;
            var layers = new JArray();
            foreach (var layer in game.Layers)
            {
                var sectors = new JArray();
                foreach (var sector in layer.Sectors) sectors.Add(new JObject {
                    ["startAngle"] = sector.StartAngle, ["sweepAngle"] = sector.SweepAngle, ["dangerous"] = sector.Dangerous
                });
                var walls = new JArray();
                foreach (var wall in layer.Walls) walls.Add(new JObject {
                    ["angle"] = wall.Angle, ["widthAngle"] = wall.WidthAngle, ["height"] = wall.Height
                });
                layers.Add(new JObject { ["index"] = layer.Index, ["y"] = layer.Y, ["state"] = layer.State.ToString(), ["sectors"] = sectors, ["walls"] = walls });
            }
            string json = new JObject {
                ["schemaVersion"] = copy.SchemaVersion, ["gameId"] = copy.GameId, ["bestScore"] = Decimal(copy.BestScore),
                ["hasStarted"] = copy.HasStarted, ["resultRecorded"] = copy.ResultRecorded,
                ["game"] = new JObject {
                    ["rulesVersion"] = game.RulesVersion, ["randomState"] = game.RandomState, ["nextLayerIndex"] = game.NextLayerIndex,
                    ["originLayer"] = game.OriginLayer, ["passedLayers"] = game.PassedLayers, ["naturalStreak"] = game.NaturalStreak,
                    ["towerAngle"] = game.TowerAngle, ["ballY"] = game.BallY, ["ballVelocity"] = game.BallVelocity,
                    ["score"] = Decimal(game.Score), ["power"] = game.Power.ToString(), ["impactLayerIndex"] = game.ImpactLayerIndex,
                    ["gameOver"] = game.GameOver, ["layers"] = layers
                }
            }.ToString(Formatting.None);
            if (Encoding.UTF8.GetByteCount(json) > MaximumLength) throw new ArgumentException("Oversized happy-ball save.", nameof(data));
            return json;
        }

        public static bool TryDecode(string json, out HappyBallSaveData data, out string reason)
        {
            data = null; reason = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength)
            { reason = "Missing or oversized happy-ball save."; return false; }
            try
            {
                // Json.NET accepts JavaScript extensions by default. The disk contract is strict JSON.
                new StrictJsonSyntax(json).Validate();
                JObject root;
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { MaxDepth = MaximumDepth, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException("Trailing data after save object.");
                }
                RequireFields(root, RootFields);
                if (Integer(root["schemaVersion"]) != 1 || Text(root["gameId"]) != "happy-ball") throw new FormatException("Unsupported happy-ball save schema.");
                var candidate = new HappyBallSaveData {
                    SchemaVersion = Integer(root["schemaVersion"]), GameId = Text(root["gameId"]),
                    BestScore = DecimalInteger(root["bestScore"]), HasStarted = Boolean(root["hasStarted"]),
                    ResultRecorded = Boolean(root["resultRecorded"]), Game = DecodeGame(Object(root["game"]))
                };
                if (!candidate.TryValidate(out reason)) return false;
                candidate.Game = HappyBallGame.NormalizeForCurrentRules(candidate.Game);
                candidate.BestScore = Math.Max(candidate.BestScore, candidate.Game.Score);
                data = candidate; return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException || exception is OverflowException || exception is ArgumentException)
            { reason = exception.Message; return false; }
        }

        private static HappyBallSnapshot DecodeGame(JObject game)
        {
            RequireFields(game, GameFields);
            int rulesVersion = Integer(game["rulesVersion"]);
            if (rulesVersion != HappyBallRules.Version && rulesVersion != HappyBallRules.LegacyVersion)
                throw new FormatException("Unsupported happy-ball rules version.");
            var source = Array(game["layers"], HappyBallRules.MaximumLayers);
            var layers = new HappyBallLayer[source.Count];
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = Object(source[i]); RequireFields(layer, LayerFields);
                var sectorSource = Array(layer["sectors"], 32); var sectors = new HappyBallSector[sectorSource.Count];
                for (int j = 0; j < sectors.Length; j++)
                {
                    var sector = Object(sectorSource[j]); RequireFields(sector, SectorFields);
                    sectors[j] = new HappyBallSector { StartAngle = Number(sector["startAngle"]), SweepAngle = Number(sector["sweepAngle"]), Dangerous = Boolean(sector["dangerous"]) };
                }
                var wallSource = Array(layer["walls"], 8); var walls = new HappyBallWall[wallSource.Count];
                for (int j = 0; j < walls.Length; j++)
                {
                    var wall = Object(wallSource[j]); RequireFields(wall, WallFields);
                    walls[j] = new HappyBallWall { Angle = Number(wall["angle"]), WidthAngle = Number(wall["widthAngle"]), Height = Number(wall["height"]) };
                }
                layers[i] = new HappyBallLayer { Index = Integer(layer["index"]), Y = Number(layer["y"]), State = LayerState(layer["state"]), Sectors = sectors, Walls = walls };
            }
            return new HappyBallSnapshot {
                RulesVersion = Integer(game["rulesVersion"]), RandomState = UnsignedInteger(game["randomState"]),
                NextLayerIndex = Integer(game["nextLayerIndex"]), OriginLayer = Integer(game["originLayer"]),
                PassedLayers = Integer(game["passedLayers"]), NaturalStreak = Integer(game["naturalStreak"]),
                TowerAngle = Number(game["towerAngle"]), BallY = Number(game["ballY"]), BallVelocity = Number(game["ballVelocity"]),
                Score = DecimalInteger(game["score"]), Power = Power(game["power"]), ImpactLayerIndex = Integer(game["impactLayerIndex"]),
                GameOver = Boolean(game["gameOver"]), Layers = layers
            };
        }
        private static HappyBallPower Power(JToken token)
        {
            switch (Text(token)) {
                case "Normal": return HappyBallPower.Normal;
                case "Armed": return HappyBallPower.Armed;
                case "Spent": return HappyBallPower.Spent;
                default: throw new FormatException("Unknown happy-ball power state.");
            }
        }
        private static HappyBallLayerState LayerState(JToken token)
        {
            switch (Text(token)) {
                case "Active": return HappyBallLayerState.Active;
                case "Passed": return HappyBallLayerState.Passed;
                case "Broken": return HappyBallLayerState.Broken;
                default: throw new FormatException("Unknown happy-ball layer state.");
            }
        }
        private static void RequireFields(JObject value, string[] fields)
        {
            if (value.Count != fields.Length) throw new FormatException("Unexpected save fields.");
            foreach (string field in fields) if (value.Property(field) == null) throw new FormatException("Missing save field: " + field);
        }
        private static JObject Object(JToken token)
        { var value = token as JObject; if (value == null) throw new FormatException("Expected an object."); return value; }
        private static JArray Array(JToken token, int maximum)
        { var value = token as JArray; if (value == null || value.Count > maximum) throw new FormatException("Invalid array length."); return value; }
        private static int Integer(JToken token)
        { if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected an integer token."); return checked((int)token); }
        private static uint UnsignedInteger(JToken token)
        { if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected an unsigned integer token."); return checked((uint)token); }
        private static string Text(JToken token)
        { if (token == null || token.Type != JTokenType.String) throw new FormatException("Expected a string token."); return (string)token; }
        private static bool Boolean(JToken token)
        { if (token == null || token.Type != JTokenType.Boolean) throw new FormatException("Expected a boolean token."); return (bool)token; }
        private static double Number(JToken token)
        {
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) throw new FormatException("Expected a numeric token.");
            double value = (double)token;
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new FormatException("Expected a finite number.");
            return value;
        }
        private static string Decimal(long value) { return value.ToString(CultureInfo.InvariantCulture); }
        private static long DecimalInteger(JToken token)
        {
            string text = Text(token);
            if (text.Length == 0) throw new FormatException("Empty decimal string.");
            foreach (char c in text) if (c < '0' || c > '9') throw new FormatException("Expected unsigned decimal digits.");
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) throw new FormatException("Decimal value exceeds Int64.");
            return value;
        }

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
            private void Literal(string value)
            { foreach (char c in value) if (!Take(c)) Invalid(); }
            private bool Take(char c) { if (position >= text.Length || text[position] != c) return false; position++; return true; }
            private void Space() { while (position < text.Length && (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' || text[position] == '\n')) position++; }
            private static void Invalid() { throw new FormatException("Save file is not strict JSON or exceeds the depth limit."); }
        }
    }
}
