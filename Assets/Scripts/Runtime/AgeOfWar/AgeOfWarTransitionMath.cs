using System;

namespace XanaduYonder.Runtime.AgeOfWar
{
    // Moon/Transitions/Transition_Checkers, Transition_Fade and Actors/SquareScaleAndRotate.
    // The manager observes completion on the next render; it does not remove a completed
    // transition during the render that changes isComplete.
    internal sealed class AgeOfWarTransitionState
    {
        internal readonly AgeOfWarTransitionTile[] Tiles;
        internal readonly bool Fade;
        internal bool FullBitmapVisible = true, Done;
        internal double Alpha = 1;
        private bool complete;
        private double timer = .2;

        internal AgeOfWarTransitionState(int stageWidth, int stageHeight, bool fade)
        {
            if (stageWidth < 24 || stageHeight < 16) throw new ArgumentOutOfRangeException("stageWidth");
            Fade = fade;
            Tiles = new AgeOfWarTransitionTile[fade ? 0 : 24 * 16];
            double width = stageWidth / 24.0, height = stageHeight / 16.0;
            for (int row = 0; !fade && row < 16; row++)
                for (int column = 0; column < 24; column++)
                    Tiles[row * 24 + column] = new AgeOfWarTransitionTile(column, row, width, height);
        }

        internal void Advance(double dt)
        {
            if (Done) return;
            if (complete) { Done = true; return; }
            if (Fade)
            {
                Alpha = timer / .2;
                timer -= dt;
                if (timer <= 0) complete = true;
                return;
            }
            FullBitmapVisible = false;
            int remaining = 0;
            foreach (var tile in Tiles)
            {
                if (!tile.Visible) tile.Removed = true;
                else tile.Advance(dt);
                if (!tile.Removed) remaining++;
            }
            if (remaining == 0) complete = true;
        }
    }

    internal sealed class AgeOfWarTransitionTile
    {
        internal readonly double X, Y, SourceX, SourceY, ImageX, ImageY;
        internal readonly int Width, Height;
        internal double StartTimer, TimeLeft = .3, Scale = 1, Rotation;
        internal bool Visible = true, Removed;
        private bool moving;

        internal AgeOfWarTransitionTile(int column, int row, double width, double height)
        {
            X = column * width + width / 2;
            Y = row * height + height / 2;
            SourceX = column * width;
            SourceY = row * height;
            ImageX = -width / 2;
            ImageY = -height / 2;
            // AS3 BitmapData's width and height are int; actor placement retains Number precision.
            Width = (int)width;
            Height = (int)height;
            StartTimer = (row + 1) * (column + 1) / 384.0;
        }

        internal void Advance(double dt)
        {
            if (!moving)
            {
                StartTimer -= dt;
                if (StartTimer <= 0) moving = true;
            }
            else
            {
                TimeLeft -= dt;
                Scale = TimeLeft / .3;
                Rotation = 90 * (1 - Scale);
                if (TimeLeft <= 0) Visible = false;
            }
        }
    }
}
