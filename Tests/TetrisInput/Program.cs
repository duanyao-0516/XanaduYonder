using System;
using System.Collections.Generic;
using XanaduYonder.Core.Tetris;
using XanaduYonder.Runtime.Tetris;
class Program
{
 static int passed,failed;
 static void Test(string name,Action body){try{body();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static void Equal<T>(T a,T b){if(!EqualityComparer<T>.Default.Equals(a,b))throw new Exception("expected "+a+", got "+b);}
 static TetrisPointer P(int id,TetrisAction a,TetrisPointerPhase p=TetrisPointerPhase.Began,bool inside=true){return new TetrisPointer(id,a,p,inside);}
 static void Frame(TetrisInput i,long serial,params TetrisPointer[] p){i.ProcessFrame(p,true,serial);}
 static TetrisSession New(){return new TetrisSession(new TetrisGame(42),false);}
 static void Start(TetrisSession s){s.StartOrResume();Frame(s.Input,s.Snapshot.PieceSerial);}
 static void Tap(TetrisSession s,int id=4,double duration=.05){var serial=s.Snapshot.PieceSerial;Frame(s.Input,serial,P(id,TetrisAction.SoftDrop));s.Advance(duration);Frame(s.Input,s.Snapshot.PieceSerial,P(id,TetrisAction.SoftDrop,TetrisPointerPhase.Ended));s.Advance(0);}
 static void Wait(TetrisSession s,double seconds){while(seconds>.2){s.Advance(.2);seconds-=.2;}s.Advance(seconds);}
 static void Main(){
 Test("ready does not advance",()=>{var s=New();s.Advance(.2);Equal(0L,s.Snapshot.ElapsedTicks);});
 Test("running fixed step matches split frame",()=>{var a=New();var b=New();a.StartOrResume();b.StartOrResume();for(int i=0;i<100;i++){a.Advance(.02);b.Advance(.01);b.Advance(.01);}Equal(a.Snapshot.ElapsedTicks,b.Snapshot.ElapsedTicks);Equal(a.Snapshot.Y,b.Snapshot.Y);Equal(120L,a.Snapshot.ElapsedTicks);});
 Test("pause freezes and continue counts down",()=>{var s=New();Start(s);s.Advance(.1);s.Pause();var t=s.Snapshot.ElapsedTicks;for(int i=0;i<50;i++)s.Advance(.1);Equal(t,s.Snapshot.ElapsedTicks);Start(s);Equal(TetrisSessionPhase.Countdown,s.Phase);for(int i=0;i<29;i++)s.Advance(.1);Equal(t,s.Snapshot.ElapsedTicks);s.Advance(.1);Equal(TetrisSessionPhase.Running,s.Phase);s.Advance(.1);Equal(t+6,s.Snapshot.ElapsedTicks);});
 Test("long frame auto pauses without catchup",()=>{var s=New();Start(s);s.Advance(3);Equal(TetrisSessionPhase.Paused,s.Phase);Equal(0L,s.Snapshot.ElapsedTicks);});
 Test("invalid delta rejected without time mutation",()=>{var s=New();Start(s);foreach(var v in new[]{double.NaN,double.PositiveInfinity,-1d}){try{s.Advance(v);throw new Exception("accepted");}catch(ArgumentOutOfRangeException){}}Equal(0L,s.Snapshot.ElapsedTicks);});
 Test("press is immediate and repeated only after DAS",()=>{var s=New();Start(s);int x=s.Snapshot.X;Frame(s.Input,s.Snapshot.PieceSerial,P(1,TetrisAction.Left));s.Advance(0);Equal(x-1,s.Snapshot.X);s.Advance(.16);Equal(x-1,s.Snapshot.X);s.Advance(.03);Equal(x-2,s.Snapshot.X);});
 Test("second direction takes priority",()=>{var s=New();Start(s);Frame(s.Input,s.Snapshot.PieceSerial,P(1,TetrisAction.Left));s.Advance(0);Frame(s.Input,s.Snapshot.PieceSerial,P(1,TetrisAction.Left,TetrisPointerPhase.Stationary),P(2,TetrisAction.Right));s.Advance(0);Equal(3,s.Snapshot.X);s.Advance(.2);Equal(4,s.Snapshot.X);});
 Test("third contact cancels whole batch before hard drop",()=>{var s=New();Start(s);long serial=s.Snapshot.PieceSerial;Frame(s.Input,serial,P(1,TetrisAction.HardDrop),P(2,TetrisAction.Clockwise),P(3,TetrisAction.None));s.Advance(0);Equal(serial,s.Snapshot.PieceSerial);Equal(0,s.Snapshot.Rotation);});
 Test("hard drop invalidates same-frame rotation",()=>{var s=New();Start(s);long serial=s.Snapshot.PieceSerial;Frame(s.Input,serial,P(1,TetrisAction.HardDrop),P(2,TetrisAction.Clockwise));s.Advance(0);Equal(serial+1,s.Snapshot.PieceSerial);Equal(0,s.Snapshot.Rotation);});
 Test("cancel never fires and held touch cannot rearm",()=>{var s=New();Start(s);Frame(s.Input,s.Snapshot.PieceSerial,P(1,TetrisAction.HardDrop,TetrisPointerPhase.Canceled));s.Advance(0);long serial=s.Snapshot.PieceSerial;Frame(s.Input,serial,P(1,TetrisAction.HardDrop,TetrisPointerPhase.Stationary));s.Advance(.1);Equal(serial,s.Snapshot.PieceSerial);Frame(s.Input,serial);Frame(s.Input,serial,P(1,TetrisAction.HardDrop));s.Advance(0);Equal(serial+1,s.Snapshot.PieceSerial);});
 Test("missing up and exit stop repetition",()=>{var s=New();Start(s);Frame(s.Input,s.Snapshot.PieceSerial,P(1,TetrisAction.Left));s.Advance(0);Frame(s.Input,s.Snapshot.PieceSerial);s.Advance(.2);Equal(2,s.Snapshot.X);Frame(s.Input,s.Snapshot.PieceSerial,P(2,TetrisAction.Right));s.Advance(0);Frame(s.Input,s.Snapshot.PieceSerial,P(2,TetrisAction.Right,TetrisPointerPhase.Moved,false));s.Advance(.2);Equal(3,s.Snapshot.X);});
 Test("slide in from outside cannot begin",()=>{var s=New();Start(s);Frame(s.Input,s.Snapshot.PieceSerial,P(1,TetrisAction.None));Frame(s.Input,s.Snapshot.PieceSerial,P(1,TetrisAction.HardDrop,TetrisPointerPhase.Moved));s.Advance(0);Equal(1L,s.Snapshot.PieceSerial);});
 Test("duplicate id cancels frame",()=>{var s=New();Start(s);Frame(s.Input,s.Snapshot.PieceSerial,P(1,TetrisAction.HardDrop),P(1,TetrisAction.Left));s.Advance(0);Equal(1L,s.Snapshot.PieceSerial);});
 Test("soft drop release required after hard drop",()=>{var s=New();Start(s);Frame(s.Input,s.Snapshot.PieceSerial,P(1,TetrisAction.SoftDrop),P(2,TetrisAction.HardDrop));s.Advance(0);s.Advance(.2);Equal(0,s.Snapshot.Y);Equal(false,s.Input.SoftDropHeld);});
 Test("pause cancels pending drop and touch repeat",()=>{var s=New();Start(s);Frame(s.Input,s.Snapshot.PieceSerial,P(1,TetrisAction.HardDrop));s.Pause();Start(s);for(int i=0;i<30;i++)s.Advance(.1);s.Advance(.1);Equal(1L,s.Snapshot.PieceSerial);});
 Test("same-frame opposite directions tie regardless of enumeration",()=>{foreach(bool reverse in new[]{false,true}){var state=new TetrisGame(1).GetSnapshot();state.ActivePiece=TetrisPiece.O;state.X=-1;state.Y=3;var s=new TetrisSession(new TetrisGame(state),false);Start(s);var left=P(1,TetrisAction.Left);var right=P(2,TetrisAction.Right);Frame(s.Input,s.Snapshot.PieceSerial,reverse?right:left,reverse?left:right);s.Advance(0);Equal(-1,s.Snapshot.X);s.Advance(.2);Equal(-1,s.Snapshot.X);Frame(s.Input,s.Snapshot.PieceSerial,P(1,TetrisAction.Left,TetrisPointerPhase.Ended),P(2,TetrisAction.Right,TetrisPointerPhase.Stationary));s.Advance(.2);Equal(0,s.Snapshot.X);}});
 Test("moving onto another button cancels original ownership",()=>{var s=New();Start(s);Frame(s.Input,s.Snapshot.PieceSerial,P(1,TetrisAction.Left));s.Advance(0);Frame(s.Input,s.Snapshot.PieceSerial,P(1,TetrisAction.Right,TetrisPointerPhase.Moved));s.Advance(.2);Equal(2,s.Snapshot.X);});
 Test("three clean down taps hard drop only on third release",()=>{var s=New();Start(s);long serial=s.Snapshot.PieceSerial;Tap(s);Tap(s);Equal(serial,s.Snapshot.PieceSerial);Frame(s.Input,serial,P(4,TetrisAction.SoftDrop));s.Advance(.05);Equal(serial,s.Snapshot.PieceSerial);Frame(s.Input,serial,P(4,TetrisAction.SoftDrop,TetrisPointerPhase.Ended));s.Advance(0);Equal(serial+1,s.Snapshot.PieceSerial);});
 Test("one short down press preserves one-cell soft drop",()=>{var s=New();Start(s);long serial=s.Snapshot.PieceSerial;int y=s.Snapshot.Y;Tap(s);Equal(serial,s.Snapshot.PieceSerial);Equal(y+1,s.Snapshot.Y);Equal(false,s.Input.SoftDropHeld);});
 Test("triple tap never hard drops following piece",()=>{var s=New();Start(s);Tap(s);Tap(s);Tap(s);long serial=s.Snapshot.PieceSerial;Tap(s);Tap(s);Equal(serial,s.Snapshot.PieceSerial);});
 Test("subthreshold down hold stays soft and release is not a tap",()=>{var s=New();Start(s);Tap(s);Tap(s);long serial=s.Snapshot.PieceSerial;int y=s.Snapshot.Y;Frame(s.Input,serial,P(4,TetrisAction.SoftDrop));Wait(s,.3);Equal(true,s.Snapshot.Y>y);Frame(s.Input,serial,P(4,TetrisAction.SoftDrop,TetrisPointerPhase.Ended));s.Advance(0);Equal(serial,s.Snapshot.PieceSerial);Tap(s);Tap(s);Equal(serial,s.Snapshot.PieceSerial);Tap(s);Equal(serial+1,s.Snapshot.PieceSerial);});
 Test("half-second down hold hard drops once and never carries to the next piece",()=>{
  var s=New();Start(s);long serial=s.Snapshot.PieceSerial;Frame(s.Input,serial,P(4,TetrisAction.SoftDrop));
  Wait(s,.49);Equal(serial,s.Snapshot.PieceSerial);Equal(true,s.Snapshot.Y>0);Wait(s,.01);
  Equal(serial+1,s.Snapshot.PieceSerial);Equal(false,s.Input.SoftDropHeld);Wait(s,.7);Equal(serial+1,s.Snapshot.PieceSerial);
  Frame(s.Input,s.Snapshot.PieceSerial,P(4,TetrisAction.SoftDrop,TetrisPointerPhase.Ended));Frame(s.Input,s.Snapshot.PieceSerial,P(4,TetrisAction.SoftDrop));Wait(s,.5);Equal(serial+2,s.Snapshot.PieceSerial);
 });
 Test("rotation contact cancels the current long-press gesture before its threshold",()=>{
  foreach(bool reverse in new[]{false,true}){
   var input=new TetrisInput();Frame(input,1,P(4,TetrisAction.SoftDrop));for(int n=0;n<29;n++)input.Tick(1);
   var down=P(4,TetrisAction.SoftDrop,TetrisPointerPhase.Stationary);var rotate=P(2,TetrisAction.Clockwise);
   Frame(input,1,reverse?rotate:down,reverse?down:rotate);input.Tick(1);
   Equal(true,input.TryTake(out var action));Equal(TetrisAction.Clockwise,action.Action);Equal(false,input.TryTake(out action));
   Frame(input,1,down,P(2,TetrisAction.Clockwise,TetrisPointerPhase.Ended));for(int n=0;n<60;n++)input.Tick(1);
   Equal(true,input.SoftDropHeld);Equal(false,input.TryTake(out action));
  }
 });
 Test("cancel exit pause and piece changes disarm a held hard drop",()=>{
  foreach(int boundary in new[]{0,1,2,3}){
   var input=new TetrisInput();Frame(input,1,P(4,TetrisAction.SoftDrop));for(int n=0;n<29;n++)input.Tick(1);
   if(boundary==0)Frame(input,1,P(4,TetrisAction.SoftDrop,TetrisPointerPhase.Canceled));
   else if(boundary==1)Frame(input,1,P(4,TetrisAction.SoftDrop,TetrisPointerPhase.Moved,false));
   else if(boundary==2)input.Cancel();else input.PieceChanged();
   for(int n=0;n<60;n++)input.Tick(boundary==3?2:1);Equal(false,input.TryTake(out var action));Equal(false,input.SoftDropHeld);
  }
 });
 Test("slow tap gap starts a new sequence",()=>{var s=New();Start(s);Tap(s);Tap(s);Wait(s,.35);long serial=s.Snapshot.PieceSerial;Tap(s);Tap(s);Equal(serial,s.Snapshot.PieceSerial);Tap(s);Equal(serial+1,s.Snapshot.PieceSerial);});
 Test("total triple window is bounded",()=>{var s=New();Start(s);Tap(s,4,.15);Wait(s,.18);Tap(s,4,.15);Wait(s,.18);long serial=s.Snapshot.PieceSerial;Tap(s,4,.15);Equal(serial,s.Snapshot.PieceSerial);});
 Test("canceled down clears earlier taps",()=>{var s=New();Start(s);Tap(s);Tap(s);long serial=s.Snapshot.PieceSerial;Frame(s.Input,serial,P(4,TetrisAction.SoftDrop,TetrisPointerPhase.Canceled));Frame(s.Input,serial);Tap(s);Tap(s);Equal(serial,s.Snapshot.PieceSerial);Tap(s);Equal(serial+1,s.Snapshot.PieceSerial);});
 Test("drag out and missing up clear the tap sequence",()=>{foreach(bool missing in new[]{false,true}){var s=New();Start(s);Tap(s);Tap(s);long serial=s.Snapshot.PieceSerial;Frame(s.Input,serial,P(4,TetrisAction.SoftDrop));s.Advance(.05);if(missing)Frame(s.Input,serial);else Frame(s.Input,serial,P(4,TetrisAction.SoftDrop,TetrisPointerPhase.Ended,false));s.Advance(0);Tap(s);Tap(s);Equal(serial,s.Snapshot.PieceSerial);Tap(s);Equal(serial+1,s.Snapshot.PieceSerial);}});
 Test("hold changes piece and resets down tap history",()=>{var s=New();Start(s);Tap(s);Tap(s);Frame(s.Input,s.Snapshot.PieceSerial,P(7,TetrisAction.Hold));s.Advance(0);Frame(s.Input,s.Snapshot.PieceSerial,P(7,TetrisAction.Hold,TetrisPointerPhase.Ended));long serial=s.Snapshot.PieceSerial;Tap(s);Tap(s);Equal(serial,s.Snapshot.PieceSerial);Tap(s);Equal(serial+1,s.Snapshot.PieceSerial);});
 Test("overlapping down fingers cannot manufacture triple taps",()=>{var s=New();Start(s);Tap(s);long serial=s.Snapshot.PieceSerial;Frame(s.Input,serial,P(1,TetrisAction.SoftDrop),P(2,TetrisAction.SoftDrop));s.Advance(.05);Frame(s.Input,serial,P(1,TetrisAction.SoftDrop,TetrisPointerPhase.Ended),P(2,TetrisAction.SoftDrop,TetrisPointerPhase.Ended));s.Advance(0);Equal(serial,s.Snapshot.PieceSerial);Tap(s);Tap(s);Equal(serial,s.Snapshot.PieceSerial);});
 Test("another finger clears tap history without blocking movement or soft drop",()=>{
  var s=New();Start(s);Tap(s);Tap(s);long serial=s.Snapshot.PieceSerial;int x=s.Snapshot.X;
  Frame(s.Input,serial,P(4,TetrisAction.SoftDrop),P(2,TetrisAction.Left));s.Advance(.02);
  Equal(x-1,s.Snapshot.X);Equal(true,s.Input.SoftDropHeld);
  Frame(s.Input,serial,P(4,TetrisAction.SoftDrop,TetrisPointerPhase.Stationary),P(2,TetrisAction.Left,TetrisPointerPhase.Ended));s.Advance(.02);
  Frame(s.Input,serial,P(4,TetrisAction.SoftDrop,TetrisPointerPhase.Ended));s.Advance(0);Equal(serial,s.Snapshot.PieceSerial);
  Tap(s);Tap(s);Equal(serial,s.Snapshot.PieceSerial);Tap(s);Equal(serial+1,s.Snapshot.PieceSerial);
 });
 Test("second contact on third release cancels tap before command arbitration",()=>{
  foreach(var other in new[]{TetrisAction.Left,TetrisAction.Clockwise,TetrisAction.None})foreach(bool reverse in new[]{false,true}) {
   var input=new TetrisInput();long serial=1;
   for(int n=0;n<2;n++){input.ProcessFrame(new[]{P(4,TetrisAction.SoftDrop)},true,serial,n*.1);input.ProcessFrame(new[]{P(4,TetrisAction.SoftDrop,TetrisPointerPhase.Ended)},true,serial,n*.1+.05);}
   input.ProcessFrame(new[]{P(4,TetrisAction.SoftDrop)},true,serial,.2);
   var release=P(4,TetrisAction.SoftDrop,TetrisPointerPhase.Ended);var second=P(2,other);
   input.ProcessFrame(reverse?new[]{second,release}:new[]{release,second},true,serial,.25);
   int commands=0;while(input.TryTake(out var action)){Equal(other,action.Action);Equal(serial,action.PieceSerial);commands++;}
   Equal(other==TetrisAction.None?0:1,commands);
  }
 });
 Test("pause clears completed taps and waits for clean release",()=>{var s=New();Start(s);Tap(s);Tap(s);s.Pause();Start(s);for(int i=0;i<30;i++)s.Advance(.1);Frame(s.Input,s.Snapshot.PieceSerial);long serial=s.Snapshot.PieceSerial;Tap(s);Tap(s);Equal(serial,s.Snapshot.PieceSerial);Tap(s);Equal(serial+1,s.Snapshot.PieceSerial);});
 Test("short hold duration is independent of gap before press",()=>{var s=New();Start(s);Tap(s,4,.05);Wait(s,.18);Tap(s,4,.15);long serial=s.Snapshot.PieceSerial;Tap(s,4,.05);Equal(serial+1,s.Snapshot.PieceSerial);});
 Test("production timestamp measures holds without simulation steps",()=>{var i=new TetrisInput();long serial=1;double time=100;for(int n=0;n<3;n++){i.ProcessFrame(new[]{P(4,TetrisAction.SoftDrop)},true,serial,time);i.ProcessFrame(new[]{P(4,TetrisAction.SoftDrop,TetrisPointerPhase.Ended)},true,serial,time+.05);time+=.1;}Equal(true,i.TryTake(out var action));Equal(TetrisAction.HardDrop,action.Action);Equal(serial,action.PieceSerial);Equal(false,i.TryTake(out action));});
 Test("terminal session ignores completed down taps",()=>{var game=new TetrisGame(42);while(!game.GameOver)game.Execute(TetrisCommand.HardDrop);var s=new TetrisSession(game,true);var before=s.Snapshot;Tap(s);Tap(s);Tap(s);Equal(TetrisSessionPhase.Ended,s.Phase);Equal(before.PieceSerial,s.Snapshot.PieceSerial);Equal(before.Score,s.Snapshot.Score);});
 Console.WriteLine("Tetris input/session: "+passed+" passed, "+failed+" failed");Environment.ExitCode=failed==0?0:1;
 }
}
