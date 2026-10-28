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
 Console.WriteLine("Tetris input/session: "+passed+" passed, "+failed+" failed");Environment.ExitCode=failed==0?0:1;
 }
}
