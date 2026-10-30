using System;
namespace XanaduYonder.Runtime.AgeOfWar
{
    public static class AgeOfWarCameraMath
    {
        public static double Clamp(int edition,double x,double visibleWidth)
        {
            if(!AgeOfWarViewportMath.Finite(visibleWidth)||visibleWidth<=0)visibleWidth=edition==1?650:720;
            double left=edition==1?0:-900,right=edition==1?1000:900;
            if(visibleWidth>=right-left)return (visibleWidth-left-right)/2;
            return Math.Max(visibleWidth-right,Math.Min(-left,x));
        }
        public static double Step(int edition,double x,double pointerX,double pointerY,int tickRate,int speed,bool paused,double visibleWidth=0)
        {
            if(visibleWidth<=0)visibleWidth=edition==1?650:720;
            x=Clamp(edition,x,visibleWidth);
            if(edition==1)
            {
                if(pointerY>120)
                {
                    if(pointerX>visibleWidth-100)x-=(pointerX-(visibleWidth-100))/10;
                    if(pointerX<100)x+=(100-pointerX)/10;
                    x=Clamp(1,x,visibleWidth);
                }
                // AVM1 DisplayObject coordinates are signed integer twips. Keep the
                // double expression intact before truncation (original runtime probe).
                return (int)(x*20)/20.0;
            }
            return StepTwo(x,pointerX,pointerY,speed/(double)tickRate,paused,visibleWidth);
        }
        public static double StepTwo(double x,double pointerX,double pointerY,double elapsed,bool paused,double visibleWidth=720)
        {
            x=Clamp(2,x,visibleWidth);
            if(paused)return x;
            if(pointerY>75&&pointerY<460)
            {
                double dt=elapsed;
                if(pointerX>visibleWidth-100)x-= (pointerX-(visibleWidth-100))*dt*15;
                else if(pointerX<100)x+=(100-pointerX)*dt*15;
                x=Clamp(2,x,visibleWidth);
            }
            return Math.Floor(x+.5);
        }
    }
}
