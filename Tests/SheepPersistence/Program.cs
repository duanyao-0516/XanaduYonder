using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Sheep;
using XanaduYonder.Runtime.Sheep;

internal static class Program
{
    private static int passed;
    private static int failed;

    private static int Main()
    {
        Check("Absent input returns a reason without partial state", () => Reject(null, "absent input"));
        Check("Both levels round trip with exact 64-bit completion counts", RoundTrip);
        Check("Literal schema fixture decodes without serializer defaults", KnownPayload);
        Check("Legacy challenge layouts and historical used flags still restore", LegacyChallengeState);
        Check("Slot order, reserve and tool state survive a real played game", PlayedRoundTrip);
        Check("Historical used flags preserve unlimited tool eligibility after JSON and disk restore", ReusableToolsRoundTrip);
        Check("Six reserve cards from repeated removes survive schema 1 storage", RepeatedRemoveRoundTrip);
        Check("Won tutorial and recorded challenge remain distinguishable", WinningRoundTrip);
        Check("Lost and revived games retain exact tray and tool state", LostAndRevivedRoundTrip);
        Check("Every required field rejects missing and explicit null", RequiredFields);
        Check("Strict token types reject coercion and number loss", InvalidTokens);
        Check("Malformed, duplicate, deep and trailing JSON is rejected", InvalidDocuments);
        Check("Both character and UTF-8 byte limits are enforced", OversizedPayloads);
        Check("Impossible snapshots fail encode and decode", InvalidStates);
        Check("Completion accounting cannot be forged or omitted", WinAccounting);
        Check("Missing save starts quietly with independent filename", MissingSave);
        Check("Fresh store restores primary and valid backup", BackupRecovery);
        Check("A corrupt primary cannot overwrite the valid backup", PreserveValidBackup);
        Check("Two bad files return no partial saved state", BothInvalid);
        Check("Invalid UTF-8 falls back to a valid backup", InvalidUtf8);
        Check("Oversized files fall back to a valid backup", OversizedFile);
        Check("Temporary and atomic replacement failures preserve primary", WriteFailures);
        Check("Validation failure leaves primary and backup byte-identical", InvalidWritePreservesPrimary);
        Check("2048 and Jump save files stay byte-identical", ExistingFilesUntouched);
        Check("Decoded and loaded arrays have independent ownership", LoadedSnapshotsIndependent);
        Check("Orphan temporary file is ignored", OrphanTemporary);
        Console.WriteLine("Sheep persistence checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void RoundTrip()
    {
        foreach (var level in new[] { 1, 2 })
        {
            var original = Example(level);
            original.CompletedRuns = long.MaxValue;
            original.Game.RandomState = uint.MaxValue;
            var json = SheepSaveCodec.Encode(original);
            var root = JObject.Parse(json);
            Assert((int)root["schemaVersion"] == 1 && (string)root["gameId"] == "sheep", "Wrong schema identity.");
            Assert(root["completedRuns"].Type == JTokenType.String, "64-bit count must be written as a decimal string.");
            Assert(root["game"]["kinds"] is JArray && root["game"]["locations"] is JArray, "Snapshot arrays were omitted.");
            Assert(SheepSaveCodec.TryDecode(json, out var decoded, out var reason), reason);
            EqualState(original, decoded);
            Assert(!ReferenceEquals(original.Game.Kinds, decoded.Game.Kinds), "Decoded kinds alias input.");
        }
    }

    private static void PlayedRoundTrip()
    {
        var generated = SheepLevelGenerator.Generate(2, 23);
        var game = new SheepGame(generated.Snapshot);
        Assert(game.Pick(generated.SolutionIds[0]).Changed, "Cannot pick fixture card.");
        Assert(game.UseTool(SheepTool.Undo), "Cannot undo fixture card.");
        Assert(game.Pick(generated.SolutionIds[0]).Changed, "Cannot replay fixture card.");
        Assert(game.Pick(generated.SolutionIds[1]).Changed, "Cannot pick second fixture card.");
        Assert(game.UseTool(SheepTool.RemoveThree), "Cannot move fixture cards into reserve.");
        Assert(game.UseTool(SheepTool.Shuffle), "Cannot shuffle fixture board.");
        var reserve = game.GetSnapshot().Reserve;
        Assert(reserve.Length == 2, "Fixture needs a nonempty reserve.");
        Assert(game.Pick(reserve[0]).Changed, "Cannot pick fixture reserve.");
        var original = new SheepSaveData { Game = game.GetSnapshot(), CompletedRuns = 9007199254740993L };
        Assert(original.Game.RemoveUsed && original.Game.UndoUsed && original.Game.ShuffleUsed, "Fixture tool flags missing.");
        Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(original), out var decoded, out var error), error);
        EqualState(original, decoded);
        var resumed = new SheepGame(decoded.Game);
        Assert(resumed.CanPick(resumed.GetSnapshot().Reserve[0]), "Restored reserve lost its selectable state.");
    }

    private static void KnownPayload()
    {
        const string json = "{\"schemaVersion\":1,\"gameId\":\"sheep\",\"game\":{\"level\":1,\"layoutId\":0,\"kinds\":[0,0,0,0,0,0,1,1,1,1,1,1,2,2,2,2,2,2],\"locations\":[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0],\"tray\":[],\"reserve\":[],\"randomState\":17,\"removeUsed\":false,\"undoUsed\":false,\"shuffleUsed\":false,\"reviveUsed\":false,\"lastPickedId\":-1,\"lastSource\":-1,\"lastReserveIndex\":-1},\"completedRuns\":\"9007199254740993\",\"winRecorded\":false}";
        Assert(SheepSaveCodec.TryDecode(json, out var decoded, out var error), error);
        Assert(decoded.Game.Level == 1 && decoded.Game.LayoutId == 0 && decoded.Game.RandomState == 17, "Fixture identity changed.");
        Assert(decoded.Game.Kinds.Length == 18 && decoded.Game.Kinds.Count(kind => kind == 2) == 6, "Fixture card counts changed.");
        Assert(decoded.Game.Tray.Length == 0 && decoded.Game.Reserve.Length == 0 && decoded.Game.LastPickedId == -1, "Missing fields got defaults.");
        Assert(decoded.CompletedRuns == 9007199254740993L && !decoded.WinRecorded, "Literal completion count lost precision.");
    }

    private static void ReusableToolsRoundTrip()
    {
        var generated = SheepLevelGenerator.Generate(2, 23);
        var game = new SheepGame(generated.Snapshot);
        var id = generated.SolutionIds[0];
        Assert(game.Pick(id).Changed && game.UseTool(SheepTool.Undo), "First undo fixture failed.");
        Assert(game.Pick(id).Changed && game.UseTool(SheepTool.RemoveThree), "First remove fixture failed.");
        Assert(game.UseTool(SheepTool.Shuffle), "First shuffle fixture failed.");
        Assert(game.Pick(id).Changed, "Reserve pick fixture failed.");
        var source = new SheepSaveData { Game = game.GetSnapshot(), CompletedRuns = 12 };
        Assert(source.Game.RemoveUsed && source.Game.UndoUsed && source.Game.ShuffleUsed, "Expected historical flags from real actions.");
        Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(source), out var decoded, out var error), error);
        EqualState(source, decoded);
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            Assert(store.TrySave(decoded, out error), error);
            var disk = new SheepSaveStore(directory).Load(out var warning);
            EqualState(decoded, disk);
            Assert(warning == null, warning);
            var resumed = new SheepGame(disk.Game);
            Assert(resumed.CanUseTool(SheepTool.RemoveThree) && resumed.CanUseTool(SheepTool.Undo) && resumed.CanUseTool(SheepTool.Shuffle),
                "Historical flags cannot impose quotas after restoring old schema 1 data.");
            Assert(resumed.UseTool(SheepTool.Undo) && resumed.GetSnapshot().Reserve.SequenceEqual(new[] { id }), "Reused undo did not restore reserve.");
            Assert(resumed.Pick(id).Changed && resumed.UseTool(SheepTool.RemoveThree), "Reused remove failed after restore.");
            Assert(resumed.UseTool(SheepTool.Shuffle) && resumed.UseTool(SheepTool.Shuffle), "Repeated shuffles failed after restore.");
            var next = new SheepSaveData { Game = resumed.GetSnapshot(), CompletedRuns = 12 };
            Assert(store.TrySave(next, out error), error);
            EqualState(next, new SheepSaveStore(directory).Load(out warning));
        });
    }

    private static void LegacyChallengeState()
    {
        // v0.3.0 used these three layout ids. Card 143 is the highest right blind-stack card in each.
        foreach (var layoutId in new[] { 0, 1, 2 })
        {
            var state = new SheepSnapshot
            {
                Level = 2, LayoutId = layoutId, RandomState = 17,
                Kinds = Enumerable.Range(0, 144).Select(id => id / 9).ToArray(),
                Locations = new SheepLocation[144], Tray = new[] { 143 }, Reserve = new[] { 131 },
                RemoveUsed = true, UndoUsed = true, ShuffleUsed = true,
                LastPickedId = 143, LastSource = (int)SheepLocation.Board, LastReserveIndex = -1
            };
            state.Locations[143] = SheepLocation.Tray;
            state.Locations[131] = SheepLocation.Reserve;
            var source = new SheepSaveData { Game = state, CompletedRuns = 7 };
            Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(source), out var decoded, out var error), error);
            EqualState(source, decoded);
            foreach (var tool in new[] { SheepTool.RemoveThree, SheepTool.Undo, SheepTool.Shuffle })
            {
                var game = new SheepGame(decoded.Game);
                Assert(game.CanUseTool(tool) && game.UseTool(tool), "Legacy used flag still blocks " + tool);
                Assert(game.GetSnapshot().TryValidate(out error), error);
            }
        }
    }

    private static void RepeatedRemoveRoundTrip()
    {
        var game = new SheepGame(2, 29);
        while (game.GetSnapshot().Tray.Length < 6)
        {
            var state = game.GetSnapshot();
            var id = Enumerable.Range(0, state.Kinds.Length).Where(game.CanPick)
                .OrderBy(card => state.Tray.Count(slot => state.Kinds[slot] == state.Kinds[card])).First();
            Assert(game.Pick(id).Changed && game.Phase != SheepPhase.Lost, "Could not create six-card fixture.");
        }
        Assert(game.UseTool(SheepTool.RemoveThree) && game.UseTool(SheepTool.RemoveThree), "Two move actions must be allowed.");
        var source = new SheepSaveData { Game = game.GetSnapshot() };
        Assert(source.Game.Reserve.Length == 6 && source.Game.RemoveUsed && !source.Game.ReviveUsed, "Repeated move does not need a revival flag.");
        Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(source), out var decoded, out var error), error);
        EqualState(source, decoded);
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            Assert(store.TrySave(source, out error), error);
            EqualState(source, new SheepSaveStore(directory).Load(out var warning));
            Assert(warning == null, warning);
        });
    }

    private static void LostAndRevivedRoundTrip()
    {
        SheepGame lost = null;
        for (uint seed = 1; seed <= 32 && lost == null; seed++)
        {
            var candidate = new SheepGame(2, seed);
            while (candidate.Phase == SheepPhase.Playing)
            {
                var snapshot = candidate.GetSnapshot();
                var id = Enumerable.Range(0, snapshot.Kinds.Length).Where(candidate.CanPick)
                    .OrderBy(card => snapshot.Tray.Count(slot => snapshot.Kinds[slot] == snapshot.Kinds[card])).First();
                Assert(candidate.Pick(id).Changed, "Greedy fixture pick failed.");
            }
            if (candidate.Phase == SheepPhase.Lost) lost = candidate;
        }
        Assert(lost != null, "Could not find a legal losing fixture.");
        var original = new SheepSaveData { Game = lost.GetSnapshot() };
        Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(original), out var restored, out var error), error);
        EqualState(original, restored);
        Assert(new SheepGame(restored.Game).Phase == SheepPhase.Lost, "Lost board became playable on restore.");
        Assert(lost.UseTool(SheepTool.Revive), "A first loss must allow revive.");
        original.Game = lost.GetSnapshot();
        Assert(original.Game.ReviveUsed && original.Game.Reserve.Length == 3, "Revive fixture state missing.");
        Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(original), out restored, out error), error);
        EqualState(original, restored);
        Assert(new SheepGame(restored.Game).Phase == SheepPhase.Playing, "Revived board did not stay playable.");
    }

    private static void WinningRoundTrip()
    {
        foreach (var level in new[] { 1, 2 })
        {
            var original = Won(level);
            Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(original), out var decoded, out var error), error);
            EqualState(original, decoded);
            Assert(new SheepGame(decoded.Game).Phase == SheepPhase.Won, "Restored game is not won.");
        }
    }

    private static void RequiredFields()
    {
        var original = JObject.Parse(SheepSaveCodec.Encode(Example()));
        foreach (var section in new[] { "", "game" })
        {
            var target = section.Length == 0 ? original : (JObject)original[section];
            foreach (var property in target.Properties())
            {
                var removed = (JObject)original.DeepClone();
                (section.Length == 0 ? removed : (JObject)removed[section]).Remove(property.Name);
                Reject(removed.ToString(), "missing " + section + "." + property.Name);
                var nulled = (JObject)original.DeepClone();
                (section.Length == 0 ? nulled : (JObject)nulled[section])[property.Name] = JValue.CreateNull();
                Reject(nulled.ToString(), "null " + section + "." + property.Name);
            }
        }
    }

    private static void InvalidTokens()
    {
        foreach (var value in new JToken[] { 0, 2, "1", 1.0, true, new JArray() }) InvalidRoot("schemaVersion", value);
        foreach (var value in new JToken[] { "jump", "SHEEP", "", 1, new JObject() }) InvalidRoot("gameId", value);
        foreach (var value in new JToken[] { new JArray(), "state", 1 }) InvalidRoot("game", value);
        foreach (var value in new JToken[] { 5, -1, "-1", "9223372036854775808", "1.5", "+5", " 5", "5 ", true })
            InvalidRoot("completedRuns", value);
        foreach (var name in new[] { "level", "layoutId", "lastPickedId", "lastSource", "lastReserveIndex" })
            foreach (var value in new JToken[] { "2", 2.0, true, long.MaxValue, long.MinValue, new JObject() }) InvalidGame(name, value);
        foreach (var value in new JToken[] { "2", 2.0, true, -1, 4294967296L, new JObject() }) InvalidGame("randomState", value);
        foreach (var name in new[] { "removeUsed", "undoUsed", "shuffleUsed", "reviveUsed" })
            foreach (var value in new JToken[] { "false", 0, 1, new JObject() }) InvalidGame(name, value);
        foreach (var value in new JToken[] { "false", 0, 1, new JObject() }) InvalidRoot("winRecorded", value);
        foreach (var name in new[] { "kinds", "locations", "tray", "reserve" })
        {
            foreach (var value in new JToken[] { 0, "[]", new JObject() }) InvalidGame(name, value);
            foreach (var value in new JToken[] { JValue.CreateNull(), "0", 0.0, true, long.MaxValue, new JObject(), new JArray() })
                InvalidGame(name, new JArray(value));
        }
        InvalidRoot("unexpectedField", 1);
        InvalidGame("unexpectedField", 1);
        var badLocation = JObject.Parse(SheepSaveCodec.Encode(Example()));
        badLocation["game"]["locations"][0] = 256;
        Reject(badLocation.ToString(), "undefined location enum");
    }

    private static void InvalidDocuments()
    {
        var valid = SheepSaveCodec.Encode(Example());
        foreach (var invalid in new[] { (string)null, "", " ", "{", "null", "[]", "17", "{}", valid + "{}", valid + "17", valid + " trailing",
            valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1"),
            valid.Replace("\"level\":1", "\"level\":1,\"level\":1"),
            valid.Replace("\"randomState\":", "\"randomState\":1e999,\"bad\":"),
            "{\"game\":[[[[[[[[[]]]]]]]]]}" }) Reject(invalid, "invalid document");
        Assert(SheepSaveCodec.TryDecode(valid + " \n\t", out var data, out var error), error);
        Assert(data != null, "Trailing whitespace is valid JSON.");
    }

    private static void OversizedPayloads()
    {
        Assert(SheepSaveCodec.MaximumLength == 128 * 1024, "Limit must be 128 KiB.");
        var valid = SheepSaveCodec.Encode(Example());
        Reject(valid + new string(' ', SheepSaveCodec.MaximumLength), "oversized text");
        Reject("{\"unknown\":\"" + new string('界', SheepSaveCodec.MaximumLength / 2) + "\"}", "oversized UTF-8 text");
    }

    private static void InvalidStates()
    {
        var mutations = new Action<SheepSnapshot>[]
        {
            game => game.Level = 0,
            game => game.Level = 3,
            game => game.LayoutId = -1,
            game => game.Kinds = null,
            game => game.Kinds = new int[0],
            game => game.Kinds[0] = 16,
            game => game.Locations = null,
            game => game.Locations = new SheepLocation[0],
            game => game.Locations[0] = (SheepLocation)4,
            game => game.Tray = null,
            game => game.Reserve = null,
            game => game.Tray = new[] { 0 },
            game => game.Reserve = new[] { 0 },
            game => game.RandomState = 0,
            game => game.LastPickedId = 999,
            game => game.LastSource = 1,
            game => game.LastReserveIndex = 99
        };
        foreach (var mutate in mutations)
        {
            var invalid = Example();
            mutate(invalid.Game);
            ExpectArgumentException(() => SheepSaveCodec.Encode(invalid));
        }
        ExpectArgumentException(() => SheepSaveCodec.Encode(null));
        ExpectArgumentException(() => SheepSaveCodec.Encode(new SheepSaveData()));
        InvalidGame("level", 3);
        InvalidGame("layoutId", -1);
        InvalidGame("randomState", 0);
        InvalidGame("tray", new JArray(0));
        InvalidGame("reserve", new JArray(0));
        InvalidGame("lastPickedId", 999);
        InvalidGame("lastSource", 1);
    }

    private static void WinAccounting()
    {
        var invalid = Example();
        invalid.CompletedRuns = -1;
        ExpectArgumentException(() => SheepSaveCodec.Encode(invalid));
        invalid = Example(2);
        invalid.CompletedRuns = 1;
        invalid.WinRecorded = true;
        ExpectArgumentException(() => SheepSaveCodec.Encode(invalid));
        invalid = Won(1);
        invalid.CompletedRuns = 1;
        invalid.WinRecorded = true;
        ExpectArgumentException(() => SheepSaveCodec.Encode(invalid));
        invalid = Won(2);
        invalid.WinRecorded = false;
        ExpectArgumentException(() => SheepSaveCodec.Encode(invalid));
        invalid = Won(2);
        invalid.CompletedRuns = 0;
        ExpectArgumentException(() => SheepSaveCodec.Encode(invalid));
        var won = JObject.Parse(SheepSaveCodec.Encode(Won(2)));
        won["winRecorded"] = false;
        Reject(won.ToString(), "unrecorded completed challenge");
        won["winRecorded"] = true;
        won["completedRuns"] = "0";
        Reject(won.ToString(), "recorded win with no completion");
        InvalidRoot("winRecorded", true);
    }

    private static void MissingSave()
    {
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            Assert(store.SavePath == Path.Combine(directory, "sheep-v1.json"), "Wrong independent filename.");
            Assert(store.Load(out var warning) == null && warning == null, "First launch should be quiet.");
        });
        ExpectArgumentException(() => new SheepSaveStore(null));
        ExpectArgumentException(() => new SheepSaveStore(" "));
    }

    private static void BackupRecovery()
    {
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            Assert(store.TrySave(Example(), out var error), error);
            EqualState(Example(), store.Load(out var warning));
            Assert(warning == null, warning);
            Assert(store.TrySave(Example(2), out error), error);
            EqualState(Example(2), new SheepSaveStore(directory).Load(out warning));
            Assert(warning == null, warning);
            Assert(File.Exists(store.SavePath + ".bak"), "Second save needs backup.");
            Assert(!File.Exists(store.SavePath + ".tmp"), "Successful save left temporary file.");
            File.WriteAllText(store.SavePath, "{interrupted");
            EqualState(Example(), new SheepSaveStore(directory).Load(out warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Recovery needs a warning.");
            File.Delete(store.SavePath);
            EqualState(Example(), new SheepSaveStore(directory).Load(out warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Missing primary recovery needs warning.");
        });
    }

    private static void PreserveValidBackup()
    {
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            Assert(store.TrySave(Example(), out var error), error);
            Assert(store.TrySave(Example(2), out error), error);
            File.WriteAllText(store.SavePath, "broken primary");
            var next = Example(2, 48);
            Assert(store.TrySave(next, out error), error);
            EqualState(next, store.Load(out var warning));
            File.WriteAllText(store.SavePath, "broken again");
            EqualState(Example(), store.Load(out warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Expected backup warning.");
        });
    }

    private static void BothInvalid()
    {
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            File.WriteAllText(store.SavePath, "broken");
            File.WriteAllText(store.SavePath + ".bak", "also broken");
            Assert(store.Load(out var warning) == null, "Corrupt files exposed partial data.");
            Assert(!string.IsNullOrWhiteSpace(warning), "Corrupt files need warning.");
        });
    }

    private static void InvalidUtf8()
    {
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            File.WriteAllBytes(store.SavePath, new byte[] { 0xff, 0xfe, 0xff });
            File.WriteAllText(store.SavePath + ".bak", SheepSaveCodec.Encode(Example()));
            EqualState(Example(), store.Load(out var warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Invalid UTF-8 needs warning.");
        });
    }

    private static void OversizedFile()
    {
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            File.WriteAllText(store.SavePath, new string(' ', SheepSaveCodec.MaximumLength + 1));
            File.WriteAllText(store.SavePath + ".bak", SheepSaveCodec.Encode(Example()));
            EqualState(Example(), store.Load(out var warning));
            Assert(!string.IsNullOrWhiteSpace(warning), "Oversized recovery needs warning.");
        });
    }

    private static void WriteFailures()
    {
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            Directory.Delete(directory);
            File.WriteAllText(directory, "occupied");
            Assert(!store.TrySave(Example(), out var error), "Writing into a file path should fail.");
            Assert(!string.IsNullOrWhiteSpace(error), "Missing storage reason.");
        });
        foreach (var suffix in new[] { ".tmp", ".bak" })
            WithDirectory(directory =>
            {
                var store = new SheepSaveStore(directory);
                Assert(store.TrySave(Example(), out var error), error);
                var original = File.ReadAllBytes(store.SavePath);
                Directory.CreateDirectory(store.SavePath + suffix);
                Assert(!store.TrySave(Example(2), out error), "Blocked " + suffix + " should fail.");
                Assert(!string.IsNullOrWhiteSpace(error), "Missing write failure reason.");
                EqualBytes(original, File.ReadAllBytes(store.SavePath));
                EqualState(Example(), store.Load(out var warning));
            });
    }

    private static void InvalidWritePreservesPrimary()
    {
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            Assert(store.TrySave(Example(), out var error), error);
            Assert(store.TrySave(Example(2), out error), error);
            var primary = File.ReadAllBytes(store.SavePath);
            var backup = File.ReadAllBytes(store.SavePath + ".bak");
            var invalid = Example();
            invalid.Game.RandomState = 0;
            Assert(!store.TrySave(invalid, out error), "Invalid state was written.");
            Assert(!string.IsNullOrWhiteSpace(error), "Invalid state needs error.");
            EqualBytes(primary, File.ReadAllBytes(store.SavePath));
            EqualBytes(backup, File.ReadAllBytes(store.SavePath + ".bak"));
        });
    }

    private static void ExistingFilesUntouched()
    {
        WithDirectory(directory =>
        {
            var sentinel = new byte[] { 0, 255, 1, 2, 8, 13, 10, 31 };
            foreach (var name in new[] { "xanaduyonder-v1.json", "jump-v1.json" })
                foreach (var suffix in new[] { "", ".bak", ".tmp" }) File.WriteAllBytes(Path.Combine(directory, name + suffix), sentinel);
            var store = new SheepSaveStore(directory);
            Assert(store.TrySave(Example(), out var error), error);
            Assert(store.TrySave(Example(2), out error), error);
            EqualState(Example(2), store.Load(out var warning));
            foreach (var name in new[] { "xanaduyonder-v1.json", "jump-v1.json" })
                foreach (var suffix in new[] { "", ".bak", ".tmp" }) EqualBytes(sentinel, File.ReadAllBytes(Path.Combine(directory, name + suffix)));
        });
    }

    private static void LoadedSnapshotsIndependent()
    {
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            var original = Example();
            Assert(store.TrySave(original, out var error), error);
            var first = store.Load(out var warning);
            var second = store.Load(out warning);
            Assert(!ReferenceEquals(first.Game, second.Game) && !ReferenceEquals(first.Game.Kinds, second.Game.Kinds) &&
                   !ReferenceEquals(first.Game.Locations, second.Game.Locations) && !ReferenceEquals(first.Game.Tray, second.Game.Tray) &&
                   !ReferenceEquals(first.Game.Reserve, second.Game.Reserve), "Loaded objects alias each other.");
            first.Game.Kinds[0] = 999;
            first.Game.Locations[0] = SheepLocation.Removed;
            first.CompletedRuns = 99;
            original.Game.Kinds[0] = 888;
            EqualState(Example(), second);
            EqualState(Example(), store.Load(out warning));
        });
    }

    private static void OrphanTemporary()
    {
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            File.WriteAllText(store.SavePath + ".tmp", SheepSaveCodec.Encode(Example(2)));
            Assert(store.Load(out var warning) == null && warning == null, "Temporary data must not be treated as committed.");
        });
    }

    private static SheepSaveData Example(int level = 1, uint seed = 17)
    {
        return new SheepSaveData { Game = new SheepGame(level, seed).GetSnapshot() };
    }

    private static SheepSaveData Won(int level)
    {
        var generated = SheepLevelGenerator.Generate(level, 41);
        var game = new SheepGame(generated.Snapshot);
        foreach (var id in generated.SolutionIds) Assert(game.Pick(id).Changed, "Winning fixture rejected solution card " + id);
        Assert(game.Phase == SheepPhase.Won, "Solution must win the fixture.");
        return new SheepSaveData { Game = game.GetSnapshot(), CompletedRuns = level == 2 ? 1 : 0, WinRecorded = level == 2 };
    }

    private static void EqualState(SheepSaveData expected, SheepSaveData actual)
    {
        Assert(actual != null && actual.Game != null, "Loaded save or game is null.");
        var a = expected.Game;
        var b = actual.Game;
        Assert(a.Level == b.Level && a.LayoutId == b.LayoutId && a.RandomState == b.RandomState, "Identity or RNG changed.");
        Assert(a.Kinds.SequenceEqual(b.Kinds) && a.Locations.SequenceEqual(b.Locations), "Card types or positions changed.");
        Assert(a.Tray.SequenceEqual(b.Tray) && a.Reserve.SequenceEqual(b.Reserve), "Ordered slots changed.");
        Assert(a.RemoveUsed == b.RemoveUsed && a.UndoUsed == b.UndoUsed && a.ShuffleUsed == b.ShuffleUsed && a.ReviveUsed == b.ReviveUsed, "Tool state changed.");
        Assert(a.LastPickedId == b.LastPickedId && a.LastSource == b.LastSource && a.LastReserveIndex == b.LastReserveIndex, "Undo state changed.");
        Assert(expected.CompletedRuns == actual.CompletedRuns && expected.WinRecorded == actual.WinRecorded, "Completion accounting changed.");
    }

    private static void InvalidRoot(string name, JToken value)
    {
        var root = JObject.Parse(SheepSaveCodec.Encode(Example()));
        root[name] = value.DeepClone();
        Reject(root.ToString(Formatting.None), "root " + name + "=" + value);
    }

    private static void InvalidGame(string name, JToken value)
    {
        var root = JObject.Parse(SheepSaveCodec.Encode(Example()));
        root["game"][name] = value.DeepClone();
        Reject(root.ToString(Formatting.None), "game " + name + "=" + value);
    }

    private static void Reject(string json, string label)
    {
        Assert(!SheepSaveCodec.TryDecode(json, out var data, out var reason), "Accepted " + label);
        Assert(data == null, "Rejected payload exposed partial state: " + label);
        Assert(!string.IsNullOrWhiteSpace(reason), "Rejected payload has no reason: " + label);
    }

    private static void EqualBytes(byte[] expected, byte[] actual)
    {
        Assert(expected.SequenceEqual(actual), "Existing file bytes changed.");
    }

    private static void WithDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "XanaduYonder-Sheep-Persistence-" + Guid.NewGuid().ToString("N"));
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
