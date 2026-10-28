using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Runtime.FlappyBird;

internal static class Program
{
    private static int passed;
    private static int failed;

    private static int Main()
    {
        Check("Data starts at zero and Copy is independent", Copy);
        Check("Schema 1 round trips exact 64-bit decimal strings", RoundTrip);
        Check("Known payload accepts only the independent record schema", KnownPayload);
        Check("Every required field rejects missing and explicit null", RequiredFields);
        Check("Wrong types, identities, versions, ranges and unknown fields are rejected", InvalidTokens);
        Check("Malformed, duplicate and trailing documents are rejected", InvalidDocuments);
        Check("Character, UTF-8 byte and parser depth limits are enforced", PayloadLimits);
        Check("Invalid in-memory models cannot be encoded", InvalidModels);
        Check("First launch reads fresh zero data without creating files", MissingSave);
        Check("Valid writes flush and retain the previous valid primary as backup", BackupRecovery);
        Check("Missing primary recovers from backup without rewriting files", MissingPrimaryRecovery);
        Check("Corrupt primary replacement retains a good backup byte-for-byte", PreserveValidBackup);
        Check("Invalid primary and backup return zero and a persistent warning", BothInvalid);
        Check("Invalid UTF-8 and oversized disk payloads recover from backup", InvalidDiskFiles);
        Check("Unreadable primary is reported as an error", UnreadablePrimary);
        Check("Blocked directory, temporary and backup paths preserve committed bytes", WriteFailures);
        Check("Invalid models do not mutate or create disk files", InvalidWritePreservesFiles);
        Check("Untrusted load rejects default overwrite until a positive new record", UntrustedDefaultProtection);
        Check("Untrusted load protection survives removal of the failed files", UntrustedHistory);
        Check("A higher valid backup cannot be replaced by a lower primary", PreserveHigherBackup);
        Check("Disk corruption after load cannot permit default overwrite", CorruptionAfterLoad);
        Check("A stale or lower record cannot overwrite a newer disk record", NoRecordRegression);
        Check("Repeated same-record saves preserve the last distinct backup", SameRecordIsIdempotent);
        Check("A stale temporary file is not treated as a committed record", TemporaryFilePolicy);
        Check("All other game saves remain byte-identical", ExistingFilesUntouched);
        Check("Loads return independent records and do not retain caller mutation", LoadedDataIndependent);
        Console.WriteLine("Flappy Bird persistence checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void Copy()
    {
        var original = new FlappySaveData();
        Assert(original.BestScore == 0, "New players must start at zero.");
        original.BestScore = long.MaxValue;
        var copy = original.Copy();
        Assert(!ReferenceEquals(original, copy) && copy.BestScore == long.MaxValue, "Copy lost score or aliases its source.");
        copy.BestScore = 0;
        Assert(original.BestScore == long.MaxValue, "Copy mutation changed source.");
    }

    private static void RoundTrip()
    {
        foreach (var score in new[] { 0L, 1L, 9007199254740993L, long.MaxValue })
        {
            var input = Data(score);
            var json = FlappySaveCodec.Encode(input);
            var root = JObject.Parse(json);
            Assert(root.Count == 3, "Only schemaVersion, gameId and bestScore may be persisted.");
            Assert(root["schemaVersion"].Type == JTokenType.Integer && (int)root["schemaVersion"] == 1, "Wrong schema version.");
            Assert(root["gameId"].Type == JTokenType.String && (string)root["gameId"] == "flappy-bird", "Wrong stable game ID.");
            Assert(root["bestScore"].Type == JTokenType.String, "Score must be a decimal string.");
            Assert((string)root["bestScore"] == score.ToString(System.Globalization.CultureInfo.InvariantCulture), "Score lost precision.");
            Assert(FlappySaveCodec.TryDecode(json, out var decoded, out var error), error);
            EqualScore(score, decoded);
            Assert(!ReferenceEquals(input, decoded), "Decode aliases source.");
        }
    }

    private static void KnownPayload()
    {
        const string json = "{\"bestScore\":\"37\",\"gameId\":\"flappy-bird\",\"schemaVersion\":1}";
        Assert(FlappySaveCodec.TryDecode(json + " \n\t", out var decoded, out var error), error);
        EqualScore(37, decoded);
    }

    private static void RequiredFields()
    {
        foreach (var property in JObject.Parse(Json(37)).Properties())
        {
            var root = JObject.Parse(Json(37));
            root.Remove(property.Name);
            Reject(root.ToString(), "missing " + property.Name);
            root[property.Name] = JValue.CreateNull();
            Reject(root.ToString(), "null " + property.Name);
        }
    }

    private static void InvalidTokens()
    {
        foreach (var token in new JToken[] { 0, -1, 2, "1", 1.0, true, long.MaxValue, new JArray(), new JObject() }) InvalidField("schemaVersion", token);
        foreach (var token in new JToken[] { "dino", "Flappy-bird", "flappy-bird ", "", 1, true, new JArray(), new JObject() }) InvalidField("gameId", token);
        foreach (var token in new JToken[] { 0, 1.0, -1, true, "", "-1", "-0", "+1", " 1", "1 ", "1.0", "1e2", "0x10", "١", "1\0", "0\0", "9223372036854775808", "99999999999999999999999999999", new JArray(), new JObject() }) InvalidField("bestScore", token);
        foreach (var field in new[] { "activeRun", "soundEnabled", "hapticsEnabled", "game", "BestScore", "unexpected" }) InvalidField(field, new JObject());
    }

    private static void InvalidDocuments()
    {
        var valid = Json(37);
        foreach (var invalid in new[] { (string)null, "", " ", "{", "null", "[]", "17", "{}", valid + "{}", valid + "17", valid + " garbage" }) Reject(invalid, "invalid document");
        foreach (var property in JObject.Parse(valid).Properties())
        {
            var field = property.ToString(Formatting.None);
            Reject(valid.Replace(field, field + "," + field), "duplicate " + property.Name);
        }
        Reject(valid.Replace("\"gameId\":\"flappy-bird\"", "\"gameId\":\"flappy-bird\",\"\\u0067ameId\":\"flappy-bird\""), "escaped duplicate key");
        Reject(valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":1e999"), "nonfinite schema");
    }

    private static void PayloadLimits()
    {
        Reject(Json(1) + new string(' ', FlappySaveCodec.MaximumLength), "oversized characters");
        Reject("{\"unknown\":\"" + new string('界', FlappySaveCodec.MaximumLength / 2) + "\"}", "oversized UTF-8");
        var deep = "{\"unknown\":" + new string('[', 128) + "0" + new string(']', 128) + "}";
        Reject(deep, "excessive depth");
        FlappySaveCodec.TryDecode(deep, out var ignored, out var reason);
        Assert(reason.IndexOf("MaxDepth", StringComparison.Ordinal) >= 0, "Depth must be bounded by the real parser.");
    }

    private static void InvalidModels()
    {
        foreach (var invalid in new[] { (FlappySaveData)null, Data(-1), Data(long.MinValue) })
            ExpectArgument(() => FlappySaveCodec.Encode(invalid));
    }

    private static void MissingSave()
    {
        WithDirectory(directory =>
        {
            var store = new FlappySaveStore(Path.Combine(directory, "missing"));
            Assert(store.SavePath == Path.Combine(directory, "missing", "flappy-bird-v1.json"), "Wrong independent filename.");
            var first = store.Load(out var warning);
            EqualScore(0, first);
            Assert(warning == null, "First launch should be silent.");
            Assert(!ReferenceEquals(first, store.Load(out warning)), "Default objects are shared.");
            Assert(!Directory.Exists(Path.GetDirectoryName(store.SavePath)), "Load created storage directory.");
        });
        ExpectArgument(() => new FlappySaveStore(null));
        ExpectArgument(() => new FlappySaveStore(" "));
    }

    private static void BackupRecovery()
    {
        WithDirectory(directory =>
        {
            var store = new FlappySaveStore(directory);
            Save(store, 12);
            EqualScore(12, new FlappySaveStore(directory).Load(out var warning));
            Assert(warning == null, warning);
            Save(store, 37);
            EqualScore(37, new FlappySaveStore(directory).Load(out warning));
            Assert(warning == null, warning);
            EqualFile(12, store.SavePath + ".bak");
            Assert(!File.Exists(store.SavePath + ".tmp"), "Successful write left a temporary file.");
            File.WriteAllText(store.SavePath, "{interrupted");
            EqualScore(12, new FlappySaveStore(directory).Load(out warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Backup recovery needs a warning.");
        });
    }

    private static void MissingPrimaryRecovery()
    {
        WithDirectory(directory =>
        {
            var store = new FlappySaveStore(directory);
            File.WriteAllText(store.SavePath + ".bak", Json(37));
            EqualScore(37, store.Load(out var warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Missing primary recovery needs a warning.");
            Assert(!File.Exists(store.SavePath), "Load rewrote primary.");
            var backup = File.ReadAllBytes(store.SavePath + ".bak");
            Save(store, 37);
            EqualFile(37, store.SavePath);
            EqualBytes(backup, File.ReadAllBytes(store.SavePath + ".bak"));
        });
    }

    private static void PreserveValidBackup()
    {
        WithDirectory(directory =>
        {
            var store = new FlappySaveStore(directory);
            Save(store, 12);
            Save(store, 37);
            var backup = File.ReadAllBytes(store.SavePath + ".bak");
            File.WriteAllText(store.SavePath, "broken");
            Save(store, 40);
            EqualFile(40, store.SavePath);
            EqualBytes(backup, File.ReadAllBytes(store.SavePath + ".bak"));
            Assert(store.Load(out var warning).BestScore == 40 && warning == null, "Successful repair still warned.");
        });
    }

    private static void BothInvalid()
    {
        WithDirectory(directory =>
        {
            var store = new FlappySaveStore(directory);
            File.WriteAllText(store.SavePath, "{\"schemaVersion\":1}");
            File.WriteAllText(store.SavePath + ".bak", "bad backup");
            var primary = File.ReadAllBytes(store.SavePath);
            var backup = File.ReadAllBytes(store.SavePath + ".bak");
            for (var i = 0; i < 2; i++)
            {
                EqualScore(0, store.Load(out var warning));
                Assert(!string.IsNullOrWhiteSpace(warning), "Bad files require a warning every load.");
            }
            EqualBytes(primary, File.ReadAllBytes(store.SavePath));
            EqualBytes(backup, File.ReadAllBytes(store.SavePath + ".bak"));
        });
    }

    private static void InvalidDiskFiles()
    {
        WithDirectory(directory =>
        {
            var store = new FlappySaveStore(directory);
            File.WriteAllText(store.SavePath + ".bak", Json(37));
            foreach (var invalid in new[] { new byte[] { 0xff, 0xfe, 0xff }, Encoding.UTF8.GetBytes(new string(' ', FlappySaveCodec.MaximumLength + 1)), new byte[0] })
            {
                File.WriteAllBytes(store.SavePath, invalid);
                EqualScore(37, store.Load(out var warning));
                Assert(!string.IsNullOrWhiteSpace(warning), "Invalid file recovery needs warning.");
            }
        });
    }

    private static void UnreadablePrimary()
    {
        WithDirectory(directory =>
        {
            var store = new FlappySaveStore(directory);
            Directory.CreateDirectory(store.SavePath);
            EqualScore(0, store.Load(out var warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "A directory at save path must not resemble first launch.");
            Assert(!store.TrySave(Data(37), out var error) && !string.IsNullOrWhiteSpace(error), "Unreadable primary accepted a write.");
            Assert(!File.Exists(store.SavePath + ".tmp"), "Failed primary replacement left temporary data.");
        });
    }

    private static void WriteFailures()
    {
        WithDirectory(directory =>
        {
            Directory.Delete(directory);
            File.WriteAllText(directory, "occupied");
            var store = new FlappySaveStore(directory);
            Assert(!store.TrySave(Data(37), out var error) && !string.IsNullOrWhiteSpace(error), "File as directory accepted write.");
        });
        foreach (var suffix in new[] { ".tmp", ".bak" })
        {
            WithDirectory(directory =>
            {
                var store = new FlappySaveStore(directory);
                Save(store, 12);
                var before = File.ReadAllBytes(store.SavePath);
                Directory.CreateDirectory(store.SavePath + suffix);
                Assert(!store.TrySave(Data(37), out var error) && !string.IsNullOrWhiteSpace(error), "Blocked " + suffix + " accepted write.");
                EqualBytes(before, File.ReadAllBytes(store.SavePath));
                Assert(!File.Exists(store.SavePath + ".tmp"), "Failed save left temporary data.");
            });
        }
    }

    private static void InvalidWritePreservesFiles()
    {
        WithDirectory(directory =>
        {
            var absent = Path.Combine(directory, "absent");
            Assert(!new FlappySaveStore(absent).TrySave(Data(-1), out var ignored), "Invalid record accepted.");
            Assert(!Directory.Exists(absent), "Invalid model created directory.");
            var store = new FlappySaveStore(directory);
            Save(store, 12);
            Save(store, 37);
            var primary = File.ReadAllBytes(store.SavePath);
            var backup = File.ReadAllBytes(store.SavePath + ".bak");
            foreach (var invalid in new[] { (FlappySaveData)null, Data(-1), Data(long.MinValue) })
            {
                Assert(!store.TrySave(invalid, out var error) && !string.IsNullOrWhiteSpace(error), "Invalid data accepted write.");
                EqualBytes(primary, File.ReadAllBytes(store.SavePath));
                EqualBytes(backup, File.ReadAllBytes(store.SavePath + ".bak"));
            }
        });
    }

    private static void UntrustedDefaultProtection()
    {
        foreach (var useLoad in new[] { false, true })
        {
            WithDirectory(directory =>
            {
                var store = new FlappySaveStore(directory);
                File.WriteAllText(store.SavePath, "unreadable record");
                File.WriteAllText(store.SavePath + ".bak", "unreadable backup");
                var primary = File.ReadAllBytes(store.SavePath);
                var backup = File.ReadAllBytes(store.SavePath + ".bak");
                if (useLoad) EqualScore(0, store.Load(out var ignored));
                Assert(!store.TrySave(Data(0), out var error) && !string.IsNullOrWhiteSpace(error), "Default zero destroyed untrusted data.");
                EqualBytes(primary, File.ReadAllBytes(store.SavePath));
                EqualBytes(backup, File.ReadAllBytes(store.SavePath + ".bak"));
                Save(store, 1);
                EqualFile(1, store.SavePath);
                EqualBytes(backup, File.ReadAllBytes(store.SavePath + ".bak"));
            });
        }
    }

    private static void CorruptionAfterLoad()
    {
        WithDirectory(directory =>
        {
            var store = new FlappySaveStore(directory);
            Save(store, 37);
            EqualScore(37, store.Load(out var warning));
            File.WriteAllText(store.SavePath, "broken after read");
            Assert(!store.TrySave(Data(1), out var error), "Known record was downgraded after external corruption.");
            Assert(File.ReadAllText(store.SavePath) == "broken after read", "Rejected write changed corrupt primary.");
            Save(store, 37);
            EqualFile(37, store.SavePath);
        });
    }

    private static void UntrustedHistory()
    {
        foreach (var corruptSuffix in new[] { "", ".bak" })
        {
            WithDirectory(directory =>
            {
                var store = new FlappySaveStore(directory);
                File.WriteAllText(store.SavePath + corruptSuffix, "unknown record");
                EqualScore(0, store.Load(out var warning));
                Assert(!string.IsNullOrWhiteSpace(warning), "A sole invalid file must warn.");
                File.Delete(store.SavePath + corruptSuffix);
                Assert(!store.TrySave(Data(0), out var error), "Default value overwrote an untrusted load after file removal.");
                Assert(!File.Exists(store.SavePath), "Rejected save created a primary.");
                Save(store, 1);
                EqualScore(1, store.Load(out warning));
                Assert(warning == null, "Successful new record did not clear untrusted state.");
            });
        }
    }

    private static void PreserveHigherBackup()
    {
        WithDirectory(directory =>
        {
            var store = new FlappySaveStore(directory);
            File.WriteAllText(store.SavePath, Json(12));
            File.WriteAllText(store.SavePath + ".bak", Json(37));
            var backup = File.ReadAllBytes(store.SavePath + ".bak");
            EqualScore(37, store.Load(out var warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Recovering a higher backup must warn.");
            Assert(!store.TrySave(Data(20), out var error), "Higher backup was ignored.");
            Save(store, 37);
            EqualFile(37, store.SavePath);
            EqualBytes(backup, File.ReadAllBytes(store.SavePath + ".bak"));
            EqualScore(37, store.Load(out warning));
            Assert(warning == null, "An equal-score backup should not override the valid primary.");
            Save(store, 40);
            EqualFile(40, store.SavePath);
            EqualBytes(backup, File.ReadAllBytes(store.SavePath + ".bak"));
        });
    }

    private static void NoRecordRegression()
    {
        WithDirectory(directory =>
        {
            var stale = new FlappySaveStore(directory);
            Save(stale, 12);
            stale.Load(out var warning);
            var fresh = new FlappySaveStore(directory);
            Save(fresh, 37);
            var primary = File.ReadAllBytes(stale.SavePath);
            var backup = File.ReadAllBytes(stale.SavePath + ".bak");
            Assert(!stale.TrySave(Data(20), out var error), "Stale data overwrote a higher disk record.");
            EqualBytes(primary, File.ReadAllBytes(stale.SavePath));
            EqualBytes(backup, File.ReadAllBytes(stale.SavePath + ".bak"));
            File.WriteAllText(stale.SavePath, "broken");
            Assert(!new FlappySaveStore(directory).TrySave(Data(1), out error), "Save regressed a higher legal backup.");
        });
    }

    private static void SameRecordIsIdempotent()
    {
        WithDirectory(directory =>
        {
            var store = new FlappySaveStore(directory);
            Save(store, 12);
            Save(store, 37);
            var primary = File.ReadAllBytes(store.SavePath);
            var backup = File.ReadAllBytes(store.SavePath + ".bak");
            Save(store, 37);
            EqualBytes(primary, File.ReadAllBytes(store.SavePath));
            EqualBytes(backup, File.ReadAllBytes(store.SavePath + ".bak"));
        });
    }

    private static void TemporaryFilePolicy()
    {
        WithDirectory(directory =>
        {
            var store = new FlappySaveStore(directory);
            File.WriteAllText(store.SavePath + ".tmp", Json(long.MaxValue));
            EqualScore(0, store.Load(out var warning));
            Assert(warning == null, "An uncommitted temp file was reported as a saved record.");
            Assert(!store.TrySave(Data(-1), out var error), "Invalid data accepted.");
            EqualFile(long.MaxValue, store.SavePath + ".tmp");
            Save(store, 37);
            EqualFile(37, store.SavePath);
            Assert(!File.Exists(store.SavePath + ".tmp"), "Stale temporary file remained after successful save.");
        });
    }

    private static void ExistingFilesUntouched()
    {
        WithDirectory(directory =>
        {
            var names = new[] { "xanaduyonder-v1.json", "jump-v1.json", "watermelon-v1.json", "sheep-v1.json", "sudoku-v1.json", "idiom-v1.json", "dino-v1.json", "sokoban-v1.json", "minesweeper-v1.json", "happy-ball-v1.json", "gomoku-v1.json", "zuma-v1.json" };
            var suffixes = new[] { "", ".bak", ".tmp" };
            var sentinel = new byte[] { 0, 255, 31, 10, 13, 2, 1, 8 };
            foreach (var name in names)
                foreach (var suffix in suffixes) File.WriteAllBytes(Path.Combine(directory, name + suffix), sentinel);
            var store = new FlappySaveStore(directory);
            Save(store, 12);
            Save(store, 37);
            File.WriteAllText(store.SavePath, "bad primary");
            EqualScore(12, store.Load(out var warning));
            Save(store, 40);
            foreach (var name in names)
                foreach (var suffix in suffixes) EqualBytes(sentinel, File.ReadAllBytes(Path.Combine(directory, name + suffix)));
        });
    }

    private static void LoadedDataIndependent()
    {
        WithDirectory(directory =>
        {
            var store = new FlappySaveStore(directory);
            Save(store, 37);
            var first = store.Load(out var warning);
            var second = store.Load(out warning);
            Assert(!ReferenceEquals(first, second), "Loads share mutable state.");
            first.BestScore = long.MaxValue;
            EqualScore(37, second);
            EqualScore(37, store.Load(out warning));
            Save(store, 38);
        });
    }

    private static FlappySaveData Data(long score) { return new FlappySaveData { BestScore = score }; }
    private static string Json(long score) { return "{\"schemaVersion\":1,\"gameId\":\"flappy-bird\",\"bestScore\":\"" + score + "\"}"; }
    private static void Save(FlappySaveStore store, long score) { Assert(store.TrySave(Data(score), out var error), error); }
    private static void EqualScore(long score, FlappySaveData data) { Assert(data != null && data.BestScore == score, "Expected score " + score + "."); }
    private static void EqualFile(long score, string path)
    {
        Assert(FlappySaveCodec.TryDecode(File.ReadAllText(path), out var data, out var error), error);
        EqualScore(score, data);
    }
    private static void InvalidField(string name, JToken value)
    {
        var root = JObject.Parse(Json(37));
        root[name] = value.DeepClone();
        Reject(root.ToString(), name + "=" + value);
    }
    private static void Reject(string json, string label)
    {
        Assert(!FlappySaveCodec.TryDecode(json, out var data, out var error), "Accepted " + label);
        Assert(data == null, "Rejected payload exposed partial state.");
        Assert(!string.IsNullOrWhiteSpace(error), "Rejected payload lacks reason.");
    }
    private static void EqualBytes(byte[] expected, byte[] actual)
    {
        Assert(expected.Length == actual.Length, "File length changed.");
        for (var i = 0; i < expected.Length; i++) Assert(expected[i] == actual[i], "File byte changed at " + i);
    }
    private static void WithDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "XanaduYonder-Flappy-Persistence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            if (File.Exists(directory)) File.Delete(directory);
        }
    }
    private static void ExpectArgument(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Expected ArgumentException.");
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
