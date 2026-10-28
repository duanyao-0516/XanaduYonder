using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Zuma;

namespace XanaduYonder.Runtime.Zuma
{
    // Explicit token contracts intentionally do not use serializer coercion or field defaults.
    public static class ZumaSaveCodec
    {
        public const int MaximumLength = 256 * 1024;
        public const int MaximumDepth = 12;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static string Encode(ZumaSaveData data, Func<string, ZumaLevelDefinition> resolver = null)
        {
            if (data == null) throw new ArgumentException("Missing Zuma save data.", nameof(data));
            var copy = data.Copy();
            if (!copy.TryValidate(resolver, out var reason)) throw new ArgumentException(reason, nameof(data));
            var records = new JArray();
            foreach (var record in copy.Profile.Records)
                records.Add(new JObject { ["mapId"] = record.MapId, ["difficultyId"] = record.DifficultyId,
                    ["bestScore"] = Decimal(record.BestScore), ["bestTimeSeconds"] = record.BestTimeSeconds });
            var root = new JObject
            {
                ["schemaVersion"] = 1, ["gameId"] = "zuma", ["rulesVersion"] = 1, ["contentVersion"] = 1,
                ["profile"] = new JObject { ["lastMapId"] = copy.Profile.LastMapId,
                    ["lastDifficultyId"] = copy.Profile.LastDifficultyId, ["records"] = records },
                ["session"] = copy.Session == null ? JValue.CreateNull() : (JToken)WriteSnapshot(copy.Session)
            };
            var json = root.ToString(Formatting.None);
            if (StrictUtf8.GetByteCount(json) > MaximumLength) throw new ArgumentException("Zuma save exceeds its byte limit.", nameof(data));
            return json;
        }

        public static bool TryDecode(string json, out ZumaSaveData data, out string reason)
        {
            var result = Decode(json);
            data = result.Data;
            reason = result.Warning;
            return result.Status == ZumaLoadStatus.Loaded;
        }

        public static ZumaLoadResult Decode(string json, Func<string, ZumaLevelDefinition> resolver = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Missing Zuma save.");
                // A future version can move its version fields beyond the current size limit.
                // Without a complete bounded document, compatibility cannot be established.
                if (json.Length > MaximumLength || StrictUtf8.GetByteCount(json) > MaximumLength) return UnverifiableSize();
                // The shape/depth limits below belong to schema 1. Recognize a future root before
                // applying those limits, while keeping the entire inspection within the byte bound.
                if (HasUnsupportedVersion(json)) return Unsupported();
                new JsonSyntax(json).Check();
                JObject root;
                using (var text = new StringReader(json))
                using (var reader = new StrictReader(text) { MaxDepth = MaximumDepth, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException("Trailing data after Zuma save.");
                }
                // Inspect versions before requiring current-version fields. Future documents must never
                // be treated as repairable corruption just because their shape has changed.
                if (Version(root["schemaVersion"]) || Version(root["rulesVersion"]) || Version(root["contentVersion"]))
                    return Unsupported();
                Fields(root, "schemaVersion", "gameId", "rulesVersion", "contentVersion", "profile", "session");
                if (Text(root["gameId"]) != "zuma") throw new FormatException("Wrong save game identity.");
                var profile = Object(root["profile"]);
                Fields(profile, "lastMapId", "lastDifficultyId", "records");
                var records = Array(profile["records"], 72);
                if (records.Count != 72) throw new FormatException("Expected exactly 72 Zuma records.");
                var data = new ZumaSaveData { Profile = new ZumaProfile { LastMapId = Text(profile["lastMapId"]),
                    LastDifficultyId = Text(profile["lastDifficultyId"]), Records = new ZumaRecord[72] } };
                for (var i = 0; i < records.Count; i++)
                {
                    var record = Object(records[i]);
                    Fields(record, "mapId", "difficultyId", "bestScore", "bestTimeSeconds");
                    data.Profile.Records[i] = new ZumaRecord { MapId = Text(record["mapId"]), DifficultyId = Text(record["difficultyId"]),
                        BestScore = Long(record["bestScore"]), BestTimeSeconds = Integer(record["bestTimeSeconds"]) };
                }
                if (root["session"].Type != JTokenType.Null)
                {
                    var session = Object(root["session"]);
                    if (Version(session["rulesVersion"]) || Version(session["contentVersion"])) return Unsupported();
                    data.Session = ReadSnapshot(session);
                }
                if (!data.TryValidate(resolver, out var reason)) throw new FormatException(reason);
                return new ZumaLoadResult { Status = ZumaLoadStatus.Loaded, Data = data };
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException || exception is OverflowException || exception is ArgumentException)
            {
                return new ZumaLoadResult { Status = ZumaLoadStatus.Corrupt, Warning = "Zuma存档内容无效：" + exception.Message };
            }
        }
        internal static bool HasUnsupportedVersion(string prefix)
        {
            try
            {
                using (var text = new StringReader(prefix))
                using (var reader = new JsonTextReader(text) { MaxDepth = null, DateParseHandling = DateParseHandling.None })
                {
                    if (!reader.Read() || reader.TokenType != JsonToken.StartObject) return false;
                    while (reader.Read())
                    {
                        if (reader.Depth == 0) return false;
                        if (reader.Depth != 1 || reader.TokenType != JsonToken.PropertyName) continue;
                        var name = (string)reader.Value;
                        if (!reader.Read()) return false;
                        if ((name == "schemaVersion" || name == "rulesVersion" || name == "contentVersion") && reader.TokenType == JsonToken.Integer)
                        {
                            // Compare the raw integer text without narrowing a future version to Int32.
                            var value = Convert.ToString(reader.Value, CultureInfo.InvariantCulture);
                            if (value != "1" && value.Length > 0 && value[0] >= '1' && value[0] <= '9') return true;
                        }
                        reader.Skip();
                    }
                }
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException || exception is OverflowException)
            { /* The strict current-version decoder below reports malformed or incomplete input. */ }
            return false;
        }
        private static ZumaLoadResult Unsupported()
        { return new ZumaLoadResult { Status = ZumaLoadStatus.UnsupportedVersion, Warning = "Zuma存档由不兼容的版本创建，请使用兼容版本打开。原存档已保护。" }; }
        internal static ZumaLoadResult UnverifiableSize()
        {
            return new ZumaLoadResult { Status = ZumaLoadStatus.UnsupportedVersion,
                Warning = "Zuma 存档超过当前可验证的容量，无法确认版本兼容性。原文件已保护，请使用兼容版本打开。" };
        }
        private static bool Version(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected a positive integer Zuma version.");
            // Json.NET represents integers beyond Int64 as BigInteger. Decide compatibility before
            // narrowing any version, for both the root and the embedded session contracts.
            var value = Convert.ToString(((JValue)token).Value, CultureInfo.InvariantCulture);
            if (value == "1") return false;
            if (string.IsNullOrEmpty(value) || value[0] < '1' || value[0] > '9') throw new FormatException("Invalid Zuma version.");
            return true;
        }
        private static JObject WriteSnapshot(ZumaSnapshot value)
        {
            var balls = new JArray();
            foreach (var item in value.Balls) balls.Add(WriteBallSnapshot(item));
            var bullets = new JArray();
            foreach (var item in value.Bullets) bullets.Add(WriteBulletSnapshot(item));
            var trailparticles = new JArray();
            foreach (var item in value.TrailParticles) trailparticles.Add(WriteTrailParticleSnapshot(item));
            return new JObject
            {
                ["rulesVersion"] = value.RulesVersion,
                ["contentVersion"] = value.ContentVersion,
                ["sessionId"] = value.SessionId,
                ["mapId"] = value.MapId,
                ["difficultyId"] = value.DifficultyId,
                ["phase"] = value.Phase,
                ["randomAlgorithm"] = value.RandomAlgorithm,
                ["randomState"] = value.RandomState,
                ["score"] = Decimal(value.Score),
                ["totalTicks"] = Decimal(value.TotalTicks),
                ["playTicks"] = Decimal(value.PlayTicks),
                ["revision"] = Decimal(value.Revision),
                ["aceTimeSeconds"] = value.AceTimeSeconds,
                ["chainBonus"] = value.ChainBonus,
                ["maxChainBonus"] = value.MaxChainBonus,
                ["maxCombo"] = value.MaxCombo,
                ["totalCombos"] = value.TotalCombos,
                ["totalCoins"] = value.TotalCoins,
                ["glowTicks"] = value.GlowTicks,
                ["generating"] = value.Generating,
                ["initialRoll"] = value.InitialRoll,
                ["endReached"] = value.EndReached,
                ["introInitialized"] = value.IntroInitialized,
                ["outroInitialized"] = value.OutroInitialized,
                ["resultCommitted"] = value.ResultCommitted,
                ["resultAcknowledged"] = value.ResultAcknowledged,
                ["chainSpeed"] = value.ChainSpeed,
                ["lastClearedPosition"] = value.LastClearedPosition,
                ["introPosition"] = value.IntroPosition,
                ["introPreviousPosition"] = value.IntroPreviousPosition,
                ["outroPosition"] = value.OutroPosition,
                ["outroPreviousPosition"] = value.OutroPreviousPosition,
                ["capacityRejections"] = value.CapacityRejections,
                ["frog"] = WriteFrogSnapshot(value.Frog),
                ["coin"] = WriteCoinSnapshot(value.Coin),
                ["balls"] = balls,
                ["bullets"] = bullets,
                ["trailParticles"] = trailparticles,
            };
        }
        private static ZumaSnapshot ReadSnapshot(JObject value)
        {
            Fields(value, "rulesVersion", "contentVersion", "sessionId", "mapId", "difficultyId", "phase", "randomAlgorithm", "randomState", "score", "totalTicks", "playTicks", "revision", "aceTimeSeconds", "chainBonus", "maxChainBonus", "maxCombo", "totalCombos", "totalCoins", "glowTicks", "generating", "initialRoll", "endReached", "introInitialized", "outroInitialized", "resultCommitted", "resultAcknowledged", "chainSpeed", "lastClearedPosition", "introPosition", "introPreviousPosition", "outroPosition", "outroPreviousPosition", "capacityRejections", "frog", "coin", "balls", "bullets", "trailParticles");
            var ballsTokens = Array(value["balls"], 256);
            var balls = new ZumaBallSnapshot[ballsTokens.Count];
            for (var i = 0; i < balls.Length; i++) balls[i] = ReadBallSnapshot(Object(ballsTokens[i]));
            var bulletsTokens = Array(value["bullets"], 32);
            var bullets = new ZumaBulletSnapshot[bulletsTokens.Count];
            for (var i = 0; i < bullets.Length; i++) bullets[i] = ReadBulletSnapshot(Object(bulletsTokens[i]));
            var trailparticlesTokens = Array(value["trailParticles"], 256);
            var trailparticles = new ZumaTrailParticleSnapshot[trailparticlesTokens.Count];
            for (var i = 0; i < trailparticles.Length; i++) trailparticles[i] = ReadTrailParticleSnapshot(Object(trailparticlesTokens[i]));
            return new ZumaSnapshot
            {
                RulesVersion = Integer(value["rulesVersion"]),
                ContentVersion = Integer(value["contentVersion"]),
                SessionId = Text(value["sessionId"]),
                MapId = Text(value["mapId"]),
                DifficultyId = Text(value["difficultyId"]),
                Phase = Text(value["phase"]),
                RandomAlgorithm = Text(value["randomAlgorithm"]),
                RandomState = Unsigned(value["randomState"]),
                Score = Long(value["score"]),
                TotalTicks = Long(value["totalTicks"]),
                PlayTicks = Long(value["playTicks"]),
                Revision = Long(value["revision"]),
                AceTimeSeconds = Integer(value["aceTimeSeconds"]),
                ChainBonus = Integer(value["chainBonus"]),
                MaxChainBonus = Integer(value["maxChainBonus"]),
                MaxCombo = Integer(value["maxCombo"]),
                TotalCombos = Integer(value["totalCombos"]),
                TotalCoins = Integer(value["totalCoins"]),
                GlowTicks = Integer(value["glowTicks"]),
                Generating = Boolean(value["generating"]),
                InitialRoll = Boolean(value["initialRoll"]),
                EndReached = Boolean(value["endReached"]),
                IntroInitialized = Boolean(value["introInitialized"]),
                OutroInitialized = Boolean(value["outroInitialized"]),
                ResultCommitted = Boolean(value["resultCommitted"]),
                ResultAcknowledged = Boolean(value["resultAcknowledged"]),
                ChainSpeed = Number(value["chainSpeed"]),
                LastClearedPosition = Number(value["lastClearedPosition"]),
                IntroPosition = Number(value["introPosition"]),
                IntroPreviousPosition = Number(value["introPreviousPosition"]),
                OutroPosition = Number(value["outroPosition"]),
                OutroPreviousPosition = Number(value["outroPreviousPosition"]),
                CapacityRejections = Integer(value["capacityRejections"]),
                Frog = ReadFrogSnapshot(Object(value["frog"])),
                Coin = ReadCoinSnapshot(Object(value["coin"])),
                Balls = balls,
                Bullets = bullets,
                TrailParticles = trailparticles,
            };
        }
        private static JObject WriteBallSnapshot(ZumaBallSnapshot value)
        {
            return new JObject
            {
                ["pathPosition"] = value.PathPosition,
                ["x"] = value.X,
                ["y"] = value.Y,
                ["angle"] = value.Angle,
                ["speed"] = value.Speed,
                ["color"] = value.Color,
                ["combo"] = value.Combo,
                ["explosionTicks"] = value.ExplosionTicks,
                ["fadeTicks"] = value.FadeTicks,
                ["inserted"] = value.Inserted,
                ["exploding"] = value.Exploding,
                ["isSingle"] = value.IsSingle,
                ["goBack"] = value.GoBack,
                ["hidden"] = value.Hidden,
                ["drawPriority"] = value.DrawPriority,
            };
        }
        private static ZumaBallSnapshot ReadBallSnapshot(JObject value)
        {
            Fields(value, "pathPosition", "x", "y", "angle", "speed", "color", "combo", "explosionTicks", "fadeTicks", "inserted", "exploding", "isSingle", "goBack", "hidden", "drawPriority");
            return new ZumaBallSnapshot
            {
                PathPosition = Number(value["pathPosition"]),
                X = Number(value["x"]),
                Y = Number(value["y"]),
                Angle = Number(value["angle"]),
                Speed = Number(value["speed"]),
                Color = Integer(value["color"]),
                Combo = Integer(value["combo"]),
                ExplosionTicks = Integer(value["explosionTicks"]),
                FadeTicks = Integer(value["fadeTicks"]),
                Inserted = Boolean(value["inserted"]),
                Exploding = Boolean(value["exploding"]),
                IsSingle = Boolean(value["isSingle"]),
                GoBack = Boolean(value["goBack"]),
                Hidden = Boolean(value["hidden"]),
                DrawPriority = Boolean(value["drawPriority"]),
            };
        }
        private static JObject WriteBulletSnapshot(ZumaBulletSnapshot value)
        {
            return new JObject
            {
                ["x"] = value.X,
                ["y"] = value.Y,
                ["directionX"] = value.DirectionX,
                ["directionY"] = value.DirectionY,
                ["color"] = value.Color,
                ["slot"] = value.Slot,
            };
        }
        private static ZumaBulletSnapshot ReadBulletSnapshot(JObject value)
        {
            Fields(value, "x", "y", "directionX", "directionY", "color", "slot");
            return new ZumaBulletSnapshot
            {
                X = Number(value["x"]),
                Y = Number(value["y"]),
                DirectionX = Number(value["directionX"]),
                DirectionY = Number(value["directionY"]),
                Color = Integer(value["color"]),
                Slot = Integer(value["slot"]),
            };
        }
        private static JObject WriteFrogSnapshot(ZumaFrogSnapshot value)
        {
            return new JObject
            {
                ["currentColor"] = value.CurrentColor,
                ["nextColor"] = value.NextColor,
                ["recoilTicks"] = value.RecoilTicks,
                ["directionX"] = value.DirectionX,
                ["directionY"] = value.DirectionY,
            };
        }
        private static ZumaFrogSnapshot ReadFrogSnapshot(JObject value)
        {
            Fields(value, "currentColor", "nextColor", "recoilTicks", "directionX", "directionY");
            return new ZumaFrogSnapshot
            {
                CurrentColor = Integer(value["currentColor"]),
                NextColor = Integer(value["nextColor"]),
                RecoilTicks = Integer(value["recoilTicks"]),
                DirectionX = Number(value["directionX"]),
                DirectionY = Number(value["directionY"]),
            };
        }
        private static JObject WriteCoinSnapshot(ZumaCoinSnapshot value)
        {
            return new JObject
            {
                ["active"] = value.Active,
                ["blinking"] = value.Blinking,
                ["positionIndex"] = value.PositionIndex,
                ["timerTicks"] = value.TimerTicks,
                ["fadeTicks"] = value.FadeTicks,
            };
        }
        private static ZumaCoinSnapshot ReadCoinSnapshot(JObject value)
        {
            Fields(value, "active", "blinking", "positionIndex", "timerTicks", "fadeTicks");
            return new ZumaCoinSnapshot
            {
                Active = Boolean(value["active"]),
                Blinking = Boolean(value["blinking"]),
                PositionIndex = Integer(value["positionIndex"]),
                TimerTicks = Integer(value["timerTicks"]),
                FadeTicks = Integer(value["fadeTicks"]),
            };
        }
        private static JObject WriteTrailParticleSnapshot(ZumaTrailParticleSnapshot value)
        {
            return new JObject
            {
                ["x"] = value.X,
                ["y"] = value.Y,
                ["ageTicks"] = value.AgeTicks,
            };
        }
        private static ZumaTrailParticleSnapshot ReadTrailParticleSnapshot(JObject value)
        {
            Fields(value, "x", "y", "ageTicks");
            return new ZumaTrailParticleSnapshot
            {
                X = Number(value["x"]),
                Y = Number(value["y"]),
                AgeTicks = Integer(value["ageTicks"]),
            };
        }
        private static void Fields(JObject value, params string[] fields)
        {
            if (value.Count != fields.Length) throw new FormatException("Unexpected Zuma object fields.");
            foreach (var field in fields) if (value.Property(field) == null) throw new FormatException("Missing Zuma field: " + field);
        }
        private static JObject Object(JToken token)
        { if (!(token is JObject value)) throw new FormatException("Expected an object."); return value; }
        private static JArray Array(JToken token, int capacity)
        { if (!(token is JArray value) || value.Count > capacity) throw new FormatException("Invalid or oversized array."); return value; }
        private static int Integer(JToken token)
        { if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected Int32 JSON integer."); return checked((int)token); }
        private static uint Unsigned(JToken token)
        { if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected UInt32 JSON integer."); return checked((uint)token); }
        private static string Text(JToken token)
        { if (token == null || token.Type != JTokenType.String) throw new FormatException("Expected string."); return (string)token; }
        private static bool Boolean(JToken token)
        { if (token == null || token.Type != JTokenType.Boolean) throw new FormatException("Expected boolean."); return (bool)token; }
        private static float Number(JToken token)
        {
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)) throw new FormatException("Expected finite number.");
            var value = (float)token;
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new FormatException("Expected finite number.");
            return value;
        }
        private static long Long(JToken token)
        {
            var text = Text(token);
            if (text.Length == 0 || text.Length > 19) throw new FormatException("Expected nonnegative Int64 decimal string.");
            foreach (var character in text) if (character < '0' || character > '9') throw new FormatException("Expected nonnegative Int64 decimal string.");
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) throw new FormatException("Int64 decimal string overflow.");
            return value;
        }
        private static string Decimal(long value) { return value.ToString(CultureInfo.InvariantCulture); }
        // Json.NET deliberately accepts JavaScript extensions (hex numbers and trailing commas).
        // Validate the RFC JSON grammar before constructing tokens so disk corruption is never coerced.
        private sealed class JsonSyntax
        {
            private readonly string text;
            private int index;
            public JsonSyntax(string text) { this.text = text; }
            public void Check() { Value(0); Space(); if (index != text.Length) Fail(); }
            private void Value(int depth)
            {
                if (depth > MaximumDepth) Fail();
                Space();
                if (index >= text.Length) Fail();
                var c = text[index];
                if (c == '{')
                {
                    index++; Space(); if (Take('}')) return;
                    do { Space(); Quoted(); Space(); Need(':'); Value(depth + 1); Space(); if (Take('}')) return; Need(','); } while (true);
                }
                if (c == '[')
                {
                    index++; Space(); if (Take(']')) return;
                    do { Value(depth + 1); Space(); if (Take(']')) return; Need(','); } while (true);
                }
                if (c == '"') { Quoted(); return; }
                if (c == 't') { Literal("true"); return; }
                if (c == 'f') { Literal("false"); return; }
                if (c == 'n') { Literal("null"); return; }
                Take('-');
                if (!Take('0')) { if (index >= text.Length || text[index] < '1' || text[index] > '9') Fail(); Digits(); }
                if (Take('.')) { if (!Digit()) Fail(); Digits(); }
                if (Take('e') || Take('E')) { if (!Take('+')) Take('-'); if (!Digit()) Fail(); Digits(); }
            }
            private void Quoted()
            {
                Need('"');
                while (index < text.Length)
                {
                    var c = text[index++];
                    if (c == '"') return;
                    if (c < 0x20) Fail();
                    if (c != '\\') continue;
                    if (index >= text.Length) Fail();
                    c = text[index++];
                    if (c == 'u')
                    {
                        for (var i = 0; i < 4; i++)
                        {
                            if (index >= text.Length) Fail();
                            c = text[index++];
                            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) Fail();
                        }
                    }
                    else if (c != '"' && c != '\\' && c != '/' && c != 'b' && c != 'f' && c != 'n' && c != 'r' && c != 't') Fail();
                }
                Fail();
            }
            private void Literal(string value) { foreach (var c in value) Need(c); }
            private void Space() { while (index < text.Length && (text[index] == ' ' || text[index] == '\t' || text[index] == '\r' || text[index] == '\n')) index++; }
            private bool Digit() { return index < text.Length && text[index] >= '0' && text[index] <= '9'; }
            private void Digits() { while (Digit()) index++; }
            private bool Take(char c) { if (index < text.Length && text[index] == c) { index++; return true; } return false; }
            private void Need(char c) { if (!Take(c)) Fail(); }
            private static void Fail() { throw new FormatException("Malformed JSON syntax in Zuma save."); }
        }
        private sealed class StrictReader : JsonTextReader
        {
            public StrictReader(TextReader reader) : base(reader) { }
            public override bool Read()
            {
                var result = base.Read();
                if (result && (TokenType == JsonToken.Comment || TokenType == JsonToken.Undefined || TokenType == JsonToken.StartConstructor ||
                    ((TokenType == JsonToken.String || TokenType == JsonToken.PropertyName) && QuoteChar != '"')))
                    throw new FormatException("Non-JSON syntax in Zuma save.");
                return result;
            }
        }
    }
}
