using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.GoldMiner;
using XanaduYonder.Runtime.GoldMiner;

internal static class Program
{
    static int checks, transactions, reloads;
    static Dictionary<string, string> resources;
    static readonly uint Seed = 12345;
    static readonly string OutputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts/validation/gold-miner");
    static readonly JArray completedLevels = new JArray(), purchases = new JArray();
    static readonly Dictionary<GoldMinerMode, GoldMinerRootSnapshot> firstWins = new Dictionary<GoldMinerMode, GoldMinerRootSnapshot>();
    static string savesDirectory;

    static int Main()
    {
        var timer = Stopwatch.StartNew(); bool passed = false;
        try
        {
            string resourcesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets/Resources/GoldMiner/Levels");
            resources = Directory.GetFiles(resourcesDirectory, "*.json").ToDictionary(p => "GoldMiner/Levels/" + Path.GetFileNameWithoutExtension(p), File.ReadAllText, StringComparer.Ordinal);
            savesDirectory = Path.Combine(OutputDirectory, "campaign-saves-" + Guid.NewGuid().ToString("N"));
            var store = new GoldMinerSaveStore(Path.Combine(savesDirectory, "baseline"));
            var session = new GoldMinerSession();
            var catalog = new GoldMinerLevelCatalog(key => resources[key]);
            RunLegacyShopTransition(catalog);
            RunLegacyV2Transition(catalog);
            string warning; Check(store.Load(out warning) == null && warning == null, "new campaign begins without a saved run");
            foreach (var mode in Modes())
            {
                var start = catalog.CreateStart(mode, 1, Seed);
                string id = mode == GoldMinerMode.Solo ? "11111111111111111111111111111111" : "22222222222222222222222222222222";
                Commit(session, store, session.PrepareNewRun(mode, id, Seed, start), mode, "new challenge");
            }
            foreach (var mode in Modes()) RunBaseline(ref session, store, catalog, mode);
            var complete = store.Load(out warning);
            Check(complete != null && warning == null, "completed challenges load without recovery warning");
            foreach (var mode in Modes())
            {
                Check(complete.GetRun(mode).Current.Phase == GoldMinerPhase.Completed, "both stored challenges are complete");
                Check(complete.GetRecord(mode).BestClearedLevel == 10 && complete.GetRecord(mode).CompletionCount == 1, "both records contain one ten-level completion");
            }
            foreach (var mode in Modes()) RunAllItemsBranch(catalog, mode);
            Check(completedLevels.Count == 20 && purchases.Count == 2, "two complete base campaigns plus two all-item purchase branches");
            passed = true;
            Console.WriteLine("PASS GoldMiner campaign: " + checks + " checks; 20 complete levels; 2 all-item shop/failure/retry branches; " + transactions + " durable transactions; " + reloads + " reloads.");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine("FAIL GoldMiner campaign: " + exception); return 1; }
        finally
        {
            timer.Stop(); Directory.CreateDirectory(OutputDirectory);
            var report = new JObject {
                ["passed"] = passed, ["recordedAtUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["elapsedSeconds"] = timer.Elapsed.TotalSeconds, ["checks"] = checks, ["durableTransactions"] = transactions,
                ["reloads"] = reloads, ["baseCampaignLevels"] = completedLevels, ["allItemBranches"] = purchases,
                ["saveDirectory"] = savesDirectory,
                ["coverageBoundary"] = "The two complete ten-level campaigns purchase no items and execute shipped witnesses through production Session. Separate branches purchase every item, activate all buffs in level 2, genuinely lose after a real collection, reload, retry, and reproduce that collection. Buffed ten-level completion is not claimed."
            };
            File.WriteAllText(Path.Combine(OutputDirectory, "campaign.json"), report.ToString(Formatting.Indented) + "\n", new UTF8Encoding(false));
        }
    }

    static IEnumerable<GoldMinerMode> Modes() { return new[] { GoldMinerMode.Solo, GoldMinerMode.Coop }; }
    static void RunLegacyShopTransition(GoldMinerLevelCatalog catalog)
    {
        string fixture = Path.Combine(Directory.GetCurrentDirectory(), "Tests/GoldMinerPersistence/Fixtures/v1-coop-shop-before.json");
        string directory = Path.Combine(savesDirectory, "v1-shop-migration");
        Directory.CreateDirectory(directory);
        var store = new GoldMinerSaveStore(directory);
        string original = File.ReadAllText(fixture);
        File.WriteAllText(store.SavePath, original);
        string warning;
        var legacy = store.Load(out warning);
        Check(legacy != null && warning == null && legacy.RulesVersion == 1, "captured v1 shop loads without recovery");
        var session = new GoldMinerSession(legacy);
        Check(session.ActiveMode == GoldMinerMode.Coop, "legacy shop belongs to cooperation");
        var prior = session.GetRunSnapshot();
        var next = catalog.CreateStart(GoldMinerMode.Coop, 2, prior.RunSeed, prior);
        Check(next.CatalogVersion == prior.Current.CatalogVersion && next.LayoutProfileId == prior.Current.LayoutProfileId,
            "geometry/schema version remains compatible across balance migration");
        Check(next.LayoutContentHash != prior.Current.LayoutContentHash, "next level binds to the newly verified v3 catalog");
        Check(next.OpeningCash == prior.Current.Cash && next.OpeningShopSpent == prior.Current.ShopSpent,
            "the first v3 checkpoint retains actual v1 cash and shop history");
        Commit(session, store, session.PrepareNextLevel(next), GoldMinerMode.Coop, "v1 shop to v3 second level");
        Check(File.ReadAllText(store.SavePath + ".bak") == original, "v1 shop remains the exact backup after first v3 transition");
        Check(session.GetRunSnapshot().Current.Phase == GoldMinerPhase.Ready, "migrated campaign continues with an explicit ready screen");
        Check(session.GetRunSnapshot().Current.Target == GoldMinerRules.LegacyTarget(GoldMinerMode.Coop, 2), "the legacy campaign keeps its cumulative curve when using the v3 catalog");
    }
    static void RunLegacyV2Transition(GoldMinerLevelCatalog catalog)
    {
        string directory = Path.Combine(savesDirectory, "v2-won-migration"); Directory.CreateDirectory(directory);
        var store = new GoldMinerSaveStore(directory);
        string original = File.ReadAllText("Tests/GoldMinerPersistence/Fixtures/v2-solo-won-coop-failed.json");
        File.WriteAllText(store.SavePath, original); string warning;
        var legacy = store.Load(out warning); Check(legacy != null && warning == null && legacy.RulesVersion == 2, "actual v2 won save loads");
        var session = new GoldMinerSession(legacy); session.EnterMode(GoldMinerMode.Solo);
        Check(session.GetStateSnapshot().Target == 1500 && session.GetStateSnapshot().Cash == 3680, "the accepted v2 second-level result is unchanged");
        Commit(session, store, session.PrepareEnterShop(), GoldMinerMode.Solo, "v2 accepted win enters shop");
        Check(File.ReadAllText(store.SavePath + ".bak") == original, "first v3 write retains exact v2 backup");
        var prior = session.GetRunSnapshot();
        Commit(session, store, session.PrepareNextLevel(catalog.CreateStart(GoldMinerMode.Solo, 3, prior.RunSeed, prior)), GoldMinerMode.Solo, "v2 campaign enters new third level");
        Check(session.GetStateSnapshot().Cash == 3680 && session.GetStateSnapshot().Target == 2550,
            "the ongoing legacy campaign keeps its old cumulative curve and observed wallet");
        Commit(session, store, session.PrepareRetryLevel(), GoldMinerMode.Solo, "migrated legacy third-level retry");
        Check(session.GetStateSnapshot().Cash == 3680 && session.GetStateSnapshot().Target == 2550, "retry keeps the legacy target and original wallet");
        var record = session.GetSnapshot().SoloRecord;
        Commit(session, store, session.PrepareNewRun(GoldMinerMode.Solo, "33333333333333333333333333333333", Seed,
            catalog.CreateStart(GoldMinerMode.Solo, 1, Seed)), GoldMinerMode.Solo, "new challenge after v2 migration");
        Check(session.GetStateSnapshot().Cash == 0 && session.GetStateSnapshot().Target == 1200, "new challenge uses v3 first target");
        Check(session.GetSnapshot().SoloRecord.BestRunGrossIncome == record.BestRunGrossIncome &&
            session.GetSnapshot().SoloRecord.BestClearedLevel == record.BestClearedLevel, "new challenge retains historical v2 records");
    }
    static void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
    static string Encode(GoldMinerRootSnapshot snapshot) { return GoldMinerSaveCodec.Encode(snapshot); }
    static string Other(GoldMinerRootSnapshot snapshot, GoldMinerMode active)
    {
        var json = JObject.Parse(Encode(snapshot)); string other = active == GoldMinerMode.Solo ? "coop" : "solo";
        return json["slots"][other].ToString(Formatting.None) + "|" + json["records"][other].ToString(Formatting.None);
    }
    static void Commit(GoldMinerSession session, GoldMinerSaveStore store, GoldMinerCandidate candidate, GoldMinerMode changedMode, string label)
    {
        Check(candidate != null, label + " prepare: " + session.LastError);
        var before = session.GetSnapshot(); string unchanged = Other(before, changedMode), beforeText = Encode(before), error;
        Check(store.TrySave(candidate.Snapshot, out error), label + " persist: " + error);
        Check(Encode(session.GetSnapshot()) == beforeText, label + " persistence must not publish before commit");
        Check(session.Commit(candidate), label + " commit: " + session.LastError); transactions++;
        Check(Other(session.GetSnapshot(), changedMode) == unchanged, label + " preserves other slot and record");
        string warning; var loaded = store.Load(out warning);
        Check(loaded != null && warning == null && Encode(loaded) == Encode(session.GetSnapshot()), label + " durable round trip");
    }
    static void Save(GoldMinerSession session, GoldMinerSaveStore store)
    {
        string error; long revision = session.StateRevision;
        Check(store.TrySave(session.GetSnapshot(), out error), "save live simulation: " + error);
        session.MarkSaved(revision); Check(!session.Dirty, "successful save clears matching dirty revision");
    }
    static GoldMinerSession Reload(GoldMinerSession session, GoldMinerSaveStore store)
    {
        Save(session, store); string before = Encode(session.GetSnapshot()), warning;
        var restored = store.Load(out warning); Check(restored != null && warning == null && Encode(restored) == before, "disk reload preserves exact root");
        var next = new GoldMinerSession(restored); reloads++;
        Check(next.IsSuspended && !next.Step().Advanced, "cold reload does not consume time before explicit continue");
        Check(Encode(next.GetSnapshot()) == before, "suspension preserves all saved simulation data");
        return next;
    }
    static void Continue(GoldMinerSession session)
    {
        session.Resume(GoldMinerSuspendReason.NeedsExplicitContinue);
        Check(!session.IsSuspended, "explicit continue releases suspension");
    }
    static JToken Witness(GoldMinerRunSnapshot run)
    {
        string key = "GoldMiner/Levels/" + GoldMinerRules.ModeId(run.Mode) + "-" + run.Current.LevelIndex.ToString("00", CultureInfo.InvariantCulture);
        return JObject.Parse(resources[key])["layouts"].Single(x => (string)x["id"] == run.Current.LayoutId)["witness"];
    }
    static void RunBaseline(ref GoldMinerSession session, GoldMinerSaveStore store, GoldMinerLevelCatalog catalog, GoldMinerMode mode)
    {
        session.EnterMode(mode); Save(session, store);
        string other = Other(session.GetSnapshot(), mode);
        for (int level = 1; level <= 10; level++)
        {
            var opening = session.GetRunSnapshot();
            Check(opening.Current.LevelIndex == level && opening.Current.Phase == GoldMinerPhase.Ready && opening.Current.ActiveBuffs.IsEmpty, "base campaign ready without buffs");
            var witness = Witness(opening); var commands = (JArray)witness["inputs"];
            Continue(session); Check(session.StartLevel(), "start real catalog level");
            long sequence = opening.LastCommandSeq; int index = 0, tick = 0;
            while (true)
            {
                while (index < commands.Count && (int)commands[index]["tick"] == tick)
                    Check(session.TryLaunch((int)commands[index++]["playerId"], ++sequence), "campaign witness launch must be accepted");
                var step = session.Step();
                if (!step.Advanced) throw new InvalidOperationException("Campaign stopped before settlement.");
                tick++;
                if (level == 5 && tick == 120)
                {
                    session = Reload(session, store); Continue(session);
                    Check(session.GetRunSnapshot().LastCommandSeq == sequence, "reload preserves command sequence");
                }
                if (step.SettlementReady) break;
                if (tick > 10000) throw new InvalidOperationException("Campaign exceeded finite drain bound.");
            }
            var drained = session.GetRunSnapshot();
            Check(index == commands.Count && drained.Current.SimulationTick == (int)witness["endTick"], "all actual witness commands consumed at expected time");
            Check(drained.Current.Cash - opening.Current.Cash >= (long)witness["expectedIncome"], "catalog bag outcomes preserve baseline witness income");
            Check(drained.Current.Cash >= drained.Current.Target, "real collected cash reaches current target");
            Commit(session, store, session.PrepareSettlement(), mode, "level settlement");
            var won = session.GetRunSnapshot();
            Check(won.Current.Phase == (level == 10 ? GoldMinerPhase.Completed : GoldMinerPhase.Won), "actual settlement wins the selected level");
            Check(session.GetSnapshot().GetRecord(mode).BestClearedLevel == level, "record advances one real level at a time");
            Check(Other(session.GetSnapshot(), mode) == other, "whole level preserves the other challenge");
            completedLevels.Add(new JObject { ["mode"] = GoldMinerRules.ModeId(mode), ["levelIndex"] = level, ["layoutId"] = won.Current.LayoutId,
                ["cash"] = won.Current.Cash, ["target"] = won.Current.Target, ["income"] = won.Current.Cash - opening.Current.Cash,
                ["endTick"] = tick, ["commands"] = index, ["shopSpent"] = won.Current.ShopSpent, ["phase"] = won.Current.Phase.ToString() });
            if (level == 1) firstWins.Add(mode, session.GetSnapshot());
            if (level == 10) break;
            Commit(session, store, session.PrepareEnterShop(), mode, "enter real shop");
            var prior = session.GetRunSnapshot();
            Check(prior.Current.Shop.Offers.Length == 5 && prior.Current.Shop.Spent == 0, "base campaign enters shop and chooses no purchases");
            var next = catalog.CreateStart(mode, level + 1, Seed, prior);
            Check(next.PatternId != prior.Current.PatternId, "consecutive catalog patterns differ");
            Commit(session, store, session.PrepareNextLevel(next), mode, "advance real catalog level");
        }
        session = Reload(session, store);
        var final = session.GetSnapshot(); var run = final.GetRun(mode); var record = final.GetRecord(mode);
        Check(run.Current.ShopSpent == 0 && record.CompletionCount == 1 && record.BestRunGrossIncome == run.Current.Players.Sum(p => p.TotalCollected), "base challenge record uses actual gross income");
        Check(session.PrepareSettlement() == null && session.PrepareEnterShop() == null, "completed challenge cannot settle again or enter a final shop");
        Check(Encode(session.GetSnapshot()) == Encode(final), "duplicate result requests do not increment records");
    }

    static void RunAllItemsBranch(GoldMinerLevelCatalog catalog, GoldMinerMode mode)
    {
        var session = new GoldMinerSession(firstWins[mode]);
        var store = new GoldMinerSaveStore(Path.Combine(savesDirectory, "all-items-" + GoldMinerRules.ModeId(mode)));
        Save(session, store); string other = Other(session.GetSnapshot(), mode);
        Commit(session, store, session.PrepareEnterShop(), mode, "branch enters real first shop");
        var before = session.GetRunSnapshot();
        Check(before.Current.Cash >= GoldMinerRules.ShopMaximum(1), "real first-level earnings fund all first-shop inventory");
        var bought = new JArray();
        foreach (var item in new[] { GoldMinerItem.Strength, GoldMinerItem.Clover, GoldMinerItem.RockBook, GoldMinerItem.DiamondOil })
        {
            Commit(session, store, session.PreparePurchase(item), mode, "purchase " + item);
            bought.Add(item.ToString());
        }
        for (int i = 0; i < 3; i++)
        {
            int player = i % GoldMinerRules.PlayerCount(mode);
            Commit(session, store, session.PreparePurchase(GoldMinerItem.Dynamite, player), mode, "purchase allocated dynamite");
            bought.Add("Dynamite:P" + (player + 1));
        }
        var shop = session.GetRunSnapshot();
        Check(shop.Current.Shop.Offers.All(o => o.Remaining == 0) && shop.Current.Shop.Spent == GoldMinerRules.ShopMaximum(1), "all inventory sold with exact rule spending");
        Check(before.Current.Cash - shop.Current.Cash == shop.Current.Shop.Spent, "all-item shop consumes real balance");
        string sold = Encode(session.GetSnapshot());
        foreach (GoldMinerItem item in Enum.GetValues(typeof(GoldMinerItem))) Check(session.PreparePurchase(item) == null, "sold-out purchase cannot be repeated");
        Check(Encode(session.GetSnapshot()) == sold, "failed purchase requests cannot mutate cash/inventory");
        session = Reload(session, store);
        shop = session.GetRunSnapshot();
        var next = catalog.CreateStart(mode, 2, Seed, shop);
        Commit(session, store, session.PrepareNextLevel(next), mode, "activate purchased buffs in second level");
        var ready = session.GetRunSnapshot();
        Check(ready.Current.ActiveBuffs.Strength && ready.Current.ActiveBuffs.Clover && ready.Current.ActiveBuffs.RockBook && ready.Current.ActiveBuffs.DiamondOil && ready.Current.PendingBuffs.IsEmpty, "all purchased buffs activate once");
        Check(ready.Current.Cash == shop.Current.Cash && ready.Current.ShopSpent == shop.Current.ShopSpent, "next checkpoint preserves actual shop cash and spending");
        Check(ready.Current.Players.Select(p => p.Bombs).SequenceEqual(shop.Current.Players.Select(p => p.Bombs)), "allocated bombs survive next checkpoint");
        string checkpoint = JsonConvert.SerializeObject(ready.Checkpoint);
        var firstInput = Witness(ready)["inputs"][0];
        int collectionTick; long firstIncome;
        RunOneCollection(ref session, store, firstInput, true, out collectionTick, out firstIncome);
        var failed = session.GetRunSnapshot();
        Check(failed.Current.Phase == GoldMinerPhase.Failed && failed.Current.Cash < failed.Current.Target, "branch genuinely fails by time and cash after real collection");
        Check(failed.Current.Players.Sum(p => p.LevelCollected) == firstIncome && firstIncome > 0, "failed attempt contains actual credited income");
        Check(failed.Current.ActiveBuffs.Same(ready.Current.ActiveBuffs), "failed attempt retains paid active buffs");
        session = Reload(session, store); long sequence = session.GetRunSnapshot().LastCommandSeq;
        Commit(session, store, session.PrepareRetryLevel(), mode, "retry paid second-level checkpoint");
        var retry = session.GetRunSnapshot();
        Check(retry.AttemptIndex == ready.AttemptIndex + 1 && retry.LastCommandSeq == sequence, "retry increments attempt without rewinding commands");
        Check(JsonConvert.SerializeObject(retry.Checkpoint) == checkpoint, "retry preserves complete original paid checkpoint");
        Check(JsonConvert.SerializeObject(retry.Current) == JsonConvert.SerializeObject(ready.Checkpoint.CreateState(mode)), "retry restores opening cash, entities, RNG, buffs, bombs and contributions");
        Check(retry.Current.Cash == ready.Current.Cash && retry.Current.ShopSpent == ready.Current.ShopSpent, "retry removes failed income without refunding purchases");
        int repeatedTick; long repeatedIncome;
        RunOneCollection(ref session, store, firstInput, false, out repeatedTick, out repeatedIncome);
        Check(repeatedTick == collectionTick && repeatedIncome == firstIncome, "paid retry reproduces exact real collection timing and bag value");
        Check(Other(session.GetSnapshot(), mode) == other, "shop/buff/failure/retry branch preserves other mode slot and record");
        purchases.Add(new JObject { ["mode"] = GoldMinerRules.ModeId(mode), ["source"] = "actual first-level win", ["purchases"] = bought,
            ["spent"] = shop.Current.Shop.Spent, ["level2LayoutId"] = ready.Current.LayoutId, ["openingCash"] = ready.Current.Cash,
            ["failedAttemptIncome"] = firstIncome, ["collectionTick"] = collectionTick, ["repeatCollectionTick"] = repeatedTick,
            ["retryVerified"] = true, ["fullBuffedCampaignCompleted"] = false });
    }
    static void RunOneCollection(ref GoldMinerSession session, GoldMinerSaveStore store, JToken input, bool finishFailure, out int collectionTick, out long income)
    {
        Continue(session); Check(session.StartLevel(), "start paid second-level attempt");
        var opening = session.GetRunSnapshot(); long sequence = opening.LastCommandSeq;
        int launchTick = (int)input["tick"], player = (int)input["playerId"], tick = 0; collectionTick = -1; income = 0;
        while (true)
        {
            if (tick == launchTick) Check(session.TryLaunch(player, ++sequence), "real first aimed launch under purchased strength");
            var step = session.Step(); if (!step.Advanced) throw new InvalidOperationException("Paid attempt stopped unexpectedly."); tick++;
            foreach (var e in step.Events) if (e.Kind == GoldMinerEventKind.Collected) { collectionTick = tick; income += e.Amount; }
            if (collectionTick >= 0 && !finishFailure) { Save(session, store); return; }
            if (step.SettlementReady) break;
            if (tick > 10000) throw new InvalidOperationException("Paid attempt exceeded finite drain bound.");
        }
        Check(collectionTick >= 0 && income > 0, "paid attempt collected through real Core");
        Commit(session, store, session.PrepareSettlement(), opening.Mode, "failed paid attempt settlement");
    }
}
