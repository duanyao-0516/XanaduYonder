using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using XanaduYonder.Runtime.Dino;

internal static class Program
{
    private static int passed;
    private static int failed;

    private static int Main()
    {
        Check("New data defaults to two zero records and portrait", Defaults);
        Check("Record helper updates only the selected orientation and rejects negative scores", Records);
        Check("Version 1 round trip preserves both 64-bit records as decimal strings", RoundTrip);
        Check("Known minimal payload decodes without serializer defaults", KnownPayload);
        Check("Every required field rejects missing and explicit null", RequiredFields);
        Check("Field token types, score ranges, and exact orientation are enforced", InvalidTokens);
        Check("Malformed documents, duplicate keys, and trailing content are rejected", InvalidDocuments);
        Check("Text, UTF-8 byte size, and JSON depth are bounded", PayloadLimits);
        Check("Invalid in-memory data cannot be encoded", InvalidModels);
        Check("Missing save returns a fresh default object without warning", MissingSave);
        Check("Fresh stores read primary and recover the previous valid backup", BackupRecovery);
        Check("Missing primary recovers from a valid backup", MissingPrimaryRecovery);
        Check("Replacing corrupt primary preserves the last valid backup", PreserveValidBackup);
        Check("Both invalid files yield defaults and a warning without partial records", BothInvalid);
        Check("Invalid UTF-8 and oversized disk files recover from backup", InvalidDiskFiles);
        Check("An unreadable path warns instead of appearing to be first launch", UnreadablePrimary);
        Check("Temporary write and atomic replacement failures preserve prior bytes", WriteFailures);
        Check("Invalid models cannot replace primary or backup", InvalidWritePreservesFiles);
        Check("Dino writes and recovery leave every other game file byte-identical", ExistingFilesUntouched);
        Check("Loads return independent data objects", LoadedDataIndependent);
        Console.WriteLine("Dino persistence checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void Defaults()
    {
        EqualState(new DinoSaveData { BestPortraitScore = 0, BestLandscapeScore = 0, PreferredOrientation = "portrait" }, new DinoSaveData());
    }

    private static void Records()
    {
        var data = new DinoSaveData();
        Assert(data.GetBest(false) == 0 && data.GetBest(true) == 0, "Default best records must be zero.");
        Assert(data.Record(false, 10), "First portrait record was not accepted.");
        Assert(!data.Record(false, 10) && !data.Record(false, 9), "Equal or lower score replaced the record.");
        Assert(data.Record(true, long.MaxValue), "64-bit landscape record was not accepted.");
        Assert(data.GetBest(false) == 10 && data.GetBest(true) == long.MaxValue, "Orientation records interfered.");
        Assert(data.PreferredOrientation == "portrait", "Record changed orientation preference.");
        ExpectArgumentException(() => data.Record(false, -1));
        ExpectArgumentException(() => data.Record(true, long.MinValue));
        Assert(data.GetBest(false) == 10 && data.GetBest(true) == long.MaxValue, "Rejected score mutated records.");
    }

    private static void RoundTrip()
    {
        var original = new DinoSaveData { BestPortraitScore = 9007199254740993L, BestLandscapeScore = long.MaxValue, PreferredOrientation = "landscape" };
        var json = DinoSaveCodec.Encode(original);
        var root = JObject.Parse(json);
        Assert(root.Count == 5, "Only the schema identity, two records, and orientation may be persisted.");
        Assert(root["schemaVersion"].Type == JTokenType.Integer && (int)root["schemaVersion"] == 1 && (string)root["gameId"] == "dino", "Wrong schema identity.");
        Assert(root["bestPortraitScore"].Type == JTokenType.String && root["bestLandscapeScore"].Type == JTokenType.String, "64-bit scores must be written as strings.");
        Assert((string)root["bestPortraitScore"] == "9007199254740993" && (string)root["bestLandscapeScore"] == "9223372036854775807", "Score text lost precision.");
        Assert(DinoSaveCodec.TryDecode(json, out var decoded, out var reason), reason);
        EqualState(original, decoded);
        Assert(!ReferenceEquals(original, decoded), "Decoded data aliases input.");
    }

    private static void KnownPayload()
    {
        const string json = "{\"schemaVersion\":1,\"gameId\":\"dino\",\"bestPortraitScore\":\"12\",\"bestLandscapeScore\":\"34\",\"preferredOrientation\":\"portrait\"}";
        Assert(DinoSaveCodec.TryDecode(json, out var decoded, out var error), error);
        EqualState(Example(), decoded);
        Assert(DinoSaveCodec.TryDecode(DinoSaveCodec.Encode(new DinoSaveData()) + " \n\t", out decoded, out error), error);
        EqualState(new DinoSaveData(), decoded);
    }

    private static void RequiredFields()
    {
        var original = JObject.Parse(DinoSaveCodec.Encode(Example()));
        foreach (var property in original.Properties())
        {
            var removed = (JObject)original.DeepClone();
            removed.Remove(property.Name);
            Reject(removed.ToString(), "missing " + property.Name);
            var nulled = (JObject)original.DeepClone();
            nulled[property.Name] = JValue.CreateNull();
            Reject(nulled.ToString(), "null " + property.Name);
        }
    }

    private static void InvalidTokens()
    {
        foreach (var value in new JToken[] { 0, 2, -1, "1", 1.0, true, long.MaxValue, new JArray(), new JObject() }) InvalidRoot("schemaVersion", value);
        foreach (var value in new JToken[] { "jump", "Dino", "dino ", "", 1, true, new JObject(), new JArray() }) InvalidRoot("gameId", value);
        foreach (var name in new[] { "bestPortraitScore", "bestLandscapeScore" })
            foreach (var value in new JToken[] { 0, 1.0, -1, true, "", "-1", "-0", "+1", " 1", "1 ", "1.0", "1e2", "0x10", "١", "1\0", "0\0", "9223372036854775808", "99999999999999999999999999999999", new JObject(), new JArray() }) InvalidRoot(name, value);
        foreach (var value in new JToken[] { "", "Portrait", "LANDSCAPE", "portrait ", " landscape", "square", 0, true, new JArray(), new JObject() }) InvalidRoot("preferredOrientation", value);
        InvalidRoot("game", new JObject());
        InvalidRoot("unexpectedField", 1);
    }

    private static void InvalidDocuments()
    {
        var valid = DinoSaveCodec.Encode(Example());
        foreach (var invalid in new[] { (string)null, "", " ", "{", "null", "[]", "17", "{}", valid + "{}", valid + "17", valid + " trailing" }) Reject(invalid, "invalid document");
        foreach (var property in JObject.Parse(valid).Properties())
        {
            var field = property.ToString(Newtonsoft.Json.Formatting.None);
            Reject(valid.Replace(field, field + "," + field), "duplicate " + property.Name);
        }
        Reject(valid.Replace("\"gameId\":\"dino\"", "\"gameId\":\"dino\",\"\\u0067ameId\":\"dino\""), "escaped duplicate key");
        Reject(valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":1e999"), "non-finite schema");
    }

    private static void PayloadLimits()
    {
        var valid = DinoSaveCodec.Encode(Example());
        Reject(valid + new string(' ', DinoSaveCodec.MaximumLength), "oversized characters");
        Reject("{\"unknown\":\"" + new string('界', DinoSaveCodec.MaximumLength / 2) + "\"}", "oversized UTF-8 text");
        var deeplyNested = "{\"unknown\":" + new string('[', 128) + "0" + new string(']', 128) + "}";
        Reject(deeplyNested, "excessive depth");
        DinoSaveCodec.TryDecode(deeplyNested, out var ignored, out var reason);
        Assert(reason.IndexOf("MaxDepth", StringComparison.Ordinal) >= 0, "Depth must be rejected by the bounded reader before schema validation.");
    }

    private static void InvalidModels()
    {
        ExpectArgumentException(() => DinoSaveCodec.Encode(null));
        var mutations = new Action<DinoSaveData>[]
        {
            data => data.BestPortraitScore = -1,
            data => data.BestLandscapeScore = long.MinValue,
            data => data.PreferredOrientation = null,
            data => data.PreferredOrientation = "",
            data => data.PreferredOrientation = "Portrait",
            data => data.PreferredOrientation = "landscape "
        };
        foreach (var mutate in mutations)
        {
            var data = Example();
            mutate(data);
            ExpectArgumentException(() => DinoSaveCodec.Encode(data));
        }
    }

    private static void MissingSave()
    {
        WithDirectory(directory =>
        {
            var store = new DinoSaveStore(Path.Combine(directory, "not-created-yet"));
            Assert(store.SavePath == Path.Combine(directory, "not-created-yet", "dino-v1.json"), "Dino filename is not independent.");
            var first = store.Load(out var warning);
            EqualState(new DinoSaveData(), first);
            Assert(warning == null, "First launch should have no warning.");
            var second = store.Load(out warning);
            Assert(!ReferenceEquals(first, second), "Default loads share mutable data.");
            Assert(!Directory.Exists(Path.GetDirectoryName(store.SavePath)), "Load should not create storage directories.");
        });
        ExpectArgumentException(() => new DinoSaveStore(null));
        ExpectArgumentException(() => new DinoSaveStore(" "));
    }

    private static void BackupRecovery()
    {
        WithDirectory(directory =>
        {
            var store = new DinoSaveStore(directory);
            var first = Example();
            Assert(store.TrySave(first, out var error), error);
            EqualState(first, new DinoSaveStore(directory).Load(out var warning));
            Assert(warning == null, warning);
            var second = Example(90, 100, "landscape");
            Assert(store.TrySave(second, out error), error);
            EqualState(second, new DinoSaveStore(directory).Load(out warning));
            Assert(warning == null, warning);
            Assert(DinoSaveCodec.TryDecode(File.ReadAllText(store.SavePath + ".bak"), out var backup, out error), error);
            EqualState(first, backup);
            Assert(!File.Exists(store.SavePath + ".tmp"), "Successful save left temporary data.");
            File.WriteAllText(store.SavePath, "{interrupted write");
            EqualState(first, new DinoSaveStore(directory).Load(out warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Backup recovery needs a warning.");
        });
    }

    private static void MissingPrimaryRecovery()
    {
        WithDirectory(directory =>
        {
            var store = new DinoSaveStore(directory);
            File.WriteAllText(store.SavePath + ".bak", DinoSaveCodec.Encode(Example()));
            EqualState(Example(), store.Load(out var warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Missing primary recovery needs a warning.");
            Assert(!File.Exists(store.SavePath), "Load must not rewrite the primary.");
        });
    }

    private static void PreserveValidBackup()
    {
        WithDirectory(directory =>
        {
            var store = new DinoSaveStore(directory);
            Assert(store.TrySave(Example(), out var error), error);
            Assert(store.TrySave(Example(40, 50), out error), error);
            var backupBytes = File.ReadAllBytes(store.SavePath + ".bak");
            File.WriteAllText(store.SavePath, "broken primary");
            Assert(store.TrySave(Example(100, 200, "landscape"), out error), error);
            EqualState(Example(100, 200, "landscape"), store.Load(out var warning));
            EqualBytes(backupBytes, File.ReadAllBytes(store.SavePath + ".bak"));
            File.WriteAllText(store.SavePath, "broken again");
            EqualState(Example(), store.Load(out warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Expected recovery warning.");
        });
    }

    private static void BothInvalid()
    {
        WithDirectory(directory =>
        {
            var store = new DinoSaveStore(directory);
            File.WriteAllText(store.SavePath, DinoSaveCodec.Encode(Example()).Replace("\"portrait\"", "null"));
            File.WriteAllText(store.SavePath + ".bak", "also broken");
            EqualState(new DinoSaveData(), store.Load(out var warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Corrupt files need a warning.");
        });
    }

    private static void InvalidDiskFiles()
    {
        WithDirectory(directory =>
        {
            var store = new DinoSaveStore(directory);
            File.WriteAllText(store.SavePath + ".bak", DinoSaveCodec.Encode(Example()));
            foreach (var invalid in new[] { new byte[] { 0xff, 0xfe, 0xff }, Encoding.UTF8.GetBytes(new string(' ', DinoSaveCodec.MaximumLength + 1)), new byte[0] })
            {
                File.WriteAllBytes(store.SavePath, invalid);
                EqualState(Example(), store.Load(out var warning));
                Assert(!string.IsNullOrWhiteSpace(warning), "Invalid file recovery needs a warning.");
            }
        });
    }

    private static void UnreadablePrimary()
    {
        WithDirectory(directory =>
        {
            var store = new DinoSaveStore(directory);
            Directory.CreateDirectory(store.SavePath);
            EqualState(new DinoSaveData(), store.Load(out var warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "A directory at the save path is an error, not a missing file.");
        });
    }

    private static void WriteFailures()
    {
        WithDirectory(directory =>
        {
            var store = new DinoSaveStore(directory);
            Directory.Delete(directory);
            File.WriteAllText(directory, "occupied");
            Assert(!store.TrySave(Example(), out var error), "Writing into a file path should fail.");
            Assert(!string.IsNullOrWhiteSpace(error), "Missing storage failure reason.");
        });
        foreach (var blockedSuffix in new[] { ".tmp", ".bak" })
        {
            WithDirectory(directory =>
            {
                var store = new DinoSaveStore(directory);
                Assert(store.TrySave(Example(), out var error), error);
                var original = File.ReadAllBytes(store.SavePath);
                Directory.CreateDirectory(store.SavePath + blockedSuffix);
                Assert(!store.TrySave(Example(100, 200), out error), "Blocked " + blockedSuffix + " should fail.");
                Assert(!string.IsNullOrWhiteSpace(error), "Missing write failure reason.");
                EqualBytes(original, File.ReadAllBytes(store.SavePath));
                EqualState(Example(), store.Load(out var warning));
                Assert(!File.Exists(store.SavePath + ".tmp"), "Failed replacement left a temporary file.");
            });
        }
    }

    private static void InvalidWritePreservesFiles()
    {
        WithDirectory(directory =>
        {
            var store = new DinoSaveStore(directory);
            Assert(store.TrySave(Example(), out var error), error);
            Assert(store.TrySave(Example(50, 80), out error), error);
            var original = File.ReadAllBytes(store.SavePath);
            var backup = File.ReadAllBytes(store.SavePath + ".bak");
            foreach (var bad in new[] { (DinoSaveData)null, Example(-1, 0), Example(0, -1), Example(0, 0, null), Example(0, 0, "invalid") })
            {
                Assert(!store.TrySave(bad, out error), "Invalid state was written.");
                Assert(!string.IsNullOrWhiteSpace(error), "Invalid state needs a failure reason.");
                EqualBytes(original, File.ReadAllBytes(store.SavePath));
                EqualBytes(backup, File.ReadAllBytes(store.SavePath + ".bak"));
            }
        });
    }

    private static void ExistingFilesUntouched()
    {
        WithDirectory(directory =>
        {
            var sentinel = new byte[] { 0, 255, 1, 2, 8, 13, 10, 31 };
            var names = new[] { "xanaduyonder-v1.json", "jump-v1.json", "watermelon-v1.json", "sheep-v1.json", "sudoku-v1.json", "idiom-v1.json" };
            var suffixes = new[] { "", ".bak", ".tmp" };
            foreach (var name in names)
                foreach (var suffix in suffixes) File.WriteAllBytes(Path.Combine(directory, name + suffix), sentinel);
            var store = new DinoSaveStore(directory);
            Assert(store.TrySave(Example(), out var error), error);
            Assert(store.TrySave(Example(100, 200), out error), error);
            File.WriteAllText(store.SavePath, "broken");
            EqualState(Example(), store.Load(out var warning));
            Assert(store.TrySave(Example(300, 400), out error), error);
            foreach (var name in names)
                foreach (var suffix in suffixes) EqualBytes(sentinel, File.ReadAllBytes(Path.Combine(directory, name + suffix)));
        });
    }

    private static void LoadedDataIndependent()
    {
        WithDirectory(directory =>
        {
            var store = new DinoSaveStore(directory);
            Assert(store.TrySave(Example(), out var error), error);
            var first = store.Load(out var warning);
            var second = store.Load(out warning);
            Assert(!ReferenceEquals(first, second), "Loads share a mutable object.");
            first.BestPortraitScore = 0;
            first.BestLandscapeScore = 0;
            first.PreferredOrientation = "landscape";
            EqualState(Example(), second);
            EqualState(Example(), store.Load(out warning));
        });
    }

    private static void InvalidRoot(string name, JToken value)
    {
        var root = JObject.Parse(DinoSaveCodec.Encode(Example()));
        root[name] = value.DeepClone();
        Reject(root.ToString(), name + "=" + value);
    }

    private static void Reject(string json, string label)
    {
        Assert(!DinoSaveCodec.TryDecode(json, out var data, out var reason), "Accepted " + label + ": " + json);
        Assert(data == null, "Rejected payload exposed partial state: " + label);
        Assert(!string.IsNullOrWhiteSpace(reason), "Rejected payload has no reason: " + label);
    }

    private static DinoSaveData Example(long portrait = 12, long landscape = 34, string orientation = "portrait")
    {
        return new DinoSaveData { BestPortraitScore = portrait, BestLandscapeScore = landscape, PreferredOrientation = orientation };
    }

    private static void EqualState(DinoSaveData expected, DinoSaveData actual)
    {
        Assert(actual != null, "Load returned null.");
        Assert(expected.BestPortraitScore == actual.BestPortraitScore, "Portrait record changed.");
        Assert(expected.BestLandscapeScore == actual.BestLandscapeScore, "Landscape record changed.");
        Assert(expected.PreferredOrientation == actual.PreferredOrientation, "Orientation preference changed.");
    }

    private static void EqualBytes(byte[] expected, byte[] actual)
    {
        Assert(expected.Length == actual.Length, "Existing file size changed.");
        for (var index = 0; index < expected.Length; index++) Assert(expected[index] == actual[index], "Existing file byte changed at " + index);
    }

    private static void WithDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "XanaduYonder-Dino-Persistence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            if (File.Exists(directory)) File.Delete(directory);
        }
    }

    private static void ExpectArgumentException(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Expected ArgumentException for invalid data.");
    }

    private static void Check(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS: " + name); }
        catch (Exception error) { failed++; Console.Error.WriteLine("FAIL: " + name + "\n" + error); }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
