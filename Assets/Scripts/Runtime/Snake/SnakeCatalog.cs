using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Snake;

namespace XanaduYonder.Runtime.Snake
{
    /// <summary>The approved first pack remains a draft pending device tuning. Coordinates are row-major.</summary>
    public sealed class SnakeCatalog
    {
        public const string SupportedRulesVersion = "snake-rules-v1-draft";
        public const string SupportedPackVersion = "snake-levels-v1-draft-20261028";
        private readonly SnakeRules[] levels;
        private readonly string[] names;
        private SnakeCatalog(SnakeRules[] levels, string[] names) { this.levels = levels; this.names = names; }
        public int Count { get { return levels.Length; } }
        public string PackVersion { get { return SupportedPackVersion; } }
        public string RulesVersion { get { return SupportedRulesVersion; } }
        public SnakeRules Get(int level) { CheckIndex(level); return levels[level].Copy(); }
        public string Name(int level) { CheckIndex(level); return names[level]; }
        public int FindLevel(string id)
        { for (int i = 0; i < levels.Length; i++) if (levels[i].LevelId == id) return i; throw new FormatException("Unknown snake level id."); }
        private void CheckIndex(int level) { if (level < 0 || level >= levels.Length) throw new ArgumentOutOfRangeException(nameof(level)); }

        public static SnakeCatalog Parse(string json)
        {
            JObject root = SnakeJson.Parse(json);
            SnakeJson.Fields(root, "schemaVersion", "draft", "rulesVersion", "packVersion", "defaults", "levels");
            Require(SnakeJson.Int(root["schemaVersion"]) == 1 && SnakeJson.Bool(root["draft"]), "Unsupported level schema or draft status.");
            Require(SnakeJson.Text(root["rulesVersion"]) == SupportedRulesVersion && SnakeJson.Text(root["packVersion"]) == SupportedPackVersion, "Unsupported snake content version.");
            var defaults = SnakeJson.Object(root["defaults"]);
            SnakeJson.Fields(defaults, "columns", "rows", "coordinateSystem", "initialSnake", "initialDirection", "initialSafeSteps", "growthPerFood", "speedPolicy");
            Require(SnakeJson.Int(defaults["columns"]) == 20 && SnakeJson.Int(defaults["rows"]) == 24, "Unexpected board dimensions.");
            var coordinates = SnakeJson.Object(defaults["coordinateSystem"]);
            SnakeJson.Fields(coordinates, "origin", "indexBase", "xDirection", "yDirection");
            Require(SnakeJson.Text(coordinates["origin"]) == "top-left" && SnakeJson.Int(coordinates["indexBase"]) == 0 &&
                SnakeJson.Text(coordinates["xDirection"]) == "right" && SnakeJson.Text(coordinates["yDirection"]) == "down", "Unsupported coordinate system.");
            int[] body = Coordinates(defaults["initialSnake"]);
            Require(body.Length == 4 && body[0] == 406 && body[1] == 405 && body[2] == 404 && body[3] == 403, "Unexpected initial snake.");
            Require(SnakeJson.Text(defaults["initialDirection"]) == "right" && SnakeJson.Int(defaults["initialSafeSteps"]) == 3 &&
                SnakeJson.Int(defaults["growthPerFood"]) == 1 && SnakeJson.Text(defaults["speedPolicy"]) == "fixed-per-level", "Unsupported initial or speed policy.");
            var source = SnakeJson.Array(root["levels"], 12);
            var rules = new SnakeRules[12]; var names = new string[12]; var usedNames = new HashSet<string>();
            string[] chapters = { "01-basics", "02-islands", "03-dividers", "04-corridors" };
            int previousTick = 220, previousTarget = 8, wrapCount = 0;
            for (int i = 0; i < source.Count; i++)
            {
                var level = SnakeJson.Object(source[i]);
                SnakeJson.Fields(level, "id", "name", "chapter", "boundary", "tickMs", "targetFood", "obstacles");
                string id = SnakeJson.Text(level["id"]), name = SnakeJson.Text(level["name"]), boundary = SnakeJson.Text(level["boundary"]);
                Require(id == "snake-" + (i + 1).ToString("00") && SnakeJson.Text(level["chapter"]) == chapters[i / 3], "Unexpected level order or chapter.");
                Require(name.Trim().Length > 0 && name.Trim().Length <= 40 && usedNames.Add(name), "Invalid or duplicate level name.");
                Require(boundary == "wrap" || boundary == "solid", "Unknown level boundary.");
                int tick = SnakeJson.Int(level["tickMs"]), target = SnakeJson.Int(level["targetFood"]);
                Require(tick >= 140 && tick <= previousTick && target >= previousTarget && target <= 30, "Invalid target or speed progression.");
                int[] walls = Coordinates(level["obstacles"]);
                if (i < 2) Require(walls.Length == 0 && boundary == (i == 0 ? "wrap" : "solid"), "Invalid introductory level.");
                rules[i] = new SnakeRules { Mode = SnakeMode.Campaign, Difficulty = 0, LevelId = id, Width = 20, Height = 24,
                    Boundary = boundary == "wrap" ? SnakeBoundary.Wrap : SnakeBoundary.Solid, InitialTickMs = tick, MinimumTickMs = tick,
                    TargetFood = target, InitialBody = (int[])body.Clone(), Obstacles = walls };
                ValidateGeometry(rules[i]); names[i] = name; previousTick = tick; previousTarget = target;
                if (boundary == "wrap") wrapCount++;
            }
            Require(wrapCount >= 3 && wrapCount <= 4, "Expected three or four wrap levels.");
            return new SnakeCatalog(rules, names);
        }
        private static int[] Coordinates(JToken token)
        {
            var array = SnakeJson.Array(token); Require(array.Count <= 480, "Too many coordinates.");
            var cells = new int[array.Count]; var used = new HashSet<int>();
            for (int i = 0; i < cells.Length; i++)
            {
                var xy = SnakeJson.Array(array[i], 2); int x = SnakeJson.Int(xy[0]), y = SnakeJson.Int(xy[1]);
                Require(x >= 0 && x < 20 && y >= 0 && y < 24, "Coordinate outside board.");
                cells[i] = y * 20 + x; Require(used.Add(cells[i]), "Duplicate coordinate.");
            }
            return cells;
        }
        private static void ValidateGeometry(SnakeRules rules)
        {
            var blocked = new HashSet<int>(rules.Obstacles);
            foreach (int cell in rules.InitialBody) Require(!blocked.Contains(cell), "Obstacle overlaps initial snake.");
            Require(rules.TargetFood < 480 - blocked.Count - 4, "Target consumes growth capacity.");
            for (int step = 1; step <= 3; step++) Require(!blocked.Contains(406 + step), "Initial rightward movement is blocked.");
            var graph = new List<int>[480]; int free = 0;
            for (int cell = 0; cell < graph.Length; cell++)
            {
                if (blocked.Contains(cell)) continue;
                var neighbors = new List<int>();
                foreach (int next in Neighbors(cell, rules.Boundary)) if (!blocked.Contains(next)) neighbors.Add(next);
                Require(neighbors.Count >= 2, "Static dead end in level."); graph[cell] = neighbors; free++;
            }
            var visited = new HashSet<int> { 406 }; var queue = new Queue<int>(); queue.Enqueue(406);
            while (queue.Count > 0) foreach (int next in graph[queue.Dequeue()]) if (visited.Add(next)) queue.Enqueue(next);
            Require(visited.Count == free, "Disconnected empty cells.");
            var covered = new HashSet<int>();
            for (int y = 0; y < 24; y++) for (int x = 0; x < 20; x++)
            {
                if (rules.Boundary == SnakeBoundary.Solid && (x == 19 || y == 23)) continue;
                int[] square = { y * 20 + x, y * 20 + (x + 1) % 20, ((y + 1) % 24) * 20 + x, ((y + 1) % 24) * 20 + (x + 1) % 20 };
                bool open = true; foreach (int cell in square) if (blocked.Contains(cell)) open = false;
                if (open) foreach (int cell in square) covered.Add(cell);
            }
            Require(covered.Count == free, "Cell outside an open two by two area.");
            int[] discovered = new int[480], low = new int[480]; int clock = 0;
            CheckBottlenecks(406, -1, graph, discovered, low, ref clock);
        }
        private static void CheckBottlenecks(int cell, int parent, List<int>[] graph, int[] discovered, int[] low, ref int clock)
        {
            discovered[cell] = low[cell] = ++clock; int children = 0;
            foreach (int next in graph[cell])
            {
                if (discovered[next] == 0)
                {
                    children++; CheckBottlenecks(next, cell, graph, discovered, low, ref clock); low[cell] = Math.Min(low[cell], low[next]);
                    Require(parent == -1 || low[next] < discovered[cell], "One-cell static bottleneck.");
                }
                else if (next != parent) low[cell] = Math.Min(low[cell], discovered[next]);
            }
            Require(parent != -1 || children <= 1, "One-cell static bottleneck.");
        }
        private static IEnumerable<int> Neighbors(int cell, SnakeBoundary boundary)
        {
            int x = cell % 20, y = cell / 20; int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i], ny = y + dy[i];
                if (boundary == SnakeBoundary.Wrap) { nx = (nx + 20) % 20; ny = (ny + 24) % 24; }
                if (nx >= 0 && nx < 20 && ny >= 0 && ny < 24) yield return ny * 20 + nx;
            }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new FormatException(message); }
    }
}
