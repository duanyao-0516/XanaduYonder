using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Sheep
{
    public static class SheepBoardLayout
    {
        public static SheepPosition[] GetPositions(int level, int layoutId)
        {
            if (level < 1 || level > 2) throw new ArgumentOutOfRangeException("level");
            if (layoutId < 0 || layoutId > 2) throw new ArgumentOutOfRangeException("layoutId");
            var positions = new List<SheepPosition>(level == 1 ? 18 : 144);
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
