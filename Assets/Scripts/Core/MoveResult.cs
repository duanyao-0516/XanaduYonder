using System.Collections.Generic;

namespace XanaduYonder.Core
{
    public sealed class TileMotion
    {
        public int FromIndex { get; private set; }
        public int ToIndex { get; private set; }
        public int Value { get; private set; }
        public bool Merged { get; private set; }

        internal TileMotion(int fromIndex, int toIndex, int value, bool merged)
        {
            FromIndex = fromIndex;
            ToIndex = toIndex;
            Value = value;
            Merged = merged;
        }
    }

    public sealed class MoveResult
    {
        public bool Changed { get; private set; }
        public long ScoreDelta { get; private set; }
        public List<TileMotion> Motions { get; private set; }
        public int SpawnIndex { get; private set; }
        public int SpawnValue { get; private set; }

        internal MoveResult(bool changed, long scoreDelta, List<TileMotion> motions, int spawnIndex, int spawnValue)
        {
            Changed = changed;
            ScoreDelta = scoreDelta;
            Motions = motions;
            SpawnIndex = spawnIndex;
            SpawnValue = spawnValue;
        }
    }
}
