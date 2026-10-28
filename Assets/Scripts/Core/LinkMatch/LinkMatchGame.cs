using System;

namespace XanaduYonder.Core.LinkMatch
{
    public sealed class LinkMatchGame
    {
        private int[] tiles;
        public LinkMatchLevel Level { get; private set; }
        public int Remaining { get; private set; }
        public bool Won { get { return Remaining == 0; } }
        public LinkMatchGame(LinkMatchLevel level, int[] tiles)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            if (tiles.Length != level.Columns * level.Rows) throw new ArgumentException("Board length does not match level.", nameof(tiles));
            var counts = new int[level.Kinds];
            for (int i = 0; i < tiles.Length; i++) {
                if (tiles[i] < -1 || tiles[i] >= counts.Length) throw new ArgumentException("Unknown fruit id.", nameof(tiles));
                if (tiles[i] >= 0) { counts[tiles[i]]++; Remaining++; }
            }
            for (int kind = 0; kind < counts.Length; kind++)
                if (counts[kind] > 4 || counts[kind] % 2 != 0) throw new ArgumentException("Each fruit must have zero, two or four remaining tiles.", nameof(tiles));
            Level = level; this.tiles = (int[])tiles.Clone();
        }
        public int TileAt(int index)
        {
            if (index < 0 || index >= tiles.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return tiles[index];
        }
        public int[] GetTiles() { return (int[])tiles.Clone(); }
        public bool TryMatch(int first, int second, out LinkMatchPath path)
        {
            path = FindPath(first, second);
            if (path == null) return false;
            tiles[first] = tiles[second] = -1; Remaining -= 2;
            return true;
        }
        public LinkMatchPair FindMatch()
        {
            for (int first = 0; first < tiles.Length; first++) {
                if (tiles[first] < 0) continue;
                for (int second = first + 1; second < tiles.Length; second++) {
                    var path = FindPath(first, second);
                    if (path != null) return new LinkMatchPair(first, second, path);
                }
            }
            return null;
        }
        public LinkMatchDeal Shuffle(uint seed)
        {
            var deal = LinkMatchGenerator.Reshuffle(Level, tiles, seed);
            tiles = deal.Tiles;
            return deal;
        }
        private LinkMatchPath FindPath(int first, int second)
        {
            if (first < 0 || second < 0 || first >= tiles.Length || second >= tiles.Length || first == second || tiles[first] < 0 || tiles[first] != tiles[second]) return null;
            return LinkMatchPathFinder.Find(Level, tiles, first, second);
        }
    }
}
