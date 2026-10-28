using System;
using System.Collections.Generic;
using XanaduYonder.Core.FlappyBird;
internal static class Program
{
    private static int count;
    private static void Check(bool value,string message) { if(!value)throw new Exception(message);count++; }
    private static void Near(double a,double b,string message) { Check(Math.Abs(a-b)<1e-8,message); }
    private static int Main()
    {
        try {
            var s=new FlappySession(1); Check(s.Phase==FlappyPhase.Ready,"entry is ready"); s.Advance(.1); Near(s.Game.ElapsedTime,0,"ready frozen");
            s.Press();s.Press();var r=s.Advance(FlappyRules.FixedStep);Check(s.Phase==FlappyPhase.Running&&r.Flapped,"first tap starts and flaps once");
            r=s.Advance(FlappyRules.FixedStep);Check(!r.Flapped,"tap consumed once");
            s.Press();s.Suspend();Check(s.Phase==FlappyPhase.Paused,"running suspend");s.Resume();Check(s.Phase==FlappyPhase.Countdown&&s.CountdownNumber==3,"full countdown");
            for(int i=0;i<60;i++)s.Advance(FlappyRules.FixedStep);Check(s.CountdownNumber==2,"two remains");s.Press();s.Suspend();s.Resume();Check(s.CountdownNumber==3,"countdown interruption restarts");
            for(int i=0;i<180;i++)s.Advance(FlappyRules.FixedStep);Check(s.Phase==FlappyPhase.Running,"three seconds resume");r=s.Advance(FlappyRules.FixedStep);Check(!r.Flapped,"paused input never leaks");
            s.Press();s.Advance(.101);Check(s.Phase==FlappyPhase.Paused,"long frame pauses");
            var ready=new FlappySession(2);ready.Suspend();ready.Resume();Check(ready.Phase==FlappyPhase.Ready,"ready interruption stable");
            var dead=new FlappySession(3);dead.Press();for(int i=0;i<120&&!dead.Game.GameOver;i++)dead.Advance(FlappyRules.FixedStep);Check(dead.Phase==FlappyPhase.Dying,"death presentation phase");dead.Restart(8);Check(dead.Phase==FlappyPhase.Dying,"restart gate during death");dead.Suspend();Check(dead.Phase==FlappyPhase.Result,"interrupt death settles result");Check(!dead.CanRestart,"death guard applies on immediate interruption");
            for(int i=0;i<30;i++)dead.Advance(FlappyRules.FixedStep);Check(dead.CanRestart,"restart unlocks after guard");dead.Restart(9);Check(dead.Phase==FlappyPhase.Ready&&!dead.Game.GameOver,"restart stays ready");
            PartitionChecks();
            DeathTimingChecks();
            ExtraLifecycleChecks();
            LivingTimedReplayChecks();
            Console.WriteLine("Flappy session checks: "+count+" passed, 0 failed.");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static void PartitionChecks()
    {
        FlappySession baseline=null;
        foreach(int hz in new[]{30,60,90,120}) {
            var s=new FlappySession(18);s.Press();
            for(int frame=0;frame<hz*10;frame++) { if(frame>0&&frame%(hz/3)==0)s.Press();s.Advance(1.0/hz); }
            if(baseline==null)baseline=s;
            else { Near(s.Game.ElapsedTime,baseline.Game.ElapsedTime,"partition time "+hz);Near(s.Game.BirdY,baseline.Game.BirdY,"partition height "+hz);Check(s.Game.Score==baseline.Game.Score&&s.Phase==baseline.Phase,"partition outcome "+hz); }
        }
    }
    private static void DeathTimingChecks()
    {
        foreach(int hz in new[]{30,60,90,120}) {
            var s=new FlappySession(1);s.Press();double wall=0;
            while(!s.Game.GameOver){s.Advance(1.0/hz);wall+=1.0/hz;}
            double resultAt=s.Game.ElapsedTime+FlappyRules.DyingSeconds;
            while(wall+.01<resultAt){s.Advance(.01);wall+=.01;}
            s.Advance(resultAt-wall);
            Check(s.Phase==FlappyPhase.Result,"death presentation completes relative to exact collision "+hz);
            Check(!s.CanRestart,"result starts its own visible guard");
            for(int i=0;i<20;i++)s.Advance(FlappyRules.FixedStep);
            Check(!s.CanRestart,"guard still blocks before 350ms result exposure");
            s.Advance(FlappyRules.FixedStep);Check(s.CanRestart,"guard unlocks after 350ms result exposure");
            s.Suspend();Check(s.CanRestart,"repeated result interruption does not reset guard");
        }
    }
    private static void ExtraLifecycleChecks()
    {
        foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,-.01,.2}) {
            var s=new FlappySession(3);s.Press();s.Advance(.01);s.Press();s.Advance(invalid);
            Check(s.Phase==FlappyPhase.Paused,"invalid clock pauses");Near(s.Game.ElapsedTime,0,"invalid clock drops accumulated partial step");
            s.Resume();for(int i=0;i<30;i++)s.Advance(.1);
            Check(!s.Advance(FlappyRules.FixedStep).Flapped,"stale pending press cleared");
        }
        var paused=new FlappySession(4);paused.Press();paused.Suspend();paused.Restart(5);
        Check(paused.Phase==FlappyPhase.Ready,"paused restart allowed");
        Check(!paused.Advance(.1).Flapped,"restart never flaps");
        var cleared=new FlappySession(6);cleared.Press();cleared.ClearInput();Check(!cleared.Advance(FlappyRules.FixedStep).Flapped,"explicit input clear");
        var many=new FlappySession(7);many.Press();var result=many.Advance(.1);Check(result.Flapped,"batched frame aggregate includes first flap");
        Near(many.Game.ElapsedTime,.1,"six fixed steps consumed");
        Check(!many.Advance(0).Flapped,"zero frame cannot repeat input");
    }

    private static void LivingTimedReplayChecks()
    {
        var reference=new FlappyGame(71);var presses=new List<long>();long ticks=0;
        RecordPlan(reference,FlappyReachability.Startup,presses,ref ticks);int source=2;
        for(int segment=0;segment<100;segment++) {
            FlappyPipe next=new FlappyPipe();foreach(var pipe in reference.Pipes)if(!pipe.Passed){next=pipe;break;}
            int target=(int)Math.Round(next.GapCenter/FlappyRules.PlayHeight*10-3);
            RecordPlan(reference,FlappyReachability.Recipe(source,target,reference.BirdY),presses,ref ticks);source=target;
        }
        foreach(int hz in new[]{30,60,90,120}) {
            var s=new FlappySession(71);int next=0;double wall=0,total=ticks*FlappyRules.FixedStep;
            // Deliver the same timestamped input stream, splitting a render frame when
            // it contains an event. Render polling cannot reconstruct missing timestamps.
            for(int frame=0;wall<total-1e-9;frame++) {
                double end=Math.Min(total,(frame+1.0)/hz);
                while(next<presses.Count&&presses[next]*FlappyRules.FixedStep<=end+1e-10) {
                    double eventAt=presses[next]*FlappyRules.FixedStep;
                    s.Advance(Math.Max(0,eventAt-wall));s.Press();wall=eventAt;next++;
                }
                s.Advance(Math.Max(0,end-wall));wall=end;
            }
            Check(s.Phase==FlappyPhase.Running&&!s.Game.GameOver,"long timestamped replay alive "+hz);
            Check(s.Game.Score==101,"long replay scores match "+hz);
            Near(s.Game.BirdY,reference.BirdY,"long replay height matches "+hz);
            Near(s.Game.BirdVelocity,reference.BirdVelocity,"long replay velocity matches "+hz);
            Near(s.Game.ElapsedTime,reference.ElapsedTime,"long replay clock matches "+hz);
            Check(s.Game.Pipes.Count==reference.Pipes.Count,"long replay obstacle count matches "+hz);
            for(int i=0;i<s.Game.Pipes.Count;i++) { Near(s.Game.Pipes[i].X,reference.Pipes[i].X,"long replay obstacle positions match");Near(s.Game.Pipes[i].GapCenter,reference.Pipes[i].GapCenter,"long replay random sequence matches"); }
        }
    }
    private static void RecordPlan(FlappyGame game,int[] intervals,List<long> presses,ref long ticks)
    {
        foreach(int interval in intervals)for(int i=0;i<interval;i++) { if(i==0)presses.Add(ticks);Check(!game.Step(i==0).Died,"input timeline is a legal production trajectory");ticks++; }
    }

}
