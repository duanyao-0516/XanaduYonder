using System;
namespace XanaduYonder.Core.AgeOfWar.Two
{
    /// <summary>MainMenu/Difficulty/Credits colorTransform tween; ColorTransformPlugin gives exposure priority over brightness.</summary>
    public sealed class AgeOfWarTwoExposure
    {
        private bool hovered;
        private double from,target,started,duration;
        public void Hover(bool value,double now,double outDuration)
        {
            if(value==hovered)return;
            from=Sample(now);hovered=value;target=value?.3:0;started=now;duration=value?.2:outDuration;
        }
        public double Sample(double now)
        {
            if(duration<=0)return target;
            double fraction=Math.Max(0,Math.Min(1,(now-started)/duration));
            return from+(target-from)*(1-(1-fraction)*(1-fraction));
        }
    }
}
