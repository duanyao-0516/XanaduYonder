using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.GoldMiner;

namespace XanaduYonder.Runtime.GoldMiner
{
    /// <summary>Loads only shipped, verified layouts. Selection never mutates the caller's run.</summary>
    public sealed class GoldMinerLevelCatalog
    {
        public const int MaximumCatalogBytes = 64 * 1024;
        public const int MaximumShardBytes = 512 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly string[] Patterns = { "balanced", "left", "right" };
        private static readonly string[] Kinds = {
            "goldSmall", "goldMedium", "goldLarge", "rockSmall", "rockLarge",
            "diamond", "mysteryBag", "mole", "diamondMole"
        };
        private static readonly GoldMinerKind[] InitialKindOrder = {
            GoldMinerKind.RockLarge, GoldMinerKind.GoldLarge, GoldMinerKind.GoldMedium,
            GoldMinerKind.RockSmall, GoldMinerKind.GoldSmall, GoldMinerKind.Diamond,
            GoldMinerKind.MysteryBag, GoldMinerKind.Mole, GoldMinerKind.DiamondMole
        };
        private readonly Func<string, string> loader;
        private Dictionary<string, string> shardHashes;

        private sealed class Layout
        {
            internal string Id, Pattern;
            internal GoldMinerEntity[] Entities;
        }

        public GoldMinerLevelCatalog(Func<string, string> loader)
        {
            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        public GoldMinerLevelStart CreateStart(GoldMinerMode mode, int levelIndex, uint runSeed,
            GoldMinerRunSnapshot previous = null)
        {
            if (!GoldMinerRules.ValidMode(mode) || levelIndex < 1 || levelIndex > GoldMinerRules.LevelCount || runSeed == 0)
                throw new ArgumentException("Invalid gold miner level request.");
            string reason;
            if (previous == null ? levelIndex != 1 : previous.Mode != mode || previous.RunSeed != runSeed ||
                previous.Current == null || previous.Current.Phase != GoldMinerPhase.Shop ||
                previous.Current.LevelIndex + 1 != levelIndex || !previous.TryValidate(out reason))
                throw new ArgumentException("The next level requires a valid matching shop snapshot.");

            // All resource validation happens before the private RNG copies are advanced.
            string file = GoldMinerRules.ModeId(mode) + "-" + levelIndex.ToString("00", CultureInfo.InvariantCulture);
            if (shardHashes == null) shardHashes = LoadCatalog();
            string hash = shardHashes[file + ".json"];
            var layouts = LoadShard(file, hash, mode, levelIndex);
            var prior = previous == null ? null : previous.Current;
            uint initial = runSeed;
            uint layoutRng = prior == null ? GoldMinerRules.NextUInt(ref initial) : prior.LayoutRng;
            uint bagRng = prior == null ? GoldMinerRules.NextUInt(ref initial) : prior.BagRng;
            uint layoutSeed = layoutRng;
            string previousPattern = prior == null ? null : prior.PatternId;
            var candidates = layouts.FindAll(layout => layout.Pattern != previousPattern);
            if (candidates.Count != (prior == null ? 12 : 8))
                throw new InvalidDataException("Invalid gold miner pattern selection pool.");
            var selected = candidates[(int)(GoldMinerRules.NextUInt(ref layoutRng) % (uint)candidates.Count)];
            var buffs = prior == null ? new GoldMinerBuffs() : prior.PendingBuffs.Copy();
            var entities = new GoldMinerEntity[selected.Entities.Length];
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i] = selected.Entities[i].Copy();
                if (entities[i].Kind == GoldMinerKind.MysteryBag)
                    entities[i].BagValue = GoldMinerRules.DrawBag(ref bagRng, buffs.Clover);
            }
            var players = new GoldMinerOpeningPlayer[GoldMinerRules.PlayerCount(mode)];
            for (int i = 0; i < players.Length; i++)
                players[i] = new GoldMinerOpeningPlayer {
                    PlayerId = i, Bombs = prior == null ? 1 : prior.Players[i].Bombs,
                    TotalCollected = prior == null ? 0 : prior.Players[i].TotalCollected
                };
            var start = new GoldMinerLevelStart {
                LevelIndex = levelIndex, CatalogVersion = 1, LayoutProfileId = GoldMinerRules.LayoutProfileId(mode),
                LayoutId = selected.Id, LayoutContentHash = hash, PatternId = selected.Pattern,
                PreviousPatternId = previousPattern, LevelLayoutSeed = layoutSeed, LayoutRng = layoutRng, BagRng = bagRng,
                OpeningCash = prior == null ? 0 : prior.Cash, OpeningShopSpent = prior == null ? 0 : prior.ShopSpent,
                OpeningPlayers = players, ActiveBuffs = buffs, InitialHooks = GoldMinerRules.InitialHooks(mode),
                InitialEntities = entities
            };
            if (!start.TryValidate(mode, out reason))
                throw new InvalidDataException("Invalid selected gold miner checkpoint: " + reason);
            return start;
        }

        private Dictionary<string, string> LoadCatalog()
        {
            var root = ReadObject("GoldMiner/Levels/catalog", MaximumCatalogBytes, null);
            Require(root, "schemaVersion rulesVersion catalogVersion shards");
            Versions(root);
            var shards = Array(root["shards"], 20, 20);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var token in shards)
            {
                var shard = Object(token);
                Require(shard, "mode levelIndex file sha256");
                string mode = String(shard["mode"]);
                if (mode != "solo" && mode != "coop") Fail("Unknown shard mode.");
                int level = Integer(shard["levelIndex"], 1, GoldMinerRules.LevelCount);
                string expected = mode + "-" + level.ToString("00", CultureInfo.InvariantCulture) + ".json";
                string file = String(shard["file"]), hash = String(shard["sha256"]);
                if (file != expected || result.ContainsKey(file) || !IsHash(hash)) Fail("Invalid shard identity or SHA-256.");
                result.Add(file, hash);
            }
            // Twenty unique members of the fixed 2 x 10 set necessarily cover the complete catalog.
            return result;
        }

        private List<Layout> LoadShard(string file, string hash, GoldMinerMode mode, int level)
        {
            var root = ReadObject("GoldMiner/Levels/" + file, MaximumShardBytes, hash);
            Require(root, "schemaVersion rulesVersion catalogVersion mode levelIndex layouts");
            Versions(root);
            if (String(root["mode"]) != GoldMinerRules.ModeId(mode) || Integer(root["levelIndex"], 1, 10) != level)
                Fail("Shard mode or level does not match its catalog identity.");
            var layouts = Array(root["layouts"], 12, 12);
            var result = new List<Layout>(12);
            string previousId = null;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in layouts)
            {
                var item = Object(token);
                Require(item, "id patternId variantSeed entities witness");
                string id = String(item["id"]), pattern = String(item["patternId"]);
                string prefix = file + "-" + pattern + "-";
                if (System.Array.IndexOf(Patterns, pattern) < 0 || !id.StartsWith(prefix, StringComparison.Ordinal) ||
                    id.Length != prefix.Length + 1 || id[id.Length - 1] < '0' || id[id.Length - 1] > '3' ||
                    !ids.Add(id) || (previousId != null && string.CompareOrdinal(previousId, id) >= 0))
                    Fail("Invalid or unsorted layout identity.");
                previousId = id;
                Long(item["variantSeed"], 1, uint.MaxValue);
                var entities = ReadEntities(item["entities"], mode, level);
                ReadWitness(item["witness"], entities, mode, level);
                result.Add(new Layout { Id = id, Pattern = pattern, Entities = entities });
            }
            // Twelve distinct members of the fixed three-pattern/four-variant set cover every variant.
            return result;
        }

        private static GoldMinerEntity[] ReadEntities(JToken token, GoldMinerMode mode, int level)
        {
            var source = Array(token, 1, GoldMinerRules.MaxEntities);
            var entities = new GoldMinerEntity[source.Count];
            var counts = new int[Kinds.Length];
            var expectedKinds = new List<GoldMinerKind>();
            foreach (var kind in InitialKindOrder)
                for (int i = 0; i < GoldMinerRules.Count(mode, level, kind); i++) expectedKinds.Add(kind);
            if (source.Count != expectedKinds.Count) Fail("Layout object count does not match the rules.");
            int animalIndex = 0;
            int animalCount = GoldMinerRules.Count(mode, level, GoldMinerKind.Mole) +
                GoldMinerRules.Count(mode, level, GoldMinerKind.DiamondMole);
            for (int i = 0; i < entities.Length; i++)
            {
                var item = Object(source[i]);
                Require(item, "id kind status x y bagValue ownerPlayerId moveDirection");
                int kind = System.Array.IndexOf(Kinds, String(item["kind"]));
                if (kind < 0 || kind != (int)expectedKinds[i] || Integer(item["id"], 1, GoldMinerRules.MaxEntities) != i + 1 ||
                    String(item["status"]) != "available" || item["ownerPlayerId"].Type != JTokenType.Null)
                    Fail("Invalid initial entity identity or ownership.");
                bool bag = kind == (int)GoldMinerKind.MysteryBag;
                if (bag ? Long(item["bagValue"], 100, 100) != 100 : item["bagValue"].Type != JTokenType.Null)
                    Fail("Invalid initial mystery bag value.");
                var entity = new GoldMinerEntity {
                    Id = i + 1, Kind = (GoldMinerKind)kind, Status = GoldMinerEntityStatus.Available,
                    X = Number(item["x"]), Y = Number(item["y"]), BagValue = bag ? (long?)100 : null,
                    MoveDirection = Integer(item["moveDirection"], -1, 1)
                };
                bool animal = GoldMinerRules.IsAnimal(entity.Kind);
                if (animal ? entity.MoveDirection == 0 : entity.MoveDirection != 0) Fail("Invalid entity movement.");
                if (animal)
                {
                    double track = 1.8 + animalIndex * (GoldMinerRules.Height(mode) - 2.4) / Math.Max(1, animalCount - 1);
                    if (Math.Abs(entity.Y - track) > 1e-9 || entity.MoveDirection != (animalIndex % 2 == 0 ? -1 : 1))
                        Fail("Invalid initial animal track or direction.");
                    animalIndex++;
                }
                double radius = GoldMinerRules.Radius(entity.Kind), inset = animal ? 0 : .04;
                if (entity.X < radius + inset || entity.X > GoldMinerRules.Width(mode) - radius - inset ||
                    entity.Y < radius + inset || entity.Y > GoldMinerRules.Height(mode) - radius - inset)
                    Fail("Entity is outside the initial mine boundary.");
                bool reachable = false;
                for (int player = 0; player < GoldMinerRules.PlayerCount(mode); player++)
                    reachable |= Math.Abs(Math.Atan2(entity.X - GoldMinerRules.OriginX(mode, player), entity.Y)) <= GoldMinerRules.MaxAngle;
                if (!reachable) Fail("Entity is outside the hook's angular reach.");
                for (int j = 0; j < i; j++)
                {
                    var other = entities[j];
                    double minimum = radius + GoldMinerRules.Radius(other.Kind) +
                        (animal || GoldMinerRules.IsAnimal(other.Kind) ? 0 : .08);
                    double x = other.X - entity.X, y = other.Y - entity.Y;
                    if (x * x + y * y < minimum * minimum) Fail("Initial entities overlap or lack the required spacing.");
                }
                entities[i] = entity;
                counts[kind]++;
            }
            for (int kind = 0; kind < counts.Length; kind++)
                if (counts[kind] != GoldMinerRules.Count(mode, level, (GoldMinerKind)kind))
                    Fail("Layout object counts do not match the rules.");
            return entities;
        }

        private static void ReadWitness(JToken token, GoldMinerEntity[] entities, GoldMinerMode mode, int level)
        {
            var item = Object(token);
            Require(item, "requiredIncome inputs expectedIncome endTick");
            long required = GoldMinerRules.RequiredIncome(mode, level), total = 0;
            foreach (var entity in entities) total += GoldMinerRules.BaseValue(entity.Kind);
            // Bag BaseValue is its minimum value in the version-one rules.
            if (Long(item["requiredIncome"], 0, long.MaxValue) != required) Fail("Witness target does not match the rules.");
            Long(item["expectedIncome"], required, total);
            int endTick = Integer(item["endTick"], 1, 10000);
            var inputs = Array(item["inputs"], 1, 1024);
            int lastTick = -1, lastPlayer = -1;
            foreach (var input in inputs)
            {
                var command = Object(input);
                Require(command, "tick playerId action");
                int tick = Integer(command["tick"], 0, GoldMinerRules.LevelTicks - 1);
                int player = Integer(command["playerId"], 0, GoldMinerRules.PlayerCount(mode) - 1);
                if (String(command["action"]) != "launch" || tick >= endTick || tick < lastTick ||
                    (tick == lastTick && player <= lastPlayer)) Fail("Invalid or unsorted witness command.");
                lastTick = tick;
                lastPlayer = player;
            }
        }

        private JObject ReadObject(string path, int maximumBytes, string expectedHash)
        {
            try
            {
                string json = loader(path);
                if (string.IsNullOrEmpty(json) || json.Length > maximumBytes) Fail("Missing or oversized level resource: " + path);
                byte[] bytes = Utf8.GetBytes(json);
                if (bytes.Length > maximumBytes) Fail("Oversized UTF-8 level resource: " + path);
                if (expectedHash != null)
                {
                    string actual;
                    using (var sha = SHA256.Create())
                        actual = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                    if (actual != expectedHash) Fail("Level resource SHA-256 mismatch: " + path);
                }
                new StandardJson(json).Validate();
                using (var source = new StringReader(json))
                using (var reader = new JsonTextReader(source) { MaxDepth = 12, DateParseHandling = DateParseHandling.None })
                {
                    var result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) Fail("Trailing level resource content.");
                    return result;
                }
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException ||
                exception is OverflowException || exception is ArgumentException || exception is IOException)
            {
                throw new InvalidDataException("Cannot load gold miner level resource " + path + ": " + exception.Message, exception);
            }
        }

        private static void Versions(JObject item)
        {
            Integer(item["schemaVersion"], 1, 1);
            Integer(item["rulesVersion"], GoldMinerRules.Version, GoldMinerRules.Version);
            Integer(item["catalogVersion"], 1, 1);
        }
        private static JObject Object(JToken token)
        {
            var value = token as JObject;
            if (value == null) Fail("Expected a JSON object.");
            return value;
        }
        private static JArray Array(JToken token, int minimum, int maximum)
        {
            var value = token as JArray;
            if (value == null || value.Count < minimum || value.Count > maximum) Fail("Invalid JSON array size.");
            return value;
        }
        private static string String(JToken token)
        {
            if (token == null || token.Type != JTokenType.String) Fail("Expected a JSON string.");
            return (string)token;
        }
        private static int Integer(JToken token, int minimum, int maximum)
        {
            return (int)Long(token, minimum, maximum);
        }
        private static long Long(JToken token, long minimum, long maximum)
        {
            long value = 0;
            if (token == null || token.Type != JTokenType.Integer ||
                !long.TryParse(token.ToString(Formatting.None), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value) ||
                value < minimum || value > maximum) Fail("Expected an in-range JSON integer.");
            return value;
        }
        private static double Number(JToken token)
        {
            double value = 0;
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float) ||
                !double.TryParse(token.ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                !GoldMinerRules.IsFinite(value)) Fail("Expected a finite JSON number.");
            return value;
        }
        private static void Require(JObject item, string schema)
        {
            var names = schema.Split(' ');
            if (item.Count != names.Length) Fail("Missing or unknown level resource field.");
            foreach (var name in names) if (item.Property(name, StringComparison.Ordinal) == null) Fail("Missing level resource field: " + name);
        }
        private static bool IsHash(string value)
        {
            if (value.Length != 64) return false;
            foreach (char c in value) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
        private static void Fail(string message) { throw new InvalidDataException(message); }

        /// <summary>Rejects Json.NET's nonstandard syntax before bounded token materialization.</summary>
        private sealed class StandardJson
        {
            private readonly string source;
            private int index;
            internal StandardJson(string source) { this.source = source; }
            internal void Validate()
            {
                Value(0);
                Space();
                if (index != source.Length) Fail("Trailing JSON data.");
            }
            private void Value(int depth)
            {
                if (depth > 12) Fail("Level resource JSON is too deeply nested.");
                Space();
                if (index == source.Length) Fail("Incomplete JSON value.");
                char c = source[index];
                if (c == '{')
                {
                    index++;
                    Space();
                    if (Take('}')) return;
                    do { Space(); Quoted(); Space(); Need(':'); Value(depth + 1); Space(); } while (Take(','));
                    Need('}');
                }
                else if (c == '[')
                {
                    index++;
                    Space();
                    if (Take(']')) return;
                    do { Value(depth + 1); Space(); } while (Take(','));
                    Need(']');
                }
                else if (c == '"') Quoted();
                else if (c == 't') Literal("true");
                else if (c == 'f') Literal("false");
                else if (c == 'n') Literal("null");
                else Numeric();
            }
            private void Quoted()
            {
                Need('"');
                while (index < source.Length)
                {
                    char c = source[index++];
                    if (c == '"') return;
                    if (c < 32) Fail("Unescaped JSON control character.");
                    if (c != '\\') continue;
                    if (index == source.Length) Fail("Incomplete JSON escape.");
                    c = source[index++];
                    if (c == 'u')
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (index == source.Length) Fail("Incomplete Unicode escape.");
                            char hex = source[index++];
                            if (!((hex >= '0' && hex <= '9') || (hex >= 'a' && hex <= 'f') || (hex >= 'A' && hex <= 'F')))
                                Fail("Invalid Unicode escape.");
                        }
                    }
                    else if ("\"\\/bfnrt".IndexOf(c) < 0) Fail("Invalid JSON escape.");
                }
                Fail("Unterminated JSON string.");
            }
            private void Numeric()
            {
                Take('-');
                if (!Take('0'))
                {
                    if (index == source.Length || source[index] < '1' || source[index] > '9') Fail("Invalid JSON number.");
                    Digits();
                }
                if (Take('.')) { int start = index; Digits(); if (start == index) Fail("Missing decimal digits."); }
                if (Take('e') || Take('E'))
                {
                    if (!Take('+')) Take('-');
                    int start = index;
                    Digits();
                    if (start == index) Fail("Missing exponent digits.");
                }
            }
            private void Digits() { while (index < source.Length && source[index] >= '0' && source[index] <= '9') index++; }
            private void Literal(string text) { foreach (char c in text) Need(c); }
            private void Space() { while (index < source.Length && " \t\r\n".IndexOf(source[index]) >= 0) index++; }
            private bool Take(char c)
            {
                if (index == source.Length || source[index] != c) return false;
                index++;
                return true;
            }
            private void Need(char c) { if (!Take(c)) Fail("Invalid standard JSON syntax."); }
        }
    }
}
