using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.GoldMiner;
using XanaduYonder.Runtime.GoldMiner;
class Program
{
    static string root;static int checks;
    static void Assert(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static GoldMinerLevelCatalog Catalog(){return new GoldMinerLevelCatalog(key=>File.ReadAllText(Path.Combine(root,"Assets/Resources",key+".json")));}
    static GoldMinerRunSnapshot Run(GoldMinerMode mode,GoldMinerLevelStart start,uint seed){return new GoldMinerRunSnapshot {RunId="11111111111111111111111111111111",BalanceProfile=GoldMinerBalanceProfile.Source,Mode=mode,RunSeed=seed,Current=start.CreateState(mode),Checkpoint=start.Copy()};}
    static GoldMinerRunSnapshot Shop(GoldMinerRunSnapshot run){var c=run.Current;foreach(var e in c.Entities){e.Status=GoldMinerEntityStatus.Collected;e.Source.Award=e.Source.Reward=="money"?GoldMinerSourceRules.Value(e,c.ActiveBuffs,run.Mode):0;e.Source.AwardPlayer=0;c.Players[0].TotalCollected+=e.Source.Award;c.Players[0].LevelCollected+=e.Source.Award;c.Cash+=e.Source.Award;}c.RemainingTicks=0;c.SimulationTick=GoldMinerSourceRules.DurationTicks(run.Mode);c.ActiveBuffs=new GoldMinerBuffs();c.Phase=GoldMinerPhase.Shop;c.Shop=GoldMinerEconomy.CreateShop(run);return run;}
    static GoldMinerRootSnapshot Root(GoldMinerRunSnapshot run){var value=new GoldMinerRootSnapshot {LastMode=run.Mode};if(run.Mode==GoldMinerMode.Solo)value.Solo=run;else value.Coop=run;if(run.Current.Phase==GoldMinerPhase.Shop){var record=value.GetRecord(run.Mode,run.BalanceProfile);record.BestClearedLevel=run.Current.LevelIndex;record.BestRunGrossIncome=GoldMinerValidationGross(run.Current);}return value;}
    static GoldMinerRootSnapshot RoundTrip(GoldMinerRootSnapshot source){string error;GoldMinerRootSnapshot copy;Assert(GoldMinerSaveCodec.TryDecode(GoldMinerSaveCodec.Encode(source),out copy,out error),error);return copy;}
    static void Main(string[] args)
    {
        root=args[0];string reason;
        var contract=JObject.Parse(File.ReadAllText(Path.Combine(root,"docs/gold-miner/source-contract.json")));
        for(int level=1;level<=40;level++){
            Assert(GoldMinerSourceRules.Target(GoldMinerMode.Solo,level)==(long)contract["goals"][level-1],"original JS goal");
            for(int item=0;item<5;item++){
                var range=contract["prices"][level-1][item];long min=(long)range["min"],max=(long)range["max"];
                Assert(GoldMinerSourceEconomy.ValidPrice(GoldMinerMode.Solo,level,(GoldMinerItem)item,min)&&GoldMinerSourceEconomy.ValidPrice(GoldMinerMode.Solo,level,(GoldMinerItem)item,max),"original JS price endpoints");
                Assert(!GoldMinerSourceEconomy.ValidPrice(GoldMinerMode.Solo,level,(GoldMinerItem)item,min-1)&&!GoldMinerSourceEconomy.ValidPrice(GoldMinerMode.Solo,level,(GoldMinerItem)item,max+1),"original JS price boundaries");
            }
        }
        var seen=new HashSet<string>();int objectCount=0;
        foreach(var mode in new[]{GoldMinerMode.Solo,GoldMinerMode.Coop}) {
            var data=JObject.Parse(File.ReadAllText(Path.Combine(root,"Assets/Resources/GoldMiner/Source",GoldMinerRules.ModeId(mode)+".json")));
            for(uint seed=1;seed<=30;seed++) {
                GoldMinerRunSnapshot previous=null;
                for(int level=1;level<=(mode==GoldMinerMode.Solo?40:30);level++) {
                    var start=Catalog().CreateSourceStart(mode,level,seed,previous);var run=Run(mode,start,seed);
                    Assert(start.TryValidate(mode,out reason),reason);Assert(run.TryValidate(out reason),reason);
                    Assert(start.CreateState(mode).RemainingTicks==(mode==GoldMinerMode.Solo?3720:2400),"duration");
                    Assert(start.CreateState(mode).Target==(mode==GoldMinerMode.Solo?5L*(27L*level*level+28L*level+75):500+150L*level),"target");
                    if(seen.Add(start.LayoutId)) {
                        var map=mode==GoldMinerMode.Solo?data["maps"][start.Source.MapIndex]["data"]:data["maps"][(start.Source.MapIndex+1).ToString()];
                        Assert(start.InitialEntities.Length==((JArray)map).Count,"map object count");
                        for(int i=0;i<start.InitialEntities.Length;i++) {
                            var e=start.InitialEntities[i];var raw=map[i];bool solo=mode==GoldMinerMode.Solo;
                            Assert(e.X==((double)raw["x"]+(solo?35:0))*.01,"exact source x");Assert(e.Y==((double)raw["y"]+(solo?4:-55))*.01,"exact source y");
                            Assert(e.Source.Key==(string)raw[solo?"name":"key"],"source identity");
                            if(!solo||e.Source.Type!=12){var type=data["types"][e.Source.Key];Assert(e.Source.Value==(long)type[solo?"price":"prices"]&&e.Source.Weight==(double)type["weight"],"exact source value and weight");}
                            if(solo){Assert(e.Source.ScaleX==(double)raw["scale"]["x"]&&e.Source.ScaleY==(double)raw["scale"]["y"]&&e.Source.Flip==(bool)raw["flip"],"source scale/flip");}
                            objectCount++;
                        }
                    }
                    var copy=RoundTrip(Root(run)).GetRun(mode);Assert(copy.Checkpoint.LayoutId==start.LayoutId,"checkpoint roundtrip");
                    previous=Shop(run);Assert(previous.TryValidate(out reason),reason);
                }
            }
        }
        Assert(seen.Count==60&&objectCount==1205,"all 60 maps and 1205 objects");
        Console.WriteLine("PASS all 60 maps / 1205 exact coordinates, types, transforms, 40 solo levels, 30 duo levels and save checkpoints");
        foreach(var mode in new[]{GoldMinerMode.Solo,GoldMinerMode.Coop}) {
            var run=Run(mode,Catalog().CreateSourceStart(mode,1,7),7);var game=new GoldMinerGame(run);Assert(game.StartLevel(),"start");long seq=0;
            for(int tick=0;tick<GoldMinerSourceRules.DurationTicks(mode);tick++) {
                if(tick%70==0)for(int p=0;p<GoldMinerRules.PlayerCount(mode);p++)game.TryLaunch(p,++seq);
                var step=game.Step();var state=game.GetSnapshot();Assert(state.TryValidate(out reason),reason+" tick "+tick);
                if(tick%29==0){var restored=new GoldMinerGame(RoundTrip(Root(state)).GetRun(mode));var a=game.Step();var b=restored.Step();Assert(GoldMinerSaveCodec.Encode(Root(game.GetSnapshot()))==GoldMinerSaveCodec.Encode(Root(restored.GetSnapshot())),"inflight deterministic replay");tick++;}
                if(step.SettlementReady)break;
            }
            Assert(game.SettlementReady&&game.GetStateSnapshot().SimulationTick==GoldMinerSourceRules.DurationTicks(mode),"exact deadline no drain");
        }
        Console.WriteLine("PASS source simulation validates every tick and exact restored in-flight continuation in both modes");
        foreach(var mode in new[]{GoldMinerMode.Solo,GoldMinerMode.Coop}) {
            var original=Run(mode,Catalog().CreateSourceStart(mode,1,7),7);
            var orphan=original.Copy();orphan.Current.Phase=GoldMinerPhase.Running;orphan.Current.Hooks[0].Source.RewardTicks=1;
            Assert(!orphan.TryValidate(out reason),"orphan delayed reward rejected before game construction");
            var wrongMode=original.Copy();wrongMode.Current.Hooks[0].Source.BombTicks=mode==GoldMinerMode.Solo?1:0;wrongMode.Current.Hooks[0].Source.ResetTicks=mode==GoldMinerMode.Coop?1:0;
            Assert(!wrongMode.TryValidate(out reason),"invalid delayed bomb/reset state rejected");
            var invalidBag=original.Copy();for(int index=0;index<invalidBag.Current.Entities.Length;index++)if(invalidBag.Current.Entities[index].Source.Type==12){invalidBag.Current.Entities[index].Source.Weight=95;invalidBag.Checkpoint.InitialEntities[index].Source.Weight=95;break;}
            Assert(!invalidBag.TryValidate(out reason),"both copies cannot forge original bag outcomes");
            var altered=original.Copy();altered.Checkpoint.InitialEntities[0].Source.InitialX+=.01;altered.Checkpoint.InitialEntities[0].X+=.01;altered.Current.Entities[0].Source.InitialX+=.01;altered.Current.Entities[0].X+=.01;
            Assert(!altered.TryValidate(out reason),"both snapshot copies cannot forge a source position");
            var state=original.Current;state.Phase=GoldMinerPhase.Running;state.RemainingTicks=1;state.SimulationTick=GoldMinerSourceRules.DurationTicks(mode)-1;
            var ore=state.Entities[0];var hook=state.Hooks[0];ore.Status=GoldMinerEntityStatus.Held;ore.OwnerPlayerId=0;hook.State=GoldMinerHookState.RetractingLoaded;hook.HeldEntityId=ore.Id;
            hook.Source.Y=0;hook.Source.X=GoldMinerSourceRules.OriginX(mode,0);
            if(mode==GoldMinerMode.Solo){hook.Source.RewardTicks=1;hook.Source.PendingEntityId=ore.Id;}
            else{hook.Source.BombTicks=1;state.Source.SharedBombs=0;}
            var game=new GoldMinerGame(RoundTrip(Root(original)).GetRun(mode));game.Step();Assert(game.SettlementReady&&game.GetStateSnapshot().Cash==0,"original timeout does not grant unfinished reward");
        }
        Console.WriteLine("PASS immutable source identity, pending money/bomb save and immediate timeout");
        GoldMinerRunSnapshot edgePrior=null, edgeRun=null;
        for(int level=1;level<=14;level++){edgeRun=Run(GoldMinerMode.Coop,Catalog().CreateSourceStart(GoldMinerMode.Coop,level,31,edgePrior),31);if(level<14)edgePrior=Shop(edgeRun);}
        edgeRun.Current.Phase=GoldMinerPhase.Running;
        foreach(var e in edgeRun.Current.Entities)if(e.Source.Key!="gold3"||Math.Abs(e.X-5.66)>.001)e.Status=GoldMinerEntityStatus.Destroyed;
        edgeRun.Current.Hooks[0].AngleRadians=56*Math.PI/180;
        var edgeGame=new GoldMinerGame(edgeRun);Assert(edgeGame.TryLaunch(0,1),"edge launch");
        for(int tick=0;tick<160;tick++){edgeGame.Step();Assert(edgeGame.GetSnapshot().TryValidate(out reason),"source edge return "+tick+": "+reason);}
        var edgeHeld=edgeRun.Copy();var edgeHook=edgeHeld.Current.Hooks[0];GoldMinerEntity edgeOre=null;
        foreach(var e in edgeHeld.Current.Entities)if(e.Source.Key=="gold3"&&Math.Abs(e.X-5.66)<.001)edgeOre=e;
        edgeHook.Source.TravelDirection=-1;edgeHook.State=GoldMinerHookState.RetractingLoaded;edgeHook.HeldEntityId=edgeOre.Id;edgeHook.Source.X=5.12;edgeHook.Source.Y=(5.12-1.85)/Math.Tan(edgeHook.AngleRadians);
        edgeOre.Status=GoldMinerEntityStatus.Held;edgeOre.OwnerPlayerId=0;GoldMinerSourceRules.HookPoint(GoldMinerMode.Coop,edgeHook,out edgeOre.X,out edgeOre.Y);
        Assert(edgeHeld.TryValidate(out reason),"edge held fixture must be accepted: "+reason);
        var heldEdgeGame=new GoldMinerGame(edgeHeld);heldEdgeGame.Step();Assert(heldEdgeGame.GetSnapshot().TryValidate(out reason),"accepted boundary held state must stay valid: "+reason);
        Console.WriteLine("PASS duo source boundary preserves held-object state");
        GoldMinerRunSnapshot tntPrior=null, tntRun=null;
        for(int level=1;level<=7;level++){tntRun=Run(GoldMinerMode.Solo,Catalog().CreateSourceStart(GoldMinerMode.Solo,level,11,tntPrior),11);if(level<7)tntPrior=Shop(tntRun);}
        var tntState=tntRun.Current;tntState.Phase=GoldMinerPhase.Running;GoldMinerEntity tnt=null;
        foreach(var e in tntState.Entities)if(e.Source.Type==9){tnt=e;break;}
        var tntHook=tntState.Hooks[0];double tdx=tnt.X-3.2,tdy=tnt.Y,tdistance=Math.Sqrt(tdx*tdx+tdy*tdy);
        tntHook.AngleRadians=Math.Atan2(tdx,tdy);tntHook.Source.X=3.2+Math.Sin(tntHook.AngleRadians)*(tdistance-.4);tntHook.Source.Y=Math.Cos(tntHook.AngleRadians)*(tdistance-.4);tntHook.State=GoldMinerHookState.Extending;
        var tntGame=new GoldMinerGame(tntRun);tntGame.Step();var exploded=tntGame.GetStateSnapshot();
        foreach(var e in tntState.Entities)if(e.Id!=tnt.Id&&e.Source.MotionEndX==e.Source.InitialX){double dx=e.X-tnt.X,dy=e.Y-tnt.Y;Assert((exploded.Entities[e.Id-1].Status==GoldMinerEntityStatus.Destroyed)==(dx*dx+dy*dy<=1),"original TNT 100px static-neighbor radius");}
        for(int tick=0;tick<180;tick++)tntGame.Step();var tntDone=tntGame.GetStateSnapshot();Assert(tntDone.Entities[tnt.Id-1].Status==GoldMinerEntityStatus.Collected&&tntDone.Cash==tntState.Cash+1,"original TNT returns one dollar after its explosion");
        Console.WriteLine("PASS solo original TNT radius and one-dollar returned reward");
        for(uint seed=1;seed<100;seed++) {
            var prior=Shop(Run(GoldMinerMode.Coop,Catalog().CreateSourceStart(GoldMinerMode.Coop,1,seed),seed));
            if(prior.Current.Shop.Offers[0].Remaining==0)continue;
            GoldMinerEconomy.Purchase(prior.Current,GoldMinerItem.Dynamite,0);
            var bombRun=Run(GoldMinerMode.Coop,Catalog().CreateSourceStart(GoldMinerMode.Coop,2,seed,prior),seed);var bs=bombRun.Current;bs.Phase=GoldMinerPhase.Running;
            GoldMinerEntity big=null;foreach(var e in bs.Entities)if(e.Source.Type==5){big=e;break;}
            var bh=bs.Hooks[0];double dx=big.X-1.85,dy=big.Y-.05,distance=Math.Sqrt(dx*dx+dy*dy);bh.AngleRadians=Math.Atan2(dx,dy);bh.Source.X=1.85+Math.Sin(bh.AngleRadians)*(distance-.25);bh.Source.Y=Math.Cos(bh.AngleRadians)*(distance-.25);bh.State=GoldMinerHookState.RetractingLoaded;bh.HeldEntityId=big.Id;bh.Source.TravelDirection=-1;big.Status=GoldMinerEntityStatus.Held;big.OwnerPlayerId=0;
            var bombGame=new GoldMinerGame(bombRun);Assert(bombGame.TryUseDynamite(0,1),"source shared bomb reservation");Assert(bombGame.GetStateSnapshot().Source.SharedBombs==0,"shared stock reserved synchronously");
            bombGame=new GoldMinerGame(RoundTrip(Root(bombGame.GetSnapshot())).Coop);
            for(int tick=0;tick<29;tick++)bombGame.Step();Assert(bombGame.GetStateSnapshot().Entities[big.Id-1].Status==GoldMinerEntityStatus.Held,"source bomb waits original 500ms");
            bombGame.Step();Assert(bombGame.GetStateSnapshot().Entities[big.Id-1].Status==GoldMinerEntityStatus.Destroyed,"source bomb detonates at 30 ticks after resume");Assert(bombGame.GetSnapshot().TryValidate(out reason),reason);break;
        }
        Console.WriteLine("PASS duo synchronous shared reservation and saved 500ms bomb delay");
        GoldMinerRunSnapshot soloRun=null;
        for(uint seed=1;seed<2000;seed++) {
            var candidate=Run(GoldMinerMode.Solo,Catalog().CreateSourceStart(GoldMinerMode.Solo,1,seed),seed);
            foreach(int kind in new[]{5,3,2}) foreach(var ore in candidate.Current.Entities) if(ore.Source.Type==kind&&ore.Status==GoldMinerEntityStatus.Available) {
                ore.Status=GoldMinerEntityStatus.Collected;ore.Source.Award=ore.Source.Value;ore.Source.AwardPlayer=0;
                candidate.Current.Cash+=ore.Source.Award;candidate.Current.Players[0].TotalCollected+=ore.Source.Award;candidate.Current.Players[0].LevelCollected+=ore.Source.Award;break;
            }
            candidate.Current.RemainingTicks=0;candidate.Current.SimulationTick=3720;candidate.Current.Phase=GoldMinerPhase.Shop;candidate.Current.Shop=GoldMinerEconomy.CreateShop(candidate);
            foreach(var offer in candidate.Current.Shop.Offers)if(offer.Remaining==1)GoldMinerEconomy.Purchase(candidate.Current,offer.ItemId,0);
            if(candidate.Current.Cash<0){soloRun=candidate;break;}
        }
        Assert(soloRun!=null,"source shop can incur debt from an actually cleared 650 target");RoundTrip(Root(soloRun));
        foreach(var mode in new[]{GoldMinerMode.Solo,GoldMinerMode.Coop})for(uint seed=1;seed<50;seed++) {
            var candidate=Shop(Run(mode,Catalog().CreateSourceStart(mode,1,seed),seed));
            if(candidate.Current.Shop.Offers[1].Remaining==0||candidate.Current.Shop.Offers[0].Remaining==0)continue;
            GoldMinerEconomy.Purchase(candidate.Current,GoldMinerItem.Strength,0);GoldMinerEconomy.Purchase(candidate.Current,GoldMinerItem.Dynamite,0);
            Assert(candidate.TryValidate(out reason),reason);
            var buff=candidate.Copy();buff.Current.PendingBuffs.Strength=false;Assert(!buff.TryValidate(out reason),"purchased source strength cannot disappear");
            var bomb=candidate.Copy();if(mode==GoldMinerMode.Solo)bomb.Current.Players[0].Bombs--;else bomb.Current.Source.SharedBombs--;
            Assert(!bomb.TryValidate(out reason),"purchased source bombs cannot disappear");
            var allocation=candidate.Copy();allocation.Current.Shop.Offers[0].Allocations[0].Count=0;Assert(!allocation.TryValidate(out reason),"source allocation must match purchase");
            break;
        }
        Console.WriteLine("PASS source solo debt roundtrip; total assertions "+checks);
    }
    static long GoldMinerValidationGross(GoldMinerState s){long total=0;foreach(var p in s.Players)total+=p.TotalCollected;return total;}
}
