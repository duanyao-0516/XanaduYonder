using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.GoldMiner;

namespace XanaduYonder.Runtime.GoldMiner
{
    /// <summary>The version-one disk boundary. No serializer defaults or token coercion are used.</summary>
    public static class GoldMinerSaveCodec
    {
        public const int MaximumLength = 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        // An explicit allowlist, deliberately independent of the set of fields a future model may add.
        // Reflection only reads/writes these public DTO fields; it never instantiates a type from JSON.
        private static readonly Dictionary<Type, FieldInfo[]> Schemas = new Dictionary<Type, FieldInfo[]> {
            { typeof(GoldMinerRecord), Fields<GoldMinerRecord>("BestClearedLevel BestRunGrossIncome CompletionCount") },
            { typeof(GoldMinerRunSnapshot), Fields<GoldMinerRunSnapshot>("RunId Mode RunSeed AttemptIndex LastCommandSeq Current Checkpoint") },
            { typeof(GoldMinerState), Fields<GoldMinerState>("Phase LevelIndex LayoutProfileId LayoutId CatalogVersion LayoutContentHash Cash Target ShopSpent RemainingTicks SimulationTick LayoutRng BagRng PatternId PreviousPatternId LevelLayoutSeed Players Hooks Entities ActiveBuffs PendingBuffs Shop Settlement") },
            { typeof(GoldMinerLevelStart), Fields<GoldMinerLevelStart>("LevelIndex LayoutProfileId LayoutId CatalogVersion LayoutContentHash PatternId PreviousPatternId LevelLayoutSeed OpeningCash OpeningShopSpent OpeningPlayers ActiveBuffs InitialHooks InitialEntities LayoutRng BagRng") },
            { typeof(GoldMinerPlayer), Fields<GoldMinerPlayer>("PlayerId Bombs TotalCollected LevelCollected") },
            { typeof(GoldMinerOpeningPlayer), Fields<GoldMinerOpeningPlayer>("PlayerId Bombs TotalCollected") },
            { typeof(GoldMinerHook), Fields<GoldMinerHook>("PlayerId State AngleRadians SwingDirection Length HeldEntityId") },
            { typeof(GoldMinerEntity), Fields<GoldMinerEntity>("Id Kind Status X Y MoveDirection BagValue OwnerPlayerId") },
            { typeof(GoldMinerBuffs), Fields<GoldMinerBuffs>("Strength Clover RockBook DiamondOil") },
            { typeof(GoldMinerShop), Fields<GoldMinerShop>("Id EntryCash EntryShopSpent EntryBombs Spent Offers") },
            { typeof(GoldMinerOffer), Fields<GoldMinerOffer>("ItemId Price Remaining Purchased Allocations") },
            { typeof(GoldMinerBombStock), Fields<GoldMinerBombStock>("PlayerId Bombs") },
            { typeof(GoldMinerAllocation), Fields<GoldMinerAllocation>("PlayerId Count") },
            { typeof(GoldMinerSettlement), Fields<GoldMinerSettlement>("Id Outcome Cash Target Contributions RunGrossIncome RecordCommitted") },
            { typeof(GoldMinerContribution), Fields<GoldMinerContribution>("PlayerId Amount") }
        };

        public static string Encode(GoldMinerRootSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentException("Missing gold miner snapshot.", nameof(snapshot));
            var value = snapshot.Copy();
            string reason;
            if (!value.TryValidate(out reason)) throw new ArgumentException(reason, nameof(snapshot));
            var root = new JObject {
                ["schemaVersion"] = value.SchemaVersion, ["rulesVersion"] = value.RulesVersion,
                ["gameId"] = value.GameId, ["txnSeq"] = Write(value.TxnSeq), ["lastMode"] = Write(value.LastMode),
                ["records"] = new JObject { ["solo"] = Write(value.SoloRecord), ["coop"] = Write(value.CoopRecord) },
                ["slots"] = new JObject { ["solo"] = Write(value.Solo), ["coop"] = Write(value.Coop) }
            };
            var json = root.ToString(Formatting.None);
            if (Utf8.GetByteCount(json) > MaximumLength) throw new ArgumentException("Gold miner snapshot exceeds 1 MiB.", nameof(snapshot));
            return json;
        }

        public static bool TryDecode(string json, out GoldMinerRootSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            try
            {
                if (string.IsNullOrEmpty(json) || json.Length > MaximumLength || Utf8.GetByteCount(json) > MaximumLength)
                    throw new FormatException("Missing or oversized gold miner snapshot.");
                // Json.NET accepts extensions such as comments, trailing commas and single quotes.
                // Check standard JSON grammar before using it to materialize tokens.
                new JsonGrammar(json).Validate();
                JObject root;
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { MaxDepth = 12, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException("Trailing gold miner data.");
                }
                Require(root, "schemaVersion rulesVersion gameId txnSeq lastMode records slots");
                var records = Object(root["records"]); Require(records, "solo coop");
                var slots = Object(root["slots"]); Require(slots, "solo coop");
                var candidate = new GoldMinerRootSnapshot {
                    SchemaVersion = (int)Read(root["schemaVersion"], typeof(int)),
                    RulesVersion = (int)Read(root["rulesVersion"], typeof(int)),
                    GameId = (string)Read(root["gameId"], typeof(string)),
                    TxnSeq = (long)Read(root["txnSeq"], typeof(long)),
                    LastMode = (GoldMinerMode)Read(root["lastMode"], typeof(GoldMinerMode)),
                    SoloRecord = (GoldMinerRecord)Read(records["solo"], typeof(GoldMinerRecord)),
                    CoopRecord = (GoldMinerRecord)Read(records["coop"], typeof(GoldMinerRecord)),
                    Solo = (GoldMinerRunSnapshot)Read(slots["solo"], typeof(GoldMinerRunSnapshot), true),
                    Coop = (GoldMinerRunSnapshot)Read(slots["coop"], typeof(GoldMinerRunSnapshot), true)
                };
                if (!candidate.TryValidate(out error)) return false;
                snapshot = candidate;
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException ||
                exception is OverflowException || exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        private static FieldInfo[] Fields<T>(string names)
        {
            var split = names.Split(' '); var result = new FieldInfo[split.Length];
            for (var i = 0; i < split.Length; i++)
                result[i] = typeof(T).GetField(split[i], BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new InvalidOperationException("Gold miner schema field missing: " + split[i]);
            return result;
        }
        private static string Camel(string name) { return char.ToLowerInvariant(name[0]) + name.Substring(1); }
        private static bool CanBeNull(FieldInfo field)
        {
            return Nullable.GetUnderlyingType(field.FieldType) != null || field.Name == "PreviousPatternId" ||
                field.FieldType == typeof(GoldMinerShop) || field.FieldType == typeof(GoldMinerSettlement) ||
                (field.DeclaringType == typeof(GoldMinerOffer) && field.Name == "Allocations");
        }
        private static JToken Write(object value)
        {
            if (value == null) return JValue.CreateNull();
            var type = value.GetType();
            if (type == typeof(long)) return new JValue(((long)value).ToString(CultureInfo.InvariantCulture));
            if (type.IsEnum) return new JValue(Camel(Enum.GetName(type, value) ?? throw new ArgumentException("Invalid gold miner enum.")));
            if (type == typeof(string) || type == typeof(bool) || type == typeof(int) || type == typeof(uint) || type == typeof(double)) return new JValue(value);
            if (type.IsArray)
            {
                var result = new JArray(); foreach (var item in (Array)value) result.Add(Write(item)); return result;
            }
            FieldInfo[] fields;
            if (!Schemas.TryGetValue(type, out fields)) throw new ArgumentException("Unsupported gold miner model type.");
            var obj = new JObject(); foreach (var field in fields) obj.Add(Camel(field.Name), Write(field.GetValue(value))); return obj;
        }
        private static object Read(JToken token, Type type, bool nullable = false)
        {
            if (token == null) throw new FormatException("Missing gold miner field.");
            if (token.Type == JTokenType.Null)
            {
                if (!nullable) throw new FormatException("Unexpected null gold miner field.");
                return null;
            }
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(long))
            {
                if (token.Type != JTokenType.String) throw new FormatException("Amounts and sequences must be decimal strings.");
                var text = (string)token;
                if (text.Length == 0 || text.Length > 19 || (text.Length > 1 && text[0] == '0')) throw new FormatException("Noncanonical decimal string.");
                foreach (var c in text) if (c < '0' || c > '9') throw new FormatException("Noncanonical decimal string.");
                return long.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
            }
            if (type == typeof(int) || type == typeof(uint))
            {
                if (token.Type != JTokenType.Integer) throw new FormatException("Expected an integer token.");
                if (type == typeof(int)) return checked((int)token);
                return checked((uint)token);
            }
            if (type == typeof(double))
            {
                if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float) throw new FormatException("Expected a numeric token.");
                var number = (double)token;
                if (double.IsNaN(number) || double.IsInfinity(number)) throw new FormatException("Expected a finite number.");
                return number;
            }
            if (type == typeof(string) || type.IsEnum)
            {
                if (token.Type != JTokenType.String) throw new FormatException("Expected a string token.");
                var text = (string)token;
                if (type == typeof(string)) return text;
                foreach (var value in Enum.GetValues(type)) if (Camel(value.ToString()) == text) return value;
                throw new FormatException("Unknown gold miner enum value.");
            }
            if (type == typeof(bool))
            {
                if (token.Type != JTokenType.Boolean) throw new FormatException("Expected a boolean token.");
                return (bool)token;
            }
            if (type.IsArray)
            {
                var array = token as JArray; var element = type.GetElementType();
                var limit = element == typeof(GoldMinerEntity) ? 128 : element == typeof(GoldMinerOffer) ? 5 : 2;
                if (array == null || array.Count > limit) throw new FormatException("Invalid or oversized gold miner array.");
                var result = Array.CreateInstance(element, array.Count);
                for (var i = 0; i < array.Count; i++) result.SetValue(Read(array[i], element), i);
                return result;
            }
            FieldInfo[] fields;
            if (!Schemas.TryGetValue(type, out fields)) throw new FormatException("Unsupported gold miner model type.");
            var obj = Object(token);
            if (obj.Count != fields.Length) throw new FormatException("Unexpected gold miner fields.");
            var instance = Activator.CreateInstance(type);
            foreach (var field in fields)
            {
                var name = Camel(field.Name);
                if (obj.Property(name) == null) throw new FormatException("Missing gold miner field: " + name);
                field.SetValue(instance, Read(obj[name], field.FieldType, CanBeNull(field)));
            }
            return instance;
        }
        private static JObject Object(JToken token) { return token as JObject ?? throw new FormatException("Expected an object token."); }
        private static void Require(JObject value, string names)
        {
            var fields = names.Split(' ');
            if (value.Count != fields.Length) throw new FormatException("Unexpected gold miner fields.");
            foreach (var name in fields) if (value.Property(name) == null) throw new FormatException("Missing gold miner field: " + name);
        }

        private sealed class JsonGrammar
        {
            private readonly string text; private int index;
            internal JsonGrammar(string text) { this.text = text; }
            internal void Validate() { Value(0); Space(); if (index != text.Length) Bad(); }
            private void Bad() { throw new FormatException("Invalid JSON syntax at character " + index + "."); }
            private void Space() { while (index < text.Length && (text[index] == ' ' || text[index] == '\t' || text[index] == '\r' || text[index] == '\n')) index++; }
            private bool Take(char c) { Space(); if (index < text.Length && text[index] == c) { index++; return true; } return false; }
            private void Need(char c) { if (!Take(c)) Bad(); }
            private void Value(int depth)
            {
                Space(); if (index == text.Length) { Bad(); return; }
                var c = text[index];
                if (c == '{' || c == '[')
                {
                    if (depth == 12) Bad(); index++;
                    var end = c == '{' ? '}' : ']'; if (Take(end)) return;
                    do { if (c == '{') { String(); Need(':'); } Value(depth + 1); if (Take(end)) return; Need(','); } while (true);
                }
                if (c == '"') { String(); return; }
                if (c == 't') { Literal("true"); return; }
                if (c == 'f') { Literal("false"); return; }
                if (c == 'n') { Literal("null"); return; }
                if (c == '-') index++;
                if (index >= text.Length) { Bad(); return; }
                if (text[index] == '0') index++;
                else { if (text[index] < '1' || text[index] > '9') Bad(); Digits(); }
                if (index < text.Length && text[index] == '.') { index++; RequiredDigits(); }
                if (index < text.Length && (text[index] == 'e' || text[index] == 'E')) { index++; if (index < text.Length && (text[index] == '+' || text[index] == '-')) index++; RequiredDigits(); }
            }
            private void Digits() { while (index < text.Length && text[index] >= '0' && text[index] <= '9') index++; }
            private void RequiredDigits() { var start = index; Digits(); if (index == start) Bad(); }
            private void Literal(string word) { for (var i = 0; i < word.Length; i++) if (index >= text.Length || text[index++] != word[i]) Bad(); }
            private int Hex()
            {
                var number = 0;
                for (var i = 0; i < 4; i++) { if (index >= text.Length) Bad(); var c = text[index++]; var digit = c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1; if (digit < 0) Bad(); number = number * 16 + digit; }
                return number;
            }
            private void String()
            {
                Need('"');
                while (index < text.Length)
                {
                    var c = text[index++]; if (c == '"') return; if (c < 32) Bad();
                    if (c != '\\') continue;
                    if (index >= text.Length) Bad(); c = text[index++];
                    if (c == 'u')
                    {
                        var code = Hex();
                        if (code >= 0xd800 && code <= 0xdbff)
                        {
                            if (index + 1 >= text.Length || text[index++] != '\\' || text[index++] != 'u') Bad();
                            var low = Hex(); if (low < 0xdc00 || low > 0xdfff) Bad();
                        }
                        else if (code >= 0xdc00 && code <= 0xdfff) Bad();
                    }
                    else if (c != '"' && c != '\\' && c != '/' && c != 'b' && c != 'f' && c != 'n' && c != 'r' && c != 't') Bad();
                }
                Bad();
            }
        }
    }
}
