using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Zuma;
using XanaduYonder.Runtime.Zuma;

internal static class Program
{
    private static int passed, failed;
    private static int Main()
    {
        Check("Missing files produce a complete new profile without writing", () => WithDirectory(dir => {
            var store = new ZumaSaveStore(dir); var result = store.Load();
            Assert(result.Status == ZumaLoadStatus.Missing && result.Data.Profile.Records.Length == 72, "Missing classification/profile");
            Assert(!File.Exists(store.SavePath), "Load must not write"); }));
        Check("All 72 records and Int64 scores survive exact JSON", () => {
            var data = ZumaSaveData.CreateNew(); foreach(var r in data.Profile.Records) r.BestScore = long.MaxValue;
            var json = ZumaSaveCodec.Encode(data); var parsed = JObject.Parse(json);
            Assert(parsed["session"].Type == JTokenType.Null, "No session must be explicit null");
            Assert(parsed["profile"]["records"][0]["bestScore"].Type == JTokenType.String, "Int64 as string");
            var decoded = ZumaSaveCodec.Decode(json); Assert(decoded.Status == ZumaLoadStatus.Loaded, decoded.Warning);
            Assert(decoded.Data.Profile.Records.All(r=>r.BestScore == long.MaxValue), "Score precision"); } );
        Check("Every required profile/root/record field rejects missing/null", RequiredFields);
        Check("Extra fields and duplicate record identities rejected", () => {
            RejectEdit(r => r["extra"] = 1); RejectEdit(r=>((JObject)r["profile"])["extra"] = 1);
            RejectEdit(r=>((JObject)r["profile"]["records"][0])["extra"] = 1);
            RejectEdit(r=>r["profile"]["records"][1] = r["profile"]["records"][0].DeepClone()); });
        Check("Strict integers and score strings reject coercion", () => {
            RejectEdit(r=>r["schemaVersion"] = "1"); RejectEdit(r=>r["schemaVersion"] = 1.0);
            foreach(var value in new JToken[]{-1,1.5,"1",true,JValue.CreateNull()}) RejectEdit(r=>r["profile"]["records"][0]["bestTimeSeconds"] = value);
            foreach(var value in new JToken[]{1,"-1","+1"," 1","1e2","9223372036854775808",JValue.CreateNull()}) RejectEdit(r=>r["profile"]["records"][0]["bestScore"] = value);
            RejectEdit(r=>r["profile"]["lastMapId"] = "not-a-map"); });
        Check("Unknown versions are distinguished before unknown fields", () => {
            foreach(var version in new[]{"schemaVersion","rulesVersion","contentVersion"}) {
                var root=FreshJson(); root[version]=2; root["futureData"]=new JObject(); root.Remove("profile");
                Assert(ZumaSaveCodec.Decode(root.ToString()).Status == ZumaLoadStatus.UnsupportedVersion, version); } });
        Check("Malformed duplicate trailing deep and oversized JSON rejected", () => {
            var json=ZumaSaveCodec.Encode(ZumaSaveData.CreateNew());
            Reject(json+" {}"); Reject(json.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1"));
            Reject("{\"nested\":"+new string('[',13)+"0"+new string(']',13)+"}"); Reject(new string(' ',ZumaSaveCodec.MaximumLength+1));
            Reject(json.Replace("\"spiral\"", "NaN")); Reject("// ignored comment\n"+json); });
        Check("Strict JSON grammar rejects comments, trailing commas and nondecimal numbers", () => {
            var json=ZumaSaveCodec.Encode(ZumaSaveData.CreateNew());
            Reject(json.Insert(json.Length-1,",")); Reject(json.Replace("\"schemaVersion\":1", "\"schemaVersion\":0x1"));
            Reject(json.Replace("\"schemaVersion\":1", "\"schemaVersion\":01")); Reject(json.Replace("\"schemaVersion\":1", "\"schemaVersion\":+1"));
        });
        Check("Primary restore and backup recovery classify distinctly", () => WithDirectory(dir => {
            var store=new ZumaSaveStore(dir); var data=ZumaSaveData.CreateNew(); Assert(store.Save(data).Success,"First save");
            data.Profile.LastMapId="claw"; Assert(store.Save(data).Success,"Second save");
            Assert(new ZumaSaveStore(dir).Load().Data.Profile.LastMapId=="claw","Main wins");
            var backup=File.ReadAllBytes(store.SavePath+".bak"); File.WriteAllText(store.SavePath,"broken");
            Assert(store.Load().Status==ZumaLoadStatus.RecoveredFromBackup,"Recovery"); Assert(store.Save(data).Success,"Repair");
            Assert(backup.SequenceEqual(File.ReadAllBytes(store.SavePath+".bak")),"Corrupt main replaced good backup"); }));
        Check("Corrupt store blocks save, explicit reset archives both originals", () => WithDirectory(dir => {
            var store=new ZumaSaveStore(dir); File.WriteAllText(store.SavePath,"bad-main"); File.WriteAllText(store.SavePath+".bak","bad-backup");
            Assert(store.Load().Status==ZumaLoadStatus.Corrupt && store.IsWriteProtected,"Corrupt protection");
            Assert(!store.Save(ZumaSaveData.CreateNew()).Success,"Corrupt overwrite"); Assert(store.ResetCorrupt().Success,"Explicit reset");
            Assert(store.Load().Status==ZumaLoadStatus.Loaded,"Reset result");
            var archives=Directory.GetFiles(dir,"*.recovery-*").Select(File.ReadAllText).ToArray();
            Assert(archives.Contains("bad-main") && archives.Contains("bad-backup"),"Originals not archived"); }));
        Check("Future primary never falls back to old backup or resets", () => WithDirectory(dir => {
            var store=new ZumaSaveStore(dir); var future=FreshJson();future["rulesVersion"]=2;
            File.WriteAllText(store.SavePath,future.ToString());File.WriteAllText(store.SavePath+".bak",ZumaSaveCodec.Encode(ZumaSaveData.CreateNew()));
            var bytes=File.ReadAllBytes(store.SavePath);Assert(store.Load().Status==ZumaLoadStatus.UnsupportedVersion,"Future main");
            Assert(!store.Save(ZumaSaveData.CreateNew()).Success && !store.ResetCorrupt().Success,"Future write/reset");
            Assert(bytes.SequenceEqual(File.ReadAllBytes(store.SavePath)),"Future bytes changed"); }));
        Check("Future backup protected even alongside a valid primary", () => WithDirectory(dir => {
            var store=new ZumaSaveStore(dir);Assert(store.Save(ZumaSaveData.CreateNew()).Success,"Save");
            var future=FreshJson();future["contentVersion"]=2;File.WriteAllText(store.SavePath+".bak",future.ToString());
            var original=File.ReadAllBytes(store.SavePath);var backup=File.ReadAllBytes(store.SavePath+".bak");
            Assert(store.Load().Status==ZumaLoadStatus.Loaded,"Valid main must win");Assert(!store.Save(ZumaSaveData.CreateNew()).Success,"Future backup overwrite");
            Assert(original.SequenceEqual(File.ReadAllBytes(store.SavePath)) && backup.SequenceEqual(File.ReadAllBytes(store.SavePath+".bak")),"Version protected bytes changed"); }));
        Check("Corrupt main with future backup remains unsupported", () => WithDirectory(dir=> {
            var store=new ZumaSaveStore(dir);File.WriteAllText(store.SavePath,"bad");var future=FreshJson();future["schemaVersion"]=4;File.WriteAllText(store.SavePath+".bak",future.ToString());
            Assert(store.Load().Status==ZumaLoadStatus.UnsupportedVersion && !store.ResetCorrupt().Success,"Future backup protection"); }));
        Check("Actual read I/O failure does not fall back or overwrite", () => WithDirectory(dir=> {
            var store=new ZumaSaveStore(dir);Directory.CreateDirectory(store.SavePath);File.WriteAllText(store.SavePath+".bak",ZumaSaveCodec.Encode(ZumaSaveData.CreateNew()));
            Assert(store.Load().Status==ZumaLoadStatus.IoError,"Directory primary is I/O error");
            Assert(!store.Save(ZumaSaveData.CreateNew()).Success && !store.ResetCorrupt().Success,"I/O write protection"); }));
        Check("Actual temporary and backup target errors preserve committed primary", () => WithDirectory(dir=> {
            var store=new ZumaSaveStore(dir);Assert(store.Save(ZumaSaveData.CreateNew()).Success,"Fixture");var original=File.ReadAllBytes(store.SavePath);
            Directory.CreateDirectory(store.SavePath+".tmp");Assert(!store.Save(ZumaSaveData.CreateNew()).Success,"Temporary error");Directory.Delete(store.SavePath+".tmp");
            Directory.CreateDirectory(store.SavePath+".bak");Assert(!store.Save(ZumaSaveData.CreateNew()).Success,"Backup error");
            Assert(original.SequenceEqual(File.ReadAllBytes(store.SavePath)),"Commit damaged after error"); }));
        Check("Invalid UTF8 is corrupt, while unverifiable oversized files are protected", () => WithDirectory(dir=> {
            var store=new ZumaSaveStore(dir);File.WriteAllBytes(store.SavePath,new byte[]{0xc3,0x28});Assert(store.Load().Status==ZumaLoadStatus.Corrupt,"UTF8");
            File.WriteAllBytes(store.SavePath,new byte[ZumaSaveCodec.MaximumLength+1]);Assert(store.Load().Status==ZumaLoadStatus.UnsupportedVersion && store.IsWriteProtected,"Unverifiable byte bound"); }));
        Check("Orphan temporary file is ignored, all eleven legacy files untouched", () => WithDirectory(dir=> {
            var names=new[]{"xanaduyonder-v1.json","jump-v1.json","sheep-v1.json"}.SelectMany(n=>new[]{n,n+".bak",n+".tmp"}).Concat(new[]{"progress.bin","settings.txt"}).ToArray();
            foreach(var name in names)File.WriteAllText(Path.Combine(dir,name),name+" existing");
            var store=new ZumaSaveStore(dir);File.WriteAllText(store.SavePath+".tmp","uncommitted");Assert(store.Load().Status==ZumaLoadStatus.Missing,"Orphan promoted");
            Assert(store.Save(ZumaSaveData.CreateNew()).Success,"Save");foreach(var name in names)Assert(File.ReadAllText(Path.Combine(dir,name))==name+" existing","Legacy touched: "+name); }));
        Check("All causal snapshot fields round trip and replay future inputs exactly", FullSnapshot);
        Check("Every session/nested field rejects missing/null and strict types", SessionContracts);
        Check("Capacity maxima plus all profile records fit bounded UTF8 file", CapacityPayload);
        Check("Results commit once, acknowledge separately, survive failed disk retry", Results);
        Check("Invalid snapshots, invalid path bounds and forged result accounting rejected", InvalidSession);
        Check("Validation failures leave primary and backup byte-identical", () => WithDirectory(dir=> {
            var store=new ZumaSaveStore(dir);Assert(store.Save(ZumaSaveData.CreateNew()).Success,"Fixture");Assert(store.Save(ZumaSaveData.CreateNew()).Success,"Backup fixture");
            var main=File.ReadAllBytes(store.SavePath);var backup=File.ReadAllBytes(store.SavePath+".bak");var invalid=Played();invalid.Session.Frog.DirectionY=float.NaN;
            Assert(!store.Save(invalid).Success,"Invalid write accepted");Assert(main.SequenceEqual(File.ReadAllBytes(store.SavePath)) && backup.SequenceEqual(File.ReadAllBytes(store.SavePath+".bak")),"Validation touched bytes"); }));
        Check("Archive creation failure leaves corrupt originals unchanged", ArchiveFailure);
        Check("Actual macOS atomic replacement failure preserves primary and backup", ReplaceFailure);
        Check("Future-version depth extensions cannot fall back or be overwritten", () => WithDirectory(dir=> {
            var store=new ZumaSaveStore(dir);var nested=new string('[',20)+"0"+new string(']',20);
            foreach(var json in new[]{"{\"schemaVersion\":2,\"future\":"+nested+"}","{\"future\":"+nested+",\"schemaVersion\":2}"}) {
                File.WriteAllText(store.SavePath,json);File.WriteAllText(store.SavePath+".bak",ZumaSaveCodec.Encode(ZumaSaveData.CreateNew()));
                Assert(store.Load().Status==ZumaLoadStatus.UnsupportedVersion,"Future depth classified as corruption");
                Assert(!store.Save(ZumaSaveData.CreateNew()).Success && File.ReadAllText(store.SavePath)==json,"Future deep file overwritten"); } }));
        Check("Committed winning record cannot omit its positive completion time", () => {
            var data=ZumaSaveData.CreateNew();data.CommitResult(Won());var json=JObject.Parse(ZumaSaveCodec.Encode(data));json["profile"]["records"][0]["bestTimeSeconds"]=0;Reject(json.ToString());
        });
        Check("Cross-map retry survives disposing visual assets after a failed replacement", CrossMapCachedRetry);
        Check("Unavailable uncached level is I/O protected and cannot reset or escape Save", UnavailableLevel);
        Check("Oversized unknown-version primary cannot recover or overwrite from an old backup", OversizedVersionProtection);
        Check("Every root and session positive large version protects primary and backup", LargeVersionProtection);
        Check("Accepted winning time boundary always commits and saves without overflow", WinningTimeBoundary);
        Console.WriteLine("Zuma persistence checks: "+passed+" passed, "+failed+" failed.");return failed==0?0:1;
    }
    private static ZumaLevelDefinition Level()
    {
        var points=new ZumaPathPoint[12001];
        for(var i=0;i<points.Length;i++) points[i]=new ZumaPathPoint{X=100+i*.05f,Y=200};
        return new ZumaLevelDefinition{Id="spiral",DisplayName="Persistence fixture",FrogX=400,FrogY=500,Points=points,Coins=new[]{new ZumaPoint{X=900,Y=400}}};
    }
    private static ZumaSaveData Played()
    {
        var level=Level();var data=ZumaSaveData.CreateNew();var s=new ZumaGame(level,"rabbit",12345).Snapshot();
        s.Phase="play";s.InitialRoll=false;s.IntroInitialized=true;s.TotalTicks=9007199254740993L;s.PlayTicks=1234;s.Revision=9007199254740993L;
        s.Score=9007199254740993L;s.Generating=false;s.ChainBonus=4;s.MaxChainBonus=5;s.MaxCombo=2;s.TotalCombos=3;s.TotalCoins=4;
        s.Balls=new[]{new ZumaBallSnapshot{PathPosition=264,Color=0,GoBack=true,Speed=-1.25f,Combo=2},new ZumaBallSnapshot{PathPosition=232,Color=1,Inserted=true,IsSingle=true},new ZumaBallSnapshot{PathPosition=200,Color=2,Exploding=true,ExplosionTicks=12,FadeTicks=9}};
        s.Bullets=new[]{new ZumaBulletSnapshot{X=420,Y=450,DirectionY=-1,Color=3}};
        s.Frog.RecoilTicks=7;s.Frog.CurrentColor=2;s.Frog.NextColor=3;s.Coin.Active=true;s.Coin.Blinking=true;s.Coin.TimerTicks=123;s.Coin.FadeTicks=4;
        data.Session=ZumaGame.Restore(level,s).Snapshot();return data;
    }
    private static void FullSnapshot()
    {
        var level=Level();var source=Played();var json=ZumaSaveCodec.Encode(source,id=>level);
        var tokens=JObject.Parse(json);foreach(var name in new[]{"score","totalTicks","playTicks","revision"})Assert(tokens["session"][name].Type==JTokenType.String,"Long token "+name);
        var decoded=ZumaSaveCodec.Decode(json,id=>level);Assert(decoded.Status==ZumaLoadStatus.Loaded,decoded.Warning);
        Assert(ZumaSaveCodec.Encode(decoded.Data,id=>level)==json,"Full snapshot roundtrip differs");
        Assert(!ReferenceEquals(source.Session.Balls,decoded.Data.Session.Balls) && !ReferenceEquals(source.Session.Frog,decoded.Data.Session.Frog),"Aliased snapshot");
        var a=ZumaGame.Restore(level,source.Session);var b=ZumaGame.Restore(level,decoded.Data.Session);
        for(var tick=0;tick<180;tick++)
        {
            if(tick%17==0){a.Aim(600,100);b.Aim(600,100);a.TryFire();b.TryFire();}if(tick%29==0){a.Swap();b.Swap();}a.Tick();b.Tick();
            source.Session=a.Snapshot();decoded.Data.Session=b.Snapshot();Assert(ZumaSaveCodec.Encode(source,id=>level)==ZumaSaveCodec.Encode(decoded.Data,id=>level),"Resume divergence at "+tick);
        }
        // Each phase is persisted without turning pause into a rules phase or dropping causal trail particles.
        foreach(var phase in new[]{"intro","lossDrain","winOutro","won","lost"})
        {
            var state=Played();var s=state.Session;s.Phase=phase;s.ChainBonus=0;s.MaxChainBonus=5;s.Frog.RecoilTicks=0;
            if(phase=="intro"){s=new ZumaGame(level,"rabbit",7).Snapshot();s.IntroInitialized=true;s.TrailParticles=new[]{new ZumaTrailParticleSnapshot{X=123,Y=200,AgeTicks=12}};state.Session=s;}
            else if(phase=="lossDrain"){s.EndReached=true;s.ChainSpeed=32;}
            else {s.Balls=new ZumaBallSnapshot[0];s.Bullets=new ZumaBulletSnapshot[0];s.TrailParticles=new ZumaTrailParticleSnapshot[0];s.EndReached=phase=="lost";s.OutroInitialized=phase=="won";}
            if(phase=="winOutro"){s.OutroInitialized=true;s.OutroPosition=234;s.OutroPreviousPosition=222;s.LastClearedPosition=200;s.TrailParticles=new[]{new ZumaTrailParticleSnapshot{X=111,Y=200,AgeTicks=3}};}
            var serialized=ZumaSaveCodec.Encode(state,id=>level);var restored=ZumaSaveCodec.Decode(serialized,id=>level);
            Assert(restored.Status==ZumaLoadStatus.Loaded && ZumaSaveCodec.Encode(restored.Data,id=>level)==serialized,"Phase roundtrip "+phase+": "+restored.Warning);
        }
        WithDirectory(dir=>{var store=new ZumaSaveStore(dir,id=>level);Assert(store.Save(source).Success,"Snapshot disk save");Assert(ZumaSaveCodec.Encode(store.Load().Data,id=>level)==ZumaSaveCodec.Encode(source,id=>level),"Snapshot disk restore");});
    }
    private static void SessionContracts()
    {
        var source=JObject.Parse(ZumaSaveCodec.Encode(Played()));
        foreach(var path in new[]{"session","session.frog","session.coin","session.balls[0]","session.bullets[0]"})
        {
            var obj=(JObject)source.SelectToken(path);
            foreach(var property in obj.Properties())
            {
                var r=(JObject)source.DeepClone();((JObject)r.SelectToken(path)).Remove(property.Name);Reject(r.ToString());
                r=(JObject)source.DeepClone();r.SelectToken(path)[property.Name]=JValue.CreateNull();Reject(r.ToString());
            }
            var extra=(JObject)source.DeepClone();extra.SelectToken(path)["unknown"]=true;Reject(extra.ToString());
        }
        foreach(var path in new[]{"session.score","session.totalTicks","session.playTicks","session.revision"}){var r=(JObject)source.DeepClone();r.SelectToken(path).Replace(123);Reject(r.ToString());}
        foreach(var path in new[]{"session.randomState","session.frog.currentColor","session.coin.timerTicks","session.balls[0].explosionTicks"}){var r=(JObject)source.DeepClone();r.SelectToken(path).Replace("1");Reject(r.ToString());}
        foreach(var path in new[]{"session.chainSpeed","session.frog.directionX","session.balls[0].x","session.bullets[0].y"}){var r=(JObject)source.DeepClone();r.SelectToken(path).Replace("NaN");Reject(r.ToString());}
        var future=(JObject)source.DeepClone();future["session"]["rulesVersion"]=2;Assert(ZumaSaveCodec.Decode(future.ToString()).Status==ZumaLoadStatus.UnsupportedVersion,"Session future version");
    }
    private static void CapacityPayload()
    {
        var data=Played();var s=data.Session;s.Balls=Enumerable.Range(0,256).Select(i=>new ZumaBallSnapshot{PathPosition=9000-i*32,Color=i%4,IsSingle=i%2==0}).ToArray();
        s.Bullets=Enumerable.Range(0,32).Select(i=>new ZumaBulletSnapshot{X=400+i,Y=400,DirectionX=1,Color=i%4,Slot=i}).ToArray();
        s.TrailParticles=Enumerable.Range(0,256).Select(i=>new ZumaTrailParticleSnapshot{X=100+i,Y=200,AgeTicks=i%35}).ToArray();
        foreach(var record in data.Profile.Records){record.BestScore=long.MaxValue;record.BestTimeSeconds=int.MaxValue;}
        var json=ZumaSaveCodec.Encode(data);var size=Encoding.UTF8.GetByteCount(json);Assert(size<ZumaSaveCodec.MaximumLength,"Full-capacity size "+size);
        var result=ZumaSaveCodec.Decode(json);Assert(result.Status==ZumaLoadStatus.Loaded && result.Data.Session.Balls.Length==256 && result.Data.Session.Bullets.Length==32 && result.Data.Session.TrailParticles.Length==256,"Capacity truncated");
        Console.WriteLine("INFO Maximum-capacity fixture UTF8 bytes: "+size);
        foreach(var name in new[]{"balls","bullets","trailParticles"}){var root=JObject.Parse(json);var list=(JArray)root["session"][name];list.Add(list[0].DeepClone());Reject(root.ToString());}
    }
    private static ZumaSnapshot Won()
    {
        var s=Played().Session;s.Phase="won";s.Balls=new ZumaBallSnapshot[0];s.Bullets=new ZumaBulletSnapshot[0];s.TrailParticles=new ZumaTrailParticleSnapshot[0];s.OutroInitialized=true;s.PlayTicks=6000;s.TotalTicks=7000;s.Score=12345;return s;
    }
    private static void Results()
    {
        var data=ZumaSaveData.CreateNew();var result=Won();Assert(data.CommitResult(result),"First result commit");
        var record=data.GetRecord("spiral","rabbit");Assert(record.BestScore==12345 && record.BestTimeSeconds==100 && data.Session.ResultCommitted && !data.Session.ResultAcknowledged,"Result transaction");
        Assert(!result.ResultCommitted,"Input snapshot mutated");var committed=ZumaSaveCodec.Encode(data);
        Assert(!data.CommitResult(result) && ZumaSaveCodec.Encode(data)==committed,"Repeated commit changed state");
        WithDirectory(dir=>{var store=new ZumaSaveStore(dir);Directory.CreateDirectory(store.SavePath+".tmp");Assert(!store.Save(data).Success,"Disk failure fixture");
            Assert(!data.CommitResult(result),"Retry reapplied result");Directory.Delete(store.SavePath+".tmp");Assert(store.Save(data).Success,"Retry save");
            var loaded=store.Load().Data;Assert(!loaded.CommitResult(result),"Reload reapplied result");Assert(loaded.AcknowledgeResult() && !loaded.AcknowledgeResult(),"Ack idempotence");
            Assert(store.Save(loaded).Success && store.Load().Data.Session.ResultAcknowledged,"Ack not persisted");
            Assert(loaded.GetRecord("spiral","rabbit").BestScore==12345,"Ack changed score"); });
        var loss=Won();loss.SessionId=Guid.NewGuid().ToString("N");loss.Phase="lost";loss.EndReached=true;loss.Score=99999;
        Assert(data.CommitResult(loss),"Loss terminal commit");Assert(data.GetRecord("spiral","rabbit").BestScore==12345,"Loss improved record");
    }
    private static void InvalidSession()
    {
        Action<Action<ZumaSnapshot>> invalid=edit=>{var data=Played();edit(data.Session);var threw=false;try{ZumaSaveCodec.Encode(data);}catch(ArgumentException){threw=true;}Assert(threw,"Invalid state encoded");};
        invalid(s=>s.RandomState=0);invalid(s=>s.Frog.DirectionX=float.NaN);invalid(s=>s.Balls[0].PathPosition=float.PositiveInfinity);invalid(s=>s.Balls[0]=null);
        invalid(s=>s.Phase="paused");invalid(s=>s.ResultAcknowledged=true);invalid(s=>s.ResultCommitted=true);invalid(s=>s.PlayTicks=s.TotalTicks+1);
        var bounds=Played();bounds.Session.Balls[0].PathPosition=99000;var json=ZumaSaveCodec.Encode(bounds);
        Assert(ZumaSaveCodec.Decode(json,id=>Level()).Status==ZumaLoadStatus.Corrupt,"Map path bounds skipped");
        var forged=ZumaSaveData.CreateNew();forged.Session=Won();forged.Session.ResultCommitted=true;
        var threwInvalid=false;try{ZumaSaveCodec.Encode(forged);}catch(ArgumentException){threwInvalid=true;}Assert(threwInvalid,"Committed win without records accepted");
    }
    private static void ArchiveFailure()
    {
        WithDirectory(dir=>{
            if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) { Console.WriteLine("SKIP Unix permission fixture on this platform"); return; }
            var store=new ZumaSaveStore(dir);File.WriteAllText(store.SavePath,"corrupt-main");File.WriteAllText(store.SavePath+".bak","corrupt-backup");
            var mode=File.GetUnixFileMode(dir);File.SetUnixFileMode(dir,UnixFileMode.UserRead|UnixFileMode.UserExecute);
            try { Assert(!store.ResetCorrupt().Success,"Archive write failure not reported");Assert(File.ReadAllText(store.SavePath)=="corrupt-main" && File.ReadAllText(store.SavePath+".bak")=="corrupt-backup","Failed archive reset changed originals"); }
            finally { File.SetUnixFileMode(dir,mode); }
        });
    }
    private static void ReplaceFailure()
    {
        if(!OperatingSystem.IsMacOS()){Console.WriteLine("SKIP macOS immutable-file replacement fixture on this platform");return;}
        WithDirectory(dir=>{
            var store=new ZumaSaveStore(dir);Assert(store.Save(ZumaSaveData.CreateNew()).Success,"Fixture");Assert(store.Save(ZumaSaveData.CreateNew()).Success,"Backup fixture");
            var before=File.ReadAllBytes(store.SavePath);var backup=File.ReadAllBytes(store.SavePath+".bak");
            SetImmutable(store.SavePath,true);
            try {var changed=ZumaSaveData.CreateNew();changed.Profile.LastMapId="claw";Assert(!store.Save(changed).Success,"Immutable primary replacement should fail");
                Assert(before.SequenceEqual(File.ReadAllBytes(store.SavePath)),"Atomic replacement changed primary on failure");
                Assert(backup.SequenceEqual(File.ReadAllBytes(store.SavePath+".bak")),"Atomic replacement changed backup on failure");}
            finally {SetImmutable(store.SavePath,false);if(File.Exists(store.SavePath+".bak"))SetImmutable(store.SavePath+".bak",false);}
        });
    }
    private static void SetImmutable(string path,bool immutable)
    {
        using(var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo{
            FileName="/usr/bin/chflags",Arguments=(immutable?"uchg":"nouchg")+" "+path,UseShellExecute=false,RedirectStandardError=true}))
        {process.WaitForExit();Assert(process.ExitCode==0,"Cannot establish immutable fixture: "+process.StandardError.ReadToEnd());}
    }
    private static Func<string,ZumaLevelDefinition> CachedResolver(Func<string,ZumaLevelDefinition> loader)
    { return new ZumaLevelCache(loader).Get; }
    private static void CrossMapCachedRetry()
    {
        WithDirectory(dir=>{
            var a=Level();var b=Level();b.Id="claw";var callsA=0;var callsB=0;var visualLoaderOpen=true;
            var resolver=CachedResolver(id=>{if(!visualLoaderOpen)throw new InvalidOperationException("Visual assets disposed");
                if(id=="spiral"){callsA++;return a;}if(id=="claw"){callsB++;return b;}throw new InvalidOperationException("Unexpected map");});
            var store=new ZumaSaveStore(dir,resolver);var dataA=Played();Assert(store.Save(dataA).Success,"Cannot commit map A fixture");
            var original=File.ReadAllBytes(store.SavePath);var dataB=Played();dataB.Session.MapId="claw";dataB.Profile.LastMapId="claw";
            Directory.CreateDirectory(store.SavePath+".tmp");Assert(!store.Save(dataB).Success,"Replacement must fail with temporary directory blocker");
            Assert(original.SequenceEqual(File.ReadAllBytes(store.SavePath)),"Failed map B replacement changed map A");
            Assert(callsA==1 && callsB==1,"Both map definitions were not retained independently");
            visualLoaderOpen=false;Directory.Delete(store.SavePath+".tmp");
            var retry=store.Save(dataB);Assert(retry.Success,"Retry after visual disposal failed: "+retry.Error);
            Assert(store.Load().Data.Session.MapId=="claw","Map B was not committed after retry");
            Assert(ZumaSaveCodec.Decode(File.ReadAllText(store.SavePath+".bak"),resolver).Data.Session.MapId=="spiral","Map A backup was not retained");
            Assert(callsA==1 && callsB==1,"Retry invoked the disposed visual loader");
        });
    }
    private static void UnavailableLevel()
    {
        WithDirectory(dir=>{
            var data=Played();var json=ZumaSaveCodec.Encode(data);var path=Path.Combine(dir,"zuma-v1.json");
            File.WriteAllText(path,json);File.WriteAllText(path+".bak",ZumaSaveCodec.Encode(ZumaSaveData.CreateNew()));
            foreach(var failure in new Exception[]{new InvalidOperationException("Unavailable resource"),new FileNotFoundException("Missing level resource"),new ArgumentException("Invalid resource")})
            {
                var resolver=CachedResolver(id=>{throw failure;});var store=new ZumaSaveStore(dir,resolver);
                var loaded=store.Load();Assert(loaded.Status==ZumaLoadStatus.IoError && store.IsWriteProtected,"Unavailable level incorrectly classified as "+loaded.Status);
                Assert(!store.ResetCorrupt().Success,"Resource error permitted corrupt reset");var save=store.Save(data);Assert(!save.Success && !string.IsNullOrEmpty(save.Error),"Resource error escaped or was ignored by Save");
                Assert(File.ReadAllText(path)==json,"Unavailable content overwrote disk data");
                try{ZumaSaveCodec.Decode(json,resolver);throw new Exception("Codec swallowed resource IOException");}
                catch(IOException e){Assert(e.InnerException==failure,"Loader exception cause was lost");}
            }
        });
    }
    private static void OversizedVersionProtection()
    {
        WithDirectory(dir=>{
            var store=new ZumaSaveStore(dir);var future="{\"padding\":\""+new string('x',ZumaSaveCodec.MaximumLength)+"\",\"schemaVersion\":2}";
            File.WriteAllText(store.SavePath,future);File.WriteAllText(store.SavePath+".bak",ZumaSaveCodec.Encode(ZumaSaveData.CreateNew()));
            var primary=File.ReadAllBytes(store.SavePath);var backup=File.ReadAllBytes(store.SavePath+".bak");
            Assert(store.Load().Status==ZumaLoadStatus.UnsupportedVersion,"Unverifiable oversized primary was treated as recoverable corruption");
            Assert(store.IsWriteProtected && !store.Save(ZumaSaveData.CreateNew()).Success && !store.ResetCorrupt().Success,"Unverifiable oversized save was writable/resettable");
            Assert(primary.SequenceEqual(File.ReadAllBytes(store.SavePath)) && backup.SequenceEqual(File.ReadAllBytes(store.SavePath+".bak")),"Version-hidden oversized bytes changed");
            Assert(ZumaSaveCodec.Decode(future).Status==ZumaLoadStatus.UnsupportedVersion,"Direct codec has weaker oversized protection");
            File.WriteAllText(store.SavePath,ZumaSaveCodec.Encode(ZumaSaveData.CreateNew()));File.WriteAllText(store.SavePath+".bak",future);
            primary=File.ReadAllBytes(store.SavePath);backup=File.ReadAllBytes(store.SavePath+".bak");
            Assert(store.Load().Status==ZumaLoadStatus.Loaded && !store.Save(ZumaSaveData.CreateNew()).Success,"Unverifiable oversized backup may be replaced");
            Assert(primary.SequenceEqual(File.ReadAllBytes(store.SavePath)) && backup.SequenceEqual(File.ReadAllBytes(store.SavePath+".bak")),"Oversized backup protection changed bytes");
        });
    }
    private static void LargeVersionProtection()
    {
        foreach(var field in new[]{"schemaVersion","rulesVersion","contentVersion","session.rulesVersion","session.contentVersion"})
        foreach(var number in new[]{"2147483648","9223372036854775808","18446744073709551616"})
        {
            var data=ZumaSaveData.CreateNew();data.Session=Won();var root=JObject.Parse(ZumaSaveCodec.Encode(data));root.SelectToken(field).Replace(JToken.Parse(number));var future=root.ToString();
            Assert(ZumaSaveCodec.Decode(future).Status==ZumaLoadStatus.UnsupportedVersion,"Positive future version narrowed before recognition: "+field+"="+number);
            WithDirectory(dir=>{
                var store=new ZumaSaveStore(dir);var current=ZumaSaveCodec.Encode(ZumaSaveData.CreateNew());
                File.WriteAllText(store.SavePath,future);File.WriteAllText(store.SavePath+".bak",current);
                Assert(store.Load().Status==ZumaLoadStatus.UnsupportedVersion && !store.Save(data).Success && !store.ResetCorrupt().Success,"Future primary permits fallback/write/reset: "+field);
                Assert(File.ReadAllText(store.SavePath)==future && File.ReadAllText(store.SavePath+".bak")==current,"Future primary changed");
                File.WriteAllText(store.SavePath,current);File.WriteAllText(store.SavePath+".bak",future);
                Assert(store.Load().Status==ZumaLoadStatus.Loaded && !store.Save(data).Success,"Future backup overwritten: "+field);
                Assert(File.ReadAllText(store.SavePath)==current && File.ReadAllText(store.SavePath+".bak")==future,"Future backup changed");
                File.WriteAllText(store.SavePath,"bad");Assert(store.Load().Status==ZumaLoadStatus.UnsupportedVersion && !store.ResetCorrupt().Success,"Corrupt main bypassed future backup protection");
            });
        }
    }
    private static void WinningTimeBoundary()
    {
        var over=((long)int.MaxValue+1)*ZumaGame.TickRate;var valid=ZumaSaveData.CreateNew();valid.Session=Won();
        var root=JObject.Parse(ZumaSaveCodec.Encode(valid));root["session"]["playTicks"]=over.ToString(System.Globalization.CultureInfo.InvariantCulture);root["session"]["totalTicks"]=over.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var invalid=ZumaSaveCodec.Decode(root.ToString());Assert(invalid.Status==ZumaLoadStatus.Corrupt && invalid.Data==null,"Overflowing terminal time accepted by decode");
        var fresh=ZumaSaveData.CreateNew();var tooLong=Won();tooLong.PlayTicks=over;tooLong.TotalTicks=over;
        var rejected=false;try{fresh.CommitResult(tooLong);}catch(ArgumentException){rejected=true;}catch(OverflowException){throw new Exception("CommitResult narrowed unvalidated time");}
        Assert(rejected && fresh.Session==null && fresh.GetRecord("spiral","rabbit").BestScore==0,"Invalid result mutated profile or was accepted");
        var boundary=Won();boundary.PlayTicks=ZumaGame.MaximumPlayTicks;boundary.TotalTicks=ZumaGame.MaximumPlayTicks;
        valid.Session=boundary;var loaded=ZumaSaveCodec.Decode(ZumaSaveCodec.Encode(valid));Assert(loaded.Status==ZumaLoadStatus.Loaded,loaded.Warning);
        Assert(loaded.Data.CommitResult(loaded.Data.Session),"Largest accepted time cannot commit");
        Assert(loaded.Data.GetRecord("spiral","rabbit").BestTimeSeconds==int.MaxValue,"Time boundary changed score protocol");
        WithDirectory(dir=>{
            var store=new ZumaSaveStore(dir);File.WriteAllText(store.SavePath,root.ToString());Assert(store.Load().Status==ZumaLoadStatus.Corrupt,"Store restored time that cannot commit");
            Assert(store.ResetCorrupt().Success,"Cannot reset current-schema invalid time fixture");
            Directory.CreateDirectory(store.SavePath+".tmp");Assert(!store.Save(loaded.Data).Success,"Temporary failure fixture");Directory.Delete(store.SavePath+".tmp");
            Assert(store.Save(loaded.Data).Success,"Boundary result save failed");var restored=store.Load().Data;
            Assert(!restored.CommitResult(restored.Session) && restored.AcknowledgeResult(),"Boundary result replay was not idempotent");
            Assert(store.Save(restored).Success,"Boundary acknowledged save failed");
        });
    }
    private static JObject FreshJson(){return JObject.Parse(ZumaSaveCodec.Encode(ZumaSaveData.CreateNew()));}
    private static void RequiredFields(){var source=FreshJson();foreach(var path in new[]{"","profile","profile.records[0]"}){var obj=path==""?source:(JObject)source.SelectToken(path);foreach(var p in obj.Properties().ToArray()){var r=(JObject)source.DeepClone();var target=path==""?r:(JObject)r.SelectToken(path);target.Remove(p.Name);Reject(r.ToString());r=(JObject)source.DeepClone();target=path==""?r:(JObject)r.SelectToken(path);if(p.Name!="session"){target[p.Name]=JValue.CreateNull();Reject(r.ToString());}}}}
    private static void RejectEdit(Action<JObject> edit){var r=FreshJson();edit(r);Reject(r.ToString());}
    private static void Reject(string json){Assert(ZumaSaveCodec.Decode(json).Status==ZumaLoadStatus.Corrupt,"Accepted malformed: "+json.Substring(0,Math.Min(json.Length,120)));}
    private static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Check(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.Error.WriteLine("FAIL "+name+": "+e.Message);}}
    private static void WithDirectory(Action<string> action){var dir=Path.Combine(Path.GetTempPath(),"zuma-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);try{action(dir);}finally{Directory.Delete(dir,true);}}
}
