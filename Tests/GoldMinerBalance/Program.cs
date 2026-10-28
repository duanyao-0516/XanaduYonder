using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.GoldMiner;
using XanaduYonder.Runtime.GoldMiner;

internal static class Program
{
 static GoldMinerLayout Layout(JToken j) { return new GoldMinerLayout {
  Id=(string)j["id"], PatternId=(string)j["patternId"], VariantSeed=(uint)j["variantSeed"],
  Entities=j["entities"].Select(e=>new GoldMinerEntity {Id=(int)e["id"],Kind=(GoldMinerKind)Enum.Parse(typeof(GoldMinerKind),(string)e["kind"],true),Status=GoldMinerEntityStatus.Available,X=(double)e["x"],Y=(double)e["y"],BagValue=e["bagValue"].Type==JTokenType.Null?(long?)null:(long)e["bagValue"],MoveDirection=(int)e["moveDirection"]}).ToArray(),
  Witness=new GoldMinerWitness{RequiredIncome=(long)j["witness"]["requiredIncome"],ExpectedIncome=(long)j["witness"]["expectedIncome"],EndTick=(int)j["witness"]["endTick"],Inputs=j["witness"]["inputs"].Select(x=>new GoldMinerWitnessInput {Tick=(int)x["tick"],PlayerId=(int)x["playerId"],Action=(string)x["action"]}).ToArray()}}; }

    static void Main()
    {
        var rows = new JArray(); var summary = new JArray();
        var resources = Directory.GetFiles("Assets/Resources/GoldMiner/Levels", "*.json").ToDictionary(
            p => "GoldMiner/Levels/" + Path.GetFileNameWithoutExtension(p), File.ReadAllText);
        var catalog = new GoldMinerLevelCatalog(key => resources[key]);
        foreach (var mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
        for (int level = 1; level <= 10; level++)
        {
            var shard = JObject.Parse(resources["GoldMiner/Levels/" + GoldMinerRules.ModeId(mode) + "-" + level.ToString("00")]);
            var incomes = new Dictionary<string, List<long>> { ["coarse"] = new List<long>(), ["focused"] = new List<long>() };
            var goalSeconds = new List<double>();
            foreach (var token in shard["layouts"])
            {
                var layout = Layout(token); string reason;
                if (!GoldMinerPlanner.ReplayWitness(mode, level, layout, out reason)) throw new Exception(reason);
                var row = new JObject { ["mode"] = GoldMinerRules.ModeId(mode), ["level"] = level, ["layout"] = layout.Id,
                    ["legacyTarget"] = GoldMinerRules.LegacyTarget(mode, level), ["target"] = GoldMinerRules.Target(mode, level),
                    ["requiredIncome"] = layout.Witness.RequiredIncome, ["witnessIncome"] = layout.Witness.ExpectedIncome,
                    ["witnessEndTick"] = layout.Witness.EndTick };
                var game = new GoldMinerGame(GoldMinerLevelGenerator.CreateProofRun(mode, level, layout));
                game.StartLevel(); var run = game.OwnedSnapshot; long opening = run.Current.Cash; int index = 0, first = -1;
                while (!game.SettlementReady)
                {
                    while (index < layout.Witness.Inputs.Length && layout.Witness.Inputs[index].Tick == run.Current.SimulationTick)
                    { var input = layout.Witness.Inputs[index++]; if (!game.TryLaunch(input.PlayerId, run.LastCommandSeq + 1)) throw new Exception("Rejected witness"); }
                    game.Step();
                    if (first < 0 && run.Current.Cash - opening >= layout.Witness.RequiredIncome) first = run.Current.SimulationTick;
                }
                row["witnessNeededSeconds"] = first / 60.0; goalSeconds.Add(first / 60.0);
                foreach (var profile in new[] { SimulationPolicy.Coarse, SimulationPolicy.Focused })
                {
                    var samples = new JArray();
                    for (int sample = 0; sample < 5; sample++)
                    {
                        int seed = sample + 7919 * level + (int)mode * 987;
                        var result = SimulationPolicy.Run(mode, level, layout, profile, seed, false);
                        incomes[profile.Name].Add(result.Witness.ExpectedIncome);
                        samples.Add(new JObject { ["seed"] = seed, ["income"] = result.Witness.ExpectedIncome,
                            ["launches"] = result.Witness.Inputs.Length, ["endTick"] = result.Witness.EndTick });
                    }
                    row[profile.Name] = samples;
                }
                rows.Add(row);
            }
            summary.Add(new JObject { ["mode"] = GoldMinerRules.ModeId(mode), ["level"] = level,
                ["legacyTarget"] = GoldMinerRules.LegacyTarget(mode, level), ["target"] = GoldMinerRules.Target(mode, level),
                ["requiredIncome"] = GoldMinerRules.RequiredIncome(mode, level),
                ["coarseIncome"] = Quantiles(incomes["coarse"]), ["focusedIncome"] = Quantiles(incomes["focused"]),
                ["witnessMedianNeededSeconds"] = goalSeconds.OrderBy(x => x).Skip((goalSeconds.Count - 1) / 2).Take(2).Average() });
        }
        var campaigns = new JArray();
        foreach (var mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
        foreach (var profile in new[] { SimulationPolicy.Coarse, SimulationPolicy.Focused })
        foreach (bool legacy in new[] { true, false })
        foreach (bool shop in new[] { false, true })
        for (int sample = 1; sample <= 60; sample++)
            campaigns.Add(Campaign(catalog, mode, profile, legacy, shop, (uint)(sample * 7919)));
        var report = new JObject { ["rulesVersion"] = GoldMinerRules.Version,
            ["scope"] = "All 240 original geometries, real production Core. Per-layout policies: no buffs/bombs, bags=100, five fixed seeds each. Coarse=300ms,+-4deg,70% reaction; focused=100ms,+-2deg,95%. These synthetic aiming policies are not human win rates, nor optimal upper bounds.",
            ["campaignScope"] = "960 actual Core/Session campaigns: two modes x two policies x old/current targets x no shop/buy every available item x 60 fixed seeds. Same current geometry for both curves; only the accepted target is overridden to the supported legacy value for the old control. Real Catalog RNG, bags, buffs, inventory caps, spending and cumulative carry. Policies do not use dynamite, but purchases obey real caps. A campaign stops on its first genuine failure; no retries or adaptive target changes.",
            ["summary"] = summary, ["layouts"] = rows, ["campaigns"] = campaigns };
        string output = "artifacts/validation/gold-miner/balance.json"; Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, report.ToString(Formatting.Indented) + "\n");
        Console.WriteLine("PASS balance measurement: " + rows.Count + " layouts, 2400 policy runs, " + campaigns.Count + " real campaigns. Synthetic results are not human success rates.");
    }
    static JObject Quantiles(List<long> values)
    {
        values.Sort(); return new JObject { ["q10"] = values[(int)(values.Count * .1)],
            ["median"] = (values[(values.Count - 1) / 2] + values[values.Count / 2]) / 2.0,
            ["q90"] = values[(int)(values.Count * .9)] };
    }
    static JObject Campaign(GoldMinerLevelCatalog catalog, GoldMinerMode mode, SimulationPolicy.Profile profile,
        bool legacy, bool shop, uint seed)
    {
        var session = new GoldMinerSession(); var levels = new JArray();
        Require(session.Commit(session.PrepareNewRun(mode, seed.ToString("x32"), seed, catalog.CreateStart(mode, 1, seed))), session.LastError);
        session.EnterMode(mode);
        for (int level = 1; level <= 10; level++)
        {
            var root = session.GetSnapshot(); var initial = root.GetRun(mode);
            if (legacy) initial.Current.Target = GoldMinerRules.LegacyTarget(mode, level);
            long opening = initial.Current.Cash, target = initial.Current.Target;
            var result = SimulationPolicy.Run(initial, profile, (int)seed + level * 7919, false, Math.Max(0, target - opening));
            if (mode == GoldMinerMode.Solo) root.Solo = result.FinalRun; else root.Coop = result.FinalRun;
            session = new GoldMinerSession(root); session.EnterMode(mode);
            Require(session.Commit(session.PrepareSettlement()), session.LastError);
            var state = session.GetStateSnapshot(); bool success = state.Phase == GoldMinerPhase.Won || state.Phase == GoldMinerPhase.Completed;
            levels.Add(new JObject { ["level"] = level, ["layout"] = state.LayoutId, ["openingCash"] = opening,
                ["target"] = target, ["income"] = state.Cash - opening, ["cash"] = state.Cash,
                ["passed"] = success, ["startedAboveTarget"] = opening >= target, ["shopSpent"] = state.ShopSpent });
            if (!success || level == 10) break;
            Require(session.Commit(session.PrepareEnterShop()), session.LastError);
            if (shop)
            {
                foreach (var item in new[] { GoldMinerItem.Strength, GoldMinerItem.DiamondOil, GoldMinerItem.Clover, GoldMinerItem.RockBook, GoldMinerItem.Dynamite })
                    while (true) { var buy = session.PreparePurchase(item, 0); if (buy == null) break; Require(session.Commit(buy), session.LastError); }
            }
            var prior = session.GetRunSnapshot();
            Require(session.Commit(session.PrepareNextLevel(catalog.CreateStart(mode, level + 1, seed, prior))), session.LastError);
        }
        return new JObject { ["mode"] = GoldMinerRules.ModeId(mode), ["profile"] = profile.Name,
            ["curve"] = legacy ? "legacy" : "v3", ["shop"] = shop ? "all-available" : "none", ["seed"] = seed,
            ["completed"] = session.GetStateSnapshot().Phase == GoldMinerPhase.Completed, ["levels"] = levels };
    }
    static void Require(bool condition, string reason) { if (!condition) throw new Exception(reason); }
}
