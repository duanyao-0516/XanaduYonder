using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Feihualing;
using XanaduYonder.Runtime.Feihualing;

internal static class Program
{
    private const string Fixture = "{\"version\":1,\"corpus\":\"fixture\",\"works\":[[\"test\",\"测试作者\",\"测试正文\",\"月照山前路。明月照山泉。白山月下飞。高山望月归。山林明鸟月。\"]],\"lines\":[[0,0,5,0,1,[]],[0,6,5,1,1,[]],[0,12,5,2,1,[]],[0,18,5,3,1,[]],[0,24,5,4,1,[]]],\"cards\":[" +
        "[\"char-moon\",0,\"月\",\"月\",\"\",0,[[0,1,2,3,4]],[]]," +
        "[\"position-moon\",1,\"五言月\",\"月\",\"\",5,[[0],[1],[2],[3],[4]],[]]," +
        "[\"pair-moon-hill\",2,\"月和山\",\"月\",\"山\",0,[[0,1,2,3,4]],[]]," +
        "[\"theme-fixture\",3,\"主题测试\",\"\",\"\",0,[[0,1,2,3,4]],[[0,\"证据一\"],[1,\"证据二\"],[2,\"证据三\"],[3,\"证据四\"],[4,\"证据五\"]]]]}";
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static byte[] Zip(string json)
    {
        using (var destination = new MemoryStream())
        {
            using (var gzip = new GZipStream(destination, CompressionMode.Compress, true))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                gzip.Write(bytes, 0, bytes.Length);
            }
            return destination.ToArray();
        }
    }
    private static void Reject(string json)
    {
        try { FeihualingCatalogReader.Read(Zip(json)); }
        catch (Exception failure) when (failure is FormatException || failure is ArgumentException || failure is InvalidDataException) { return; }
        throw new Exception("Reader accepted invalid generated data.");
    }
    private static void CheckCodecAndFile()
    {
        var catalog = FeihualingCatalogReader.Read(Zip(Fixture));
        Require(catalog.Works.Length == 1 && catalog.Text(3) == "高山望月归", "UTF-16 offset failed.");
        Reject(Fixture.Replace("\"version\":1", "\"version\":1,\"version\":1"));
        Reject(Fixture.Replace("[0,24,5,4,1,[]]", "[0,99,5,4,1,[]]"));
        Reject(Fixture.Replace("[0,1,2,3,4]", "[0,1,2,3,99]"));
        Reject(Fixture.Replace("\"test\"", "null"));
        Reject(Fixture.Replace("[0,24,5,4,1,[]]", "[0,24,5,4,true,[]]"));
        Reject(Fixture.Replace("[0,1,2,3,4]", "[0,1,2,3,4.0]"));
        var noTheme = JObject.Parse(Fixture);
        ((JArray)noTheme["cards"]).RemoveAt(3);
        Reject(noTheme.ToString());
        var retired = JObject.Parse(Fixture);
        retired["retired"] = new JArray(0);
        var corrected = FeihualingCatalogReader.Read(Zip(retired.ToString()));
        var fresh = PoetrySession.Start(corrected, corrected.Cards[0], PoetryPlayers.Solo, PoetryGoal.Learning, 0);
        Require(fresh.Submit("月照山前路", 1).Status == PoetryAnswerStatus.Unknown, "A retired source fragment was still playable.");
        retired["retired"] = new JArray(99);
        Reject(retired.ToString());
        retired["retired"] = new JArray(0, 0);
        Reject(retired.ToString());
        string path = Path.Combine(Path.GetTempPath(), "feihualing-save-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(path);
            var store = new FeihualingSaveStore(path, catalog);
            Require(Path.GetFileName(store.SavePath) == "feihualing-v4.json", "A catalog revision needs an independent active save.");
            Require(store.Load(out _) == null && store.CanWrite, "Missing save must be writable.");
            var profile = new FeihualingProfile();
            Require(FeihualingProfile.SlotCount == 24 && !profile.AutoCompanion, "New profiles need 24 slots and disabled auto companion.");
            var session = PoetrySession.Start(catalog, catalog.Cards[0], PoetryPlayers.Duo, PoetryGoal.Speed, 0);
            Require(session.Ready(10), "Could not start turn.");
            Require(session.Submit("月照山前路，", 1010).Status == PoetryAnswerStatus.Accepted, "Poetry fixture could not be played.");
            int speedSlot = FeihualingProfile.SlotFor(PoetryPlayers.Duo, PoetryGoal.Speed, PoetryRule.Character);
            profile.SelectedSlot = speedSlot;
            profile.SelectedCards[speedSlot] = "char-moon";
            profile.Matches[speedSlot] = session.Capture(10000);
            profile.Drafts[speedSlot] = "待提交草稿";
            profile.AutoCompanion = true;
            var learning = PoetrySession.Start(catalog, catalog.Cards[0], PoetryPlayers.Solo, PoetryGoal.Learning, 0);
            const long longLearningElapsed = 2147483650L;
            Require(learning.Submit("月照山前路", longLearningElapsed).Status == PoetryAnswerStatus.Accepted, "Unlimited learning timer failed.");
            profile.Matches[0] = learning.Capture(longLearningElapsed);
            int competitionSlot = FeihualingProfile.SlotFor(PoetryPlayers.Solo, PoetryGoal.Competition, PoetryRule.Character);
            var competition = PoetrySession.Start(catalog, catalog.Cards[0], PoetryPlayers.Solo, PoetryGoal.Competition, 0);
            Require(competition.Hints().Count == 1 && competition.CompetitionHintsRemaining == 2, "Fixture did not spend one competitive hint.");
            profile.Matches[competitionSlot] = competition.Capture(0);
            profile.RecordBest("char-moon", 20000);
            profile.RecordBest("char-moon", 30000);
            Require(store.TrySave(profile, out var error), error ?? "Writing the independent save failed.");
            var second = new FeihualingSaveStore(path, catalog);
            var read = second.Load(out error);
            Require(read != null && read.AutoCompanion && read.Drafts[speedSlot] == "待提交草稿" && read.BestMilliseconds("char-moon") == 20000, error ?? "Save failed to round trip.");
            Require(read.Matches[speedSlot].Phase == PoetryPhase.Handoff && read.Matches[speedSlot].History.Length == 1 && read.Matches[5] == null, "Return contaminated another slot or advanced a handoff.");
            Require(read.Matches[0].History[0].ElapsedMilliseconds == longLearningElapsed, "Unlimited learning elapsed did not survive the file round trip.");
            Require(PoetrySession.TryRestore(catalog, read.Matches[competitionSlot], 0, out var resumed) &&
                resumed.Hints().Count == 1 && resumed.CompetitionHintsRemaining == 2,
                "Reloading a competitive clue changed the quota or lost the clue.");
            Require(second.TrySave(profile, out error), error ?? "Second save failed.");
            File.WriteAllText(store.SavePath, "{\"bad\":true}");
            var recovered = new FeihualingSaveStore(path, catalog);
            Require(recovered.Load(out _) != null, "Valid backup was not recovered.");
            Require(recovered.TrySave(profile, out _), "Could not save after recovering the backup.");
            var archiveRoot = Path.Combine(path, "feihualing-recovery");
            var preserved = Directory.GetFiles(archiveRoot, "feihualing-v4.json", SearchOption.AllDirectories);
            Require(preserved.Length == 1 && File.ReadAllText(preserved[0]) == "{\"bad\":true}", "Recovering the backup discarded corrupt original bytes.");
            var validBackup = JObject.Parse(File.ReadAllText(store.SavePath + ".bak"));
            var future = (JObject)validBackup.DeepClone();
            future["schemaVersion"] = 3;
            future["futureSettings"] = true;
            File.WriteAllText(store.SavePath, future.ToString());
            var futureStore = new FeihualingSaveStore(path, catalog);
            Require(futureStore.Load(out _) == null && futureStore.NeedsRecovery && !futureStore.CanWrite,
                "A future schema must be protected even when its shape changed and a v2 backup exists.");
            validBackup["corpus"] = "old-version";
            File.WriteAllText(store.SavePath, validBackup.ToString());
            var old = new FeihualingSaveStore(path, catalog);
            Require(old.Load(out _) == null && old.NeedsRecovery && !old.CanWrite, "Incompatible save was not protected.");
            Require(!old.TrySave(profile, out _), "Overwrote incompatible save.");
            Require(old.TryResetProtected(out _) && Directory.Exists(Path.Combine(path, "feihualing-recovery")), "Source save was not safely archived.");
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    private static void CheckLegacy()
    {
        var catalog = FeihualingCatalogReader.Read(Zip(Fixture));
        string path = Path.Combine(Path.GetTempPath(), "feihualing-legacy-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(path);
            var slots = new JArray();
            for (int index = 0; index < 16; index++)
                slots.Add(new JObject {
                    ["card"] = index == 12 ? "char-moon" : "",
                    ["draft"] = index == 12 ? "旧版草稿" : "",
                    ["match"] = JValue.CreateNull()
                });
            var legacy = new JObject {
                ["schemaVersion"] = 1, ["gameId"] = "feihualing", ["corpus"] = "previous-catalog",
                ["selected"] = 12, ["slots"] = slots, ["best"] = new JArray { new JArray("char-moon", 15000) }
            }.ToString();
            string legacyPath = Path.Combine(path, "feihualing-v1.json");
            File.WriteAllText(legacyPath, legacy);
            var store = new FeihualingSaveStore(path, catalog);
            var profile = store.LoadProfile(out _);
            int speed = FeihualingProfile.SlotFor(PoetryPlayers.Duo, PoetryGoal.Speed, PoetryRule.Character);
            Require(profile != null && profile.SelectedSlot == speed && profile.Drafts[speed] == "旧版草稿" &&
                profile.SelectedCards[speed] == "char-moon" && !profile.AutoCompanion, "Legacy settings did not migrate into Speed.");
            Require(profile.BestMilliseconds("char-moon") == 0, "A result measured against a prior corpus contaminated the new best.");
            Require(File.ReadAllText(legacyPath) == legacy, "Legacy save was modified.");
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    private static void CheckPreviousCatalog(string previousPath, PoetryCatalog catalog, string filename, bool previouslyHadHints)
    {
        var previous = FeihualingCatalogReader.Read(File.ReadAllBytes(previousPath));
        var original = new FeihualingProfile { AutoCompanion = true };
        var oldMatch = PoetrySession.Start(previous, previous.FindCard("char-moon"), PoetryPlayers.Solo, PoetryGoal.Competition, 0);
        Require(oldMatch.Submit("床前明月光", 10).Status == PoetryAnswerStatus.Accepted &&
            oldMatch.Computer(20).Status == PoetryAnswerStatus.Accepted, "Previous fixture could not make a whole turn.");
        if (previouslyHadHints)
            Require(oldMatch.Hints().Count == 1, "Previous fixture could not show a competitive clue.");
        int slot = FeihualingProfile.SlotFor(PoetryPlayers.Solo, PoetryGoal.Competition, PoetryRule.Character);
        original.SelectedSlot = slot;
        original.SelectedCards[slot] = "char-moon";
        original.Drafts[slot] = "旧对局草稿";
        original.Matches[slot] = oldMatch.Capture(25);
        original.RecordBest("char-moon", 9000);
        if (previouslyHadHints)
        {
            var earlier = PoetrySession.Start(previous, previous.FindCard("char-moon"), PoetryPlayers.Solo, PoetryGoal.Learning, 0);
            Require(earlier.Submit("床前明月光", 10).Status == PoetryAnswerStatus.Accepted, "Could not prepare an inherited match.");
            original.Matches[0] = earlier.Capture(20);
            original.Matches[0].AnswerLineLimit = 253241;
        }
        var preChange = JObject.Parse(FeihualingSaveCodec.Encode(original, previous));
        if (!previouslyHadHints)
        {
            var match = (JObject)preChange["slots"][slot]["match"];
            match.Remove("competitionHints");
            match.Remove("answerLineLimit");
        }
        var preservedJson = preChange.ToString();

        string directory = Path.Combine(Path.GetTempPath(), "feihualing-v2-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var oldPath = Path.Combine(directory, filename);
            File.WriteAllText(oldPath, preservedJson);
            var store = new FeihualingSaveStore(directory, catalog);
            var profile = store.LoadProfile(out var warning);
            Require(profile != null && profile.SelectedSlot == slot && profile.AutoCompanion &&
                profile.Drafts[slot] == "旧对局草稿" && profile.BestMilliseconds("char-moon") == 0,
                warning ?? "Previous corpus preferences or match did not migrate.");
            Require(profile.Matches[slot].History.Length == 2 &&
                profile.Matches[slot].AnswerLineLimit == previous.Lines.Length,
                "Previous match was lost or expanded to gameplay it was not started with.");
            if (previouslyHadHints)
                Require(profile.Matches[0].AnswerLineLimit == 253241, "An already migrated match lost its original answer range.");
            Require(PoetrySession.TryRestore(catalog, profile.Matches[slot], 0, out var restored) &&
                restored.CompetitionHintsRemaining == (previouslyHadHints ? 2 : 3) && restored.Hints().Count == 1 &&
                restored.CompetitionHintsRemaining == 2,
                "Previous match did not resume with the new hint feature.");
            profile.Matches[slot] = restored.Capture(10);
            Require(store.TrySave(profile, out _) && File.ReadAllText(oldPath) == preservedJson,
                "Second-generation save was changed during migration.");
            var read = new FeihualingSaveStore(directory, catalog).Load(out _);
            Require(read?.Matches[slot].CompetitionHintLines.Length == 1, "Migrated hint state was lost.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void Main(string[] args)
    {
        NotebookChecks.Run();
        CheckCodecAndFile();
        CheckLegacy();
        if (args.Length > 0)
        {
            var catalog = FeihualingCatalogReader.Read(File.ReadAllBytes(args[0]));
            Require(catalog.Works.Length >= 25000 && catalog.Works.Length <= 30000, "Published corpus count outside revised scope.");
            Require(catalog.FindCard("char-moon") != null, "The approved default card is missing.");
            foreach (string cardId in new[] { "char-mountain", "char-flower" })
            {
                var example = PoetrySession.Start(catalog, catalog.FindCard(cardId), PoetryPlayers.Solo, PoetryGoal.Learning, 0);
                var answer = example.Submit("山寺桃花始盛开，", 1);
                Require(answer.Status == PoetryAnswerStatus.Accepted &&
                    catalog.Works[catalog.Lines[answer.LineId].WorkIndex].Title == "大林寺桃花",
                    cardId + " failed the formerly missing Bai Juyi answer and citation.");
            }
            foreach (var title in new[] { "月上柳梢头", "春花秋月何时了" })
            {
                var session = PoetrySession.Start(catalog, catalog.FindCard("char-moon"), PoetryPlayers.Solo, PoetryGoal.Learning, 0);
                Require(session.Submit(title + "，", 1).Status == PoetryAnswerStatus.Accepted, "Reviewed missing line did not pass 月: " + title);
            }
            var cleaned = PoetrySession.Start(catalog, catalog.FindCard("char-moon"), PoetryPlayers.Solo, PoetryGoal.Learning, 0);
            Require(cleaned.Submit(" —『月上柳梢头』～✦🌙！！！、 ", 1).Status == PoetryAnswerStatus.Accepted &&
                cleaned.History[0].Input == "月上柳梢头", "Decoration was not removed from a sent poem.");
            Require(PoetrySession.TryRestore(catalog, cleaned.Capture(2), 3, out var restored) &&
                restored.History[0].Input == "月上柳梢头", "The cleaned answer did not survive restore.");
            Require(cleaned.Submit("『月上柳梢头』：「人约黄昏后」", 4).Status == PoetryAnswerStatus.MultipleLines,
                "Filtering punctuation silently grouped two answers.");
            Require(cleaned.Submit("月上柳梢头x！", 5).Status == PoetryAnswerStatus.Unknown,
                "The punctuation filter accepted a different answer with ordinary letters.");
            var marked = PoetrySession.Start(catalog, catalog.FindCard("char-mountain"), PoetryPlayers.Solo, PoetryGoal.Learning, 0);
            Require(marked.Submit("『驾长车、踏破贺兰山缺』！！！", 1).Status == PoetryAnswerStatus.Accepted &&
                marked.History[0].Input == "驾长车、踏破贺兰山缺", "Cleaning modified an internal editorial caesura.");
            var markedComma = PoetrySession.Start(catalog, catalog.FindCard("char-sun"), PoetryPlayers.Solo, PoetryGoal.Learning, 0);
            Require(markedComma.Submit("“弃我去者，昨日之日不可留”！", 1).Status == PoetryAnswerStatus.Accepted &&
                markedComma.History[0].Input == "弃我去者，昨日之日不可留", "Cleaning modified an internal editorial comma.");
            var variant = PoetrySession.Start(catalog, catalog.FindCard("char-moon"), PoetryPlayers.Solo, PoetryGoal.Learning, 0);
            Require(variant.Submit("月到柳梢头", 1).Status == PoetryAnswerStatus.Accepted &&
                variant.Submit("月上柳梢头", 2).Status == PoetryAnswerStatus.Duplicate,
                "Published variants both need to be accepted as one response group.");
            foreach (var item in new[]
            {
                ("char-rain", "清明时节雨纷纷"),
                ("char-flower", "牧童遥指杏花村"),
                ("char-rain", "寒雨连江夜入吴"),
                ("char-cloud", "千里黄云白日曛"),
                ("char-sun", "弃我去者昨日之日不可留"),
                ("char-mountain", "驾长车、踏破贺兰山缺")
            })
            {
                var session = PoetrySession.Start(catalog, catalog.FindCard(item.Item1), PoetryPlayers.Solo, PoetryGoal.Learning, 0);
                Require(session.Submit(item.Item2, 1).Status == PoetryAnswerStatus.Accepted, "Audited classic did not pass: " + item.Item2);
            }
            var correctedCi = PoetrySession.Start(catalog, catalog.FindCard("char-mountain"), PoetryPlayers.Solo, PoetryGoal.Learning, 0);
            Require(correctedCi.Submit("贺兰山缺", 1).Status != PoetryAnswerStatus.Accepted,
                "New games should use the reviewed boundary for 满江红.");
            if (args.Length > 1) CheckPreviousCatalog(args[1], catalog, "feihualing-v3.json", true);
            if (args.Length > 2) CheckPreviousCatalog(args[2], catalog, "feihualing-v2.json", false);
            foreach (PoetryRule rule in Enum.GetValues(typeof(PoetryRule)))
            {
                int count = 0;
                foreach (PoetryCard card in catalog.Cards)
                    if (card.Rule == rule)
                    {
                        count++;
                        for (int players = 0; players < 2; players++)
                        for (int goal = 0; goal < 3; goal++)
                        {
                            var session = PoetrySession.Start(catalog, card, (PoetryPlayers)players, (PoetryGoal)goal, 0);
                            if (session.Phase == PoetryPhase.Handoff) Require(session.Ready(0), "A card did not permit ready.");
                            string sample = null;
                            foreach (int id in card.Candidates[0])
                            {
                                sample = session.Suggestion(id);
                                if (sample != null) break;
                            }
                            Require(sample != null && session.Submit(sample, 1).Status == PoetryAnswerStatus.Accepted,
                                card.Id + " rejected a prebuilt valid line.");
                        }
                    }
                Require(count >= 11, rule + " still lacks playable card variety.");
            }
            Console.WriteLine("Offline release catalog loaded: {0} works, {1} lines, {2} cards.", catalog.Works.Length, catalog.Lines.Length, catalog.Cards.Length);
        }
        Console.WriteLine("Feihualing catalog/save checks passed.");
    }
}
