using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.GoldMiner;
using XanaduYonder.Runtime.GoldMiner;

internal static class Program
{
    static int passed, failed;
    const string RunId = "11111111111111111111111111111111";
    static void Main()
    {
        Check("root schema uses exact wrappers and canonical Int64 strings", () => {
            var json = Encode(new GoldMinerRootSnapshot { TxnSeq = long.MaxValue }); var root = JObject.Parse(json);
            Assert(root.Count == 7 && root["records"]["solo"]["completionCount"].Type == JTokenType.String);
            Assert((string)root["txnSeq"] == "9223372036854775807" && root["slots"]["solo"].Type == JTokenType.Null && root["soloRecord"] == null);
            Assert(Decode(json).TxnSeq == long.MaxValue);
        });
        Check("every required field at every object depth rejects removal and unknown siblings", () => {
            foreach (var snapshot in new[] { Shop(), Won() }) {
            var root = JObject.Parse(Encode(snapshot)); var paths = new List<string>(); ObjectPaths(root, paths);
            foreach (var path in paths) {
                var obj = (JObject)(path == "" ? root : root.SelectToken(path)); var keys = new List<string>();
                foreach (var property in obj.Properties()) keys.Add(property.Name);
                foreach (var key in keys) {
                    var altered = (JObject)root.DeepClone(); var target = (JObject)(path == "" ? altered : altered.SelectToken(path)); target.Remove(key); Reject(altered.ToString(Formatting.None), "missing " + path + "." + key);
                }
                var unknown = (JObject)root.DeepClone(); ((JObject)(path == "" ? unknown : unknown.SelectToken(path))).Add("unknown", 0); Reject(unknown.ToString(Formatting.None), "unknown " + path);
            }
            }
        });
        Check("integer tokens never coerce floats strings booleans null or overflow", () => {
            var root = JObject.Parse(Encode(Ready()));
            foreach (var invalid in new JToken[] { new JValue(1.0), new JValue("1"), new JValue(true), JValue.CreateNull(), JToken.Parse("2147483648") })
                Reject(Change(root, "schemaVersion", invalid));
            foreach (var invalid in new JToken[] { new JValue(1.0), new JValue("1"), new JValue(-1), JToken.Parse("4294967296"), JValue.CreateNull() })
                Reject(Change(root, "slots.solo.runSeed", invalid));
            Reject(Change(root, "slots.solo.current.activeBuffs.strength", new JValue(1)));
        });
        Check("amount sequence and completion strings reject all noncanonical or overflowing forms", () => {
            var root = JObject.Parse(Encode(Ready()));
            foreach (var invalid in new[] { "", "00", "01", "+1", "-1", " 1", "1 ", "1.0", "1e1", "9223372036854775808", "18446744073709551615", "١" })
                foreach (var field in new[] { "txnSeq", "slots.solo.lastCommandSeq", "slots.solo.current.cash", "records.coop.completionCount" }) Reject(Change(root, field, new JValue(invalid)), field+"="+invalid);
            foreach (var token in new JToken[] { new JValue(1), new JValue(1.0), new JValue(true), JValue.CreateNull() }) Reject(Change(root, "txnSeq", token));
        });
        Check("null only exists at declared optional fields; no null collection elements", () => {
            var root = JObject.Parse(Encode(Ready()));
            foreach (var field in new[] { "records", "records.solo", "slots", "slots.solo.current", "slots.solo.checkpoint", "slots.solo.runId", "slots.solo.current.players", "slots.solo.current.players[0]", "slots.solo.current.activeBuffs", "slots.solo.checkpoint.initialEntities[0]" }) Reject(Change(root, field, JValue.CreateNull()), field);
            Assert(Decode(root.ToString()).Solo.Current.Shop == null);
        });
        Check("duplicate fields trailing data and JSON extensions are rejected", () => {
            var json = Encode(Ready());
            Reject(json.Replace("\"txnSeq\":\"0\"", "\"txnSeq\":\"0\",\"txnSeq\":\"0\""));
            Reject(json.Replace("\"cash\":\"0\"", "\"cash\":\"0\",\"cash\":\"0\""));
            Reject(json + " {}"); Reject(json + " trailing"); Reject("/*comment*/" + json); Reject(json.Replace("\"gameId\"", "gameId"));
            Reject(json.Replace("\"gameId\"", "'gameId'")); Reject(json.Insert(json.Length-1, ","));
            Reject(json.Replace("\"gameId\":\"gold-miner\"", "\"gameId\":\"\\uD800\""));
            Reject("{\"a\":" + new string('[', 13) + "0" + new string(']', 13) + "}");
        });
        Check("version identity enum casing and finite numeric boundaries are strict", () => {
            var root = JObject.Parse(Encode(Ready()));
            Reject(Change(root, "schemaVersion", new JValue(2)));
            Reject(Change(root, "rulesVersion", new JValue(GoldMinerRules.Version + 1)));
            Reject(Change(root, "rulesVersion", new JValue(0)));
            Reject(Change(root, "rulesVersion", new JValue(1)), "legacy rules cannot declare a new target");
            Reject(Change(root, "rulesVersion", new JValue(2)), "legacy rules cannot declare a new target");
            Reject(Change(root, "gameId", new JValue("watermelon"))); Reject(Change(root, "lastMode", new JValue("Solo")));
            Reject(Change(root, "slots.solo.current.hooks[0].state", new JValue(0)));
            Reject(Change(root, "slots.solo.current.entities[0].kind", new JValue("unknown")));
            Reject(Encode(Ready()).Replace("\"x\":3.0", "\"x\":NaN"));
            Reject(Change(root, "slots.solo.current.entities[0].x", JToken.Parse("1e999")));
        });
        Check("captured v1 device saves migrate both slots losslessly and preserve a v1 backup", () => {
            foreach (var name in new[] { "coop-shop-before", "coop-native-dual", "coop-second-ready", "coop-native-cross", "coop-native-same-half" })
            WithDirectory(dir => {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "Tests/GoldMinerPersistence/Fixtures/v1-" + name + ".json");
                var legacyText = File.ReadAllText(path);
                var expected = JObject.Parse(legacyText);
                Assert((int)expected["rulesVersion"] == 1, "fixture must be an actual pre-upgrade save");
                var store = new GoldMinerSaveStore(dir);
                File.WriteAllText(store.SavePath, legacyText);
                string warning, error;
                var legacy = store.Load(out warning);
                Assert(legacy != null && warning == null && !store.RequiresRecovery);
                var session = new GoldMinerSession(legacy);
                var migrated = session.GetSnapshot();
                Assert(session.Dirty && migrated.RulesVersion == GoldMinerRules.Version && legacy.RulesVersion == 1);
                expected["rulesVersion"] = GoldMinerRules.Version;
                Assert(JToken.DeepEquals(expected, JObject.Parse(Encode(migrated))),
                    "migration changed saved clocks, money, hooks, layout hash, checkpoint, slots or records");
                Assert(store.TrySave(migrated, out error), error);
                Assert(File.ReadAllText(store.SavePath + ".bak") == legacyText, "original v1 backup must remain byte-identical");
                var restored = store.Load(out warning);
                Assert(warning == null && Encode(restored) == Encode(migrated));
                Assert(!new GoldMinerSession(restored).Dirty, "the persisted v3 migration must be idempotent");
                if (name == "coop-native-cross" || name == "coop-native-same-half")
                {
                    var run = restored.Coop;
                    long expectedIncome = 0;
                    foreach (var entity in run.Current.Entities)
                        if (entity.Status == GoldMinerEntityStatus.Held)
                            expectedIncome += GoldMinerRules.Value(entity, run.Current.ActiveBuffs);
                    Assert((name == "coop-native-cross") == (expectedIncome > 0), "real fixture must retain its claimed/empty in-flight hook");
                    var game = new GoldMinerGame(run);
                    for (int tick = 0; tick < GoldMinerRules.MaximumSimulationTicks(GoldMinerMode.Coop); tick++)
                    {
                        game.Step(); var state = game.GetStateSnapshot();
                        if (state.Hooks[0].State == GoldMinerHookState.Swinging && state.Hooks[1].State == GoldMinerHookState.Swinging)
                            break;
                    }
                    var finished = game.GetStateSnapshot();
                    Assert(finished.Hooks[0].State == GoldMinerHookState.Swinging && finished.Hooks[1].State == GoldMinerHookState.Swinging);
                    Assert(finished.Cash - run.Current.Cash == expectedIncome, "migrated in-flight load credits once using its existing identity");
                }
                Console.WriteLine("MIGRATION captured v1 " + name + ": both slots preserved, v1 backup retained");
            });
        });
        Check("v2 actual device targets and records survive v3 migration and retry", () => {
            Assert(GoldMinerRules.Version == 3);
            foreach (var name in new[] { "v2-solo-won-coop-failed", "v2-solo-loaded" })
            WithDirectory(dir => {
                var original = File.ReadAllText("Tests/GoldMinerPersistence/Fixtures/" + name + ".json");
                var store = new GoldMinerSaveStore(dir); File.WriteAllText(store.SavePath, original);
                string warning, error; var legacy = store.Load(out warning);
                Assert(legacy != null && warning == null && legacy.RulesVersion == 2);
                var session = new GoldMinerSession(legacy);
                var expected = JObject.Parse(original); expected["rulesVersion"] = 3;
                Assert(JToken.DeepEquals(expected, JObject.Parse(Encode(session.GetSnapshot()))), "v2 migration changes only rulesVersion");
                Assert(store.TrySave(session.GetSnapshot(), out error) && File.ReadAllText(store.SavePath + ".bak") == original);
                foreach (var mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
                {
                    session.EnterMode(mode); var run = session.GetRunSnapshot();
                    if (run.Current.Phase != GoldMinerPhase.Failed && run.Current.Phase != GoldMinerPhase.Running) continue;
                    long target = run.Current.Target; var retry = session.PrepareRetryLevel();
                    Assert(retry != null && session.Commit(retry));
                    var retried = session.GetRunSnapshot();
                    Assert(retried.Current.Target == target && retried.Current.Cash == run.Checkpoint.OpeningCash,
                        "retry preserves the accepted old target and original opening funds");
                    Assert(retried.Current.Target < GoldMinerRules.Target(mode, retried.Current.LevelIndex));
                    Assert(Decode(Encode(session.GetSnapshot())).RulesVersion == 3);
                }
            });
            var bad = JObject.Parse(File.ReadAllText("Tests/GoldMinerPersistence/Fixtures/v2-solo-loaded.json"));
            bad["rulesVersion"] = 3;
            bad["slots"]["solo"]["current"]["target"] = "2551";
            Reject(bad.ToString(), "v3 still rejects arbitrary targets outside the explicit old/new curves");
        });
        Check("legacy campaigns retain their curve through every next level and reload", () => {
            foreach (var version in new[] { 1, 2, 3 })
            foreach (var mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
            for (int level = 1; level < GoldMinerRules.LevelCount; level++)
            {
                var run = Run(mode, level, GoldMinerRules.LegacyTarget(mode, level));
                run.Current.Target = GoldMinerRules.LegacyTarget(mode, level);
                run.Current.Phase = GoldMinerPhase.Draining; run.Current.RemainingTicks = 0; run.Current.SimulationTick = 3600;
                var root = new GoldMinerRootSnapshot { RulesVersion = version, LastMode = mode };
                if (mode == GoldMinerMode.Solo) root.Solo = run; else root.Coop = run;
                var session = new GoldMinerSession(Decode(Encode(root)));
                Assert(session.Commit(session.PrepareSettlement()), session.LastError);
                Assert(session.Commit(session.PrepareEnterShop()), session.LastError);
                Assert(session.Commit(session.PreparePurchase(GoldMinerItem.Strength)), session.LastError);
                var current = session.GetStateSnapshot();
                var next = Start(mode, level + 1, current.Cash);
                next.PatternId = "left"; next.PreviousPatternId = current.PatternId;
                next.LayoutId = GoldMinerRules.ModeId(mode) + "-" + (level + 1).ToString("00") + "-left-0";
                next.OpeningShopSpent = current.ShopSpent; next.ActiveBuffs = current.PendingBuffs.Copy();
                for (int player = 0; player < current.Players.Length; player++)
                {
                    next.OpeningPlayers[player].Bombs = current.Players[player].Bombs;
                    next.OpeningPlayers[player].TotalCollected = current.Players[player].TotalCollected;
                }
                Assert(session.Commit(session.PrepareNextLevel(next)), session.LastError);
                var restored = new GoldMinerSession(Decode(Encode(session.GetSnapshot())));
                Assert(restored.GetStateSnapshot().Target == GoldMinerRules.LegacyTarget(mode, level + 1),
                    "legacy " + version + " " + mode + " " + level + "->" + (level + 1) + " must not jump to the v3 wallet curve");
                Assert(restored.Commit(restored.PrepareRetryLevel()), restored.LastError);
                Assert(restored.GetStateSnapshot().Target == GoldMinerRules.LegacyTarget(mode, level + 1) &&
                    restored.GetStateSnapshot().Cash == current.Cash && restored.GetStateSnapshot().ActiveBuffs.Strength);
                var record = restored.GetSnapshot().GetRecord(mode);
                Assert(restored.Commit(restored.PrepareNewRun(mode, new string('3', 32), 2, Start(mode))), restored.LastError);
                Assert(restored.GetStateSnapshot().Target == GoldMinerRules.Target(mode, 1) && restored.GetStateSnapshot().Cash == 0);
                Assert(restored.GetSnapshot().GetRecord(mode).BestClearedLevel == record.BestClearedLevel &&
                    restored.GetSnapshot().GetRecord(mode).BestRunGrossIncome == record.BestRunGrossIncome);
            }
        });
        Check("oversized characters bytes and arrays never restore partial data", () => {
            var json = Encode(Ready()); Reject(new string(' ', GoldMinerSaveCodec.MaximumLength) + json);
            Reject("{\"x\":\"" + new string('金', 400000) + "\"}");
            var root = JObject.Parse(json); var entities = (JArray)root["slots"]["solo"]["current"]["entities"];
            while (entities.Count < 129) entities.Add(entities[0].DeepClone()); Reject(root.ToString());
            root = JObject.Parse(json); ((JArray)root["slots"]["solo"]["current"]["players"]).Add(root["slots"]["solo"]["current"]["players"][0].DeepClone()); Reject(root.ToString());
        });
        Check("checkpoint RNG entity identity money and lifetimes are validated together", () => {
            var root = JObject.Parse(Encode(Ready()));
            foreach (var field in new[] { "slots.solo.current.layoutRng", "slots.solo.current.bagRng", "slots.solo.runSeed" }) Reject(Change(root, field, new JValue(0)));
            Reject(Change(root, "slots.solo.checkpoint.layoutRng", new JValue(99)));
            Reject(Change(root, "slots.solo.mode", new JValue("coop")));
            Reject(Change(root, "slots.solo.current.cash", new JValue("1")));
            Reject(Change(root, "slots.solo.current.entities[1].id", new JValue(1)));
            Reject(Change(root, "slots.solo.current.entities[0].bagValue", new JValue("100")));
            Reject(Change(root, "slots.solo.current.pendingBuffs.strength", new JValue(true)));
            Reject(Change(root, "slots.solo.current.activeBuffs.clover", new JValue(true)));
            root = JObject.Parse(Encode(Won())); Reject(Change(root, "records.solo.bestClearedLevel", new JValue(0)));
            Reject(Change(root, "slots.solo.current.settlement.recordCommitted", new JValue(false)));
        });
        Check("all seven phases and two independent slots round trip", () => {
            var states = new[] { Ready(), Running(), Draining(false), Won(), Failed(), Shop(), Completed() }; var seen = new HashSet<GoldMinerPhase>();
            foreach (var state in states) { state.Coop = Run(GoldMinerMode.Coop); var copy = Decode(Encode(state)); seen.Add(copy.Solo.Current.Phase); Assert(Encode(copy) == Encode(state)); copy.Coop.Current.Entities[0].X = 7; Assert(state.Coop.Current.Entities[0].X == 3); }
            Assert(seen.Count == 7);
        });
        Check("bag values animal direction and checkpoint buffs survive exact round trip", () => {
            var root=Ready();var start=root.Solo.Checkpoint;
            start.InitialEntities=new[]{new GoldMinerEntity {Id=1,Kind=GoldMinerKind.MysteryBag,X=2,Y=3,BagValue=250},new GoldMinerEntity {Id=2,Kind=GoldMinerKind.Mole,X=4,Y=5,MoveDirection=-1},new GoldMinerEntity {Id=3,Kind=GoldMinerKind.DiamondMole,X=3,Y=5,MoveDirection=1}};
            start.ActiveBuffs.Strength=true;root.Solo.Current=start.CreateState(GoldMinerMode.Solo);var copy=Decode(Encode(root));
            Assert(copy.Solo.Current.Entities[0].BagValue==250&&copy.Solo.Current.Entities[1].MoveDirection==-1&&copy.Solo.Checkpoint.ActiveBuffs.Strength);
            Assert((string)JObject.Parse(Encode(root))["slots"]["solo"]["current"]["entities"][0]["bagValue"]=="250");
        });
        Check("restored retry preserves command order RNG and complete checkpoint without new draws", () => {
            var root=Failed();root.TxnSeq=19;root.Solo.AttemptIndex=4;root.Solo.LastCommandSeq=long.MaxValue;
            root.Solo.Current.Settlement.Id=RunId+"/1/4";var restored=new GoldMinerSession(Decode(Encode(root)));
            Assert(restored.Commit(restored.PrepareRetryLevel()),restored.LastError);var result=restored.GetSnapshot();
            Assert(result.TxnSeq==20&&result.Solo.AttemptIndex==5&&result.Solo.LastCommandSeq==long.MaxValue);
            Assert(result.Solo.Current.Phase==GoldMinerPhase.Ready&&result.Solo.Current.BagRng==root.Solo.Checkpoint.BagRng&&result.Solo.Current.LayoutRng==root.Solo.Checkpoint.LayoutRng);
            Assert(result.Solo.Current.Entities[0].X==root.Solo.Checkpoint.InitialEntities[0].X);
        });
        Check("shop below the old target preserves purchases allocations and pending buffs", () => {
            var session = new GoldMinerSession(Shop());
            foreach (var item in new[] { GoldMinerItem.Strength, GoldMinerItem.Clover, GoldMinerItem.RockBook, GoldMinerItem.DiamondOil, GoldMinerItem.Dynamite, GoldMinerItem.Dynamite, GoldMinerItem.Dynamite }) Assert(session.Commit(session.PreparePurchase(item)), session.LastError);
            var root = Decode(Encode(session.GetSnapshot())); Assert(root.Solo.Current.Cash < root.Solo.Current.Target); Assert(root.Solo.Current.Players[0].Bombs == 4);
            Assert(root.Solo.Current.PendingBuffs.Strength && root.Solo.Current.PendingBuffs.DiamondOil);
            var json=JObject.Parse(Encode(root)); Reject(Change(json,"slots.solo.current.shop.offers[0].allocations[0].count",new JValue(0)));
            Reject(Change(json,"slots.solo.current.shop.entryShopSpent",new JValue("1")));
        });
        Check("both extending and loaded co-op hooks resume the identical production simulation", () => {
            var run=Run(GoldMinerMode.Coop);run.Current.Phase=GoldMinerPhase.Running;
            foreach(var hook in run.Current.Hooks)hook.AngleRadians=0;
            var game=new GoldMinerGame(run);Assert(game.TryLaunch(0,1));Assert(game.TryLaunch(1,2));
            for(var i=0;i<5;i++)game.Step(); Assert(game.GetSnapshot().Current.Hooks[0].State==GoldMinerHookState.Extending); Replay(game.GetSnapshot());
            for(var i=0;i<100;i++) { if(game.GetSnapshot().Current.Hooks[0].State==GoldMinerHookState.RetractingLoaded)break;game.Step(); }
            Assert(game.GetSnapshot().Current.Hooks[0].State==GoldMinerHookState.RetractingLoaded && game.GetSnapshot().Current.Hooks[1].State==GoldMinerHookState.RetractingLoaded);
            Replay(game.GetSnapshot());
            var root = new GoldMinerRootSnapshot { Coop=game.GetSnapshot(),LastMode=GoldMinerMode.Coop }; var json=JObject.Parse(Encode(root));
            Reject(Change(json,"slots.coop.current.hooks[1].heldEntityId",json["slots"]["coop"]["current"]["hooks"][0]["heldEntityId"].DeepClone()));
            Reject(Change(json,"slots.coop.current.entities[0].ownerPlayerId",new JValue(1)));
            Reject(Change(json,"slots.coop.current.entities[0].x",new JValue(8)));
        });
        Check("invalid encode is rejected before touching any file", () => WithDirectory(dir => {
            var store=new GoldMinerSaveStore(dir);string error;Assert(store.TrySave(Ready(),out error),error);var before=File.ReadAllBytes(store.SavePath);
            var invalid=Ready();invalid.Solo.Current.Cash=1;Assert(!store.TrySave(invalid,out error)&&error!=null);Equal(before,File.ReadAllBytes(store.SavePath));Assert(!File.Exists(store.SavePath+".tmp"));
        }));
        Check("absent files are quiet and committed replacement creates a valid previous backup", () => WithDirectory(dir => {
            var store=new GoldMinerSaveStore(dir);string warning,error;Assert(store.Load(out warning)==null&&warning==null&&!store.RequiresRecovery);
            var first=Ready();Assert(store.TrySave(first,out error),error);var second=Ready();second.TxnSeq=1;Assert(store.TrySave(second,out error),error);
            Assert(store.Load(out warning).TxnSeq==1&&warning==null);Assert(Decode(File.ReadAllText(store.SavePath+".bak")).TxnSeq==0);Assert(!File.Exists(store.SavePath+".tmp"));
        }));
        Check("corrupt primary recovers a legal backup and never overwrites it with corruption", () => WithDirectory(dir => {
            var store=new GoldMinerSaveStore(dir);string error,warning;var good=Encode(Ready());File.WriteAllText(store.SavePath,"broken");File.WriteAllText(store.SavePath+".bak",good);
            Assert(store.Load(out warning)!=null&&warning!=null&&!store.RequiresRecovery);var next=Ready();next.TxnSeq=2;Assert(store.TrySave(next,out error),error);
            Assert(File.ReadAllText(store.SavePath+".bak")==good && store.Load(out warning).TxnSeq==2);
        }));
        Check("corrupt unsupported invalid UTF8 oversized and orphan temporary files require explicit recovery", () => {
            foreach(var bytes in new[] { Encoding.UTF8.GetBytes("broken"), Encoding.UTF8.GetBytes(Encode(Ready()).Replace("\"schemaVersion\":1","\"schemaVersion\":2")), new byte[]{0xff,0xfe,0x80}, new byte[GoldMinerSaveCodec.MaximumLength+1] }) WithDirectory(dir => {
                var store=new GoldMinerSaveStore(dir);File.WriteAllBytes(store.SavePath,bytes);string warning,error;Assert(store.Load(out warning)==null&&warning!=null&&store.RequiresRecovery);
                Assert(!store.TrySave(Ready(),out error));Equal(bytes,File.ReadAllBytes(store.SavePath));
            });
            WithDirectory(dir=>{var store=new GoldMinerSaveStore(dir);File.WriteAllText(store.SavePath+".tmp",Encode(Ready()));string warning,error;Assert(store.Load(out warning)==null&&store.RequiresRecovery&&warning!=null);Assert(!store.TrySave(Ready(),out error));});
        });
        Check("explicit archival preserves all three old files and leaves other game paths untouched", () => WithDirectory(dir => {
            var store=new GoldMinerSaveStore(dir);foreach(var ext in new[]{"",".bak",".tmp"})File.WriteAllText(store.SavePath+ext,"old"+ext);
            var other=Path.Combine(dir,"watermelon-v1.json");File.WriteAllText(other,"other game");string warning,error;store.Load(out warning);Assert(store.RequiresRecovery);
            Assert(store.TryArchiveInvalid(out error),error);Assert(!store.RequiresRecovery);var archived=Directory.GetFiles(dir,"gold-miner-v1.json*.invalid-*");Assert(archived.Length==3);
            var contents=new HashSet<string>();foreach(var path in archived)contents.Add(File.ReadAllText(path));Assert(contents.Contains("old")&&contents.Contains("old.bak")&&contents.Contains("old.tmp"));
            Assert(store.TrySave(Ready(),out error),error);Assert(File.ReadAllText(other)=="other game");
        }));
        Check("valid primary or backup cannot be archived through invalid-file recovery", () => WithDirectory(dir => {
            var store=new GoldMinerSaveStore(dir);string error;Assert(store.TrySave(Ready(),out error));var good=File.ReadAllText(store.SavePath);Assert(!store.TryArchiveInvalid(out error));Assert(File.ReadAllText(store.SavePath)==good);Assert(!store.RequiresRecovery,"valid files must not be marked corrupt after refusing archive");Assert(store.TrySave(Ready(),out error),error);
        }));
        Check("write flush and pre-replace failure preserve primary and backup and allow exact retry", () => {
            foreach(var fault in new[]{"write","flush","replaceBefore"})WithDirectory(dir=>{
                var io=new FaultFiles();var store=new GoldMinerSaveStore(dir,io);string error;Assert(store.TrySave(Ready(),out error));var second=Ready();second.TxnSeq=1;Assert(store.TrySave(second,out error));
                var main=File.ReadAllBytes(store.SavePath);var backup=File.ReadAllBytes(store.SavePath+".bak");var next=Ready();next.TxnSeq=2;io.Fault=fault;
                Assert(!store.TrySave(next,out error)&&error!=null);Equal(main,File.ReadAllBytes(store.SavePath));Equal(backup,File.ReadAllBytes(store.SavePath+".bak"));
                io.Fault=null;Assert(store.TrySave(next,out error),error);Assert(Decode(File.ReadAllText(store.SavePath)).TxnSeq==2);
            });
        });
        Check("post-replace and first-move errors commit only exact validated candidate bytes and sequence", () => {
            foreach(var fault in new[]{"replaceAfter","moveAfter"})WithDirectory(dir=>{
                var io=new FaultFiles();var store=new GoldMinerSaveStore(dir,io);string error;if(fault=="replaceAfter")Assert(store.TrySave(Ready(),out error));
                var next=Ready();next.TxnSeq=3;io.Fault=fault;Assert(store.TrySave(next,out error),error);Assert(error==null&&Decode(File.ReadAllText(store.SavePath)).TxnSeq==3);
            });
            WithDirectory(dir=>{var io=new FaultFiles();var store=new GoldMinerSaveStore(dir,io);string error;Assert(store.TrySave(Ready(),out error));io.Fault="replaceBefore";var sameSequence=Ready();sameSequence.LastMode=GoldMinerMode.Coop;Assert(!store.TrySave(sameSequence,out error));});
        });
        Check("failed first write is retryable in memory and not adopted on cold startup", () => WithDirectory(dir => {
            var io=new FaultFiles { Fault="flush" };var store=new GoldMinerSaveStore(dir,io);string error,warning;Assert(!store.TrySave(Ready(),out error));
            Assert(new GoldMinerSaveStore(dir).Load(out warning)==null&&warning!=null);io.Fault=null;Assert(store.TrySave(Ready(),out error),error);
        }));
        Check("failed archive preserves every byte and blocks a fresh write until retry succeeds", () => WithDirectory(dir => {
            var io=new FaultFiles();var store=new GoldMinerSaveStore(dir,io);foreach(var ext in new[]{"",".bak",".tmp"})File.WriteAllText(store.SavePath+ext,"old"+ext);string warning,error;store.Load(out warning);
            io.Fault="archiveSecond";Assert(!store.TryArchiveInvalid(out error)&&store.RequiresRecovery);Assert(!store.TrySave(Ready(),out error));
            var values=new HashSet<string>();foreach(var path in Directory.GetFiles(dir))values.Add(File.ReadAllText(path));Assert(values.Contains("old")&&values.Contains("old.bak")&&values.Contains("old.tmp"));
            io.Fault=null;Assert(store.TryArchiveInvalid(out error),error);Assert(store.TrySave(Ready(),out error),error);
        }));
        Check("real inaccessible target path reports failure without mutating the candidate", () => WithDirectory(dir => {
            var path=Path.Combine(dir,"a-file");File.WriteAllText(path,"untouched");var store=new GoldMinerSaveStore(path);var root=Ready();string error;
            Assert(!store.TrySave(root,out error)&&error!=null);Assert(root.Solo.Current.Phase==GoldMinerPhase.Ready&&File.ReadAllText(path)=="untouched");
        }));
        Check("small real-file reads allocate proportionally rather than a maximum-size buffer", () => WithDirectory(dir => {
            var path=Path.Combine(dir,"small.json");var data=Encoding.UTF8.GetBytes(Encode(new GoldMinerRootSnapshot()));File.WriteAllBytes(path,data);var io=new GoldMinerSaveFileSystem();
            Equal(data,io.ReadBounded(path,GoldMinerSaveCodec.MaximumLength));
            var before=GC.GetAllocatedBytesForCurrentThread();
            for(var i=0;i<100;i++)Equal(data,io.ReadBounded(path,GoldMinerSaveCodec.MaximumLength));
            var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Console.WriteLine("ALLOC 100 small reads: "+allocated+" bytes");
            Assert(allocated<4*1024*1024,"small file reads allocated "+allocated+" bytes");
        }));
        Check("one hundred empty-root saves avoid maximum-size primary and backup allocations", () => WithDirectory(dir => {
            var store=new GoldMinerSaveStore(dir);var root=new GoldMinerRootSnapshot();string error;Assert(store.TrySave(root,out error),error);Assert(store.TrySave(root,out error),error);
            var before=GC.GetAllocatedBytesForCurrentThread();
            for(var i=0;i<100;i++)Assert(store.TrySave(root,out error),error);
            var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Console.WriteLine("ALLOC 100 empty-root saves: "+allocated+" bytes");
            Assert(allocated<16*1024*1024,"small save cycle allocated "+allocated+" bytes");
        }));
        Check("real file reads accept the exact byte limit and reject excess before buffer allocation", () => WithDirectory(dir => {
            var store=new GoldMinerSaveStore(dir);var json=Encode(new GoldMinerRootSnapshot());var data=Encoding.UTF8.GetBytes(json+new string(' ',GoldMinerSaveCodec.MaximumLength-json.Length));File.WriteAllBytes(store.SavePath,data);
            var io=new GoldMinerSaveFileSystem();Equal(data,io.ReadBounded(store.SavePath,GoldMinerSaveCodec.MaximumLength));string warning;
            Assert(store.Load(out warning)!=null&&warning==null&&!store.RequiresRecovery);
            using(var stream=new FileStream(store.SavePath,FileMode.Append,FileAccess.Write))stream.WriteByte(32);
            var before=GC.GetAllocatedBytesForCurrentThread();var rejected=false;
            try {io.ReadBounded(store.SavePath,GoldMinerSaveCodec.MaximumLength);}catch(InvalidDataException){rejected=true;}
            var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert(rejected,"maximum plus one byte accepted");Assert(allocated<64*1024,"oversized file allocated a payload buffer: "+allocated);
            Assert(store.Load(out warning)==null&&warning!=null&&store.RequiresRecovery);
            File.WriteAllBytes(store.SavePath,new byte[0]);Assert(io.ReadBounded(store.SavePath,GoldMinerSaveCodec.MaximumLength).Length==0);
        }));
        Console.WriteLine("GoldMinerPersistence: "+passed+" passed, "+failed+" failed");if(failed!=0)Environment.Exit(1);
    }

    static GoldMinerLevelStart Start(GoldMinerMode mode=GoldMinerMode.Solo,int level=1,long opening=0)
    {
        var count=GoldMinerRules.PlayerCount(mode);var players=new GoldMinerOpeningPlayer[count];for(var i=0;i<count;i++)players[i]=new GoldMinerOpeningPlayer {PlayerId=i,Bombs=1,TotalCollected=i==0?opening:0};
        return new GoldMinerLevelStart {LevelIndex=level,CatalogVersion=1,LayoutProfileId=GoldMinerRules.LayoutProfileId(mode),LayoutId=GoldMinerRules.ModeId(mode)+"-"+level.ToString("00")+"-balanced-0",LayoutContentHash=new string('a',64),PatternId="balanced",PreviousPatternId=level==1?null:"left",LevelLayoutSeed=1,LayoutRng=2,BagRng=3,OpeningCash=opening,OpeningPlayers=players,ActiveBuffs=new GoldMinerBuffs(),InitialHooks=GoldMinerRules.InitialHooks(mode),InitialEntities=new[]{new GoldMinerEntity {Id=1,Kind=GoldMinerKind.GoldSmall,X=3,Y=2},new GoldMinerEntity {Id=2,Kind=GoldMinerKind.GoldSmall,X=mode==GoldMinerMode.Solo?5:9,Y=2}}};
    }
    static GoldMinerRunSnapshot Run(GoldMinerMode mode=GoldMinerMode.Solo,int level=1,long opening=0) {var start=Start(mode,level,opening);return new GoldMinerRunSnapshot {RunId=RunId,Mode=mode,RunSeed=1,Checkpoint=start,Current=start.CreateState(mode)};}
    static GoldMinerRootSnapshot Ready() {return new GoldMinerRootSnapshot {Solo=Run()};}
    static GoldMinerRootSnapshot Running() {var r=Ready();r.Solo.Current.Phase=GoldMinerPhase.Running;return r;}
    static GoldMinerRootSnapshot Draining(bool success,int level=1) {var r=new GoldMinerRootSnapshot {Solo=Run(GoldMinerMode.Solo,level,success?GoldMinerRules.Target(GoldMinerMode.Solo,level)+350:0)};r.Solo.Current.Phase=GoldMinerPhase.Draining;r.Solo.Current.RemainingTicks=0;r.Solo.Current.SimulationTick=3600;return r;}
    static GoldMinerRootSnapshot Won() {var s=new GoldMinerSession(Draining(true));Assert(s.Commit(s.PrepareSettlement()),s.LastError);return s.GetSnapshot();}
    static GoldMinerRootSnapshot Failed() {var s=new GoldMinerSession(Draining(false));Assert(s.Commit(s.PrepareSettlement()),s.LastError);return s.GetSnapshot();}
    static GoldMinerRootSnapshot Shop() {var s=new GoldMinerSession(Won());Assert(s.Commit(s.PrepareEnterShop()),s.LastError);return s.GetSnapshot();}
    static GoldMinerRootSnapshot Completed() {var s=new GoldMinerSession(Draining(true,10));Assert(s.Commit(s.PrepareSettlement()),s.LastError);return s.GetSnapshot();}
    static void Replay(GoldMinerRunSnapshot run)
    {
        var root=new GoldMinerRootSnapshot {Coop=run,LastMode=GoldMinerMode.Coop};var decoded=Decode(Encode(root));var a=new GoldMinerGame(run);var b=new GoldMinerGame(decoded.Coop);
        for(var i=0;i<300;i++) {a.Step();b.Step();Assert(Encode(new GoldMinerRootSnapshot {Coop=a.GetSnapshot()})==Encode(new GoldMinerRootSnapshot {Coop=b.GetSnapshot()}),"diverged at step "+i);}
        Assert(a.GetSnapshot().Current.Cash==200);
    }
    static string Encode(GoldMinerRootSnapshot root) {return GoldMinerSaveCodec.Encode(root);}
    static GoldMinerRootSnapshot Decode(string json) {GoldMinerRootSnapshot root;string error;Assert(GoldMinerSaveCodec.TryDecode(json,out root,out error),error);return root;}
    static void Reject(string json,string why="invalid JSON accepted") {GoldMinerRootSnapshot root;string error;Assert(!GoldMinerSaveCodec.TryDecode(json,out root,out error)&&root==null&&!string.IsNullOrEmpty(error),why);}
    static string Change(JObject source,string path,JToken value) {var root=(JObject)source.DeepClone();root.SelectToken(path).Replace(value);return root.ToString(Formatting.None);}
    static void ObjectPaths(JToken token,List<string> paths) {if(token is JObject)paths.Add(token.Path);if(token is JContainer)foreach(var child in ((JContainer)token).Children())ObjectPaths(child is JProperty?((JProperty)child).Value:child,paths);}
    static void WithDirectory(Action<string> action) {var dir=Path.Combine(Path.GetTempPath(),"gold-miner-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);try{action(dir);}finally{Directory.Delete(dir,true);}}
    static void Equal(byte[] a,byte[] b) {Assert(a.Length==b.Length);for(var i=0;i<a.Length;i++)Assert(a[i]==b[i]);}
    static void Check(string name,Action test) {try{test();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.Error.WriteLine("FAIL "+name+": "+e.Message);}}
    static void Assert(bool value,string message="assertion failed") {if(!value)throw new Exception(message);}

    sealed class FaultFiles : IGoldMinerSaveFileSystem
    {
        readonly GoldMinerSaveFileSystem real=new GoldMinerSaveFileSystem();public string Fault;int archiveMoves;
        public bool Exists(string path) {return real.Exists(path);}
        public byte[] ReadBounded(string path,int length) {return real.ReadBounded(path,length);}
        public void CreateDirectory(string path) {real.CreateDirectory(path);}
        public void WriteAndFlush(string path,byte[] bytes) {if(Fault=="write")throw new IOException("Injected write failure");if(Fault=="flush"){File.WriteAllBytes(path,bytes);throw new IOException("Injected flush failure");}real.WriteAndFlush(path,bytes);}
        public void Replace(string temporary,string destination,string backup) {if(Fault=="replaceBefore")throw new IOException("Injected pre-replace failure");real.Replace(temporary,destination,backup);if(Fault=="replaceAfter")throw new IOException("Injected post-replace failure");}
        public void Move(string source,string destination) {if(Fault=="archiveSecond"&&++archiveMoves==2)throw new IOException("Injected archive failure");real.Move(source,destination);if(Fault=="moveAfter")throw new IOException("Injected post-move failure");}
    }
}
