using System;
using UnityEngine;
using XanaduYonder.Core.GoldMiner;

namespace XanaduYonder.Runtime.GoldMiner
{
    // Source-pixel measurements of the approved complete claw/load illustrations.
    // Scale by the metal suspension eye, never by the varying canvas or load bounds.
    internal readonly struct GoldMinerCaughtArt
    {
        public readonly string Resource;
        public readonly Vector2 Ring;
        public readonly float RingDiameter;
        public readonly Vector2 LoadCenter;
        private GoldMinerCaughtArt(string id, float ringX, float ringY, float diameter, float loadX, float loadY)
        {
            Resource = "characters/" + id;
            Ring = new Vector2(ringX / 1254f, 1 - ringY / 1254f);
            RingDiameter = diameter / 1254f;
            LoadCenter = new Vector2(loadX / 1254f, 1 - loadY / 1254f);
        }
        public static readonly Vector2 OpenRing = new Vector2(411f / 817f, 589f / 694f);
        public const float OpenRingDiameterPerHeight = 204f / 694f;
        private static readonly GoldMinerCaughtArt[] Types =
        {
            new GoldMinerCaughtArt("caught-gold-small", 626, 211.5f, 305, 626, 837),
            new GoldMinerCaughtArt("caught-gold-medium", 626.5f, 180.5f, 254, 630, 850),
            new GoldMinerCaughtArt("caught-gold-large", 627, 100.5f, 179, 632, 810),
            new GoldMinerCaughtArt("caught-rock-small", 628, 296.5f, 232, 627, 829),
            new GoldMinerCaughtArt("caught-rock-large", 628, 209, 188, 626, 868),
            new GoldMinerCaughtArt("caught-diamond", 626.5f, 180.5f, 254, 626, 857),
            new GoldMinerCaughtArt("caught-mystery-bag", 627, 200.5f, 208, 621, 806),
            new GoldMinerCaughtArt("caught-mole", 626.5f, 136, 210, 653, 843),
            new GoldMinerCaughtArt("caught-diamond-mole", 626, 136, 211, 656, 846)
        };
        public static GoldMinerCaughtArt For(GoldMinerKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= Types.Length) throw new ArgumentOutOfRangeException(nameof(kind));
            return Types[index];
        }
    }
}
