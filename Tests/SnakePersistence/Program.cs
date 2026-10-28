using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Snake;
using XanaduYonder.Runtime.Snake;

internal static class Program
{
    private static int assertions;
    private static string root;
    private static SnakeCatalog catalog;
    private static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    private static void Reject(Action action, string message)
    { bool rejected = false; try { action(); } catch (FormatException) { rejected = true; } catch (ArgumentException) { rejected = true; } catch (Newtonsoft.Json.JsonException) { rejected = true; } Check(rejected, message); }
    private static void Main()
    {
        root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        CatalogTests(); ProfileTests(); CodecTests(); BoundaryAndFullBoardTests(); StoreTests();
        Console.WriteLine("PASS: Snake persistence " + assertions + " assertions; actual JSON and temporary directories.");
    }
    private static void CatalogTests()
    {
        string production = Path.Combine(root, "Assets/Resources/Snake/levels.json");
        Check(File.Exists(production), "Approved level resource has not been delivered.");
        string json = File.ReadAllText(production);
        Check(json == File.ReadAllText(Path.Combine(root, "docs/snake/levels-v1.draft.json")), "Production maps must exactly equal approved draft.");
        catalog = SnakeCatalog.Parse(json);
        Check(catalog.Count == 12 && catalog.PackVersion == "snake-levels-v1-draft-20261028" && catalog.RulesVersion == "snake-rules-v1-draft", "Catalog versions/count.");
        Check(catalog.Name(0) == "环游起步" && catalog.Get(0).Boundary == SnakeBoundary.Wrap && catalog.Get(1).Boundary == SnakeBoundary.Solid, "Level identity/boundary.");
        var copy = catalog.Get(2); copy.Obstacles[0] = 0; Check(catalog.Get(2).Obstacles[0] != 0, "Catalog rules must be defensive copies.");
        Action<Action<JObject>> bad = mutate => { var data = JObject.Parse(json); mutate(data); Reject(() => SnakeCatalog.Parse(data.ToString()), "Malformed catalog accepted: " + data.ToString().Substring(0, 30)); };
        bad(d => d.Remove("packVersion")); bad(d => d["packVersion"] = true); bad(d => d["packVersion"] = "future"); bad(d => d["schemaVersion"] = true);
        bad(d => d["defaults"] = null); bad(d => d["defaults"]["initialSnake"][0][0] = true);
        bad(d => d["levels"][2]["boundary"] = "other"); bad(d => d["levels"][2]["tickMs"] = 1); bad(d => d["levels"][2]["targetFood"] = 476);
        bad(d => ((JArray)d["levels"][2]["obstacles"]).Add(new JArray(8,8)));
        bad(d => ((JArray)d["levels"][2]["obstacles"]).Add(new JArray(20,8)));
        bad(d => ((JArray)d["levels"][2]["obstacles"]).Add(new JArray(6,20)));
        bad(d => ((JArray)d["levels"][2]["obstacles"]).Add(new JArray(9,20)));
        bad(d => d["levels"][2]["obstacles"] = new JArray(Enumerable.Range(0,24).Select(y => new JArray(10,y))));
        Reject(() => SnakeCatalog.Parse(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1")), "Duplicate catalog key accepted.");
        Reject(() => SnakeCatalog.Parse("/* comment */" + json), "Nonstandard JSON accepted.");
    }
    private static SnakeSession Session(SnakeRules rules, int food = 0, SnakeState state = SnakeState.Lost, double elapsed = 12)
    {
        var body = Enumerable.Range(0, 4 + food).Select(i => 400 + 4 + food - 1 - i).ToArray();
        var direction = SnakeDirection.Right;
        if (state == SnakeState.Lost)
        {
            if (food == 0 && rules.Boundary == SnakeBoundary.Wrap) state = SnakeState.Playing;
            else if (food == 0) body = new[] { 419, 418, 417, 416 };
            else { body = new[] { 405, 406, 426, 425 }.Concat(Enumerable.Range(0, food).Select(i => 424 - i)).ToArray(); direction = SnakeDirection.Down; }
        }
        return new SnakeSession(rules, new SnakeSessionData { SessionId = Guid.NewGuid().ToString("D"),
            Board = new SnakeSnapshot { Body = body, Food = state == SnakeState.Playing ? 0 : -1, FoodEaten = food, Score = food * 10, Direction = direction,
                State = state, TickCount = Math.Max(20, food), RandomState = 123 }, ElapsedSeconds = elapsed });
    }
    private static SnakeProfile Filled()
    {
        var profile = new SnakeProfile(catalog);
        for (int d = 0; d < 3; d++) for (int b = 0; b < 2; b++) profile.Capture(Session(SnakeRules.Classic(d, (SnakeBoundary)b), d + b));
        profile.Capture(Session(catalog.Get(0), 8, SnakeState.WonTarget));
        return profile;
    }
    private static void ProfileTests()
    {
        var profile = Filled();
        var locked = new SnakeProfile(catalog);
        Reject(() => locked.Capture(Session(catalog.Get(1), 10, SnakeState.WonTarget)), "Profile accepted a locked campaign level.");
        Check(locked.CampaignSlot == null && locked.UnlockedLevel == 0, "Rejected capture mutated profile.");
        for (int d = 0; d < 3; d++) for (int b = 0; b < 2; b++) Check(profile.GetClassic(d, (SnakeBoundary)b).Board.Score == (d + b) * 10, "Classic slot overwritten.");
        Check(profile.CampaignLevel == 0 && profile.CampaignSlot.Board.State == SnakeState.WonTarget && profile.UnlockedLevel == 1 && profile.CompletedCount == 1, "Campaign completion/unlock.");
        var original = Session(SnakeRules.Classic(1, SnakeBoundary.Wrap), 5);
        var stale = new SnakeSession(original.Game.Rules, original.Export());
        profile.Capture(original); profile.Capture(original); profile.Capture(stale);
        Check(profile.GetClassicRecord(1, SnakeBoundary.Wrap).Losses == 2 && stale.ResultRecorded, "Duplicate terminal accounting.");
        Check(profile.GetBestScore(1, SnakeBoundary.Wrap) == 50, "Best score not grouped.");
        var detached = profile.GetClassic(1, SnakeBoundary.Wrap); detached.Board.Body[0] = 100;
        Check(profile.GetClassic(1, SnakeBoundary.Wrap).Board.Body[0] != 100, "Mutable slot escaped.");
        var restored = new SnakeSession(catalog.Get(profile.CampaignLevel), profile.CampaignSlot); Check(restored.Paused, "Restored game must be paused.");
        profile.Capture(Session(catalog.Get(0), 8, SnakeState.WonTarget, 8));
        Check(profile.CompletedCount == 1 && profile.GetCampaignRecord(0).Wins == 2 && profile.GetCampaignRecord(0).BestElapsedSeconds == 8, "Replay completion/time accounting.");
        var campaign = new SnakeProfile(catalog);
        for (int level = 0; level < catalog.Count; level++)
        {
            SnakeRules rules = catalog.Get(level); int[] body = CampaignPath(rules);
            int head = body[0], neck = body[1]; SnakeDirection facing = head == neck-1 ? SnakeDirection.Left : head == neck+1 ? SnakeDirection.Right : head < neck ? SnakeDirection.Up : SnakeDirection.Down;
            var won = new SnakeSession(rules, new SnakeSessionData { SessionId = Guid.NewGuid().ToString("D"), ElapsedSeconds = 100,
                Board = new SnakeSnapshot { Body = body, Food = -1, Direction = facing, State = SnakeState.WonTarget, FoodEaten = rules.TargetFood, Score = rules.TargetFood*10, TickCount = 200, RandomState = 7 } });
            campaign.Capture(won);
            Check(campaign.UnlockedLevel == Math.Min(11,level+1) && campaign.CompletedCount == level+1, "Sequential chapter unlock failed.");
        }
        SnakeProfile allLevels; string allReason;
        Check(SnakeSaveCodec.TryDecode(SnakeSaveCodec.Encode(campaign), catalog, out allLevels, out allReason) && allLevels.CompletedCount == 12 && allLevels.CampaignLevel == 11, "Final chapter progress did not survive persistence.");
    }
    private static int[] CampaignPath(SnakeRules rules)
    {
        int count = 4 + rules.TargetFood; var path = new System.Collections.Generic.List<int> { 406 };
        var occupied = new System.Collections.Generic.HashSet<int>(rules.Obstacles); occupied.Add(406);
        Func<int,bool> visit = null;
        visit = cell =>
        {
            if (path.Count == count) return true;
            int x = cell%20, y = cell/20;
            int[] next = { x<19?cell+1:-1, y<23?cell+20:-1, x>0?cell-1:-1, y>0?cell-20:-1 };
            foreach (int n in next) if (n>=0 && occupied.Add(n))
            { path.Add(n); if (visit(n)) return true; path.RemoveAt(path.Count-1); occupied.Remove(n); }
            return false;
        };
        Check(visit(406), "Cannot construct a valid terminal persistence fixture."); return path.ToArray();
    }
    private static void CodecTests()
    {
        string json = SnakeSaveCodec.Encode(Filled()); SnakeProfile decoded; string reason;
        Check(SnakeSaveCodec.TryDecode(json, catalog, out decoded, out reason), "Roundtrip: " + reason);
        Check(decoded.CompletedCount == 1 && decoded.GetBestScore(2, SnakeBoundary.Solid) == 30, "Roundtrip records.");
        Action<Action<JObject>> bad = mutate => { var d = JObject.Parse(json); mutate(d); Check(!SnakeSaveCodec.TryDecode(d.ToString(), catalog, out decoded, out reason), "Invalid save accepted."); };
        bad(d => d.Remove("rulesVersion")); bad(d => d["rulesVersion"] = null); bad(d => d["rulesVersion"] = "future"); bad(d => d["schemaVersion"] = true);
        bad(d => d["levelPackVersion"] = "future"); bad(d => d["selectedMode"] = 2); bad(d => d["selectedDifficulty"] = "1"); bad(d => d["selectedBoundary"] = "Future");
        bad(d => d["classicSlots"][0]["sessionId"] = "not-guid"); bad(d => d["classicSlots"][0]["randomState"] = 0);
        bad(d => d["classicSlots"][0]["score"] = 10); bad(d => d["classicSlots"][0]["foodEaten"] = 1);
        bad(d => d["classicSlots"][0]["body"][0] = d["classicSlots"][0]["body"][1].DeepClone());
        bad(d => d["classicSlots"][0]["body"][0] = 100); bad(d => d["classicSlots"][0]["body"][0] = 480);
        bad(d => d["classicSlots"][0]["state"] = "WonFullBoard"); bad(d => d["classicSlots"][0]["state"] = 3);
        bad(d => d["classicSlots"][0]["direction"] = "future"); bad(d => d["classicSlots"][0]["tickCount"] = -1);
        bad(d => d["classicSlots"][0]["elapsedSeconds"] = -0.1); bad(d => d["classicSlots"][0]["elapsedSeconds"] = "1");
        bad(d => d["classicSlots"][0]["boundary"] = "Solid");
        bad(d => d["classicSlots"][0]["mode"] = "Campaign");
        bad(d => d["classicSlots"][0]["tickCount"] = 0.5); bad(d => d["classicSlots"][0]["randomState"] = 4294967296L);
        bad(d => d["campaignSlot"]["foodEaten"] = 9); bad(d => d["campaignSlot"]["state"] = "Playing");
        bad(d => d["campaignSlot"]["body"][0] = 440); bad(d => d["classicRecords"][0]["wins"] = -1);
        bad(d => d["classicRecords"][0]["bestScore"] = 7); bad(d => d["campaignRecords"][0]["bestElapsedSeconds"] = null);
        bad(d => d["unlockedLevel"] = 0); bad(d => d["classicSlots"][0]["resultRecorded"] = true);
        bad(d => d["classicSlots"][2]["resultRecorded"] = false);
        bad(d => d["campaignSlot"]["resultRecorded"] = false);
        bad(d => d["classicRecords"][0]["bestScore"] = 10);
        bad(d => d["classicRecords"][3]["bestScore"] = 4760);
        var source = JObject.Parse(json);
        foreach (var property in source.Properties().ToArray()) { string key = property.Name; bad(d => d.Remove(key)); }
        foreach (var property in ((JObject)source["classicSlots"][0]).Properties().ToArray())
        {
            string key = property.Name; bad(d => ((JObject)d["classicSlots"][0]).Remove(key));
            if (key != "food" && key != "levelId") bad(d => d["classicSlots"][0][key] = null);
        }
        foreach (string invalid in new[] { json.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,"), json.Replace("\"gameId\":\"snake\"", "'gameId':'snake'"),
            json.Replace("\"elapsedSeconds\":12", "\"elapsedSeconds\":NaN"), json.Replace("\"elapsedSeconds\":12", "\"elapsedSeconds\":1e9999") })
            Check(!SnakeSaveCodec.TryDecode(invalid, catalog, out decoded, out reason), "JSON extension/overflow accepted."); bad(d => d["campaignSlot"]["targetFood"] = 9);
        bad(d => d["campaignSlot"]["body"][0] = null); bad(d => d["campaignSlot"]["resultRecorded"] = "true");
        bad(d => d["classicSlots"][0]["food"] = d["classicSlots"][0]["body"][0].DeepClone());
        Check(!SnakeSaveCodec.TryDecode(json.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1"), catalog, out decoded, out reason), "Duplicate key accepted.");
        Check(!SnakeSaveCodec.TryDecode(json + "{}", catalog, out decoded, out reason), "Trailing root accepted.");
        Check(!SnakeSaveCodec.TryDecode("/* c */" + json, catalog, out decoded, out reason), "Comment accepted.");
        Check(!SnakeSaveCodec.TryDecode(new string(' ', SnakeSaveCodec.MaximumLength + 1), catalog, out decoded, out reason), "Oversize accepted.");
        Check(!SnakeSaveCodec.TryDecode(new string('[', 30) + "0" + new string(']', 30), catalog, out decoded, out reason), "Deep JSON accepted.");
        var live = new SnakeProfile(catalog); live.Capture(new SnakeSession(new SnakeGame(SnakeRules.Classic(0, SnakeBoundary.Wrap), 1)));
        var liveJson = JObject.Parse(SnakeSaveCodec.Encode(live)); liveJson["classicSlots"][0]["food"] = null;
        Check(!SnakeSaveCodec.TryDecode(liveJson.ToString(), catalog, out decoded, out reason), "Playing without food accepted.");
    }
    private static void BoundaryAndFullBoardTests()
    {
        var rules = SnakeRules.Classic(0, SnakeBoundary.Wrap);
        var data = new SnakeSessionData { SessionId = Guid.NewGuid().ToString("D"), Board = new SnakeSnapshot { Body = new[] { 400,419,418,417 }, Food = 0, Direction = SnakeDirection.Right,
            RandomState = 42, State = SnakeState.Playing, TickCount = 25 }, ElapsedSeconds = 5 };
        var profile = new SnakeProfile(catalog); profile.Capture(new SnakeSession(rules, data));
        SnakeProfile decoded; string reason;
        Check(SnakeSaveCodec.TryDecode(SnakeSaveCodec.Encode(profile), catalog, out decoded, out reason), "Wrap-adjacent body rejected: " + reason);
        var solid = new SnakeProfile(catalog); solid.Capture(new SnakeSession(new SnakeGame(SnakeRules.Classic(0, SnakeBoundary.Solid), 2)));
        var invalid = JObject.Parse(SnakeSaveCodec.Encode(solid)); invalid["classicSlots"][1]["body"] = new JArray(400,419,418,417);
        Check(!SnakeSaveCodec.TryDecode(invalid.ToString(), catalog, out decoded, out reason), "Solid body accepted wrap adjacency.");
        int[] full = Enumerable.Range(0,24).SelectMany(y => Enumerable.Range(0,20).Select(x => y*20+(y%2 == 0 ? x : 19-x))).ToArray();
        data.Board = new SnakeSnapshot { Body = full, Food = -1, FoodEaten = 476, Score = 4760, Direction = SnakeDirection.Left, State = SnakeState.WonFullBoard, TickCount = 10000, RandomState = uint.MaxValue };
        data.ElapsedSeconds = 120;
        var winner = new SnakeSession(rules, data); profile.Capture(winner); profile.Capture(winner);
        Check(profile.GetClassicRecord(0, SnakeBoundary.Wrap).Wins == 1 && profile.GetBestFullBoardElapsed(0, SnakeBoundary.Wrap) == 120, "Full-board accounting.");
        data.SessionId = Guid.NewGuid().ToString("D"); data.ElapsedSeconds = 150; profile.Capture(new SnakeSession(rules, data));
        Check(profile.GetBestFullBoardElapsed(0, SnakeBoundary.Wrap) == 120, "Slower full-board time replaced best.");
        data.SessionId = Guid.NewGuid().ToString("D"); data.ElapsedSeconds = 90; profile.Capture(new SnakeSession(rules, data));
        Check(SnakeSaveCodec.TryDecode(SnakeSaveCodec.Encode(profile), catalog, out decoded, out reason) && decoded.GetBestFullBoardElapsed(0, SnakeBoundary.Wrap) == 90 && decoded.GetBestScore(0, SnakeBoundary.Wrap) == 4760, "Full-board roundtrip/best time.");
        Check(decoded.GetBestFullBoardElapsed(0, SnakeBoundary.Solid) == null, "Full-board record leaked to solid.");
        var impossible = JObject.Parse(SnakeSaveCodec.Encode(Filled())); var impossibleSlot = impossible["campaignSlot"];
        impossibleSlot["body"] = JArray.FromObject(full); impossibleSlot["foodEaten"] = 476; impossibleSlot["score"] = 4760;
        impossibleSlot["tickCount"] = 10000; impossibleSlot["direction"] = "Left"; impossibleSlot["state"] = "WonFullBoard"; impossibleSlot["resultRecorded"] = false;
        Check(!SnakeSaveCodec.TryDecode(impossible.ToString(), catalog, out decoded, out reason), "Campaign continued past its target to a fabricated full-board win.");
    }
    private static void StoreTests()
    {
        string directory = Path.Combine(Path.GetTempPath(), "snake-persistence-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var store = new SnakeSaveStore(directory, catalog); string error; string warning;
            Check(store.Load(out warning) == null && warning == null, "Missing save is first use.");
            var profile = Filled(); Check(store.TrySave(profile, out error), "Initial save: " + error);
            profile.SelectedMode = 1; Check(store.TrySave(profile, out error), "Replace: " + error);
            string backup = File.ReadAllText(store.SavePath + ".bak"); File.WriteAllText(store.SavePath, "broken");
            var recovered = store.Load(out warning); Check(recovered != null && warning != null, "Backup recovery must warn.");
            Check(store.TrySave(recovered, out error) && File.ReadAllText(store.SavePath + ".bak") == backup, "Corrupt primary replaced good backup.");
            var future = JObject.Parse(File.ReadAllText(store.SavePath)); future["levelPackVersion"] = "future-pack"; string futureText = future.ToString(); File.WriteAllText(store.SavePath, futureText);
            var recordsOnly = store.Load(out warning); Check(recordsOnly != null && warning != null && recordsOnly.CampaignSlot == null && recordsOnly.GetBestScore(2, SnakeBoundary.Solid) == 30, "Version mismatch must preserve records, reject stale sessions.");
            Check(store.TrySave(recordsOnly, out error), "New version save: " + error);
            Check(Directory.GetFiles(directory, "snake-v1.json.incompatible-*").Any(p => File.ReadAllText(p) == futureText), "Incompatible source was not preserved.");
            // A current primary must not erase an incompatible backup when rotating backups.
            File.WriteAllText(store.SavePath + ".bak", futureText);
            Check(store.TrySave(profile, out error), "Save with an old-content backup: " + error);
            Check(Directory.GetFiles(directory, "snake-v1.json.bak.incompatible-*").Any(p => File.ReadAllText(p) == futureText), "Incompatible backup was overwritten without preservation.");
            future["classicRecords"][0]["bestScore"] = true; File.WriteAllText(store.SavePath, future.ToString());
            recordsOnly = store.Load(out warning);
            Check(recordsOnly != null && warning != null && recordsOnly.GetBestScore(2, SnakeBoundary.Solid) == 30 && recordsOnly.GetClassicRecord(0, SnakeBoundary.Wrap).BestScore == 0, "One invalid record discarded valid peers.");
            Check(File.ReadAllText(store.SavePath) == future.ToString(), "Load rewrote incompatible file.");
            var slotVersion = JObject.Parse(SnakeSaveCodec.Encode(profile)); slotVersion["classicSlots"][0]["levelPackVersion"] = "future-slot";
            File.WriteAllText(store.SavePath, slotVersion.ToString());
            recordsOnly = store.Load(out warning);
            Check(recordsOnly != null && warning != null && recordsOnly.GetClassic(0, SnakeBoundary.Wrap) == null && recordsOnly.GetBestScore(2, SnakeBoundary.Solid) == 30, "Unknown slot version silently resumed a backup.");
            File.WriteAllText(store.SavePath, SnakeSaveCodec.Encode(profile)); File.WriteAllText(store.SavePath + ".tmp", "interrupted");
            Check(store.Load(out warning) != null && warning == null, "Interrupted temporary file superseded committed save.");
            string committed = File.ReadAllText(store.SavePath);
            Check(!store.TrySave(null, out error) && error != null && File.ReadAllText(store.SavePath) == committed, "Invalid data touched committed state.");
            File.Delete(store.SavePath + ".bak"); File.WriteAllBytes(store.SavePath, new byte[] { 0xff, 0xfe, 0xfd });
            Check(store.Load(out warning) == null && warning != null, "Malformed UTF8 read accepted.");
            File.WriteAllText(store.SavePath, new string(' ', SnakeSaveCodec.MaximumLength + 1));
            Check(store.Load(out warning) == null && warning != null, "Oversized file read accepted.");
            File.WriteAllText(store.SavePath, "broken"); File.WriteAllText(store.SavePath + ".bak", "also broken");
            Check(store.Load(out warning) == null && warning != null, "Two corrupt files silently treated as first use.");
            File.Delete(store.SavePath); Directory.CreateDirectory(store.SavePath); Check(!store.TrySave(profile, out error) && !string.IsNullOrEmpty(error), "Storage failure reported success.");
        }
        finally { Directory.Delete(directory, true); }
    }
}
