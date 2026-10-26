using System;
using System.Collections.Generic;
using System.Text.Json;
using XanaduYonder.Core.Sheep;

internal static class Program
{
    private static int checks;
    private static int failed;

    private static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            uint seed; int level;
            if (args.Length != 3 || args[0] != "--fixture" || !uint.TryParse(args[1], out seed) || !int.TryParse(args[2], out level) || level < 1 || level > 2)
            {
                Console.Error.WriteLine("Usage: test-sheep.sh [--fixture <uint seed> <level 1|2>]");
                return 2;
            }
            Console.WriteLine(FixtureJson(level, seed));
            return 0;
        }
        Run("legacy layouts retain bounded nonoverlapping geometry", Layouts);
        Run("generator is deterministic and balanced for all variants", Generation);
        Run("real game replays 600 generated solution witnesses", Witnesses);
        Run("snapshot arrays and game ownership are defensive", Copies);
        Run("malformed external snapshots are rejected", InvalidSnapshots);
        Run("invalid level and layout arguments are rejected", delegate {
            Throws<ArgumentOutOfRangeException>(delegate { SheepBoardLayout.GetPositions(0, 0); });
            Throws<ArgumentOutOfRangeException>(delegate { SheepBoardLayout.GetPositions(1, 3); });
            Throws<ArgumentOutOfRangeException>(delegate { new SheepGame(3, 1); });
            Throws<ArgumentNullException>(delegate { new SheepGame((SheepSnapshot)null); });
        });
        Run("only exposed board and reserve cards can be picked", Blocking);
        Run("any overlapping higher layer blocks including blind stacks", HigherLayers);
        Run("seventh card matches before full-tray loss", SeventhMatch);
        Run("seven unmatched cards lose and reject more picks", Loss);
        Run("tray insertion groups equal kinds and removes exact triple", TrayOrder);
        Run("remove can repeat and preserves first-three ordering", Remove);
        Run("remove handles a tray with fewer than three cards", RemoveSmall);
        Run("remove is bounded by reserve space instead of usage count", RemoveCapacity);
        Run("undo is reusable after every new eligible pick", Undo);
        Run("undo preserves reserve order and restores source ownership", UndoReserve);
        Run("matching selection cannot be undone", UndoMatch);
        Run("remove and undo can rescue full tray", Rescue);
        Run("revive is separate from remove and available once after loss", Revive);
        Run("repeated remove rescues keep revival independent", RepeatedRescue);
        Run("shuffle preserves fixed cards and board multiset", Shuffle);
        Run("invalid commands are atomic including RNG", InvalidActions);
        Run("snapshot round trips preserve continuation and RNG", Continuation);
        Run("win requires clearing board tray and reserve", Win);
        Run("won game rejects all actions", WonActions);
        Run("reserve undo metadata respects reusable tool capacity", ReserveUndoCapacity);
        Run("board undo cannot erase the history required by tool flags", UndoHistoryFlags);
        Run("tutorial snapshots cannot claim a revival", TutorialRevival);
        Run("overlap blocks across nonadjacent layers but touching edges do not", ExactOverlap);
        Run("random action sequences preserve restorable state", RandomActions);
        Run("fixture export contains complete snapshot solution and geometry", FixtureExport);
        Console.WriteLine("Sheep checks: " + checks + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void Layouts()
    {
        for (int level = 1; level <= 2; level++)
        for (int variant = 0; variant < 3; variant++)
        {
            var positions = SheepBoardLayout.GetPositions(level, variant);
            Assert(positions.Length == (level == 1 ? 18 : 144), "card count");
            int left = 0, right = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                var p = positions[i];
                Assert(p.Id == i && p.X >= 0 && p.X <= 14 && p.Y >= 0 && p.Y <= 12 && p.Layer >= 0, "geometry bounds");
                Assert(p.BlindStack >= 0 && p.BlindStack <= 2, "blind stack index");
                if (p.BlindStack == 1) left++;
                if (p.BlindStack == 2) right++;
                for (int j = 0; j < i; j++)
                    Assert(p.Layer != positions[j].Layer || !Overlaps(p, positions[j]), "same layer cards overlap");
            }
            if (level == 2) Assert(left >= 3 && right >= 3, "challenge has two blind stacks");
            positions[0].X = 100;
            Assert(SheepBoardLayout.GetPositions(level, variant)[0].X != 100, "layout must not expose mutable shared positions");
        }
    }

    private static void Generation()
    {
        var variants = new[] { new HashSet<int>(), new HashSet<int>() };
        for (int level = 1; level <= 2; level++)
        for (uint seed = 0; seed < 300; seed++)
        {
            var a = SheepLevelGenerator.Generate(level, seed);
            var b = SheepLevelGenerator.Generate(level, seed);
            Same(a.Snapshot, b.Snapshot, "seed determinism");
            Equal(a.SolutionIds, b.SolutionIds, "witness determinism");
            Valid(a.Snapshot);
            variants[level - 1].Add(a.Snapshot.LayoutId);
            Assert(level == 1 || a.Snapshot.LayoutId >= SheepBoardLayout.NewLayoutStart, "new challenges use the separated layouts");
            Assert(a.Snapshot.RandomState != 0 && a.Snapshot.Tray.Length == 0 && a.Snapshot.Reserve.Length == 0, "fresh state");
            int[] kinds = new int[level == 1 ? 3 : 16];
            foreach (int kind in a.Snapshot.Kinds) kinds[kind]++;
            foreach (int count in kinds) Assert(count == (level == 1 ? 6 : 9), "balanced kinds");
        }
        Assert(variants[0].Count == 3, "all tutorial layouts are selected");
        Assert(variants[1].Count == SheepBoardLayout.ChallengeLayoutCount - SheepBoardLayout.NewLayoutStart, "all new challenge layouts are selected");
    }

    private static void Witnesses()
    {
        int largestTray = 0;
        for (int level = 1; level <= 2; level++)
        for (uint seed = 0; seed < 300; seed++)
        {
            var generated = SheepLevelGenerator.Generate(level, seed);
            var game = new SheepGame(generated.Snapshot);
            var picked = new HashSet<int>();
            foreach (int id in generated.SolutionIds)
            {
                Assert(picked.Add(id) && game.CanPick(id), "witness must use each legal exposed tile once");
                var move = game.Pick(id);
                Assert(move.Changed && move.PickedId == id && move.Phase != SheepPhase.Lost, "witness cannot lose");
                largestTray = Math.Max(largestTray, game.GetSnapshot().Tray.Length);
                Assert(game.GetSnapshot().Tray.Length <= 6, "settled witness tray capacity");
                Valid(game.GetSnapshot());
            }
            Assert(game.Phase == SheepPhase.Won && game.RemainingCount == 0 && game.ClearedCount == generated.SolutionIds.Length, "witness clears all cards");
        }
        Assert(largestTray >= 5, "generation must interleave triples rather than always emit three identical cards");
    }

    private static void Copies()
    {
        var source = SheepLevelGenerator.Generate(2, 33).Snapshot;
        var game = new SheepGame(source);
        var before = game.GetSnapshot();
        source.Kinds[0] = 99; source.Locations[0] = SheepLocation.Removed;
        var exported = game.GetSnapshot(); exported.Kinds[1] = 99;
        Same(before, game.GetSnapshot(), "import and export arrays are private");
        var fixture = Fixture(new[] { 0, 1 }, new[] { 2, 3 });
        var copy = fixture.Copy(); copy.Tray[0] = -1; copy.Reserve[0] = -1;
        Assert(fixture.Tray[0] >= 0 && fixture.Reserve[0] >= 0, "tray and reserve are deep copied");
    }

    private static void InvalidSnapshots()
    {
        Invalid(delegate(SheepSnapshot s) { s.Level = 0; });
        Invalid(delegate(SheepSnapshot s) { s.Level = 3; });
        Invalid(delegate(SheepSnapshot s) { s.LayoutId = -1; });
        Invalid(delegate(SheepSnapshot s) { s.LayoutId = SheepBoardLayout.ChallengeLayoutCount; });
        Invalid(delegate(SheepSnapshot s) { s.RandomState = 0; });
        Invalid(delegate(SheepSnapshot s) { s.Kinds = null; });
        Invalid(delegate(SheepSnapshot s) { s.Locations = null; });
        Invalid(delegate(SheepSnapshot s) { s.Tray = null; });
        Invalid(delegate(SheepSnapshot s) { s.Reserve = null; });
        Invalid(delegate(SheepSnapshot s) { s.Kinds = new int[1]; });
        Invalid(delegate(SheepSnapshot s) { s.Locations = new SheepLocation[1]; });
        Invalid(delegate(SheepSnapshot s) { s.Kinds[0] = -1; });
        Invalid(delegate(SheepSnapshot s) { s.Kinds[0] = 16; });
        Invalid(delegate(SheepSnapshot s) { s.Kinds[0] = (s.Kinds[0] + 1) % 16; });
        Invalid(delegate(SheepSnapshot s) { s.Locations[0] = (SheepLocation)4; });
        Invalid(delegate(SheepSnapshot s) { s.Locations[0] = SheepLocation.Tray; });
        Invalid(delegate(SheepSnapshot s) { s.Locations[0] = SheepLocation.Reserve; });
        Invalid(delegate(SheepSnapshot s) { s.Locations[0] = SheepLocation.Removed; });
        Invalid(delegate(SheepSnapshot s) { s.Tray = new[] { 0 }; });
        Invalid(delegate(SheepSnapshot s) { s.Tray = new[] { -1 }; });
        Invalid(delegate(SheepSnapshot s) { s.Reserve = new[] { 144 }; });
        Invalid(delegate(SheepSnapshot s) { s.LastPickedId = 0; });
        Invalid(delegate(SheepSnapshot s) { s.LastSource = 0; });
        Invalid(delegate(SheepSnapshot s) { s.LastReserveIndex = 0; });
        var duplicate = Fixture(new[] { 0, 1 }, new int[0]); duplicate.Tray[1] = duplicate.Tray[0]; Invalid(duplicate);
        var fullMatch = Fixture(new[] { 0, 0, 0 }, new int[0]); Invalid(fullMatch);
        var reserve = Fixture(new int[0], new[] { 0 }); reserve.RemoveUsed = false; Invalid(reserve);
        var tooMany = Fixture(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, new int[0]); Invalid(tooMany);
        var impossibleUndo = Fixture(new[] { 0 }, new int[0]); impossibleUndo.LastPickedId = impossibleUndo.Tray[0]; impossibleUndo.LastSource = 2; impossibleUndo.LastReserveIndex = 10; Invalid(impossibleUndo);
    }

    private static void Blocking()
    {
        var game = new SheepGame(2, 8);
        var snapshot = game.GetSnapshot(); var positions = SheepBoardLayout.GetPositions(2, snapshot.LayoutId);
        int blocked = 0, exposed = 0;
        for (int i = 0; i < positions.Length; i++)
        {
            bool expected = true;
            foreach (var other in positions) if (other.Layer > positions[i].Layer && Overlaps(other, positions[i])) expected = false;
            Assert(game.CanPick(i) == expected, "pickability follows geometric overlap");
            if (expected) exposed++; else { blocked++; Assert(!game.Pick(i).Changed, "blocked picks cannot mutate"); }
        }
        Assert(blocked > 0 && exposed > 0, "layout has exposed and covered cards");
        Assert(!game.CanPick(-1) && !game.CanPick(positions.Length), "unknown card ids are invalid");
    }

    private static void HigherLayers()
    {
        var state = SheepLevelGenerator.Generate(2, 1).Snapshot;
        var positions = SheepBoardLayout.GetPositions(2, state.LayoutId);
        int low = -1, top = -1;
        foreach (var p in positions) if (p.BlindStack == 1) { if (low < 0 || p.Layer < positions[low].Layer) low = p.Id; if (top < 0 || p.Layer > positions[top].Layer) top = p.Id; }
        var game = new SheepGame(state);
        Assert(low >= 0 && top >= 0 && !game.CanPick(low) && game.CanPick(top), "only blind stack top is available");
        Assert(positions[top].Layer > positions[low].Layer + 1, "stack exercises nonadjacent layers");
        var result = game.Pick(top); Assert(result.Changed && !game.CanPick(low), "removing top does not uncover entire blind stack");
    }

    private static void SeventhMatch()
    {
        var s = Fixture(new[] { 0, 0, 1, 1, 2, 2 }, new int[0]); int id = ExposedOfKind(s, 0);
        var game = new SheepGame(s); var move = game.Pick(id);
        Assert(move.Changed && move.MatchedIds.Length == 3 && game.Phase == SheepPhase.Playing && game.GetSnapshot().Tray.Length == 4, "seventh matching card clears before loss");
        Assert(game.ClearedCount == 3 && game.RemainingCount == 141, "matched cards count as cleared"); Valid(game.GetSnapshot());
    }

    private static void Loss()
    {
        var s = Fixture(new[] { 0, 1, 2, 3, 4, 5 }, new int[0]); int id = ExposedOfKind(s, 6);
        var game = new SheepGame(s); Assert(game.Pick(id).Phase == SheepPhase.Lost, "seventh distinct kind loses");
        var before = game.GetSnapshot(); Assert(!game.Pick(0).Changed, "loss disallows further pick");
        Same(before, game.GetSnapshot(), "rejected loss pick is atomic"); Valid(before);
    }

    private static void TrayOrder()
    {
        var s = Fixture(new[] { 0, 1, 2 }, new int[0]); int id = ExposedOfKind(s, 1); var game = new SheepGame(s);
        game.Pick(id); var after = game.GetSnapshot(); Assert(after.Tray[2] == id && after.Kinds[after.Tray[3]] == 2, "insert adjacent to matching kind");
        int third = ExposedOfKind(after, 1); game = new SheepGame(after); var move = game.Pick(third);
        Assert(move.MatchedIds.Length == 3 && Array.IndexOf(move.MatchedIds, id) >= 0 && Array.IndexOf(move.MatchedIds, third) >= 0, "correct triple ids reported");
        Assert(game.GetSnapshot().Tray.Length == 2 && game.GetSnapshot().Kinds[game.GetSnapshot().Tray[0]] == 0, "unmatched order preserved");
    }

    private static void Remove()
    {
        var s = Fixture(new[] { 0, 1, 2, 3, 4 }, new int[0]); var game = new SheepGame(s);
        Assert(game.CanUseTool(SheepTool.RemoveThree) && game.UseTool(SheepTool.RemoveThree), "remove available");
        var after = game.GetSnapshot(); Equal(new[] { s.Tray[0], s.Tray[1], s.Tray[2] }, after.Reserve, "first three move");
        Assert(after.Tray.Length == 2 && after.RemoveUsed && after.LastPickedId == -1, "remove state");
        Assert(game.UseTool(SheepTool.RemoveThree), "a prior remove does not consume future uses");
        var twice = game.GetSnapshot();
        Equal(s.Tray, twice.Reserve, "repeated removes append in original order");
        Assert(twice.Tray.Length == 0 && twice.RemoveUsed && !game.CanUseTool(SheepTool.RemoveThree), "empty tray still disallows remove");
        foreach (int id in twice.Reserve) Assert(game.CanPick(id), "reserve may be picked independent of layers"); Valid(twice);
    }

    private static void RemoveSmall()
    {
        var game = new SheepGame(Fixture(new[] { 0, 1 }, new int[0]));
        Assert(game.UseTool(SheepTool.RemoveThree) && game.GetSnapshot().Reserve.Length == 2 && game.GetSnapshot().Tray.Length == 0, "remove moves up to three");
    }

    private static void RemoveCapacity()
    {
        var game = new SheepGame(Fixture(new[] { 6, 7, 8 }, new[] { 0, 1, 2, 3, 4, 5 }));
        var full = game.GetSnapshot();
        Assert(!game.CanUseTool(SheepTool.RemoveThree) && !game.UseTool(SheepTool.RemoveThree), "full reserve rejects another move");
        Same(full, game.GetSnapshot(), "capacity rejection is atomic");
        for (int i = 0; i < 3; i++) Assert(game.Pick(full.Reserve[i]).Changed, "reserve card returns to tray");
        Assert(game.CanUseTool(SheepTool.RemoveThree) && game.UseTool(SheepTool.RemoveThree), "freeing reserve space enables another use");
        Assert(game.GetSnapshot().Reserve.Length == 6 && game.GetSnapshot().Tray.Length == 3, "six-card reserve remains bounded");
        Valid(game.GetSnapshot());
    }

    private static void Undo()
    {
        var game = new SheepGame(2, 5); var before = game.GetSnapshot(); int id = FirstPickable(game);
        game.Pick(id); Assert(game.CanUseTool(SheepTool.Undo) && game.UseTool(SheepTool.Undo), "undo available after pick");
        var after = game.GetSnapshot(); Assert(after.Locations[id] == SheepLocation.Board && after.Tray.Length == 0 && after.UndoUsed, "undo puts card on original board location");
        Equal(before.Kinds, after.Kinds, "undo preserves kinds"); Assert(!game.UseTool(SheepTool.Undo), "no earlier history is invented"); Valid(after);
        for (int cycle = 0; cycle < 8; cycle++)
        {
            Assert(game.Pick(id).Changed, "same restored board card remains selectable");
            game = new SheepGame(game.GetSnapshot());
            Assert(game.CanUseTool(SheepTool.Undo) && game.UseTool(SheepTool.Undo), "used flag does not disable future undo");
            Assert(game.GetSnapshot().Locations[id] == SheepLocation.Board && game.GetSnapshot().Tray.Length == 0, "each undo returns latest pick");
            Valid(game.GetSnapshot());
        }
    }

    private static void UndoReserve()
    {
        var s = Fixture(new[] { 4 }, new[] { 0, 1, 2 }); var game = new SheepGame(s); int id = s.Reserve[1];
        game.Pick(id); var picked = game.GetSnapshot();
        Assert(picked.LastSource == 2 && picked.LastReserveIndex == 1, "reserve source recorded");
        game = new SheepGame(picked); Assert(game.UseTool(SheepTool.Undo), "undo survives save restore");
        Equal(s.Reserve, game.GetSnapshot().Reserve, "original reserve order restored"); Valid(game.GetSnapshot());
    }

    private static void UndoMatch()
    {
        var s = Fixture(new[] { 0, 0 }, new int[0]); int id = ExposedOfKind(s, 0); var game = new SheepGame(s);
        Assert(game.Pick(id).MatchedIds.Length == 3 && !game.CanUseTool(SheepTool.Undo) && game.GetSnapshot().LastPickedId == -1, "matching move invalidates one-step undo");
    }

    private static void Rescue()
    {
        var s = Fixture(new[] { 0, 1, 2, 3, 4, 5 }, new int[0]); int id = ExposedOfKind(s, 6);
        var game = new SheepGame(s); game.Pick(id); Assert(game.UseTool(SheepTool.Undo) && game.Phase == SheepPhase.Playing && game.GetSnapshot().Tray.Length == 6, "undo rescues loss");
        game = new SheepGame(s); game.Pick(id); Assert(game.UseTool(SheepTool.RemoveThree) && game.Phase == SheepPhase.Playing && game.GetSnapshot().Tray.Length == 4, "remove rescues loss");
    }

    private static void Revive()
    {
        var s = Fixture(new[] { 0, 1, 2, 3, 4, 5, 6 }, new int[0]); var game = new SheepGame(s);
        Assert(game.CanUseTool(SheepTool.Revive) && game.UseTool(SheepTool.Revive), "revive available after loss");
        var revived = game.GetSnapshot(); Assert(revived.ReviveUsed && !revived.RemoveUsed && revived.Reserve.Length == 3 && revived.Tray.Length == 4, "revive keeps separate remove entitlement");
        Assert(game.UseTool(SheepTool.RemoveThree) && game.GetSnapshot().Reserve.Length == 6, "remove can follow revive to cap six");
        Assert(!game.UseTool(SheepTool.Revive), "revive once"); Valid(game.GetSnapshot());
        Assert(!new SheepGame(2, 4).UseTool(SheepTool.Revive), "no revive during play");
    }

    private static void RepeatedRescue()
    {
        var game = new SheepGame(Fixture(new[] { 0, 1, 2, 3, 4, 5, 6 }, new int[0]));
        for (int cycle = 0; cycle < 5; cycle++)
        {
            Assert(game.Phase == SheepPhase.Lost && game.UseTool(SheepTool.RemoveThree), "remove rescues every full tray when space exists");
            var reserve = game.GetSnapshot().Reserve;
            Assert(reserve.Length == 3 && !game.GetSnapshot().ReviveUsed, "remove never consumes revival");
            foreach (int id in reserve) Assert(game.Pick(id).Changed, "return unique reserve cards to full tray");
            Valid(game.GetSnapshot());
        }
        Assert(game.UseTool(SheepTool.Revive), "revival remains available after repeated removes");
        Assert(game.UseTool(SheepTool.RemoveThree), "move remains available after revival if capacity allows");
        Assert(game.GetSnapshot().Reserve.Length == 6 && game.GetSnapshot().ReviveUsed, "combined reserve is bounded at six");
        Assert(!game.UseTool(SheepTool.Revive), "revival still has one use");
        Valid(game.GetSnapshot());
    }

    private static void Shuffle()
    {
        var s = Fixture(new[] { 0, 1 }, new[] { 2, 3 }); var game = new SheepGame(s);
        Assert(game.UseTool(SheepTool.Shuffle), "shuffle succeeds"); var after = game.GetSnapshot();
        Assert(after.ShuffleUsed && after.RandomState != s.RandomState && after.LastPickedId == -1, "shuffle uses RNG and clears undo");
        Equal(s.Tray, after.Tray, "tray remains"); Equal(s.Reserve, after.Reserve, "reserve remains");
        var beforeKinds = new List<int>(); var afterKinds = new List<int>();
        for (int i = 0; i < s.Kinds.Length; i++) { Assert(s.Locations[i] == after.Locations[i], "positions remain"); if (s.Locations[i] == SheepLocation.Board) { beforeKinds.Add(s.Kinds[i]); afterKinds.Add(after.Kinds[i]); } else Assert(s.Kinds[i] == after.Kinds[i], "non-board kind unchanged"); }
        beforeKinds.Sort(); afterKinds.Sort(); Equal(beforeKinds.ToArray(), afterKinds.ToArray(), "board multiset preserved");
        for (int repeat = 0; repeat < 8; repeat++)
        {
            var previous = game.GetSnapshot();
            game = new SheepGame(previous);
            Assert(game.CanUseTool(SheepTool.Shuffle) && game.UseTool(SheepTool.Shuffle), "shuffle stays available after use and restore");
            var next = game.GetSnapshot();
            Assert(next.RandomState != previous.RandomState, "each shuffle advances the saved RNG");
            var currentKinds = new List<int>();
            for (int i = 0; i < next.Kinds.Length; i++)
                if (next.Locations[i] == SheepLocation.Board) currentKinds.Add(next.Kinds[i]);
                else Assert(next.Kinds[i] == s.Kinds[i], "repeated shuffle leaves fixed kinds untouched");
            currentKinds.Sort(); Equal(beforeKinds.ToArray(), currentKinds.ToArray(), "every shuffle preserves inventory");
            Equal(s.Tray, next.Tray, "every shuffle preserves tray"); Equal(s.Reserve, next.Reserve, "every shuffle preserves reserve");
            Valid(next);
        }
        Assert(!new SheepGame(Fixture(new[] { 0, 1, 2, 3, 4, 5, 6 }, new int[0])).UseTool(SheepTool.Shuffle), "shuffle cannot rescue full tray");
    }

    private static void InvalidActions()
    {
        var game = new SheepGame(2, 19); var before = game.GetSnapshot();
        Assert(!game.UseTool(SheepTool.RemoveThree) && !game.UseTool(SheepTool.Undo) && !game.UseTool(SheepTool.Revive) && !game.UseTool((SheepTool)99), "unavailable tools reject");
        Assert(!game.Pick(-1).Changed && !game.Pick(int.MaxValue).Changed && !game.CanUseTool((SheepTool)(-1)), "invalid ids reject");
        Same(before, game.GetSnapshot(), "invalid actions unchanged");
        int id = FirstPickable(game); game.Pick(id); before = game.GetSnapshot(); Assert(!game.Pick(id).Changed, "cannot pick tray tile again"); Same(before, game.GetSnapshot(), "duplicate pick unchanged");
    }

    private static void Continuation()
    {
        var generated = SheepLevelGenerator.Generate(2, 143); var first = new SheepGame(generated.Snapshot);
        for (int i = 0; i < 47; i++) first.Pick(generated.SolutionIds[i]);
        var second = new SheepGame(first.GetSnapshot());
        for (int i = 47; i < generated.SolutionIds.Length; i++) { first.Pick(generated.SolutionIds[i]); second.Pick(generated.SolutionIds[i]); Same(first.GetSnapshot(), second.GetSnapshot(), "restored witness continuation"); }
        var a = new SheepGame(2, 91); a.Pick(FirstPickable(a)); var b = new SheepGame(a.GetSnapshot()); a.UseTool(SheepTool.Shuffle); b.UseTool(SheepTool.Shuffle); Same(a.GetSnapshot(), b.GetSnapshot(), "saved RNG continues identically");
    }

    private static void Win()
    {
        var generated = SheepLevelGenerator.Generate(1, 29); var game = new SheepGame(generated.Snapshot);
        foreach (int id in generated.SolutionIds) { game.Pick(id); if (game.GetSnapshot().Tray.Length > 0 && !game.GetSnapshot().RemoveUsed) { Assert(game.UseTool(SheepTool.RemoveThree), "move witness tile to reserve"); int reserve = game.GetSnapshot().Reserve[0]; game.Pick(reserve); } }
        Assert(game.Phase == SheepPhase.Won && game.GetSnapshot().Reserve.Length == 0, "all areas empty wins"); Valid(game.GetSnapshot());
        var last = generated.Snapshot.Copy(); for (int i = 0; i < last.Locations.Length; i++) last.Locations[i] = SheepLocation.Removed;
        var ids = new List<int>(); for (int i = 0; i < last.Kinds.Length && ids.Count < 3; i++) if (last.Kinds[i] == 0) ids.Add(i);
        foreach (int id in ids) last.Locations[id] = SheepLocation.Reserve;
        last.Reserve = ids.ToArray(); last.RemoveUsed = true; var reserveGame = new SheepGame(last);
        Assert(reserveGame.Phase == SheepPhase.Playing && reserveGame.RemainingCount == 3, "empty board with reserve is not won");
        foreach (int id in ids) reserveGame.Pick(id); Assert(reserveGame.Phase == SheepPhase.Won, "reserve triple can finish game");
    }

    private static void WonActions()
    {
        var g = SheepLevelGenerator.Generate(1, 1); var game = new SheepGame(g.Snapshot); foreach (int id in g.SolutionIds) game.Pick(id);
        var before = game.GetSnapshot(); foreach (SheepTool tool in Enum.GetValues(typeof(SheepTool))) Assert(!game.UseTool(tool) && !game.CanUseTool(tool), "no tools after win");
        Assert(!game.Pick(0).Changed, "no pick after win"); Same(before, game.GetSnapshot(), "won state remains unchanged");
    }

    private static void ReserveUndoCapacity()
    {
        var s = Fixture(new[] { 4, 5, 6, 7 }, new[] { 0, 1, 2 });
        s.RemoveUsed = false; s.ReviveUsed = true;
        s.LastPickedId = s.Tray[0]; s.LastSource = 2; s.LastReserveIndex = 0;
        Invalid(s);
        var six = Fixture(new[] { 6 }, new[] { 0, 1, 2, 3, 4, 5 });
        six.LastPickedId = six.Tray[0]; six.LastSource = 2; six.LastReserveIndex = 3;
        Invalid(six);
        var five = Fixture(new[] { 6 }, new[] { 0, 1, 2, 3, 4 });
        five.LastPickedId = five.Tray[0]; five.LastSource = 2; five.LastReserveIndex = 3;
        Valid(five);
        var game = new SheepGame(five);
        Assert(game.UseTool(SheepTool.Undo) && game.GetSnapshot().Reserve.Length == 6, "return to reusable move reserve may fill sixth space");
    }

    private static void TutorialRevival()
    {
        var g = SheepLevelGenerator.Generate(1, 1); var game = new SheepGame(g.Snapshot);
        foreach (int id in g.SolutionIds) game.Pick(id);
        var s = game.GetSnapshot(); s.ReviveUsed = true; Invalid(s);
    }

    private static void UndoHistoryFlags()
    {
        var game = new SheepGame(2, 19);
        Assert(game.Pick(FirstPickable(game)).Changed, "pick fixture card");
        var state = game.GetSnapshot(); state.RemoveUsed = true;
        Invalid(state);
        state = Fixture(new[] { 0, 1, 2, 3, 4, 5, 6 }, new int[0]);
        state.ReviveUsed = true; state.LastPickedId = state.Tray[6]; state.LastSource = 0;
        Invalid(state);
    }

    private static void ExactOverlap()
    {
        var p = new[] { new SheepPosition { Id = 0, X = 4, Y = 4, Layer = 0 }, new SheepPosition { Id = 1, X = 5, Y = 5, Layer = 8 } };
        var locations = new[] { SheepLocation.Board, SheepLocation.Board };
        Assert(!SheepBoardLayout.IsExposed(p, locations, 0), "partial corner overlap from distant layer blocks");
        p[1].X = 6; Assert(SheepBoardLayout.IsExposed(p, locations, 0), "horizontal edge touch does not cover");
        p[1].X = 5; p[1].Y = 6; Assert(SheepBoardLayout.IsExposed(p, locations, 0), "vertical edge touch does not cover");
        p[1].Y = 5; locations[1] = SheepLocation.Tray; Assert(SheepBoardLayout.IsExposed(p, locations, 0), "non-board cards never block");
    }

    private static void RandomActions()
    {
        for (int level = 1; level <= 2; level++)
        for (uint seed = 1; seed <= 80; seed++)
        {
            var random = new Random((int)seed); var game = new SheepGame(level, seed);
            for (int step = 0; step < 300 && game.Phase != SheepPhase.Won; step++)
            {
                var ids = new List<int>(); var available = new List<SheepTool>();
                for (int id = 0; id < game.GetSnapshot().Kinds.Length; id++) if (game.CanPick(id)) ids.Add(id);
                foreach (SheepTool tool in Enum.GetValues(typeof(SheepTool))) if (game.CanUseTool(tool)) available.Add(tool);
                if (ids.Count == 0 && available.Count == 0) break;
                if (available.Count > 0 && (ids.Count == 0 || random.Next(4) == 0)) game.UseTool(available[random.Next(available.Count)]);
                else game.Pick(ids[random.Next(ids.Count)]);
                Valid(game.GetSnapshot());
                var restored = new SheepGame(game.GetSnapshot()); Same(game.GetSnapshot(), restored.GetSnapshot(), "all reachable actions remain restorable");
                Assert(game.Phase == restored.Phase && game.RemainingCount + game.ClearedCount == (level == 1 ? 18 : 144), "derived counters survive restore");
                game = restored;
            }
        }
    }

    private static void FixtureExport()
    {
        using (var json = JsonDocument.Parse(FixtureJson(2, 17)))
        {
            Assert(json.RootElement.GetProperty("Snapshot").GetProperty("Level").GetInt32() == 2, "export level");
            Assert(json.RootElement.GetProperty("Snapshot").GetProperty("Kinds").GetArrayLength() == 144, "export kinds");
            Assert(json.RootElement.GetProperty("SolutionIds").GetArrayLength() == 144 && json.RootElement.GetProperty("Positions").GetArrayLength() == 144, "export solution and geometry");
        }
    }

    private static string FixtureJson(int level, uint seed)
    {
        var generated = SheepLevelGenerator.Generate(level, seed);
        return JsonSerializer.Serialize(new {
            Snapshot = generated.Snapshot,
            SolutionIds = generated.SolutionIds,
            Positions = SheepBoardLayout.GetPositions(level, generated.Snapshot.LayoutId)
        }, new JsonSerializerOptions { IncludeFields = true });
    }

    private static SheepSnapshot Fixture(int[] trayKinds, int[] reserveKinds)
    {
        var s = SheepLevelGenerator.Generate(2, 1234).Snapshot;
        var tray = new List<int>(); var reserve = new List<int>();
        foreach (int kind in trayKinds) { int id = ExposedOfKind(s, kind); s.Locations[id] = SheepLocation.Tray; tray.Add(id); }
        foreach (int kind in reserveKinds) { int id = ExposedOfKind(s, kind); s.Locations[id] = SheepLocation.Reserve; reserve.Add(id); }
        s.Tray = tray.ToArray(); s.Reserve = reserve.ToArray(); s.RemoveUsed = reserve.Count > 0;
        return s;
    }

    // Keep the complete kind multiset while placing the desired kind on an exposed board tile.
    private static int ExposedOfKind(SheepSnapshot s, int kind)
    {
        var positions = SheepBoardLayout.GetPositions(s.Level, s.LayoutId); int chosen = -1;
        foreach (var p in positions)
        {
            if (s.Locations[p.Id] != SheepLocation.Board) continue;
            bool blocked = false; foreach (var q in positions) if (s.Locations[q.Id] == SheepLocation.Board && q.Layer > p.Layer && Overlaps(p, q)) blocked = true;
            if (!blocked) { chosen = p.Id; break; }
        }
        Assert(chosen >= 0, "fixture has an exposed board tile");
        for (int i = 0; i < s.Kinds.Length; i++) if (s.Locations[i] == SheepLocation.Board && s.Kinds[i] == kind) { int swap = s.Kinds[chosen]; s.Kinds[chosen] = kind; s.Kinds[i] = swap; return chosen; }
        throw new InvalidOperationException("fixture kind exhausted");
    }

    private static int FirstPickable(SheepGame game) { for (int i = 0; i < game.GetSnapshot().Kinds.Length; i++) if (game.CanPick(i)) return i; throw new InvalidOperationException("no available tile"); }
    private static bool Overlaps(SheepPosition a, SheepPosition b) { return Math.Abs(a.X - b.X) < 2 && Math.Abs(a.Y - b.Y) < 2; }
    private static void Valid(SheepSnapshot s) { string error; Assert(s.TryValidate(out error), error); }
    private static void Invalid(Action<SheepSnapshot> change) { var s = SheepLevelGenerator.Generate(2, 9).Snapshot; change(s); Invalid(s); }
    private static void Invalid(SheepSnapshot s) { string error; Assert(!s.TryValidate(out error) && !string.IsNullOrEmpty(error), "corrupt snapshot must be rejected"); Throws<ArgumentException>(delegate { new SheepGame(s); }); }
    private static void Same(SheepSnapshot a, SheepSnapshot b, string message)
    {
        Assert(a.Level == b.Level && a.LayoutId == b.LayoutId && a.RandomState == b.RandomState && a.RemoveUsed == b.RemoveUsed && a.UndoUsed == b.UndoUsed && a.ShuffleUsed == b.ShuffleUsed && a.ReviveUsed == b.ReviveUsed && a.LastPickedId == b.LastPickedId && a.LastSource == b.LastSource && a.LastReserveIndex == b.LastReserveIndex, message);
        Equal(a.Kinds, b.Kinds, message); Equal(a.Tray, b.Tray, message); Equal(a.Reserve, b.Reserve, message);
        Assert(a.Locations.Length == b.Locations.Length, message); for (int i = 0; i < a.Locations.Length; i++) Assert(a.Locations[i] == b.Locations[i], message);
    }
    private static void Equal(int[] a, int[] b, string message) { Assert(a.Length == b.Length, message); for (int i = 0; i < a.Length; i++) Assert(a[i] == b[i], message); }
    private static void Run(string name, Action test) { try { test(); checks++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
}
