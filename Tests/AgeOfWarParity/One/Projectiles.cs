using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using XanaduYonder.Core.AgeOfWar.One;
internal static class Program
{
 static readonly BindingFlags Fields=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static object G(object o,string n){return o.GetType().GetField(n,Fields).GetValue(o);}
 static void Row(params object[] a){Console.WriteLine(string.Join(",",Array.ConvertAll(a,x=>Convert.ToString(x,CultureInfo.InvariantCulture))));}
 static void Main(){var battle=new AgeOfWarOneBattle(1,1);var particles=new HashSet<long>();
  for(int frame=1;frame<=40;frame++){
   battle.Tick();
   if(frame==1)for(int id=1;id<=12;id++)battle.GetType().GetMethod("CreateProjectile",Fields).Invoke(battle,new object[]{400.0,400.0,30.0,10.0,id.ToString(),1});
   Row("F",frame);
   foreach(var p in (IEnumerable)G(battle,"_projectiles")){
    var id=(string)G(p,"Id");if(!(bool)G(p,"Initialized")){Row("B",frame,id,G(p,"X"),G(p,"Y"),G(p,"Rotation"),"undefined","undefined","undefined");continue;}Row("B",frame,id,G(p,"X"),G(p,"Y"),G(p,"Rotation"),id=="5"?"undefined":G(p,"Xs"),id=="5"?"undefined":G(p,"Ys"),G(p,"Gravity"));
   }
  }
 }
}
