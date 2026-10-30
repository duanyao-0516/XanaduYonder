using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using XanaduYonder.Runtime.AgeOfWar;
internal static class Program
{
 static int passed,failed;
 static readonly BindingFlags B=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static object New(int edition=1,Func<int,int> random=null){var type=typeof(AgeOfWarClock).Assembly.GetType("XanaduYonder.Runtime.AgeOfWar.AgeOfWarOpeningState");Check(type!=null,"Native opening state is missing");return Activator.CreateInstance(type,B,null,new object[]{edition,random??(n=>2)},null);}
 static object Call(object o,string name,params object[] a){return o.GetType().GetMethod(name,B).Invoke(o,a);}
 static T Get<T>(object o,string name){return (T)o.GetType().GetProperty(name,B).GetValue(o);}
 static string Field(object o,string n){return (string)o.GetType().GetField(n,B).GetValue(o);}
 static object[] Cues(object s){return ((Array)Call(s,"DrainCues")).Cast<object>().ToArray();}
 static void Tick(object s,int n=1){for(int i=0;i<n;i++)Call(s,"Tick");}
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 static void Equal(object expected,object actual){Check(expected.Equals(actual),"expected "+expected+", actual "+actual);}
 static void Test(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.GetBaseException().Message);}}
 static object OneIntro(){var s=New();Call(s,"ResourceProgress",0L,0L,true);Tick(s);Check((bool)Call(s,"PressPlay"),"source press");Tick(s,3);Equal("intro-background",Get<string>(s,"SceneKey"));Equal(1,Get<int>(s,"SourceFrame"));return s;}
 static object TwoSponsor(){var s=New(2);Call(s,"ResourceProgress",0L,0L,true);Check((bool)Call(s,"PressPlay"),"click");Tick(s);return s;}
 static void Main(){
  Test("loading stays until actual readiness without simulated percentages",()=>{var s=New();Tick(s,400);Equal(false,Get<bool>(s,"CanPressPlay"));Call(s,"ResourceProgress",50L,100L,false);Equal(.5,Get<double>(s,"Progress"));Call(s,"ResourceProgress",100L,100L,false);Equal(false,Get<bool>(s,"CanPressPlay"));});
  Test("unknown byte count with actual ready reaches original loaded label",()=>{var s=New();Call(s,"ResourceProgress",0L,0L,true);Equal("loaded-lead",Get<string>(s,"SceneKey"));Equal(3,Get<int>(s,"SourceFrame"));Equal(false,Get<bool>(s,"CanPressPlay"));Tick(s);Equal("loaded",Get<string>(s,"SceneKey"));Equal(true,Get<bool>(s,"CanPressPlay"));});
  Test("One original press preserves root frames5 and6 before child1",()=>{var s=New();Call(s,"ResourceProgress",0L,0L,true);Tick(s);Call(s,"PressPlay");Equal(4,Get<int>(s,"SourceFrame"));Tick(s);Equal(5,Get<int>(s,"SourceFrame"));Equal("loaded",Get<string>(s,"SceneKey"));Tick(s);Equal("intro-lead",Get<string>(s,"SceneKey"));Tick(s);Equal("intro/1",Get<string>(s,"OverlaySceneKey"));});
  Test("One160 frames render completely before root8",()=>{var s=OneIntro();Tick(s,159);Equal(160,Get<int>(s,"SourceFrame"));Equal(false,Get<bool>(s,"Done"));Tick(s);Equal(true,Get<bool>(s,"Done"));Equal(8,Get<int>(s,"SourceFrame"));});
  Test("One sound frames and stream start are once per source frame",()=>{var s=OneIntro();var sounds=Cues(s).ToList();for(int i=0;i<160;i++){Tick(s);sounds.AddRange(Cues(s));}Equal(5,sounds.Count(x=>Field(x,"Kind")=="sound"&&Field(x,"Key")=="sound/1002"));Equal(1,sounds.Count(x=>Field(x,"Kind")=="stream-start"&&Field(x,"Key")=="intro-stream"));Equal(1,sounds.Count(x=>Field(x,"Kind")=="stream-stop"));});
  Test("One has no play or skip action inside the intro",()=>{var s=OneIntro();Equal(false,(bool)Call(s,"PressPlay"));Equal(1,Get<int>(s,"SourceFrame"));});
  Test("fire is raw frame1 at first parent frame",()=>{var s=OneIntro();Equal(0,Call(s,"ResolveFrame","nested/intro/27/1","random-fire",0,0));});
  Test("fire independent clocks reset only with original birthFrame",()=>{int draws=0;var s=New(1,n=>++draws);Call(s,"ResourceProgress",0L,0L,true);Tick(s);Call(s,"PressPlay");Tick(s,4);Equal(0,Call(s,"ResolveFrame","a","random-fire",0,1));Equal(1,Call(s,"ResolveFrame","b","random-fire",0,1));Equal(2,draws);Tick(s);Equal(1,Call(s,"ResolveFrame","a","random-fire",0,1));Equal(2,draws);Tick(s,3);Equal(2,Call(s,"ResolveFrame","a","random-fire",0,5));Equal(3,draws);});
  Test("fire frame31 jumps to3 rather than looping to1",()=>{var s=New(1,n=>24);Call(s,"ResourceProgress",0L,0L,true);Tick(s);Call(s,"PressPlay");Tick(s,4);Equal(23,Call(s,"ResolveFrame","a","random-fire",0,1));Tick(s,6);Equal(29,Call(s,"ResolveFrame","a","random-fire",0,1));Tick(s);Equal(2,Call(s,"ResolveFrame","a","random-fire",0,1));});
  Test("suspension discards fractional time without catching up",()=>{var s=OneIntro();Call(s,"Advance",.02);Call(s,"Suspend",true);Call(s,"Advance",5.0);Equal(1,Get<int>(s,"SourceFrame"));Call(s,"Suspend",false);Call(s,"Advance",.01);Equal(1,Get<int>(s,"SourceFrame"));Call(s,"Advance",.015);Equal(2,Get<int>(s,"SourceFrame"));});
  Test("Two Play starts Sponsor music and checkers immediately",()=>{var s=New(2);Call(s,"ResourceProgress",0L,0L,true);Equal(true,Get<bool>(s,"CanPressPlay"));Call(s,"PressPlay");Equal("sponsor-initial",Get<string>(s,"SceneKey"));var cues=Cues(s);Check(cues.Any(x=>Field(x,"Kind")=="music"&&Field(x,"Key")=="music/2"),"Music2 constructor timing");Check(cues.Any(x=>Field(x,"Kind")=="transition"&&Field(x,"Key")=="checkers"),"checkers");});
  Test("Two sponsor stream starts on child source2",()=>{var s=TwoSponsor();Equal(1,Get<int>(s,"SourceFrame"));Check(!Cues(s).Any(x=>Field(x,"Kind")=="stream-start"),"not on child1");Tick(s);Check(Cues(s).Any(x=>Field(x,"Kind")=="stream-start"&&Field(x,"Key")=="sponsor-stream"),"child2");});
  Test("Two native sponsor keeps playing while screen transition waits",()=>{var s=TwoSponsor();Tick(s,185);Equal(186,Get<int>(s,"SourceFrame"));Equal("sponsor",Get<string>(s,"SceneKey"));Tick(s);Equal("sponsor-initial",Get<string>(s,"SceneKey"));Tick(s,50);Equal(false,Get<bool>(s,"Done"));Equal("sponsor-initial",Get<string>(s,"SceneKey"));Call(s,"TransitionComplete");Tick(s);Equal("author/1",Get<string>(s,"SceneKey"));Check(Cues(s).Any(x=>Field(x,"Kind")=="transition"&&Field(x,"Key")=="fade"),"author fade");});
  Test("Two Author66 stops and next active manager step enters menu",()=>{var s=TwoSponsor();Call(s,"TransitionComplete");Tick(s,187);Equal("author/1",Get<string>(s,"SceneKey"));Call(s,"TransitionComplete");Tick(s,65);Equal(66,Get<int>(s,"SourceFrame"));Equal(false,Get<bool>(s,"Done"));Tick(s);Equal(true,Get<bool>(s,"Done"));});
  Test("disposing stops only the active opening stream exactly once",()=>{var s=TwoSponsor();Tick(s);Cues(s);Call(s,"Dispose");var cues=Cues(s);Equal(1,cues.Count(x=>Field(x,"Kind")=="stream-stop"));Check(!cues.Any(x=>Field(x,"Kind")=="music"),"menu music continues");Call(s,"Dispose");Equal(0,Cues(s).Length);});
  Test("Sponsor removes its streaming child at root3 even while transition waits",()=>{var s=TwoSponsor();Tick(s);Cues(s);Tick(s,185);Check(Cues(s).Any(x=>Field(x,"Kind")=="stream-stop"),"child58 removed at root3 stops its stream");});
  Test("preloader hover tween has original .3 second quadratic-out scale and exposure",()=>{var type=typeof(AgeOfWarClock).Assembly.GetType("XanaduYonder.Runtime.AgeOfWar.AgeOfWarOpeningHover");Check(type!=null,"Opening hover state missing");var t=Activator.CreateInstance(type,true);Call(t,"Hover",true,0.0);Equal(.75,Call(t,"Sample",.15));Call(t,"Hover",false,.15);Equal(.1875,Call(t,"Sample",.3));});
  Test("4160 actual original fire rows replay with observed birth phases",()=>{
   var rows=File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,"OriginalOneOpening/reference.csv")).Where(x=>x.StartsWith("I,")).Select(x=>x.Split(',')).ToArray();
   var births=new Dictionary<int,int>();var phaseInputs=new Queue<int>();
   foreach(var row in rows){int identity=int.Parse(row[6]);if(!births.ContainsKey(identity)){int frame=int.Parse(row[2]);births[identity]=frame-1;if(frame>1)phaseInputs.Enqueue(int.Parse(row[7]));}}
   var s=New(1,n=>phaseInputs.Dequeue());Call(s,"ResourceProgress",0L,0L,true);Tick(s);Call(s,"PressPlay");Tick(s,3);int assertions=0;
   for(int frame=1;frame<=160;frame++){foreach(var row in rows.Where(r=>int.Parse(r[2])==frame)){Equal(int.Parse(row[7])-1,Call(s,"ResolveFrame","nested/intro/"+row[3]+"/"+row[5],"random-fire",0,births[int.Parse(row[6])]));assertions++;}if(frame<160)Tick(s);}
   Equal(4160,assertions);Equal(0,phaseInputs.Count);Equal(false,Get<bool>(s,"Done"));Tick(s);Equal(true,Get<bool>(s,"Done"));
  });
  Console.WriteLine(passed+" passed, "+failed+" failed");if(failed>0)Environment.Exit(1);
 }
}
