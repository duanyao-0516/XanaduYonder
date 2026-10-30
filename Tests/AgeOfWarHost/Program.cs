using System;
using XanaduYonder.Runtime.AgeOfWar;
class Program
{
 static int count;
 static void Check(bool value,string message){if(!value)throw new Exception(message);count++;}
 static void Main()
 {
  foreach(int hz in new[]{40,150})
  {
   var clock=new AgeOfWarClock(hz);int ticks=0;
   for(int i=0;i<600;i++)clock.Advance(1.0/60,true,()=>ticks++);
   Check(ticks==hz*10,"Source tick rate must not depend on display refresh rate: "+hz);
   clock.Advance(75,false,()=>ticks++);int before=ticks;
   clock.Advance(1.0/hz,true,()=>ticks++);
   Check(ticks==before+1,"Background time must not be replayed");
   clock.Reset();clock.Advance(double.NaN,true,()=>ticks++);clock.Advance(-1,true,()=>ticks++);
   Check(ticks==before+1,"Invalid clocks cannot corrupt the simulation accumulator");
  }
  var a=AgeOfWarViewportMath.Fit(10,20,2400,1080,650,450);
  Check(Math.Abs(a.Width/a.Height-650.0/450)<1e-6,"Original stage aspect preserved");
  Check(a.Left>=10&&a.Bottom>=20&&a.Left+a.Width<=2410.001&&a.Bottom+a.Height<=1100.001,"Inside safe area");
  double x,y;
  Check(a.ToStage(a.Left,a.Bottom+a.Height,out x,out y)&&Math.Abs(x)<1e-8&&Math.Abs(y)<1e-8,"Flash top left conversion");
  Check(!a.ToStage(a.Left-1,a.Bottom,out x,out y),"Letterbox never owns gameplay input");
  Check(AgeOfWarViewportMath.Fit(0,0,0,0,720,480).Width==0,"Transient zero sized surface is inert");
  Check(AgeOfWarCameraMath.Step(1,0,650,200,40,1,true)==-10,"First game paused menu still scrolls by source frame amount");
  Check(AgeOfWarCameraMath.Step(1,-349,650,200,40,1,false)==-350,"First game clamps after source movement");
  Check(AgeOfWarCameraMath.Step(2,800,720,200,150,1,false)==790,"Second game source tick camera movement");
  Check(AgeOfWarCameraMath.Step(2,800,720,200,150,2,false)==780,"Second game double speed also doubles source camera delta");
  Check(AgeOfWarCameraMath.Step(2,800,720,200,150,1,true)==800,"Second game paused camera freezes");
  Check(AgeOfWarCameraMath.Step(2,800,621,200,150,1,false)==800,"Second game rounds the camera every native tick");
  Check(AgeOfWarCameraMath.Step(1,0,550.99,200,40,1,false)==-.05,"Flash camera DisplayObject assignment truncates toward zero to twips");
  Check(AgeOfWarCameraMath.Step(1,-150.1,557,200,40,1,false)==-150.75,"Flash camera preserves double addition before twip truncation");
  var stage=new AgeOfWarStageClock();
  Check(stage.Sample(10.0019)==0,"First active stage event establishes the getTimer baseline");
  Check(Math.Abs(stage.Sample(10.0101)-.009)<1e-12,"Moon elapsed uses integer millisecond differences");
  Check(Math.Abs(stage.Sample(10.0435)-.033)<1e-12,"One stage event keeps actual elapsed time rather than fixed150Hz substeps");
  stage.Reset();Check(stage.Sample(90)==0,"Background resume discards time without replaying old stage events");
  Check(Math.Abs(stage.Sample(90.5)-.5)<1e-12,"Foreground elapsed is not silently capped unlike fixed-step catchup");
  Console.WriteLine("Age of War host checks: "+count+" passed");
 }
}
