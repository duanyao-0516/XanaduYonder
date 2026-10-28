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
        public readonly Rect Help;
        public readonly Rect Status;
        public readonly float HeadingY;
        public readonly float ScoresY;
        public double WorldWidth => Track.width / 1.2;
        public float Bottom => Mathf.Max(Jump.yMax, Pause.yMax) + 16;

        public DinoPageLayout(float width, float height)
        {
            Landscape = width > height;
            float innerWidth = width - 64;
            if (Landscape)
            {
                HeadingY = 104;
                ScoresY = 100;
                Track = new Rect(32, 172, innerWidth, Mathf.Max(0, Mathf.Min(320, height - 400)));
                float controlsY = height - 200;
                float duckWidth = Mathf.Min(248, (innerWidth - 184) * .45f);
                float jumpWidth = Mathf.Min(300, (innerWidth - 184) * .55f);
                Duck = new Rect(32, controlsY, duckWidth, 152);
                Jump = new Rect(width - 32 - jumpWidth, controlsY, jumpWidth, 152);
                float gapWidth = Jump.x - Duck.xMax;
                Pause = new Rect(Duck.xMax + (gapWidth - 136) * .5f, controlsY + 4, 136, 76);
                Help = default(Rect);
                Status = new Rect(Duck.xMax + 8, controlsY + 96, gapWidth - 16, 42);
            }
            else
            {
                Track = new Rect(32, Mathf.Max(344, height - 824), innerWidth, 320);
                ScoresY = Track.y - 128;
                HeadingY = Mathf.Max(112, 112 + (ScoresY - 230) * .4f);
                Duck = new Rect(32, height - 380, 208, 168);
                Jump = new Rect(256, height - 380, width - 288, 168);
                Help = new Rect(32, height - 164, 208, 76);
                Pause = new Rect(width - 240, height - 164, 208, 76);
                Status = new Rect(56, Duck.y - 64, width - 112, 34);
            }
        }
    }
}
