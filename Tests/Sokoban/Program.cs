using System;
using XanaduYonder.Core;
using XanaduYonder.Core.Sokoban;
internal static class Program
{
    static int passed;
    static void Main()
    {
        Check("walk push goal count and terminal",()=>{var g=Game();Assert(!g.Move(Direction.Up).Changed);Equal(g.Moves,0);Assert(g.Move(Direction.Right).Changed);Equal(g.Moves,1);Assert(g.Move(Direction.Right).Pushed);Equal(g.Pushes,1);Assert(g.Won);Assert(!g.Move(Direction.Left).Changed);Assert(!g.Undo());});
        Check("two crates and edge cannot be crossed",()=>{var g=new SokobanGame(Level(new[]{"#######","#@$$..#","#######"}));Assert(!g.Move(Direction.Right).Changed);Equal(g.Player,8);Assert(!g.Move(Direction.Left).Changed);Equal(g.Moves,0);});
        Check("undo restores complete trajectory and goals",()=>{var g=new SokobanGame(Level(new[]{"#######","#@ $ .#","#  .  #","# $   #","#######"}));Assert(g.Move(Direction.Right).Changed);Assert(g.Move(Direction.Right).Pushed);Assert(g.Move(Direction.Right).Pushed);Equal(g.GoalsFilled,1);Assert(g.Undo());Equal(g.GoalsFilled,0);Assert(g.Level.IsGoal(12));Assert(g.Undo());Assert(g.Undo());Assert(!g.Undo());Equal(g.Moves,0);Equal(g.Pushes,0);Equal(g.Player,g.Level.InitialPlayer);Equal(g.Actions.Length,0);});
        Check("snapshots and terrain are defensively copied",()=>{var rows=new[]{"######","#@ $.#","######"};var level=Level(rows);rows[1]="######";var boxes=level.InitialBoxes;boxes[0]=0;Assert(!level.IsWall(7));var g=new SokobanGame(level);var snapshot=g.GetSnapshot();var boxCopy=snapshot.Boxes;boxCopy[0]=0;g.Move(Direction.Right);Equal(snapshot.Player,7);Equal(snapshot.Boxes[0],9);Equal(snapshot.Moves,0);});
        Check("restart clears trajectory",()=>{var g=Game();g.Move(Direction.Right);g.Restart();Equal(g.Moves,0);Equal(g.Pushes,0);Equal(g.Actions.Length,0);Assert(!g.CanUndo);});
        Check("static corner is warning only",()=>{var g=new SokobanGame(Level(new[]{"######","#    #","# $@.#","######"}));var r=g.Move(Direction.Left);Assert(r.Changed&&r.Pushed&&r.StaticCorner);Assert(!g.Won);Assert(g.Undo());});
        Check("50000 move cap retains every undo",()=>{var g=new SokobanGame(Level(new[]{"#######","#@    #","# $ . #","#######"}));for(int i=0;i<50000;i++)Assert(g.Move(i%2==0?Direction.Right:Direction.Left).Changed);Assert(g.AtMoveLimit);Assert(!g.Move(Direction.Right).Changed);Equal(g.Moves,50000);Assert(g.Undo());Assert(!g.AtMoveLimit);for(int i=0;i<49999;i++)Assert(g.Undo());Equal(g.Moves,0);Assert(!g.Undo());});
        Check("level rejects malformed and initially complete maps",()=>{Reject(()=>Level(new[]{"####","#@$.#","####"}));Reject(()=>Level(new[]{"#####","#@* #","#####"}));Reject(()=>Level(new[]{"#####","#@$. ","#####"}));Reject(()=>Level(new[]{"#####","#@?$.#","#####"}));});
        Console.WriteLine("Sokoban: "+passed+" passed");
    }
    static SokobanGame Game(){return new SokobanGame(Level(new[]{"######","#@ $.#","######"}));}
    static SokobanLevel Level(string[] rows){return new SokobanLevel("test","v1","Test","tutorial",1,rows);}
    static void Check(string name,Action run){run();passed++;Console.WriteLine("PASS "+name);}
    static void Equal(int a,int b){Assert(a==b,a+" != "+b);}
    static void Assert(bool condition,string message="Assertion failed"){if(!condition)throw new Exception(message);}
    static void Reject(Action a){try{a();}catch(ArgumentException){return;}throw new Exception("Expected rejection");}
}
