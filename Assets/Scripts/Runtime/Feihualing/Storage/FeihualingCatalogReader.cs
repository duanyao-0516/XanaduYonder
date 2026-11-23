using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using XanaduYonder.Core.Feihualing;
using XanaduYonder.Runtime.Idiom;

namespace XanaduYonder.Runtime.Feihualing
{
    public static class FeihualingCatalogReader
    {
        public const string ResourcePath = "Feihualing/catalog";
        private const int MaximumCompressed = 12_000_000;
        private const int MaximumExpanded = 72_000_000;
        private const int MaximumWorks = 30_000;
        private const int MaximumLines = 450_000;
        private const int MaximumCards = 200;

        // Only decode and validate the release artifact. The computer-side importer
        // decides all sentence boundaries, aliases, candidate sets and themes.
        public static PoetryCatalog Read(byte[] data)
        {
            if (data == null || data.Length < 20 || data.Length > MaximumCompressed) throw new FormatException("Invalid poetry resource size.");
            using (var source = new MemoryStream(data, false))
            using (var gzip = new GZipStream(source, CompressionMode.Decompress))
            using (var checkedStream = new SizedReaderStream(gzip))
            using (var text = new StreamReader(checkedStream, IdiomStrictJson.Utf8, false, 8192))
            using (var reader = new JsonTextReader(text) { MaxDepth = 9, DateParseHandling = DateParseHandling.None })
            {
                ReadToken(reader, JsonToken.StartObject);
                var fields = new HashSet<string>(StringComparer.Ordinal);
                string version = null;
                PoetryWork[] works = null;
                PoetryLine[] lines = null;
                PoetryCard[] cards = null;
                int[] retiredLines = null;
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType != JsonToken.PropertyName) throw new FormatException("Poetry resource field expected.");
                    var field = (string)reader.Value;
                    if (!fields.Add(field)) throw new FormatException("Repeated poetry resource field.");
                    switch (field)
                    {
                        case "version":
                            ReadToken(reader, JsonToken.Integer);
                            if (!(reader.Value is long) || (long)reader.Value != 1) throw new FormatException("Unsupported poetry format.");
                            break;
                        case "corpus":
                            ReadToken(reader, JsonToken.String);
                            version = (string)reader.Value;
                            IdiomStrictJson.ValidateText(version, 96);
                            break;
                        case "works": works = Rows(reader, MaximumWorks, ReadWork); break;
                        case "lines": lines = Rows(reader, MaximumLines, ReadLine); break;
                        case "cards": cards = Rows(reader, MaximumCards, ReadCard); break;
                        case "retired":
                            ReadToken(reader, JsonToken.StartArray);
                            retiredLines = ReadCandidates(reader);
                            break;
                        default: throw new FormatException("Unexpected poetry resource field.");
                    }
                }
                if (reader.TokenType != JsonToken.EndObject || reader.Read() || fields.Count != (retiredLines == null ? 5 : 6))
                    throw new FormatException("Incomplete poetry resource.");
                int ruleMask = 0;
                bool hasOpeningCard = false;
                foreach (var card in cards)
                {
                    ruleMask |= 1 << (int)card.Rule;
                    hasOpeningCard |= card.Rule == PoetryRule.Character && card.Id == "char-moon" && card.Key == "月";
                }
                if (ruleMask != 15 || !hasOpeningCard) throw new FormatException("Poetry resource is missing an opening or rule.");
                return new PoetryCatalog(version, works, lines, cards, retiredLines);
            }
        }

        private static void ReadToken(JsonTextReader reader, JsonToken expected)
        {
            if (!reader.Read() || reader.TokenType != expected) throw new FormatException("Unexpected poetry resource token.");
        }

        private static T[] Rows<T>(JsonTextReader reader, int maximum, Func<JsonTextReader,T> read)
        {
            var rows = ReadArray(reader, maximum, read);
            if (rows.Length == 0) throw new FormatException("Empty poetry resource table.");
            return rows;
        }

        // Read each field directly. There is no need to allocate a JSON object tree for
        // hundreds of thousands of rows or render each numeric token back to a string.
        private static T[] ReadArray<T>(JsonTextReader reader, int maximum, Func<JsonTextReader,T> read)
        {
            ReadToken(reader, JsonToken.StartArray);
            List<T> values = null;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray) return values?.ToArray() ?? Array.Empty<T>();
                if (reader.TokenType != JsonToken.StartArray || (values != null && values.Count == maximum))
                    throw new FormatException("Invalid poetry resource array.");
                if (values == null) values = new List<T>();
                values.Add(read(reader));
            }
            throw new FormatException("Truncated poetry resource array.");
        }

        private static PoetryWork ReadWork(JsonTextReader reader)
        {
            var work = new PoetryWork(ReadText(reader, 96), ReadText(reader, 100), ReadText(reader, 300), ReadText(reader, 16000));
            ReadToken(reader, JsonToken.EndArray);
            return work;
        }

        private static PoetryLine ReadLine(JsonTextReader reader)
        {
            int work = ReadInteger(reader, MaximumWorks - 1);
            int start = ReadInteger(reader, 16000);
            int length = ReadInteger(reader, 100);
            int group = ReadInteger(reader, int.MaxValue);
            bool common = ReadInteger(reader, 1) == 1;
            var aliases = ReadAliases(reader);
            ReadToken(reader, JsonToken.EndArray);
            return new PoetryLine(work, start, length, group, common, aliases);
        }

        private static PoetryCard ReadCard(JsonTextReader reader)
        {
            string id = ReadText(reader, 64);
            var rule = (PoetryRule)ReadInteger(reader, 3);
            string title = ReadText(reader, 64);
            string key = ReadText(reader, 24, true);
            string secondKey = ReadText(reader, 24, true);
            int width = ReadInteger(reader, 7);
            var sets = ReadArray(reader, 7, ReadCandidates);
            var reasons = new Dictionary<int, string>();
            ReadToken(reader, JsonToken.StartArray);
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                if (reader.TokenType != JsonToken.StartArray || reasons.Count == MaximumLines)
                    throw new FormatException("Invalid theme reason.");
                int lineId = ReadInteger(reader, MaximumLines - 1);
                if (reasons.ContainsKey(lineId)) throw new FormatException("Repeated theme reason.");
                reasons.Add(lineId, ReadText(reader, 240));
                ReadToken(reader, JsonToken.EndArray);
            }
            if (reader.TokenType != JsonToken.EndArray) throw new FormatException("Truncated theme reasons.");
            ReadToken(reader, JsonToken.EndArray);
            return new PoetryCard(id, rule, title, key, secondKey, width, sets, reasons);
        }

        private static int[] ReadCandidates(JsonTextReader reader)
        {
            var ids = new List<int>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray) return ids.ToArray();
                if (reader.TokenType != JsonToken.Integer || ids.Count == MaximumLines)
                    throw new FormatException("Invalid poetry candidate.");
                ids.Add(Integer(reader, MaximumLines - 1));
            }
            throw new FormatException("Truncated poetry candidates.");
        }

        private static string ReadText(JsonTextReader reader, int maximum, bool allowEmpty = false)
        {
            ReadToken(reader, JsonToken.String);
            var text = (string)reader.Value;
            IdiomStrictJson.ValidateText(text, maximum, allowEmpty);
            return text;
        }

        private static string[] ReadAliases(JsonTextReader reader)
        {
            ReadToken(reader, JsonToken.StartArray);
            string first = null;
            List<string> aliases = null;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray)
                    return aliases?.ToArray() ?? (first == null ? Array.Empty<string>() : new[] { first });
                if (reader.TokenType != JsonToken.String || aliases?.Count == 12)
                    throw new FormatException("Invalid poetry alias.");
                var alias = (string)reader.Value;
                IdiomStrictJson.ValidateText(alias, 100);
                if (first == null) first = alias;
                else
                {
                    if (aliases == null) aliases = new List<string> { first };
                    aliases.Add(alias);
                }
            }
            throw new FormatException("Truncated poetry aliases.");
        }

        private static int ReadInteger(JsonTextReader reader, int maximum)
        {
            ReadToken(reader, JsonToken.Integer);
            return Integer(reader, maximum);
        }

        private static int Integer(JsonTextReader reader, int maximum)
        {
            if (!(reader.Value is long number) || number < 0 || number > maximum)
                throw new FormatException("Invalid poetry resource number.");
            return (int)number;
        }

        private sealed class SizedReaderStream : Stream
        {
            private readonly Stream source;
            private int count;
            public SizedReaderStream(Stream source) { this.source = source; }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => count; set => throw new NotSupportedException(); }
            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int length) => throw new NotSupportedException();
            public override int Read(byte[] buffer, int offset, int length)
            {
                int read = source.Read(buffer, offset, length);
                count += read;
                if (count > MaximumExpanded) throw new InvalidDataException("Poetry resource exceeded its expanded limit.");
                return read;
            }
        }
    }
}
