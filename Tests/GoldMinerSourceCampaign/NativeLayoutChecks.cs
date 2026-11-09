using System;
using System.Collections.Generic;
using System.IO;
using XanaduYonder.Core.GoldMiner;
using XanaduYonder.Runtime.GoldMiner;

static class NativeLayoutChecks
{
    static int checks;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static GoldMinerRunSnapshot Run(GoldMinerMode mode, GoldMinerLevelStart start, uint seed)
    {
        return new GoldMinerRunSnapshot { RunId = "11111111111111111111111111111111", BalanceProfile = GoldMinerBalanceProfile.Source,
            Mode = mode, RunSeed = seed, Current = start.CreateState(mode), Checkpoint = start.Copy() };
    }
    static GoldMinerRunSnapshot Shop(GoldMinerRunSnapshot run)
    {
        var c = run.Current;
        foreach (var e in c.Entities)
        {
            e.Status = GoldMinerEntityStatus.Collected;
            e.Source.Award = e.Source.Reward == "money" ? GoldMinerSourceRules.Value(e, c.ActiveBuffs, run.Mode) : 0;
            e.Source.AwardPlayer = 0;
            c.Players[0].TotalCollected += e.Source.Award; c.Players[0].LevelCollected += e.Source.Award; c.Cash += e.Source.Award;
        }
        c.RemainingTicks = 0; c.SimulationTick = GoldMinerSourceRules.DurationTicks(run.Mode);
        c.ActiveBuffs = new GoldMinerBuffs(); c.Phase = GoldMinerPhase.Shop; c.Shop = GoldMinerEconomy.CreateShop(run);
        return run;
    }
    static GoldMinerRootSnapshot Root(GoldMinerRunSnapshot run)
    {
        var root = new GoldMinerRootSnapshot { LastMode = run.Mode };
        if (run.Mode == GoldMinerMode.Solo) root.Solo = run; else root.Coop = run;
        return root;
    }
    public static void Run(string root)
    {
        Check(GoldMinerSourceRules.Height(GoldMinerMode.Solo, 3) == 10.2, "long-screen solo mine must use the full 10.2-unit depth");
        var catalog = new GoldMinerLevelCatalog(key => File.ReadAllText(Path.Combine(root, "Assets/Resources", key + ".json")));
        var seen = new HashSet<string>(); var firstLevels = new Dictionary<string, GoldMinerRunSnapshot>();
        int objects = 0;
        foreach (var mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
        for (uint seed = 1; seed <= 30; seed++)
        {
            GoldMinerRunSnapshot previous = null;
            for (int level = 1; level <= (mode == GoldMinerMode.Solo ? 10 : 30); level++)
            {
                var start = catalog.CreateSourceStart(mode, level, seed, previous);
                Check(start.CatalogVersion == 3, "new campaigns must use native geometry");
                var original = catalog.CreateSourceStart(mode, level, seed, previous, catalogVersion: 2);
                string reason;
                Check(start.TryValidate(mode, out reason), "native checkpoint: " + reason);
                Check(start.Source.MapIndex == original.Source.MapIndex, "geometry must not change source map choice");
                if (seen.Add(start.LayoutId))
                {
                    Check(start.InitialEntities.Length == original.InitialEntities.Length, "source inventory count");
                    foreach (var hook in start.InitialHooks)
                    {
                        double x, y; GoldMinerSourceRules.HookPoint(mode, hook, out x, out y, start.CatalogVersion);
                        double dx = x - hook.Source.X;
                        Check(hook.Length == .4 && Math.Abs(Math.Sqrt(dx * dx + y * y) - .4) < 1e-9, "native hook tip and visible claw must share a .4-unit length");
                    }
                    CheckGeometry(mode, start, original);
                    CheckReachable(mode, start, seed);
                    objects += start.InitialEntities.Length;
                    if (level == 1) firstLevels.Add(start.LayoutId, Run(mode, start, seed));
                    var repeat = catalog.CreateSourceStart(mode, level, seed, previous);
                    Check(GoldMinerSaveCodec.Encode(Root(Run(mode, start, seed))) == GoldMinerSaveCodec.Encode(Root(Run(mode, repeat, seed))), "deterministic native layout");
                    var forged = Run(mode, start, seed);
                    forged.Checkpoint.InitialEntities[0].Source.InitialX += .01;
                    forged.Checkpoint.InitialEntities[0].X += .01;
                    forged.Current.Entities[0].Source.InitialX += .01;
                    forged.Current.Entities[0].X += .01;
                    Check(!forged.TryValidate(out reason), "native signatures reject changed geometry");
                }
                previous = Shop(Run(mode, start, seed));
            }
        }
        Check(seen.Count == 60 && objects == 1205, "all 60 native maps and 1205 ores");
        foreach (var entry in firstLevels) { CheckEmptyTravel(entry.Value); CheckFirstLevel(entry.Key, entry.Value); }
        var legacy = catalog.CreateSourceStart(GoldMinerMode.Solo, 1, 7, catalogVersion: 2);
        var legacyShop = Shop(Run(GoldMinerMode.Solo, legacy, 7));
        string before = GoldMinerSaveCodec.Encode(Root(Run(GoldMinerMode.Solo, legacy, 7)));
        var next = catalog.CreateSourceStart(GoldMinerMode.Solo, 2, 7, legacyShop);
        Check(next.CatalogVersion == 3 && legacy.CatalogVersion == 2, "old campaign upgrades only at the next level");
        Check(before == GoldMinerSaveCodec.Encode(Root(Run(GoldMinerMode.Solo, legacy, 7))), "legacy checkpoint stays untouched");
        Console.WriteLine("PASS native mine: " + checks + " checks; 60 maps / 1205 ores, source economy, spacing, density, real hook reachability and first-level witnesses");
    }
    static void CheckGeometry(GoldMinerMode mode, GoldMinerLevelStart start, GoldMinerLevelStart original)
    {
        double w = GoldMinerSourceRules.Width(mode, start.CatalogVersion), h = GoldMinerSourceRules.Height(mode, start.CatalogVersion);
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        for (int i = 0; i < start.InitialEntities.Length; i++)
        {
            var e = start.InitialEntities[i]; var d = e.Source; var old = original.InitialEntities[i];
            Check(e.Kind == old.Kind && d.Key == old.Source.Key && d.Type == old.Source.Type && d.Value == old.Source.Value && d.Weight == old.Source.Weight && d.Reward == old.Source.Reward, "exact source economy " + start.LayoutId);
            Check(e.X - d.Radius >= 0 && e.X + d.Radius <= w && e.Y - d.Radius > 0 && e.Y + d.Radius <= h, "native bounds " + start.LayoutId + "/" + e.Id);
            Check(d.MotionEndX - d.Radius >= 0 && d.MotionEndX + d.Radius <= w, "moving ore bounds");
            Check(e.X == Math.Round(e.X, 6, MidpointRounding.AwayFromZero) && e.Y == Math.Round(e.Y, 6, MidpointRounding.AwayFromZero) && d.MotionEndX == Math.Round(d.MotionEndX, 6, MidpointRounding.AwayFromZero), "canonical cross-runtime geometry");
            if (old.Source.MotionEndX != old.Source.InitialX) Check(Math.Abs(d.MotionEndX - d.InitialX) >= .2, "moving ore retains a patrol: " + start.LayoutId + "/" + e.Id);
            Check(e.X == d.InitialX && e.Y == d.InitialY, "single simulation/render coordinates");
            minX = Math.Min(minX, e.X); maxX = Math.Max(maxX, e.X); minY = Math.Min(minY, e.Y); maxY = Math.Max(maxY, e.Y);
            for (int j = 0; j < i; j++)
            {
                var b = start.InitialEntities[j]; double dx = b.X - e.X, dy = b.Y - e.Y;
                Check(dx * dx + dy * dy >= Math.Pow(d.Radius + b.Source.Radius + .079, 2), "initial ores must not overlap");
            }
        }
        Check(maxY >= h * .8 && minY <= h * .3 && maxY - minY >= h * .55, "ore depth coverage " + start.LayoutId + " min=" + minY + " max=" + maxY + " height=" + h);
        Check(maxX - minX >= w * .58, "ore width coverage " + start.LayoutId);
    }
    static void CheckReachable(GoldMinerMode mode, GoldMinerLevelStart start, uint seed)
    {
        // Isolate each ore to prove its collision circle can be reached by an actual native hook.
        // Other ores may legitimately occlude it until collected; they must not make geometry unreachable.
        foreach (var target in start.InitialEntities)
        {
            bool caught = false;
            for (int player = 0; player < GoldMinerRules.PlayerCount(mode) && !caught; player++)
            {
                var run = Run(mode, start, seed); run.Current.Phase = GoldMinerPhase.Running;
                foreach (var ore in run.Current.Entities) if (ore.Id != target.Id) ore.Status = GoldMinerEntityStatus.Destroyed;
                double origin = GoldMinerSourceRules.OriginX(mode, player, start.CatalogVersion), aimX = target.X;
                double speed = mode == GoldMinerMode.Solo ? .1 * GoldMinerSourceLayout.SpeedScale(mode, start.CatalogVersion) : GoldMinerSourceRules.RetractPerTick(mode, run.Current, null);
                double angle = 0;
                for (int iteration = 0; iteration < 8; iteration++)
                {
                    angle = Math.Atan2(aimX - origin, target.Y);
                    double ticks = Math.Max(0, (target.Y - .4 * Math.Cos(angle)) / (speed * Math.Cos(angle)));
                    double t = ticks % 504, progress = t <= 252 ? t / 252 : (504 - t) / 252;
                    aimX = target.Source.InitialX + (target.Source.MotionEndX - target.Source.InitialX) * progress;
                }
                if (Math.Abs(angle) > Math.PI / 3) continue;
                run.Current.Hooks[player].AngleRadians = angle;
                var game = new GoldMinerGame(run); Check(game.TryLaunch(player, 1), "isolated ore launch");
                for (int tick = 0; tick < 500 && !caught; tick++)
                {
                    var step = game.Step();
                    foreach (var e in step.Events) if (e.Kind == GoldMinerEventKind.Caught && e.EntityId == target.Id) caught = true;
                    if (game.GetStateSnapshot().Hooks[player].State == GoldMinerHookState.Swinging) break;
                }
                string reason; Check(game.GetSnapshot().TryValidate(out reason), "native loaded state: " + reason);
            }
            Check(caught, "actual hook must reach " + start.LayoutId + "/" + target.Id);
        }
    }
    static void CheckEmptyTravel(GoldMinerRunSnapshot initial)
    {
        for (int player = 0; player < GoldMinerRules.PlayerCount(initial.Mode); player++)
        foreach (double angle in new[] { -Math.PI / 3, 0, Math.PI / 3 })
        {
            var run = initial.Copy(); run.Current.Phase = GoldMinerPhase.Running;
            foreach (var ore in run.Current.Entities) ore.Status = GoldMinerEntityStatus.Destroyed;
            run.Current.Hooks[player].AngleRadians = angle;
            var game = new GoldMinerGame(run); Check(game.TryLaunch(player, 1), "native empty deep launch");
            double deepestTip = 0; bool returned = false;
            for (int tick = 0; tick < 1000; tick++)
            {
                game.Step(); var snapshot = game.GetSnapshot(); string reason;
                Check(snapshot.TryValidate(out reason), "deep native travel must validate every tick: " + reason);
                GoldMinerRootSnapshot restored;
                Check(GoldMinerSaveCodec.TryDecode(GoldMinerSaveCodec.Encode(Root(snapshot)), out restored, out reason), "deep native travel must remain saveable: " + reason);
                double x, y; GoldMinerSourceRules.HookPoint(initial.Mode, snapshot.Current.Hooks[player], out x, out y, snapshot.Current.CatalogVersion);
                deepestTip = Math.Max(deepestTip, y);
                if (snapshot.Current.Hooks[player].State == GoldMinerHookState.Swinging) { returned = true; break; }
            }
            Check(returned, "empty native hook must return from the boundary");
            if (angle == 0) Check(deepestTip >= GoldMinerSourceRules.Height(initial.Mode, 3), "vertical hook must reach actual mine floor");
        }
    }
    static void CheckFirstLevel(string layoutId, GoldMinerRunSnapshot run)
    {
        var game = new GoldMinerGame(run); Check(game.StartLevel(), "first level start"); long seq = 0;
        for (int tick = 0; tick < GoldMinerSourceRules.DurationTicks(run.Mode) - 1; tick++)
        {
            var state = game.GetStateSnapshot();
            if (state.Cash >= state.Target) { Console.WriteLine("PASS first-level witness " + layoutId + ": " + state.Cash + "/" + state.Target + " in " + tick + " ticks"); return; }
            for (int player = 0; player < state.Hooks.Length; player++)
            {
                var hook = state.Hooks[player]; if (hook.State != GoldMinerHookState.Swinging) continue;
                double origin = GoldMinerSourceRules.OriginX(run.Mode, player, state.CatalogVersion);
                GoldMinerEntity nearest = null; double distance = double.MaxValue;
                foreach (var ore in state.Entities)
                {
                    if (ore.Status != GoldMinerEntityStatus.Available) continue;
                    double dx = ore.X - origin, dy = ore.Y;
                    double along = dx * Math.Sin(hook.AngleRadians) + dy * Math.Cos(hook.AngleRadians);
                    double across = Math.Abs(dx * Math.Cos(hook.AngleRadians) - dy * Math.Sin(hook.AngleRadians));
                    if (along > 0 && along < distance && across < ore.Source.Radius * .7) { nearest = ore; distance = along; }
                }
                if (nearest != null && nearest.Source.Reward == "money" && nearest.Source.Value >= 50 && nearest.Kind != GoldMinerKind.Tnt)
                    game.TryLaunch(player, ++seq);
            }
            game.Step();
            if (tick % 29 == 0)
            {
                var live = game.GetSnapshot(); string reason;
                Check(live.TryValidate(out reason), "native live validation: " + reason);
                GoldMinerRootSnapshot decoded;
                Check(GoldMinerSaveCodec.TryDecode(GoldMinerSaveCodec.Encode(Root(live)), out decoded, out reason), "native save decode: " + reason);
                var beforeReload = new GoldMinerGame(live); var afterReload = new GoldMinerGame(decoded.GetRun(run.Mode));
                beforeReload.Step(); afterReload.Step();
                Check(GoldMinerSaveCodec.Encode(Root(beforeReload.GetSnapshot())) == GoldMinerSaveCodec.Encode(Root(afterReload.GetSnapshot())), "native restored next tick must match");
            }
        }
        Check(game.GetStateSnapshot().Cash >= game.GetStateSnapshot().Target, "first-level witness failed: " + layoutId + " cash=" + game.GetStateSnapshot().Cash);
    }
}
