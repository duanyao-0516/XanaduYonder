using System;
using System.Collections.Generic;
using XanaduYonder.Core.Sheep;

namespace XanaduYonder.Runtime.Sheep
{
    internal struct SheepTileBounds
    {
        public readonly float X, Y, Width, Height;
        public SheepTileBounds(float x, float y, float width, float height)
        { X = x; Y = y; Width = width; Height = height; }
    }

    // Reflow belongs to presentation. Core coordinates and saved layout ids remain unchanged.
    // Only completely empty horizontal channels can grow; no card overlap can change.
    internal sealed class SheepBoardGeometry
    {
        public const float TileWidth = 72, TileHeight = 81;
        private const float Pitch = 36, Padding = 8, MaximumExtraGap = 72;
        private const float FanX = 2.7f, FanY = .35f;
        private readonly float left, sourceTop;
        private readonly List<Band> bands = new List<Band>();
        public float Scale { get; private set; }
        public float AddedGap { get; private set; }
        public float ContentHeight { get; private set; }
        public float TopPadding { get; private set; }

        private struct Band
        {
            public float Top, Bottom;
            public Band(float top, float bottom) { Top = top; Bottom = bottom; }
        }

        public SheepBoardGeometry(SheepPosition[] positions, float width, float height)
        {
            if (positions == null || positions.Length == 0) throw new ArgumentException("A board needs positions.", "positions");
            var intervals = new List<Band>(positions.Length);
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            foreach (var position in positions)
            {
                var x = position.X * Pitch;
                var y = position.Y * Pitch;
                minX = Math.Min(minX, x - (position.BlindStack == 1 ? 11 * FanX : 0));
                maxX = Math.Max(maxX, x + TileWidth + (position.BlindStack == 2 ? 11 * FanX : 0));
                // Blind stack counts sit beneath the full fan and reserve their own clear space.
                if (position.BlindStack > 0)
                {
                    minX = Math.Min(minX, x - 6);
                    maxX = Math.Max(maxX, x + 78);
                }
                intervals.Add(new Band(y, y + (position.BlindStack > 0 ? 118 : TileHeight)));
            }
            intervals.Sort((a, b) => a.Top.CompareTo(b.Top));
            foreach (var interval in intervals)
            {
                if (bands.Count == 0 || interval.Top > bands[bands.Count - 1].Bottom)
                    bands.Add(interval);
                else
                {
                    var band = bands[bands.Count - 1];
                    band.Bottom = Math.Max(band.Bottom, interval.Bottom);
                    bands[bands.Count - 1] = band;
                }
            }
            sourceTop = bands[0].Top;
            var sourceHeight = bands[bands.Count - 1].Bottom - sourceTop;
            var sourceWidth = maxX - minX;
            var availableWidth = Math.Max(1, width - Padding * 2);
            var availableHeight = Math.Max(1, height - Padding * 2);
            Scale = Math.Min(availableWidth / sourceWidth, availableHeight / sourceHeight);
            AddedGap = bands.Count > 1 ? Math.Min(MaximumExtraGap,
                Math.Max(0, availableHeight - sourceHeight * Scale) / (bands.Count - 1)) : 0;
            ContentHeight = sourceHeight * Scale + AddedGap * (bands.Count - 1);
            TopPadding = (height - ContentHeight) * .5f;
            left = (width - sourceWidth * Scale) * .5f - minX * Scale;
        }

        public SheepTileBounds TileBounds(SheepPosition position, int blindDepth)
        {
            var depth = position.BlindStack > 0 ? Math.Max(0, Math.Min(11, blindDepth)) : 0;
            var side = position.BlindStack == 1 ? -1 : 1;
            return new SheepTileBounds(left + (position.X * Pitch + side * depth * FanX) * Scale,
                MapY(position.Y * Pitch) + depth * FanY * Scale, TileWidth * Scale, TileHeight * Scale);
        }

        public SheepTileBounds StackLabelBounds(SheepPosition position)
        {
            return new SheepTileBounds(left + (position.X * Pitch - 6) * Scale,
                MapY(position.Y * Pitch) + 92 * Scale, 84 * Scale, 26 * Scale);
        }

        private float MapY(float y)
        {
            var index = 0;
            while (index + 1 < bands.Count && y >= bands[index + 1].Top) index++;
            return TopPadding + (y - sourceTop) * Scale + index * AddedGap;
        }
    }
}
