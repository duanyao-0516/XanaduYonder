using System;
using System.IO;
using UnityEngine;
using XanaduYonder.Core.BakeryBasket;

namespace XanaduYonder.Runtime.BakeryBasket
{
    internal static class BakeryOpeningPoses
    {
        internal const int Seed = 47;
        internal const int Format = 1;
        internal const string ResourcePath = "BakeryBasket/OpeningPoses";
        private static readonly Lazy<Pose[][]> Openings = new Lazy<Pose[][]>(Load);

        internal static Pose[] Get(int seed, BakeryTheme theme, bool tapered) =>
            seed == Seed ? Openings.Value?[(int)theme * 2 + (tapered ? 1 : 0)] : null;

        private static Pose[][] Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            int count = BakeryThemeCatalog.All.Count * 2;
            const int poseBytes = 7 * sizeof(float);
            if (asset == null || asset.dataSize != 3 * sizeof(int) + count * BakeryRound.PieceCount * poseBytes) return null;
            using var reader = new BinaryReader(new MemoryStream(asset.bytes, false));
            if (reader.ReadInt32() != Format || reader.ReadInt32() != Seed || reader.ReadInt32() != count) return null;
            var layouts = new Pose[count][];
            for (int layout = 0; layout < count; layout++)
            {
                var poses = new Pose[BakeryRound.PieceCount];
                for (int id = 0; id < poses.Length; id++)
                {
                    var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    var rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    if (!(position.sqrMagnitude < 100 && position.y > -.45f &&
                        Mathf.Abs(Quaternion.Dot(rotation, rotation) - 1) < .01f)) return null;
                    poses[id] = new Pose(position, rotation);
                }
                layouts[layout] = poses;
            }
            return layouts;
        }
    }
}
