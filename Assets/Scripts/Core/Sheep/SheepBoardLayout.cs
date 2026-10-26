using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Sheep
{
    public static class SheepBoardLayout
    {
        public const int NewLayoutStart = 3;
        public const int ChallengeLayoutCount = 15;

        public static bool IsSupported(int level, int layoutId)
        {
            return (level == 1 && layoutId >= 0 && layoutId < NewLayoutStart) ||
                (level == 2 && layoutId >= 0 && layoutId < ChallengeLayoutCount);
        }

        public static SheepPosition[] GetPositions(int level, int layoutId)
        {
            if (level < 1 || level > 2) throw new ArgumentOutOfRangeException("level");
            if (!IsSupported(level, layoutId)) throw new ArgumentOutOfRangeException("layoutId");
            var positions = new List<SheepPosition>(level == 1 ? 18 : 144);
            if (layoutId >= NewLayoutStart)
            {
                AddSeparatedLayout(positions, layoutId - NewLayoutStart);
                return positions.ToArray();
            }
            if (level == 1)
            {
                AddGrid(positions, 4, 4, 4, 3, 0, layoutId);
                AddGrid(positions, 5, 5, 3, 2, 1, layoutId);
            }
            else
            {
                // Broad shallow edges surround a narrower, offset five-layer tower.
                AddGrid(positions, 2, 0, 6, 6, 0, layoutId);
                AddGrid(positions, 3, 1, 5, 6, 1, layoutId);
                AddGrid(positions, 4, 0, 4, 6, 2, layoutId);
                AddGrid(positions, 5, 1, 3, 6, 3, layoutId);
                AddGrid(positions, 4, 3, 3, 4, 4, layoutId);
                for (int side = 1; side <= 2; side++)
                for (int layer = 0; layer < 12; layer++)
                    positions.Add(new SheepPosition {
                        Id = positions.Count, X = side == 1 ? 0 : 14,
                        Y = layoutId == 2 ? 2 : 10, Layer = layer, BlindStack = side
                    });
            }
            return positions.ToArray();
        }

        private static void AddSeparatedLayout(List<SheepPosition> positions, int variantId)
        {
            int family = variantId / 3, variant = variantId % 3;
            // Separate islands keep genuine empty channels through every layer.
            // Shapes are immutable by id because saved games derive geometry from this id.
            switch (family)
            {
                case 0: // Two staggered horizontal islands, 54 cards each.
                    AddStaggeredPile(positions, 3 + variant, 4, 3, 2, 9, variant, true, true);
                    AddStaggeredPile(positions, 5 - variant, 11, 3, 2, 9, variant + 1, true, true);
                    break;
                case 1: // A crown above two narrow legs, 36 cards each.
                    AddStaggeredPile(positions, 4 + variant, 4, 3, 2, 6, variant, true, true);
                    AddStaggeredPile(positions, 1 + variant % 2, 10, 2, 3, 6, variant + 1, false, false);
                    AddStaggeredPile(positions, 10, 10, 2, 3, 6, variant, false, false);
                    break;
                case 2: // Four small islands with rotating missing corners, 27 each.
                    AddCornerPile(positions, 2 + variant % 2, 4 + variant / 2, 9, variant);
                    AddCornerPile(positions, 9, 4, 9, variant + 1);
                    AddCornerPile(positions, 3 - variant % 2, 11, 9, variant + 2);
                    AddCornerPile(positions, 9, 11, 9, variant + 3);
                    break;
                default: // A broad irregular upper shelf and two smaller feet: 72 + 18 + 18.
                    AddStaggeredPile(positions, 3 + variant % 2, 3, 4, 3, 6, variant, true, true);
                    AddCornerPile(positions, 2 + variant % 2, 11, 6, variant);
                    AddCornerPile(positions, 9, 11, 6, variant + 2);
                    break;
            }

            // Six shallow side faces each have one covered backup. The three-island
            // family puts these higher so they cannot join the lower legs into a slab.
            for (int layer = 0; layer < 2; layer++)
            for (int side = 0; side < 2; side++)
            for (int row = 0; row < 3; row++)
                Add(positions, side == 0 ? 0 : 14, 4 + row * (family == 1 ? 2 : 4), layer, 0);

            // The top corner blind stacks never cover the main or shallow faces.
            for (int side = 1; side <= 2; side++)
            for (int layer = 0; layer < 12; layer++)
                Add(positions, side == 1 ? 0 : 14, 0, layer, side);
        }

        private static void AddStaggeredPile(List<SheepPosition> positions, int x, int y, int columns,
            int rows, int layers, int variant, bool shiftVertically, bool shiftBothWays)
        {
            for (int layer = 0; layer < layers; layer++)
            {
                int dx = shiftBothWays ? (layer + variant) % 3 - 1 : (layer + variant) % 2;
                int dy = shiftVertically ? (layer + variant) % 2 : 0;
                for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                    Add(positions, x + column * 2 + dx + (row + layer + variant) % 2,
                        y + row * 2 + dy, layer, 0);
            }
        }

        private static void AddCornerPile(List<SheepPosition> positions, int x, int y, int layers, int variant)
        {
            for (int layer = 0; layer < layers; layer++)
            {
                int missing = (layer + variant) % 4;
                for (int corner = 0; corner < 4; corner++)
                {
                    if (corner == missing) continue;
                    Add(positions, x + (corner % 2) * 2 + (layer + variant) % 2,
                        y + (corner / 2) * 2 + layer % 2, layer, 0);
                }
            }
        }

        private static void Add(List<SheepPosition> positions, int x, int y, int layer, int blindStack)
        {
            positions.Add(new SheepPosition { Id = positions.Count, X = x, Y = y, Layer = layer, BlindStack = blindStack });
        }

        private static void AddGrid(List<SheepPosition> positions, int x, int y, int columns, int rows, int layer, int variant)
        {
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                int px = x + column * 2, py = y + row * 2;
                if (variant == 1) px = 14 - px;
                if (variant == 2) py = 12 - py;
                positions.Add(new SheepPosition { Id = positions.Count, X = px, Y = py, Layer = layer, BlindStack = 0 });
            }
        }

        internal static bool Overlaps(SheepPosition a, SheepPosition b)
        {
            // Coordinates are in half-tile units. Touching an edge is not covering a face.
            return Math.Abs(a.X - b.X) < 2 && Math.Abs(a.Y - b.Y) < 2;
        }

        internal static bool IsExposed(SheepPosition[] positions, SheepLocation[] locations, int id)
        {
            SheepPosition target = positions[id];
            for (int i = 0; i < positions.Length; i++)
                if (locations[i] == SheepLocation.Board && positions[i].Layer > target.Layer && Overlaps(positions[i], target))
                    return false;
            return true;
        }
    }
}
