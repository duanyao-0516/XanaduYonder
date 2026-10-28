using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using XanaduYonder.Core.Zuma;

internal static class Program
{
    private static int passed;
    private const long ClearedGaugeScore=3001;
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    private static int Main()
    {
        try
        {
            Run("four original difficulty profiles", Difficulties);
            Run("generation cannot stop below the strict gauge outside loss", GenerationStateValidation);
            Run("effective play time fits Int32 result seconds and saturates safely", PlayTimeBoundary);
            Run("intro freezes play time and rolls initial chain", Intro);
            Run("aim zero preserves direction; recoil rejects without consuming", Shooting);
            Run("three match inserted in front scores then expires", Match);
            Run("fifth chain gets original bonus", ChainBonus);
            Run("gaps preserve separation then reverse same colors", Gap);
            Run("rejoining gap earns combo", Combo);
            Run("tunnel and exploding balls cannot be hit", Tunnel);
            Run("coin schedule blink expiry and capture", Coins);
            Run("strict gauge then win outro final score", Win);
            Run("pit drains before loss and freezes play time", Loss);
            Run("capacity rejection preserves RNG and ammo", Capacity);
            Run("complete snapshots replay every causal phase", Replay);
            Run("snapshots and levels are owned copies", Ownership);
            Run("invalid snapshots rejected", Invalid);
            Run("impossible phase snapshots rejected", InvalidPhase);
            Run("insertion before tail retains original 32-sample placement", InsertBeforeTail);
            Run("all 18 paths and four difficulties drain legally", RealPaths);
            Run("corrected source generation has a fixed seed golden sequence", GenerationGolden);
            Run("bullet insertion resolves a full match on the next step", BulletMatch);
            Run("merging explosion groups use original shared removal", ExplosionGroups);
            Run("last-moving group controls near-pit slowdown", Slowdown);
            Run("swap during initial roll does not fire", InitialSwap);
            Run("empty chain regeneration preserves original bullet ordering", EmptyChainBullet);
            Run("shot misses reset chain and do not queue", Miss);
            Run("new shots reuse first free source slot", BulletSlots);
            Run("sustained live shooting across tunneled maps remains recoverable", LivePlay);
            Console.WriteLine("PASS " + passed + " Zuma scenarios"); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static void Run(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Eq<T>(T a, T b, string message) { Check(EqualityComparer<T>.Default.Equals(a,b), message + ": " + a + " != " + b); }
    private static ZumaLevelDefinition Level(int length = 12000)
    {
        var p = new ZumaPathPoint[length+1];
        for (int i=0;i<p.Length;i++) p[i]=new ZumaPathPoint { X=100+i*0.05f, Y=200 };
        return new ZumaLevelDefinition { Id="spiral", DisplayName="Test", FrogX=400,FrogY=500,Points=p, Coins=new[]{new ZumaPoint{X=900,Y=400}} };
    }
    private static ZumaSnapshot Play(ZumaLevelDefinition level)
    {
        var s = new ZumaGame(level,"rabbit",12345).Snapshot();
        s.Phase="play";s.InitialRoll=false;s.IntroInitialized=true;s.TrailParticles=new ZumaTrailParticleSnapshot[0];
        // Controlled finite chains are legal only after clearing the strict generation threshold.
        s.Balls=new[]{Ball(200,0),Ball(168,1),Ball(136,2)};s.Generating=false;s.Score=ClearedGaugeScore;
        s.Frog.CurrentColor=0;s.Frog.NextColor=1;return s;
    }
    private static ZumaBallSnapshot Ball(float pos,int color) { return new ZumaBallSnapshot { PathPosition=pos,Color=color }; }
    private static void Difficulties()
    {
        string[] ids={"rabbit","eagle","jaguar","sun_god"};int[] colors={4,5,6,6}, starts={40,50,60,60},ace={70,100,120,150};
        float[] speeds={.8f,.95f,.75f,1.25f};
        for(int i=0;i<4;i++){var d=ZumaDifficulty.Get(ids[i]);Eq(d.ColorCount,colors[i],"colors");Eq(d.StartCount,starts[i],"start");Eq(d.Speed,speeds[i],"speed");Eq(d.AceTimeSeconds,ace[i],"ace");}
    }
    private static void GenerationStateValidation()
    {
        var level=Level();
        foreach(var mode in new[]{"rabbit","eagle","jaguar","sun_god"})
        {
            var s=new ZumaGame(level,mode,12345).Snapshot();s.Phase="play";s.IntroInitialized=true;s.InitialRoll=false;
            s.Generating=false;s.Balls=new ZumaBallSnapshot[0];s.Score=0;
            Check(!s.TryValidate(level,out _),mode+" empty stopped chain below gauge must be rejected");
            bool rejected=false;try{ZumaGame.Restore(level,s);}catch(ArgumentException){rejected=true;}
            Check(rejected,mode+" Restore must reject unrecoverable deadlock");
            s.Score=ZumaDifficulty.Get(mode).GaugeScore;
            Check(!s.TryValidate(level,out _),mode+" gauge equality cannot stop generation");
            s.Balls=new[]{Ball(200,0)};s.Score=0;
            Check(!s.TryValidate(level,out _),mode+" nonempty stopped chain below gauge must also be rejected");
            s.Phase="intro";Check(!s.TryValidate(level,out _),mode+" intro cannot stop generation below gauge");
            s.Phase="lossDrain";s.EndReached=true;
            Check(s.TryValidate(level,out var reason),mode+" stopped low-score loss remains valid: "+reason);
            s.Phase="play";s.EndReached=false;s.Generating=true;s.Balls=new ZumaBallSnapshot[0];
            Check(s.TryValidate(level,out reason),mode+" empty generating transition remains valid: "+reason);
            var game=ZumaGame.Restore(level,s);game.Tick();Eq(game.Snapshot().Balls.Length,1,mode+" legal empty chain regenerates");
        }
    }
    private static void PlayTimeBoundary()
    {
        var level=Level();long maximum=(long)int.MaxValue*60+59;
        foreach(var phase in new[]{"intro","play","lossDrain","winOutro","won","lost"})
        {
            var s=Play(level);s.Phase=phase;s.PlayTicks=maximum+1;s.TotalTicks=maximum+1;
            if(phase=="intro"){s.Generating=true;s.InitialRoll=true;}
            if(phase=="lossDrain"||phase=="lost")s.EndReached=true;
            if(phase=="winOutro"||phase=="won"||phase=="lost")
            {s.Balls=new ZumaBallSnapshot[0];s.OutroInitialized=phase!="lost";}
            Check(!s.TryValidate(level,out _),phase+" result-second overflow must be rejected");
            bool rejected=false;try{ZumaGame.Restore(level,s);}catch(ArgumentException){rejected=true;}
            Check(rejected,phase+" Restore must reject overflow");
            s.PlayTicks=maximum;
            Check(s.TryValidate(level,out var reason),phase+" final representable second remains valid: "+reason);
        }
        var active=Play(level);active.PlayTicks=maximum-1;active.TotalTicks=9007199254740993L;
        var game=ZumaGame.Restore(level,active);game.Tick();Eq(game.Snapshot().PlayTicks,maximum,"last representable tick");
        game.Tick();var after=game.Snapshot();Eq(after.PlayTicks,maximum,"play tick saturation");
        Eq(after.TotalTicks,9007199254740995L,"independent Int64 total ticks keep exact precision");
        Eq(checked((int)(after.PlayTicks/60)),int.MaxValue,"result conversion is safe");
        Check(after.TryValidate(level,out var failure),"model must not emit an unreadable boundary state: "+failure);
        var restored=ZumaGame.Restore(level,after);game.Tick();restored.Tick();
        Eq(JsonSerializer.Serialize(game.Snapshot(),Json),JsonSerializer.Serialize(restored.Snapshot(),Json),"boundary replay");
    }
    private static void Intro()
    {
        var level=Level();var g=new ZumaGame(level,"rabbit",1);Eq(g.Snapshot().Phase,"intro","intro");
        Check(!g.TryFire(),"no intro fire");int n=0;while(g.Snapshot().Phase=="intro"&&n++<2000)g.Tick();
        Eq(g.Snapshot().PlayTicks,0L,"intro time");Check(n<2000,"intro finishes");
        for(int i=0;i<120;i++)g.Tick();var s=g.Snapshot();Check(!s.InitialRoll,"initial roll finishes");Check(s.Balls.Length>=40,"40 start balls");Check(s.PlayTicks==120,"only play counted");
    }
    private static void Shooting()
    {
        var l=Level();var s=Play(l);var g=ZumaGame.Restore(l,s);Check(g.Aim(800,500),"aim");Check(!g.Aim(400,500),"zero aim");Check(g.TryFire(),"first shot");
        var first=g.Snapshot();Check(!g.TryFire(),"cooldown");Eq(g.Snapshot().RandomState,first.RandomState,"rejected RNG");Eq(first.Bullets.Length,1,"one bullet");Eq(first.Frog.RecoilTicks,10,"recoil 10");
        for(int i=0;i<10;i++)g.Tick();Check(g.TryFire(),"recoil expires");var before=g.Snapshot();Check(g.Swap(),"swap");Eq(g.Snapshot().Frog.CurrentColor,before.Frog.NextColor,"swap current");
    }
    private static void Match()
    {
        var l=Level();var s=Play(l);s.Balls=new[]{Ball(264,0),Ball(232,0),Ball(200,0)};s.Balls[0].Inserted=true;
        var g=ZumaGame.Restore(l,s);g.Tick();var hit=g.Snapshot();Eq(hit.Score-s.Score,30L,"match score");Check(hit.Balls[0].Exploding,"explodes");
        for(int i=0;i<31;i++)g.Tick();Eq(g.Snapshot().Balls.Length,0,"explosion deletion");
    }
    private static void ChainBonus()
    {
        var l=Level();var s=Play(l);s.ChainBonus=4;s.MaxChainBonus=4;s.Balls=new[]{Ball(264,0),Ball(232,0),Ball(200,0)};s.Balls[0].Inserted=true;
        var g=ZumaGame.Restore(l,s);g.Tick();Eq(g.Snapshot().Score-s.Score,130L,"fifth bonus");Eq(g.Snapshot().MaxChainBonus,5,"max chain");
    }
    private static void Gap()
    {
        var l=Level();var s=Play(l);s.Balls=new[]{Ball(300,0),Ball(100,0)};var g=ZumaGame.Restore(l,s);g.Tick();var after=g.Snapshot();Check(after.Balls[0].Speed<0,"backtracks");Check(after.Balls[0].GoBack,"go back state");
        s.Balls[1].Color=1;g=ZumaGame.Restore(l,s);g.Tick();Eq(g.Snapshot().Balls[0].PathPosition,300f,"different color gap stationary");
    }
    private static void Combo()
    {
        var l=Level();var s=Play(l);s.Balls=new[]{Ball(300,0),Ball(160,0),Ball(128,0)};s.Balls[1].Combo=1;
        var g=ZumaGame.Restore(l,s);for(int i=0;i<150&&g.Snapshot().Score==s.Score;i++)g.Tick();var after=g.Snapshot();Eq(after.Score-s.Score,130L,"combo score");Eq(after.TotalCombos,1,"combo count");Eq(after.MaxCombo,1,"max combo");
    }
    private static void Tunnel()
    {
        var l=Level();for(int i=0;i<l.Points.Length;i++)l.Points[i].Hidden=true;
        var s=Play(l);s.Bullets=new[]{new ZumaBulletSnapshot { X=110,Y=185,DirectionY=1,Color=3 }};var g=ZumaGame.Restore(l,s);g.Tick();Eq(g.Snapshot().Balls.Length,3,"hidden chain untouched");Eq(g.Snapshot().Bullets.Length,1,"bullet stays");
        for(int i=0;i<l.Points.Length;i++)l.Points[i].Hidden=false;
        g=ZumaGame.Restore(l,s);g.Tick();Eq(g.Snapshot().Balls.Length,4,"visible chain insertion");Eq(g.Snapshot().Bullets.Length,0,"bullet consumed");
    }
    private static void Coins()
    {
        var l=Level();var s=Play(l);s.Coin.TimerTicks=600;var g=ZumaGame.Restore(l,s);g.Tick();Check(g.Snapshot().Coin.Active,"coin 601");
        s=g.Snapshot();s.Coin.TimerTicks=600;g=ZumaGame.Restore(l,s);g.Tick();Check(g.Snapshot().Coin.Blinking,"blink 601");
        s=g.Snapshot();s.Coin.TimerTicks=180;g=ZumaGame.Restore(l,s);g.Tick();Check(!g.Snapshot().Coin.Active,"expiry 181");
        s=Play(l);s.Coin.Active=true;s.Coin.PositionIndex=0;s.ChainBonus=4;s.MaxChainBonus=4;
        s.Bullets=new[]{new ZumaBulletSnapshot {X=900,Y=385,DirectionY=1,Color=0}};g=ZumaGame.Restore(l,s);g.Tick();
        Eq(g.Snapshot().Score-s.Score,500L,"coin score");Eq(g.Snapshot().TotalCoins,1,"coin count");Eq(g.Snapshot().ChainBonus,0,"coin resets chain");Eq(g.Snapshot().Bullets.Length,0,"coin consumes bullet");
    }
    private static void Win()
    {
        var l=Level(600);var s=Play(l);s.Balls=new ZumaBallSnapshot[0];s.Score=3000;s.Generating=true;
        var g=ZumaGame.Restore(l,s);g.Tick();Check(g.Snapshot().Phase=="play","strict gauge equal");Check(g.Snapshot().Balls.Length==1,"empty generating chain safe");
        s=Play(l);s.Balls=new ZumaBallSnapshot[0];s.Score=3001;s.LastClearedPosition=200;g=ZumaGame.Restore(l,s);g.Tick();Eq(g.Snapshot().Phase,"winOutro","win transition");long ticks=g.Snapshot().PlayTicks;
        for(int i=0;i<200&&g.Snapshot().Phase!="won";i++)g.Tick();Eq(g.Snapshot().Phase,"won","outro ends");Eq(g.Snapshot().PlayTicks,ticks,"outro freezes play ticks");Eq(g.Snapshot().Score,3701L,"outro exact 7 pops");
    }
    private static void Loss()
    {
        var l=Level(600);var s=Play(l);s.Balls=new[]{Ball(599.5f,0),Ball(400,1)};s.Balls[0].Speed=1;s.ChainSpeed=1;s.Generating=true;var g=ZumaGame.Restore(l,s);
        for(int i=0;i<100&&g.Snapshot().Phase=="play";i++)g.Tick();Eq(g.Snapshot().Phase,"lossDrain","loss drain");long t=g.Snapshot().PlayTicks;
        for(int i=0;i<100&&g.Snapshot().Phase!="lost";i++)g.Tick();Eq(g.Snapshot().Phase,"lost","drained");Eq(g.Snapshot().PlayTicks,t,"loss frozen");
    }
    private static void Capacity()
    {
        var l=Level();var s=Play(l);s.Balls=new ZumaBallSnapshot[256];for(int i=0;i<256;i++)s.Balls[i]=Ball(9000-i*32,i%4);
        var g=ZumaGame.Restore(l,s);var before=g.Snapshot();Check(!g.TryFire(),"full chain rejected");Eq(g.Snapshot().RandomState,before.RandomState,"full chain RNG");Eq(g.Snapshot().Frog.CurrentColor,before.Frog.CurrentColor,"ammo preserved");
        s=Play(l);s.Bullets=new ZumaBulletSnapshot[32];for(int i=0;i<32;i++)s.Bullets[i]=new ZumaBulletSnapshot{Slot=i,X=100,Y=500,DirectionX=1,Color=0};g=ZumaGame.Restore(l,s);before=g.Snapshot();Check(!g.TryFire(),"full bullets rejected");Eq(g.Snapshot().RandomState,before.RandomState,"bullets RNG");
    }
    private static void Replay()
    {
        var l=Level();var originals=new List<ZumaGame>{new ZumaGame(l,"jaguar",99)};
        var s=Play(l);s.Balls[0].Inserted=true;s.Balls[1].Exploding=true;s.Balls[1].ExplosionTicks=7;s.Frog.RecoilTicks=5;s.Coin.Active=true;s.Coin.Blinking=true;s.Coin.TimerTicks=90;s.Balls[2].IsSingle=true;originals.Add(ZumaGame.Restore(l,s));
        s=Play(l);s.Phase="lossDrain";s.EndReached=true;s.ChainSpeed=32;originals.Add(ZumaGame.Restore(l,s));
        s=Play(l);s.Phase="winOutro";s.Balls=new ZumaBallSnapshot[0];s.Score=3500;s.OutroInitialized=true;s.OutroPosition=300;s.OutroPreviousPosition=280;s.TrailParticles=new[]{new ZumaTrailParticleSnapshot{X=100,Y=200,AgeTicks=12}};originals.Add(ZumaGame.Restore(l,s));
        foreach(var original in originals){var restored=ZumaGame.Restore(l,original.Snapshot());for(int i=0;i<400;i++){if(i%17==0){original.Aim(700,300);restored.Aim(700,300);original.TryFire();restored.TryFire();}if(i%31==0){original.Swap();restored.Swap();}original.Tick();restored.Tick();Eq(JsonSerializer.Serialize(original.Snapshot(),Json),JsonSerializer.Serialize(restored.Snapshot(),Json),"complete replay");}}
    }
    private static void Ownership()
    {
        var l=Level();var g=new ZumaGame(l,"rabbit",1);var first=g.Snapshot();first.Balls[0].Color=99;first.Frog.CurrentColor=99;l.Points[0].X=9999;var after=g.Snapshot();Check(after.Balls[0].Color<4,"deep ball clone");Check(after.Frog.CurrentColor<4,"deep frog clone");Check(after.Balls[0].X!=9999,"level ownership");
    }
    private static void Invalid()
    {
        var l=Level();var s=Play(l);s.RandomState=0;Check(!ZumaGame.Validate(l,s,out _),"zero RNG");s=Play(l);s.Balls[0].PathPosition=float.NaN;Check(!ZumaGame.Validate(l,s,out _),"NaN");s=Play(l);s.Balls[0].Color=5;Check(!ZumaGame.Validate(l,s,out _),"out of profile color");s=Play(l);s.Balls=null;Check(!ZumaGame.Validate(l,s,out _),"null balls");
    }
    private static void InvalidPhase()
    {
        var l=Level();var s=Play(l);s.Phase="won";s.Score=3500;s.OutroInitialized=true;
        Check(!ZumaGame.Validate(l,s,out _),"terminal cannot retain balls");
        s=Play(l);s.Phase="lossDrain";Check(!ZumaGame.Validate(l,s,out _),"drain requires end reached");
        s=Play(l);s.Phase="winOutro";s.Balls=new ZumaBallSnapshot[0];s.Score=0;Check(!ZumaGame.Validate(l,s,out _),"outro requires threshold");
    }
    private static void InsertBeforeTail()
    {
        var l=Level();for(int i=0;i<l.Points.Length;i++)l.Points[i].X=100+i*1.5f;
        var s=Play(l);s.Balls=new[]{Ball(264,0),Ball(232,1),Ball(200,2)};
        // Hit the back half of index 1, inserting at original len-1. The original CollidesBack
        // returns false for that last slot before len increments, placing it exactly 32 behind.
        s.Bullets=new[]{new ZumaBulletSnapshot{X=100+212*1.5f,Y=185,DirectionY=1,Color=3}};
        var g=ZumaGame.Restore(l,s);g.Tick();var a=g.Snapshot();Eq(a.Balls.Length,4,"inserted length");
        Check(Math.Abs(a.Balls[2].PathPosition-(a.Balls[1].PathPosition-32))<.001f,"original near-tail placement");
    }
    private static void RealPaths()
    {
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
        string[] maps={"spiral","claw","riverbed","targetglyph","turnaround","longrange","tiltspiral","underover","warshak","loopy","groovefest","spaceinvaders","triangle","coaster","squaresville","tunnellevel","overunder","inversespiral"};
        string[] modes={"rabbit","eagle","jaguar","sun_god"};
        var options=new JsonSerializerOptions{IncludeFields=true,PropertyNameCaseInsensitive=true};
        foreach(string map in maps)
        {
            var level=JsonSerializer.Deserialize<ZumaLevelDefinition>(File.ReadAllText(Path.Combine(root,"Assets/Resources/Zuma/Levels",map+".json")),options);
            Check(ZumaGame.ValidateLevel(level,out var error),map+": "+error);
            foreach(string mode in modes)
            {
                var game=new ZumaGame(level,mode,12345);ZumaSnapshot s=game.Snapshot();int ticks=0;
                while(s.Phase!="lost"&&ticks<30000)
                {
                    for(int i=0;i<60;i++){game.Tick();ticks++;}s=game.Snapshot();
                    Check(s.TryValidate(level,out error),map+"/"+mode+" tick "+ticks+": "+error);
                    Check(s.CapacityRejections==0,map+" capacity overflow");
                    var copy=ZumaGame.Restore(level,s);game.Tick();copy.Tick();ticks++;
                    Eq(JsonSerializer.Serialize(game.Snapshot(),Json),JsonSerializer.Serialize(copy.Snapshot(),Json),map+" live replay");
                }
                Eq(s.Phase,"lost",map+"/"+mode+" completes unplayed drain");
            }
        }
    }
    private static void GenerationGolden()
    {
        var l=Level();var s=new ZumaGame(l,"rabbit",12345).Snapshot();s.Phase="play";s.IntroInitialized=true;
        var game=ZumaGame.Restore(l,s);int count=0;
        while(game.Snapshot().InitialRoll&&count++<100)game.Tick();s=game.Snapshot();
        // Independently calculated from xorshift32 plus the corrected C Append branch order,
        // including next/current-color correction after each generation step.
        string colors="";var singles=new List<int>();for(int i=0;i<s.Balls.Length;i++){colors+=s.Balls[i].Color;if(s.Balls[i].IsSingle)singles.Add(i);}
        Eq(count,46,"initial roll ticks");Eq(colors,"2033231133331111000012300003022223200001","golden colors");
        Eq(string.Join(",",singles),"34","single state retained");Eq(s.RandomState,263449u,"full PRNG state");
    }
    private static void BulletMatch()
    {
        var l=Level();for(int i=0;i<l.Points.Length;i++)l.Points[i].X=100+i*1.5f;
        var s=Play(l);s.Balls=new[]{Ball(264,0),Ball(232,0),Ball(200,1)};
        s.Bullets=new[]{new ZumaBulletSnapshot{X=510,Y=185,DirectionY=1,Color=0}};
        var game=ZumaGame.Restore(l,s);game.Tick();s=game.Snapshot();Eq(s.Score,ClearedGaugeScore,"no premature removal");Eq(s.Balls.Length,4,"bullet inserted");Check(s.Balls[0].Inserted,"inserted pending");
        game.Tick();s=game.Snapshot();Eq(s.Score-ClearedGaugeScore,30L,"next-step match score");Check(s.Balls[0].Exploding&&s.Balls[1].Exploding&&s.Balls[2].Exploding,"three matched balls");
    }
    private static void ExplosionGroups()
    {
        var l=Level();var s=Play(l);s.Balls=new[]{Ball(264,0),Ball(232,0),Ball(200,1)};
        s.Balls[0].Exploding=true;s.Balls[0].ExplosionTicks=30;s.Balls[1].Exploding=true;s.Balls[1].ExplosionTicks=5;
        var game=ZumaGame.Restore(l,s);game.Tick();Eq(game.Snapshot().Balls.Length,1,"whole connected exploding group deleted");
    }
    private static void Slowdown()
    {
        var l=Level(1000);var s=Play(l);s.Balls=new[]{Ball(850,0),Ball(818,1),Ball(786,2)};var game=ZumaGame.Restore(l,s);game.Tick();Eq(game.Snapshot().ChainSpeed,.2f,"near end slowdown");
        s.Balls[2].PathPosition=100;game=ZumaGame.Restore(l,s);game.Tick();Eq(game.Snapshot().ChainSpeed,.8f,"separated moving tail determines speed");
    }
    private static void InitialSwap()
    {
        var l=Level();var s=new ZumaGame(l,"rabbit",1).Snapshot();s.Phase="play";s.IntroInitialized=true;
        var game=ZumaGame.Restore(l,s);Check(game.Swap(),"source allows initial-roll swap");Eq(game.Snapshot().Frog.CurrentColor,1,"swapped color");Check(!game.TryFire(),"initial roll still rejects shot");
    }
    private static void EmptyChainBullet()
    {
        var l=Level();var s=Play(l);s.Balls=new ZumaBallSnapshot[0];s.Generating=true;s.Score=0;
        s.Bullets=new[]{new ZumaBulletSnapshot{X=300,Y=400,DirectionX=1,Color=0}};
        var game=ZumaGame.Restore(l,s);game.Tick();Eq(game.Snapshot().Bullets[0].X,300f,"original empty-chain bullet freeze");
        game.Tick();Eq(game.Snapshot().Bullets[0].X,315f,"movement resumes after generation");
    }
    private static void Miss()
    {
        var l=Level();var s=Play(l);s.ChainBonus=4;s.MaxChainBonus=4;s.Bullets=new[]{new ZumaBulletSnapshot{X=1331,Y=400,DirectionX=1,Color=0}};
        var game=ZumaGame.Restore(l,s);game.Tick();Eq(game.Snapshot().ChainBonus,0,"miss resets chain");Eq(game.Snapshot().Bullets.Length,0,"miss removed");
    }
    private static void BulletSlots()
    {
        var l=Level();var s=Play(l);s.Bullets=new[]{new ZumaBulletSnapshot{Slot=1,X=100,Y=500,DirectionX=1,Color=1}};
        var game=ZumaGame.Restore(l,s);Check(game.TryFire(),"new shot");var bullets=game.Snapshot().Bullets;
        Eq(bullets[0].Slot,0,"lowest free slot is reused");Eq(bullets[0].X,l.FrogX,"new shot processes before old slot one");Eq(bullets[1].Slot,1,"old shot retains slot");
    }
    private static void LivePlay()
    {
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
        var options=new JsonSerializerOptions{IncludeFields=true,PropertyNameCaseInsensitive=true};
        foreach(string map in new[]{"spiral","underover","loopy","inversespiral"})
        foreach(string mode in new[]{"rabbit","eagle","jaguar","sun_god"})
        {
            var level=JsonSerializer.Deserialize<ZumaLevelDefinition>(File.ReadAllText(Path.Combine(root,"Assets/Resources/Zuma/Levels",map+".json")),options);
            var game=new ZumaGame(level,mode,7654321);var restored=ZumaGame.Restore(level,game.Snapshot());
            for(int tick=0;tick<12000;tick++)
            {
                var state=game.Snapshot();if(state.Phase=="lost"||state.Phase=="won")break;
                if(tick%11==0&&state.Phase=="play")
                {
                    ZumaBallSnapshot target=null;
                    foreach(var ball in state.Balls)if(!ball.Hidden&&!ball.Exploding&&(target==null||ball.Color==state.Frog.CurrentColor)){target=ball;if(ball.Color==state.Frog.CurrentColor)break;}
                    if(target!=null){game.Aim(target.X,target.Y);restored.Aim(target.X,target.Y);Eq(game.TryFire(),restored.TryFire(),"live shot acceptance");}
                }
                if(tick%77==0){game.Swap();restored.Swap();}
                game.Tick();restored.Tick();
                if(tick%61==0)
                {
                    state=game.Snapshot();Check(state.TryValidate(level,out var reason),map+"/"+mode+" tick"+tick+": "+reason);
                    Check(state.CapacityRejections==0,map+" live capacity");
                    Eq(JsonSerializer.Serialize(state,Json),JsonSerializer.Serialize(restored.Snapshot(),Json),"live replay");
                    restored=ZumaGame.Restore(level,state);
                }
            }
        }
    }
}
