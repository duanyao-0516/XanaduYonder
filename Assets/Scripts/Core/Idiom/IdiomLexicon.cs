using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Idiom
{
    public sealed class IdiomSoundReading
    {
        public string FirstKey { get; private set; }
        public string LastKey { get; private set; }
        public IdiomSoundReading(string firstKey, string lastKey)
        { FirstKey = Key(firstKey); LastKey = Key(lastKey); }
        private static string Key(string value)
        {
            if (string.IsNullOrEmpty(value)) throw new ArgumentException("Sound key is required.");
            foreach (char c in value) if (c < 'a' || c > 'z') throw new ArgumentException("Expected normalized lowercase pinyin.");
            return value;
        }
    }

    public sealed class IdiomEntry
    {
        public string Id { get; private set; }
        public string Word { get; private set; }
        public string Pinyin { get; private set; }
        public string FirstKey { get; private set; }
        public string LastKey { get; private set; }
        public string SenseId { get; private set; }
        public string DetailChunk { get; private set; }
        public IReadOnlyList<IdiomSoundReading> SoundReadings { get; private set; }
        public bool Common { get; private set; }
        public string FirstCharacter { get; private set; }
        public string LastCharacter { get; private set; }

        public IdiomEntry(string id, string word, string pinyin, string firstKey, string lastKey,
                          string senseId, string detailChunk, bool common)
            : this(id, word, Required(pinyin, "pinyin"), firstKey, lastKey, senseId, detailChunk, common,
                   new[] { new IdiomSoundReading(firstKey, lastKey) }) { }

        public IdiomEntry(string id, string word, string pinyin, string firstKey, string lastKey,
                          string senseId, string detailChunk, bool common, IEnumerable<IdiomSoundReading> soundReadings)
        {
            if (soundReadings == null) throw new ArgumentNullException("soundReadings");
            var readings = new List<IdiomSoundReading>(); var pairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reading in soundReadings)
            {
                if (reading == null) throw new ArgumentException("Sound reading cannot be null.");
                if (pairs.Add(reading.FirstKey + ":" + reading.LastKey)) readings.Add(reading);
            }
            if (firstKey != (readings.Count == 0 ? "" : readings[0].FirstKey) ||
                lastKey != (readings.Count == 0 ? "" : readings[0].LastKey))
                throw new ArgumentException("Legacy keys must match the first paired reading, or be empty when unavailable.");
            SoundReadings = readings.AsReadOnly();
            string first, last;
            if (!IdiomCharacters.TryFour(word, out first, out last))
                throw new ArgumentException("An idiom must contain exactly four Han Unicode scalars.", "word");
            Id = Required(id, "id"); Word = word; Pinyin = pinyin ?? throw new ArgumentNullException("pinyin");
            FirstKey = firstKey; LastKey = lastKey;
            SenseId = Required(senseId, "senseId"); DetailChunk = Required(detailChunk, "detailChunk");
            Common = common; FirstCharacter = first; LastCharacter = last;
        }

        private static string Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
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
        private readonly List<IdiomEntry> soundEntries = new List<IdiomEntry>();
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
                var indexed = new HashSet<string>(StringComparer.Ordinal);
                foreach (var reading in entry.SoundReadings)
                    if (indexed.Add(reading.FirstKey)) AddIndex(bySound, reading.FirstKey, entry);
                if (entry.SoundReadings.Count > 0) soundEntries.Add(entry);
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
        { return Following(mode, last, last == null ? null : IdiomGame.TailKeys(last, null)); }

        internal IReadOnlyList<IdiomEntry> Following(IdiomMode mode, IdiomEntry last, IReadOnlyList<string> soundKeys)
        {
            if (last == null) return mode == IdiomMode.SameCharacter ? Entries : soundEntries.AsReadOnly();
            List<IdiomEntry> matches;
            if (mode == IdiomMode.SameCharacter)
                return byCharacter.TryGetValue(last.LastCharacter, out matches) ? (IReadOnlyList<IdiomEntry>)matches : Empty;
            if (soundKeys == null || soundKeys.Count == 0) return Empty;
            if (soundKeys.Count == 1)
                return bySound.TryGetValue(soundKeys[0], out matches) ? (IReadOnlyList<IdiomEntry>)matches : Empty;
            var result = new List<IdiomEntry>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in soundKeys)
                if (bySound.TryGetValue(key, out matches))
                    foreach (var entry in matches) if (ids.Add(entry.Id)) result.Add(entry);
            result.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            return result.AsReadOnly();
        }
        private static void AddIndex(Dictionary<string, List<IdiomEntry>> index, string key, IdiomEntry entry)
        {
            List<IdiomEntry> values;
            if (!index.TryGetValue(key, out values)) { values = new List<IdiomEntry>(); index.Add(key, values); }
            values.Add(entry);
        }
    }
}
