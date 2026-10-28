using System;
using System.Collections.Generic;
using System.Reflection;
using XanaduYonder.Core.FlappyBird;
internal static class Program
{
    private static int count;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
    private static void Near(double actual, double expected, string message) { Check(Math.Abs(actual-expected)<1e-8,message+": "+actual+" != "+expected); }
    private static int Main()
    {
        try {
            GentleFlightChecks();
            var game = new FlappyGame(7);
            Near(game.BirdY, FlappyRules.PlayHeight*.55, "initial bird height");
            Near(game.BirdVelocity,0,"initial velocity");
            Check(game.Score==0 && !game.GameOver,"fresh game");
            var result=game.Step(true);
            Check(result.Flapped && !result.Died && result.Points==0,"one impulse");
            Near(game.BirdVelocity,FlappyRules.FlapVelocity-FlappyRules.Gravity*FlappyRules.FixedStep,"gravity follows flap");
            double velocity=game.BirdVelocity;
            game.Step(false); Check(game.BirdVelocity<velocity,"not an automatic repeated flap");
            var falling=new FlappyGame(8);
            for(int i=0;i<180 && !falling.GameOver;i++) falling.Step(false);
            Check(falling.GameOver,"ground kills"); Near(falling.BirdY,FlappyRules.BirdRadius,"stops at swept ground contact");
            double deadY=falling.BirdY, deadTime=falling.ElapsedTime;
            for(int i=0;i<100;i++) { result=falling.Step(true); Check(!result.Flapped&&!result.Died&&result.Points==0,"terminal callbacks once"); }
            Near(falling.BirdY,deadY,"dead pose frozen"); Near(falling.ElapsedTime,deadTime,"dead time frozen");
            var ceiling=new FlappyGame(9);
            for(int i=0;i<40;i++) ceiling.Step(true);
            Check(!ceiling.GameOver,"ceiling does not kill"); Near(ceiling.BirdY,FlappyRules.PlayHeight-FlappyRules.BirdRadius,"ceiling clamp"); Near(ceiling.BirdVelocity,0,"ceiling clears upward velocity");
            var a=new FlappyGame(123); var b=new FlappyGame(123);
            for(int i=0;i<100;i++) { a.Step(i%27==0); b.Step(i%27==0); Near(a.BirdY,b.BirdY,"seed replay bird"); Check(a.Pipes.Count==b.Pipes.Count,"seed replay count"); for(int j=0;j<a.Pipes.Count;j++) Near(a.Pipes[j].GapCenter,b.Pipes[j].GapCenter,"seed replay gaps"); }
            Check(FlappyReachability.VerifyContract(),"all enabled transitions compose through invariant entry states");
            CollisionChecks();
            EventOrderingChecks();
            CertifiedLongRun();
            IdentityWrapChecks();
            ConnectorBoundaryChecks();
            VisiblePipeGeometryChecks();
            Console.WriteLine("Flappy rules checks: "+count+" passed, 0 failed."); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static void CollisionChecks()
    {
        double hit;
        Check(FlappyCollision.Sweep(0,0,100,0,40,-10,60,10,5,out hit),"fast sweep cannot tunnel"); Near(hit,.35,"first side contact");
        Check(!FlappyCollision.Sweep(0,16,100,16,40,-10,60,10,5,out hit),"clear above rectangle");
        Check(FlappyCollision.Sweep(0,15,100,15,40,-10,60,10,5,out hit),"tangent contact"); Near(hit,.4,"corner tangent time");
        Check(!FlappyCollision.Sweep(34,14,36,14,40,-10,60,10,5,out hit),"rounded corner avoids square false positive");
        Check(FlappyCollision.Sweep(36,13,38,13,40,-10,60,10,5,out hit),"rounded corner contact"); Near(hit,0,"initial tangent contact");
    }
    private static FlappyGame Scenario(double y, params FlappyPipe[] obstacles)
    {
        var game=new FlappyGame(1);
        typeof(FlappyGame).GetProperty("BirdY").SetValue(game,y);
        // Cancel this step's gravity so the event-order fixtures travel horizontally.
        typeof(FlappyGame).GetProperty("BirdVelocity").SetValue(game,FlappyRules.Gravity*FlappyRules.FixedStep);
        var pipes=(List<FlappyPipe>)typeof(FlappyGame).GetField("pipes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
        pipes.Clear();pipes.AddRange(obstacles);return game;
    }
    private static void EventOrderingChecks()
    {
        double y=FlappyRules.PlayHeight*.5, travel=FlappyRules.Speed*FlappyRules.FixedStep;
        foreach(double scoringTime in new[]{.25,.5,.75}) {
            var scoring=new FlappyPipe(1,FlappyRules.BirdX-FlappyRules.BirdRadius-FlappyRules.PipeWidth+travel*scoringTime,y);
            var killing=new FlappyPipe(2,FlappyRules.BirdX+FlappyRules.BirdRadius+travel*.5,y-FlappyRules.GapHeight/2-5);
            var game=Scenario(y,scoring,killing);var result=game.Step(false);
            Check(result.Died,"event fixture collides");Near(game.ElapsedTime,FlappyRules.FixedStep*.5,"stop at collision time");
            int expected=scoringTime<.5?1:0;
            Check(result.Points==expected&&game.Score==expected,"only scores strictly before death at "+scoringTime);
            Near(game.Pipes[1].X,FlappyRules.BirdX+FlappyRules.BirdRadius,"all geometry freezes at contact");
        }
        var one=Scenario(y,new FlappyPipe(1,FlappyRules.BirdX-FlappyRules.BirdRadius-FlappyRules.PipeWidth+travel*.5,y));
        Check(one.Step(false).Points==1&&one.Pipes[0].Passed,"full body crossing awards once");
        for(int i=0;i<10;i++)Check(one.Step(false).Points==0,"passed pipe cannot repeat");
        var cap=Scenario(y,new FlappyPipe(1,FlappyRules.BirdX-FlappyRules.BirdRadius-FlappyRules.PipeWidth+travel*.1,y),
            new FlappyPipe(2,FlappyRules.BirdX-FlappyRules.BirdRadius-FlappyRules.PipeWidth+travel*.3,y));
        typeof(FlappyGame).GetProperty("Score").SetValue(cap,long.MaxValue-1);
        Check(cap.Step(false).Points==1&&cap.Score==long.MaxValue,"score saturates without overflow");
        double ny,nv;FlappyMotion.Predict(y,-FlappyRules.TerminalVelocity,false,out ny,out nv);
        Near(nv,-FlappyRules.TerminalVelocity,"terminal descent bounded");
    }
    private static void CertifiedLongRun()
    {
        foreach(uint seed in new uint[]{0,1,7,0xffffffff}) {
            var game=new FlappyGame(seed);ReplayPlan(game,FlappyReachability.Startup);
            Check(game.Score==1,"certified startup scores");int source=2;bool[] levels=new bool[5];levels[source]=true;
            for(int segment=0;segment<1000;segment++) {
                FlappyPipe next=new FlappyPipe();bool found=false;
                foreach(var pipe in game.Pipes)if(!pipe.Passed){next=pipe;found=true;break;}
                Check(found,"next obstacle available before choosing input");
                int target=(int)Math.Round(next.GapCenter/FlappyRules.PlayHeight*10-3);levels[target]=true;
                Check(Math.Abs(source-target)<=1,"only certified transitions generated");
                ReplayPlan(game,FlappyReachability.Recipe(source,target,game.BirdY));source=target;
                Check(game.Score==segment+2,"each complete crossing counts once: segment="+segment+" actual="+game.Score+" nextX="+game.Pipes[0].X);
                Check(Math.Abs(game.BirdY-next.GapCenter)<=FlappyReachability.EntryTolerance+1e-7,"connector returns invariant interval");
            }
            for(int level=0;level<5;level++)Check(levels[level],"all five levels reached seed "+seed);
        }
    }
    private static void ReplayPlan(FlappyGame game,int[] intervals)
    {
        foreach(int interval in intervals)for(int i=0;i<interval;i++) {
            var result=game.Step(i==0);Check(!result.Died&&!game.GameOver,"certified trajectory alive");
            Check(game.Pipes.Count<=FlappyRules.MaximumPipes,"bounded obstacle storage");
            Check(!double.IsNaN(game.BirdY)&&!double.IsInfinity(game.BirdY),"finite state");
        }
    }

    private static void IdentityWrapChecks()
    {
        var game=new FlappyGame(1);
        typeof(FlappyGame).GetField("nextId",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,int.MaxValue);
        for(int spawn=0;spawn<2;spawn++) {
            typeof(FlappyGame).GetField("spawnCountdown",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,1);
            game.Step(true);
        }
        var ids=new HashSet<int>();foreach(var pipe in game.Pipes)Check(ids.Add(pipe.Id),"pipe identity remains unique after numeric wrap");
    }
    private static void ConnectorBoundaryChecks()
    {
        for(int source=0;source<5;source++)for(int target=Math.Max(0,source-1);target<=Math.Min(4,source+1);target++)
            foreach(double error in new[]{-2.0,-1.0,0,1.0,2.0}) {
                double y=FlappyReachability.Center(source)+error,v=0;
                foreach(int interval in FlappyReachability.Recipe(source,target,y))for(int tick=0;tick<interval;tick++) {
                    double ny,nv;FlappyMotion.Predict(y,v,tick==0,out ny,out nv);y=ny;v=nv;
                }
                Check(Math.Abs(y-FlappyReachability.Center(target))<=2+1e-8,"invariant boundary has a legal connector");
            }
    }

    private static void VisiblePipeGeometryChecks()
    {
        double gap=FlappyRules.PlayHeight*.5,travel=FlappyRules.Speed*FlappyRules.FixedStep;
        foreach(double y in new[]{100.0,450.0}) {
            var safe=Scenario(y,new FlappyPipe(1,FlappyRules.BirdX+FlappyRules.BirdRadius,gap));
            Check(!safe.Step(false).Died,"transparent margin outside narrow pipe stem is safe");
            var hit=Scenario(y,new FlappyPipe(1,FlappyRules.BirdX+FlappyRules.BirdRadius-5.45+travel*.5,gap));
            Check(hit.Step(false).Died,"visible pipe stem still collides");Near(hit.ElapsedTime,FlappyRules.FixedStep*.5,"stem contact follows visible inset");
        }
        foreach(double y in new[]{gap-FlappyRules.GapHeight/2-5,gap+FlappyRules.GapHeight/2+5}) {
            var cap=Scenario(y,new FlappyPipe(1,FlappyRules.BirdX+FlappyRules.BirdRadius+travel*.5,gap));
            Check(cap.Step(false).Died,"full width endcap collides");Near(cap.ElapsedTime,FlappyRules.FixedStep*.5,"cap contact retains full width");
        }
    }

    private static void GentleFlightChecks()
    {
        // Physical-device feedback: slower descent and more time to plan the next pipe.
        var game=new FlappyGame(1);double initialY=game.BirdY,peak=game.BirdY;int peakTick=0,returnTick=0,tick=0;
        while(!game.GameOver&&tick<180) {
            game.Step(tick==0);tick++;
            if(game.BirdY>peak){peak=game.BirdY;peakTick=tick;}
            if(returnTick==0&&peakTick>0&&game.BirdY<initialY)returnTick=tick;
        }
        Check(peakTick>=20&&peakTick<=23,"gentler flap gives at least a third of a second before apex");
        Check(game.ElapsedTime>=1.25&&game.ElapsedTime<=1.35,"first unassisted flap allows about 1.27 seconds before ground");
        Check(returnTick==43,"an isolated impulse stays above its starting height for about 0.717 seconds");
        Near(FlappyRules.Gravity/FlappyRules.PlayHeight,2.2,"approved gentler gravity");
        Near(FlappyRules.FlapVelocity/FlappyRules.PlayHeight,.8,"approved flap impulse");
        Near(FlappyRules.TerminalVelocity/FlappyRules.PlayHeight,.95,"approved descent speed cap");
        Near(FlappyRules.Speed/FlappyRules.Width,.30,"approved slower pipes");
        Near(FlappyRules.GapHeight/FlappyRules.PlayHeight,.32,"approved wider gap");
        var fresh=new FlappyGame(1);
        double firstContact=(fresh.Pipes[0].X-FlappyRules.BirdX-FlappyRules.BirdRadius)/FlappyRules.Speed;
        Check(firstContact>=2.27&&firstContact<=2.29,"first obstacle leaves over 2.27 seconds to react");
        Near(FlappyRules.PipeIntervalTicks*FlappyRules.FixedStep,2.2,"extra time between pipes");
        double freeWindow=FlappyRules.PipeIntervalTicks*FlappyRules.FixedStep-(FlappyRules.PipeWidth+2*FlappyRules.BirdRadius)/FlappyRules.Speed;
        Check(freeWindow>=1.27,"at least 1.27 seconds between full-width obstacle-overlap windows");
        Near(FlappyRules.Speed*FlappyRules.PipeIntervalTicks*FlappyRules.FixedStep,237.6,"slower motion does not squeeze pipe spacing");
        Near(FlappyRules.FirstPassTick*FlappyRules.FixedStep,3.2,"first full crossing time");
        Near(FlappyRules.GapHeight/2-FlappyRules.BirdRadius,69.816,"wider vertical center clearance");
    }

}
