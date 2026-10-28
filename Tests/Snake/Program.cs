using System;
using System.Linq;
using XanaduYonder.Core.Snake;
using XanaduYonder.Runtime.Snake;
internal static class Program
{
    private static int passed, failed;
    private static int Main()
    {
        Check("classic defaults and six rule variants", () => {
            int[] starts={240,200,160}, mins={160,100,80};
            for(int d=0;d<3;d++) foreach(SnakeBoundary b in Enum.GetValues(typeof(SnakeBoundary))) {
                var r=SnakeRules.Classic(d,b); Eq(r.Width,20);Eq(r.Height,24);Eq(r.Capacity,480);Eq(r.InitialTickMs,starts[d]);Eq(r.MinimumTickMs,mins[d]);
                var g=new SnakeGame(r,3); Seq(g.Body,new[]{406,405,404,403});Eq(g.State,SnakeState.Playing);Assert(!g.Body.Contains(g.Food)); }
        });
        Check("four wrap directions normalize landing", () => {
            var r=Small(SnakeBoundary.Wrap,5,5);
            Wrap(r,new[]{4,3,2,1},SnakeDirection.Right,0); Wrap(r,new[]{0,1,2,3},SnakeDirection.Left,4);
            Wrap(r,new[]{0,5,10,15},SnakeDirection.Up,20); Wrap(r,new[]{20,15,10,5},SnakeDirection.Down,0);
        });
        Check("solid edges lose without reducing capacity", () => {
            var r=Small(SnakeBoundary.Solid,5,5); Eq(r.Capacity,25);
            var g=Game(r,new[]{4,3,2,1},SnakeDirection.Right,24);g.Step(SnakeDirection.Right);Eq(g.State,SnakeState.Lost);Seq(g.Body,new[]{4,3,2,1});Eq(g.TickCount,101L);
        });
        Check("wrap landing obstacle still loses", () => {
            var r=Small(SnakeBoundary.Wrap,5,5);r.Obstacles=new[]{0};var g=Game(r,new[]{4,3,2,1},SnakeDirection.Right,24);g.Step(SnakeDirection.Right);Eq(g.State,SnakeState.Lost);Eq(r.Capacity,24);
        });
        Check("wrap landing occupied non-tail still loses", () => {
            var r=Small(SnakeBoundary.Wrap,5,5);var g=Game(r,new[]{4,9,5,0,1},SnakeDirection.Up,24);g.Step(SnakeDirection.Right);Eq(g.State,SnakeState.Lost);
        });
        Check("non-growing move can enter vacating tail", () => {
            var g=Game(Small(),new[]{6,7,12,11},SnakeDirection.Left,24);g.Step(SnakeDirection.Down);Eq(g.State,SnakeState.Playing);Seq(g.Body,new[]{11,6,7,12});
        });
        Check("food grows immediately and awards ten", () => {
            var g=Game(Small(),new[]{8,7,6,5},SnakeDirection.Right,9);g.Step(SnakeDirection.Right);Seq(g.Body,new[]{9,8,7,6,5});Eq(g.FoodEaten,1);Eq(g.Score,10);Assert(!g.Body.Contains(g.Food));
        });
        Check("reverse command cannot reverse core movement", () => {
            var g=Game(Small(),new[]{8,7,6,5},SnakeDirection.Right,24);g.Step(SnakeDirection.Left);Eq(g.Body[0],9);Eq(g.GetSnapshot().Direction,SnakeDirection.Right);
        });
        Check("ordinary movement leaves RNG and food unchanged", () => {
            var g=Game(Small(),new[]{8,7,6,5},SnakeDirection.Right,24);g.Step(SnakeDirection.Right);Eq(g.RandomState,123u);Eq(g.Food,24);
        });
        Check("last food wins full board before target or RNG", () => {
            var r=Small(SnakeBoundary.Solid,3,2);r.InitialBody=new[]{1,0,3,4};r.Mode=SnakeMode.Campaign;r.LevelId="last";r.TargetFood=2;
            var g=Game(r,new[]{1,0,3,4,5},SnakeDirection.Right,2);g.Step(SnakeDirection.Right);Eq(g.State,SnakeState.WonFullBoard);Eq(g.Body.Length,6);Eq(g.Food,-1);Eq(g.RandomState,123u);Eq(g.FoodEaten,2);Eq(g.Score,20);
            var snap=g.GetSnapshot();Assert(snap.TryValidate(r,out var reason),reason);g.Step(SnakeDirection.Down);Eq(g.TickCount,snap.TickCount);Eq(g.RandomState,snap.RandomState);
        });
        Check("full board subtracts obstacles", () => {
            var r=Small(SnakeBoundary.Solid,3,2);r.InitialBody=new[]{1,0,3,4};r.Obstacles=new[]{5};
            var g=Game(r,new[]{1,0,3,4},SnakeDirection.Right,2);g.Step(SnakeDirection.Right);Eq(g.State,SnakeState.WonFullBoard);Eq(g.Body.Length,5);Eq(g.RandomState,123u);
        });
        Check("target win stops food generation before full board", () => {
            var r=Small();r.Mode=SnakeMode.Campaign;r.LevelId="test";r.TargetFood=1;
            var g=Game(r,new[]{8,7,6,5},SnakeDirection.Right,9);g.Step(SnakeDirection.Right);Eq(g.State,SnakeState.WonTarget);Eq(g.Food,-1);Eq(g.RandomState,123u);Eq(g.CurrentTickMilliseconds,200);
        });
        Check("food samples all free cells through unbiased rank", () => {
            var r=Small(SnakeBoundary.Wrap,3,2);r.InitialBody=new[]{1,0,3,4};int a=0,b=0;
            for(uint seed=1;seed<=20000;seed++){var g=new SnakeGame(r,seed);if(g.Food==2)a++;else if(g.Food==5)b++;else throw new Exception("occupied food");}
            Assert(Math.Abs(a-b)<400,"food rank distribution skewed");Assert(a>0&&b>0);
        });
        Check("same snapshot replays deterministic RNG and food", () => {
            var r=Small(SnakeBoundary.Wrap);var a=Game(r,new[]{8,7,6,5},SnakeDirection.Right,9);var b=new SnakeGame(r,a.GetSnapshot());a.Step(SnakeDirection.Right);b.Step(SnakeDirection.Right);Eq(a.Food,b.Food);Eq(a.RandomState,b.RandomState);Seq(a.Body,b.Body);
        });
        Check("game isolates rules, restore snapshot and exported arrays", () => {
            var r=Small();var s=Snapshot(new[]{8,7,6,5},SnakeDirection.Right,24);var g=new SnakeGame(r,s);r.InitialBody[0]=0;r.Obstacles=new[]{9};s.Body[0]=0;g.Body[0]=0;g.GetSnapshot().Body[0]=0;g.Rules.InitialBody[0]=0;Eq(g.Body[0],8);g.Step(SnakeDirection.Right);Eq(g.State,SnakeState.Playing);
        });
        Check("snapshot rejects structural and counter corruption", () => {
            var r=Small();var s=Snapshot(new[]{8,7,6,5},SnakeDirection.Right,24);
            Bad(r,s,x=>x.Body=null);Bad(r,s,x=>x.Body[1]=x.Body[0]);Bad(r,s,x=>x.Body[1]=20);Bad(r,s,x=>x.Food=8);Bad(r,s,x=>x.Food=-1);Bad(r,s,x=>x.Direction=(SnakeDirection)99);Bad(r,s,x=>x.State=(SnakeState)99);Bad(r,s,x=>x.FoodEaten=1);Bad(r,s,x=>x.Score=10);Bad(r,s,x=>x.RandomState=0);Bad(r,s,x=>x.TickCount=-1);Bad(r,s,x=>x.State=SnakeState.WonFullBoard);Bad(r,s,x=>{x.State=SnakeState.WonTarget;x.Food=-1;});
        });
        Check("non-lost snapshot direction must follow neck to head", () => {
            var r=Small();var s=Snapshot(new[]{8,7,6,5},SnakeDirection.Right,24);
            Bad(r,s,x=>x.Direction=SnakeDirection.Left);Bad(r,s,x=>x.Direction=SnakeDirection.Down);
            r.Mode=SnakeMode.Campaign;r.LevelId="direction";r.TargetFood=1;
            var g=Game(r,new[]{8,7,6,5},SnakeDirection.Right,9);g.Step(SnakeDirection.Right);
            Bad(r,g.GetSnapshot(),x=>x.Direction=SnakeDirection.Up);
        });
        Check("zero-tick initial snapshots preserve right-facing geometry", () => {
            var r=Small(SnakeBoundary.Wrap);r.InitialBody=new[]{0,4,3,2};var g=new SnakeGame(r,4);
            Assert(g.GetSnapshot().TryValidate(r,out var reason),reason);Eq(g.TickCount,0L);
            var wrong=Small();wrong.InitialBody=new[]{5,6,7,8};Throws(()=>new SnakeGame(wrong,1));
            var tiny=Small(SnakeBoundary.Wrap,2,2);tiny.InitialBody=new[]{1,0,2,3};var full=new SnakeGame(tiny,4);
            Assert(full.GetSnapshot().TryValidate(tiny,out reason),reason);Eq(full.State,SnakeState.WonFullBoard);
        });
        Check("lost snapshots have no remaining food", () => {
            var r=Small();var g=Game(r,new[]{4,3,2,1},SnakeDirection.Right,24);g.Step(SnakeDirection.Right);
            Assert(g.GetSnapshot().TryValidate(r,out var reason),reason);Bad(r,g.GetSnapshot(),x=>x.Food=24);
        });
        Check("campaign cannot eat beyond its target before full-board win", () => {
            var r=Small(SnakeBoundary.Solid,3,2);r.InitialBody=new[]{1,0,3,4};r.Mode=SnakeMode.Campaign;r.LevelId="prior-target";r.TargetFood=1;
            var s=Snapshot(new[]{2,1,0,3,4,5},SnakeDirection.Right,-1);s.State=SnakeState.WonFullBoard;
            Assert(!s.TryValidate(r,out var reason),"full board cannot excuse an earlier target win");Throws(()=>new SnakeGame(r,s));
        });
        Check("snapshot adjacency respects wrap but not solid", () => {
            var s=Snapshot(new[]{0,4,3,2},SnakeDirection.Right,24);Assert(s.TryValidate(Small(SnakeBoundary.Wrap),out var reason),reason);Assert(!s.TryValidate(Small(),out reason));
        });
        Check("zero seed normalized but bad rules rejected", () => {
            Assert(new SnakeGame(Small(),0).RandomState!=0);Throws(()=>SnakeRules.Classic(3,SnakeBoundary.Wrap));
            var r=Small();r.Obstacles=new[]{0,0};Throws(()=>new SnakeGame(r,1));r=Small();r.InitialBody=new[]{8,7,6,25};Throws(()=>new SnakeGame(r,1));
        });
        Check("classic speed thresholds and minimum are exact", () => {
            int[] starts={240,200,160}, mins={160,100,80};for(int d=0;d<3;d++)foreach(int food in new[]{0,4,5,9,10,100,200}) {
                var r=SnakeRules.Classic(d,SnakeBoundary.Wrap);var body=Serpentine(20,24).Take(4+food).ToArray();var s=Snapshot(body,SnakeDirection.Left,479);var g=new SnakeGame(r,s);Eq(g.CurrentTickMilliseconds,Math.Max(mins[d],starts[d]-(food/5)*5)); }
        });
        Check("session begins paused and countdown excludes time and movement", () => {
            var s=Session();Assert(s.Paused&&!s.CanPlay);Assert(!s.QueueDirection(SnakeDirection.Up));s.Resume();Eq(s.CountdownSeconds,3.0);Assert(!s.CanPlay);for(int i=0;i<12;i++)Eq(s.Advance(.25),0);Eq(s.CountdownSeconds,0.0);Eq(s.ElapsedSeconds,0.0);Assert(s.CanPlay);Eq(s.Advance(.2),1);Near(s.ElapsedSeconds,.2);
        });
        Check("countdown completion never advances in same frame", () => {
            var s=Session();s.Resume();for(int i=0;i<11;i++)s.Advance(.25);s.Advance(.2);Eq(s.Advance(.2),0);Eq(s.Game.TickCount,100L);Near(s.ElapsedSeconds,0);Eq(s.Advance(.2),1);
        });
        Check("queue accepts two turns relative to tail and one per tick", () => {
            var s=Running();Assert(!s.QueueDirection(SnakeDirection.Right));Assert(!s.QueueDirection(SnakeDirection.Left));Assert(s.QueueDirection(SnakeDirection.Up));Assert(!s.QueueDirection(SnakeDirection.Down));Assert(s.QueueDirection(SnakeDirection.Left));Assert(!s.QueueDirection(SnakeDirection.Down));Eq(s.Advance(.2),1);Eq(s.Game.Body[0],3);Eq(s.Advance(.2),1);Eq(s.Game.Body[0],2);
        });
        Check("normal frames retain fractional time and tick at boundary", () => {
            var s=Running();Eq(s.Advance(.13),0);Eq(s.Advance(.07),1);Eq(s.Advance(.05),0);Eq(s.Advance(.15),1);Eq(s.Game.TickCount,102L);Near(s.ElapsedSeconds,.4);
        });
        Check("pause clears queued turns and accumulator", () => {
            var s=Running();s.QueueDirection(SnakeDirection.Up);s.Advance(.15);s.Pause();Assert(s.Paused);Resume(s);Eq(s.Advance(.1),0);Eq(s.Advance(.1),1);Eq(s.Game.Body[0],9);Near(s.ElapsedSeconds,.35);
        });
        Check("clear input removes pending turns without pausing", () => {
            var s=Running();s.QueueDirection(SnakeDirection.Up);s.ClearInput();Assert(s.CanPlay);s.Advance(.2);Eq(s.Game.Body[0],9);
        });
        Check("slow frame pauses atomically without movement or time", () => {
            var s=Running();s.QueueDirection(SnakeDirection.Up);s.Advance(.1);Eq(s.Advance(.251),0);Assert(s.Paused);Eq(s.Game.TickCount,100L);Near(s.ElapsedSeconds,.1);Resume(s);s.Advance(.2);Eq(s.Game.Body[0],9);
        });
        Check("more than two overdue ticks pauses before running any", () => {
            var r=Small(SnakeBoundary.Wrap);r.InitialTickMs=r.MinimumTickMs=80;var s=Running(r);Eq(s.Advance(.25),0);Assert(s.Paused);Eq(s.Game.TickCount,100L);Near(s.ElapsedSeconds,0);
        });
        Check("speed change cannot hide a third due tick from preflight", () => {
            var r=Small(SnakeBoundary.Wrap,10,10);r.InitialTickMs=6;r.MinimumTickMs=1;
            var s=new SnakeSession(Game(r,new[]{57,56,55,54,53,52,51,50},SnakeDirection.Right,58));Resume(s);
            Eq(s.Advance(.008),0);Assert(s.Paused);Eq(s.Game.TickCount,100L);Eq(s.Game.RandomState,123u);Near(s.ElapsedSeconds,0);
        });
        Check("two due ticks run and normal frame does not drop time", () => {
            var r=Small(SnakeBoundary.Wrap);r.InitialTickMs=r.MinimumTickMs=100;var s=Running(r);Eq(s.Advance(.25),2);Assert(!s.Paused);Eq(s.Game.TickCount,102L);Near(s.ElapsedSeconds,.25);Eq(s.Advance(.05),1);
        });
        Check("terminal freezes elapsed at actual tick and ignores input", () => {
            var r=Small();r.Mode=SnakeMode.Campaign;r.LevelId="one";r.TargetFood=1;var s=new SnakeSession(Game(r,new[]{8,7,6,5},SnakeDirection.Right,9));Resume(s);Eq(s.Advance(.25),1);Eq(s.Game.State,SnakeState.WonTarget);Near(s.ElapsedSeconds,.2);Assert(!s.CanPlay&&!s.QueueDirection(SnakeDirection.Up));s.Advance(.2);s.Resume();Near(s.ElapsedSeconds,.2);s.MarkResultRecorded();s.MarkResultRecorded();Assert(s.ResultRecorded);
        });
        Check("restore retains identity and time but starts paused with empty input", () => {
            var s=Running();s.Advance(.1);s.QueueDirection(SnakeDirection.Up);var d=s.Export();var restored=new SnakeSession(s.Game.Rules,d);Assert(restored.Paused);Eq(restored.SessionId,s.SessionId);Near(restored.ElapsedSeconds,.1);Resume(restored);restored.Advance(.2);Eq(restored.Game.Body[0],9);d.Board.Body[0]=0;Eq(restored.Game.Body[0],9);
        });
        Check("ordinary due ticks do not clone and revalidate the board", () => {
            var s=Running();s.Advance(.2);long before=GC.GetAllocatedBytesForCurrentThread();int moved=0;
            for(int i=0;i<100;i++)moved+=s.Advance(.2);
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;Eq(moved,100);
            Assert(allocated<20000,"ordinary ticks allocated "+allocated+" bytes; expected only moved bodies");
        });
        Check("session rejects invalid time and inconsistent result marker", () => {
            var s=Session();Throws(()=>s.Advance(double.NaN));Throws(()=>s.Advance(double.PositiveInfinity));Throws(()=>s.Advance(-.1));Throws(()=>s.MarkResultRecorded());var d=s.Export();d.ElapsedSeconds=double.NaN;Throws(()=>new SnakeSession(s.Game.Rules,d));d=s.Export();d.SessionId="bad";Throws(()=>new SnakeSession(s.Game.Rules,d));d=s.Export();d.ResultRecorded=true;Throws(()=>new SnakeSession(s.Game.Rules,d));
        });
        Console.WriteLine("Snake rules/session: "+passed+" passed, "+failed+" failed");return failed==0?0:1;
    }
    private static SnakeRules Small(SnakeBoundary boundary=SnakeBoundary.Solid,int width=5,int height=5){return new SnakeRules{Width=width,Height=height,Mode=SnakeMode.Classic,Difficulty=1,Boundary=boundary,InitialTickMs=200,MinimumTickMs=100,InitialBody=new[]{8,7,6,5},Obstacles=new int[0]};}
    private static SnakeSnapshot Snapshot(int[] body,SnakeDirection direction,int food){return new SnakeSnapshot{Body=body,Direction=direction,Food=food,FoodEaten=body.Length-4,Score=(body.Length-4)*10,TickCount=Math.Max(100,body.Length-4),RandomState=123,State=SnakeState.Playing};}
    private static SnakeGame Game(SnakeRules r,int[] body,SnakeDirection dir,int food){return new SnakeGame(r,Snapshot(body,dir,food));}
    private static void Wrap(SnakeRules r,int[] body,SnakeDirection dir,int target){var g=Game(r,body,dir,24);g.Step(dir);Eq(g.Body[0],target);Eq(g.State,SnakeState.Playing);}
    private static SnakeSession Session(SnakeRules r=null){return new SnakeSession(Game(r??Small(SnakeBoundary.Wrap),new[]{8,7,6,5},SnakeDirection.Right,24));}
    private static SnakeSession Running(SnakeRules r=null){var s=Session(r);Resume(s);return s;}
    private static void Resume(SnakeSession s){s.Resume();for(int i=0;i<12;i++)s.Advance(.25);Assert(s.CanPlay);}
    private static int[] Serpentine(int width,int height){var a=new int[width*height];for(int y=0;y<height;y++)for(int x=0;x<width;x++)a[y*width+x]=y*width+(y%2==0?x:width-1-x);return a;}
    private static void Bad(SnakeRules r,SnakeSnapshot s,Action<SnakeSnapshot> mutate){var copy=s.Copy();mutate(copy);Assert(!copy.TryValidate(r,out var reason),"invalid snapshot accepted");Throws(()=>new SnakeGame(r,copy));}
    private static void Throws(Action a){try{a();}catch(ArgumentException){return;}catch(InvalidOperationException){return;}throw new Exception("Expected argument/state rejection");}
    private static void Seq(int[] a,int[] b){Assert(a.SequenceEqual(b),"different body");}
    private static void Near(double a,double b){Assert(Math.Abs(a-b)<1e-9,a+" != "+b);}
    private static void Eq<T>(T a,T b){Assert(Equals(a,b),a+" != "+b);}
    private static void Assert(bool value,string reason="Assertion failed"){if(!value)throw new Exception(reason);}
    private static void Check(string name,Action test){try{test();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.GetType().Name+": "+e.Message);}}
}
