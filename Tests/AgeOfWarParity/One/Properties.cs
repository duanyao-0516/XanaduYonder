using System;
using System.Linq;
using System.Reflection;
using XanaduYonder.Core.AgeOfWar.One;
using XanaduYonder.Core.AgeOfWar;
internal static class Program
{
 static readonly BindingFlags Fields=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static int failures;
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,Fields).Invoke(o,a);}
 static void Equal(double expected,double actual){if(Math.Abs(expected-actual)>1e-9)throw new Exception("expected "+expected+", actual "+actual);}
 static void Test(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.GetBaseException().Message);}}
 static void Main(){
  Test("AVM1 original part6 first fade writes 98.828125 percent alpha",()=>{var b=new AgeOfWarOneBattle(1,1);Call(b,"CreateParticle",400.0,300.0,6,0,0.0);b.Tick();Equal(.98828125,b.Capture().Visuals.Single(v=>v.Clip=="part/6").Alpha);});
  Test("AVM1 original part6 expires on fade write86",()=>{var b=new AgeOfWarOneBattle(1,1);Call(b,"CreateParticle",400.0,300.0,6,0,0.0);for(int i=0;i<85;i++)b.Tick();Equal(.00390625,b.Capture().Visuals.Single(v=>v.Clip=="part/6").Alpha);b.Tick();Equal(0,b.Capture().Visuals.Count(v=>v.Clip=="part/6"));});
  Test("AVM1 display rotation normalizes attachment 181 degrees",()=>{var b=new AgeOfWarOneBattle(1,1);Call(b,"CreateProjectile",400.0,300.0,181.0,10.0,"1",1);Equal(-179,b.Capture().Visuals.Single(v=>v.Clip=="projectile/1").Rotation);});
  Test("AVM1 particle attachment retains raw transform until the next frame",()=>{var b=new AgeOfWarOneBattle(1,1);Call(b,"CreateParticle",400.0,300.0,4,0,181.0);var p=b.Capture().Visuals.Single(v=>v.Clip=="part/4");Equal(400,p.X);Equal(300,p.Y);Equal(0,p.Rotation);});
  Test("AVM1 reward movement quantizes each display write",()=>{var b=new AgeOfWarOneBattle(1,1);Call(b,"CreateParticle",400.0,300.0,1,0,20.0);b.Tick();b.Tick();Equal(294.25,b.Capture().Visuals.Single(v=>v.Clip=="part/1").Y);});
  Test("AVM1 special5 attachment retains source initial transform",()=>{var b=new AgeOfWarOneBattle(1,1);b.Tick();b.GetType().GetField("_age",Fields).SetValue(b,5);b.Execute(new AgeOfWarCommand(AgeOfWarAction.Special));var v=b.Capture().Visuals.Single(x=>x.Clip=="special/5");Equal(0,v.X);Equal(0,v.Y);});
  Environment.ExitCode=failures==0?0:1;
 }
}
