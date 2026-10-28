using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.GoldMiner;
using XanaduYonder.Runtime.GoldMiner;

internal static class Program
{
    static int passed, failed;
    static Dictionary<string, string> resources;
    const string CatalogKey = "GoldMiner/Levels/catalog", SoloKey = "GoldMiner/Levels/solo-01";

    static void Main(string[] args)
    {
        var directory = Path.Combine(args[0], "Assets/Resources/GoldMiner/Levels");
        resources = Directory.GetFiles(directory, "*.json").ToDictionary(
            file => "GoldMiner/Levels/" + Path.GetFileNameWithoutExtension(file), File.ReadAllText, StringComparer.Ordinal);
        Check("complete production catalog exists", () => Assert(resources.Count == 21 && resources.ContainsKey(CatalogKey)));
        if (failed != 0) { Environment.ExitCode = 1; return; }
        Check("fixed seed selects the approved layout and independent bag stream", () => {
            var start = Catalog().CreateStart(GoldMinerMode.Solo, 1, 12345);
            Assert(start.LayoutId == "solo-01-balanced-3", start.LayoutId);
            Assert(start.LevelLayoutSeed == 3336926330U && start.LayoutRng == 1697253807U);
            Assert(start.BagRng == 1955480042U);
            Assert(start.InitialEntities.Where(e => e.Kind == GoldMinerKind.MysteryBag).All(e => e.BagValue == 100));
            Assert(start.OpeningCash == 0 && start.OpeningShopSpent == 0 && start.OpeningPlayers[0].Bombs == 1);
            Assert(start.PreviousPatternId == null && start.ActiveBuffs.IsEmpty);
            string reason;
            Assert(start.TryValidate(GoldMinerMode.Solo, out reason), reason);
        });
        Check("second golden seed exercises nonminimum bag outcomes", () => {
            var start = Catalog().CreateStart(GoldMinerMode.Solo, 1, 1);
            Assert(start.LayoutId == "solo-01-balanced-1");
            var bags = start.InitialEntities.Where(e => e.Kind == GoldMinerKind.MysteryBag).ToArray();
            Assert(bags[0].BagValue == 250 && bags[1].BagValue == 500);
            Assert(start.BagRng == 307599695U);
        });
        Check("same seed is stable and returned checkpoints are independent", () => {
            var catalog = Catalog();
            var first = catalog.CreateStart(GoldMinerMode.Coop, 1, uint.MaxValue);
            string before = JsonConvert.SerializeObject(first);
            first.InitialEntities[0].X = -100;
            first.OpeningPlayers[0].Bombs = 8;
            first.ActiveBuffs.Strength = true;
            Assert(JsonConvert.SerializeObject(catalog.CreateStart(GoldMinerMode.Coop, 1, uint.MaxValue)) == before);
        });
        Check("all 240 shipped variants pass runtime validation and selection", () => {
            var catalog = Catalog();
            var selected = new HashSet<string>();
            foreach (GoldMinerMode mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
            for (int level = 1; level <= 10; level++)
            {
                var pools = level == 1 ? new[] { "" } : new[] { "balanced", "left", "right" };
                foreach (string previousPattern in pools)
                {
                    var prior = level == 1 ? null : Shop(mode, level - 1, previousPattern);
                    var seen = new HashSet<string>();
                    for (uint seed = 1; seed <= 256 && seen.Count < (level == 1 ? 12 : 8); seed++)
                    {
                        if (prior != null) prior.Current.LayoutRng = prior.Checkpoint.LayoutRng = seed;
                        var start = catalog.CreateStart(mode, level, prior == null ? seed : prior.RunSeed, prior);
                        string reason;
                        Assert(start.TryValidate(mode, out reason), reason);
                        Assert(start.PatternId != previousPattern);
                        Assert(start.LayoutProfileId == GoldMinerRules.LayoutProfileId(mode));
                        Assert(start.LayoutContentHash == Hash(resources[Key(mode, level)]));
                        selected.Add(start.LayoutId);
                        seen.Add(start.LayoutId);
                    }
                    Assert(seen.Count == (level == 1 ? 12 : 8), "Incomplete candidate pool.");
                }
            }
            Assert(selected.Count == 240, selected.Count.ToString(CultureInfo.InvariantCulture));
        });
        Check("next level preserves purchased inventory, cash, contributions and buffs", () => {
            var prior = Shop(GoldMinerMode.Coop, 1, "balanced", true);
            var original = JsonConvert.SerializeObject(prior);
            var start = Catalog().CreateStart(GoldMinerMode.Coop, 2, prior.RunSeed, prior);
            Assert(start.OpeningCash == prior.Current.Cash && start.OpeningShopSpent == prior.Current.ShopSpent);
            Assert(start.ActiveBuffs.Same(prior.Current.PendingBuffs) && start.ActiveBuffs.Clover && start.ActiveBuffs.Strength);
            Assert(start.OpeningPlayers[1].Bombs == prior.Current.Players[1].Bombs);
            for (int i = 0; i < start.OpeningPlayers.Length; i++)
                Assert(start.OpeningPlayers[i].TotalCollected == prior.Current.Players[i].TotalCollected);
            uint rng = prior.Current.BagRng;
            foreach (var entity in start.InitialEntities)
                if (entity.Kind == GoldMinerKind.MysteryBag) Assert(entity.BagValue == GoldMinerRules.DrawBag(ref rng, true));
            Assert(rng == start.BagRng && start.PreviousPatternId == "balanced" && start.PatternId != "balanced");
            Assert(JsonConvert.SerializeObject(prior) == original, "Caller snapshot mutated.");
        });
        Check("2000 fixed seed samples span both modes and all ten levels", () => {
            var first = Catalog();
            var second = Catalog();
            int samples = 0;
            foreach (GoldMinerMode mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
            for (int level = 1; level <= 10; level++)
            {
                var previous = new GoldMinerRunSnapshot[3];
                if (level > 1)
                    for (int pattern = 0; pattern < 3; pattern++)
                        previous[pattern] = Shop(mode, level - 1, new[] { "balanced", "left", "right" }[pattern], true);
                for (uint sample = 1; sample <= 100; sample++)
                {
                    uint seed = unchecked(0x93572411U + (uint)mode * 1000003U + (uint)level * 1009U + sample * 7919U);
                    var prior = level == 1 ? null : previous[sample % 3].Copy();
                    if (prior != null)
                    {
                        prior.RunSeed = seed;
                        prior.Current.LayoutRng = prior.Checkpoint.LayoutRng = seed;
                        prior.Current.BagRng = prior.Checkpoint.BagRng = seed ^ 0x1234U;
                    }
                    string before = JsonConvert.SerializeObject(prior);
                    var a = first.CreateStart(mode, level, seed, prior);
                    var b = second.CreateStart(mode, level, seed, prior);
                    string reason;
                    Assert(a.TryValidate(mode, out reason), reason);
                    Assert(JsonConvert.SerializeObject(a) == JsonConvert.SerializeObject(b));
                    Assert(before == JsonConvert.SerializeObject(prior));
                    Assert(prior == null || a.PatternId != prior.Current.PatternId);
                    samples++;
                }
            }
            Assert(samples == 2000);
        });
        Check("only catalog and requested shard are loaded", () => {
            var calls = new List<string>();
            var catalog = new GoldMinerLevelCatalog(key => { calls.Add(key); return resources[key]; });
            catalog.CreateStart(GoldMinerMode.Solo, 1, 7);
            Assert(calls.SequenceEqual(new[] { CatalogKey, SoloKey }));
            calls.Clear();
            catalog.CreateStart(GoldMinerMode.Coop, 1, 9);
            Assert(calls.SequenceEqual(new[] { "GoldMiner/Levels/coop-01" }));
        });
        Check("invalid requests cannot load or advance anything", () => {
            int calls = 0;
            var catalog = new GoldMinerLevelCatalog(key => { calls++; return resources[key]; });
            RejectArgument(() => catalog.CreateStart(GoldMinerMode.Solo, 0, 1));
            RejectArgument(() => catalog.CreateStart(GoldMinerMode.Solo, 2, 1));
            RejectArgument(() => catalog.CreateStart(GoldMinerMode.Coop, 11, 1));
            RejectArgument(() => catalog.CreateStart((GoldMinerMode)12, 1, 1));
            RejectArgument(() => catalog.CreateStart(GoldMinerMode.Solo, 1, 0));
            var shop = Shop(GoldMinerMode.Solo, 1, "balanced");
            RejectArgument(() => catalog.CreateStart(GoldMinerMode.Coop, 2, shop.RunSeed, shop));
            RejectArgument(() => catalog.CreateStart(GoldMinerMode.Solo, 2, shop.RunSeed + 1, shop));
            shop.Current.Phase = GoldMinerPhase.Running;
            RejectArgument(() => catalog.CreateStart(GoldMinerMode.Solo, 2, shop.RunSeed, shop));
            Assert(calls == 0);
        });
        Check("every required field and unexpected field is rejected at every schema layer", () => {
            foreach (string key in new[] { CatalogKey, SoloKey })
            {
                var source = JObject.Parse(resources[key]);
                var paths = key == CatalogKey ? new[] { "", "shards[0]" } :
                    new[] { "", "layouts[0]", "layouts[0].entities[0]", "layouts[0].witness", "layouts[0].witness.inputs[0]" };
                foreach (string path in paths)
                {
                    var obj = (JObject)(path == "" ? source : source.SelectToken(path));
                    foreach (string field in obj.Properties().Select(p => p.Name).ToArray())
                        Bad(key, root => ((JObject)(path == "" ? root : root.SelectToken(path))).Property(field).Remove());
                    Bad(key, root => ((JObject)(path == "" ? root : root.SelectToken(path)))["unexpected"] = 1);
                }
            }
        });
        Check("wrong versions fail at catalog and shard", () => {
            foreach (string key in new[] { CatalogKey, SoloKey })
            foreach (string field in new[] { "schemaVersion", "rulesVersion", "catalogVersion" })
            foreach (JToken value in new JToken[] { 0, field == "rulesVersion" ? 1 : 2, GoldMinerRules.Version + 1, 1.0, "1", JValue.CreateNull() })
                Bad(key, root => root[field] = value.DeepClone());
        });
        Check("catalog paths, uniqueness, mode and digest are bounded", () => {
            Bad(CatalogKey, root => root["shards"][0]["file"] = "../solo-01.json");
            Bad(CatalogKey, root => root["shards"][0]["file"] = "https://example.com/solo-01.json");
            Bad(CatalogKey, root => root["shards"][0]["mode"] = "Solo");
            Bad(CatalogKey, root => root["shards"][0]["sha256"] = new string('g', 64));
            Bad(CatalogKey, root => root["shards"][0]["sha256"] = "bad");
            Bad(CatalogKey, root => root["shards"][1] = root["shards"][0].DeepClone());
            Bad(CatalogKey, root => ((JArray)root["shards"]).RemoveAt(0));
            Bad(CatalogKey, root => ((JArray)root["shards"]).Add(root["shards"][0].DeepClone()));
        });
        Check("shard byte tampering is rejected before parsing", () => {
            var changed = Copy(); changed[SoloKey] += " "; Reject(Catalog(changed));
        });
        Check("layout identity, variant coverage and order are strict", () => {
            Bad(SoloKey, root => root["mode"] = "coop");
            Bad(SoloKey, root => root["levelIndex"] = 2);
            Bad(SoloKey, root => root["layouts"][0]["patternId"] = "unknown");
            Bad(SoloKey, root => root["layouts"][0]["id"] = "solo-01-balanced-4");
            Bad(SoloKey, root => root["layouts"][0]["id"] = "coop-01-balanced-0");
            Bad(SoloKey, root => root["layouts"][0]["variantSeed"] = 0);
            Bad(SoloKey, root => root["layouts"][0]["variantSeed"] = 4294967296L);
            Bad(SoloKey, root => root["layouts"][1] = root["layouts"][0].DeepClone());
            Bad(SoloKey, root => { var first = root["layouts"][0].DeepClone(); root["layouts"][0] = root["layouts"][1].DeepClone(); root["layouts"][1] = first; });
            Bad(SoloKey, root => ((JArray)root["layouts"]).RemoveAt(0));
        });
        Check("entity identity, count, initial status, bags and movement are strict", () => {
            string path = "layouts[0].entities[0]";
            Bad(SoloKey, root => root.SelectToken(path)["id"] = 2);
            Bad(SoloKey, root => root.SelectToken(path)["kind"] = "mole");
            Bad(SoloKey, root => root.SelectToken(path)["kind"] = "9");
            Bad(SoloKey, root => root.SelectToken(path)["status"] = "collected");
            Bad(SoloKey, root => root.SelectToken(path)["ownerPlayerId"] = 0);
            Bad(SoloKey, root => root.SelectToken(path)["bagValue"] = 100);
            Bad(SoloKey, root => root.SelectToken(path)["moveDirection"] = 1);
            Bad(SoloKey, root => root["layouts"][0]["entities"].Last["bagValue"] = 250);
            Bad(SoloKey, root => ((JArray)root["layouts"][0]["entities"]).RemoveAt(0));
            Bad(SoloKey, root => ((JArray)root["layouts"][0]["entities"]).Add(root["layouts"][0]["entities"][0].DeepClone()));
        });
        Check("nonfinite, coerced, out of bounds and overlapping geometry is rejected", () => {
            foreach (JToken value in new JToken[] { "NaN", "1.5", -0.1, 999, JValue.CreateNull(), true })
                Bad(SoloKey, root => root["layouts"][0]["entities"][0]["x"] = value.DeepClone());
            Bad(SoloKey, root => {
                var entities = root["layouts"][0]["entities"];
                entities[1]["x"] = entities[0]["x"].DeepClone();
                entities[1]["y"] = entities[0]["y"].DeepClone();
            });
        });
        Check("initial animal direction and track cannot be silently changed", () => {
            var prior = Shop(GoldMinerMode.Coop, 2, "balanced");
            foreach (bool changeTrack in new[] { false, true })
            {
                var data = Copy();
                string key = Key(GoldMinerMode.Coop, 3);
                var shard = JObject.Parse(data[key]);
                var animal = shard["layouts"][0]["entities"].First(e => (string)e["kind"] == "mole");
                if (changeTrack) animal["y"] = (double)animal["y"] + 0.01;
                else animal["moveDirection"] = -(int)animal["moveDirection"];
                data[key] = shard.ToString(Formatting.None);
                Rehash(data, key);
                Reject(() => Catalog(data).CreateStart(GoldMinerMode.Coop, 3, prior.RunSeed, prior));
            }
        });
        Check("witness target, value, timing, action and ordering are bounded", () => {
            Bad(SoloKey, root => root["layouts"][0]["witness"]["requiredIncome"] = 1);
            Bad(SoloKey, root => root["layouts"][0]["witness"]["expectedIncome"] = 649);
            Bad(SoloKey, root => root["layouts"][0]["witness"]["expectedIncome"] = long.MaxValue);
            Bad(SoloKey, root => root["layouts"][0]["witness"]["endTick"] = 0);
            Bad(SoloKey, root => root["layouts"][0]["witness"]["endTick"] = 10001);
            Bad(SoloKey, root => root["layouts"][0]["witness"]["inputs"][0]["tick"] = -1);
            Bad(SoloKey, root => root["layouts"][0]["witness"]["inputs"][0]["tick"] = 3600);
            Bad(SoloKey, root => root["layouts"][0]["witness"]["inputs"][0]["playerId"] = 1);
            Bad(SoloKey, root => root["layouts"][0]["witness"]["inputs"][0]["action"] = "dynamite");
            Bad(SoloKey, root => root["layouts"][0]["witness"]["inputs"][1] = root["layouts"][0]["witness"]["inputs"][0].DeepClone());
            Bad(SoloKey, root => root["layouts"][0]["witness"]["inputs"] = new JArray());
        });
        Check("nonstandard JSON, duplicate properties and trailing data are rejected", () => {
            foreach (string json in new[] {
                resources[CatalogKey].Replace("\"schemaVersion\":1", "\"schemaVersion\":01"),
                resources[CatalogKey].Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1"),
                resources[CatalogKey].Replace("\"schemaVersion\":1", "schemaVersion:1"),
                resources[CatalogKey].Replace("\"schemaVersion\":1", "'schemaVersion':1"),
                "/*comment*/" + resources[CatalogKey], resources[CatalogKey].TrimEnd().Insert(1, ","),
                resources[CatalogKey].TrimEnd().Insert(resources[CatalogKey].TrimEnd().Length - 1, ","),
                resources[CatalogKey] + "{}", resources[CatalogKey].Replace("\"schemaVersion\":1", "\"schemaVersion\":NaN")
            }) { var changed = Copy(); changed[CatalogKey] = json; Reject(Catalog(changed)); }
        });
        Check("catalog and shard limits are UTF-8 byte limits", () => {
            var changed = Copy(); changed[CatalogKey] = resources[CatalogKey] + new string(' ', GoldMinerLevelCatalog.MaximumCatalogBytes);
            Reject(Catalog(changed));
            changed = Copy(); changed[SoloKey] = resources[SoloKey] + new string(' ', GoldMinerLevelCatalog.MaximumShardBytes);
            Rehash(changed, SoloKey); Reject(Catalog(changed));
            changed = Copy(); changed[CatalogKey] = "{\"padding\":\"" + new string('旸', 24000) + "\"}";
            Assert(changed[CatalogKey].Length < GoldMinerLevelCatalog.MaximumCatalogBytes); Reject(Catalog(changed));
        });
        Check("missing resources and malformed Unicode fail safely", () => {
            Reject(new GoldMinerLevelCatalog(key => null));
            Reject(new GoldMinerLevelCatalog(key => { throw new FileNotFoundException("missing"); }));
            var changed = Copy(); changed[CatalogKey] = "\ud800"; Reject(Catalog(changed));
        });
        Check("load failure leaves the caller RNG intact and retry is deterministic", () => {
            var previous = Shop(GoldMinerMode.Solo, 1, "balanced", true);
            string before = JsonConvert.SerializeObject(previous);
            var changed = Copy(); changed["GoldMiner/Levels/solo-02"] += " ";
            var catalog = Catalog(changed);
            Reject(() => catalog.CreateStart(GoldMinerMode.Solo, 2, previous.RunSeed, previous));
            Assert(JsonConvert.SerializeObject(previous) == before);
            changed["GoldMiner/Levels/solo-02"] = resources["GoldMiner/Levels/solo-02"];
            var retried = catalog.CreateStart(GoldMinerMode.Solo, 2, previous.RunSeed, previous);
            var expected = Catalog().CreateStart(GoldMinerMode.Solo, 2, previous.RunSeed, previous);
            Assert(JsonConvert.SerializeObject(retried) == JsonConvert.SerializeObject(expected));
        });
        Console.WriteLine("Gold miner catalog: " + passed + " passed, " + failed + " failed.");
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }

    // This valid post-settlement fixture tests transfer contracts. GoldMinerLevels separately
    // replays every production witness through real fixed steps; this fixture is not a reachability proof.
    static GoldMinerRunSnapshot Shop(GoldMinerMode mode, int level, string pattern, bool purchases = false)
    {
        var source = JObject.Parse(resources[Key(mode, level)]);
        var data = source["layouts"].First(item => (string)item["patternId"] == pattern).ToObject<GoldMinerLayout>();
        var run = GoldMinerLevelGenerator.CreateProofRun(mode, level, data);
        var state = run.Current;
        state.Phase = GoldMinerPhase.Draining; state.RemainingTicks = 0; state.SimulationTick = 3600;
        foreach (var entity in state.Entities)
        {
            entity.Status = GoldMinerEntityStatus.Collected;
            long amount = GoldMinerRules.Value(entity, state.ActiveBuffs);
            int player = mode == GoldMinerMode.Solo ? 0 : entity.Id % 2;
            state.Players[player].TotalCollected += amount;
            state.Players[player].LevelCollected += amount;
            state.Cash += amount;
        }
        var root = new GoldMinerRootSnapshot { LastMode = mode };
        if (mode == GoldMinerMode.Solo) root.Solo = run; else root.Coop = run;
        var session = new GoldMinerSession(root);
        Assert(session.Commit(session.PrepareSettlement()), session.LastError);
        Assert(session.Commit(session.PrepareEnterShop()), session.LastError);
        if (purchases)
        {
            Assert(session.Commit(session.PreparePurchase(GoldMinerItem.Strength)), session.LastError);
            Assert(session.Commit(session.PreparePurchase(GoldMinerItem.Clover)), session.LastError);
            Assert(session.Commit(session.PreparePurchase(GoldMinerItem.Dynamite, mode == GoldMinerMode.Coop ? 1 : 0)), session.LastError);
        }
        return session.GetRunSnapshot();
    }
    static string Key(GoldMinerMode mode, int level) { return "GoldMiner/Levels/" + GoldMinerRules.ModeId(mode) + "-" + level.ToString("00", CultureInfo.InvariantCulture); }
    static Dictionary<string, string> Copy() { return new Dictionary<string, string>(resources, StringComparer.Ordinal); }
    static GoldMinerLevelCatalog Catalog(Dictionary<string, string> data = null) { return new GoldMinerLevelCatalog(key => (data ?? resources)[key]); }
    static string Hash(string text)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(new UTF8Encoding(false, true).GetBytes(text))).Replace("-", "").ToLowerInvariant();
    }
    static void Rehash(Dictionary<string, string> data, string key)
    {
        var catalog = JObject.Parse(data[CatalogKey]);
        foreach (var shard in catalog["shards"])
            if ((string)shard["file"] == key.Substring("GoldMiner/Levels/".Length) + ".json") shard["sha256"] = Hash(data[key]);
        data[CatalogKey] = catalog.ToString(Formatting.None);
    }
    static void Bad(string key, Action<JObject> mutate)
    {
        var data = Copy(); var json = JObject.Parse(data[key]); mutate(json); data[key] = json.ToString(Formatting.None);
        if (key != CatalogKey) Rehash(data, key);
        Reject(Catalog(data));
    }
    static void Reject(GoldMinerLevelCatalog catalog) { Reject(() => catalog.CreateStart(GoldMinerMode.Solo, 1, 12345)); }
    static void Reject(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new Exception("Expected InvalidDataException.");
    }
    static void RejectArgument(Action action)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new Exception("Expected ArgumentException.");
    }
    static void Check(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception exception) { failed++; Console.WriteLine("FAIL " + name + ": " + exception.Message); }
    }
    static void Assert(bool condition, string message = "Assertion failed.") { if (!condition) throw new Exception(message); }
}
