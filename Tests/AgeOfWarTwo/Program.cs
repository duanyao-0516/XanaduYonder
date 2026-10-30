using System;
using System.Linq;
using System.Xml.Linq;
using XanaduYonder.Core.AgeOfWar;
using XanaduYonder.Core.AgeOfWar.Two;
internal static class Program
{
    static int assertions;
    static void Check(bool value,string message) { assertions++; if(!value) throw new Exception(message); }
    static void Equal(double expected,double actual,string message) { Check(Math.Abs(expected-actual)<.0000001,message+": expected "+expected+", got "+actual); }
    static AgeOfWarTwoBattle New(uint seed=1) { return new AgeOfWarTwoBattle(1,seed); }
    static void Ticks(AgeOfWarTwoBattle b,int n) { for(int i=0;i<n;i++) b.Tick(); }
    static AgeOfWarCommand C(AgeOfWarAction a,int id=0,int slot=0) { return new AgeOfWarCommand(a,id,slot); }
    static int Main()
    {
        try
        {
            OriginalWindowFade(); DisplayAlphaAndRotation(); OriginalStageSteps(); DisplayObjectTwips(); CatalogAndHud(); QueueAndClock(); EvolutionAndUpgrades(); CombatAndAnimation(); ProjectileCases(); SpecialCases(); AiAndWholeGames(); PresentationTransforms(); TurretEscrowAndErrors(); OriginalTooltips(); DamageDisplayAndFrameRate(); OriginalExposure();
            Console.WriteLine("PASS Age of War 2: "+assertions+" source-backed assertions"); return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine("FAIL "+ex); return 1; }
    }
    static void OriginalWindowFade()
    {
        var type=typeof(AgeOfWarTwoBattle).Assembly.GetType("XanaduYonder.Core.AgeOfWar.Two.AgeOfWarTwoWindowFade");Check(type!=null,"Original persistent tooltip opacity state exists");
        dynamic first=Activator.CreateInstance(type),second=Activator.CreateInstance(type);first.Visible=true;first.Advance(.017);Equal(17/256.0,(double)first.Alpha,"Window opacity increments4*elapsed through fixed256th source setter");
        first.Visible=false;second.Visible=true;first.Advance(.01);second.Advance(.01);Check((double)first.Alpha>0&&(double)second.Alpha>0,"Old and new source windows coexist during cross fade");
        first.Alpha=.99;first.Visible=true;first.Advance(.02);Check((double)first.Alpha>1,"Source window alpha may overshoot1 until its following render");first.Advance(.02);Equal(1,(double)first.Alpha,"Next source window render normalizes overshoot");
    }
    static void DisplayAlphaAndRotation()
    {
        var b=New();var unit=b.CreateUnit(2,1);unit.HealthAlpha=2;Equal(2,unit.HealthAlpha,"AS3 alpha preserves values above1");
        unit.HealthAlpha=.9999;Equal(255/256.0,unit.HealthAlpha,"AS3 health alpha setter stores integer256ths");
        unit.Render(.017);Equal(246/256.0,unit.HealthAlpha,"Health fade quantizes each DisplayObject assignment");
        unit.HealthAlpha=-.1;Equal(-25/256.0,unit.HealthAlpha,"AS3 negative alpha truncates toward zero without clamping");
        var e=new AgeOfWarTwoBattle.Effect(b,0,0,10,false);e.Render(.017);Equal(253/256.0,e.Alpha,"Damage-number alpha uses source256th setter");
        var p=new AgeOfWarTwoBattle.Projectile(b,"p1",1,0,-30,0,0,10,10,"Support");p.Rotation=181;Equal(-179,p.Rotation,"AS3 rotation normalizes above180");p.Rotation=-181;Equal(179,p.Rotation,"AS3 rotation normalizes below-180");p.Rotation=180;Equal(180,p.Rotation,"Positive180 boundary stays positive");p.Rotation=-180;Equal(-180,p.Rotation,"Negative180 boundary stays negative");
        e.Rotation=720;Equal(0,e.Rotation,"Special display rotation normalizes full revolutions");var t=b.CreateTurret(1,1,1);t.Rotation=-181;Equal(179,t.Rotation,"Cannon property uses same original rotation getter");
    }
    static void OriginalStageSteps()
    {
        var method=typeof(AgeOfWarTwoBattle).GetMethod("Step");Check(method!=null,"Actual ENTER_FRAME elapsed API exists");
        var b=New();Action<double> phase=null;method.Invoke(b,new object[]{.017,phase});Equal(59.983,b.SpecialTimer,"One original17ms stage event advances17ms");Equal(1,b.NativeTick,"One actual stage event advances native clips once");
        b.SetFrameRate(25);method.Invoke(b,new object[]{.017,phase});Equal(59.966,b.SpecialTimer,"Requested FPS does not replace actual elapsed delta");
        b.Execute(C(AgeOfWarAction.Speed));method.Invoke(b,new object[]{.017,phase});Equal(59.932,b.SpecialTimer,"2x applies once to actual elapsed seconds");
        b.Execute(C(AgeOfWarAction.Pause));method.Invoke(b,new object[]{.7,phase});Equal(59.932,b.SpecialTimer,"Pause freezes Moon and gameplay elapsed");Equal(4,b.NativeTick,"Paused native clips still receive each actual stage event");
        b=New();var enemy=b.CreateUnit(2,2);enemy.X=0;enemy.Wait=1000;b.Projectiles.Add(new AgeOfWarTwoBattle.Projectile(b,"p1",1,0,-30,180,0,10,10,"Support"));
        double atCamera=0,scaled=0;phase=dt=>{atCamera=enemy.Health;scaled=dt;};method.Invoke(b,new object[]{.017,phase});Equal(140,atCamera,"Camera phase runs after units before projectile damage");Equal(130,enemy.Health,"Projectile phase follows camera callback");Equal(.017,scaled,"Camera receives same scaled elapsed as other managers");
    }
    static void DisplayObjectTwips()
    {
        var b=New();var unit=b.CreateUnit(2,1);
        double[] inputs={.01,.024,.025,.049,.099,-.099,150.1+.7,151.45+.7};
        double[] expected={0,0,0,0,.05,-.05,150.75,152.1};
        for(int i=0;i<inputs.Length;i++){unit.X=inputs[i];Equal(expected[i],unit.X,"AS3 original Sprite.x writeback probe "+i);}
        var p=new AgeOfWarTwoBattle.Projectile(b,"p1",1,.099,-.099,0,0,10,10,"Support");Equal(.05,p.X,"Projectile constructor quantizes DisplayObject.x");Equal(-.05,p.Y,"Projectile constructor quantizes DisplayObject.y");
        p.XSpeed=.001;p.YSpeed=.001;p.Render(.017);Equal(0,p.X,"Projectile position truncates after += assignment");Equal(0,p.Y,"Negative projectile position truncates towards zero");Equal(.001,p.XSpeed,"Ordinary Number velocity is not quantized");
        var e=b.CreateStrike(4,1,.099,-.099);Equal(.05,e.X,"Special strike x uses original display setter");Equal(-.05,e.Y,"Special strike y uses original display setter");
        var text=new AgeOfWarTwoBattle.Effect(b,.099,-.099,20,false);Equal(.05,text.X,"Damage number display x is quantized");text.Render(.017);Equal(Math.Truncate((-.05-(2-.017)*.017*20)*20)/20,text.Y,"Damage number y quantizes after source timer update");
    }
    static void CatalogAndHud()
    {
        var b=New(); var f=b.Capture();
        Equal(2500,f.Gold,"XML initial money"); Equal(1000,f.Experience,"XML initial XP"); Equal(1000,f.PlayerHealth,"XML base health");
        Check(f.Units.Length==4&&f.Turrets.Length==3&&f.Upgrades.Length==12,"First-age HUD counts");
        Check(b.Data.Element("units").Elements().Count()==29&&b.Data.Element("turrets").Elements().Count()==17&&b.Data.Element("projectiles").Elements().Count()==22,"Complete source catalogs");
        Check(b.Data.Element("upgrades").Elements().Count()==18&&b.Data.Element("upgrades").Elements().Elements().Count()==58,"18 upgrade types and 58 levels");
        Check(b.Data.Element("specials").Elements().Count()==7&&b.Data.Element("bases").Elements().Count()==7,"All eras and skills");
        Check(f.Units[3].LockedAppearance&&f.Units[3].Requirement=="Dino Hut","Source locked presentation and requirement");
        Equal(2000,f.SpecialExperienceCost,"Special XP offer");
        for(int i=1;i<=29;i++) { var timeline=SourceTimelines.All["u"+i]; Check(timeline.Labels.ContainsKey("idle")&&timeline.Labels.ContainsKey("die")&&timeline.Labels.ContainsKey("attackMelee"),"Exact animation labels for u"+i); }
        for(int i=1;i<=17;i++) { var timeline=SourceTimelines.All["t"+i]; Check(timeline.Labels.ContainsKey("shoot")&&timeline.Labels.ContainsKey("shootLaunch"),"Turret firing labels "+i); }
        for(int age=1;age<=7;age++) { b.Age=age; f=b.Capture(); Check(f.Units.Length==(age==7?6:4),"Age-specific offers "+age); Check(f.Upgrades.All(u=>u.Id==age||u.Id>7),"One age-specific unlock plus eleven shared upgrades"); }
        b.Age=4; Check(b.Capture().Units.Single(u=>u.Id==15).LockedAppearance,"Mage appears locked");
        Check(b.Execute(C(AgeOfWarAction.Train,15)),"Original unavailableUnits reset permits mage despite visible lock");
        b.Age=7; Check(b.Capture().Units[0].Id==24,"Original future HUD retains Tank");
    }
    static void QueueAndClock()
    {
        var b=New(); Check(!b.Execute(C(AgeOfWarAction.Train,4)),"Dino lock"); Check(b.Execute(C(AgeOfWarAction.Upgrade,1)),"Buy Dino Hut"); Check(b.Execute(C(AgeOfWarAction.Train,4)),"Train unlocked dinosaur");
        Equal(1600,b.Gold,"Pay at enqueue"); Equal(2,b.Population,"Reserve population at enqueue");
        var q=New(); for(int i=0;i<5;i++) Check(q.Execute(C(AgeOfWarAction.Train,2)),"Five waiting same-type entries allowed"); Check(!q.Execute(C(AgeOfWarAction.Train,2)),"Sixth waiting entry refused (source >4)");
        q.Tick(); Check(q.Queue.Count==4&&q.Building==2,"HUD dequeues before construction advances"); Check(q.Execute(C(AgeOfWarAction.Train,2)),"Building unit excluded from waiting queue cap");
        var a=New(); a.Execute(C(AgeOfWarAction.Train,2)); Ticks(a,120); Check(a.Units.Count==0,"Training doesn't spawn in decrement branch");
        for(int i=0;i<3&&a.Units.Count==0;i++) a.Tick(); Check(a.Units.Count==1,"Training completion spawns through HUD");
        Check(a.Units[0].Animation.Current>0&&a.Units[0].Wait<.2,"New player unit receives animations and unit update in creation tick");
        var before=a.Capture(); a.Execute(C(AgeOfWarAction.Pause)); Ticks(a,500); Equal(before.Tick,a.Capture().Tick,"Pause freezes simulation"); Equal(before.SpecialProgress,a.Capture().SpecialProgress,"Pause freezes cooldown"); a.Execute(C(AgeOfWarAction.Pause));
        var normal=New(20); var fast=New(20); fast.Execute(C(AgeOfWarAction.Speed)); normal.Tick(); fast.Tick(); Equal((60-normal.SpecialTimer)*2,60-fast.SpecialTimer,"2x scales elapsed seconds");
        var pop=New(); pop.Gold=100000; for(int i=0;i<10;i++) Check(pop.Execute(C(AgeOfWarAction.Train,i%2+1)),"Reserve max population"); Check(!pop.Execute(C(AgeOfWarAction.Train,3)),"Population cap10");
        var cooldown=New(); cooldown.SpecialTimer=.001; cooldown.Tick(); Check(!cooldown.Capture().SpecialReady,"Negative cooldown is not ready until next HUD normalization"); cooldown.Tick(); Check(cooldown.Capture().SpecialReady,"Cooldown normalized exactly to zero");
    }
    static void EvolutionAndUpgrades()
    {
        var b=New(); b.PlayerHealth=700; b.Experience=7000; Check(b.Execute(C(AgeOfWarAction.Evolve)),"Paid evolution"); Equal(1200,b.PlayerHealth,"Evolution adds only maximum-health delta"); Equal(0,b.Experience,"Evolution spends XP");
        b=New(); b.Gold=100000; var old=b.CreateUnit(2,1); Check(b.Execute(C(AgeOfWarAction.Upgrade,12)),"Infantry armor purchase"); var fresh=b.CreateUnit(2,1); Equal(140,old.Health,"Existing unit armor unchanged"); Equal(168,fresh.Health,"New unit armor sampled at spawn");
        foreach(double difficulty in new[]{.6,1.0,1.5,2.0}) { var d=new AgeOfWarTwoBattle(difficulty,1); Equal(AgeOfWarTwoBattle.Round(140*difficulty),d.CreateUnit(2,2).Health,"CPU-only difficulty health"); Equal(25,d.Damage("Infantry","base",25,25,2),"Difficulty doesn't multiply damage"); }
        string[] types={"Support","Infantry","Anti-Armor","Armored"}; string[] targets={"Anti-Armor","Support","Armored","Infantry"};
        for(int i=0;i<4;i++) { Equal(38,b.Damage(types[i],targets[i],25,25,2),"Counter bonus ceil after rounding"); Equal(25,b.Damage(types[i],"base",25,25,2),"No base counter bonus"); }
        b=New(); b.Gold=100000; b.Execute(C(AgeOfWarAction.Upgrade,8)); var enemy=b.CreateUnit(2,2); Equal(130,enemy.Reward,"Income sampled from victim side (enemy has no player upgrade)"); var player=b.CreateUnit(2,1); Equal(143,player.Reward,"Player income affects own casualty value");
        int gold=b.Gold,xp=b.Experience; enemy.Health=0; enemy.Render(0); Equal(gold+130,b.Gold,"Enemy kill reward"); Equal(xp+130,b.Experience,"Enemy kill XP"); enemy.Render(0); Equal(gold+130,b.Gold,"Rewards only once");
        player.Health=0; player.Render(0); Equal(xp+130+72,b.Experience,"Own death grants rounded half reward");
        b=New(); b.Gold=100000; b.Execute(C(AgeOfWarAction.Upgrade,18)); b.Execute(C(AgeOfWarAction.Upgrade,17)); var turret=b.CreateTurret(1,1,1); Equal(400,turret.Range,"Turrets Range XML vs Turret Range lookup mismatch preserved"); Equal(8,b.Damage("Turret","Infantry",8,8,1),"Turrets Damage has no DamageManager case");
        b=New(); Check(b.Execute(C(AgeOfWarAction.BuyTurretSlot)),"First tower 250"); Check(b.Execute(C(AgeOfWarAction.BuyTurret,1)),"Buy first turret"); Check(b.Execute(C(AgeOfWarAction.PlaceTurret,0,1)),"Build first turret"); Equal(1500,b.Gold,"Tower plus turret costs"); Check(b.Execute(C(AgeOfWarAction.SellTurret,0,1)),"Sell slot turret"); Equal(1875,b.Gold,"Half-cost resale");
        b=New(); b.Experience=2000; b.SpecialTimer=0; Check(b.Execute(C(AgeOfWarAction.Special)),"Ready and affordable special"); Equal(0,b.Experience,"Special spends XP"); Equal(60,b.SpecialTimer,"Special cooldown resets");
    }
    static void CombatAndAnimation()
    {
        var b=New(); var player=b.CreateUnit(2,1); var enemy=b.CreateUnit(2,2); player.X=-25; enemy.X=25; player.Wait=enemy.Wait=0;
        player.Render(0); Equal(140,enemy.Health,"Starting attack causes no immediate damage");
        player.Animation.Render(.699); Equal(140,enemy.Health,"Damage waits for complete 21-frame windup");
        player.Animation.Render(.001); Check(enemy.Health<140&&player.Animation.Label=="attackMeleeHit","Melee callback damages then plays recovery");
        var health=enemy.Health; player.Animation.Render(.5); Equal(health,enemy.Health,"Recovery frames don't deal damage");
        var walking=new Animation("u2"); walking.Play("walkStart",false,()=>walking.Play("walkCycle")); walking.Render(5.0/30); Check(walking.Label=="walkCycle"&&walking.Frame==0,"Animation callback resets clock; surplus time not carried");
        var loop=new Animation("u2"); loop.Play("idle"); loop.Render(90.0/30); Equal(0,loop.Frame,"Idle wraps at exact source length");
        b=New(); player=b.CreateUnit(2,1); player.X=570; player.Wait=0; player.Render(0); player.Animation.Render(.7); Check(b.EnemyHealth<1000,"Melee hits enemy base through callback");
        for(int id=1;id<=29;id++)
        {
            b=New((uint)id); player=b.CreateUnit(id,1); enemy=b.CreateUnit(29,2); player.X=0; enemy.X=40; player.Wait=0; enemy.Wait=0; enemy.Health=100000;
            Ticks(b,600); Check(enemy.Health<100000,"Every unit has effective source-timed attack: "+id);
        }
        for(int id=1;id<=17;id++)
        {
            b=New((uint)id); enemy=b.CreateUnit(29,2); enemy.X=-400; enemy.Wait=1000; enemy.Health=100000; var turret=b.CreateTurret(id,1,1); Ticks(b,750); Check(enemy.Health<100000,"Every turret hits through source bp trajectory: "+id);
        }
    }
    static void ProjectileCases()
    {
        foreach(var definition in New().Data.Element("projectiles").Elements())
        {
            string name=AgeOfWarTwoBattle.S(definition,"name"); var b=New(); var first=b.CreateUnit(2,2); var last=b.CreateUnit(2,2); first.X=last.X=0;
            var p=new AgeOfWarTwoBattle.Projectile(b,name,1,0,-35,180,0,10,10,"Support"); p.Render(0);
            Equal(140,first.Health,"Collision insertion order first unaffected "+name); Equal(130,last.Health,"Last matching target is hit "+name); Check(!p.Alive,"Projectile consumed on hit "+name);
            if(definition.Attribute("explosion")!=null) Check(b.Effects.Count==1&&b.Effects[0].Clip=="explosion","Explosion cosmetic "+name);
            b=New(); p=new AgeOfWarTwoBattle.Projectile(b,name,1,581,-50,180,0,10,10,"Support"); p.Render(0); Equal(990,b.EnemyHealth,"All projectile base hit paths "+name);
            b=New(); p=new AgeOfWarTwoBattle.Projectile(b,name,2,-581,-50,180,0,10,10,"Support"); p.Render(0); Equal(990,b.PlayerHealth,"Both projectile base directions "+name);
        }
        var g=New(); var ballistic=new AgeOfWarTwoBattle.Projectile(g,"p3",1,0,-50,180,1,1,1,"Support"); ballistic.Render(.1); Equal(30,ballistic.X,"Projectile x uses3*100 and direction"); Equal(.25,ballistic.YSpeed,"Gravity mass dt/4 after position");
        var delayed=new AgeOfWarTwoBattle.Projectile(g,"p1",1,0,1,0,0,1,1,"Support"); g.Projectiles.Add(delayed); g.Tick(); Check(g.Projectiles.Contains(delayed)&&!delayed.Alive,"Dead projectile retained through collision tick"); g.Tick(); Check(!g.Projectiles.Contains(delayed),"Manager removes dead projectile next tick");
    }
    static void SpecialCases()
    {
        int[] damages={0,200,500,800,800,1000,600,1500};
        for(int age=1;age<=7;age++)
        {
            var b=New(); var friend=b.CreateUnit(29,1); var first=b.CreateUnit(29,2); var last=b.CreateUnit(29,2); friend.X=first.X=last.X=0;
            var strike=b.CreateStrike(age,1,0,-30); if(age!=2&&age!=7) strike.Render(0);
            Equal(15000,friend.Health,"Special doesn't hit friendly "+age); Equal(15000,first.Health,"Special last collision wins "+age); Equal(15000-damages[age],last.Health,"Distinctive special damage "+age); Equal(1000,b.EnemyHealth,"Special never damages bases "+age);
            if(age==2||age==7) { Check(strike.Alive,"Beam persists after instant damage"); strike.Render(.25); Equal(.5,strike.Alpha,"Beam fades .5s"); strike.Render(.25); Check(!strike.Alive,"Beam expires at .5s"); }
        }
        var arrows=New(); var arrow=arrows.CreateStrike(4,1,500,0); arrow.Render(0); Check(arrow.Ground&&arrow.Alive,"Arrows stick in ground"); arrow.Render(3.99); Check(arrow.Alive,"Ground arrows persist4s"); arrow.Render(.01); Check(!arrow.Alive,"Ground arrows removed after4s");
        for(int age=1;age<=5;age++)
        {
            var b=New(); b.CreateSpecial(1,age); var emitter=b.Effects[0]; emitter.Render(emitter.Interval); Check(b.Effects.Count==2,"All rain skills spawn timed strike "+age); Check(b.Effects[1].Age==age,"Correct strike subtype "+age);
        }
        foreach(int age in new[]{6,7})
        {
            var b=New(); b.CreateSpecial(1,age); var emitter=b.Effects[0]; emitter.Render(.01); Check(emitter.Alive&&double.IsNaN(emitter.Timer),"Uninitialized AS Number timer is NaN, plane/ray survive "+age); emitter.X=-590; emitter.Render(.2); Check(b.Effects.Count==2,"Moving emitter creates strike inside battlefield "+age); emitter.X=1079; emitter.Render(.01); Check(!emitter.Alive,"Moving emitter exits at1080 "+age);
        }
        var deferred=New(); deferred.CreateSpecial(1,1); Ticks(deferred,31); Check(deferred.Effects.Any(e=>!e.Emitter&&e.Age==1),"Rain emits via dynamic objects");
        var strikeNew=deferred.Effects.First(e=>!e.Emitter&&e.Age==1); Check(strikeNew.Y< -390,"Appended dynamic object waits until following render");
    }
    static void DamageDisplayAndFrameRate()
    {
        var rate=typeof(AgeOfWarTwoBattle).GetMethod("SetFrameRate"); Check(rate!=null,"Source FPS slider rate API"); var b=New(); rate.Invoke(b,new object[]{25}); Equal(25,b.TickRate,"Minimum FPS25"); b.Tick(); Equal(59.96,b.SpecialTimer,"Seconds delta uses selected stage FPS"); rate.Invoke(b,new object[]{300}); Equal(300,b.TickRate,"Maximum FPS300");
        var display=typeof(AgeOfWarTwoBattle).GetMethod("SetShowDamage"); Check(display!=null,"Show Damage option API"); display.Invoke(b,new object[]{true}); var target=b.CreateUnit(2,2); target.X=0; var p=new AgeOfWarTwoBattle.Projectile(b,"p1",1,0,-30,180,0,10,10,"Armored"); p.Render(0);
        var number=b.Capture().Visuals.Single(v=>v.TextScene=="damage-critical"); Check(number.Text=="15"&&number.TextField=="damageTXT","Critical floating number uses final counter damage");
        b.Tick(); var after=b.Capture().Visuals.Single(v=>v.InstanceId==number.InstanceId); Check(after.Y<number.Y&&after.Alpha<1,"Damage floats up and fades after timer decrement");
        var hv=b.Capture().Visuals.Single(v=>v.InstanceId==target.InstanceId); Check(hv.HealthAlpha>1&&hv.HealthY==-77&&hv.HitWidth==60&&hv.HitHeight==70,"Projectile hit reveals original health display and world hit bounds");
        var hover=typeof(AgeOfWarTwoBattle).GetMethod("HoverUnit"); Check(hover!=null,"Unit hover API"); hover.Invoke(b,new object[]{target.InstanceId}); Ticks(b,300); hv=b.Capture().Visuals.Single(v=>v.InstanceId==target.InstanceId); Equal(1,hv.HealthAlpha,"Hover holds health display at1"); hover.Invoke(b,new object[]{0L}); Ticks(b,301); hv=b.Capture().Visuals.Single(v=>v.InstanceId==target.InstanceId); Equal(0,hv.HealthAlpha,"Mouseout fades2persecond");
    }
    static void OriginalExposure()
    {
        var type=typeof(AgeOfWarTwoBattle).Assembly.GetType("XanaduYonder.Core.AgeOfWar.Two.AgeOfWarTwoExposure");
        Check(type!=null,"Source menu color-transform tween is available");
        var value=Activator.CreateInstance(type);var hover=type.GetMethod("Hover");var sample=type.GetMethod("Sample");
        hover.Invoke(value,new object[]{true,0.0,.5});
        Equal(.225,Convert.ToDouble(sample.Invoke(value,new object[]{.1})),"TweenLite default quadratic ease out reaches75% at half duration");
        Equal(.3,Convert.ToDouble(sample.Invoke(value,new object[]{.2})),"Exposure1.3 yields RGB offset76.5/255, brightness ignored");
        hover.Invoke(value,new object[]{false,.2,.5});
        Equal(.075,Convert.ToDouble(sample.Invoke(value,new object[]{.45})),"MainMenu out duration .5 and source ease");
        Equal(0,Convert.ToDouble(sample.Invoke(value,new object[]{.7})),"MainMenu out restores identity color transform");
    }
    static void OriginalTooltips()
    {
        var method=typeof(AgeOfWarTwoBattle).GetMethod("CaptureTooltip"); Check(method!=null,"Original tooltip field capture is available");
        var b=New(); Func<AgeOfWarAction,int,System.Collections.Generic.Dictionary<string,string>> tooltip=(a,id)=>(System.Collections.Generic.Dictionary<string,string>)method.Invoke(b,new object[]{a,id});
        var unit=tooltip(AgeOfWarAction.Train,1); Check(unit["nameTXT"]=="Slinger"&&unit["costTXT"]=="100 Gold"&&unit["walkTXT"]=="2.3m/s"&&unit["damageTXT"]=="15 - 30"&&unit["buildTXT"]=="1s","Original unit text formatting");
        Check(tooltip(AgeOfWarAction.Train,2)["rangeTXT"]=="none","Zero range text"); Check(tooltip(AgeOfWarAction.Train,4)["descTXT"]=="You need Dino Hut to unlock this unit","Original requirement tooltip"); b.Execute(C(AgeOfWarAction.Upgrade,1)); Check(tooltip(AgeOfWarAction.Upgrade,1)["costTXT"]==""&&tooltip(AgeOfWarAction.Upgrade,1)["levelTXT"]=="1 / 1","Completed upgrade fields");
        Check(tooltip(AgeOfWarAction.Special,1)["costTXT"]=="2000 Xp"&&tooltip(AgeOfWarAction.Evolve,1)["costTXT"]=="7000 Xp","Xp exact case"); Check(tooltip(AgeOfWarAction.SellTurret,1)["onT.costTXT"]=="375$","Source resale suffix"); Check(tooltip(AgeOfWarAction.BuyTurretSlot,1)["costTXT"]=="250 Gold","Tower tooltip");
        b.Gold=100000; b.Execute(C(AgeOfWarAction.Upgrade,12)); Check(tooltip(AgeOfWarAction.Train,2)["hpTXT"]=="140","Tooltips intentionally show base XML health after upgrades");
    }
    static void TurretEscrowAndErrors()
    {
        var b=New(); b.Execute(C(AgeOfWarAction.BuyTurretSlot)); int existing=b.Turrets.Count;
        Check(b.Execute(C(AgeOfWarAction.BuyTurret,1)),"Buy enters placement"); Equal(1500,b.Gold,"Escrow immediately deducted"); Equal(1,b.Capture().PendingTurretId,"Pending turret exposed"); Check(b.Turrets.Count==existing,"Purchase doesn't spawn until placement");
        Ticks(b,20); Check(b.Execute(C(AgeOfWarAction.Cancel)),"Cancel active placement"); Equal(2250,b.Gold,"Cancel returns exact escrow"); Equal(0,b.Capture().PendingTurretId,"Cancel clears placement");
        b.Execute(C(AgeOfWarAction.BuyTurret,1)); Check(b.Execute(C(AgeOfWarAction.PlaceTurret,0,1)),"Place purchased turret"); Equal(0,b.Capture().PendingTurretId,"Placement clears pending state"); Check(b.Turrets.Count==existing+1,"Place creates turret"); Check(!b.Execute(C(AgeOfWarAction.Cancel)),"No refund after placement"); Equal(1500,b.Gold,"Placement charges only once");
        b=New(); Check(!b.Execute(C(AgeOfWarAction.Train,4)),"Blocked order emits error"); Check(b.DrainEvents().Last().Message=="Unit locked","Original locked error text"); b.Gold=0; Check(!b.Execute(C(AgeOfWarAction.Train,2)),"Insufficient gold rejected"); Check(b.DrainEvents().Last().Message=="Not enough Gold","Original gold error text");
    }
    static void PresentationTransforms()
    {
        var b=New(); var f=b.Capture(); var playerBase=f.Visuals.Single(v=>v.InstanceId==-1); Equal(-491.3,playerBase.X,"Base render origin uses actual SWF placement, not collision edge"); Equal(100,playerBase.Y,"SWF base vertical offset");
        b.Execute(C(AgeOfWarAction.BuyTurretSlot)); f=b.Capture(); var extension=f.Visuals.Single(v=>v.Clip=="base-extension/1/1"&&v.X<0); Equal(-621.8,extension.X,"Tower parent + child placement"); Equal(-143.75,extension.Y,"Tower transform y"); Equal(.75,extension.ScaleX,"Tower local scale");
        b.CreateTurret(1,1,1); f=b.Capture(); Check(f.Visuals.Count(v=>v.Clip=="turret/1"&&v.X<0)==2,"Separate stationary base and aiming barrel");
        var unit=b.CreateUnit(2,1); var second=b.CreateTurret(2,1,2); f=b.Capture(); var ordered=f.Visuals.OrderBy(v=>v.Depth).ToArray();
        Check(Array.FindIndex(ordered,v=>v.InstanceId==unit.InstanceId)<Array.FindIndex(ordered,v=>v.Clip=="turret/1"),"GameScreen adds turrets manager above units manager");
        Check(Array.FindLastIndex(ordered,v=>v.Clip=="turret/1")<Array.FindIndex(ordered,v=>v.InstanceId==-1000-second.InstanceId),"Turret child base/cannon order retains manager insertion order");
        var buildFrame=typeof(AgeOfWarFrame).GetField("BuildTurretFrame");var sellFrame=typeof(AgeOfWarTurretSlot).GetField("SellFrame");
        Check(buildFrame!=null&&sellFrame!=null,"World control animation frames exposed");
        Ticks(b,5);f=b.Capture();Equal(2,Convert.ToDouble(buildFrame.GetValue(f)),"Build cross uses60fps Moon clock");
        Equal(1,Convert.ToDouble(sellFrame.GetValue(f.TurretSlots.Single(s=>!s.Enemy&&s.Slot==1))),"Sell cross uses30fps since turret creation");
        b.Execute(C(AgeOfWarAction.Pause));Ticks(b,5);f=b.Capture();Equal(2,Convert.ToDouble(buildFrame.GetValue(f)),"Pause freezes world-control Moon animations");b.Execute(C(AgeOfWarAction.Pause));
        var p=new AgeOfWarTwoBattle.Projectile(b,"p4",1,0,-500,180,0,1,1,"Support"); b.Projectiles.Add(p); b.Tick(); var p1=b.Capture().Visuals.Single(v=>v.InstanceId==p.InstanceId); b.Tick(); var p2=b.Capture().Visuals.Single(v=>v.InstanceId==p.InstanceId); Check(p1.Frame!=p2.Frame,"Native projectile movie clip animates");
        b.Execute(C(AgeOfWarAction.Pause)); b.Tick(); var p3=b.Capture().Visuals.Single(v=>v.InstanceId==p.InstanceId); Check(p3.Frame!=p2.Frame,"Unmanaged native movie clips continue during pause");
    }
    static void AiAndWholeGames()
    {
        var b=New(); b.PlayerHealth=b.EnemyHealth=100000000; Ticks(b,150*400); Check(b.EnemyAge>=2,"AI event queue advances eras"); Check(b.EnemySpots>=1,"AI scripted tower construction");
        b=New(); b.PlayerHealth=b.EnemyHealth=100000000; Ticks(b,150*2200); Check(b.EnemyAge==7,"AI completes all seven age schedules"); Check(b.EnemySpots==4,"AI reaches all four tower spots"); Check(b.Turrets.Any(t=>t.Side==2&&t.TypeId==17),"AI final heavy plasma defense"); Check(b.EnemyPopulation<=10,"AI population bounded");
        var a=New(123); var c=New(123); a.Execute(C(AgeOfWarAction.Train,2)); c.Execute(C(AgeOfWarAction.Train,2)); Ticks(a,20000); Ticks(c,20000); Equal(a.PlayerHealth,c.PlayerHealth,"Seeded repeatability health"); Equal(a.Gold,c.Gold,"Seeded repeatability economy");
        var loss=New(); Ticks(loss,150*300); Check(loss.Capture().Ended&&!loss.Capture().Victory,"Passive full game reaches source defeat");
        var win=New(); var god=win.CreateUnit(29,1); god.X=540; god.Wait=0; win.EnemyHealth=1; Ticks(win,150*5); Check(win.Capture().Ended&&win.Capture().Victory,"Real attack reaches victory"); long finalTick=win.Capture().Tick; win.Tick(); Equal(finalTick,win.Capture().Tick,"Battle stops after outcome");
    }
}
