using UnityEngine;

namespace XanaduYonder.Runtime.Dino
{
    // Page geometry owns the visible track. Physics receives this exact width, not the screen width.
    internal readonly struct DinoPageLayout
    {
        public readonly bool Landscape;
        public readonly Rect Track;
        public readonly Rect Duck;
        public readonly Rect Jump;
        public readonly Rect Pause;
        public readonly Rect Direction;
        public readonly float HintY;
        public readonly float HeadingY;
        public readonly float ScoresY;
        public double WorldWidth => Track.width / 1.2;
        public float Bottom => Mathf.Max(Jump.yMax, Pause.yMax) + 54;

        public DinoPageLayout(float width, float height)
        {
            Landscape = width > height;
            float innerWidth = width - 112;
            float extra = Mathf.Max(0, height - UiFactory.DesignHeight);
            HeadingY = Landscape ? 102 : 112 + extra * .08f;
            ScoresY = Landscape ? 102 : 230 + extra * .16f;
            float top = Landscape ? 222 : 366 + extra * .45f;
            float trackHeight = Landscape ? Mathf.Min(320, height - top - 224) : 320;
            Track = new Rect(56, top, innerWidth, Mathf.Max(0, trackHeight));
            float controlsY = Track.yMax + 28;
            float actionWidth = Landscape ? Mathf.Min(296, (innerWidth - 184) * .5f) : (innerWidth - 16) * .5f;
            Duck = new Rect(56, controlsY, actionWidth, 96);
            Jump = new Rect(width - 56 - actionWidth, controlsY, actionWidth, 96);
            HintY = Jump.yMax + 16;
            Pause = Landscape ? new Rect(width * .5f - 68, controlsY + 10, 136, 76)
                : new Rect(width * .5f + 8, HintY + 62, actionWidth, 76);
            Direction = Landscape ? Pause : new Rect(56, Pause.y, actionWidth, 76);
        }
    }
}
