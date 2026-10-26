using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace XanaduYonder.Runtime.Idiom
{
    public static class IdiomNotebookCodec
    {
        public const int MaximumBytes = 32 * 1024 * 1024;
        public const int MaximumEntries = 50000;
        public const int MaximumMeaningLength = 65536;

        public static string Encode(IdiomNotebookData data)
        {
            if (data == null || data.Entries == null) throw new ArgumentException("生词本记录不完整，暂未保存。");
            if (data.Entries.Count > MaximumEntries) throw new IdiomLimitException();
            var items = new JArray();
            foreach (var item in data.Entries)
            {
                if (item == null) throw new ArgumentException("生词本记录不完整，暂未保存。");
                items.Add(new JObject { ["id"] = item.Id, ["senseId"] = item.SenseId, ["word"] = item.Word,
                    ["pinyin"] = item.Pinyin, ["meaning"] = item.Meaning, ["addedAt"] = item.AddedAt,
                    ["dictionaryVersion"] = item.DictionaryVersion });
            }
            var root = new JObject { ["schemaVersion"] = 1, ["gameId"] = "idiom-notebook", ["entries"] = items };
            string json = IdiomStrictJson.Write(root, MaximumBytes);
            Decode(json);
            return json;
        }

        public static bool TryDecode(string json, out IdiomNotebookData data, out string error)
        {
            try { data = Decode(json); error = null; return true; }
            catch (Exception ex) when (IdiomStoreErrors.IsDataError(ex))
            { data = null; error = "生词本记录无法读取，原文件已保留。"; return false; }
        }

        internal static IdiomNotebookData Decode(string json)
        {
            var root = IdiomStrictJson.Read(json, MaximumBytes);
            IdiomStrictJson.Identity(root, "idiom-notebook");
            IdiomStrictJson.Object(root, "schemaVersion", "gameId", "entries");
            var entries = IdiomStrictJson.Array(root["entries"], MaximumEntries);
            var result = new IdiomNotebookData();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var words = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in entries)
            {
                var item = IdiomStrictJson.Object(token, "id", "senseId", "word", "pinyin", "meaning", "addedAt", "dictionaryVersion");
                var parsed = new IdiomNotebookItem {
                    Id = IdiomStrictJson.Text(item["id"], 128), SenseId = IdiomStrictJson.Text(item["senseId"], 128),
                    Word = IdiomStrictJson.Text(item["word"], 16), Pinyin = IdiomStrictJson.Text(item["pinyin"], 128),
                    Meaning = IdiomStrictJson.Text(item["meaning"], MaximumMeaningLength),
                    DictionaryVersion = IdiomStrictJson.Text(item["dictionaryVersion"], 128),
                    AddedAt = IdiomStrictJson.Integer(item["addedAt"], 0, 253402300799L) };
                if (!ids.Add(parsed.Id) || !words.Add(parsed.Word)) throw new FormatException();
                result.Entries.Add(parsed);
            }
            return result;
        }
    }
}
