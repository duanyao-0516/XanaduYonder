using System;
using System.Collections.Generic;
using System.Linq;
using XanaduYonder.Core.Idiom;

internal static class LearningTests
{
    private const long Now = 1800000000;
    private static void A(bool value, string message) { if (!value) throw new Exception(message); }
    private static IdiomEntry E(string id, string word, bool common = true)
    { return new IdiomEntry(id, word, "yī yī yī yī", "yi", "yi", "sense-" + id, "0", common); }
    private static IdiomLexicon Loop()
    { return new IdiomLexicon(new[] { E("a", "一甲甲一"), E("b", "一乙乙一"), E("c", "一丙丙一"), E("d", "一丁丁一"), E("e", "一戊戊一", false), E("f", "一己己一", false) }, "learning"); }
    private static IdiomLearningProfile P(IdiomLexicon lex) { return new IdiomLearningProfile(lex); }
    private static IdiomGame G(IdiomLexicon lex, IdiomLearningProfile profile, uint seed = 17, long now = Now, IdiomMatchKind kind = IdiomMatchKind.Computer)
    { return new IdiomGame(lex, IdiomMode.SameCharacter, seed, kind, profile, () => now); }
    private static IdiomGame Restore(IdiomLexicon lex, IdiomSnapshot s, IdiomLearningProfile profile, long now = Now)
    { return new IdiomGame(lex, s, profile, () => now); }
    private static int Count(IdiomLearningProfile p, string id, bool assisted)
    {
        foreach (var use in p.GetSnapshot().Uses)
            if (use.EntryId == id) return assisted ? (int)use.AssistedUses : (int)use.IndependentUses;
        return 0;
    }
    public static void PositiveAllWords()
    {
        var lex = Loop(); var counts = lex.Entries.ToDictionary(e => e.Id, e => 0);
        for (uint seed = 1; seed <= 10000; seed++)
        {
            var profile = P(lex); profile.AddNotebook("e", Now);
            var game = G(lex, profile, seed); game.Start(IdiomActor.App);
            A(game.LastEntry != null, "a legal opening exists"); counts[game.LastEntry.Id]++;
        }
        A(counts.Values.All(n => n > 0), "ordinary, due review and every other legal word retain positive probability");
        A(counts["f"] > 350 && counts["f"] < 650, "remaining legal pool receives approximately five percent");
        A(counts["e"] > 1700 && counts["e"] < 2100, "due review receives twenty percent within the ninety-five percent preferred pool");
        int ordinary = counts["a"] + counts["b"] + counts["c"] + counts["d"];
        A(ordinary > 7300 && ordinary < 7900, "ordinary words receive eighty percent within the preferred pool");
        var onlyRare = new IdiomLexicon(new[] { E("rare", "甲一一乙", false), E("spare", "丙一一丁", false) }, "only-rare");
        var seen = new HashSet<string>();
        for (uint seed = 1; seed <= 100; seed++)
        {
            var game = G(onlyRare, P(onlyRare), seed); game.Start(IdiomActor.App);
            A(game.LastEntry != null && game.EndReason == IdiomEndReason.PlayerNoMoves, "a legal zero-successor word may open and end naturally");
            seen.Add(game.LastEntry.Id);
        }
        A(seen.Count == 2, "remaining pool gets all selection probability when preferred pool is empty");
    }
    public static void Pools()
    {
        var lex = Loop(); IdiomLearningProfile p = P(lex); p.Remember("e"); p.AddNotebook("c", Now);
        var seen = new HashSet<string>();
        for (uint seed = 1; seed <= 1000; seed++)
        {
            IdiomLearningProfile fresh = P(lex); fresh.ReplaceSnapshot(p.GetSnapshot());
            var game = G(lex, fresh, seed); game.Start(IdiomActor.App); seen.Add(game.LastEntry.Id);

        }
        A(seen.SetEquals(lex.Entries.Select(e => e.Id)), "familiar, notebook and unfamiliar legal words all remain reachable");
        var human = G(lex, p); human.Start(IdiomActor.Player);
        A(human.Submit(lex.Entries.Single(e => e.Id == "f").Word).Success, "human input uses full lexicon");
        A(p.IsFamiliar("f"), "independent valid unfamiliar input becomes familiar immediately");
    }
    public static void Learning()
    {
        var lex = Loop(); IdiomLearningProfile p = P(lex);
        var game = G(lex, p); game.Start(IdiomActor.Player); game.RecordDefinitionViewed("e");
        game = Restore(lex, game.GetSnapshot(), p);
        A(game.Submit(lex.Entries.Single(e => e.Id == "e").Word).Success && !p.IsFamiliar("e"), "definition-aided input is not familiarity, including after restore");
        A(Count(p, "e", true) == 1 && Count(p, "e", false) == 0, "assisted use has separate evidence");
        game = G(lex, p); game.Start(IdiomActor.Player); game.Submit(lex.Entries.Single(e => e.Id == "e").Word);
        A(p.IsFamiliar("e") && Count(p, "e", false) == 1, "later independent use adds familiarity once");
        game.Submit(lex.Entries.Single(e => e.Id == "e").Word);
        A(Count(p, "e", false) == 1, "failed repeat cannot duplicate learning event");
        game = G(lex, p, kind: IdiomMatchKind.Local); game.Start(IdiomActor.Left); game.Submit(lex.Entries.Single(e => e.Id == "f").Word);
        A(!p.IsFamiliar("f") && Count(p, "f", false) == 0, "local humans do not teach phone-owner profile");
        game = G(lex, p); game.Start(IdiomActor.Player); var hint = game.RequestHint(); string shown = hint.EntryIds[hint.EntryIds.Count - 1];
        int previousAssisted = Count(p, shown, true);
        game.Submit(lex.Entries.Single(e => e.Id == shown).Word);
        A(Count(p, shown, true) == previousAssisted + 1, "entire displayed hint group is assisted even without tapping suggestion");
    }
    public static void Notebook()
    {
        var lex = Loop(); IdiomLearningProfile p = P(lex); p.Remember("e"); p.AddNotebook("e", Now);
        A(p.IsFamiliar("e") && p.IsNotebook("e"), "notebook suspends rather than erases existing familiarity");
        p.RemoveNotebook("e"); A(p.IsFamiliar("e"), "ordinary removal restores prior familiar evidence");
        p.AddNotebook("f", Now); p.RemoveNotebook("f"); A(!p.IsFamiliar("f"), "ordinary removal cannot invent familiarity");
        p.AddNotebook("f", Now); p.Remember("f"); p.Remember("f");
        A(p.IsFamiliar("f") && !p.IsNotebook("f") && Count(p, "f", false) == 0, "explicit remembered transition is idempotent, not an independent recall");
    }
    private static IdiomLexicon ReviewLex()
    { return new IdiomLexicon(new[] { E("start", "甲一一乙", false), E("review", "乙一一丙", false), E("after", "丙一一丁"), E("more", "丁一一戊") }, "review"); }
    public static void ReviewCooldown()
    {
        var lex = ReviewLex(); IdiomLearningProfile p = P(lex); p.AddNotebook("review", Now);
        var game = G(lex, p); game.Start(IdiomActor.Player); game.Submit("甲一一乙");
        A(game.LastEntry.Id == "review" && !p.IsFamiliar("review"), "first eligible review may happen immediately, without promotion");
        var firstExposure = p.GetSnapshot();
        A(!p.SelectionContext(Now + 86399, true).ReviewIds.Contains("review") && p.SelectionContext(Now + 86400, true).ReviewIds.Contains("review"), "automatic preferred review honors the exact 24-hour boundary");
        game = G(lex, p, now: Now + 86399); game.Start(IdiomActor.Player); game.Submit("甲一一乙");
        A(game.LastEntry.Id == "review" && game.EndReason == IdiomEndReason.None, "cooldown removes preferred review status but preserves the only legal fallback");
        p = new IdiomLearningProfile(lex, firstExposure);
        game = G(lex, p, now: Now + 86400); game.Start(IdiomActor.Player); game.Submit("甲一一乙");
        A(game.LastEntry.Id == "review", "review available exactly at 24 hours");
        var human = G(lex, p); human.Start(IdiomActor.Player); human.Submit("乙一一丙");
        A(!p.IsFamiliar("review") && Count(p, "review", false) == 1, "independent use of notebook word records recall but does not auto-master");
    }
    public static void Diversity()
    {
        var lex = new IdiomLexicon(new[] { E("start", "甲一一乙"), E("few", "乙一一丙"), E("many", "乙一一丁"), E("x", "丙一一戊"), E("y", "丁一一戊"), E("z", "丁二二戊"), E("w", "丁三三戊") }, "diversity");
        var counts = new Dictionary<string, int> { ["few"] = 0, ["many"] = 0 };
        for (uint seed = 1; seed <= 1000; seed++)
        {
            var game = G(lex, P(lex), seed); game.Start(IdiomActor.Player); game.Submit("甲一一乙"); counts[game.LastEntry.Id]++;
        }
        A(counts["few"] > 350 && counts["many"] > 350, "1 vs 3 continuations is mild weighting, never a hard winner");
    }
    public static void ReviewBranch()
    {
        var lex = Loop(); int review = 0;
        for (uint seed = 1; seed <= 2000; seed++)
        {
            IdiomLearningProfile p = P(lex); p.AddNotebook("e", Now);
            var game = G(lex, p, seed); game.Start(IdiomActor.App); if (game.LastEntry.Id == "e") review++;
        }
        A(review > 340 && review < 460, "20 percent review branch must not depend on pool size");
    }
    public static void Recency()
    {
        var lex = Loop(); int cold = 0, recent = 0;
        for (uint seed = 1; seed <= 1000; seed++)
        {
            IdiomLearningProfile p = P(lex); var opening = G(lex, p, 17); opening.Start(IdiomActor.App); string target = opening.LastEntry.Id;
            var game = G(lex, p, seed); game.Start(IdiomActor.App); if (game.LastEntry.Id == target) recent++;
            var baseline = G(lex, P(lex), seed); baseline.Start(IdiomActor.App); if (baseline.LastEntry.Id == target) cold++;
            A(p.GetSnapshot().RecentAppIds.Count <= 10, "recent list bounded to ten");
        }
        A(recent > 0 && recent < cold / 2, "recent candidate stays possible with quarter weight");
    }
    public static void HintExploration()
    {
        var lex = Loop(); var seen = new HashSet<string>(); int other = 0;
        for (uint seed = 1; seed <= 2000; seed++)
        {
            var profile = P(lex); profile.AddNotebook("e", Now);
            var game = G(lex, profile, seed); game.Start(IdiomActor.Player);
            uint before = game.GetSnapshot().RandomState; var hint = game.RequestHint();
            A(hint != null && hint.EntryIds.Count == 3 && hint.EntryIds.Distinct().Count() == 3, "hint group contains three distinct legal words");
            A(game.GetSnapshot().RandomState == before && game.History.Count == 0, "exploration never changes APP randomness or accepted history");
            foreach (var id in hint.EntryIds) seen.Add(id);
            if (hint.EntryIds.Contains("f")) other++;
            A(game.RequestHint() == hint && game.HintCount == 1, "exploration group is cached rather than redrawn");
        }
        A(seen.SetEquals(lex.Entries.Select(e => e.Id)), "every legal word is reachable as requested assistance");
        A(other > 50 && other < 160, "third hint slot keeps approximately five percent exploration probability");
    }
    public static void Hints()
    {
        var lex = Loop(); IdiomLearningProfile p1 = P(lex), p2 = P(lex);
        var a = G(lex, p1); var b = G(lex, p2); a.Start(IdiomActor.Player); b.Start(IdiomActor.Player);
        uint before = a.GetSnapshot().RandomState; var hint = a.RequestHint(); var snapshot = a.GetSnapshot();
        A(a.RequestHint() == hint && a.HintCount == 1 && snapshot.RandomState == before, "cached hint does not draw APP randomness");
        a = Restore(lex, snapshot, p1); A(a.RequestHint().EntryIds.SequenceEqual(hint.EntryIds), "hint cache restored exactly");
        // Common input has equal selection eligibility even though its assisted-use evidence differs.
        a.Submit(lex.Entries[0].Word); b.Submit(lex.Entries[0].Word);
        A(a.History.SequenceEqual(b.History) && a.GetSnapshot().RandomState == b.GetSnapshot().RandomState, "hints cannot alter the APP random stream");
        var groups = new HashSet<string>();
        for(uint seed=1;seed<=30;seed++) { var g=G(lex,P(lex),seed); g.Start(IdiomActor.Player); groups.Add(string.Join(",",g.RequestHint().EntryIds)); }
        A(groups.Count > 1, "hints use their own weighted random stream instead of always first three IDs");
    }
    public static void PolicyEnd()
    {
        var lex = ReviewLex(); IdiomLearningProfile p = P(lex);
        var game = G(lex,p); game.Start(IdiomActor.Player); game.Submit("甲一一乙");
        A(game.LastEntry.Id == "review" && !game.IsEnded, "new APP turns use legal rare fallback instead of policy exhaustion");
        var snapshot = new IdiomSnapshot(IdiomMode.SameCharacter, IdiomActor.Player, IdiomEndReason.RecommendationExhausted, 17,
            new[] { "start" }, new IdiomHint[0], IdiomMatchKind.Computer, 19, new string[0],
            new IdiomSelectionContext(new[] { "start", "after", "more" }, new string[0]));
        p.AddNotebook("review",Now);
        var legacy = Restore(lex, snapshot, p);
        A(legacy.EndReason == IdiomEndReason.RecommendationExhausted && legacy.Submit("乙一一丙").Error == IdiomError.AlreadyEnded, "legacy policy terminal keeps its frozen witness and stays read-only");
        var next=G(lex,p);next.Start(IdiomActor.Player);next.Submit("甲一一乙");
        A(next.LastEntry.Id=="review","new actions in another slot use current shared notebook state");
    }
    public static void HintQuotas()
    {
        var lex = Loop();
        for(uint seed=1;seed<=60;seed++)
        {
            var p=P(lex); p.AddNotebook("e",Now); p.AddNotebook("f",Now);
            var game=G(lex,p,seed); game.Start(IdiomActor.Player); var hint=game.RequestHint();
            A(hint.EntryIds.Count==3 && hint.EntryIds.Count(id=>p.IsNotebook(id))==1,"reserve one review and two ordinary hints");
        }
        var normal=G(lex,P(lex)); normal.Start(IdiomActor.Player);
        A(normal.RequestHint().EntryIds.Count==3,"missing review slot fills from ordinary pool");
    }
    public static void OrdinaryThenNotebook()
    {
        var lex=ReviewLex(); var p=P(lex); p.Remember("review");
        var game=G(lex,p); game.Start(IdiomActor.Player); game.Submit("甲一一乙");
        A(game.LastEntry.Id=="review","familiar word may first be an ordinary APP reply");
        p.AddNotebook("review",Now+10);
        var next=G(lex,p,now:Now+10); next.Start(IdiomActor.Player); next.Submit("甲一一乙");
        A(!p.SelectionContext(Now+10,true).ReviewIds.Contains("review") && next.LastEntry.Id=="review", "ordinary exposure delays preferred review but cannot remove the only legal fallback");
        var local=G(lex,p,now:Now+10,kind:IdiomMatchKind.Local); local.Start(IdiomActor.Left); local.Submit("甲一一乙");
        var hint=local.RequestHint();
        A(hint!=null && hint.EntryIds.Contains("review"),"requested learning hint may show N during automatic review cooldown");
        A(p.GetSnapshot().AppExposures.Single(e=>e.EntryId=="review").LastShownUtc==Now+10,"hints do not reset the most recent fallback exposure timestamp");
    }
    public static void SharedUpdates()
    {
        var lex=ReviewLex();var p=P(lex);
        var waiting=G(lex,p); waiting.Start(IdiomActor.Player);
        var teacher=G(lex,p);teacher.Start(IdiomActor.Player);teacher.Submit("乙一一丙");
        waiting.Submit("甲一一乙");
        A(waiting.LastEntry.Id=="review","existing game consults shared updated familiarity for its next selection");
        var prior=p.GetSnapshot();p.AddNotebook("review",Now);p.ReplaceSnapshot(prior);
        var another=G(lex,p);another.Start(IdiomActor.Player);another.Submit("甲一一乙");
        A(another.LastEntry.Id=="review" && !p.IsNotebook("review"),"rollback updates existing profile instance rather than swapping references");
        var bad=new IdiomLearningSnapshot(new[]{"missing"},prior.NotebookIds,prior.RecentAppIds,prior.AppExposures,prior.Uses,prior.NotebookAdded);
        try { p.ReplaceSnapshot(bad); throw new Exception("invalid replacement accepted"); } catch(ArgumentException) { }
        A(p.IsFamiliar("review") && !p.IsNotebook("review"),"invalid replacement is atomic");
    }
    public static void NoHintDoesNotEnd()
    {
        var lex=ReviewLex();var p=P(lex);var game=G(lex,p,kind:IdiomMatchKind.Local);
        game.Start(IdiomActor.Left);game.Submit("甲一一乙");
        var hint = game.RequestHint();
        A(hint != null && hint.EntryIds.Contains("review") && !game.IsEnded && game.HintCount==1,"hint falls back to the remaining legal word when preferred candidates are absent");
        A(game.Submit("乙一一丙").Success,"human may still independently use rare legal word");
    }
    public static void PersonalWeights()
    {
        var lex=Loop();int familiar=0,common=0,older=0,newer=0;
        for(uint seed=1;seed<=1600;seed++)
        {
            var p=P(lex);
            p.ReplaceSnapshot(new IdiomLearningSnapshot(new[]{"a"},new string[0],new string[0],new IdiomAppExposure[0],
                new[]{new IdiomUseCount("a",6,0)},new IdiomNotebookAdded[0]));
            var game=G(lex,p,seed);game.Start(IdiomActor.App);
            if(game.LastEntry.Id=="a") familiar++;if(game.LastEntry.Id=="b") common++;
            p=P(lex);p.AddNotebook("e",Now-7*86400);p.AddNotebook("f",Now);
            game=G(lex,p,seed);game.Start(IdiomActor.App);
            if(game.LastEntry.Id=="e")older++;if(game.LastEntry.Id=="f")newer++;
        }
        A(familiar>common*1.2 && familiar<common*1.8,"six independent uses mildly favor a familiar word over equal common word");
        A(older>newer && newer>0,"older unshown notebook word has a mild age preference without starving fresh entries");
    }
}
