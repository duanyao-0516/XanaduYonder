using System;
using System.Collections.Generic;
using XanaduYonder.Core;
using XanaduYonder.Core.Minesweeper;
internal static class Program
{
    private static int passed;
    private static void Main()
    {
        Check("fixed specs and bounded eight-neighbors", () => {
            int[] widths={9,16,30}, heights={9,16,16}, mines={10,40,99};
            for(int d=0;d<3;d++) {var s=MinesweeperRules.Get(d); Equal(s.Width,widths[d]);Equal(s.Height,heights[d]);Equal(s.MineCount,mines[d]);
                Equal(MinesweeperRules.Neighbors(0,s.Width,s.Height).Length,3);
                foreach(int n in MinesweeperRules.Neighbors(s.Width-1,s.Width,s.Height)) Assert(n!=s.Width);
            }
        });
        Check("flags are unrestricted and deferred generation consumes no randomness", () => {
            var random=new RandomSource(7);var g=new MinesweeperGame(0,random);
            for(int i=0;i<15;i++) Assert(g.ToggleFlag(i).Changed);
            Equal(g.RemainingMarkers,-5);Equal(random.Calls,0);Assert(!g.Reveal(0).Changed);Assert(!g.Reveal(-1).Changed);Equal(random.Calls,0);
            Assert(g.State==MinesweeperState.Ready);Valid(g.GetSnapshot());
        });
        Check("all difficulties generate exact safe deterministic boards across seeds and edges", () => {
            for(int d=0;d<3;d++) {var s=MinesweeperRules.Get(d);int[] firsts={0,s.Width-1,s.Width*(s.Height-1),s.Width*s.Height-1,s.Width*(s.Height/2)+s.Width/2};
            for(int seed=0;seed<20;seed++) foreach(int first in firsts) {var a=new MinesweeperGame(d,new RandomSource(seed));var b=new MinesweeperGame(d,new RandomSource(seed));a.Reveal(first);b.Reveal(first);var x=a.GetSnapshot();var y=b.GetSnapshot();Valid(x);Equal(Count(x.Mines),s.MineCount);Equal(x.AdjacentMines(first),0);Assert(x.IsRevealed(first));
                for(int i=0;i<s.Width*s.Height;i++) Assert(x.IsMine(i)==y.IsMine(i));
            }}
        });
        Check("preflags preserve sampling and prevent zero expansion through flagged cell", () => {
            var a=new MinesweeperGame(0,new RandomSource(44));var b=new MinesweeperGame(0,new RandomSource(44));a.ToggleFlag(0);a.Reveal(10);b.Reveal(10);
            var x=a.GetSnapshot();var y=b.GetSnapshot();for(int i=0;i<81;i++) Assert(x.IsMine(i)==y.IsMine(i));Assert(x.IsFlagged(0)&&!x.IsRevealed(0));a.ToggleFlag(0);a.Reveal(0);Assert(a.GetSnapshot().IsRevealed(0));
        });
        Check("snapshots copy source arrays and expose defensive collections", () => {
            var g=new MinesweeperGame(0,new RandomSource(7));g.Reveal(40);var s=g.GetSnapshot();var flags=s.Flags;flags[0]=!flags[0];Assert(s.IsFlagged(0)!=flags[0]);
            var result=g.ToggleFlag(0);var changed=result.ChangedIndices;if(changed.Length>0){changed[0]=80;Assert(result.ChangedIndices[0]==0);}
            var restored=new MinesweeperGame(s,new RandomSource(4));restored.ToggleFlag(0);Assert(s.IsFlagged(0)==g.GetSnapshot().IsFlagged(0) || !g.GetSnapshot().IsRevealed(0));
        });
        Check("chord mismatch is inert and correct flags expand", () => {
            var g=Fixture(false);var before=g.GetSnapshot();Assert(!g.Chord(10).Changed);Assert(g.Chord(10).Reason==MinesweeperMoveReason.FlagMismatch);
            g.ToggleFlag(2);var r=g.Chord(10);Assert(r.Changed);Assert(g.State!=MinesweeperState.Lost);Valid(g.GetSnapshot());Assert(!before.IsRevealed(11));
        });
        Check("wrong equal flags lose before safe cells and detonate lowest target index", () => {
            var g=Fixture(true);g.ToggleFlag(9);g.ToggleFlag(18);var before=g.GetSnapshot();var r=g.Chord(10);Assert(r.Changed);Assert(g.State==MinesweeperState.Lost);Equal(g.GetSnapshot().DetonatedIndex.Value,2);
            var after=g.GetSnapshot();for(int i=0;i<81;i++) if(!after.IsMine(i)) Assert(before.IsRevealed(i)==after.IsRevealed(i));Valid(after);
            Assert(!g.Reveal(8).Changed&&!g.ToggleFlag(7).Changed&&!g.Chord(10).Changed);
        });
        Check("all safe cells win without flags and terminal rejects mutation", () => {
            var g=new MinesweeperGame(2,new RandomSource(8));g.Reveal(200);var s=g.GetSnapshot();for(int i=0;i<480;i++) if(!s.IsMine(i))g.Reveal(i);
            Assert(g.State==MinesweeperState.Won);Equal(g.FlagCount,0);Valid(g.GetSnapshot());Assert(!g.ToggleFlag(0).Changed&&!g.Reveal(0).Changed);
        });
        Check("invalid snapshots reject inconsistent ready, active, won and lost state", () => {
            var mines=new bool[81];var opens=new bool[81];var flags=new bool[81];mines[80]=true;
            var s=new MinesweeperSnapshot(0,false,null,MinesweeperState.Ready,null,mines,opens,flags);Assert(!s.TryValidate(out var reason));
            var g=new MinesweeperGame(0,new RandomSource(2));g.Reveal(0);s=g.GetSnapshot();opens=s.Revealed;flags=s.Flags;flags[0]=true;
            Assert(!new MinesweeperSnapshot(0,true,0,MinesweeperState.Active,null,s.Mines,opens,flags).TryValidate(out reason));
            Assert(!new MinesweeperSnapshot(0,true,0,MinesweeperState.Won,null,s.Mines,s.Revealed,s.Flags).TryValidate(out reason));
        });
        Check("first reveal alone can win a fully connected safe area",()=>{
            var game=new MinesweeperGame(0,new ZeroRandom());var result=game.Reveal(40);Assert(result.Changed&&game.State==MinesweeperState.Won);
            Equal(result.ChangedIndices.Length,71);Equal(game.FlagCount,0);Valid(game.GetSnapshot());
        });
        Check("chord mine wins over final unflagged safe target",()=>{
            var mines=new bool[81];foreach(int i in new[]{2,60,61,62,63,64,65,66,67,68})mines[i]=true;
            var revealed=new bool[81];for(int i=0;i<81;i++)revealed[i]=!mines[i]&&i!=1&&i!=9;
            var flags=new bool[81];flags[9]=true;var game=new MinesweeperGame(new MinesweeperSnapshot(0,true,40,MinesweeperState.Active,null,mines,revealed,flags),new ZeroRandom());
            Assert(game.GetSnapshot().AdjacentMines(10)==1);var result=game.Chord(10);Assert(game.State==MinesweeperState.Lost);
            Equal(result.ChangedIndices.Length,1);Equal(result.ChangedIndices[0],2);Assert(!game.GetSnapshot().IsRevealed(1));Valid(game.GetSnapshot());
        });
        Console.WriteLine("Minesweeper: "+passed+" passed");
    }
    private static MinesweeperGame Fixture(bool two) {
        var m=new bool[81];int[] positions=two?new[]{2,11,60,61,62,63,64,65,66,67}:new[]{2,60,61,62,63,64,65,66,67,68};foreach(int i in positions)m[i]=true;
        var r=new bool[81];r[10]=true;r[40]=true;return new MinesweeperGame(new MinesweeperSnapshot(0,true,40,MinesweeperState.Active,null,m,r,new bool[81]),new RandomSource(1));
    }
    private static int Count(bool[] a){int n=0;foreach(bool b in a)if(b)n++;return n;}
    private static void Valid(MinesweeperSnapshot s){Assert(s.TryValidate(out var reason),reason);}
    private static void Equal(int a,int b){Assert(a==b,a+" != "+b);}
    private static void Assert(bool value,string reason="Assertion failed"){if(!value)throw new Exception(reason);}
    private static void Check(string name,Action run){run();passed++;Console.WriteLine("PASS "+name);}
    private sealed class ZeroRandom:IRandomSource {public int Next(int maxExclusive){return 0;}}
    private sealed class RandomSource:IRandomSource {private readonly Random random;public int Calls;public RandomSource(int seed){random=new Random(seed);}public int Next(int maxExclusive){Calls++;return random.Next(maxExclusive);}}
}
