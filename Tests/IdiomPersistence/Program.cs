using System;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Idiom;
using XanaduYonder.Runtime.Idiom;

internal static class Program
{
    private static int passed, failed;
    private static int Main()
    {
        Check("Notebook preserves exact sense, snapshots, timestamp and dictionary version", NotebookRoundTrip);
        Check("Notebook keeps complete long definitions", LongMeaning);
        Check("Notebook cannot write before loading", CannotWriteBeforeLoad);
        Check("Notebook duplicates and malformed snapshots cannot replace good data", NotebookInvalidModels);
        Check("Strict JSON rejects duplicate, unknown, coerced and missing fields", StrictNotebookJson);
        Check("Escaped unpaired Unicode surrogates are rejected without replacement", EscapedSurrogates);
        Check("Corrupt notebook blocks pause writes until originals are archived", ProtectedNotebook);
        Check("Failed archive preserves corrupt primary and backup", ArchiveFailure);
        Check("Valid backup recovery never replaces good backup with corrupt primary", BackupRecovery);
        Check("Temporary write failure keeps previous primary and reports failure", WriteFailure);
        Check("Invalid UTF8 and oversized files are protected", InvalidFiles);
        Check("Unknown notebook schema cannot fall back to an older backup", IncompatibleNotebook);
        Check("Other game files remain byte identical", OtherGamesUntouched);
        Check("Both modes, drafts, best counts and hint events round trip", GameRoundTrip);
        Check("Learning state uses one new aggregate document and leaves legacy files untouched", LearningAggregateContract);
        Check("Legacy identities are rejected by the learning aggregate", LegacyGameRejected);
        Check("Learning profile notebook assisted input and random streams round trip together", LearningRoundTrip);
        Check("Idiom display and keyboard preferences round trip without changing game or learning data", PreferenceRoundTrip);
        Check("Missing idiom preferences use independent defaults without discarding old records", MissingPreferences);
        Check("Malformed idiom preferences protect originals instead of coercing values", InvalidPreferences);
        Check("Failed aggregate writes preserve all domains and retries commit once", AggregateAtomicFailure);
        Check("Malformed learning state and notebook profile mismatches are rejected", InvalidLearningState);
        Check("Legacy recommendation terminals retain frozen context across profile changes", SelectionTerminalRoundTrip);
        Check("Six local hints round trip after both humans continue", SixHintsRoundTrip);
        Check("Surrendered games round trip with stable reason seven", SurrenderRoundTrip);
        Check("Four match and rule slots preserve separate progress and hint quotas", FourSlotRoundTrip);
        Check("Aggregate rejects mismatched match kinds, scores and forged local hints", InvalidLocalStates);
        Check("Unknown game schema protects primary instead of loading compatible backup", UnknownGameSchema);
        Check("Game JSON rejects unknown fields, duplicate fields and token coercion", StrictGameJson);
        Check("Core restore validates history, mode, hints and terminal state", InvalidGameStates);
        Check("Dictionary mismatch protects primary even when backup is compatible", DictionaryMismatch);
        Check("Unavailable catalog cannot load, save or reset original data", CatalogUnavailable);
        Check("Game corruption recovery preserves good backup and requires archive for bad files", GameRecovery);
        Check("Game file respects its bounded byte limit", GameFileLimit);
        Check("Long complete histories in both modes are bounded by the lexicon", LongHistories);
        Check("Four complete current-catalog-sized histories fit the save budget", FourLongHistories);
        Check("Ten thousand bookmarks retain complete records", LargeNotebook);
        Check("Notebook byte limit returns a clear error and retains old bytes", NotebookByteLimit);
        Check("Unavailable directory is not treated as a fresh empty notebook", DirectoryUnavailable);
        Console.WriteLine("Idiom persistence checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }
    private static Type Type(string name)
    {
        var result = typeof(Program).Assembly.GetType("XanaduYonder.Runtime.Idiom." + name);
        Assert(result != null, "Missing production type: " + name);
        return result;
    }
    private static dynamic New(string name, params object[] args) { return Activator.CreateInstance(Type(name), args); }
    private static dynamic Notebook()
    {
        dynamic data = New("IdiomNotebookData");
        dynamic item = New("IdiomNotebookItem");
        item.Id = "one"; item.SenseId = "one-sense"; item.Word = "一心一意";
        item.Pinyin = "yī xīn yī yì"; item.Meaning = "心思专一。";
        item.AddedAt = 1793000000L; item.DictionaryVersion = "dictionary-v1";
        data.Entries.Add(item);
        return data;
    }
    private static dynamic Load(dynamic store) { string warning; return store.Load(out warning); }
    private static void Save(dynamic store, dynamic data)
    {
        string error; Assert(store.TrySave(data, out error), error);
    }
    private static void RejectSave(dynamic store, dynamic data)
    {
        string error; Assert(!store.TrySave(data, out error), "Invalid save accepted");
        Assert(!string.IsNullOrWhiteSpace(error), "Failure needs user-facing error");
    }
    private static void NotebookRoundTrip()
    {
        WithDirectory(dir => {
            dynamic store = New("IdiomNotebookStore", dir); Assert(Load(store) == null, "Missing notebook should be null");
            Assert(store.CanWrite && !store.NeedsRecovery, "Fresh load should unlock valid writes"); Save(store, Notebook());
            dynamic loaded = Load(New("IdiomNotebookStore", dir)); dynamic item = loaded.Entries[0];
            Assert(item.Id == "one" && item.SenseId == "one-sense" && item.Word == "一心一意", "Identity changed");
            Assert(item.Pinyin == "yī xīn yī yì" && item.Meaning == "心思专一。", "Snapshot changed");
            Assert(item.AddedAt == 1793000000L && item.DictionaryVersion == "dictionary-v1", "Metadata changed");
            item.Meaning = "changed";
            Assert(Load(New("IdiomNotebookStore", dir)).Entries[0].Meaning == "心思专一。", "Load aliases prior object");
        });
    }
    private static void CannotWriteBeforeLoad() { WithDirectory(dir => RejectSave(New("IdiomNotebookStore", dir), Notebook())); }
    private static void NotebookInvalidModels()
    {
        WithDirectory(dir => {
            dynamic store = New("IdiomNotebookStore", dir); Load(store); Save(store, Notebook()); string path = store.SavePath;
            var before = File.ReadAllBytes(path);
            dynamic data = Notebook(); data.Entries.Add(data.Entries[0]); RejectSave(store, data);
            data = Notebook(); data.Entries[0].AddedAt = -1L; RejectSave(store, data);
            data = Notebook(); data.Entries[0].SenseId = ""; RejectSave(store, data);
            data = Notebook(); data.Entries[0].DictionaryVersion = ""; RejectSave(store, data);
            data = Notebook(); data.Entries[0].Meaning = new string('界', 65537); RejectSave(store, data);
            data = Notebook(); data.Entries[0].Word = "\ud800"; RejectSave(store, data);
            Equal(before, File.ReadAllBytes(path));
        });
    }
    private static void LongMeaning()
    {
        WithDirectory(dir => {
            dynamic store=New("IdiomNotebookStore",dir); Load(store); dynamic data=Notebook();
            data.Entries[0].Meaning=new string('界',65536); Save(store,data);
            Assert(Load(New("IdiomNotebookStore",dir)).Entries[0].Meaning.Length==65536,"Complete definition was truncated");
        });
    }
    private static void StrictNotebookJson()
    {
        WithDirectory(dir => {
            dynamic store = New("IdiomNotebookStore", dir); Load(store); Save(store, Notebook()); string path = store.SavePath;
            string valid = File.ReadAllText(path); var root = JObject.Parse(valid);
            foreach (string bad in new[] {valid + "{}", valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1"),
                "null", "[]", "{}", "/* comment */" + valid, valid.Replace("\"schemaVersion\"", "'schemaVersion'"),
                valid.Substring(0,valid.Length-1)+",}", "{\"x\":"+new string('[',20)+"0"+new string(']',20)+"}"})
                RejectDocument(dir, bad);
            foreach (string field in new[] {"schemaVersion", "gameId", "entries"}) {
                var changed = (JObject)root.DeepClone(); changed.Remove(field); RejectDocument(dir, changed.ToString());
                changed = (JObject)root.DeepClone(); changed[field] = JValue.CreateNull(); RejectDocument(dir, changed.ToString());
            }
            foreach (string field in new[] {"id","senseId","word","pinyin","meaning","addedAt","dictionaryVersion"}) {
                var changed = (JObject)root.DeepClone(); ((JObject)changed["entries"][0]).Remove(field); RejectDocument(dir, changed.ToString());
                changed = (JObject)root.DeepClone(); changed["entries"][0][field] = JValue.CreateNull(); RejectDocument(dir, changed.ToString());
            }
            foreach (var token in new JToken[] {"1793000000", 1.0, true, new JObject(), -1}) {
                var changed = (JObject)root.DeepClone(); changed["entries"][0]["addedAt"] = token; RejectDocument(dir, changed.ToString());
            }
            var unknown = (JObject)root.DeepClone(); unknown["unexpected"] = 1; RejectDocument(dir, unknown.ToString());
            unknown = (JObject)root.DeepClone(); unknown["entries"][0]["unexpected"] = 1; RejectDocument(dir, unknown.ToString());
            RejectDocument(dir, valid.Replace("\"id\":\"one\"", "\"id\":\"one\",\"id\":\"one\""));
        });
    }
    private static void RejectDocument(string dir, string json)
    {
        var path = Path.Combine(dir, "idiom-notebook-v1.json"); File.WriteAllText(path, json);
        if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        dynamic store = New("IdiomNotebookStore", dir); string warning;
        Assert(store.Load(out warning) == null && store.NeedsRecovery && !store.CanWrite, "Bad document accepted: " + json.Substring(0, Math.Min(json.Length, 80)));
        Assert(!string.IsNullOrEmpty(warning), "Missing corruption warning");
    }
    private static void EscapedSurrogates()
    {
        WithDirectory(dir => {
            dynamic store=New("IdiomNotebookStore",dir); Load(store); Save(store,Notebook());
            string valid=File.ReadAllText((string)store.SavePath);
            foreach(string escaped in new[]{"\\ud800","\\udc00","\\ud800a","\\ud800\\u0041"})
                RejectDocument(dir,valid.Replace("心思专一。",escaped));
            File.WriteAllText((string)store.SavePath,valid.Replace("心思专一。","\\ud842\\udfb7"));
            Assert(Load(New("IdiomNotebookStore",dir)).Entries[0].Meaning=="𠮷","Valid escaped surrogate pair rejected");
        });
    }
    private static void ProtectedNotebook()
    {
        WithDirectory(dir => {
            var path = Path.Combine(dir, "idiom-notebook-v1.json"); File.WriteAllText(path, "bad primary"); File.WriteAllText(path + ".bak", "bad backup");
            dynamic store = New("IdiomNotebookStore", dir); Assert(Load(store) == null, "Corruption should return null");
            Assert(store.NeedsRecovery && !store.CanWrite, "Corruption must protect"); RejectSave(store, Notebook());
            Assert(File.ReadAllText(path) == "bad primary", "Protected primary changed"); string error;
            Assert(store.TryResetProtected(out error), error); Assert(store.CanWrite && !store.NeedsRecovery, "Archive did not unlock");
            Assert(!File.Exists(path) && !File.Exists(path + ".bak"), "Old files still active after archive");
            var archived = Directory.GetFiles(Path.Combine(dir, "idiom-recovery"), "*", SearchOption.AllDirectories);
            Assert(Array.Exists(archived, p => File.ReadAllText(p) == "bad primary") && Array.Exists(archived, p => File.ReadAllText(p) == "bad backup"), "Archive missing originals");
            Save(store, Notebook());
        });
    }
    private static void ArchiveFailure()
    {
        WithDirectory(dir => {
            var path = Path.Combine(dir,"idiom-notebook-v1.json"); File.WriteAllText(path,"broken"); File.WriteAllText(path+".bak","also broken");
            dynamic store=New("IdiomNotebookStore",dir); Load(store); File.WriteAllText(Path.Combine(dir,"idiom-recovery"),"blocked");
            string error; Assert(!store.TryResetProtected(out error), "Archive should fail"); Assert(!store.CanWrite && store.NeedsRecovery,"Archive failure unlocked writes");
            Assert(File.ReadAllText(path)=="broken" && File.ReadAllText(path+".bak")=="also broken","Archive failure lost originals");
        });
    }
    private static void BackupRecovery()
    {
        WithDirectory(dir => {
            dynamic store=New("IdiomNotebookStore",dir); Load(store); Save(store,Notebook()); Save(store,Notebook()); string path=store.SavePath;
            var backup=File.ReadAllBytes(path+".bak"); File.WriteAllText(path,"broken"); store=New("IdiomNotebookStore",dir);
            string warning; Assert(store.Load(out warning)!=null && store.CanWrite && !string.IsNullOrEmpty(warning),"Backup recovery failed");
            dynamic data=Notebook(); data.Entries[0].Meaning="新版快照"; Save(store,data); Equal(backup,File.ReadAllBytes(path+".bak"));
            Assert(Load(New("IdiomNotebookStore",dir)).Entries[0].Meaning=="新版快照","Recovered save missing");
        });
    }
    private static void WriteFailure()
    {
        WithDirectory(dir => {
            dynamic store=New("IdiomNotebookStore",dir); Load(store); Save(store,Notebook()); string path=store.SavePath; var before=File.ReadAllBytes(path);
            Directory.CreateDirectory(path+".tmp"); dynamic data=Notebook(); data.Entries.Clear(); RejectSave(store,data);
            Equal(before,File.ReadAllBytes(path)); Assert(Load(New("IdiomNotebookStore",dir)).Entries.Count==1,"Failed save appeared successful");
        });
    }
    private static void InvalidFiles()
    {
        WithDirectory(dir => {
            var path=Path.Combine(dir,"idiom-notebook-v1.json"); File.WriteAllBytes(path,new byte[]{0xff,0xff});
            dynamic store=New("IdiomNotebookStore",dir); Assert(Load(store)==null && !store.CanWrite,"Invalid UTF8 accepted");
            using(var stream=File.Create(path)) stream.SetLength(40L*1024*1024);
            store=New("IdiomNotebookStore",dir); Assert(Load(store)==null && !store.CanWrite,"Oversized file accepted");
        });
    }
    private static void IncompatibleNotebook()
    {
        WithDirectory(dir => {
            dynamic store=New("IdiomNotebookStore",dir); Load(store); Save(store,Notebook()); Save(store,Notebook()); string path=store.SavePath;
            File.WriteAllText(path,File.ReadAllText(path).Replace("\"schemaVersion\":1","\"schemaVersion\":2"));
            store=New("IdiomNotebookStore",dir); Assert(Load(store)==null && !store.CanWrite && store.NeedsRecovery,"Incompatible primary fell back to older backup");
        });
    }
    private static void OtherGamesUntouched()
    {
        WithDirectory(dir => {
            var names=new[]{"save-v1.json","save-v1.json.bak","jump-v1.json","jump-v1.json.bak","sheep-v1.json","sheep-v1.json.bak"};
            var bytes=new byte[]{0,255,1,19,21}; foreach(var name in names) File.WriteAllBytes(Path.Combine(dir,name),bytes);
            dynamic store=New("IdiomNotebookStore",dir); Load(store); Save(store,Notebook());
            dynamic gameStore=New("IdiomSaveStore",dir,Lexicon()); Load(gameStore); Save(gameStore,GameData(Lexicon()));
            foreach(var name in names) Equal(bytes,File.ReadAllBytes(Path.Combine(dir,name)));
        });
    }
    private static Type CoreType(string name)
    {
        var type=typeof(Program).Assembly.GetType("XanaduYonder.Core.Idiom."+name);
        Assert(type!=null,"Missing production core type: "+name); return type;
    }
    private static object EnumValue(string type,int value) { return Enum.ToObject(CoreType(type),value); }
    private static dynamic Lexicon(string version="dictionary-v1")
    {
        var words=new[]{"一心一意","意气风发","发扬光大","大有可为","为所欲为","为民除害"};
        var first=new[]{"yi","yi","fa","da","wei","wei"}; var last=new[]{"yi","fa","da","wei","wei","hai"};
        var entries=Array.CreateInstance(CoreType("IdiomEntry"),words.Length);
        for(int i=0;i<words.Length;i++) entries.SetValue(Activator.CreateInstance(CoreType("IdiomEntry"),
            new object[]{"entry"+i,words[i],"yī xīn yī yì",first[i],last[i],"sense"+i,"0000",true}),i);
        return Activator.CreateInstance(CoreType("IdiomLexicon"),new object[]{entries,version});
    }
    private static dynamic GameData(dynamic lexicon)
    {
        dynamic profile=FreshProfile(lexicon);
        dynamic character=Activator.CreateInstance(CoreType("IdiomGame"),new object[]{lexicon,EnumValue("IdiomMode",0),(uint)17,EnumValue("IdiomMatchKind",0),profile,(Func<long>)(()=>200000L)});
        dynamic sound=Activator.CreateInstance(CoreType("IdiomGame"),new object[]{lexicon,EnumValue("IdiomMode",1),(uint)29,EnumValue("IdiomMatchKind",0),profile,(Func<long>)(()=>200000L)});
        Assert(character.Start((dynamic)EnumValue("IdiomActor",2)).Success,"Fixture cannot start");
        character.RequestHint(); Assert(character.Submit("一心一意").Success,"Fixture cannot submit");
        dynamic data=New("IdiomSaveData"), c=New("IdiomModeSave"), s=New("IdiomModeSave");
        c.Game=character.GetSnapshot(); c.Draft="发扬"; c.Best=1; c.NewBestInRun=true;
        s.Game=sound.GetSnapshot(); s.Draft="人先开始"; s.Best=2; s.NewBestInRun=false;
        data.ActiveMode=(dynamic)EnumValue("IdiomMode",1); data.Character=c; data.Sound=s;
        AddFreshLocalSlots(data,lexicon); data.Profile=profile.GetSnapshot();data.Notebook=New("IdiomNotebookData");return data;
    }
    private static void AddFreshLocalSlots(dynamic data,dynamic lexicon)
    {
        for(int mode=0;mode<2;mode++)
        {
            dynamic game=Activator.CreateInstance(CoreType("IdiomGame"),new object[]{lexicon,EnumValue("IdiomMode",mode),(uint)41,EnumValue("IdiomMatchKind",1)});
            dynamic slot=New("IdiomModeSave"); slot.Game=game.GetSnapshot();
            if(mode==0)data.LocalCharacter=slot;else data.LocalSound=slot;
        }
    }
    private static dynamic FreshProfile(dynamic lexicon)
    {
        return Activator.CreateInstance(CoreType("IdiomLearningProfile"),new object[]{lexicon});
    }
    private static void AddFreshLearning(dynamic data,dynamic lexicon)
    {
        data.Profile=FreshProfile(lexicon).GetSnapshot(); data.Notebook=New("IdiomNotebookData");
    }
    private static Array CoreRows(string type, params object[][] rows)
    {
        var result=Array.CreateInstance(CoreType(type),rows.Length);
        for(int i=0;i<rows.Length;i++)result.SetValue(Activator.CreateInstance(CoreType(type),rows[i]),i);
        return result;
    }
    private static dynamic LearningData(dynamic lexicon)
    {
        dynamic data=GameData(lexicon);
        data.Profile=(dynamic)Activator.CreateInstance(CoreType("IdiomLearningSnapshot"),new object[]{
            new[]{"entry0"},new[]{"entry2"},new[]{"entry1"},
            CoreRows("IdiomAppExposure",new object[]{"entry1",123000L},new object[]{"entry2",123456L}),
            CoreRows("IdiomUseCount",new object[]{"entry0",1,0},new object[]{"entry3",0,1}),
            CoreRows("IdiomNotebookAdded",new object[]{"entry2",321L})});
        dynamic item=New("IdiomNotebookItem"); item.Id="entry2"; item.Word="发扬光大"; item.SenseId="sense2";
        item.Pinyin="yī xīn yī yì";item.Meaning="保存的完整释义";item.DictionaryVersion="dictionary-v1";item.AddedAt=321L;
        data.Notebook.Entries.Add(item);
        dynamic profile=Activator.CreateInstance(CoreType("IdiomLearningProfile"),new object[]{lexicon,data.Profile});
        dynamic game=Activator.CreateInstance(CoreType("IdiomGame"),new object[]{lexicon,data.Character.Game,profile,(Func<long>)(()=>200000L)});
        game.RecordDefinitionViewed("entry3"); data.Character.Game=game.GetSnapshot(); return data;
    }
    private static void LearningRoundTrip()
    {
        WithDirectory(dir => {
            dynamic lexicon=Lexicon(), data=LearningData(lexicon),store=New("IdiomSaveStore",dir,lexicon); Load(store); Save(store,data);
            byte[] first=File.ReadAllBytes((string)store.SavePath); Save(store,data); Equal(first,File.ReadAllBytes((string)store.SavePath));
            dynamic restored=Load(New("IdiomSaveStore",dir,lexicon));
            Assert(restored.Profile.FamiliarIds.Count==1 && restored.Profile.NotebookIds.Count==1 && restored.Profile.RecentAppIds.Count==1,"Profile sets changed or duplicate save re-applied an event");
            bool foundExposure=false;foreach(dynamic row in restored.Profile.AppExposures)if(row.EntryId=="entry2" && row.LastShownUtc==123456L)foundExposure=true;
            Assert(restored.Profile.AppExposures.Count==2 && foundExposure,"APP exposure changed");
            Assert(restored.Profile.Uses.Count==2 && restored.Profile.NotebookAdded[0].AddedAtUtc==321L,"Use evidence or notebook time lost");
            Assert(restored.Notebook.Entries.Count==1 && restored.Notebook.Entries[0].Meaning=="保存的完整释义","Notebook lost from aggregate");
            Assert(restored.Character.Game.AssistedEntryIds.Count==1 && restored.Character.Game.AssistedEntryIds[0]=="entry3","Definition assistance lost");
            Assert(restored.Character.Game.RandomState==data.Character.Game.RandomState && restored.Character.Game.HintRandomState==data.Character.Game.HintRandomState,"Independent random streams changed");
        });
    }
    private static void PreferenceRoundTrip()
    {
        WithDirectory(dir => {
            dynamic lexicon=Lexicon(),data=LearningData(lexicon),store=New("IdiomSaveStore",dir,lexicon);
            Assert(data.CloseKeyboardAfterSend && !data.ShowPinyin,"New saves must close after sending and hide chat pinyin by default");
            Load(store); Save(store,data);
            var original=JObject.Parse(File.ReadAllText((string)store.SavePath));
            original.Remove("closeKeyboardAfterSend"); original.Remove("showPinyin");
            foreach(bool close in new[]{false,true}) foreach(bool pinyin in new[]{false,true})
            {
                data.CloseKeyboardAfterSend=close; data.ShowPinyin=pinyin; Save(store,data);
                dynamic restored=Load(New("IdiomSaveStore",dir,lexicon));
                Assert(restored.CloseKeyboardAfterSend==close && restored.ShowPinyin==pinyin,"Preference values did not survive a fresh disk load");
                var written=JObject.Parse(File.ReadAllText((string)store.SavePath));
                Assert(written["closeKeyboardAfterSend"].Type==JTokenType.Boolean && written["showPinyin"].Type==JTokenType.Boolean,"Preferences must be JSON booleans");
                written.Remove("closeKeyboardAfterSend"); written.Remove("showPinyin");
                Assert(JToken.DeepEquals(original,written),"Changing preferences altered games, drafts, notebook or learning profile");
            }
        });
    }
    private static void MissingPreferences()
    {
        WithDirectory(dir => {
            dynamic lexicon=Lexicon(),data=LearningData(lexicon),store=New("IdiomSaveStore",dir,lexicon);
            Load(store); Save(store,data);
            string path=store.SavePath; var root=JObject.Parse(File.ReadAllText(path));
            foreach(int mask in new[]{0,1,2})
            {
                var original=(JObject)root.DeepClone();
                original.Remove("closeKeyboardAfterSend"); original.Remove("showPinyin");
                if((mask&1)!=0) original["closeKeyboardAfterSend"]=false;
                if((mask&2)!=0) original["showPinyin"]=true;
                File.WriteAllText(path,original.ToString()); if(File.Exists(path+".bak")) File.Delete(path+".bak");
                store=New("IdiomSaveStore",dir,lexicon); dynamic restored=Load(store);
                Assert(restored!=null && store.CanWrite && !store.NeedsRecovery,"Preference-free aggregate was rejected");
                Assert(restored.CloseKeyboardAfterSend==((mask&1)==0) && restored.ShowPinyin==((mask&2)!=0),"One missing preference reset another setting or used the wrong default");
                Save(store,restored);
                var rewritten=JObject.Parse(File.ReadAllText(path));
                foreach(string field in new[]{"closeKeyboardAfterSend","showPinyin"}) { original.Remove(field);rewritten.Remove(field); }
                Assert(JToken.DeepEquals(original,rewritten),"Defaulting missing preferences changed existing records");
            }
        });
    }
    private static void InvalidPreferences()
    {
        WithDirectory(dir => {
            dynamic lexicon=Lexicon(),data=LearningData(lexicon),store=New("IdiomSaveStore",dir,lexicon);
            Load(store); Save(store,data); string path=store.SavePath; var root=JObject.Parse(File.ReadAllText(path));
            foreach(string field in new[]{"closeKeyboardAfterSend","showPinyin"})
            {
                foreach(var bad in new JToken[]{JValue.CreateNull(),"true","false",0,1,1.0,new JArray(),new JObject()})
                {
                    var changed=(JObject)root.DeepClone(); changed[field]=bad;
                    var json=changed.ToString(); RejectGameDocument(dir,lexicon,json);
                    byte[] before=File.ReadAllBytes(path);
                    store=New("IdiomSaveStore",dir,lexicon); Load(store); RejectSave(store,data); Equal(before,File.ReadAllBytes(path));
                }
                var valid=root.ToString(Newtonsoft.Json.Formatting.None);
                RejectGameDocument(dir,lexicon,valid.Replace("\""+field+"\":", "\""+field+"\":false,\""+field+"\":"));
            }
        });
    }
    private static void AggregateAtomicFailure()
    {
        WithDirectory(dir => {
            dynamic lexicon=Lexicon(),data=LearningData(lexicon),store=New("IdiomSaveStore",dir,lexicon); Load(store); Save(store,data);
            string path=store.SavePath; byte[] before=File.ReadAllBytes(path);
            dynamic profile=Activator.CreateInstance(CoreType("IdiomLearningProfile"),new object[]{lexicon,data.Profile});
            profile.Remember("entry2"); data.Profile=profile.GetSnapshot();data.Notebook.Entries.Clear();data.Character.Draft="新的草稿";
            Directory.CreateDirectory(path+".tmp"); RejectSave(store,data); Equal(before,File.ReadAllBytes(path));
            dynamic unchanged=Load(New("IdiomSaveStore",dir,lexicon));
            Assert(unchanged.Notebook.Entries.Count==1 && unchanged.Profile.NotebookIds.Count==1 && unchanged.Profile.FamiliarIds.Count==1 && unchanged.Character.Draft=="发扬","Failed transaction partially committed");
            Directory.Delete(path+".tmp"); Save(store,data); byte[] committed=File.ReadAllBytes(path); Save(store,data); Equal(committed,File.ReadAllBytes(path));
            dynamic restored=Load(New("IdiomSaveStore",dir,lexicon));
            Assert(restored.Notebook.Entries.Count==0 && restored.Profile.NotebookIds.Count==0 && restored.Profile.FamiliarIds.Count==2 && restored.Character.Draft=="新的草稿","Retry failed to commit all domains exactly once");
        });
    }
    private static void InvalidLearningState()
    {
        WithDirectory(dir => {
            dynamic lexicon=Lexicon(),store=New("IdiomSaveStore",dir,lexicon); Load(store); Save(store,LearningData(lexicon));
            var root=JObject.Parse(File.ReadAllText((string)store.SavePath));
            foreach(var change in new Action<JObject>[] {
                r=>r["profile"]["familiarIds"][0]="unknown",
                r=>((JArray)r["profile"]["familiarIds"]).Add("entry0"),
                r=>r["profile"]["notebookIds"]=new JArray(),
                r=>r["profile"]["recentAppIds"]=new JArray("entry1","entry1","entry1","entry1","entry1","entry1","entry1","entry1","entry1","entry1","entry1"),
                r=>r["profile"]["appExposures"][0]["lastShownUtc"]=-1,
                r=>r["profile"]["appExposures"][0]["entryId"]="unknown",
                r=>((JArray)r["profile"]["appExposures"]).Add(r["profile"]["appExposures"][0].DeepClone()),
                r=>r["profile"]["uses"][0]["independentUses"]=0,
                r=>r["profile"]["uses"][0]["assistedUses"]=-1,
                r=>r["profile"]["uses"][0]["independentUses"]=(long)int.MaxValue+1,
                r=>r["profile"]["notebookAdded"][0]["addedAtUtc"]=322,
                r=>r["profile"]["notebookAdded"]=new JArray(),
                r=>r["notebook"]["entries"][0]["senseId"]="wrong-sense",
                r=>r["notebook"]["entries"][0]["word"]="一心一意",
                r=>r["notebook"]["entries"][0]["dictionaryVersion"]="other-version",
                r=>r["character"]["game"]["assistedEntryIds"]=new JArray("unknown"),
                r=>r["character"]["game"]["hintRandomState"]=0,
                r=>r["character"]["game"]["endSelectionContext"]=new JObject { ["ordinaryIds"]=new JArray(),["reviewIds"]=new JArray() } })
            { var changed=(JObject)root.DeepClone();change(changed);RejectGameDocument(dir,lexicon,changed.ToString()); }
            foreach(string section in new[]{"profile.appExposures[0]","profile.uses[0]","profile.notebookAdded[0]"})
            {
                foreach(var property in ((JObject)root.SelectToken(section)).Properties())
                {
                    var changed=(JObject)root.DeepClone();((JObject)changed.SelectToken(section)).Remove(property.Name);RejectGameDocument(dir,lexicon,changed.ToString());
                    changed=(JObject)root.DeepClone();changed.SelectToken(section)[property.Name]=JValue.CreateNull();RejectGameDocument(dir,lexicon,changed.ToString());
                }
            }
        });
    }
    private static void SelectionTerminalRoundTrip()
    {
        WithDirectory(dir => {
            var lexicon=new IdiomLexicon(new[]{
                new IdiomEntry("a","甲一一乙","jiǎ yī yī yǐ","jia","yi","sense-a","00",false),
                new IdiomEntry("b","乙一一丙","yǐ yī yī bǐng","yi","bing","sense-b","00",false),
                new IdiomEntry("c","丙一一丁","bǐng yī yī dīng","bing","ding","sense-c","00",false)},"terminal-v1");
            dynamic profile=FreshProfile(lexicon);
            dynamic game=Activator.CreateInstance(CoreType("IdiomGame"),new object[]{lexicon,EnumValue("IdiomMode",0),(uint)17,EnumValue("IdiomMatchKind",0),profile,(Func<long>)(()=>200000L)});
            game.Start((dynamic)EnumValue("IdiomActor",2));Assert(game.Submit("甲一一乙").Success,"Terminal fixture failed");
            Assert((int)game.EndReason==0 && game.History.Count==2,"New games use remaining legal words instead of recommendation exhaustion");
            var legacy = new IdiomSnapshot(IdiomMode.SameCharacter, IdiomActor.Player, IdiomEndReason.RecommendationExhausted, 17,
                new[] { "a" }, new IdiomHint[0], IdiomMatchKind.Computer, 19, new string[0],
                new IdiomSelectionContext(new[] { "a" }, new string[0]));
            dynamic data=New("IdiomSaveData"),character=New("IdiomModeSave"),sound=New("IdiomModeSave");
            character.Game=legacy;character.Best=1;data.Character=character;
            sound.Game=new IdiomGame(lexicon,IdiomMode.SameSound,17).GetSnapshot();data.Sound=sound;
            AddFreshLocalSlots(data,lexicon);data.Notebook=New("IdiomNotebookData");
            profile.Remember("b");profile.Remember("c");data.Profile=profile.GetSnapshot();
            dynamic store=New("IdiomSaveStore",dir,lexicon);Load(store);Save(store,data);
            dynamic restored=Load(New("IdiomSaveStore",dir,lexicon));
            Assert((int)restored.Character.Game.EndReason==6 && restored.Character.Game.History.Count==1,"Later profile changes rewrote old terminal");
            var root=JObject.Parse(File.ReadAllText((string)store.SavePath));
            foreach(var change in new Action<JObject>[] {
                r=>r["character"]["game"]["endSelectionContext"]=JValue.CreateNull(),
                r=>r["character"]["game"]["endSelectionContext"]["ordinaryIds"]=new JArray("a","b","c"),
                r=>r["character"]["game"]["endSelectionContext"]["ordinaryIds"]=new JArray("a","a"),
                r=>r["character"]["game"]["endSelectionContext"]["reviewIds"]=new JArray("a"),
                r=>r["character"]["game"]["endSelectionContext"]["reviewIds"]=new JArray("unknown") })
            { var changed=(JObject)root.DeepClone();change(changed);RejectGameDocument(dir,lexicon,changed.ToString()); }
        });
    }
    private static void GameRoundTrip()
    {
        Type("IdiomSaveStore"); WithDirectory(dir => {
            dynamic lexicon=Lexicon(), store=New("IdiomSaveStore",dir,lexicon); Load(store); dynamic original=GameData(lexicon); Save(store,original);
            dynamic data=Load(New("IdiomSaveStore",dir,lexicon));
            Assert((int)data.ActiveMode==1 && data.Character.Draft=="发扬" && data.Sound.Draft=="人先开始","Mode/drafts changed");
            Assert(data.Character.Best==1 && data.Sound.Best==2 && data.Character.NewBestInRun && !data.Sound.NewBestInRun,"Best state changed");
            Assert(data.Character.Game.History.Count==2 && data.Character.Game.Hints.Count==1 && data.Sound.Game.History.Count==0,"Game histories changed");
            Assert(data.Character.Game.RandomState==original.Character.Game.RandomState,"Random state rerolled");
            dynamic game=Activator.CreateInstance(CoreType("IdiomGame"),new object[]{lexicon,data.Character.Game});
            Assert(game.PlayerCount==1 && game.HintCount==1,"Restored core state changed");
        });
    }
    private static void LearningAggregateContract()
    {
        WithDirectory(dir => {
            string oldGame=Path.Combine(dir,"idiom-v1.json"), oldNotebook=Path.Combine(dir,"idiom-notebook-v1.json");
            File.WriteAllText(oldGame,"legacy-game"); File.WriteAllText(oldNotebook,"legacy-notebook");
            dynamic lexicon=Lexicon(),store=New("IdiomSaveStore",dir,lexicon);
            Assert(Path.GetFileName((string)store.SavePath)=="idiom-learning-v1.json","Learning aggregate needs its own file");
            Assert(Load(store)==null && store.CanWrite,"Legacy documents must not become partial learning state");
            Save(store,GameData(lexicon)); var root=JObject.Parse(File.ReadAllText((string)store.SavePath));
            Assert((int)root["schemaVersion"]==1 && (string)root["gameId"]=="idiom-learning" && root["profile"]!=null && root["notebook"]!=null,"Aggregate must contain games, profile and notebook together");
            Assert(File.ReadAllText(oldGame)=="legacy-game" && File.ReadAllText(oldNotebook)=="legacy-notebook","Legacy files were modified");
        });
    }
    private static dynamic LocalSlot(dynamic lexicon, int mode, int first, int words, bool allHints)
    {
        dynamic game=Activator.CreateInstance(CoreType("IdiomGame"),new object[]{lexicon,EnumValue("IdiomMode",mode),(uint)41,EnumValue("IdiomMatchKind",1)});
        game.Start((dynamic)EnumValue("IdiomActor",first));
        var chain=new[]{"一心一意","意气风发","发扬光大","大有可为"};
        for(int i=0;i<words;i++) { if(allHints)game.RequestHint(); Assert(game.Submit(chain[i]).Success,"Local fixture submit failed"); }
        game.RequestHint();
        dynamic slot=New("IdiomModeSave"); slot.Game=game.GetSnapshot(); slot.Draft=mode==0?"左侧草稿":"右侧草稿";
        slot.Best=mode==0?words:words+2; slot.NewBestInRun=mode==0 && words>0; return slot;
    }
    private static dynamic FourSlotData(dynamic lexicon)
    {
        dynamic data=GameData(lexicon);
        data.ActiveMatchKind=(dynamic)EnumValue("IdiomMatchKind",1);
        data.LocalCharacter=LocalSlot(lexicon,0,1,4,true);
        data.LocalSound=LocalSlot(lexicon,1,2,1,false);
        return data;
    }
    private static void LegacyGameRejected()
    {
        WithDirectory(dir => {
            dynamic lexicon=Lexicon(),store=New("IdiomSaveStore",dir,lexicon); Load(store); Save(store,GameData(lexicon));
            string path=store.SavePath; var root=JObject.Parse(File.ReadAllText(path)); root["gameId"]="idiom";
            File.WriteAllText(path,root.ToString()); var before=File.ReadAllBytes(path);
            store=New("IdiomSaveStore",dir,lexicon);
            Assert(Load(store)==null && !store.CanWrite && store.NeedsRecovery,"Legacy identity was interpreted as learning state");
            Equal(before,File.ReadAllBytes(path));
        });
    }
    private static IdiomLexicon HintLoopLexicon()
    {
        var entries = new IdiomEntry[12];
        for (int i = 0; i < entries.Length; i++)
            entries[i] = new IdiomEntry("loop" + i, "一" + (char)(0x4e00 + i) + "心一", "yī yī xīn yī", "yi", "yi", "sense" + i, "00", true);
        return new IdiomLexicon(entries, "hint-loop-v1");
    }
    private static IdiomSaveData EmptyAggregate(IdiomLexicon lexicon)
    {
        return new IdiomSaveData {
            Character = new IdiomModeSave { Game = new IdiomGame(lexicon, IdiomMode.SameCharacter, 17).GetSnapshot() },
            Sound = new IdiomModeSave { Game = new IdiomGame(lexicon, IdiomMode.SameSound, 17).GetSnapshot() },
            LocalCharacter = new IdiomModeSave { Game = new IdiomGame(lexicon, IdiomMode.SameCharacter, 17, IdiomMatchKind.Local).GetSnapshot() },
            LocalSound = new IdiomModeSave { Game = new IdiomGame(lexicon, IdiomMode.SameSound, 17, IdiomMatchKind.Local).GetSnapshot() },
            Profile = new IdiomLearningProfile(lexicon).GetSnapshot(), Notebook = new IdiomNotebookData() };
    }
    private static void SixHintsRoundTrip()
    {
        WithDirectory(dir => {
            var lexicon = HintLoopLexicon(); var game = new IdiomGame(lexicon, IdiomMode.SameCharacter, 17, IdiomMatchKind.Local);
            game.Start(IdiomActor.Left);
            for (int i = 0; i < 6; i++) {
                Assert(game.RequestHint() != null, "Both humans must get their third hint");
                Assert(game.Submit(lexicon.Entries[i].Word).Success, "Third suggestions do not end the local match");
            }
            Assert(!game.IsEnded && game.HintCount == 6 && game.HintQuotaExhausted, "Fixture needs active continuation with exhausted quotas");
            var data = EmptyAggregate(lexicon); data.LocalCharacter.Game = game.GetSnapshot(); data.LocalCharacter.Best = 6;
            var store = new IdiomSaveStore(dir, lexicon); string warning; store.Load(out warning); Save(store, data);
            var restored = new IdiomSaveStore(dir, lexicon).Load(out warning);
            Assert(restored != null, "Codec must accept six local hint records: " + warning);
            game = new IdiomGame(lexicon, restored.LocalCharacter.Game);
            Assert(game.HintCount == 6 && !game.IsEnded && game.RequestHint() == null, "Quota and active state survive codec round trip");
            Assert(game.Submit(lexicon.Entries[6].Word).Success, "Restored local game continues after all six hints");
        });
    }
    private static void SurrenderRoundTrip()
    {
        WithDirectory(dir => {
            var lexicon = HintLoopLexicon(); var game = new IdiomGame(lexicon, IdiomMode.SameCharacter, 17);
            game.Start(IdiomActor.Player); game.Surrender();
            Assert(game.EndReason == IdiomEndReason.Surrendered && (int)game.EndReason == 7, "Fixture surrender has stable serialized value");
            var data = EmptyAggregate(lexicon); data.Character.Game = game.GetSnapshot();
            var store = new IdiomSaveStore(dir, lexicon); string warning; store.Load(out warning); Save(store, data);
            var restored = new IdiomSaveStore(dir, lexicon).Load(out warning);
            Assert(restored != null, "Codec must accept surrender reason seven: " + warning);
            game = new IdiomGame(lexicon, restored.Character.Game);
            Assert(game.EndReason == IdiomEndReason.Surrendered && game.Submit(lexicon.Entries[0].Word).Error == IdiomError.AlreadyEnded, "Surrender remains terminal after storage restore");
        });
    }
    private static void FourSlotRoundTrip()
    {
        WithDirectory(dir => {
            dynamic lexicon=Lexicon(),store=New("IdiomSaveStore",dir,lexicon); Load(store); Save(store,FourSlotData(lexicon));
            var root=JObject.Parse(File.ReadAllText((string)store.SavePath));
            Assert((int)root["schemaVersion"]==1 && root["localCharacter"]!=null && root["localSound"]!=null,"Aggregate writes all four slots");
            dynamic data=Load(New("IdiomSaveStore",dir,lexicon));
            Assert((int)data.ActiveMatchKind==1 && (int)data.ActiveMode==1,"Both active selectors persist");
            Assert(data.Character.Game.History.Count==2 && data.Sound.Game.History.Count==0 && data.LocalCharacter.Game.History.Count==4 && data.LocalSound.Game.History.Count==1,"All four histories remain independent");
            Assert(data.LocalCharacter.Draft=="左侧草稿" && data.LocalSound.Draft=="右侧草稿" && data.LocalCharacter.Best==4 && data.LocalSound.Best==3,"Local drafts and total-word records persist");
            Assert(data.LocalCharacter.NewBestInRun && !data.LocalSound.NewBestInRun,"Each local best flag persists");
            dynamic game=Activator.CreateInstance(CoreType("IdiomGame"),new object[]{lexicon,data.LocalCharacter.Game});
            Assert((int)game.MatchKind==1 && !game.IsEnded && game.HintQuotaExhausted && game.HintCount==5 && game.HintCountFor((dynamic)EnumValue("IdiomActor",1))==3 && game.HintCountFor((dynamic)EnumValue("IdiomActor",2))==2,"Active local game preserves independent per-human quotas");
            Assert(game.Submit("为所欲为").Success && game.History.Count==5,"Restored local game uses its third hint and continues");
            game=Activator.CreateInstance(CoreType("IdiomGame"),new object[]{lexicon,data.LocalSound.Game});
            Assert((int)game.NextActor==1 && game.Submit("意气风发").Success && game.History.Count==2,"Restored local game continues one human word at a time");
        });
    }
    private static void InvalidLocalStates()
    {
        WithDirectory(dir => {
            dynamic lexicon=Lexicon(),store=New("IdiomSaveStore",dir,lexicon); Load(store); Save(store,FourSlotData(lexicon));
            var root=JObject.Parse(File.ReadAllText((string)store.SavePath));
            foreach(var change in new Action<JObject>[] {
                r=>r["activeMatchKind"]=2,
                r=>r["character"]["game"]["matchKind"]=1,
                r=>r["localSound"]["game"]["matchKind"]=0,
                r=>r["localCharacter"]["game"]["matchKind"]=2,
                r=>r["localCharacter"]["best"]=2,
                r=>r["localCharacter"]["best"]=5,
                r=>r["localCharacter"]["game"]["endReason"]=2,
                r=>((JArray)r["localCharacter"]["game"]["hints"]).Add(r["localCharacter"]["game"]["hints"][0].DeepClone()),
                r=>((JArray)r["localCharacter"]["game"]["history"]).Add("entry4"),
                r=>r["localSound"]["game"]["history"][0]="unknown",
                r=>r["localSound"]["game"]["hints"][0]["entryIds"][0]="entry0" })
            { var changed=(JObject)root.DeepClone(); change(changed); RejectGameDocument(dir,lexicon,changed.ToString()); }
        });
    }
    private static void UnknownGameSchema()
    {
        WithDirectory(dir => {
            dynamic lexicon=Lexicon(),store=New("IdiomSaveStore",dir,lexicon); Load(store); Save(store,GameData(lexicon)); Save(store,GameData(lexicon));
            string path=store.SavePath; var root=JObject.Parse(File.ReadAllText(path)); root["schemaVersion"]=3;
            File.WriteAllText(path,root.ToString()); var before=File.ReadAllBytes(path);
            store=New("IdiomSaveStore",dir,lexicon); Assert(Load(store)==null && !store.CanWrite && store.NeedsRecovery,"Unknown schema fell back to compatible backup");
            RejectSave(store,GameData(lexicon)); Equal(before,File.ReadAllBytes(path));
        });
    }
    private static void StrictGameJson()
    {
        Type("IdiomSaveStore"); WithDirectory(dir => {
            dynamic lexicon=Lexicon(), store=New("IdiomSaveStore",dir,lexicon); Load(store); Save(store,GameData(lexicon)); string path=store.SavePath;
            string valid=File.ReadAllText(path); var root=JObject.Parse(valid);
            foreach(string section in new[]{"","character","sound","character.game","localCharacter","localSound","localCharacter.game","localSound.game","profile","notebook"})
            {
                var current=section.Length==0?root:(JObject)root.SelectToken(section);
                foreach(var property in current.Properties())
                {
                    var changed=(JObject)root.DeepClone(); var target=section.Length==0?changed:(JObject)changed.SelectToken(section);
                    if(section.Length!=0 || property.Name!="closeKeyboardAfterSend" && property.Name!="showPinyin")
                    { target.Remove(property.Name); RejectGameDocument(dir,lexicon,changed.ToString()); }
                    changed=(JObject)root.DeepClone(); target=section.Length==0?changed:(JObject)changed.SelectToken(section);
                    if(property.Value.Type!=JTokenType.Null) { target[property.Name]=JValue.CreateNull(); RejectGameDocument(dir,lexicon,changed.ToString()); }
                }
                var unknown=(JObject)root.DeepClone(); (section.Length==0?unknown:(JObject)unknown.SelectToken(section))["extra"]=1;
                RejectGameDocument(dir,lexicon,unknown.ToString());
            }
            foreach(string field in new[]{"activeMode","activeMatchKind","character.best","character.game.mode","character.game.matchKind","localCharacter.game.matchKind","character.game.firstActor","character.game.endReason","character.game.randomState","character.game.hintRandomState"})
                foreach(var token in new JToken[]{"1",1.0,true,new JArray(),-1}) {
                    var changed=(JObject)root.DeepClone(); changed.SelectToken(field).Replace(token); RejectGameDocument(dir,lexicon,changed.ToString());
                }
            RejectGameDocument(dir,lexicon,valid.Replace("\"randomState\":","\"randomState\":17,\"randomState\":"));
            RejectGameDocument(dir,lexicon,valid+"{}");
        });
    }
    private static void RejectGameDocument(string dir,dynamic lexicon,string json)
    {
        var path=Path.Combine(dir,"idiom-learning-v1.json"); File.WriteAllText(path,json); if(File.Exists(path+".bak"))File.Delete(path+".bak");
        dynamic store=New("IdiomSaveStore",dir,lexicon); string warning;
        Assert(store.Load(out warning)==null && !store.CanWrite && store.NeedsRecovery,"Invalid game accepted");
        Assert(!string.IsNullOrEmpty(warning),"Invalid game lacks warning");
    }
    private static void InvalidGameStates()
    {
        Type("IdiomSaveStore"); WithDirectory(dir => {
            dynamic lexicon=Lexicon(),store=New("IdiomSaveStore",dir,lexicon); Load(store); Save(store,GameData(lexicon));
            var root=JObject.Parse(File.ReadAllText((string)store.SavePath));
            foreach(var change in new Action<JObject>[] {
                r=>r["character"]["game"]["history"][1]="entry0",
                r=>r["character"]["game"]["history"][1]="unknown",
                r=>r["character"]["game"]["mode"]=1,
                r=>r["character"]["game"]["endReason"]=1,
                r=>r["character"]["game"]["randomState"]=0,
                r=>r["character"]["best"]=0,
                r=>r["sound"]["newBestInRun"]=true,
                r=>r["character"]["game"]["hints"][0]["turnIndex"]=999,
                r=>((JArray)r["character"]["game"]["hints"]).Add(r["character"]["game"]["hints"][0].DeepClone()) })
            { var changed=(JObject)root.DeepClone(); change(changed); RejectGameDocument(dir,lexicon,changed.ToString()); }
        });
    }
    private static void DictionaryMismatch()
    {
        Type("IdiomSaveStore"); WithDirectory(dir => {
            dynamic lexicon=Lexicon(),store=New("IdiomSaveStore",dir,lexicon); Load(store); Save(store,GameData(lexicon)); Save(store,GameData(lexicon)); string path=store.SavePath;
            var root=JObject.Parse(File.ReadAllText(path)); root["dictionaryVersion"]="future-dictionary"; File.WriteAllText(path,root.ToString()); var before=File.ReadAllBytes(path);
            store=New("IdiomSaveStore",dir,lexicon); Assert(Load(store)==null && !store.CanWrite && store.NeedsRecovery,"Mismatch fell back to older backup");
            RejectSave(store,GameData(lexicon)); Equal(before,File.ReadAllBytes(path));
        });
    }
    private static void CatalogUnavailable()
    {
        Type("IdiomSaveStore"); WithDirectory(dir => {
            var path=Path.Combine(dir,"idiom-learning-v1.json"); File.WriteAllText(path,"original"); dynamic store=New("IdiomSaveStore",dir,null); string error;
            Assert(store.Load(out error)==null && !store.CanWrite && !store.NeedsRecovery && !string.IsNullOrEmpty(error),"Catalog absence treated as empty game");
            Assert(!store.TryResetProtected(out error),"Catalog absence allowed reset"); Assert(File.ReadAllText(path)=="original","Original overwritten");
        });
    }
    private static void GameRecovery()
    {
        Type("IdiomSaveStore"); WithDirectory(dir => {
            dynamic lexicon=Lexicon(),store=New("IdiomSaveStore",dir,lexicon); Load(store); Save(store,GameData(lexicon)); Save(store,GameData(lexicon)); string path=store.SavePath;
            byte[] backup=File.ReadAllBytes(path+".bak"); File.WriteAllText(path,"broken"); store=New("IdiomSaveStore",dir,lexicon);
            Assert(Load(store)!=null && store.CanWrite,"Game backup did not recover"); Save(store,GameData(lexicon)); Equal(backup,File.ReadAllBytes(path+".bak"));
            File.WriteAllText(path,"broken"); File.WriteAllText(path+".bak","broken backup"); store=New("IdiomSaveStore",dir,lexicon); Load(store);
            RejectSave(store,GameData(lexicon)); string error; Assert(store.TryResetProtected(out error),error); Save(store,GameData(lexicon));
        });
    }
    private static void GameFileLimit()
    {
        Type("IdiomSaveStore"); WithDirectory(dir => {
            var path=Path.Combine(dir,"idiom-learning-v1.json"); using(var stream=File.Create(path))stream.SetLength((long)IdiomSaveCodec.MaximumBytes+1);
            dynamic store=New("IdiomSaveStore",dir,Lexicon()); Assert(Load(store)==null && !store.CanWrite,"Oversized game accepted");
        });
    }
    private static void LongHistories()
    {
        WithDirectory(dir => {
            const int count=12000;
            var entries=Array.CreateInstance(CoreType("IdiomEntry"),count+2); var history=new string[count];
            for(int i=0;i<count+2;i++)
            {
                string word="一"+(char)(0x4e00+i/200)+(char)(0x4e00+i%200)+"一";
                entries.SetValue(Activator.CreateInstance(CoreType("IdiomEntry"),new object[]{"long-entry-"+i,word,"yī yī yī yī","yi","yi","sense-"+i,"0000",false}),i);
                if(i<count)history[i]="long-entry-"+i;
            }
            dynamic lexicon=Activator.CreateInstance(CoreType("IdiomLexicon"),new object[]{entries,"large-v1"});
            dynamic data=New("IdiomSaveData"); data.ActiveMode=(dynamic)EnumValue("IdiomMode",0);
            AddFreshLocalSlots(data,lexicon); AddFreshLearning(data,lexicon);
            for(int mode=0;mode<2;mode++)
            {
                dynamic slot=New("IdiomModeSave");
                slot.Game=(dynamic)Activator.CreateInstance(CoreType("IdiomSnapshot"),new object[]{EnumValue("IdiomMode",mode),EnumValue("IdiomActor",2),EnumValue("IdiomEndReason",0),(uint)17,history,Array.CreateInstance(CoreType("IdiomHint"),0)});
                slot.Best=count/2; slot.Draft="";
                if(mode==0)data.Character=slot;else data.Sound=slot;
            }
            dynamic store=New("IdiomSaveStore",dir,lexicon); Load(store); Save(store,data);
            dynamic restored=Load(New("IdiomSaveStore",dir,lexicon));
            Assert(restored.Character.Game.History.Count==count && restored.Sound.Game.History.Count==count,"Long mode history lost");
        });
    }
    private static void FourLongHistories()
    {
        WithDirectory(dir => {
            // Exercise all 45,776 four-character entries with stable 30-character IDs.
            const int count=45776;
            var entries=new IdiomEntry[count]; var history=new string[count];
            for(int i=0;i<count;i++)
            {
                history[i]="entry-"+i.ToString("D24");
                string word="一"+(char)(0x4e00+i/200)+(char)(0x4e00+i%200)+"一";
                entries[i]=new IdiomEntry(history[i],word,"yī yī yī yī","yi","yi","sense-"+i,"0000",false);
            }
            var lexicon=new IdiomLexicon(entries,"four-large-v1");
            var slots=new IdiomModeSave[4];
            for(int i=0;i<slots.Length;i++)
            {
                var kind=i<2?IdiomMatchKind.Computer:IdiomMatchKind.Local;
                slots[i]=new IdiomModeSave { Game=new IdiomSnapshot((IdiomMode)(i%2),IdiomActor.Player,
                    IdiomEndReason.DictionaryExhausted,17,history,new IdiomHint[0],kind),
                    Best=kind==IdiomMatchKind.Local?count:(count+1)/2 };
            }
            var data=new IdiomSaveData { Character=slots[0],Sound=slots[1],LocalCharacter=slots[2],LocalSound=slots[3] };
            AddFreshLearning(data,lexicon);
            var store=new IdiomSaveStore(dir,lexicon); string error; store.Load(out error);
            Assert(store.TrySave(data,out error),error);
            Assert(new FileInfo(store.SavePath).Length>4L*1024*1024,"Fixture must exercise the previous four MiB ceiling");
            var restored=new IdiomSaveStore(dir,lexicon).Load(out error);
            Assert(restored!=null && restored.Character.Game.History.Count==count && restored.Sound.Game.History.Count==count &&
                restored.LocalCharacter.Game.History.Count==count && restored.LocalSound.Game.History.Count==count,"Four complete histories truncated");
        });
    }
    private static void LargeNotebook()
    {
        WithDirectory(dir => {
            dynamic data=New("IdiomNotebookData");
            for(int i=0;i<10000;i++)
            {
                dynamic item=New("IdiomNotebookItem"); item.Id="item"+i;item.SenseId="sense"+i;
                item.Word="一"+(char)(0x4e00+i/200)+(char)(0x4e00+i%200)+"意";
                item.Pinyin="yī xīn yī yì";item.Meaning="完整词义";item.DictionaryVersion="dictionary-v1";item.AddedAt=1793000000L+i;
                data.Entries.Add(item);
            }
            dynamic store=New("IdiomNotebookStore",dir);Load(store);Save(store,data);
            Assert(Load(New("IdiomNotebookStore",dir)).Entries.Count==10000,"Large notebook lost bookmarks");
        });
    }
    private static void NotebookByteLimit()
    {
        WithDirectory(dir => {
            dynamic store=New("IdiomNotebookStore",dir);Load(store);Save(store,Notebook()); string path=store.SavePath;var before=File.ReadAllBytes(path);
            dynamic data=New("IdiomNotebookData"); string meaning=new string('a',65536);
            for(int i=0;i<513;i++)
            {
                dynamic item=New("IdiomNotebookItem");item.Id="item"+i;item.SenseId="sense"+i;
                item.Word="一"+(char)(0x4e00+i/200)+(char)(0x4e00+i%200)+"意";
                item.Pinyin="yī xīn yī yì";item.Meaning=meaning;item.DictionaryVersion="dictionary-v1";item.AddedAt=1793000000L;
                data.Entries.Add(item);
            }
            string error;Assert(!store.TrySave(data,out error) && error.Contains("上限"),"Byte limit must report capacity clearly");
            Equal(before,File.ReadAllBytes(path));
        });
    }
    private static void DirectoryUnavailable()
    {
        WithDirectory(dir => {
            string blocked=Path.Combine(dir,"unavailable");File.WriteAllText(blocked,"not a directory");
            dynamic store=New("IdiomNotebookStore",blocked);string warning;
            Assert(store.Load(out warning)==null && !store.CanWrite && !string.IsNullOrEmpty(warning),"Unavailable directory was treated as missing save");
        });
    }
    private static void WithDirectory(Action<string> action)
    {
        var dir=Path.Combine(Path.GetTempPath(),"idiom-persistence-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try { action(dir); } finally { Directory.Delete(dir,true); }
    }
    private static void Equal(byte[] expected,byte[] actual) { Assert(Convert.ToBase64String(expected)==Convert.ToBase64String(actual),"Bytes changed"); }
    private static void Assert(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
    private static void Check(string name,Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS "+name); }
        catch(Exception ex) { failed++; Console.WriteLine("FAIL "+name+": "+(ex is TargetInvocationException ? ex.InnerException.Message : ex.Message)); }
    }
}
