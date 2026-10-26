using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Jump;
using XanaduYonder.Runtime.Jump;

internal static class Program
{
    private static int passed;
    private static int failed;

    private static int Main()
    {
        Check("Current snapshot round trip preserves every field and 64-bit scores", RoundTrip);
        Check("Version 1 known payload decodes without serializer coercion", KnownPayload);
        Check("Terminal snapshot round trip remains terminal", TerminalRoundTrip);
        Check("Every required field rejects missing and explicit null", RequiredFields);
        Check("Token types, ranges, and incompatible schemas are rejected", InvalidTokens);
        Check("Malformed JSON, duplicate properties, and trailing data are rejected", InvalidDocuments);
        Check("Payload size is bounded to 64 KiB", OversizedPayloads);
        Check("Invalid model state is rejected on both boundaries", InvalidStates);
        Check("Missing file starts without warning", MissingSave);
        Check("Primary save and valid backup recover across fresh store instances", BackupRecovery);
        Check("Replacing a corrupt primary preserves the last good backup", PreserveValidBackup);
        Check("Two invalid files return no partial saved state", BothInvalid);
        Check("Invalid UTF-8 primary recovers from valid backup", InvalidUtf8);
        Check("Oversized disk file falls back to valid backup", OversizedFile);
        Check("Storage failures are reported without losing the previous save", WriteFailures);
        Check("Invalid save cannot replace valid data", InvalidWritePreservesPrimary);
        Check("Jump writes leave existing 2048 files byte-identical", ExistingFilesUntouched);
        Check("Loaded snapshots are independent objects", LoadedSnapshotsIndependent);
        Console.WriteLine("Jump persistence checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void RoundTrip()
    {
        var original = Example();
        original.Game.Score = 9007199254740993L;
        original.Game.Steps = int.MaxValue;
        original.BestScore = long.MaxValue;
        var json = JumpSaveCodec.Encode(original);
        var root = JObject.Parse(json);
        Assert((int)root["schemaVersion"] == 1 && (string)root["gameId"] == "jump", "Wrong jump schema identity.");
        Assert(root["bestScore"].Type == JTokenType.String && root["game"]["score"].Type == JTokenType.String,
            "64-bit scores must be written as strings.");
        Assert(JumpSaveCodec.TryDecode(json, out var decoded, out var reason), reason);
        EqualState(original, decoded);
        Assert(!ReferenceEquals(original.Game, decoded.Game), "Decoded snapshot aliases input.");
    }

    private static void KnownPayload()
    {
        const string json = "{\"schemaVersion\":1,\"gameId\":\"jump\",\"game\":{\"currentHalfSize\":0.95,\"nextX\":2.8,\"nextZ\":0,\"nextHalfSize\":0.85,\"playerX\":0,\"playerZ\":0,\"score\":\"5\",\"steps\":2,\"streak\":1,\"isGameOver\":false,\"randomState\":17},\"bestScore\":\"20\"}";
        Assert(JumpSaveCodec.TryDecode(json, out var decoded, out var error), error);
        EqualState(Example(), decoded);
    }

    private static void TerminalRoundTrip()
    {
        var original = Example();
        var game = new JumpGame(original.Game);
        Assert(game.Jump(JumpGame.MaxChargeSeconds).Outcome == JumpOutcome.Missed, "Fixture must be an actual missed jump.");
        original.Game = game.GetSnapshot();
        Assert(original.Game.TryValidate(out var invalid), invalid);
        Assert(JumpSaveCodec.TryDecode(JumpSaveCodec.Encode(original), out var result, out var error), error);
        EqualState(original, result);
    }

    private static void RequiredFields()
    {
        var original = JObject.Parse(JumpSaveCodec.Encode(Example()));
        foreach (var section in new[] { "", "game" })
        {
            var target = section.Length == 0 ? original : (JObject)original[section];
            foreach (var property in target.Properties())
            {
                var removed = (JObject)original.DeepClone();
                var removedTarget = section.Length == 0 ? removed : (JObject)removed[section];
                removedTarget.Remove(property.Name);
                Reject(removed.ToString(), "missing " + section + "." + property.Name);
                var nulled = (JObject)original.DeepClone();
                var nulledTarget = section.Length == 0 ? nulled : (JObject)nulled[section];
                nulledTarget[property.Name] = JValue.CreateNull();
                Reject(nulled.ToString(), "null " + section + "." + property.Name);
            }
        }
    }

    private static void InvalidTokens()
    {
        foreach (var value in new JToken[] { 0, 2, "1", 1.0, true, new JArray() }) InvalidRoot("schemaVersion", value);
        foreach (var value in new JToken[] { "2048", "JUMP", "", 1, new JObject() }) InvalidRoot("gameId", value);
        foreach (var value in new JToken[] { new JArray(), "state", 1 }) InvalidRoot("game", value);
        foreach (var value in new JToken[] { 5, -1, "-1", "9223372036854775808", "1.5", "+5", " 5", "5 ", true })
        {
            InvalidRoot("bestScore", value);
            InvalidGame("score", value);
        }
        InvalidRoot("bestScore", "4");
        foreach (var name in new[] { "steps", "streak", "randomState" })
            foreach (var value in new JToken[] { "2", 2.0, true, -1, long.MaxValue }) InvalidGame(name, value);
        foreach (var name in new[] { "currentHalfSize", "nextX", "nextZ", "nextHalfSize", "playerX", "playerZ" })
            foreach (var value in new JToken[] { "0.8", true, new JObject(), new JArray(), double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                InvalidGame(name, value);
        foreach (var value in new JToken[] { "false", 0, 1, new JObject() }) InvalidGame("isGameOver", value);
        InvalidRoot("unexpectedField", 1);
        InvalidGame("unexpectedField", 1);
    }

    private static void InvalidDocuments()
    {
        var valid = JumpSaveCodec.Encode(Example());
        foreach (var invalid in new[] { (string)null, "", " ", "{", "null", "[]", "17", "{}", valid + "{}", valid + "17", valid + " trailing",
            valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1"),
            valid.Replace("\"steps\":2", "\"steps\":2,\"steps\":2"),
            valid.Replace("\"nextX\":2.8", "\"nextX\":1e999"),
            valid.Replace("\"nextX\":2.8", "\"nextX\":NaN"),
            valid.Replace("\"nextX\":2.8", "\"nextX\":Infinity"),
            valid.Replace("\"nextX\":2.8", "\"nextX\":-Infinity") }) Reject(invalid, "invalid document");
        Assert(JumpSaveCodec.TryDecode(valid + " \n\t", out var data, out var error), error);
        Assert(data != null, "Trailing whitespace is valid JSON.");
    }

    private static void OversizedPayloads()
    {
        var valid = JumpSaveCodec.Encode(Example());
        Reject(valid + new string(' ', JumpSaveCodec.MaximumLength), "oversized text");
        Reject("{\"unknown\":\"" + new string('界', JumpSaveCodec.MaximumLength / 2) + "\"}", "oversized UTF-8 text");
    }

    private static void InvalidStates()
    {
        var mutations = new Action<JumpSnapshot>[]
        {
            game => game.CurrentHalfSize = 0,
            game => game.NextHalfSize = 8,
            game => game.NextX = 0,
            game => game.NextZ = 2.8,
            game => game.PlayerX = 2,
            game => game.PlayerZ = double.NaN,
            game => game.Score = -1,
            game => game.Steps = -1,
            game => game.Steps = 6,
            game => game.Streak = -1,
            game => game.Streak = 3,
            game => game.RandomState = 0,
            game => game.IsGameOver = true
        };
        foreach (var mutate in mutations)
        {
            var invalid = Example();
            mutate(invalid.Game);
            ExpectArgumentException(() => JumpSaveCodec.Encode(invalid));
        }
        ExpectArgumentException(() => JumpSaveCodec.Encode(null));
        ExpectArgumentException(() => JumpSaveCodec.Encode(new JumpSaveData { Game = null, BestScore = 10 }));
        var badBest = Example();
        badBest.BestScore = badBest.Game.Score - 1;
        ExpectArgumentException(() => JumpSaveCodec.Encode(badBest));
        InvalidGame("currentHalfSize", 0);
        InvalidGame("nextHalfSize", 8);
        InvalidGame("nextX", 0);
        InvalidGame("nextZ", 2.8);
        InvalidGame("playerX", 2);
        InvalidGame("steps", 6);
        InvalidGame("streak", 3);
        InvalidGame("randomState", 0);
        InvalidGame("isGameOver", true);
    }

    private static void MissingSave()
    {
        WithDirectory(directory =>
        {
            var store = new JumpSaveStore(directory);
            Assert(store.SavePath == Path.Combine(directory, "jump-v1.json"), "Jump filename is not independent.");
            Assert(store.Load(out var warning) == null && warning == null, "First launch should have no warning.");
        });
        ExpectArgumentException(() => new JumpSaveStore(null));
        ExpectArgumentException(() => new JumpSaveStore(" "));
    }

    private static void BackupRecovery()
    {
        WithDirectory(directory =>
        {
            var store = new JumpSaveStore(directory);
            var first = Example();
            Assert(store.TrySave(first, out var error), error);
            EqualState(first, store.Load(out var warning));
            Assert(warning == null, warning);
            var second = Example(12);
            Assert(store.TrySave(second, out error), error);
            EqualState(second, new JumpSaveStore(directory).Load(out warning));
            Assert(warning == null, warning);
            Assert(File.Exists(store.SavePath + ".bak"), "Second save must preserve a backup.");
            Assert(!File.Exists(store.SavePath + ".tmp"), "Successful save left temporary data.");
            File.WriteAllText(store.SavePath, "{interrupted write");
            EqualState(first, new JumpSaveStore(directory).Load(out warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Backup recovery needs a warning.");
        });
    }

    private static void PreserveValidBackup()
    {
        WithDirectory(directory =>
        {
            var store = new JumpSaveStore(directory);
            Assert(store.TrySave(Example(), out var error), error);
            Assert(store.TrySave(Example(12), out error), error);
            File.WriteAllText(store.SavePath, "broken primary");
            Assert(store.TrySave(Example(20), out error), error);
            EqualState(Example(20), store.Load(out var warning));
            File.WriteAllText(store.SavePath, "broken again");
            EqualState(Example(), store.Load(out warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Expected recovery warning.");
        });
    }

    private static void BothInvalid()
    {
        WithDirectory(directory =>
        {
            var store = new JumpSaveStore(directory);
            File.WriteAllText(store.SavePath, "broken");
            File.WriteAllText(store.SavePath + ".bak", "also broken");
            Assert(store.Load(out var warning) == null, "Two corrupt files yielded partial data.");
            Assert(!string.IsNullOrWhiteSpace(warning), "Corrupt files need a warning.");
        });
    }

    private static void InvalidUtf8()
    {
        WithDirectory(directory =>
        {
            var store = new JumpSaveStore(directory);
            File.WriteAllBytes(store.SavePath, new byte[] { 0xff, 0xfe, 0xff });
            File.WriteAllText(store.SavePath + ".bak", JumpSaveCodec.Encode(Example()));
            EqualState(Example(), store.Load(out var warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Invalid UTF-8 recovery needs a warning.");
        });
    }

    private static void OversizedFile()
    {
        WithDirectory(directory =>
        {
            var store = new JumpSaveStore(directory);
            File.WriteAllText(store.SavePath, new string(' ', JumpSaveCodec.MaximumLength + 1));
            File.WriteAllText(store.SavePath + ".bak", JumpSaveCodec.Encode(Example()));
            EqualState(Example(), store.Load(out var warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Oversized primary recovery needs a warning.");
        });
    }

    private static void WriteFailures()
    {
        WithDirectory(directory =>
        {
            var store = new JumpSaveStore(directory);
            Directory.Delete(directory);
            File.WriteAllText(directory, "occupied");
            Assert(!store.TrySave(Example(), out var error), "Writing into a file path should fail.");
            Assert(!string.IsNullOrWhiteSpace(error), "Missing storage failure reason.");
        });
        WithDirectory(directory =>
        {
            var store = new JumpSaveStore(directory);
            Assert(store.TrySave(Example(), out var error), error);
            var original = File.ReadAllBytes(store.SavePath);
            Directory.CreateDirectory(store.SavePath + ".tmp");
            Assert(!store.TrySave(Example(20), out error), "Blocked temporary file should fail.");
            Assert(!string.IsNullOrWhiteSpace(error), "Missing temporary write failure.");
            EqualBytes(original, File.ReadAllBytes(store.SavePath));
            EqualState(Example(), store.Load(out var warning));
        });
        WithDirectory(directory =>
        {
            var store = new JumpSaveStore(directory);
            Assert(store.TrySave(Example(), out var error), error);
            var original = File.ReadAllBytes(store.SavePath);
            Directory.CreateDirectory(store.SavePath + ".bak");
            Assert(!store.TrySave(Example(20), out error), "Blocked atomic replacement should fail.");
            Assert(!string.IsNullOrWhiteSpace(error), "Missing replacement failure.");
            EqualBytes(original, File.ReadAllBytes(store.SavePath));
            EqualState(Example(), store.Load(out var warning));
        });
    }

    private static void InvalidWritePreservesPrimary()
    {
        WithDirectory(directory =>
        {
            var store = new JumpSaveStore(directory);
            Assert(store.TrySave(Example(), out var error), error);
            var original = File.ReadAllBytes(store.SavePath);
            var bad = Example();
            bad.Game.RandomState = 0;
            Assert(!store.TrySave(bad, out error), "Invalid state was written.");
            Assert(!string.IsNullOrWhiteSpace(error), "Invalid state needs a failure reason.");
            EqualBytes(original, File.ReadAllBytes(store.SavePath));
            EqualState(Example(), store.Load(out var warning));
        });
    }

    private static void ExistingFilesUntouched()
    {
        WithDirectory(directory =>
        {
            var sentinel = new byte[] { 0, 255, 1, 2, 8, 13, 10, 31 };
            var names = new[] { "xanaduyonder-v1.json", "xanaduyonder-v1.json.bak", "xanaduyonder-v1.json.tmp" };
            foreach (var name in names) File.WriteAllBytes(Path.Combine(directory, name), sentinel);
            var store = new JumpSaveStore(directory);
            Assert(store.TrySave(Example(), out var error), error);
            Assert(store.TrySave(Example(20), out error), error);
            EqualState(Example(20), store.Load(out var warning));
            foreach (var name in names) EqualBytes(sentinel, File.ReadAllBytes(Path.Combine(directory, name)));
        });
    }

    private static void LoadedSnapshotsIndependent()
    {
        WithDirectory(directory =>
        {
            var store = new JumpSaveStore(directory);
            Assert(store.TrySave(Example(), out var error), error);
            var first = store.Load(out var warning);
            var second = store.Load(out warning);
            Assert(!ReferenceEquals(first, second) && !ReferenceEquals(first.Game, second.Game), "Loaded snapshots share mutable objects.");
            first.Game.NextX = 100;
            first.BestScore = 0;
            EqualState(Example(), second);
            EqualState(Example(), store.Load(out warning));
        });
    }

    private static void InvalidRoot(string name, JToken value)
    {
        var root = JObject.Parse(JumpSaveCodec.Encode(Example()));
        root[name] = value.DeepClone();
        Reject(root.ToString(), "root " + name + "=" + value);
    }

    private static void InvalidGame(string name, JToken value)
    {
        var root = JObject.Parse(JumpSaveCodec.Encode(Example()));
        root["game"][name] = value.DeepClone();
        Reject(root.ToString(), "game " + name + "=" + value);
    }

    private static void Reject(string json, string label)
    {
        Assert(!JumpSaveCodec.TryDecode(json, out var data, out var reason), "Accepted " + label + ": " + json);
        Assert(data == null, "Rejected payload exposed partial state: " + label);
        Assert(!string.IsNullOrWhiteSpace(reason), "Rejected payload has no reason: " + label);
    }

    private static JumpSaveData Example(long score = 5)
    {
        return new JumpSaveData
        {
            Game = new JumpSnapshot
            {
                CurrentHalfSize = .95, NextX = 2.8, NextZ = 0, NextHalfSize = .85,
                PlayerX = 0, PlayerZ = 0, Score = score, Steps = score <= 6 ? 2 : score <= 12 ? 3 : 4, Streak = 1, IsGameOver = false, RandomState = 17
            },
            BestScore = Math.Max(score, 20)
        };
    }

    private static void EqualState(JumpSaveData expected, JumpSaveData actual)
    {
        Assert(actual != null && actual.Game != null, "Loaded save or game is null.");
        var a = expected.Game;
        var b = actual.Game;
        Assert(a.CurrentHalfSize == b.CurrentHalfSize && a.NextX == b.NextX && a.NextZ == b.NextZ && a.NextHalfSize == b.NextHalfSize,
            "Platform geometry changed.");
        Assert(a.PlayerX == b.PlayerX && a.PlayerZ == b.PlayerZ, "Player coordinates changed.");
        Assert(a.Score == b.Score && a.Steps == b.Steps && a.Streak == b.Streak, "Score or streak state changed.");
        Assert(a.IsGameOver == b.IsGameOver && a.RandomState == b.RandomState, "Terminal or RNG state changed.");
        Assert(expected.BestScore == actual.BestScore, "Best score changed.");
    }

    private static void EqualBytes(byte[] expected, byte[] actual)
    {
        Assert(expected.Length == actual.Length, "Existing file size changed.");
        for (var index = 0; index < expected.Length; index++) Assert(expected[index] == actual[index], "Existing file byte changed at " + index);
    }

    private static void WithDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "XanaduYonder-Jump-Persistence-" + Guid.NewGuid().ToString("N"));
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
        throw new InvalidOperationException("Expected ArgumentException for invalid state.");
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
