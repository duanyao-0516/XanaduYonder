namespace XanaduYonder.Core.AgeOfWar.Two
{
    /// <summary>Windows/*Info.render: independent alpha per window, including paused HUD renders.</summary>
    public sealed class AgeOfWarTwoWindowFade
    {
        private double alpha;
        public double Alpha {get{return alpha;}set{alpha=AgeOfWarTwoBattle.DisplayAlpha(value);}}
        public bool Visible;
        public void Advance(double seconds)
        {
            if(Visible){if(Alpha<1)Alpha+=4*seconds;else Alpha=1;}
            else {if(Alpha>0)Alpha-=4*seconds;else Alpha=0;}
        }
    }
}
