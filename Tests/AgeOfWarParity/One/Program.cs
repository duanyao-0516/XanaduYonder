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
 static string S(object x){return x is bool?((bool)x?"true":"false"):Convert.ToString(x,CultureInfo.InvariantCulture);}
 static void Row(params object[] a){Console.WriteLine(string.Join(",",Array.ConvertAll(a,S)));}
 static void Main()
 {
  var battle=new AgeOfWarOneBattle(1,1);var ids=new Dictionary<long,int>();int id=0;
  for(int frame=1;frame<=800;frame++)
  {
   battle.Tick();
   if(frame==1)for(int i=0;i<3;i++)battle.Execute(new AgeOfWarCommand(AgeOfWarAction.Train,1));
   var f=battle.Capture();
   Row("F",frame,f.Gold,f.Experience,G(battle,"_training"),G(battle,"_queue") is IList&&((IList)G(battle,"_queue")).Count>0?((IList)G(battle,"_queue"))[0]:0,G(battle,"_techTimer"),G(battle,"_aiTimer"),G(battle,"_enemyTraining"),G(battle,"_enemyPopulation"),f.PlayerHealth,f.EnemyHealth);
   foreach(var actor in (IEnumerable)G(battle,"_units")){long uid=(long)G(actor,"Uid");if(!ids.ContainsKey(uid))ids[uid]=++id;}
   foreach(var unit in (IEnumerable)G(battle,"_units"))
   {
    var uid=(long)G(unit,"Uid");if(!ids.ContainsKey(uid))ids[uid]=++id;
    if((bool)G(unit,"Dead"))continue;
    string anim=(string)G(unit,"Animation");int outer=anim=="idle"?1:anim=="walk"?2:anim=="attack"?3:anim=="die"?4:0;
    var target=G(unit,"Target");object tid=0;if(target!=null){long targetUid=(long)G(target,"Uid");tid=targetUid<0?(object)(targetUid==-1?"base1":"base2"):ids.ContainsKey(targetUid)?ids[targetUid]:0;}
    if(!(bool)G(unit,"Initialized")){Row("U",frame,ids[uid],G(unit,"Id"),G(unit,"Side"),G(unit,"X"),G(unit,"Y"),"undefined","undefined","undefined","undefined","undefined","undefined",1,1);continue;}
    Row("U",frame,ids[uid],G(unit,"Id"),G(unit,"Side"),G(unit,"X"),G(unit,"Y"),G(unit,"Health"),G(unit,"Dead"),G(unit,"ScanCounter"),tid,G(unit,"EnemyDistance"),G(unit,"ClosestDistance"),outer,G(unit,"AnimationFrame"));
   }
  }
 }
}
