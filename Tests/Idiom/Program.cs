using System;
using System.Collections.Generic;
using System.Linq;
using XanaduYonder.Core.Idiom;

internal static class Program
{
    private static int checks;
    private static int failed;

    private static int Main(string[] args)
    {
        Run("entry and lexicon validate identity and four Unicode Han scalars", Entries);
        Run("both modes and both starters create complete public turns", Starters);
        Run("character and contextual sound modes differ without tone confusion", Modes);
        Run("sound distinguishes v/u and retroflex and nasal keys", SoundKeys);
        Run("invalid submissions preserve history hints and random state", InvalidActions);
        Run("both actors share one used-word set", RepeatedWords);
        Run("APP avoids dead ends even when the dead choice is common", AvoidDeadEnds);
        Run("APP prefers common choices at equal continuation quality", CommonChoices);
        Run("machine opening uses a common live word before uncommon larger branches", CommonOpening);
        Run("APP prefers three continuations over fewer", MoreContinuations);
        Run("selection is deterministic across input order and snapshot restore", Determinism);
        Run("hints are legal at most three and cached once per player turn", HintCaching);
        Run("hints never consume APP random state or occupy candidates", HintRandomness);
        Run("third new hint ends immediately without adding messages", ThirdHint);
        Run("inactive and terminal hint requests cannot create new events", InactiveHints);
        Run("explicit finish preserves progress and blocks further play", Finish);
        Run("dictionary exhaustion has priority over no-move reasons", Exhaustion);
        Run("APP and player no-move terminals represent the correct actor", NoMoves);
        Run("snapshot collections and lexicon data cannot be mutated externally", Copies);
        Run("restore rejects unknown enums zero random state and unstarted progress", BadHeaders);
        Run("restore rejects unknown duplicate mismatched and partial history", BadHistory);
        Run("restore checks every terminal reason against final legal moves", BadEndReasons);
        Run("historical hints are validated against their own history prefix", HistoricalHints);
        Run("restore rejects forged hints and progress after third hint", BadHints);
        Run("generated mixed action sequences remain exactly restorable", Replay);
        if (args.Length == 2 && args[0] == "--catalog")
            Run("full current dictionary supports bounded actions and long restored games", delegate { CatalogChecks.Run(args[1]); });
        else if (args.Length != 0)
        {
            Console.Error.WriteLine("Usage: test-idiom.sh [--catalog game-index.json]");
            return 2;
        }
        Console.WriteLine("Idiom checks: " + checks + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static IdiomEntry E(string id, string word, string first = "yi", string last = "yi", bool common = false)
    {
        return new IdiomEntry(id, word, "yī xīn yī yì", first, last, "sense-" + id, "00", common);
    }

    private static IdiomLexicon L(params IdiomEntry[] entries) { return new IdiomLexicon(entries, "test-v1"); }
    private static IdiomLexicon Loops(int count = 16)
    {
        const string middles = "心意生世帆风气发扬光大有为民命定国安邦守功名成山水清明月";
        var rows = new List<IdiomEntry>();
        for (int i = 0; i < count; i++) rows.Add(E("loop-" + i.ToString("D2"), "一" + middles[i] + "心一", common: i % 2 == 0));
        return new IdiomLexicon(rows, "loop-v1");
    }

    private static IdiomSnapshot S(IdiomMode mode, IdiomActor first, IdiomEndReason end,
                                   IEnumerable<string> history, IEnumerable<IdiomHint> hints = null, uint random = 17)
    {
        return new IdiomSnapshot(mode, first, end, random, history, hints ?? new IdiomHint[0]);
    }
    private static IdiomGame Waiting(IdiomLexicon lex, IdiomMode mode = IdiomMode.SameCharacter)
    {
        return new IdiomGame(lex, S(mode, IdiomActor.Player, IdiomEndReason.None, new string[0]));
    }
    private static string UnusedWord(IdiomGame game, IdiomLexicon lex)
    {
        return lex.Entries.First(e => !game.History.Contains(e.Id)).Word;
    }
    private static string Stamp(IdiomSnapshot s)
    {
        return s.Mode + "/" + s.FirstActor + "/" + s.EndReason + "/" + s.RandomState + "/" +
               string.Join(",", s.History) + "/" + string.Join(";", s.Hints.Select(h => h.TurnIndex + ":" + string.Join(",", h.EntryIds)));
    }

    private static void Entries()
    {
        var e = E("unicode", "𠮷祥如意");
        Assert(e.FirstCharacter == "𠮷" && e.LastCharacter == "意", "surrogate pair is one first character");
        Throws<ArgumentException>(delegate { E("bad", "一心一"); });
        Throws<ArgumentException>(delegate { E("bad", "一A一意"); });
        Throws<ArgumentException>(delegate { E("bad", "\ud840一心意"); });
        Throws<ArgumentException>(delegate { L(E("a", "一心一意"), E("a", "意气风发")); });
        Throws<ArgumentException>(delegate { L(E("a", "一心一意"), E("b", "一心一意")); });
        var lex = L(e); IdiomEntry found;
        Assert(lex.Count == 1 && lex.Version == "test-v1" && lex.TryGetById("unicode", out found) && found == e, "identity lookup");
        Assert(lex.TryGetWord(e.Word, out found) && !lex.TryGetWord("bad", out found), "word lookup");
        Assert(!lex.TryGetById(null, out found), "null lookup is absent");
    }

    private static void Starters()
    {
        foreach (IdiomMode mode in new[] { IdiomMode.SameCharacter, IdiomMode.SameSound })
        foreach (IdiomActor first in new[] { IdiomActor.App, IdiomActor.Player })
        {
            var lex = Loops(); var game = new IdiomGame(lex, mode, 0);
            Assert(!game.IsStarted && game.FirstActor == IdiomActor.None && game.History.Count == 0, "no opening draw before choice");
            var before = Stamp(game.GetSnapshot());
            Assert(game.Submit(lex.Entries[0].Word).Error == IdiomError.NotStarted, "must choose starter");
            Assert(Stamp(game.GetSnapshot()) == before, "unstarted action atomic");
            Assert(game.Start(first).Success && game.IsStarted && game.FirstActor == first, "starter accepted");
            Assert(game.History.Count == (first == IdiomActor.App ? 1 : 0), "opening history");
            Assert(game.Start(first).Error == IdiomError.AlreadyStarted, "starter fixed");
            Assert(game.Submit(UnusedWord(game, lex)).Success, "valid player word");
            Assert(game.PlayerCount == 1 && game.History.Count == (first == IdiomActor.App ? 3 : 2), "player first counts and APP reply atomic");
        }
    }

    private static IdiomLexicon ModeLexicon()
    {
        return L(E("start", "一心一意", "yi", "yi"), E("char", "意气风发", "yi", "fa"),
                 E("sound", "一帆风顺", "yi", "shun"), E("next", "顺风顺水", "shun", "shui"),
                 E("char-next", "发扬光大", "fa", "da"), E("last", "大有作为", "da", "wei"));
    }
    private static void Modes()
    {
        var lex = ModeLexicon();
        var chars = new IdiomGame(lex, S(IdiomMode.SameCharacter, IdiomActor.App, IdiomEndReason.None, new[] { "start" }));
        Assert(chars.Submit("一帆风顺").Error == IdiomError.WrongStart, "same sound is not same character");
        Assert(chars.Submit("意气风发").Success, "exact first character allowed");
        var sound = new IdiomGame(lex, S(IdiomMode.SameSound, IdiomActor.App, IdiomEndReason.None, new[] { "start" }));
        Assert(sound.Submit("一帆风顺").Success, "tone-free yi connects different characters");
    }

    private static void SoundKeys()
    {
        foreach (var pair in new[] { new[] { "lv", "lu" }, new[] { "zhi", "zi" }, new[] { "lin", "ling" }, new[] { "chang", "zhang" } })
        {
            var lex = L(E("a", "甲一一乙", "jia", pair[0]), E("good", "丙一一丁", pair[0], "yi"),
                        E("bad", "戊一一己", pair[1], "yi"), E("more", "庚一一辛", "yi", "jia"));
            var game = new IdiomGame(lex, S(IdiomMode.SameSound, IdiomActor.App, IdiomEndReason.None, new[] { "a" }));
            Assert(game.Submit("戊一一己").Error == IdiomError.WrongStart, "distinct contextual sound keys");
            Assert(game.Submit("丙一一丁").Success, "matching contextual key");
        }
    }

    private static void InvalidActions()
    {
        var lex = Loops(); var game = Waiting(lex); game.RequestHint();
        foreach (var text in new[] { null, "", "一心一", "一心一意。", "一 心一意", "一A一意", "\ud840一心意" })
        {
            var before = Stamp(game.GetSnapshot());
            Assert(game.Submit(text).Error == IdiomError.InvalidWord, "invalid four Han input");
            Assert(Stamp(game.GetSnapshot()) == before, "invalid input preserves RNG and hints");
        }
        var prior = Stamp(game.GetSnapshot());
        Assert(game.Submit("四海为家").Error == IdiomError.UnknownWord && prior == Stamp(game.GetSnapshot()), "unknown is atomic");
        Assert(game.Submit("  " + lex.Entries[0].Word + "\t").Success, "trim only outer whitespace");
    }

    private static void RepeatedWords()
    {
        var lex = Loops(); var game = new IdiomGame(lex, IdiomMode.SameCharacter, 3); game.Start(IdiomActor.App);
        Assert(game.Submit(UnusedWord(game, lex)).Success, "round accepted");
        var ids = game.History.ToArray(); var before = Stamp(game.GetSnapshot());
        foreach (string id in ids)
        {
            IdiomEntry e; lex.TryGetById(id, out e);
            Assert(game.Submit(e.Word).Error == IdiomError.RepeatedWord, "APP and player words both used");
            Assert(Stamp(game.GetSnapshot()) == before, "repeat has no side effects");
        }
    }

    private static void AvoidDeadEnds()
    {
        var lex = L(E("first", "甲一一乙", "jia", "yi"), E("dead", "乙一一丁", "yi", "ding", true),
                    E("live", "乙一一丙", "yi", "bing"), E("after", "丙一一戊", "bing", "wu"),
                    E("spare", "己一一庚", "ji", "geng"));
        var game = Waiting(lex); game.Submit("甲一一乙");
        Assert(game.LastEntry.Id == "live" && !game.IsEnded, "live reply beats common dead reply");
    }

    private static void CommonChoices()
    {
        var lex = L(E("first", "甲一一乙"), E("uncommon", "乙一一丙"), E("common", "乙二二丙", common: true),
                    E("after", "丙一一丁"));
        var game = Waiting(lex); game.Submit("甲一一乙");
        Assert(game.LastEntry.Id == "common", "common preference within continuation tier");
        var opening = new IdiomGame(lex, IdiomMode.SameCharacter, 8); opening.Start(IdiomActor.App);
        Assert(opening.LastEntry.Id == "common", "common opening with continuation");
    }

    private static void MoreContinuations()
    {
        var lex = L(E("first", "甲一一乙"), E("few", "乙一一丙"), E("many", "乙一一丁"),
                    E("a", "丙一一戊"), E("b", "丁一一己"), E("c", "丁二二己"), E("d", "丁三三己"));
        var game = Waiting(lex); game.Submit("甲一一乙");
        Assert(game.LastEntry.Id == "many", "at least three future options preferred");
    }

    private static void CommonOpening()
    {
        var lex = L(E("common", "甲一一乙", common: true), E("after", "乙一一戊"),
                    E("uncommon", "丙一一丁"), E("a", "丁一一己"), E("b", "丁二二己"), E("c", "丁三三己"));
        var game = new IdiomGame(lex, IdiomMode.SameCharacter, 8); game.Start(IdiomActor.App);
        Assert(game.LastEntry.Id == "common", "opening must use curated common set when it has a continuation");
    }

    private static void Determinism()
    {
        var lex = Loops(); var reversed = new IdiomLexicon(lex.Entries.Reverse(), lex.Version);
        var openings = new HashSet<string>();
        for (uint seed = 1; seed <= 40; seed++)
        {
            var a = new IdiomGame(lex, IdiomMode.SameCharacter, seed);
            var b = new IdiomGame(reversed, IdiomMode.SameCharacter, seed);
            a.Start(IdiomActor.App); b.Start(IdiomActor.App); openings.Add(a.LastEntry.Id);
            Assert(Stamp(a.GetSnapshot()) == Stamp(b.GetSnapshot()), "catalog input order cannot alter RNG choices");
            for (int i = 0; i < 3; i++)
            {
                b = new IdiomGame(reversed, a.GetSnapshot()); string word = UnusedWord(a, lex);
                a.Submit(word); b.Submit(word);
                Assert(Stamp(a.GetSnapshot()) == Stamp(b.GetSnapshot()), "persistent RNG continuation identical");
            }
        }
        Assert(openings.Count > 1, "different seeds can choose different equally good words");
    }

    private static void HintCaching()
    {
        var lex = Loops(); var game = Waiting(lex);
        var hint = game.RequestHint(); var before = Stamp(game.GetSnapshot());
        Assert(hint.TurnIndex == 0 && hint.EntryIds.Count == 3 && game.HintCount == 1 && game.PlayerCount == 0, "player-first opening hint");
        for (int i = 0; i < 6; i++) Assert(game.RequestHint() == hint && Stamp(game.GetSnapshot()) == before, "cached view does not charge");
        var restored = new IdiomGame(lex, game.GetSnapshot());
        Assert(restored.CurrentHint != null && restored.RequestHint().EntryIds.SequenceEqual(hint.EntryIds) && restored.HintCount == 1, "restart reopens same hint");
        IdiomEntry entry; lex.TryGetById(hint.EntryIds[0], out entry);
        Assert(game.Submit(entry.Word).Success && game.CurrentHint == null, "hint was not used until submit and old hint not current");
        Assert(game.RequestHint().TurnIndex == 2 && game.HintCount == 2, "next player turn can request a new group");
    }

    private static void HintRandomness()
    {
        var lex = Loops(); var a = Waiting(lex); var b = Waiting(lex);
        uint state = a.GetSnapshot().RandomState; a.RequestHint();
        Assert(a.GetSnapshot().RandomState == state && a.History.Count == 0, "hint neither advances RNG nor occupies words");
        a.Submit(lex.Entries[0].Word); b.Submit(lex.Entries[0].Word);
        Assert(a.History.SequenceEqual(b.History) && a.GetSnapshot().RandomState == b.GetSnapshot().RandomState, "hint cannot influence later APP draw");
    }

    private static void ThirdHint()
    {
        var lex = Loops(); var game = Waiting(lex);
        for (int i = 0; i < 2; i++) { game.RequestHint(); Assert(game.Submit(UnusedWord(game, lex)).Success, "hint-assisted player word"); }
        int length = game.History.Count; var hint = game.RequestHint();
        Assert(game.IsEnded && game.EndReason == IdiomEndReason.HintLimit && game.HintCount == 3, "third event atomically ends");
        Assert(game.PlayerCount == 2 && game.History.Count == length && hint.TurnIndex == length, "third suggestions do not become messages");
        var before = Stamp(game.GetSnapshot());
        Assert(game.Submit(UnusedWord(game, lex)).Error == IdiomError.AlreadyEnded, "no send after third hint");
        Assert(game.RequestHint() == hint && Stamp(game.GetSnapshot()) == before, "terminal hint can be reread");
        Assert(Stamp(new IdiomGame(lex, game.GetSnapshot()).GetSnapshot()) == before, "third-hint terminal restores exactly");
    }

    private static void InactiveHints()
    {
        var game = new IdiomGame(Loops(), IdiomMode.SameCharacter, 5); string before = Stamp(game.GetSnapshot());
        Assert(game.RequestHint() == null && Stamp(game.GetSnapshot()) == before, "no hints before starter");
        game.Start(IdiomActor.Player); game.Finish(); before = Stamp(game.GetSnapshot());
        Assert(game.RequestHint() == null && Stamp(game.GetSnapshot()) == before, "finished without hint stays empty");
    }
    private static void Finish()
    {
        var lex = Loops(); var game = Waiting(lex); game.Submit(lex.Entries[0].Word); game.Finish();
        string before = Stamp(game.GetSnapshot()); game.Finish();
        Assert(game.EndReason == IdiomEndReason.EndedByPlayer && game.PlayerCount == 1 && Stamp(game.GetSnapshot()) == before, "finish is idempotent");
        Assert(game.Submit(UnusedWord(game, lex)).Error == IdiomError.AlreadyEnded, "finished game frozen");
        Assert(Stamp(new IdiomGame(lex, game.GetSnapshot()).GetSnapshot()) == before, "manual finish restores");
    }

    private static void Exhaustion()
    {
        var lex = L(E("one", "一心一意"));
        foreach (IdiomActor first in new[] { IdiomActor.Player, IdiomActor.App })
        {
            var game = new IdiomGame(lex, IdiomMode.SameCharacter, 3); game.Start(first);
            if (first == IdiomActor.Player) Assert(game.Submit("一心一意").Success, "last available player word accepted");
            Assert(game.EndReason == IdiomEndReason.DictionaryExhausted, "exhaustion outranks next actor having no words");
            Assert(game.PlayerCount == (first == IdiomActor.Player ? 1 : 0), "correct last actor count");
            new IdiomGame(lex, game.GetSnapshot());
        }
    }
    private static void NoMoves()
    {
        var lex = L(E("first", "甲一一乙"), E("spare", "丙一一丁"));
        var game = Waiting(lex); Assert(game.Submit("甲一一乙").Success, "unanswerable first player word accepted");
        Assert(game.EndReason == IdiomEndReason.AppNoMoves && game.PlayerCount == 1, "APP cannot reply");
        new IdiomGame(lex, game.GetSnapshot());
        lex = L(E("first", "甲一一乙"), E("reply", "乙一一丁"), E("spare", "戊一一己"));
        game = Waiting(lex); game.Submit("甲一一乙");
        Assert(game.EndReason == IdiomEndReason.PlayerNoMoves && game.History.Count == 2, "APP actual reply leaves player without moves");
        new IdiomGame(lex, game.GetSnapshot());
    }

    private static void Copies()
    {
        var lex = Loops(); var ids = new List<string> { lex.Entries[0].Id }; var hints = new List<IdiomHint> { new IdiomHint(0, ids) };
        var history = new List<string>(); var snapshot = S(IdiomMode.SameCharacter, IdiomActor.Player, IdiomEndReason.None, history, hints);
        ids.Clear(); hints.Clear(); history.Add("broken");
        Assert(snapshot.History.Count == 0 && snapshot.Hints.Count == 1 && snapshot.Hints[0].EntryIds.Count == 1, "nested input lists copied");
        Throws<NotSupportedException>(delegate { ((IList<string>)snapshot.History).Add("bad"); });
        Throws<NotSupportedException>(delegate { ((IList<string>)snapshot.Hints[0].EntryIds)[0] = "bad"; });
        var game = new IdiomGame(lex, snapshot); var before = Stamp(game.GetSnapshot());
        Throws<NotSupportedException>(delegate { ((IList<string>)game.History).Add("bad"); });
        Assert(Stamp(game.GetSnapshot()) == before, "game owns exported state");
        var source = lex.Entries.ToList(); var other = new IdiomLexicon(source, "copy"); source.Clear();
        Assert(other.Count == lex.Count, "lexicon copies enumeration");
    }

    private static void BadHeaders()
    {
        var lex = Loops();
        Reject(lex, S((IdiomMode)2, IdiomActor.None, IdiomEndReason.None, new string[0]));
        Reject(lex, S(IdiomMode.SameCharacter, (IdiomActor)3, IdiomEndReason.None, new string[0]));
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.None, (IdiomEndReason)9, new string[0]));
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.None, IdiomEndReason.None, new string[0], random: 0));
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.None, IdiomEndReason.None, new[] { lex.Entries[0].Id }));
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.None, IdiomEndReason.EndedByPlayer, new string[0]));
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.None, IdiomEndReason.None, new string[0], new[] { new IdiomHint(0, new[] { lex.Entries[0].Id }) }));
        Throws<ArgumentException>(delegate { new IdiomGame(lex, (IdiomSnapshot)null); });
    }
    private static void BadHistory()
    {
        var lex = Loops(); string a = lex.Entries[0].Id, b = lex.Entries[1].Id;
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.Player, IdiomEndReason.None, new[] { "missing", b }));
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.Player, IdiomEndReason.None, new[] { a, a }));
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.Player, IdiomEndReason.None, new[] { a }));
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.App, IdiomEndReason.None, new string[0]));
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.App, IdiomEndReason.None, new[] { a, b }));
        var different = L(E("a", "甲一一乙"), E("b", "丙一一丁"), E("c", "丁一一戊"));
        Reject(different, S(IdiomMode.SameCharacter, IdiomActor.Player, IdiomEndReason.None, new[] { "a", "b" }));
    }
    private static void BadEndReasons()
    {
        var lex = Loops();
        foreach (IdiomEndReason reason in new[] { IdiomEndReason.AppNoMoves, IdiomEndReason.PlayerNoMoves,
                                                  IdiomEndReason.DictionaryExhausted, IdiomEndReason.HintLimit })
            Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.Player, reason, new string[0]));
        var dead = L(E("a", "甲一一乙"), E("b", "丙一一丁"));
        Reject(dead, S(IdiomMode.SameCharacter, IdiomActor.App, IdiomEndReason.None, new[] { "a" }));
        Reject(dead, S(IdiomMode.SameCharacter, IdiomActor.App, IdiomEndReason.EndedByPlayer, new[] { "a" }));
        Reject(dead, S(IdiomMode.SameCharacter, IdiomActor.Player, IdiomEndReason.PlayerNoMoves, new[] { "a" }));
        Reject(dead, S(IdiomMode.SameCharacter, IdiomActor.App, IdiomEndReason.AppNoMoves, new[] { "a" }));
    }
    private static void HistoricalHints()
    {
        var lex = Loops(); var game = Waiting(lex); var hint = game.RequestHint();
        IdiomEntry chosen; lex.TryGetById(hint.EntryIds[0], out chosen); game.Submit(chosen.Word);
        Assert(game.History.Contains(chosen.Id), "hint later appears in accepted history");
        var restored = new IdiomGame(lex, game.GetSnapshot());
        Assert(restored.HintCount == 1 && restored.CurrentHint == null, "old hint checked before its candidate was used");
    }
    private static void BadHints()
    {
        var lex = Loops(); var ids = lex.Entries.Select(e => e.Id).ToArray();
        foreach (IdiomHint hint in new[] { new IdiomHint(-1, new[] { ids[0] }), new IdiomHint(1, new[] { ids[0] }),
            new IdiomHint(0, new string[0]), new IdiomHint(0, ids.Take(4)), new IdiomHint(0, new[] { ids[0], ids[0] }),
            new IdiomHint(0, new[] { "absent" }) })
            Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.Player, IdiomEndReason.None, new string[0], new[] { hint }));
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.Player, IdiomEndReason.None, new string[0],
                      new[] { new IdiomHint(0, new[] { ids[0] }), new IdiomHint(0, new[] { ids[1] }) }));
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.Player, IdiomEndReason.None, ids.Take(2), new[] { new IdiomHint(2, new[] { ids[0] }) }));
        var events = new[] { new IdiomHint(0, new[] { ids[0] }), new IdiomHint(2, new[] { ids[2] }), new IdiomHint(4, new[] { ids[4] }) };
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.Player, IdiomEndReason.None, ids.Take(4), events));
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.Player, IdiomEndReason.HintLimit, ids.Take(6), events));
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.Player, IdiomEndReason.EndedByPlayer, ids.Take(4), events));
        Reject(lex, S(IdiomMode.SameCharacter, IdiomActor.Player, IdiomEndReason.HintLimit, ids.Take(4), events.Take(2)));
        var mismatched = ModeLexicon();
        Reject(mismatched, S(IdiomMode.SameCharacter, IdiomActor.App, IdiomEndReason.None, new[] { "start" }, new[] { new IdiomHint(1, new[] { "sound" }) }));
    }

    private static void Replay()
    {
        var lex = Loops(24);
        for (uint seed = 1; seed <= 120; seed++)
        foreach (IdiomMode mode in new[] { IdiomMode.SameCharacter, IdiomMode.SameSound })
        {
            var game = new IdiomGame(lex, mode, seed); game.Start(seed % 2 == 0 ? IdiomActor.App : IdiomActor.Player);
            for (int turn = 0; turn < 30 && !game.IsEnded; turn++)
            {
                if ((seed + turn) % 3 == 0) { game.RequestHint(); game.RequestHint(); }
                Assert(Stamp(game.GetSnapshot()) == Stamp(new IdiomGame(lex, game.GetSnapshot()).GetSnapshot()), "every public snapshot restores");
                if (!game.IsEnded) game.Submit(UnusedWord(game, lex));
            }
            Assert(game.IsEnded, "bounded finite lexicon or third hint terminates");
            new IdiomGame(lex, game.GetSnapshot());
        }
    }

    private static void Reject(IdiomLexicon lex, IdiomSnapshot snapshot) { Throws<ArgumentException>(delegate { new IdiomGame(lex, snapshot); }); }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    private static void Run(string name, Action action)
    {
        try { action(); checks++; Console.WriteLine("PASS " + name); }
        catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
    }
}
