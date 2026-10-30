using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using XanaduYonder.Core.Idiom;

namespace XanaduYonder.Runtime.Idiom
{
    // The game index loads only when this game is opened. Definitions stay in small
    // independent bundles, with a bounded four-chunk cache for browsing the notebook.
    public sealed class IdiomCatalogLoader
    {
        private IdiomLexicon lexicon;
        private readonly Dictionary<string, Dictionary<string, IdiomDefinition>> cache =
            new Dictionary<string, Dictionary<string, IdiomDefinition>>(StringComparer.Ordinal);
        private readonly LinkedList<string> recent = new LinkedList<string>();

        public IEnumerator Load(Action<IdiomLexicon> success, Action<string> failure)
        {
            if (lexicon != null) { success(lexicon); yield break; }
            var request = Resources.LoadAsync<TextAsset>("Idioms/catalog");
            yield return request;
            var asset = request.asset as TextAsset;
            if (asset == null) { failure("离线词库加载失败，请重试。"); yield break; }
            JsonTextReader reader = null;
            string error = null;
            var entries = new List<IdiomEntry>(44000);
            string version = null;
            try
            {
                try
                {
                    reader = Open(asset.bytes, 24 * 1024 * 1024);
                    Require(reader.Read() && reader.TokenType == JsonToken.StartObject);
                    Require(reader.Read() && (string)reader.Value == "version");
                    Require(reader.Read() && reader.TokenType == JsonToken.String);
                    version = (string)reader.Value;
                    Require(!string.IsNullOrEmpty(version));
                    Require(reader.Read() && (string)reader.Value == "entries");
                    Require(reader.Read() && reader.TokenType == JsonToken.StartArray);
                }
                catch (Exception exception) { error = exception.Message; }
                while (error == null)
                {
                    bool ended = false;
                    try
                    {
                        Require(reader.Read());
                        if (reader.TokenType == JsonToken.EndArray) ended = true;
                        else
                        {
                            Require(reader.TokenType == JsonToken.StartArray);
                            var row = JArray.Load(reader);
                            entries.Add(IdiomResourceJson.ReadCatalogEntry(row));
                        }
                    }
                    catch (Exception exception) { error = exception.Message; }
                    if (ended || error != null) break;
                    if (entries.Count % 400 == 0) yield return null;
                }
                if (error == null)
                {
                    try
                    {
                        Require(reader.Read() && reader.TokenType == JsonToken.EndObject && !reader.Read());
                        Require(entries.Count > 0);
                        lexicon = new IdiomLexicon(entries, version);
                    }
                    catch (Exception exception) { error = exception.Message; }
                }
            }
            finally
            {
                if (reader != null) reader.Close();
                Resources.UnloadAsset(asset);
            }
            if (error != null)
            {
                Debug.LogWarning("Idiom catalog: " + error);
                failure("离线词库暂时无法读取，请重试。");
            }
            else success(lexicon);
        }

        public bool TryGetDefinition(string id, out IdiomDefinition definition, out string error)
        {
            definition = null;
            error = null;
            IdiomEntry entry;
            if (lexicon == null || !lexicon.TryGetById(id, out entry))
            { error = "这个词条暂时无法打开。"; return false; }
            try
            {
                Dictionary<string, IdiomDefinition> chunk;
                if (!cache.TryGetValue(entry.DetailChunk, out chunk))
                {
                    var asset = Resources.Load<TextAsset>("Idioms/details/" + entry.DetailChunk);
                    if (asset == null) throw new InvalidDataException("Missing detail chunk");
                    try
                    {
                        using (var reader = Open(asset.bytes, 16 * 1024 * 1024))
                        {
                            var rows = JArray.Load(reader, new JsonLoadSettings
                            { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                            Require(!reader.Read());
                            chunk = IdiomResourceJson.ReadDefinitions(rows, lexicon, entry.DetailChunk);
                        }
                    }
                    finally { Resources.UnloadAsset(asset); }
                    Require(chunk.ContainsKey(id));
                    if (cache.Count >= 4)
                    { cache.Remove(recent.Last.Value); recent.RemoveLast(); }
                    cache.Add(entry.DetailChunk, chunk);
                }
                recent.Remove(entry.DetailChunk);
                recent.AddFirst(entry.DetailChunk);
                Require(chunk.TryGetValue(id, out definition));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Idiom detail: " + exception.Message);
                error = "释义暂时无法读取，请稍后重试。";
                return false;
            }
        }

        public void ClearDetails() { cache.Clear(); recent.Clear(); }

        private static void Require(bool condition)
        { if (!condition) throw new InvalidDataException("Invalid offline dictionary format"); }

        private static JsonTextReader Open(byte[] bytes, int maxBytes)
        {
            using (var input = new MemoryStream(bytes, false))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192];
                int read;
                while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + read > maxBytes) throw new InvalidDataException("Dictionary chunk too large");
                    output.Write(buffer, 0, read);
                }
                var text = new UTF8Encoding(false, true).GetString(output.ToArray());
                return new JsonTextReader(new StringReader(text)) { MaxDepth = 32, DateParseHandling = DateParseHandling.None };
            }
        }
    }
}
