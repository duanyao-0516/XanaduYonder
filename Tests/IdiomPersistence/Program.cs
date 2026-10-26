using System;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;

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
        Check("Game JSON rejects unknown fields, duplicate fields and token coercion", StrictGameJson);
        Check("Core restore validates history, mode, hints and terminal state", InvalidGameStates);
        Check("Dictionary mismatch protects primary even when backup is compatible", DictionaryMismatch);
        Check("Unavailable catalog cannot load, save or reset original data", CatalogUnavailable);
        Check("Game corruption recovery preserves good backup and requires archive for bad files", GameRecovery);
        Check("Game file has a four MiB limit", GameFileLimit);
        Check("Long complete histories in both modes are bounded by the lexicon", LongHistories);
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
        dynamic character=Activator.CreateInstance(CoreType("IdiomGame"),new object[]{lexicon,EnumValue("IdiomMode",0),(uint)17});
        dynamic sound=Activator.CreateInstance(CoreType("IdiomGame"),new object[]{lexicon,EnumValue("IdiomMode",1),(uint)29});
        Assert(character.Start((dynamic)EnumValue("IdiomActor",2)).Success,"Fixture cannot start");
        character.RequestHint(); Assert(character.Submit("一心一意").Success,"Fixture cannot submit");
        dynamic data=New("IdiomSaveData"), c=New("IdiomModeSave"), s=New("IdiomModeSave");
        c.Game=character.GetSnapshot(); c.Draft="发扬"; c.Best=1; c.NewBestInRun=true;
        s.Game=sound.GetSnapshot(); s.Draft="人先开始"; s.Best=2; s.NewBestInRun=false;
        data.ActiveMode=(dynamic)EnumValue("IdiomMode",1); data.Character=c; data.Sound=s; return data;
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
    private static void StrictGameJson()
    {
        Type("IdiomSaveStore"); WithDirectory(dir => {
            dynamic lexicon=Lexicon(), store=New("IdiomSaveStore",dir,lexicon); Load(store); Save(store,GameData(lexicon)); string path=store.SavePath;
            string valid=File.ReadAllText(path); var root=JObject.Parse(valid);
            foreach(string section in new[]{"","character","sound","character.game"})
            {
                var current=section.Length==0?root:(JObject)root.SelectToken(section);
                foreach(var property in current.Properties())
                {
                    var changed=(JObject)root.DeepClone(); var target=section.Length==0?changed:(JObject)changed.SelectToken(section);
                    target.Remove(property.Name); RejectGameDocument(dir,lexicon,changed.ToString());
                    changed=(JObject)root.DeepClone(); target=section.Length==0?changed:(JObject)changed.SelectToken(section);
                    target[property.Name]=JValue.CreateNull(); RejectGameDocument(dir,lexicon,changed.ToString());
                }
                var unknown=(JObject)root.DeepClone(); (section.Length==0?unknown:(JObject)unknown.SelectToken(section))["extra"]=1;
                RejectGameDocument(dir,lexicon,unknown.ToString());
            }
            foreach(string field in new[]{"activeMode","character.best","character.game.mode","character.game.firstActor","character.game.endReason","character.game.randomState"})
                foreach(var token in new JToken[]{"1",1.0,true,new JArray(),-1}) {
                    var changed=(JObject)root.DeepClone(); changed.SelectToken(field).Replace(token); RejectGameDocument(dir,lexicon,changed.ToString());
                }
            RejectGameDocument(dir,lexicon,valid.Replace("\"randomState\":","\"randomState\":17,\"randomState\":"));
            RejectGameDocument(dir,lexicon,valid+"{}");
        });
    }
    private static void RejectGameDocument(string dir,dynamic lexicon,string json)
    {
        var path=Path.Combine(dir,"idiom-v1.json"); File.WriteAllText(path,json); if(File.Exists(path+".bak"))File.Delete(path+".bak");
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
            var path=Path.Combine(dir,"idiom-v1.json"); File.WriteAllText(path,"original"); dynamic store=New("IdiomSaveStore",dir,null); string error;
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
            var path=Path.Combine(dir,"idiom-v1.json"); using(var stream=File.Create(path))stream.SetLength(4L*1024*1024+1);
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
