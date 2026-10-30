using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Idiom;

namespace XanaduYonder.Runtime.Idiom
{
    // Validate JSON token types before DTO conversion: Newtonsoft otherwise
    // coerces numbers to strings and cannot distinguish missing/null arrays.
    public static class IdiomResourceJson
    {
        public static IdiomEntry ReadCatalogEntry(JArray row)
        {
            Require(row != null && row.Count == 9 && row[7].Type == JTokenType.Boolean && row[8] is JArray);
            for (var i = 0; i < 7; i++) Require(row[i].Type == JTokenType.String);
            var readings = new List<IdiomSoundReading>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in (JArray)row[8])
            {
                var pair = token as JArray;
                Require(pair != null && pair.Count == 2 && pair[0].Type == JTokenType.String && pair[1].Type == JTokenType.String);
                var first = (string)pair[0]; var last = (string)pair[1];
                Require(IsSoundKey(first) && IsSoundKey(last) && seen.Add(first + ":" + last));
                readings.Add(new IdiomSoundReading(first, last));
            }
            if (readings.Count == 0) Require((string)row[3] == "" && (string)row[4] == "");
            else Require((string)row[3] == (string)row[8][0][0] && (string)row[4] == (string)row[8][0][1]);
            return new IdiomEntry((string)row[0], (string)row[1], (string)row[2], (string)row[3],
                (string)row[4], (string)row[5], (string)row[6], (bool)row[7], readings);
        }

        public static Dictionary<string, IdiomDefinition> ReadDefinitions(JArray rows, IdiomLexicon lexicon, string chunkName)
        {
            Require(rows != null && rows.Count > 0 && rows.Count <= 256);
            var result = new Dictionary<string, IdiomDefinition>(StringComparer.Ordinal);
            foreach (var token in rows)
            {
                var row = token as JObject;
                Require(row != null);
                var id = String(row, "Id", true);
                String(row, "SenseId", true); String(row, "Word", true); String(row, "Pinyin"); String(row, "SourceLabel", true);
                ValidateContents(row);
                var sources = row["Sources"] as JArray;
                Require(sources != null && sources.Count > 0);
                Require(((JArray)row["Stories"]).Count == 0 && ((JArray)row["Origins"]).Count == 0 && ((JArray)row["Examples"]).Count == 0);
                var identities = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
                foreach (var sourceToken in sources)
                {
                    var source = sourceToken as JObject;
                    Require(source != null);
                    var key = String(source, "SourceKey", true);
                    var recordId = String(source, "SourceRecordId", true);
                    String(source, "SourceLabel", true); String(source, "Pinyin");
                    ValidateContents(source);
                    HashSet<string> records;
                    if (!identities.TryGetValue(key, out records)) identities.Add(key, records = new HashSet<string>(StringComparer.Ordinal));
                    Require(records.Add(recordId));
                }
                IdiomEntry expected;
                Require(lexicon != null && lexicon.TryGetById(id, out expected));
                lexicon.TryGetById(id, out expected);
                Require(expected.DetailChunk == chunkName && expected.SenseId == (string)row["SenseId"] &&
                    expected.Word == (string)row["Word"] && expected.Pinyin == (string)row["Pinyin"] && !result.ContainsKey(id));
                result.Add(id, row.ToObject<IdiomDefinition>());
            }
            foreach (var entry in lexicon.Entries)
                if (entry.DetailChunk == chunkName) Require(result.ContainsKey(entry.Id));
            return result;
        }

        private static void ValidateContents(JObject value)
        {
            String(value, "Meaning");
            var stories = value["Stories"] as JArray;
            Require(stories != null);
            foreach (var story in stories) Require(story.Type == JTokenType.String);
            ValidateCitations(value["Origins"]);
            ValidateCitations(value["Examples"]);
        }

        private static void ValidateCitations(JToken token)
        {
            var values = token as JArray;
            Require(values != null);
            foreach (var value in values)
            {
                var citation = value as JObject;
                Require(citation != null);
                String(citation, "Text"); String(citation, "Reference");
            }
        }

        private static string String(JObject value, string key, bool nonempty = false)
        {
            var token = value[key];
            Require(token != null && token.Type == JTokenType.String);
            var result = (string)token;
            Require(!nonempty || !string.IsNullOrEmpty(result));
            return result;
        }

        private static bool IsSoundKey(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (var c in value) if (c < 'a' || c > 'z') return false;
            return true;
        }

        private static void Require(bool condition)
        { if (!condition) throw new InvalidDataException("Invalid offline dictionary format"); }
    }
}
