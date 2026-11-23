using System;
using System.Collections.Generic;
using System.Linq;
using XanaduYonder.Core.Feihualing;

internal static class Program
{
    private static int checks;

    private static void Main()
    {
        var catalog = CreateCatalog();
        RuleFeedback(catalog);
        DecoratedPlayerInput(catalog);
        EveryCombination(catalog);
        PlaySoloChallenge(catalog);
        PlayDuoMatch(catalog);
        CompetitionRelay(catalog);
        Exhaustion(catalog);
        LearningHelp(catalog);
        MaskedHints(catalog);
        CompetitiveHintBudget(catalog);
        CompanionPreservesNextPosition();
        ValidateRestore(catalog);
        PreserveOldCatalogPrefix();
        RetiredCatalogLines();
        ValidateCatalog();
        Console.WriteLine("Feihualing core: " + checks + " assertions passed.");
    }

    private static PoetryCatalog CreateCatalog()
    {
        var stanzas = new[]
        {
            "床前明月光，疑是地上霜。举头望明月，低头思故乡。",
            "月出惊山鸟，明月松间照。松间月下泉，风清雨月明。海上生明月。",
            "夜来风雨声，花落知多少。风住尘香花已尽，日晚倦梳头。",
            "昨夜雨疏风骤，浓睡不消残酒。",
            "常记溪亭日暮，争渡、争渡，惊起一滩鸥鹭。",
            "𠮷月照空山，举头望山月。",
            "低头思故乡。",
            "山月照清泉。",
            "白云依山月。"
        };
        var works = new List<PoetryWork>();
        var lines = new List<PoetryLine>();
        var groups = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var work = 0; work < stanzas.Length; work++)
        {
            works.Add(new PoetryWork("w" + work, "测试作者", "测试篇" + work, stanzas[work]));
            var start = 0;
            foreach (var text in stanzas[work].Split(new[] { '，', '。' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = lines.Count;
                if (!groups.TryGetValue(text, out var group))
                    groups.Add(text, group = groups.Count);
                var aliases = line == 0 ? new[] { "牀前明月光", "床前明日光" }
                    : text == "争渡、争渡" ? new[] { "争渡争渡" } : Array.Empty<string>();
                lines.Add(new PoetryLine(work, start, text.Length, group, line != 22, aliases));
                start += text.Length + 1;
            }
        }

        var cards = new[]
        {
            new PoetryCard("moon", PoetryRule.Character, "月", "月", "", 0,
                new[] { new[] { 0, 2, 4, 5, 6, 7, 8, 18, 19, 21, 22 } }, null),
            new PoetryCard("position", PoetryRule.Position, "月五言", "月", "", 5,
                new[] { new[] { 4 }, new[] { 5, 18, 21 }, new[] { 6 }, new[] { 0, 7 }, new[] { 2, 8, 19, 22 } }, null),
            new PoetryCard("wind-rain", PoetryRule.Pair, "风雨", "风", "雨", 0,
                new[] { new[] { 7, 9, 13 } }, null),
            new PoetryCard("homesick", PoetryRule.Theme, "思乡", "", "", 0,
                new[] { new[] { 2, 20 } }, new Dictionary<int, string> { { 2, "上下文解释" }, { 20, "直接思念故乡" } }),
            new PoetryCard("home", PoetryRule.Character, "乡", "乡", "", 0,
                new[] { new[] { 3, 20 } }, null),
            new PoetryCard("crossing", PoetryRule.Character, "渡", "渡", "", 0,
                new[] { new[] { 16 } }, null),
            new PoetryCard("rare", PoetryRule.Character, "白", "白", "", 0,
                new[] { new[] { 22 } }, null),
            new PoetryCard("selected", PoetryRule.Character, "月", "月", "", 0,
                new[] { new[] { 0 } }, null),
            new PoetryCard("alias-only", PoetryRule.Character, "日", "日", "", 0,
                new[] { new[] { 0 } }, null),
            new PoetryCard("rare-pair", PoetryRule.Character, "月", "月", "", 0,
                new[] { new[] { 0, 22 } }, null)
        };
        return new PoetryCatalog("fixture", works.ToArray(), lines.ToArray(), cards);
    }

    private static PoetrySession Game(PoetryCatalog catalog, string id, PoetryPlayers players = PoetryPlayers.Solo,
        PoetryGoal goal = PoetryGoal.Learning, long now = 0, int starter = 0)
    {
        return PoetrySession.Start(catalog, catalog.FindCard(id), players, goal, now, starter);
    }

    private static void RuleFeedback(PoetryCatalog catalog)
    {
        var moon = Game(catalog, "moon");
        Answer(moon, "  ", PoetryAnswerStatus.Empty);
        Answer(moon, "随便写一句", PoetryAnswerStatus.Unknown);
        Answer(moon, "床前明月光，疑是地上霜。", PoetryAnswerStatus.MultipleLines);
        Answer(moon, "床前明月光，疑是地上霜，举头望明月", PoetryAnswerStatus.MultipleLines);
        Answer(moon, "月出惊山鸟，夜来风雨声，低头思故乡", PoetryAnswerStatus.MultipleLines);
        Answer(moon, "床前明月光疑是地上霜", PoetryAnswerStatus.MultipleLines);
        Answer(moon, "床前明月光\n疑是地上霜", PoetryAnswerStatus.MultipleLines);
        Answer(moon, "床前??明月光", PoetryAnswerStatus.Unknown);
        Answer(moon, "床前明日光", PoetryAnswerStatus.WrongCharacter);
        Answer(moon, "疑是地上霜", PoetryAnswerStatus.WrongCharacter);
        Answer(moon, " 牀前明月光！ ", PoetryAnswerStatus.Accepted);
        Answer(moon, "床前明月光。", PoetryAnswerStatus.Duplicate);
        Answer(moon, "床前明月", PoetryAnswerStatus.Unknown);

        var pair = Game(catalog, "wind-rain");
        Answer(pair, "花落知多少", PoetryAnswerStatus.WrongPair);
        Answer(pair, "昨夜雨疏风骤", PoetryAnswerStatus.Accepted);
        Answer(pair, "夜来风雨声", PoetryAnswerStatus.Accepted);

        var theme = Game(catalog, "homesick");
        Answer(theme, "疑是地上霜", PoetryAnswerStatus.WrongTheme);
        Equal(20, theme.Submit("低头思故乡", 1).LineId, "reviewed source wins over earlier unreviewed same-text source");
        Equal("直接思念故乡", theme.Card.Reasons[20], "review evidence");
        Answer(theme, "低头思故乡", PoetryAnswerStatus.Duplicate);

        var position = Game(catalog, "position");
        Answer(position, "𠮷月照空山", PoetryAnswerStatus.WrongPosition);
        Answer(position, "月出惊山鸟", PoetryAnswerStatus.Accepted);
        Answer(position, "𠮷月照空山", PoetryAnswerStatus.Accepted);

        var selected = Game(catalog, "selected");
        Answer(selected, "明月松间照", PoetryAnswerStatus.Unavailable);
        Answer(selected, "牀前明月光", PoetryAnswerStatus.Accepted);
        var restrictedTheme = Game(catalog, "homesick");
        Answer(restrictedTheme, "海上生明月", PoetryAnswerStatus.WrongTheme);

        var internalMarks = Game(catalog, "crossing");
        Answer(internalMarks, "争渡，争渡", PoetryAnswerStatus.Unknown);
        Answer(internalMarks, "争渡、争渡、", PoetryAnswerStatus.Accepted);
        Equal("争渡、争渡", internalMarks.History[0].Input, "trim a trailing mark but preserve the internal pause");
        Answer(Game(catalog, "crossing"), "争渡､争渡", PoetryAnswerStatus.Accepted);
        var explicitAlias = Game(catalog, "crossing");
        Answer(explicitAlias, "争渡争渡", PoetryAnswerStatus.Accepted);
    }

    private static void DecoratedPlayerInput(PoetryCatalog catalog)
    {
        var game = Game(catalog, "moon");
        Answer(game, "  『床👨🏾‍✈️前★明月光』~！！！  ", PoetryAnswerStatus.Accepted);
        Equal("床前明月光", game.History[0].Input, "history stores cleaned player text");
        Answer(game, "《牀前明月光》？？", PoetryAnswerStatus.Duplicate);
        Answer(game, "☆☆🏳️‍🌈", PoetryAnswerStatus.Empty);
        Answer(game, "举头望明月7️⃣", PoetryAnswerStatus.Unknown);
        Answer(game, "举头望Ⓐ明月", PoetryAnswerStatus.Unknown);
        Answer(game, "举头望A明月", PoetryAnswerStatus.Unknown);
        Answer(game, "举头望 明月", PoetryAnswerStatus.Unknown);
        Answer(game, "举头望明月\uD800", PoetryAnswerStatus.Unknown);
        Answer(game, new string('★', 512) + "举头望明月", PoetryAnswerStatus.Unknown);
        Answer(game, "「𠮷月照空山」🏳️‍🌈！", PoetryAnswerStatus.Accepted);
        Equal("𠮷月照空山", game.History[1].Input, "cleaning does not split a Han surrogate pair");

        var combined = Game(catalog, "moon");
        Answer(combined, "〖床前明月光〗 ⭐，\n〖疑是地上霜〗！！！", PoetryAnswerStatus.MultipleLines);
        Answer(combined, "床前明月光🌙；疑是地上霜", PoetryAnswerStatus.MultipleLines);
        Equal(0, combined.History.Count, "two useful lines do not get recorded as one");

        var legacy = game.Capture(1);
        var entry = legacy.History[0];
        entry.Input = "床前明月光，";
        legacy.History[0] = entry;
        True(PoetrySession.TryRestore(catalog, legacy, 1, out var restored), "existing history with terminal punctuation still replays");
        Equal("床前明月光，", restored.History[0].Input, "replay preserves legacy stored text");

        const string corrected = "驾长车、踏破贺兰山缺";
        const string next = "壮志饥餐胡虏肉";
        var works = new[] { new PoetryWork("reviewed", "岳飞", "满江红", corrected + "。\n" + next + "。") };
        var lines = new[]
        {
            new PoetryLine(0, 0, corrected.Length, 0, true, Array.Empty<string>()),
            new PoetryLine(0, corrected.Length + 2, next.Length, 1, true, Array.Empty<string>())
        };
        var card = new PoetryCard("mountain", PoetryRule.Character, "山", "山", "", 0, new[] { new[] { 0 } }, null);
        var reviewed = new PoetryCatalog("reviewed", works, lines, new[] { card });
        var mountain = Game(reviewed, card.Id);
        Answer(mountain, "「驾长车，踏破贺兰山缺」", PoetryAnswerStatus.Unknown);
        Answer(mountain, "驾长车、踏破贺兰山缺！！\n壮志饥餐胡虏肉", PoetryAnswerStatus.MultipleLines);
        Answer(mountain, "★『驾长车、踏破贺兰山缺』！！！", PoetryAnswerStatus.Accepted);
        Equal(corrected, mountain.History[0].Input, "reviewed internal pause remains in player history");
        Equal(corrected, Game(reviewed, card.Id).Companion(0).Entry.Value.Input, "generated answer still uses the exact catalog form");
    }

    private static void EveryCombination(PoetryCatalog catalog)
    {
        var ids = new[] { "moon", "position", "wind-rain", "homesick" };
        var valid = new[] { "床前明月光", "月出惊山鸟", "夜来风雨声", "低头思故乡" };
        foreach (PoetryPlayers players in Enum.GetValues(typeof(PoetryPlayers)))
        foreach (PoetryGoal goal in Enum.GetValues(typeof(PoetryGoal)))
        for (var rule = 0; rule < ids.Length; rule++)
        {
            var game = Game(catalog, ids[rule], players, goal);
            if (players == PoetryPlayers.Duo)
            {
                Equal(PoetryPhase.Handoff, game.Phase, "duo begins with handoff");
                True(game.Ready(40), "duo can enter active turn");
            }
            Answer(game, "错误输入", PoetryAnswerStatus.Unknown, 41);
            Equal(0, game.History.Count, "an invalid retry preserves history");
            Answer(game, valid[rule], PoetryAnswerStatus.Accepted, 42);
            True(PoetrySession.TryRestore(catalog, game.Capture(43), 1000, out var restored), "combination restores");
            Equal(1, restored.History.Count, "combo history restored");
            if (goal == PoetryGoal.Learning)
                True(restored.End(1000), "learning can end");
            else
                True(restored.Abandon(1000), "competition can abandon");
            Equal(PoetryPhase.Finished, restored.Phase, "combination finishes");
            True(PoetrySession.TryRestore(catalog, restored.Capture(1000), 0, out _), "finished combination restores");
        }
    }

    private static void PlaySoloChallenge(PoetryCatalog catalog)
    {
        var game = Game(catalog, "moon", goal: PoetryGoal.Speed, now: 500);
        Equal(0, game.Hints().Count, "competition has no hints");
        Equal(PoetryAnswerStatus.Unavailable, game.Companion(501).Status, "no competition companion");
        True(!game.ExposeWork(0), "competition cannot expose originals");
        Answer(game, "床前明日光", PoetryAnswerStatus.WrongCharacter, 501);
        True(game.Pause(1500), "solo pause");
        Equal(59000L, game.Remaining(90000), "pause freezes timer");
        True(game.Resume(91000), "explicit resume");
        Answer(game, "床前明月光", PoetryAnswerStatus.Accepted, 91001);
        Answer(game, "床前明月光", PoetryAnswerStatus.Duplicate, 91001);
        Answer(game, "举头望明月", PoetryAnswerStatus.Accepted, 91001);
        Answer(game, "月出惊山鸟", PoetryAnswerStatus.Accepted, 91001);
        Answer(game, "明月松间照", PoetryAnswerStatus.Accepted, 91002);
        Answer(game, "海上生明月", PoetryAnswerStatus.Accepted, 91003);
        Equal(PoetryOutcome.Completed, game.Outcome, "five unique groups win");
        Equal(1003L, game.History[4].ElapsedMilliseconds, "completion elapsed excludes pauses");
        Answer(game, "松间月下泉", PoetryAnswerStatus.Inactive, 91004);
        True(PoetrySession.TryRestore(catalog, game.Capture(94000), 10, out _), "completed solo restores");

        var exact = Game(catalog, "moon", goal: PoetryGoal.Speed, now: 300);
        Answer(exact, "床前明月光", PoetryAnswerStatus.TimedOut, 60300);
        Equal(PoetryOutcome.Timeout, exact.Outcome, "the deadline itself is too late");
        True(!exact.Tick(70000), "timeout settles once");

        var monotonic = Game(catalog, "moon", goal: PoetryGoal.Speed, now: 100);
        Equal(59000L, monotonic.Remaining(1100), "elapsed reads current time");
        Equal(59000L, monotonic.Remaining(900), "clock regression cannot restore time");
        True(PoetrySession.TryRestore(catalog, monotonic.Capture(1100), 0, out var paused), "active competition restores");
        Equal(PoetryPhase.Paused, paused.Phase, "restored competition requires explicit resume");
        Equal(59000L, paused.Remaining(85000), "restored session ignores offline duration");
        True(paused.Resume(90000), "competition resumes");
        Equal(58000L, paused.Remaining(91000), "new monotonic epoch");
    }

    private static void PlayDuoMatch(PoetryCatalog catalog)
    {
        var game = Game(catalog, "moon", PoetryPlayers.Duo, PoetryGoal.Speed, 0, 1);
        Equal(1, game.CurrentPlayer, "configured first player");
        Equal(30000L, game.Remaining(49000), "handoff freezes clock");
        True(game.Ready(50000), "first player ready");
        Answer(game, "床前明月光", PoetryAnswerStatus.Accepted, 79999);
        Equal(1, game.History[0].Player, "first player recorded");
        Equal(PoetryPhase.Handoff, game.Phase, "valid answer hands device over");
        Answer(game, "明月松间照", PoetryAnswerStatus.Inactive, 94000);
        True(game.Ready(95000), "second player ready");
        Answer(game, "牀前明月光", PoetryAnswerStatus.Duplicate, 95001);
        True(game.Tick(125000), "second player times out");
        Equal(1, game.Wins(1), "round one to right");
        Equal(PoetryPhase.RoundOver, game.Phase, "round results await handoff");
        True(game.NextRound(225000), "next round starts on demand");
        Equal(0, game.CurrentPlayer, "starter alternates");
        True(game.Ready(325000), "round 2 ready");
        Answer(game, "床前明月光", PoetryAnswerStatus.Accepted, 325001);
        True(game.Ready(325002), "turn 2 ready");
        True(game.Pass(325003), "right passes");
        Equal(1, game.Wins(0), "round two to left");
        True(game.NextRound(325004), "round three starts after tied wins");
        Equal(1, game.CurrentPlayer, "round three starter alternates back");
        True(game.Ready(325005), "round three ready");
        True(game.Pass(325006), "right loses after passing");
        Equal(0, game.Winner, "left wins best of 3");
        Equal(3, game.Rounds.Count, "exactly three results");
        True(!game.Pass(325007), "duplicate pass cannot update score");
        True(PoetrySession.TryRestore(catalog, game.Capture(325007), 0, out _), "final round restores deterministically");

        var exact = Game(catalog, "moon", PoetryPlayers.Duo, PoetryGoal.Speed);
        True(exact.Ready(50), "ready starts exact deadline test");
        True(!exact.Pass(30050), "pass at deadline cannot override timeout");
        Equal(PoetryOutcome.Timeout, exact.Rounds[0].Outcome, "timeout before simultaneous pass");
        True(exact.Abandon(30051), "abandon from round result");
        True(PoetrySession.TryRestore(catalog, exact.Capture(40000), 0, out var abandoned), "abandoned round result restores");
        Equal(PoetryOutcome.Abandoned, abandoned.Outcome, "abandon persists");
        Equal(1, abandoned.Wins(1), "abandon preserves actual round score");
    }

    private static void CompetitionRelay(PoetryCatalog catalog)
    {
        var game = Game(catalog, "moon", goal: PoetryGoal.Competition);
        Equal(-1L, game.Remaining(60_000), "competition is untimed");
        True(!game.Tick(90_000), "no competition expiration");
        True(!game.ComputerTurn, "human opens the relay");
        Equal(PoetryAnswerStatus.Unavailable, game.Computer(90_000).Status, "computer cannot open for the human");
        Answer(game, "床前明月光", PoetryAnswerStatus.Accepted, 100_000);
        True(game.ComputerTurn, "a valid human answer schedules the computer");
        Equal(0, game.Hints().Count, "computer turn has no human hints");
        True(!game.ExposeWork(0), "competition does not expose the original during play");
        Answer(game, "举头望明月", PoetryAnswerStatus.Inactive, 100_001);
        True(!game.Pass(100_001), "the user cannot pass on behalf of the computer");
        var pending = game.Capture(100_002);
        Equal(2, pending.RulesVersion, "rule version identifies goal semantics");
        var legacy = game.Capture(100_002);
        legacy.RulesVersion = 1;
        True(!PoetrySession.TryRestore(catalog, legacy, 0, out _), "v1 goal values cannot be reinterpreted");
        True(PoetrySession.TryRestore(catalog, pending, 0, out var restored), "pending turn restores");
        True(restored.ComputerTurn, "computer still owns the restored turn");
        var reply = game.Computer(100_003);
        var replayedReply = restored.Computer(1);
        Equal(PoetryAnswerStatus.Accepted, reply.Status, "computer replies from the catalog");
        Equal(reply.LineId, replayedReply.LineId, "computer choice is deterministic across restore");
        Equal(1, reply.Entry.Value.Player, "computer owns its score");
        True(!reply.Entry.Value.Companion, "computer is an opponent");
        True(!game.ComputerTurn, "a valid machine answer yields the turn");
        var edited = game.Capture(100_003);
        var forged = edited.History[1];
        var alternative = game.Card.Candidates[0].First(id => id != 0 && id != forged.LineId);
        forged.LineId = alternative;
        forged.Input = catalog.Text(alternative);
        edited.History[1] = forged;
        True(!PoetrySession.TryRestore(catalog, edited, 0, out _), "changed computer choice is rejected even if locally valid");
        Answer(game, reply.Entry.Value.Input, PoetryAnswerStatus.Duplicate, 100_004);
        True(game.Pass(100_005), "human can give up their own turn");
        Equal(1, game.Winner, "computer wins the unanswered relay");
        True(PoetrySession.TryRestore(catalog, game.Capture(100_006), 0, out _), "human loss restores");

        var impossibleReply = Game(catalog, "selected", goal: PoetryGoal.Competition);
        Answer(impossibleReply, "床前明月光", PoetryAnswerStatus.Accepted);
        True(impossibleReply.ComputerTurn, "the machine receives the unanswered turn");
        Equal(PoetryAnswerStatus.Unavailable, impossibleReply.Computer(2).Status, "machine has no unused group");
        Equal(PoetryOutcome.Passed, impossibleReply.Outcome, "no response loses the relay");
        Equal(0, impossibleReply.Winner, "the human wins when the computer cannot answer");
        True(PoetrySession.TryRestore(catalog, impossibleReply.Capture(3), 0, out _), "machine loss restores");

        var machineCanUseRare = Game(catalog, "rare-pair", goal: PoetryGoal.Competition);
        Answer(machineCanUseRare, "床前明月光", PoetryAnswerStatus.Accepted);
        var rare = machineCanUseRare.Computer(2);
        Equal(22, rare.LineId, "machine uses legal uncommon answers");
        Equal(1, machineCanUseRare.Winner, "human cannot answer when the machine uses the last group");
        True(PoetrySession.TryRestore(catalog, machineCanUseRare.Capture(3), 0, out _), "machine exhaustion win restores");

        var duo = Game(catalog, "selected", PoetryPlayers.Duo, PoetryGoal.Competition, starter: 1);
        True(duo.Ready(0), "duo enters its relay");
        True(!duo.Tick(100_000), "duo relay stays untimed");
        Answer(duo, "床前明月光", PoetryAnswerStatus.Accepted, 100_001);
        Equal(PoetryPhase.RoundOver, duo.Phase, "unanswerable position settles the small round");
        Equal(1, duo.Rounds[0].Winner, "last valid side wins the small round");
        True(duo.NextRound(100_002), "duo competition retains the next round");
        Equal(0, duo.CurrentPlayer, "duo relay changes starter");
        True(duo.Ready(100_003) && duo.Pass(200_005), "unlimited active side can pass");
        Equal(1, duo.Winner, "two competition wins finish the match");
        True(PoetrySession.TryRestore(catalog, duo.Capture(200_006), 0, out _), "untimed best-of-three result restores");
    }

    private static void Exhaustion(PoetryCatalog catalog)
    {
        var solo = Game(catalog, "home", goal: PoetryGoal.Speed);
        Answer(solo, "低头思故乡", PoetryAnswerStatus.Accepted);
        Equal(PoetryOutcome.Exhausted, solo.Outcome, "final duplicate-group answer counts then exhausts");
        Equal(1, solo.History.Count, "exhaustion retains last valid result");
        Equal(-1, solo.Winner, "exhaustion is neutral");

        var duo = Game(catalog, "home", PoetryPlayers.Duo, PoetryGoal.Speed);
        for (var round = 1; round <= 3; round++)
        {
            True(duo.Ready(10), "neutral fixture enters active round");
            Answer(duo, "低头思故乡", PoetryAnswerStatus.Accepted, 11);
            Equal(PoetryOutcome.Exhausted, duo.Rounds[round - 1].Outcome, "neutral round exhaustion");
            Equal(-1, duo.Rounds[round - 1].Winner, "draw counts as no round win");
            if (round < 3)
                True(duo.NextRound(12), "neutral result consumes a round");
        }
        Equal(-1, duo.Winner, "three neutral rounds produce a draw");
        Equal(PoetryPhase.Finished, duo.Phase, "three neutral rounds end match");

        var fastest = Game(catalog, "moon", PoetryPlayers.Duo, PoetryGoal.Speed, starter: 1);
        True(fastest.Ready(0) && fastest.Pass(1) && fastest.NextRound(2) && fastest.Ready(3), "left earns first win");
        Answer(fastest, "床前明月光", PoetryAnswerStatus.Accepted, 4);
        True(fastest.Ready(5) && fastest.Pass(6), "left earns second win");
        Equal(0, fastest.Winner, "two wins end match early");
        Equal(2, fastest.Rounds.Count, "early conclusion prevents pointless third round");
    }

    private static void LearningHelp(PoetryCatalog catalog)
    {
        var learning = Game(catalog, "moon", PoetryPlayers.Duo);
        True(learning.Ready(0), "learning ready");
        var old = learning.Hints().ToArray();
        Equal(3, old.Length, "up to three suggestions");
        True(old.SequenceEqual(learning.Hints()), "same turn has stable suggestions");
        var refreshed = learning.Hints(true);
        True(!old.Intersect(refreshed).Any(), "refresh omits already exposed groups");
        True(learning.ExposeWork(0), "display entire poem");
        Answer(learning, catalog.Text(refreshed[0]), PoetryAnswerStatus.Accepted);
        Equal(1, learning.Count(0, true), "hint earns assisted count");
        True(PoetrySession.TryRestore(catalog, learning.Capture(1), 0, out var restored), "handoff and exposure restore");
        True(restored.Ready(2), "second player receives device");
        Answer(restored, catalog.Text(old[0]), PoetryAnswerStatus.Accepted, 3);
        Equal(1, restored.Count(1, true), "previous player exposure stays shared");
        Equal(0, restored.Count(1, false), "exposed groups cannot count independently");
        True(restored.Ready(4), "back to player one");
        Answer(restored, "举头望明月", PoetryAnswerStatus.Accepted, 5);
        Equal(2, restored.Count(0, true), "full original exposes other displayed answer groups");
        True(!restored.ExposeLine(-1), "invalid exposure is ignored");

        var oneHint = Game(catalog, "alias-only");
        var candidate = oneHint.Hints().Single();
        Equal("床前明日光", oneHint.Suggestion(candidate), "an accepted variant can be suggested");
        Equal(candidate, oneHint.Hints(true).Single(), "exhausted refresh keeps previously displayed batch");
        True(PoetrySession.TryRestore(catalog, oneHint.Capture(0), 10, out var cached), "displayed hint batch restores");
        Equal(candidate, cached.Hints().Single(), "same hint after restore");
        Answer(cached, "床前明月光", PoetryAnswerStatus.WrongCharacter);
        Answer(cached, "床前明日光", PoetryAnswerStatus.Accepted);
        Equal(1, cached.Count(0, true), "alias suggestion exposes the group");

        var companion = Game(catalog, "position");
        Equal(PoetryAnswerStatus.Accepted, companion.Companion(2).Status, "solo learning offers companion");
        True(companion.History[0].Companion, "companion is recorded as companion");
        Equal(0, companion.Count(0, true) + companion.Count(0, false), "companion receives no player credit");
        Equal(2, companion.Position, "companion advances position");
        var rare = Game(catalog, "rare");
        Equal(0, rare.Hints().Count, "uncommon lines remain valid beyond the hint pool");
        Equal(PoetryAnswerStatus.Unavailable, rare.Companion(0).Status, "common pool can be exhausted");
        Answer(rare, "白云依山月", PoetryAnswerStatus.Accepted);
    }

    private static void MaskedHints(PoetryCatalog catalog)
    {
        var game = Game(catalog, "moon");
        var targets = game.Hints();
        True(targets.Contains(0), "fixture's first target is hinted");
        var context = game.ClueLines(0);
        Equal(1, context[0], "closest adjacent line is preferred");
        True(context.All(line => catalog.Lines[line].Group != catalog.Lines[0].Group), "clue excludes the answer group");
        True(!context.Intersect(targets).Any(), "a different current target is not exposed as a clue");
        var masked = game.MaskClue("床前明月光。牀前明月光！床前明日光，疑是地上霜。", 0);
        Equal("□□□□□。□□□□□！□□□□□，疑是地上霜。", masked, "all accepted spellings are concealed and context remains");
        Equal("□□□□□", game.MaskClue(catalog.Text(targets[1]), 0), "the whole hint batch conceals one another's target");
        True(catalog.WorkLineIds(0).Contains(context[0]), "contexts refer to original preceding or next lines");

        var single = Game(catalog, "selected");
        Equal(0, single.Hints().Single(), "only available group becomes the clue target");
        Answer(single, "牀前明月光", PoetryAnswerStatus.Accepted);
        Equal(1, single.Count(0, true), "uncopied masked answer still counts as assisted");

        var position = Game(catalog, "position");
        var firstTarget = position.Hints().Single();
        var displayed = position.ClueLines(firstTarget).First();
        True(position.ExposeLine(displayed), "displayed contextual group can be registered");
        Answer(position, "月出惊山鸟", PoetryAnswerStatus.Accepted);
        Answer(position, catalog.Text(displayed), PoetryAnswerStatus.Accepted);
        Equal(2, position.Count(0, true), "seen related line is assisted in the upcoming position");

        var variantWorks = new[]
        {
            new PoetryWork("first", "作者", "初稿", "𠮷月照空山，春风如故人"),
            new PoetryWork("second", "作者", "再稿", "吉月映空山，且听山前雨")
        };
        var variants = new[]
        {
            new PoetryLine(0, 0, 6, 0, true, Array.Empty<string>()),
            new PoetryLine(0, 7, 5, 1, true, Array.Empty<string>()),
            new PoetryLine(1, 0, 5, 0, false, new[] { "吉月照青山" }),
            new PoetryLine(1, 6, 5, 2, true, Array.Empty<string>())
        };
        var moon = new PoetryCard("variant", PoetryRule.Character, "月", "月", "", 0, new[] { new[] { 0, 2 } }, null);
        var grouped = new PoetryCatalog("variants", variantWorks, variants, new[] { moon });
        var hidden = Game(grouped, moon.Id);
        Equal(0, hidden.Hints().Single(), "common variant represents the shared group");
        Equal("□□□□□，□□□□□。□□□□□", hidden.MaskClue("𠮷月照空山，吉月映空山。吉月照青山", 0),
            "mask covers separate published spellings, alternate sources and Unicode-scalar lengths");
    }

    private static void CompetitiveHintBudget(PoetryCatalog catalog)
    {
        var solo = Game(catalog, "moon", goal: PoetryGoal.Competition);
        Equal(3, solo.CompetitionHintsRemaining, "new match has three targets available");
        True(!solo.HasCurrentCompetitionHint, "unopened hint has no cache");
        var first = solo.Hints().Single();
        True(solo.HasCurrentCompetitionHint, "new target becomes the current clue");
        Equal(2, solo.CompetitionHintsRemaining, "one new target consumes one use");
        True(solo.ClueLines(first).Count > 0, "target has masked contextual clues");
        True(solo.ClueLines(8).Count == 0, "only the selected competitive target grants clues");
        Equal(first, solo.Hints().Single(), "reopening never charges");
        Answer(solo, "随便写一句", PoetryAnswerStatus.Unknown);
        Equal(first, solo.Hints().Single(), "invalid answer does not reset the hint");
        Equal(2, solo.CompetitionHintsRemaining, "invalid answer leaves the quota unchanged");
        var second = solo.Hints(true).Single();
        var third = solo.Hints(true).Single();
        True(first != second && second != third && first != third, "refresh targets are distinct");
        Equal(0, solo.CompetitionHintsRemaining, "three successful targets exhaust the budget");
        Equal(third, solo.Hints(true).Single(), "exhausted refresh keeps the current target");
        True(solo.HasCurrentCompetitionHint, "cached hint remains openable at zero");
        Equal("□□□□□", solo.MaskClue(catalog.Text(first), third), "earlier masked targets stay hidden after refresh");
        True(!solo.ExposeLine(first), "competition hints never assign assisted credit");
        True(!solo.ExposeWork(0), "competition does not expose original answer groups");
        True(PoetrySession.TryRestore(catalog, solo.Capture(10), 0, out var restored), "zero-budget active clue restores");
        Equal(third, restored.Hints().Single(), "restored current clue can still reopen");
        Equal(0, restored.CompetitionHintsRemaining, "restored hint quota is exhausted");
        Answer(restored, catalog.Text(third), PoetryAnswerStatus.Accepted);
        True(!restored.History[0].Assisted, "competitive accepted answer keeps ordinary credit");
        True(restored.ComputerTurn, "accepted human answer hands turn to computer");
        True(!restored.HasCurrentCompetitionHint, "accepted answer clears the old cache");
        Equal(0, restored.Hints().Count, "computer turn cannot show hints");
        True(PoetrySession.TryRestore(catalog, restored.Capture(11), 0, out var pending), "computer pending with spent hints restores");
        Equal(0, pending.CompetitionHintsRemaining, "computer resume preserves spent hints");

        var duo = Game(catalog, "moon", PoetryPlayers.Duo, PoetryGoal.Competition);
        Equal(0, duo.Hints().Count, "handoff cannot reveal a hint");
        True(duo.Ready(0), "first player becomes active");
        var left = duo.Hints().Single();
        Answer(duo, catalog.Text(left), PoetryAnswerStatus.Accepted);
        Equal(2, duo.CompetitionHintsRemaining, "handoff keeps the shared budget");
        True(!duo.HasCurrentCompetitionHint, "handoff drops previous player's clue");
        True(duo.Ready(1), "second player becomes active");
        var right = duo.Hints().Single();
        True(right != left, "second player receives a distinct clue");
        True(duo.Pass(2), "round one ends after pass");
        Equal(0, duo.Hints().Count, "round score page shows no clue");
        True(duo.NextRound(3) && duo.Ready(4), "round two starts with other player");
        var acrossRounds = duo.Hints().Single();
        True(acrossRounds != left && acrossRounds != right, "past rounds' charged groups remain used");
        Equal(0, duo.CompetitionHintsRemaining, "the entire duel shares three uses");
        True(duo.Pause(5), "pause competitive hint fixture");
        True(!duo.HasCurrentCompetitionHint, "hidden while pause is visible");
        True(PoetrySession.TryRestore(catalog, duo.Capture(6), 0, out var duoRestored), "duo's masked clue and quota survive pause restore");
        True(duoRestored.Resume(1), "unpause competitive hint fixture");
        Equal(acrossRounds, duoRestored.Hints(true).Single(), "restored paused cache can reopen");
        True(duoRestored.Pass(2), "second round finishes the match");
        True(!duoRestored.HasCurrentCompetitionHint, "terminal review never shows active hints");

        var unavailable = Game(catalog, "rare", goal: PoetryGoal.Competition);
        Equal(0, unavailable.Hints().Count, "no clue when no usable common target");
        Equal(3, unavailable.CompetitionHintsRemaining, "no clue consumes nothing");

        var limited = Game(catalog, "home", goal: PoetryGoal.Competition);
        var only = limited.Hints().Single();
        Equal(only, limited.Hints(true).Single(), "no new target keeps existing clue");
        Equal(2, limited.CompetitionHintsRemaining, "second source of the same group cannot consume another use");

        var bad = solo.Capture(10);
        bad.CompetitionHintLines = new[] { first, second, third, 4 };
        True(!PoetrySession.TryRestore(catalog, bad, 0, out _), "over-quota history is corrupt");
        bad = solo.Capture(10);
        bad.CompetitionHintLines = new[] { first, second, second };
        True(!PoetrySession.TryRestore(catalog, bad, 0, out _), "charged group cannot repeat");
        bad = solo.Capture(10);
        bad.CompetitionHintLines = new[] { first, 1, third };
        True(!PoetrySession.TryRestore(catalog, bad, 0, out _), "charged target must belong to the card");
        bad = solo.Capture(10);
        bad.HintLines = new[] { first };
        True(!PoetrySession.TryRestore(catalog, bad, 0, out _), "current hint must be the most recent charged target");

        var speed = Game(catalog, "moon", goal: PoetryGoal.Speed);
        Equal(0, speed.Hints().Count, "speed remains independent");
        bad = speed.Capture(0);
        bad.CompetitionHintLines = new[] { first };
        True(!PoetrySession.TryRestore(catalog, bad, 0, out _), "speed snapshot cannot carry competitive hints");
    }

    private static void CompanionPreservesNextPosition()
    {
        var quote = "月月风雨夜，月出惊山鸟，松间月下泉，床前明月光，海上生明月";
        var works = new[] { new PoetryWork("one", "诗人", "诗题", quote) };
        var lines = new PoetryLine[5];
        for (var line = 0; line < lines.Length; line++)
            lines[line] = new PoetryLine(0, line * 6, 5, line, true, Array.Empty<string>());
        var cards = new[] { new PoetryCard("position", PoetryRule.Position, "月五言", "月", "", 5,
            new[] { new[] { 0, 1 }, new[] { 0 }, new[] { 2 }, new[] { 3 }, new[] { 4 } }, null) };
        var catalog = new PoetryCatalog("fixture", works, lines, cards);

        var coached = Game(catalog, "position");
        Equal(1, coached.Companion(0).LineId, "companion spares the only group at the next position");
        Answer(coached, "月月风雨夜", PoetryAnswerStatus.Accepted);
        Equal(3, coached.Position, "the spared group keeps the game moving");

        var solo = Game(catalog, "position", goal: PoetryGoal.Speed);
        Answer(solo, "月月风雨夜", PoetryAnswerStatus.Accepted);
        Equal(PoetryOutcome.Exhausted, solo.Outcome, "a player's legal choice can use up next position");
    }

    private static void ValidateRestore(PoetryCatalog catalog)
    {
        var game = Game(catalog, "position", goal: PoetryGoal.Speed);
        Answer(game, "月出惊山鸟", PoetryAnswerStatus.Accepted, 1);
        var snapshot = game.Capture(4);
        snapshot.Position = 3;
        True(!PoetrySession.TryRestore(catalog, snapshot, 0, out _), "impossible position rejected");
        snapshot = game.Capture(4);
        snapshot.History[0] = new PoetryEntry { LineId = 5, Round = 1, Input = "明月松间照", Player = 0, ElapsedMilliseconds = 1 };
        True(!PoetrySession.TryRestore(catalog, snapshot, 0, out _), "forged position history rejected");
        snapshot = game.Capture(4);
        snapshot.RemainingMilliseconds--;
        True(!PoetrySession.TryRestore(catalog, snapshot, 0, out _), "inconsistent timer rejected");
        snapshot = game.Capture(4);
        snapshot.History = null;
        True(!PoetrySession.TryRestore(catalog, snapshot, 0, out _), "missing history rejected");
        snapshot = game.Capture(4);
        snapshot.CorpusVersion = "old-fixture";
        True(!PoetrySession.TryRestore(catalog, snapshot, 0, out _), "unknown version rejected");
        snapshot = game.Capture(4);
        snapshot.ExposedGroups = new[] { catalog.Lines[4].Group };
        True(!PoetrySession.TryRestore(catalog, snapshot, 0, out _), "competitive exposure rejected");
    }

    private static void PreserveOldCatalogPrefix()
    {
        var works = new[]
        {
            new PoetryWork("one", "作者甲", "旧篇", "床前明月光，明月松间照"),
            new PoetryWork("two", "作者乙", "新篇", "海上生明月，明月照沙溪")
        };
        var lines = new[]
        {
            new PoetryLine(0, 0, 5, 0, true, Array.Empty<string>()),
            new PoetryLine(0, 6, 5, 1, false, Array.Empty<string>()),
            new PoetryLine(1, 0, 5, 2, true, Array.Empty<string>()),
            new PoetryLine(1, 6, 5, 1, true, Array.Empty<string>())
        };
        var oldMoon = new PoetryCard("moon", PoetryRule.Character, "月", "月", "", 0, new[] { new[] { 0, 1 } }, null);
        var oldCurated = new PoetryCard("curated", PoetryRule.Character, "月", "月", "", 0, new[] { new[] { 0 } }, null);
        var original = new PoetryCatalog("before", works.Take(1).ToArray(), lines.Take(2).ToArray(), new[] { oldMoon, oldCurated });
        var newMoon = new PoetryCard("moon", PoetryRule.Character, "月", "月", "", 0, new[] { new[] { 0, 1, 2, 3 } }, null);
        var newCurated = new PoetryCard("curated", PoetryRule.Character, "月", "月", "", 0, new[] { new[] { 0, 3 } }, null);
        var extended = new PoetryCatalog("after", works, lines, new[] { newMoon, newCurated });

        var match = Game(original, "moon", goal: PoetryGoal.Competition);
        Answer(match, "床前明月光", PoetryAnswerStatus.Accepted);
        var pending = match.Capture(1);
        pending.CorpusVersion = extended.Version;
        pending.AnswerLineLimit = 2;
        True(PoetrySession.TryRestore(extended, pending, 0, out var resumed), "pending match can resume against old prefix");
        Equal(1, resumed.Computer(1).LineId, "new common choices do not change resumed computer line");
        Equal(1, resumed.Winner, "old prefix still exhausts after computer's reply");
        var finished = resumed.Capture(2);
        Equal(2, finished.AnswerLineLimit, "later saves preserve the prefix");
        True(PoetrySession.TryRestore(extended, finished, 0, out _), "exact old computer history and terminal state replay");
        finished.AnswerLineLimit = 0;
        True(!PoetrySession.TryRestore(extended, finished, 0, out _), "unlocked replay cannot reinterpret old result");

        var oldLearning = Game(original, "curated").Capture(0);
        oldLearning.CorpusVersion = extended.Version;
        oldLearning.AnswerLineLimit = 2;
        True(PoetrySession.TryRestore(extended, oldLearning, 0, out var oldRules), "old learning card can resume");
        Answer(oldRules, "海上生明月", PoetryAnswerStatus.Unknown);
        Answer(oldRules, "海上生明月，明月照沙溪", PoetryAnswerStatus.Unknown);
        Answer(oldRules, "明月松间照", PoetryAnswerStatus.Unavailable);
        True(!oldRules.ExposeLine(2) && !oldRules.ExposeWork(1), "new lines cannot become exposure groups");
        Equal<string>(null, oldRules.Suggestion(2), "resumed match cannot suggest new lines");

        var oldHints = Game(original, "moon", goal: PoetryGoal.Competition).Capture(0);
        oldHints.CorpusVersion = extended.Version;
        oldHints.AnswerLineLimit = 2;
        oldHints.CompetitionHintLines = new[] { 2 };
        True(!PoetrySession.TryRestore(extended, oldHints, 0, out _), "resumed quota rejects a newly appended line");
        oldHints.CompetitionHintLines = Array.Empty<int>();
        True(PoetrySession.TryRestore(extended, oldHints, 0, out var hinted), "old match can use a new quota feature");
        Equal(0, hinted.Hints().Single(), "only old contextual targets are picked");
        Equal(0, hinted.Hints(true).Single(), "new common target is skipped even on refresh");
        Equal(2, hinted.CompetitionHintsRemaining, "skipped new lines consume no additional use");

        Answer(Game(extended, "moon"), "海上生明月", PoetryAnswerStatus.Accepted);
    }

    private static void RetiredCatalogLines()
    {
        var fixture = CreateCatalog();
        var works = fixture.Works.Concat(new[] { new PoetryWork("addition", "王湾", "次北固山下", "乡书何处达") }).ToArray();
        var extraId = fixture.Lines.Length;
        var lines = fixture.Lines.Concat(new[] {
            new PoetryLine(works.Length - 1, 0, 5, 99, true, Array.Empty<string>())
        }).ToArray();
        var cards = new[]
        {
            fixture.FindCard("moon"), fixture.FindCard("position"), fixture.FindCard("homesick"), fixture.FindCard("home"),
            new PoetryCard("only-retired-source", PoetryRule.Character, "乡", "乡", "", 0,
                new[] { new[] { 3, extraId } }, null),
            new PoetryCard("small", PoetryRule.Character, "月", "月", "", 0,
                new[] { new[] { 0, 2 } }, null)
        };
        var original = new PoetryCatalog("before-correction", works, lines, cards);
        var current = new PoetryCatalog("after-correction", works, lines, cards, new[] { 0, 3 });

        var learning = Game(current, "moon");
        Equal<string>(null, learning.Suggestion(0), "retired source is never suggested");
        True(!learning.ExposeLine(0), "retired source cannot earn exposure");
        Answer(learning, "床前明月光", PoetryAnswerStatus.Unknown);
        Answer(learning, "牀前明月光", PoetryAnswerStatus.Unknown);
        Answer(learning, "床前明月光，疑是地上霜", PoetryAnswerStatus.Unknown);
        True(!learning.Hints().Contains(0), "new hints never pick retired lines");
        Equal(2, Game(current, "moon").Companion(0).LineId, "companion skips retired candidate");

        var same = Game(current, "homesick");
        Equal(20, same.Submit("低头思故乡", 0).LineId, "exact duplicate uses a nonretired audited source");
        Equal(20, Game(current, "home").Submit("低头思故乡", 0).LineId, "ordinary card can use the valid nonretired duplicate");
        Answer(Game(current, "only-retired-source"), "低头思故乡", PoetryAnswerStatus.Unavailable);
        var small = Game(current, "small", goal: PoetryGoal.Speed);
        Answer(small, "举头望明月", PoetryAnswerStatus.Accepted);
        Equal(PoetryOutcome.Exhausted, small.Outcome, "retired candidates do not postpone exhaustion");

        var cpu = Game(current, "moon", goal: PoetryGoal.Competition);
        Answer(cpu, "月出惊山鸟", PoetryAnswerStatus.Accepted);
        Equal(2, cpu.Computer(1).LineId, "computer skips retired candidate");
        var originalMatch = Game(original, "moon", goal: PoetryGoal.Competition);
        Answer(originalMatch, "月出惊山鸟", PoetryAnswerStatus.Accepted);
        Equal(0, originalMatch.Computer(1).LineId, "historical computer selected the uncorrected answer");
        var migrated = originalMatch.Capture(2);
        migrated.CorpusVersion = current.Version;
        migrated.AnswerLineLimit = lines.Length;
        True(PoetrySession.TryRestore(current, migrated, 0, out var retained), "migrated match replays the formerly accepted computer line");
        Equal(0, retained.History[1].LineId, "historical evidence stays attached to its original source");
        migrated.AnswerLineLimit = 0;
        True(!PoetrySession.TryRestore(current, migrated, 0, out _), "new history cannot replay a retired answer");
        InvalidCatalog(() => new PoetryCatalog("invalid", works, lines, cards, new[] { lines.Length }),
            "out-of-range retirement must be rejected");
        InvalidCatalog(() => new PoetryCatalog("invalid", works, lines, cards, new[] { 0, 0 }),
            "duplicate retirement is malformed");
    }

    private static void InvalidCatalog(Func<PoetryCatalog> read, string message)
    {
        try { read(); }
        catch (ArgumentException) { checks++; return; }
        throw new InvalidOperationException(message);
    }

    private static void ValidateCatalog()
    {
        var works = new[] { new PoetryWork("one", "诗人", "诗题", "明月，晚月") };
        var lines = new[]
        {
            new PoetryLine(0, 0, 2, 0, true, new[] { "晚月" }),
            new PoetryLine(0, 3, 2, 1, true, Array.Empty<string>())
        };
        var cards = new[] { new PoetryCard("moon", PoetryRule.Character, "月", "月", "", 0, new[] { new[] { 0, 1 } }, null) };
        try
        {
            new PoetryCatalog("fixture", works, lines, cards);
            throw new InvalidOperationException("ambiguous alias collision was accepted");
        }
        catch (ArgumentException)
        {
            checks++;
        }
    }

    private static void Answer(PoetrySession game, string input, PoetryAnswerStatus status, long now = 0)
    {
        Equal(status, game.Submit(input, now).Status, game.Card.Id + " input " + input);
    }

    private static void True(bool value, string message)
    {
        Equal(true, value, message);
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        checks++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(message + ": expected " + expected + ", got " + actual);
    }
}
