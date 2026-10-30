using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using XanaduYonder.Core.AgeOfWar;
using XanaduYonder.Core.AgeOfWar.One;
internal static class Program
{
 static readonly BindingFlags Fields=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static object G(object o,string n){return o.GetType().GetField(n,Fields).GetValue(o);}
 static bool Initialized(object o){var f=o.GetType().GetField("Initialized",Fields);return f==null||(bool)f.GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,Fields).SetValue(o,v);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,Fields).Invoke(o,a);}
 static void Row(params object[] a){Console.WriteLine(string.Join(",",Array.ConvertAll(a,x=>Convert.ToString(x,CultureInfo.InvariantCulture))));}
 static void Main(){var battle=new AgeOfWarOneBattle(1,1);var shots=new Dictionary<long,int>();
  for(int frame=1;frame<=110;frame++){
   battle.Tick();
   if(frame==1){Call(battle,"CreateParticle",400.0,300.0,1,0,20.0);Call(battle,"CreateParticle",400.0,300.0,4,0,181.0);Call(battle,"CreateParticle",400.0,300.0,6,0,0.0);Set(battle,"_age",5);battle.Execute(new AgeOfWarCommand(AgeOfWarAction.Special));}
   Row("F",frame);
   foreach(var p in (IEnumerable)G(battle,"_particles")){
    var id=(int)G(p,"Id");if(id!=1&&id!=4&&id!=6)continue;
    Row("Q",frame,id,G(p,"X"),G(p,"Y"),(double)G(p,"Alpha")*100,(double)G(p,"Scale")*100,G(p,"Rotation"),id==1&&Initialized(p)?G(p,"Ys"):"undefined");
   }
   var special=G(battle,"_special");if(special!=null)Row("S",frame,G(special,"X"),G(special,"Y"),G(special,"Frame"),Initialized(special)?G(special,"Counter"):"undefined");
   foreach(var p in (IEnumerable)G(battle,"_projectiles")){
    long uid=(long)G(p,"Uid");if(!shots.ContainsKey(uid))shots.Add(uid,shots.Count);
    Row("B",frame,shots[uid],G(p,"X"),G(p,"Y"),(int)G(p,"Age")+1);
   }
  }
 }
}
