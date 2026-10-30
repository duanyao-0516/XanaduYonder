using System;

namespace XanaduYonder.Runtime.AgeOfWar
{
    public struct AgeOfWarViewportRect
    {
        public double Left,Bottom,Width,Height,StageWidth,StageHeight;
        public bool ToStage(double screenX,double screenY,out double x,out double y)
        {
            x=y=0;
            if(Width<=0||Height<=0||!AgeOfWarViewportMath.Finite(screenX)||!AgeOfWarViewportMath.Finite(screenY))return false;
            x=(screenX-Left)*StageWidth/Width;y=(Bottom+Height-screenY)*StageHeight/Height;
            return x>=0&&x<=StageWidth&&y>=0&&y<=StageHeight;
        }
    }
    public static class AgeOfWarViewportMath
    {
        internal static bool Finite(double n){return !double.IsNaN(n)&&!double.IsInfinity(n);}
        public static AgeOfWarViewportRect Fit(double left,double bottom,double width,double height,double stageWidth,double stageHeight)
        {
            if(!Finite(left)||!Finite(bottom)||!Finite(width)||!Finite(height)||!Finite(stageWidth)||!Finite(stageHeight)||width<=0||height<=0||stageWidth<=0||stageHeight<=0)return new AgeOfWarViewportRect();
            var scale=Math.Min(width/stageWidth,height/stageHeight);var w=stageWidth*scale;var h=stageHeight*scale;
            return new AgeOfWarViewportRect{Left=left+(width-w)/2,Bottom=bottom+(height-h)/2,Width=w,Height=h,StageWidth=stageWidth,StageHeight=stageHeight};
        }
        public static AgeOfWarViewportRect FillWidth(double left,double bottom,double width,double height,double stageWidth,double stageHeight)
        {
            var original=Fit(left,bottom,width,height,stageWidth,stageHeight);
            if(original.Width<=0||width/height<stageWidth/stageHeight)return original;
            // Keep the original vertical scale and expose more logical world on wide displays.
            return new AgeOfWarViewportRect{Left=left,Bottom=bottom,Width=width,Height=height,StageWidth=width*stageHeight/height,StageHeight=stageHeight};
        }
    }
}
