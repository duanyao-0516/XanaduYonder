using System;

namespace XanaduYonder.Core.GoldMiner
{
    /// <summary>Versioned mine geometry. Economy stays in SourceRules; v1 saves keep their exact original coordinates.</summary>
    public static class GoldMinerSourceLayout
    {
        public const int OriginalCatalogVersion = 2;
        public const int CurrentCatalogVersion = 3;
        public static bool IsNative(int catalogVersion) { return catalogVersion == CurrentCatalogVersion; }
        public static double Width(GoldMinerMode mode, int catalogVersion)
        {
            return IsNative(catalogVersion) ? (mode == GoldMinerMode.Solo ? 6 : 12) : (mode == GoldMinerMode.Solo ? 6.4 : 5.5);
        }
        public static double Height(GoldMinerMode mode, int catalogVersion)
        {
            return IsNative(catalogVersion) ? (mode == GoldMinerMode.Solo ? 10.2 : 4.4) : (mode == GoldMinerMode.Solo ? 4.34 : 3.45);
        }
        public static double OriginX(GoldMinerMode mode, int player, int catalogVersion)
        {
            return IsNative(catalogVersion) ? (player == 0 ? 3 : 9) : (mode == GoldMinerMode.Solo ? 3.2 : player == 0 ? 1.85 : 2.85);
        }
        // Expanding the physical mine must not double the cost of retrieving the same gold.
        // Both extension and retraction use this conversion, leaving source weights/buffs unchanged.
        public static double SpeedScale(GoldMinerMode mode, int catalogVersion)
        {
            return IsNative(catalogVersion) ? (mode == GoldMinerMode.Solo ? 2.1 : 1.65) : 1;
        }
        public static double Radius(GoldMinerKind kind)
        {
            switch (kind)
            {
                case GoldMinerKind.GoldSmall: return .20;
                case GoldMinerKind.GoldMedium: return .31;
                case GoldMinerKind.Gold250: return .37;
                case GoldMinerKind.GoldLarge: return .45;
                case GoldMinerKind.RockSmall: return .23;
                case GoldMinerKind.RockLarge: return .41;
                case GoldMinerKind.Diamond: return .18;
                case GoldMinerKind.MysteryBag: return .24;
                case GoldMinerKind.Mole: return .22;
                case GoldMinerKind.DiamondMole: return .24;
                case GoldMinerKind.Tnt: return .29;
                case GoldMinerKind.Bone: return .22;
                case GoldMinerKind.Skull: return .23;
                default: throw new ArgumentOutOfRangeException("kind");
            }
        }
        static double Clamp(double n, double min, double max) { return Math.Max(min, Math.Min(max, n)); }
        // Signatures and saves use six decimal world units, independent of runtime arithmetic contraction.
        static double Canonical(double value) { return Math.Round(value, 6, MidpointRounding.AwayFromZero); }
        static double Unit(ref uint random) { return GoldMinerRules.NextUInt(ref random) / (double)uint.MaxValue; }
        static bool Reachable(GoldMinerMode mode, double x, double y, double margin)
        {
            for (int p = 0; p < GoldMinerRules.PlayerCount(mode); p++)
                if (Math.Abs(x - OriginX(mode, p, CurrentCatalogVersion)) <= Math.Sqrt(3) * (y - margin)) return true;
            return false;
        }
        static double ReachableX(GoldMinerMode mode, double x, double y, double margin)
        {
            double best = x, distance = double.MaxValue;
            for (int p = 0; p < GoldMinerRules.PlayerCount(mode); p++)
            {
                double origin = OriginX(mode, p, CurrentCatalogVersion), span = Math.Sqrt(3) * (y - margin);
                double candidate = Clamp(x, origin - span, origin + span);
                if (Math.Abs(candidate - x) < distance) { best = candidate; distance = Math.Abs(candidate - x); }
            }
            return best;
        }
        static bool Free(GoldMinerMode mode, GoldMinerEntity[] entities, bool[] placed, int index, double x, double y)
        {
            double r = entities[index].Source.Radius;
            if (!Reachable(mode, x, y, r * .35)) return false;
            for (int j = 0; j < entities.Length; j++)
            {
                if (!placed[j]) continue;
                double dx = x - entities[j].X, dy = y - entities[j].Y;
                double gap = r + entities[j].Source.Radius + .08;
                if (dx * dx + dy * dy < gap * gap) return false;
            }
            return true;
        }
        /// <summary>Preserve each source map's inventory and rough left/right grouping, with a bounded irregular native layout.</summary>
        public static void Arrange(GoldMinerMode mode, int map, GoldMinerEntity[] entities)
        {
            double width = Width(mode, CurrentCatalogVersion), height = Height(mode, CurrentCatalogVersion);
            double originalWidth = Width(mode, OriginalCatalogVersion);
            double minY = double.MaxValue, maxY = double.MinValue;
            double minX = double.MaxValue, maxX = double.MinValue;
            var order = new int[entities.Length];
            for (int i = 0; i < entities.Length; i++)
            {
                order[i] = i;
                double normalizedX = Clamp(entities[i].Source.InitialX / originalWidth, .03, .97);
                minX = Math.Min(minX, normalizedX); maxX = Math.Max(maxX, normalizedX);
                minY = Math.Min(minY, entities[i].Source.InitialY);
                maxY = Math.Max(maxY, entities[i].Source.InitialY);
                entities[i].Source.Radius = Radius(entities[i].Kind);
            }
            // Place large ores first; original array order/IDs and every source economic value remain intact.
            Array.Sort(order, (a, b) => {
                int size = entities[b].Source.Radius.CompareTo(entities[a].Source.Radius);
                return size == 0 ? a.CompareTo(b) : size;
            });
            var placed = new bool[entities.Length];
            uint random = unchecked(0x71d493b5u + (uint)map * 104729u + (uint)mode * 15485863u);
            foreach (int i in order)
            {
                var e = entities[i]; var d = e.Source;
                double r = d.Radius, margin = r + .16;
                double left = margin, right = width - margin;
                double top = mode == GoldMinerMode.Solo ? 1.1 : .86, bottom = height - margin;
                double horizontal = (Clamp(d.InitialX / originalWidth, .03, .97) - minX) / Math.Max(.01, maxX - minX);
                double desiredX = horizontal * (right - left) + left;
                double depth = (d.InitialY - minY) / Math.Max(.01, maxY - minY);
                double desiredY = top + depth * (bottom - top);
                desiredX = Clamp(desiredX + (Unit(ref random) - .5) * (mode == GoldMinerMode.Solo ? .48 : .8), left, right);
                desiredY = Clamp(desiredY + (Unit(ref random) - .5) * .75 * (1 - .7 * depth), top, bottom);
                bool found = false;
                for (int attempt = 0; attempt < 1024; attempt++)
                {
                    double x = desiredX, y = desiredY;
                    if (attempt > 0)
                    {
                        if (attempt < 160)
                        {
                            double spread = attempt < 64 ? .65 : 1.5;
                            x = Clamp(desiredX + (Unit(ref random) * 2 - 1) * spread, left, right);
                            y = Clamp(desiredY + (Unit(ref random) * 2 - 1) * spread, top, bottom);
                        }
                        else { x = left + Unit(ref random) * (right - left); y = top + Unit(ref random) * (bottom - top); }
                    }
                    x = Clamp(ReachableX(mode, x, y, r * .4), left, right);
                    if (!Free(mode, entities, placed, i, x, y)) continue;
                    // Moving ores keep their source direction, but their patrol stays inside the actual mine.
                    bool moves = d.MotionEndX != d.InitialX && mode == GoldMinerMode.Solo && (d.Type == 6 || d.Type == 10);
                    e.X = d.InitialX = Canonical(x); e.Y = d.InitialY = Canonical(y);
                    d.MotionEndX = moves ? Canonical(Clamp(e.X + (d.Flip ? 1 : -1) * .95, left, right)) : e.X;
                    placed[i] = true; found = true; break;
                }
                if (!found) throw new InvalidOperationException("Cannot place native source map " + map + " object " + e.Id + ".");
            }
        }
    }
}
