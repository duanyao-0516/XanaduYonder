using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Idiom;
using XanaduYonder.Runtime.Idiom;

internal static class Program
{
    private static int assertions;
    private static int Main()
    {
        try
        {
            SourceSnapshotPreservesVerbatimRecords();
            LongStoriesRemainWholeWhenExpanded();
            DisplaySegmentsKeepFullOriginalText();
            LegacyDefinitionsRemainReadable();
            SourceSchemaRejectsMalformedRecords();
            IncompleteChunksAreRejected();
            CatalogReadingsPreserveValidAlternatives();
            MalformedChunksNeverEnterTheCache();
            Console.WriteLine("IDIOM_PRESENTATION_TESTS_PASS: " + assertions + " assertions");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("IDIOM_PRESENTATION_TESTS_FAIL after " + assertions + " assertions: " + exception);
            return 1;
        }
    }

    private static void SourceSnapshotPreservesVerbatimRecords()
    {
        var definition = Fixture().ToObject<IdiomDefinition>();
        var actual = Format(definition, true);
        Equal("词库：资料库甲\n原拼音\n cháng  lè \n\n释义\n 甲释义。 \n\n典故\n甲故事。\n\n出处\n甲出处原文。\n—— 甲书\n\n例句\n甲例句。\n\n\n词库：资料库乙\n原拼音\nzhǎng lè\n\n释义\n乙释义（保留分歧）。\n\n典故\n暂无\n\n出处\n暂无\n\n例句\n暂无", actual,
            "Each source has its own exact original pinyin, meaning, story and citations");
        Require(!actual.Contains("根字段不能替代来源"), "Root summary cannot replace original source text");
        Equal("拼音见各词库资料", CallFormatter("PinyinCaption", definition), "Multiple source pinyin fits the header");
        Equal(false, CallFormatter("HasLongStories", definition), "Short independent stories do not need expansion");
    }

    private static void LongStoriesRemainWholeWhenExpanded()
    {
        var value = Fixture();
        var story = new string('故', 479) + "𠀀尾\n  原空白  ";
        ((JArray)value["Sources"])[0]["Stories"] = new JArray(story, "第二则原文。");
        var definition = value.ToObject<IdiomDefinition>();
        var collapsed = Format(definition, false);
        var expanded = Format(definition, true);
        Require(collapsed.Contains(new string('故', 479) + "…"), "Collapse does not split a UTF-16 surrogate pair");
        Require(!collapsed.Contains("第二则原文。"), "Long story starts collapsed");
        Require(collapsed.Contains("乙释义（保留分歧）。"), "Collapse keeps later sources visible");
        Require(expanded.Contains(story + "\n\n第二则原文。"), "Expanded text preserves every original character and story");
        Equal(true, CallFormatter("HasLongStories", definition), "Long source stories enable expansion");
        var saved = JsonConvert.DeserializeObject<string>(JsonConvert.SerializeObject(expanded));
        Equal(expanded, saved, "Full notebook text survives JSON snapshot serialization");
        value["Sources"] = new JArray(((JArray)value["Sources"])[0]);
        ((JArray)value["Sources"])[0]["Pinyin"] = new string('p', 180);
        Equal("拼音见各词库资料", CallFormatter("PinyinCaption", value.ToObject<IdiomDefinition>()), "Even a single long source pinyin belongs in the scroll body");
    }

    private static void DisplaySegmentsKeepFullOriginalText()
    {
        var original = new string('字', 2047) + "𠀀" + new string('长', 14000) + "\n  末行  ";
        var segments = (string[])CallFormatter("DisplaySegments", original);
        Require(segments.Length > 1, "Long full text uses bounded UGUI Text segments");
        Equal(original, string.Concat(segments), "Segmentation preserves every original character in order");
        foreach (var segment in segments)
        {
            Require(segment.Length <= 2048, "Individual text mesh stays below UGUI vertex limit");
            Require(!char.IsHighSurrogate(segment[segment.Length - 1]) && !char.IsLowSurrogate(segment[0]), "Segmentation keeps surrogate pairs together");
        }
    }

    private static void MalformedChunksNeverEnterTheCache()
    {
        var catalogRow = new JArray("id-1", "长乐未央", "cháng lè", "chang", "yang", "sense-1", "chunk", true,
            new JArray { new JArray("chang", "yang") });
        UnityEngine.Resources.Assets.Clear();
        UnityEngine.Resources.Assets["Idioms/catalog"] = Compressed(new JObject { ["version"] = "fixture", ["entries"] = new JArray { catalogRow } });
        var loader = new IdiomCatalogLoader();
        IdiomLexicon loaded = null;
        string error = null;
        var loading = loader.Load(value => loaded = value, value => error = value);
        while (loading.MoveNext()) { }
        Require(loaded != null && error == null, "Real catalog iterator reads strict nine-column resource");
        var invalid = Fixture(); Source(invalid)["Meaning"] = 123;
        UnityEngine.Resources.Assets["Idioms/details/chunk"] = Compressed(new JArray(invalid));
        IdiomDefinition definition;
        Require(!loader.TryGetDefinition("id-1", out definition, out error), "Invalid source rejects the whole detail chunk");
        var cache = (System.Collections.IDictionary)typeof(IdiomCatalogLoader).GetField("cache", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(loader);
        Equal(0, cache.Count, "Invalid chunk does not poison cache");
        UnityEngine.Resources.Assets["Idioms/details/chunk"] = Compressed(new JArray(Fixture()));
        Require(loader.TryGetDefinition("id-1", out definition, out error), "A fixed resource can be retried after malformed read");
        Equal(Format(Fixture().ToObject<IdiomDefinition>(), true), Format(definition, true), "Loaded definition keeps full original text");
        Equal(1, cache.Count, "Complete valid chunk is cached");
        loader.ClearDetails();
        Equal(0, cache.Count, "ClearDetails releases all cached source records");
        var duplicateProperty = new JArray(Fixture()).ToString(Formatting.None).Replace("\"SourceKey\":\"a\"", "\"SourceKey\":\"a\",\"SourceKey\":\"b\"");
        UnityEngine.Resources.Assets["Idioms/details/chunk"] = CompressedText(duplicateProperty);
        Require(!loader.TryGetDefinition("id-1", out definition, out error) && cache.Count == 0, "Duplicate JSON fields reject before coercion or caching");
    }

    private static UnityEngine.TextAsset Compressed(JToken value) { return CompressedText(value.ToString(Formatting.None)); }
    private static UnityEngine.TextAsset CompressedText(string value)
    {
        using (var output = new MemoryStream())
        {
            using (var stream = new GZipStream(output, CompressionMode.Compress, true))
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                stream.Write(bytes, 0, bytes.Length);
            }
            return new UnityEngine.TextAsset(output.ToArray());
        }
    }

    private static void LegacyDefinitionsRemainReadable()
    {
        var value = Fixture();
        value.Remove("Sources");
        value["Meaning"] = "旧夹具释义。";
        value["Stories"] = new JArray("旧故事。");
        value["Origins"] = new JArray(Citation("旧出处。", "旧书"));
        value["Examples"] = new JArray(Citation("旧例句。", ""));
        var definition = value.ToObject<IdiomDefinition>();
        var actual = Format(definition, true);
        Require(actual.Contains("旧夹具释义。") && actual.Contains("旧故事。") && actual.Contains("旧出处。") && actual.Contains("旧例句。"), "Legacy fixtures keep every field");
        Equal("cháng lè", CallFormatter("PinyinCaption", definition), "Short legacy pinyin remains visible");
    }

    private static void SourceSchemaRejectsMalformedRecords()
    {
        var valid = Fixture();
        var parsed = ParseDefinitions(new JArray(valid));
        Equal(1, parsed.Count, "Strict parser accepts complete source records");
        Equal(Format(valid.ToObject<IdiomDefinition>(), true), Format(parsed["id-1"], true), "Production parser keeps source strings verbatim");
        foreach (var field in new[] { "SourceKey", "SourceLabel", "SourceRecordId", "Pinyin", "Meaning", "Stories", "Origins", "Examples" })
        {
            var missing = Fixture(); Source(missing).Remove(field); Reject(missing, "Missing source field " + field);
            var nil = Fixture(); Source(nil)[field] = JValue.CreateNull(); Reject(nil, "Null source field " + field);
            var wrongType = Fixture(); Source(wrongType)[field] = 42; Reject(wrongType, "Wrong source type " + field);
        }
        var missingSources = Fixture(); missingSources.Remove("Sources"); Reject(missingSources, "New resource schema requires source records");
        var mixedRoot = Fixture(); mixedRoot["Stories"] = new JArray("unowned root story"); Reject(mixedRoot, "New source schema cannot hide root story data");
        var absentContent = Fixture(); Source(absentContent)["Pinyin"] = ""; Source(absentContent)["Meaning"] = "";
        var absentText = Format(ParseDefinitions(new JArray(absentContent))["id-1"], true);
        Require(absentText.Contains("原拼音\n暂无\n\n释义\n暂无"), "Missing source content is explicit, never inferred");
        var duplicate = Fixture(); ((JArray)duplicate["Sources"]).Add(Source(duplicate).DeepClone()); Reject(duplicate, "Duplicate source record identity");
        var emptySources = Fixture(); emptySources["Sources"] = new JArray(); Reject(emptySources, "Present sources cannot silently fall back when empty");
        var nullSources = Fixture(); nullSources["Sources"] = JValue.CreateNull(); Reject(nullSources, "Null sources cannot silently fall back");
        var badStory = Fixture(); Source(badStory)["Stories"] = new JArray(JValue.CreateNull()); Reject(badStory, "Null story element");
        var badOrigin = Fixture(); Source(badOrigin)["Origins"] = new JArray(Citation("text", "ref")); Source(badOrigin)["Origins"][0]["Text"] = 17; Reject(badOrigin, "Wrong citation text type");
        var missingReference = Fixture(); Source(missingReference)["Origins"] = new JArray(new JObject { ["Text"] = "text" }); Reject(missingReference, "Missing citation reference");
        var sameIdOtherSource = Fixture(); Source(sameIdOtherSource)["SourceRecordId"] = "record-b"; ParseDefinitions(new JArray(sameIdOtherSource));
        assertions++; // Composite identity is (SourceKey, SourceRecordId), not record ID alone.
    }

    private static void IncompleteChunksAreRejected()
    {
        var first = new IdiomEntry("id-1", "长乐未央", "cháng lè", "chang", "yang", "sense-1", "chunk", true);
        var missing = new IdiomEntry("id-2", "一心一意", "yī", "yi", "yi", "sense-2", "chunk", true);
        try { Call("IdiomResourceJson", "ReadDefinitions", new JArray(Fixture()), new IdiomLexicon(new[] { first, missing }, "fixture"), "chunk"); }
        catch (InvalidDataException) { assertions++; return; }
        throw new Exception("An incomplete chunk was accepted despite missing catalog member");
    }

    private static void CatalogReadingsPreserveValidAlternatives()
    {
        var row = new JArray("id-1", "长乐未央", "cháng lè", "chang", "yang", "sense-1", "chunk", true,
            new JArray(new JArray("chang", "yang"), new JArray("zhang", "yang")));
        var entry = (IdiomEntry)Call("IdiomResourceJson", "ReadCatalogEntry", row);
        Equal("cháng lè", entry.Pinyin, "Display pinyin is not rewritten to matching keys");
        var readings = entry.GetType().GetProperty("SoundReadings");
        Require(readings != null, "Catalog entry exposes exact source reading pairs");
        Equal(2, ((System.Collections.ICollection)readings.GetValue(entry)).Count, "Both source reading alternatives reach Core");
        var noReading = (JArray)row.DeepClone(); noReading[3] = ""; noReading[4] = ""; noReading[8] = new JArray();
        Call("IdiomResourceJson", "ReadCatalogEntry", noReading); assertions++;
        foreach (var broken in new[] { new JArray("chang"), new JArray("", "yang"), new JArray("chang", 1) })
        {
            var invalid = (JArray)row.DeepClone(); invalid[8] = new JArray(broken);
            RejectCatalog(invalid);
        }
        var staleKeys = (JArray)noReading.DeepClone(); staleKeys[3] = "chang"; staleKeys[4] = "yang"; RejectCatalog(staleKeys);
    }

    private static JObject Fixture()
    {
        return new JObject
        {
            ["Id"] = "id-1", ["SenseId"] = "sense-1", ["Word"] = "长乐未央", ["Pinyin"] = "cháng lè",
            ["Meaning"] = "根字段不能替代来源", ["Stories"] = new JArray(), ["Origins"] = new JArray(), ["Examples"] = new JArray(), ["SourceLabel"] = "全部词库",
            ["Sources"] = new JArray(
                new JObject { ["SourceKey"] = "a", ["SourceLabel"] = "资料库甲", ["SourceRecordId"] = "record-a", ["Pinyin"] = " cháng  lè ", ["Meaning"] = " 甲释义。 ", ["Stories"] = new JArray("甲故事。"), ["Origins"] = new JArray(Citation("甲出处原文。", "甲书")), ["Examples"] = new JArray(Citation("甲例句。", "")) },
                new JObject { ["SourceKey"] = "b", ["SourceLabel"] = "资料库乙", ["SourceRecordId"] = "record-b", ["Pinyin"] = "zhǎng lè", ["Meaning"] = "乙释义（保留分歧）。", ["Stories"] = new JArray(), ["Origins"] = new JArray(), ["Examples"] = new JArray() })
        };
    }

    private static JObject Citation(string text, string reference) { return new JObject { ["Text"] = text, ["Reference"] = reference }; }
    private static JObject Source(JObject value) { return (JObject)value["Sources"][0]; }
    private static string Format(IdiomDefinition value, bool expanded) { return (string)CallFormatter("Format", value, expanded); }
    private static object CallFormatter(string method, params object[] values) { return Call("IdiomDefinitionText", method, values); }
    private static object Call(string typeName, string methodName, params object[] values)
    {
        var type = typeof(IdiomDefinition).Assembly.GetType("XanaduYonder.Runtime.Idiom." + typeName);
        Require(type != null, "Missing production behavior: " + typeName);
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Require(method != null, "Missing production behavior: " + typeName + "." + methodName);
        try { return method.Invoke(null, values); }
        catch (TargetInvocationException exception) { throw exception.InnerException; }
    }
    private static Dictionary<string, IdiomDefinition> ParseDefinitions(JArray rows)
    {
        var entry = new IdiomEntry("id-1", "长乐未央", "cháng lè", "chang", "yang", "sense-1", "chunk", true);
        return (Dictionary<string, IdiomDefinition>)Call("IdiomResourceJson", "ReadDefinitions", rows, new IdiomLexicon(new[] { entry }, "fixture"), "chunk");
    }
    private static void Reject(JObject value, string label)
    {
        try { ParseDefinitions(new JArray(value)); }
        catch (InvalidDataException) { assertions++; return; }
        throw new Exception(label + " was accepted");
    }
    private static void RejectCatalog(JArray row)
    {
        try { Call("IdiomResourceJson", "ReadCatalogEntry", row); }
        catch (InvalidDataException) { assertions++; return; }
        throw new Exception("Malformed catalog reading was accepted");
    }
    private static void Require(bool condition, string label) { assertions++; if (!condition) throw new Exception(label); }
    private static void Equal(object expected, object actual, string label) { Require(Equals(expected, actual), label + "\nExpected: " + expected + "\nActual: " + actual); }
}
