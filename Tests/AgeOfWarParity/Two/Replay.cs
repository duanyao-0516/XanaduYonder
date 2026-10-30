using System;
using System.Globalization;
using System.IO;
using System.Linq;
using XanaduYonder.Core.AgeOfWar;
using XanaduYonder.Core.AgeOfWar.Two;

internal static class Replay
{
    private static int mismatches, checks;
    private static double Number(string value) { return double.Parse(value,CultureInfo.InvariantCulture); }
    private static void Check(string field,int frame,double original,double native)
    {
        checks++;
        if(Math.Abs(original-native)<1e-10)return;
        if(mismatches++<15)Console.WriteLine("MISMATCH frame="+frame+" "+field+" original="+original.ToString("R",CultureInfo.InvariantCulture)+" native="+native.ToString("R",CultureInfo.InvariantCulture));
    }
    private static int Main(string[] args)
    {
        string path=args.Length==0?Path.Combine(AppContext.BaseDirectory,"reference.csv"):args[0];
        var battle=new AgeOfWarTwoBattle(1,1);
        if(!battle.Execute(new AgeOfWarCommand(AgeOfWarAction.Train,2)))throw new InvalidOperationException("Original btnU2 purchase must succeed.");
        int rows=0;bool complete=false;double elapsed=0;
        foreach(var line in File.ReadLines(path))
        {
            // A final queued stage event can precede the browser's async suspend. It is outside the sample.
            if(line.StartsWith("DONE,")){complete=true;break;}
            if(!line.StartsWith("ROW,"))continue;
            var values=line.Split(',');int frame=int.Parse(values[1],CultureInfo.InvariantCulture);
            if(frame!=rows+1)throw new InvalidDataException("Missing or repeated original frame "+frame);
            double dt=Number(values[2]);battle.Step(dt);elapsed+=dt;rows++;
            Check("gold",frame,Number(values[4]),battle.Gold);
            Check("xp",frame,Number(values[5]),battle.Experience);
            Check("pop",frame,Number(values[6]),battle.Population);
            var units=battle.Units.Where(unit=>unit.Side==1).ToArray();
            Check("units",frame,Number(values[7]),units.Length);
            if(values.Length<9||units.Length==0)continue;
            var player=units[0];
            Check("x",frame,Number(values[8]),player.X);
            Check("health",frame,Number(values[9]),player.Health);
            Check("speed",frame,Number(values[10]),player.Speed);
            Check("animationFrame",frame,Number(values[11]),player.Animation.AbsoluteFrame+1);
            checks++;
            if(values[12]!=player.Animation.Label&&mismatches++<15)
                Console.WriteLine("MISMATCH frame="+frame+" label original="+values[12]+" native="+player.Animation.Label);
        }
        if(!complete||rows==0||elapsed<4)throw new InvalidDataException("Original runtime fixture is incomplete.");
        Console.WriteLine("AoW2 original runtime: "+rows+" frames, "+checks+" checks, "+mismatches+" mismatches (training/spawn/wait/walk scope).");
        return mismatches==0?0:1;
    }
}
