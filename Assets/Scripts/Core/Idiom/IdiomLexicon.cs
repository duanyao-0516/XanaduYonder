using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Idiom
{
    public sealed class IdiomEntry
    {
        public string Id { get; private set; }
        public string Word { get; private set; }
        public string Pinyin { get; private set; }
        public string FirstKey { get; private set; }
        public string LastKey { get; private set; }
        public string SenseId { get; private set; }
        public string DetailChunk { get; private set; }
        public bool Common { get; private set; }
        public string FirstCharacter { get; private set; }
        public string LastCharacter { get; private set; }

        public IdiomEntry(string id, string word, string pinyin, string firstKey, string lastKey,
                          string senseId, string detailChunk, bool common)
        {
            string first, last;
            if (!IdiomCharacters.TryFour(word, out first, out last))
                throw new ArgumentException("An idiom must contain exactly four Han Unicode scalars.", "word");
            Id = Required(id, "id"); Word = word; Pinyin = Required(pinyin, "pinyin");
            FirstKey = Key(firstKey, "firstKey"); LastKey = Key(lastKey, "lastKey");
            SenseId = Required(senseId, "senseId"); DetailChunk = Required(detailChunk, "detailChunk");
            Common = common; FirstCharacter = first; LastCharacter = last;
        }

        private static string Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
            return value;
        }
        private static string Key(string value, string name)
        {
            Required(value, name);
            foreach (char c in value)
                if (c < 'a' || c > 'z') throw new ArgumentException("Expected a normalized lowercase pinyin key.", name);
            return value;
        }
    }

    public sealed class IdiomLexicon
    {
        private readonly Dictionary<string, IdiomEntry> byId = new Dictionary<string, IdiomEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, IdiomEntry> byWord = new Dictionary<string, IdiomEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<IdiomEntry>> byCharacter = new Dictionary<string, List<IdiomEntry>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<IdiomEntry>> bySound = new Dictionary<string, List<IdiomEntry>>(StringComparer.Ordinal);
        private static readonly IReadOnlyList<IdiomEntry> Empty = new List<IdiomEntry>().AsReadOnly();
        public int Count { get { return Entries.Count; } }
        public string Version { get; private set; }
        public IReadOnlyList<IdiomEntry> Entries { get; private set; }

        public IdiomLexicon(IEnumerable<IdiomEntry> entries, string version)
        {
            if (entries == null) throw new ArgumentNullException("entries");
            if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("Dictionary version is required.", "version");
            var ordered = new List<IdiomEntry>(entries);
            foreach (var entry in ordered)
            {
                if (entry == null) throw new ArgumentException("Dictionary entries cannot be null.", "entries");
                if (byId.ContainsKey(entry.Id) || byWord.ContainsKey(entry.Word))
                    throw new ArgumentException("Dictionary ids and words must be unique.", "entries");
                byId.Add(entry.Id, entry); byWord.Add(entry.Word, entry);
            }
            if (ordered.Count == 0) throw new ArgumentException("Dictionary cannot be empty.", "entries");
            ordered.Sort(delegate(IdiomEntry a, IdiomEntry b) { return StringComparer.Ordinal.Compare(a.Id, b.Id); });
            foreach (var entry in ordered)
            {
                AddIndex(byCharacter, entry.FirstCharacter, entry);
                AddIndex(bySound, entry.FirstKey, entry);
            }
            Entries = ordered.AsReadOnly(); Version = version;
        }

        public bool TryGetById(string id, out IdiomEntry entry)
        {
            entry = null; return id != null && byId.TryGetValue(id, out entry);
        }
        public bool TryGetWord(string word, out IdiomEntry entry)
        {
            entry = null; return word != null && byWord.TryGetValue(word, out entry);
        }

        internal IReadOnlyList<IdiomEntry> Following(IdiomMode mode, IdiomEntry last)
        {
            if (last == null) return Entries;
            List<IdiomEntry> result;
            var index = mode == IdiomMode.SameCharacter ? byCharacter : bySound;
            string key = mode == IdiomMode.SameCharacter ? last.LastCharacter : last.LastKey;
            return index.TryGetValue(key, out result) ? (IReadOnlyList<IdiomEntry>)result : Empty;
        }
        private static void AddIndex(Dictionary<string, List<IdiomEntry>> index, string key, IdiomEntry entry)
        {
            List<IdiomEntry> values;
            if (!index.TryGetValue(key, out values)) { values = new List<IdiomEntry>(); index.Add(key, values); }
            values.Add(entry);
        }
    }
}
