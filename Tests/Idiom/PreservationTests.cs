using System;
using System.Linq;
using XanaduYonder.Core.Idiom;

internal static class PreservationTests
{
    private static IdiomEntry E(string id, string word, bool common = false)
    { return new IdiomEntry(id, word, "a a a a", "a", "a", "s-" + id, "00", common); }
    private static void A(bool value, string message) { if (!value) throw new Exception(message); }
    private static IdiomGame G(IdiomLexicon lex, IdiomMode mode = IdiomMode.SameCharacter, IdiomMatchKind kind = IdiomMatchKind.Computer)
    { var g = new IdiomGame(lex, mode, 17, kind); g.Start(IdiomActor.Player); return g; }
    public static void CommonWithRareSuccessor()
    {
        var lex = new IdiomLexicon(new[] { E("a", "明知故问", true), E("b", "问心无愧", true), E("c", "愧不敢当"), E("d", "当仁不让", true) }, "preserve");
        var g = G(lex); A(g.Submit("明知故问").Success, "opening accepted");
        A(g.History.Count == 2 && g.LastEntry.Word == "问心无愧" && !g.IsEnded, "a common reply with only rare successors must be selected");
        A(g.Submit("愧不敢当").Success && g.LastEntry.Word == "当仁不让", "rare manual move and common reply stay legal");
        new IdiomGame(lex, g.GetSnapshot());
    }
    public static void FullLexiconFallback()
    {
        var lex = new IdiomLexicon(new[] { E("a", "甲甲甲乙", true), E("b", "乙乙乙丙"), E("c", "丙丙丙丁") }, "fallback");
        var g = G(lex); g.Submit("甲甲甲乙");
        A(g.History.Count == 2 && g.LastEntry.Id == "b" && !g.IsEnded, "empty preferred pools must fall back to legal rare words");
        g.Submit("丙丙丙丁"); A(g.EndReason == IdiomEndReason.DictionaryExhausted, "actual full dictionary exhaustion still ends");
        new IdiomGame(lex, g.GetSnapshot());
        g = G(lex, kind: IdiomMatchKind.Local); g.Submit("甲甲甲乙");
        A(g.RequestHint().EntryIds.SequenceEqual(new[] { "b" }), "hints also offer the only rare legal word");
        new IdiomGame(lex, g.GetSnapshot());
    }
    public static void HintFill()
    {
        var lex = new IdiomLexicon(new[] { E("a", "甲甲甲甲", true), E("b", "乙乙乙乙"), E("c", "丙丙丙丙"), E("d", "丁丁丁丁") }, "fill");
        var g = G(lex, kind: IdiomMatchKind.Local); var hint = g.RequestHint();
        A(hint.EntryIds.Count == 3 && hint.EntryIds.Contains("a"), "keep preferred hint and fill remaining slots from all legal words, including dead ends");
        new IdiomGame(lex, g.GetSnapshot());
    }
    private static IdiomEntry Multi(string id, string word, params string[] keys)
    {
        var readings = new System.Collections.Generic.List<IdiomSoundReading>();
        for (int i = 0; i < keys.Length; i += 2) readings.Add(new IdiomSoundReading(keys[i], keys[i + 1]));
        return new IdiomEntry(id, word, "原始读音", keys.Length == 0 ? "" : keys[0], keys.Length == 0 ? "" : keys[1], "s-" + id, "00", true, readings);
    }
    public static void PairedReadings()
    {
        var entries = new[] { Multi("a", "甲甲甲乙", "z", "a"), Multi("b", "乙乙乙丙", "a", "b", "c", "d"), Multi("c", "丙丙丙丁", "b", "z"), Multi("d", "丁丁丁甲", "d", "z") };
        var lex = new IdiomLexicon(entries, "paired"); var g = G(lex, IdiomMode.SameSound, IdiomMatchKind.Local);
        A(g.Submit(entries[0].Word).Success && g.Submit(entries[1].Word).Success, "matching one source reading accepted");
        A(g.Submit(entries[3].Word).Error == IdiomError.WrongStart, "must not mix first sound from one reading with tail from another");
        A(g.RequestHint().EntryIds.SequenceEqual(new[] { "c" }), "hint follows active paired sound path");
        g = new IdiomGame(lex, g.GetSnapshot()); A(g.Submit(entries[3].Word).Error == IdiomError.WrongStart, "restored path rejects cross-source pairing");
        A(g.Submit(entries[2].Word).Success && g.IsEnded, "only genuine paired successors count for natural ending");
        new IdiomGame(lex, g.GetSnapshot());
        g = G(lex, IdiomMode.SameSound, IdiomMatchKind.Local); g.Submit(entries[1].Word);
        A(g.RequestHint().EntryIds.OrderBy(s => s).SequenceEqual(new[] { "c", "d" }), "opening admits both source readings without preferring one");
        var bad = new IdiomSnapshot(IdiomMode.SameSound, IdiomActor.Player, IdiomEndReason.None, 17, new[] { "a", "b", "d" }, new IdiomHint[0], IdiomMatchKind.Local);
        try { new IdiomGame(lex, bad); throw new Exception("forged mixed-reading history accepted"); } catch (ArgumentException) { }
    }
    public static void UnparsedReading()
    {
        var lex = new IdiomLexicon(new[] { Multi("a", "甲甲甲乙"), Multi("b", "乙乙乙甲", "yi", "jia") }, "unparsed");
        var g = G(lex, kind: IdiomMatchKind.Local); A(g.Submit("甲甲甲乙").Success && !g.IsEnded, "missing reading never removes same-character entry");
        g = G(lex, IdiomMode.SameSound, IdiomMatchKind.Local);
        A((int)g.Submit("甲甲甲乙").Error == 8 && g.History.Count == 0, "same-sound gives distinct unavailable-reading error without mutation");
        A(g.RequestHint().EntryIds.SequenceEqual(new[] { "b" }), "same-sound must not recommend unparsed readings");
        new IdiomGame(lex, g.GetSnapshot());
    }
    public static void NaturalEndRequiresWholeLexiconExhaustion()
    {
        var lex = new IdiomLexicon(new[]
        {
            Multi("a", "甲甲甲乙", "jia", "yi"), Multi("b", "乙乙乙丙", "yi", "bing", "yi", "ding"),
            Multi("c", "丙丙丙丁", "bing", "ding"), Multi("d", "丁丁丁甲", "ding", "jia"),
            Multi("e", "乙甲甲乙", "yi", "yi"), E("f", "乙甲甲丙"), Multi("g", "戊戊戊己")
        }, "hard-rule");
        foreach (IdiomMode mode in Enum.GetValues(typeof(IdiomMode)))
        foreach (IdiomMatchKind kind in Enum.GetValues(typeof(IdiomMatchKind)))
        for (uint seed = 1; seed <= 100; seed++)
        {
            var profile = new IdiomLearningProfile(lex);
            profile.AddNotebook("b", 1700000000L);
            var game = new IdiomGame(lex, mode, seed, kind, profile, () => 1700000000L);
            game.Start(seed % 2 == 0 ? IdiomActor.App : IdiomActor.Player);
            for (var turn = 0; turn <= lex.Count; turn++)
            {
                string[] sounds = null;
                foreach (string id in game.History)
                {
                    IdiomEntry accepted; lex.TryGetById(id, out accepted);
                    sounds = accepted.SoundReadings.Where(r => sounds == null || sounds.Contains(r.FirstKey))
                        .Select(r => r.LastKey).Distinct().ToArray();
                }
                var legal = lex.Entries.Where(e => !game.History.Contains(e.Id) &&
                    (mode == IdiomMode.SameCharacter ? game.LastEntry == null || e.FirstCharacter == game.LastEntry.LastCharacter :
                    e.SoundReadings.Any(r => sounds == null || sounds.Contains(r.FirstKey)))).ToArray();
                A(game.IsEnded == (legal.Length == 0), "automatic ending must agree with independently enumerated entire lexicon, regardless of preferences or hints");
                new IdiomGame(lex, game.GetSnapshot(), profile, () => 1700000000L);
                if (game.IsEnded) break;
                game.RequestHint(); A(!game.IsEnded, "a hint cannot end a game with legal moves");
                A(game.Submit(legal[(int)((seed + turn) % legal.Length)].Word).Success, "a legal word can always advance the round");
            }
            A(game.IsEnded, "finite nonrepeating dictionary eventually has no moves");
        }
    }
}
