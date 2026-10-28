using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.LinkMatch
{
    public static class LinkMatchGenerator
    {
        public static LinkMatchDeal Generate(LinkMatchLevel level, uint seed)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            var tiles = new int[level.Columns * level.Rows];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = i / 4;
            return Reshuffle(level, tiles, seed);
        }
        public static LinkMatchDeal Reshuffle(LinkMatchLevel level, int[] tiles, uint seed)
        {
            var validated = new LinkMatchGame(level, tiles);
            var mask = validated.GetTiles();
            var result = new int[mask.Length];
            var counts = new int[level.Kinds];
            for (int i = 0; i < mask.Length; i++) { result[i] = -1; if (mask[i] >= 0) counts[mask[i]]++; }
            var kinds = new List<int>();
            for (int kind = 0; kind < counts.Length; kind++) for (int n = 0; n < counts[kind] / 2; n++) kinds.Add(kind);
            var random = new GeneratorRandom(seed); random.Shuffle(kinds);
            var witness = new LinkMatchPair[kinds.Count];
            for (int step = 0; step < witness.Length; step++) {
                var occupied = new List<int>();
                for (int i = 0; i < mask.Length; i++) if (mask[i] >= 0) occupied.Add(i);
                random.Shuffle(occupied);
                var pair = FindGeometricPair(level, mask, occupied);
                result[pair.First] = result[pair.Second] = kinds[step];
                mask[pair.First] = mask[pair.Second] = -1;
                witness[step] = pair;
            }

            // Validate the complete witness through the same public rule used by
            // real player submissions, and retain its exact display paths.
            var replay = new LinkMatchGame(level, result);
            for (int step = 0; step < witness.Length; step++) {
                var pair = witness[step]; LinkMatchPath path;
                if (!replay.TryMatch(pair.First, pair.Second, out path)) throw new InvalidOperationException("Generated witness failed production-rule replay.");
                witness[step] = new LinkMatchPair(pair.First, pair.Second, path);
            }
            if (!replay.Won) throw new InvalidOperationException("Generated witness did not clear the board.");
            return new LinkMatchDeal(result, witness);
        }
        private static LinkMatchPair FindGeometricPair(LinkMatchLevel level, int[] mask, List<int> occupied)
        {
            // Finite exhaustive pair scan after shuffling, never random retries.
            // Progress is guaranteed: a row with >=2 tiles has consecutive tiles
            // joined by a clear horizontal segment. Otherwise every occupied row
            // has one tile, and tiles in consecutive occupied rows join with one
            // elbow through the empty intervening rows. Removing any pair leaves
            // the same property, so every even occupied mask can be exhausted.
            for (int a = 0; a < occupied.Count; a++) for (int b = a + 1; b < occupied.Count; b++) {
                var path = LinkMatchPathFinder.Find(level, mask, occupied[a], occupied[b]);
                if (path != null) return new LinkMatchPair(occupied[a], occupied[b], path);
            }
            throw new InvalidOperationException("An even occupied mask must contain a geometric pair.");
        }
        // Local fixed algorithm makes seeds reproducible across Unity/.NET versions.
        private sealed class GeneratorRandom
        {
            private uint state;
            internal GeneratorRandom(uint seed) { state = seed; }
            private int Next(int exclusiveMax)
            {
                unchecked { state = state * 1664525u + 1013904223u; }
                return (int)(((ulong)state * (uint)exclusiveMax) >> 32);
            }
            internal void Shuffle(List<int> values)
            {
                for (int i = values.Count - 1; i > 0; i--) {
                    int j = Next(i + 1), saved = values[i]; values[i] = values[j]; values[j] = saved;
                }
            }
        }
    }
}
