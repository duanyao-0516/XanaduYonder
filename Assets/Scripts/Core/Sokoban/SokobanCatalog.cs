using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace XanaduYonder.Core.Sokoban
{
    public sealed class SokobanCatalog
    {
        private readonly SokobanLevel[] levels;
        private readonly Dictionary<string, SokobanLevel> byId = new Dictionary<string, SokobanLevel>(StringComparer.Ordinal);
        public int Count { get { return levels.Length; } }
        public string ContentVersion { get; private set; }
        private SokobanCatalog(string version, List<SokobanLevel> source)
        { ContentVersion = version; levels = source.ToArray(); foreach (var level in levels) byId.Add(level.LevelId, level); }
        public SokobanLevel At(int index) { return levels[index]; }
        public SokobanLevel Get(string id) { if (!TryGet(id, out var level)) throw new ArgumentException("Unknown level ID.", nameof(id)); return level; }
        public bool TryGet(string id, out SokobanLevel level) { level = null; return id != null && byId.TryGetValue(id, out level); }
        public static SokobanCatalog Parse(string json)
        {
            var root = SokobanJson.Parse(json); SokobanJson.Fields(root, "schemaVersion", "contentVersion", "levels");
            if (SokobanJson.Integer(root["schemaVersion"]) != 1) throw new FormatException("Unknown catalog schema.");
            string version = SokobanJson.Nonempty(root["contentVersion"]); var input = SokobanJson.Array(root["levels"]);
            if (input.Count == 0 || input.Count > 1000) throw new FormatException("Invalid catalog size.");
            var result = new List<SokobanLevel>(); var ids = new HashSet<string>(StringComparer.Ordinal); var sources = new HashSet<string>(StringComparer.Ordinal); var maps = new HashSet<string>(StringComparer.Ordinal); var orders = new HashSet<int>();
            foreach (var token in input)
            {
                var item = SokobanJson.Object(token); SokobanJson.Fields(item, "schemaVersion", "levelId", "contentVersion", "chapter", "order", "title", "source", "rows", "solution");
                if (SokobanJson.Integer(item["schemaVersion"]) != 1 || SokobanJson.Text(item["contentVersion"]) != version) throw new FormatException("Level version disagrees with catalog.");
                var source = SokobanJson.Object(item["source"]); SokobanJson.Fields(source, "collection", "sourceId");
                var array = SokobanJson.Array(item["rows"]); var rows = new string[array.Count]; for (int i = 0; i < rows.Length; i++) rows[i] = SokobanJson.Text(array[i]);
                var level = new SokobanLevel(SokobanJson.Nonempty(item["levelId"]), version, SokobanJson.Nonempty(item["title"]), SokobanJson.Nonempty(item["chapter"]), SokobanJson.Integer(item["order"]), rows,
                    SokobanJson.Nonempty(item["solution"]), SokobanJson.Nonempty(source["collection"]), SokobanJson.Nonempty(source["sourceId"]));
                if (!ids.Add(level.LevelId) || !sources.Add(level.SourceCollection + "\n" + level.SourceId) || !maps.Add(string.Join("\n", rows)) || !orders.Add(level.Order)) throw new FormatException("Duplicate level ID, source, map or order.");
                if (level.SourceCollection.IndexOf("boxoban", StringComparison.OrdinalIgnoreCase) >= 0 && (level.Width != 10 || level.Height != 10)) throw new FormatException("Published Boxoban maps must be 10 by 10.");
                result.Add(level);
            }
            result.Sort((a, b) => a.Order.CompareTo(b.Order)); return new SokobanCatalog(version, result);
        }
    }
}
