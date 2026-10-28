using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Gomoku;
using XanaduYonder.Runtime.Gomoku;
internal static class Program
{
    private static int passed;
    private static void Main()
    {
        Check("production JSON round trip preserves both mode slots and preferences", () => {
            var original = Profile(); var json = GomokuSaveCodec.Encode(original); Assert(GomokuSaveCodec.TryDecode(json, out var saved, out var reason), reason);
            Assert(saved.SelectedMode == GomokuMode.Local && saved.PreferredDifficulty == GomokuDifficulty.Hard && saved.PreferredHumanStone == GomokuStone.White && saved.PreferredBlackPlayer == 2);
            Assert(saved.Slots[0].Mode == GomokuMode.HumanVsAi && saved.Slots[1].Mode == GomokuMode.Local); Eq(saved.Slots[0].Moves.Length, 2); Eq(saved.Slots[1].Moves.Length, 1);
            var session = new GomokuSession(saved.Slots[0]); Assert(session.Paused && !session.SelectedIndex.HasValue && !session.AiPending);
            Assert(!json.Contains("state") && !json.Contains("winningLine") && !json.Contains("selectedIndex") && !json.Contains("paused"));
        });
        Check("empty slots are valid but missing and explicit null fields are rejected", () => {
            Assert(new GomokuSaveData().TryValidate(out var reason), reason); var json = JObject.Parse(GomokuSaveCodec.Encode(Profile()));
            foreach (var field in new[] {"schemaVersion","gameId","selectedMode","preferredDifficulty","preferredHumanStone","preferredBlackPlayer","slots"}) {
                var missing = (JObject)json.DeepClone(); missing.Remove(field); Reject(missing.ToString()); var nil = (JObject)json.DeepClone(); nil[field] = JValue.CreateNull(); Reject(nil.ToString());
            }
            foreach (var field in new[] {"sessionId","mode","difficulty","humanStone","blackPlayer","moves"}) {
                var missing = (JObject)json.DeepClone(); ((JObject)missing["slots"][0]).Remove(field); Reject(missing.ToString()); var nil = (JObject)json.DeepClone(); nil["slots"][0][field] = JValue.CreateNull(); Reject(nil.ToString());
            }
        });
        Check("strict tokens reject numbers strings coercion and unknown values", () => {
            var replacements = new[] {"null","true","[]","{}","\"1\"","1.0","1e0","0","2","999999999999999999999999999999999999"};
            foreach (string value in replacements) MutateRoot("schemaVersion", JToken.Parse(value));
            MutateRoot("gameId", "sudoku"); MutateRoot("gameId", 1); MutateRoot("selectedMode", "unknown"); MutateRoot("preferredDifficulty", "unknown"); MutateRoot("preferredHumanStone", "empty");
            MutateRoot("preferredBlackPlayer", "1"); MutateRoot("preferredBlackPlayer", 1.0); MutateRoot("preferredBlackPlayer", 0); MutateRoot("preferredBlackPlayer", 3);
            MutateSlot("mode", 0); MutateSlot("difficulty", "unknown"); MutateSlot("humanStone", "empty"); MutateSlot("blackPlayer", true); MutateSlot("moves", "0,1");
        });
        Check("wrong slot size mode identity and duplicate sessions are rejected", () => {
            MutateRoot("slots", new JArray()); MutateRoot("slots", new JArray(JValue.CreateNull())); MutateRoot("slots", new JArray(JValue.CreateNull(), JValue.CreateNull(), JValue.CreateNull()));
            MutateSlot("mode", "local"); var root = Json(); root["slots"][1]["sessionId"] = root["slots"][0]["sessionId"].DeepClone(); Reject(root.ToString());
            var copy = Profile().Copy(); copy.Slots[0] = copy.Slots[1]; Assert(!copy.TryValidate(out var reason));
        });
        Check("canonical nonempty GUID required at actual JSON boundary", () => {
            foreach (string id in new[] {"", "not-a-guid", Guid.Empty.ToString("D"), Guid.NewGuid().ToString("N"), "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee".ToUpperInvariant()}) MutateSlot("sessionId", id);
        });
        Check("illegal histories duplicates noninteger moves and terminal continuation rejected", () => {
            foreach (var moves in new[] {new JArray(-1),new JArray(225),new JArray(0,0),new JArray(1.0),new JArray("1"),new JArray(true),new JArray(JValue.CreateNull()),new JArray(0,15,1,16,2,17,3,18,4,19)}) MutateSlot("moves", moves);
            var tooMany = new JArray(); for (int i=0;i<226;i++) tooMany.Add(i%225); MutateSlot("moves", tooMany);
            var root=Json(); root["slots"][1]["moves"]=new JArray(0,15,1,16,2,17,3,18,4); Assert(GomokuSaveCodec.TryDecode(root.ToString(), out var saved, out var reason),reason); Assert(new GomokuSession(saved.Slots[1]).Game.State==GomokuState.BlackWon);
        });
        Check("strict parser rejects duplicate properties JavaScript syntax and trailing data", () => {
            var json = GomokuSaveCodec.Encode(Profile()); Reject(json.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1"));
            Reject(json.Replace("\"blackPlayer\":1", "\"blackPlayer\":1,\"blackPlayer\":1"));
            foreach (var bad in new[] {"{} {}", "null", "[]", "{", json+"//comment", "/*comment*/"+json, json.Replace("\"schemaVersion\"", "schemaVersion"), json.Replace("\"gameId\"", "'gameId'"), json.Replace("\"schemaVersion\":1", "\"schemaVersion\":01"), json.Replace("\"schemaVersion\":1", "\"schemaVersion\":NaN"), json.Insert(json.Length-1, ",")}) Reject(bad);
            var unknown=Json(); unknown["unexpected"]=true; Reject(unknown.ToString()); var nested=Json(); nested["slots"][0]["unexpected"]=true; Reject(nested.ToString());
        });
        Check("size and depth caps apply before deserialization", () => {
            Reject(new string(' ', GomokuSaveCodec.MaximumLength+1)); Reject("{\"x\":"+new string('[',GomokuSaveCodec.MaximumDepth+1)+"0"+new string(']',GomokuSaveCodec.MaximumDepth+1)+"}");
            var json=Json(); json["gameId"]=new string('汉',GomokuSaveCodec.MaximumLength/2); Reject(json.ToString());
        });
        Check("copies isolate arrays and encode rejects bad DTO without repair", () => {
            var original=Profile(); var copy=original.Copy(); copy.Slots[0].Moves[0]=200; copy.Slots[1]=null; Eq(original.Slots[0].Moves[0],0); Assert(original.Slots[1]!=null);
            original.Slots[0].Moves=null; Assert(!original.TryValidate(out var reason)); Throws(()=>GomokuSaveCodec.Encode(original));
            Assert(!new GomokuSaveData {Slots=null}.TryValidate(out reason)); Assert(!new GomokuSaveData {PreferredBlackPlayer=0}.TryValidate(out reason));
        });
        WithDirectory("store differentiates missing loaded recovered and invalid", directory => {
            var store=new GomokuSaveStore(directory); Assert(store.Load(out var warning)==null && warning==null && store.LastLoadStatus==GomokuLoadStatus.Missing);
            Assert(store.SavePath==Path.Combine(directory,"gomoku-v1.json")); var profile=Profile(); Assert(store.TrySave(profile,out var error),error); Assert(store.Load(out warning)!=null && warning==null && store.LastLoadStatus==GomokuLoadStatus.Loaded);
            Assert(!File.Exists(store.SavePath+".bak")); profile.PreferredBlackPlayer=1; Assert(store.TrySave(profile,out error),error); Assert(File.Exists(store.SavePath+".bak"));
            File.WriteAllText(store.SavePath,"broken"); var recovered=store.Load(out warning); Assert(recovered!=null && recovered.PreferredBlackPlayer==2 && warning!=null && store.LastLoadStatus==GomokuLoadStatus.Recovered);
            File.WriteAllText(store.SavePath+".bak","broken"); Assert(store.Load(out warning)==null && warning!=null && store.LastLoadStatus==GomokuLoadStatus.Invalid);
        });
        WithDirectory("recovery preserves valid backup when replacing damaged main", directory => {
            var store=new GomokuSaveStore(directory); var first=Profile(); Assert(store.TrySave(first,out var error),error); var second=first.Copy(); second.PreferredBlackPlayer=1; Assert(store.TrySave(second,out error),error);
            string backup=File.ReadAllText(store.SavePath+".bak"); File.WriteAllText(store.SavePath,"corrupt"); Assert(store.Load(out var warning)!=null); Assert(store.TrySave(second,out error),error);
            Assert(File.ReadAllText(store.SavePath+".bak")==backup); Assert(store.Load(out warning).PreferredBlackPlayer==1); Assert(!File.Exists(store.SavePath+".tmp"));
        });
        WithDirectory("missing primary recovers backup and unrelated game remains untouched", directory => {
            var store=new GomokuSaveStore(directory); File.WriteAllText(Path.Combine(directory,"sudoku-v1.json"),"untouched"); File.WriteAllText(store.SavePath+".bak",GomokuSaveCodec.Encode(Profile()));
            Assert(store.Load(out var warning)!=null && warning!=null && store.LastLoadStatus==GomokuLoadStatus.Recovered); Assert(store.TrySave(Profile(),out var error),error); Assert(File.ReadAllText(Path.Combine(directory,"sudoku-v1.json"))=="untouched");
        });
        WithDirectory("invalid save cannot alter committed primary or backup", directory => {
            var store=new GomokuSaveStore(directory); Assert(store.TrySave(Profile(),out var error),error); Assert(store.TrySave(Profile(),out error),error);
            string main=File.ReadAllText(store.SavePath),backup=File.ReadAllText(store.SavePath+".bak"); var bad=Profile(); bad.Slots[0].Moves=new[]{0,0}; Assert(!store.TrySave(bad,out error) && error!=null);
            Assert(File.ReadAllText(store.SavePath)==main && File.ReadAllText(store.SavePath+".bak")==backup); Assert(!store.TrySave(null,out error));
        });
        WithDirectory("actual UTF8 size and file access failures are reported", directory => {
            var store=new GomokuSaveStore(directory); File.WriteAllBytes(store.SavePath,new byte[]{0xff,0xfe}); Assert(store.Load(out var warning)==null && warning!=null && store.LastLoadStatus==GomokuLoadStatus.Invalid);
            File.WriteAllBytes(store.SavePath,new byte[GomokuSaveCodec.MaximumLength+1]); Assert(store.Load(out warning)==null && store.LastLoadStatus==GomokuLoadStatus.Invalid);
            File.Delete(store.SavePath); Directory.CreateDirectory(store.SavePath); Assert(store.Load(out warning)==null && warning!=null && store.LastLoadStatus==GomokuLoadStatus.Invalid); Assert(!store.TrySave(Profile(),out var error) && error!=null);
            string blocked=Path.Combine(directory,"not-a-directory"); File.WriteAllText(blocked,"blocked"); var blockedStore=new GomokuSaveStore(blocked); Assert(blockedStore.Load(out warning)==null && warning!=null && blockedStore.LastLoadStatus==GomokuLoadStatus.Invalid); Assert(!blockedStore.TrySave(Profile(),out error) && error!=null);
        });
        Console.WriteLine("Gomoku persistence: "+passed+" passed");
    }
    private static GomokuSaveData Profile() {
        var human=GomokuSession.Create(GomokuMode.HumanVsAi,GomokuDifficulty.Hard,GomokuStone.Black,1); human.TrySelect(0); human.ConfirmSelection(); human.AcceptAi(human.BeginAi(),1);
        var local=GomokuSession.Create(GomokuMode.Local,GomokuDifficulty.Easy,GomokuStone.White,2); local.TrySelect(112); local.ConfirmSelection();
        return new GomokuSaveData {SelectedMode=GomokuMode.Local,PreferredDifficulty=GomokuDifficulty.Hard,PreferredHumanStone=GomokuStone.White,PreferredBlackPlayer=2,Slots=new[]{human.Export(),local.Export()}};
    }
    private static JObject Json(){return JObject.Parse(GomokuSaveCodec.Encode(Profile()));}
    private static void MutateRoot(string field,JToken value){var json=Json();json[field]=value;Reject(json.ToString());}
    private static void MutateSlot(string field,JToken value){var json=Json();json["slots"][0][field]=value;Reject(json.ToString());}
    private static void Reject(string json){Assert(!GomokuSaveCodec.TryDecode(json,out var data,out var reason),"Accepted invalid JSON: "+json);Assert(data==null && !string.IsNullOrEmpty(reason));}
    private static void WithDirectory(string name,Action<string> action){Check(name,()=>{string directory=Path.Combine(Path.GetTempPath(),"gomoku-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);try{action(directory);}finally{Directory.Delete(directory,true);}});}
    private static void Throws(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Expected ArgumentException");}
    private static void Eq(int actual,int expected){Assert(actual==expected,actual+" != "+expected);}
    private static void Assert(bool value,string message="Assertion failed"){if(!value)throw new Exception(message);}
    private static void Check(string name,Action action){action();passed++;Console.WriteLine("PASS "+name);}
}
