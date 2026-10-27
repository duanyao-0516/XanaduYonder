using System;
using System.IO;
using System.Linq;
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
        Check("Schema 2 accepts independent empty mode slots", EmptyModeSlots);
        Check("Each mode and difficulty round trips with exact records", RoundTrip);
        Check("Large endless scores and custom quota boundaries remain exact", LargeScoreAndQuotaBoundaries);
        Check("Active mode only selects its own resumable slot", ModeIndependence);
        Check("Preferences are copied and validated independently of run quotas", PreferenceContract);
        Check("Played tool quotas and ordered reserve survive storage", PlayedRoundTrip);
        Check("Endless wave and whole-run quotas survive storage", EndlessWaveRoundTrip);
        Check("Challenge and tutorial terminal states remain distinguishable", WinningRoundTrip);
        Check("Lost and revived games retain exact tray and revival state", LostAndRevivedRoundTrip);
        Check("Every required field rejects missing and unexpected null", RequiredFields);
        Check("Strict token types reject coercion and number loss", InvalidTokens);
        Check("Malformed, duplicate, deep and trailing JSON is rejected", InvalidDocuments);
        Check("Both character and UTF-8 byte limits are enforced", OversizedPayloads);
        Check("Impossible snapshots fail encode and decode", InvalidStates);
        Check("Challenge accounting is tied to its slot, not active mode", WinAccounting);
        Check("Only standard endless quotas require a ranked score", EndlessAccounting);
        Check("Schema 1 is rejected and never loaded as schema 2", LegacyRejected);
        Check("Missing save starts quietly with independent filename", MissingSave);
        Check("Fresh store restores primary and valid backup", BackupRecovery);
        Check("A corrupt primary cannot overwrite the valid backup", PreserveValidBackup);
        Check("Two bad files return no partial saved state", BothInvalid);
        Check("Invalid UTF-8 falls back to a valid backup", InvalidUtf8);
        Check("Oversized files fall back to a valid backup", OversizedFile);
        Check("Temporary and atomic replacement failures preserve primary", WriteFailures);
        Check("Validation failure leaves primary and backup byte-identical", InvalidWritePreservesPrimary);
        Check("Other games and legacy sheep files stay byte-identical", ExistingFilesUntouched);
        Check("Decoded and loaded arrays have independent ownership", LoadedSnapshotsIndependent);
        Check("Orphan temporary file is ignored", OrphanTemporary);
        Console.WriteLine("Sheep persistence checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void EmptyModeSlots()
    {
        const string json = "{\"schemaVersion\":2,\"gameId\":\"sheep\",\"challenge\":null,\"endless\":null,\"tutorial\":null,\"activeMode\":0,\"preferences\":{\"defaultDifficulty\":1,\"skipDifficultyPrompt\":false,\"removeLimit\":2,\"undoLimit\":3,\"shuffleLimit\":1},\"completedRuns\":\"0\",\"endlessBest\":\"0\",\"winRecorded\":false}";
        Assert(SheepSaveCodec.TryDecode(json, out var decoded, out var error), error);
        Assert(decoded.Game == null && decoded.Challenge == null && decoded.Endless == null && decoded.Tutorial == null,
            "Unstarted mode slots must remain null.");
        Assert(JToken.DeepEquals(JObject.Parse(json), JObject.Parse(SheepSaveCodec.Encode(decoded))), "Empty schema did not round trip.");
        EqualState(new SheepSaveData(), decoded);
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            Assert(store.TrySave(decoded, out error), error);
            EqualState(decoded, store.Load(out var warning));
            Assert(warning == null, warning);
        });
    }

    private static void RoundTrip()
    {
        foreach (SheepMode mode in Enum.GetValues(typeof(SheepMode)))
        foreach (SheepDifficulty difficulty in Enum.GetValues(typeof(SheepDifficulty)))
        {
            if (mode != SheepMode.Challenge && difficulty != SheepDifficulty.Normal) continue;
            var original = new SheepSaveData { ActiveMode = mode, CompletedRuns = long.MaxValue, EndlessBest = long.MaxValue };
            original.Game = SheepLevelGenerator.Generate(mode, difficulty, 23).Snapshot;
            original.Game.RandomState = uint.MaxValue;
            var json = SheepSaveCodec.Encode(original);
            var root = JObject.Parse(json);
            Assert((int)root["schemaVersion"] == 2 && (string)root["gameId"] == "sheep", "Wrong schema identity.");
            Assert(root["completedRuns"].Type == JTokenType.String && root["endlessBest"].Type == JTokenType.String,
                "64-bit records must use decimal strings.");
            var game = (JObject)root[mode.ToString().ToLowerInvariant()];
            Assert(game["score"].Type == JTokenType.String, "Score must use a decimal string.");
            Assert(game.Property("removeUsed") == null && game.Property("undoUsed") == null && game.Property("shuffleUsed") == null,
                "Historical boolean tools leaked into schema 2.");
            Assert(SheepSaveCodec.TryDecode(json, out var decoded, out var reason), reason);
            EqualState(original, decoded);
            Assert(!ReferenceEquals(original.Game.Kinds, decoded.Game.Kinds), "Decoded kinds alias input.");
        }
    }

    private static void LargeScoreAndQuotaBoundaries()
    {
        foreach (var limit in new[] { 0, 99 })
        {
            var snapshot = SheepLevelGenerator.Generate(SheepMode.Endless, SheepDifficulty.Normal, 17, limit, limit, limit).Snapshot;
            snapshot.Wave = int.MaxValue;
            snapshot.Score = ((long)int.MaxValue - 1) * 144;
            var original = new SheepSaveData { ActiveMode = SheepMode.Endless, Endless = snapshot, EndlessBest = 9007199254740993L };
            Assert(snapshot.Score > int.MaxValue, "Fixture does not exercise 64-bit score.");
            Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(original), out var decoded, out var reason), reason);
            EqualState(original, decoded);
            Assert(decoded.Game.Score == 309237645024L && decoded.EndlessBest == 9007199254740993L,
                "Large record or score lost precision.");
        }
    }

    private static void ModeIndependence()
    {
        var original = AllModes();
        foreach (var mode in new[] { SheepMode.Challenge, SheepMode.Endless, SheepMode.Tutorial })
        {
            original.ActiveMode = mode;
            var selected = original.Game;
            var replacement = new SheepGame(selected).GetSnapshot();
            replacement.RandomState = 91;
            original.Game = replacement;
            Assert(ReferenceEquals(original.Game, replacement), "Game setter did not update selected mode.");
            Assert(original.Challenge.Mode == SheepMode.Challenge && original.Endless.Mode == SheepMode.Endless &&
                   original.Tutorial.Mode == SheepMode.Tutorial, "Switching slots overwrote another mode.");
            Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(original), out var decoded, out var reason), reason);
            EqualState(original, decoded);
        }
        original.ActiveMode = SheepMode.Challenge;
        original.Game = null;
        Assert(original.Challenge == null && original.Endless != null && original.Tutorial != null, "Clearing one slot cleared other modes.");
        Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(original), out var emptyActive, out var error), error);
        Assert(emptyActive.Game == null, "A nullable active slot must remain unstarted.");
    }

    private static void PreferenceContract()
    {
        var preferences = new SheepPreferences();
        Assert(preferences.DefaultDifficulty == SheepDifficulty.Normal && !preferences.SkipDifficultyPrompt &&
               preferences.RemoveLimit == 2 && preferences.UndoLimit == 3 && preferences.ShuffleLimit == 1 && preferences.IsStandardTools,
            "Wrong preference defaults.");
        var copy = preferences.Copy();
        copy.DefaultDifficulty = SheepDifficulty.Extreme;
        copy.SkipDifficultyPrompt = true;
        copy.RemoveLimit = 0;
        copy.UndoLimit = 99;
        copy.ShuffleLimit = 8;
        Assert(copy.TryValidate(out var reason) && !copy.IsStandardTools, reason);
        Assert(preferences.IsStandardTools && preferences.DefaultDifficulty == SheepDifficulty.Normal && !preferences.SkipDifficultyPrompt,
            "Preference copy aliases the original.");
        var data = AllModes();
        data.Preferences = copy;
        Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(data), out var decoded, out reason), reason);
        EqualState(data, decoded);
        Assert(decoded.Challenge.RemoveLimit == 2 && decoded.Endless.UndoLimit == 3 && decoded.Tutorial.ShuffleLimit == 1,
            "Preferences overwrote frozen run quotas.");
        foreach (var mutate in new Action<SheepPreferences>[] {
            value => value.DefaultDifficulty = (SheepDifficulty)(-1), value => value.DefaultDifficulty = (SheepDifficulty)4,
            value => value.RemoveLimit = -1, value => value.RemoveLimit = 100,
            value => value.UndoLimit = -1, value => value.UndoLimit = 100,
            value => value.ShuffleLimit = -1, value => value.ShuffleLimit = 100 })
        {
            var invalid = new SheepPreferences(); mutate(invalid);
            Assert(!invalid.TryValidate(out reason) && !string.IsNullOrWhiteSpace(reason), "Invalid preferences accepted.");
            ExpectArgumentException(() => SheepSaveCodec.Encode(new SheepSaveData { Preferences = invalid }));
        }
        ExpectArgumentException(() => SheepSaveCodec.Encode(new SheepSaveData { Preferences = null }));
    }

    private static void PlayedRoundTrip()
    {
        var generated = SheepLevelGenerator.Generate(SheepMode.Challenge, SheepDifficulty.Hard, 23);
        var game = new SheepGame(generated.Snapshot);
        Assert(game.Pick(generated.SolutionIds[0]).Changed && game.UseTool(SheepTool.Undo), "Cannot undo fixture card.");
        Assert(game.Pick(generated.SolutionIds[0]).Changed && game.Pick(generated.SolutionIds[1]).Changed, "Cannot replay fixture cards.");
        Assert(game.UseTool(SheepTool.RemoveThree) && game.UseTool(SheepTool.Shuffle), "Cannot use fixture tools.");
        var reserve = game.GetSnapshot().Reserve;
        Assert(reserve.Length == 2 && game.Pick(reserve[0]).Changed, "Cannot pick fixture reserve.");
        var original = new SheepSaveData { Game = game.GetSnapshot(), CompletedRuns = 9007199254740993L };
        Assert(original.Game.RemoveRemaining == 1 && original.Game.UndoRemaining == 2 && original.Game.ShuffleRemaining == 0,
            "Real actions did not consume quotas.");
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            Assert(store.TrySave(original, out var error), error);
            var decoded = store.Load(out var warning);
            EqualState(original, decoded);
            var resumed = new SheepGame(decoded.Game);
            Assert(resumed.CanUseTool(SheepTool.Undo) && resumed.UseTool(SheepTool.Undo), "Reserve undo lost across storage.");
            Assert(!resumed.CanUseTool(SheepTool.Shuffle), "Exhausted shuffle reset after restore.");
            Assert(warning == null, warning);
        });
    }

    private static void EndlessWaveRoundTrip()
    {
        var generated = SheepLevelGenerator.Generate(SheepMode.Endless, SheepDifficulty.Normal, 23);
        var game = new SheepGame(generated.Snapshot);
        var first = generated.SolutionIds[0];
        Assert(game.Pick(first).Changed && game.UseTool(SheepTool.Undo), "Cannot consume undo.");
        Assert(game.Pick(first).Changed && game.UseTool(SheepTool.RemoveThree), "Cannot consume remove.");
        foreach (var id in generated.SolutionIds) Assert(game.Pick(id).Changed, "Endless witness was rejected.");
        Assert(game.AdvanceWave(), "A completed endless wave must advance.");
        Assert(game.UseTool(SheepTool.Shuffle), "Cannot consume wave-two shuffle.");
        var snapshot = game.GetSnapshot();
        Assert(snapshot.Wave == 2 && snapshot.Score == 144 && snapshot.RemoveRemaining == 1 && snapshot.UndoRemaining == 2 &&
               snapshot.ShuffleRemaining == 0, "Whole-run quota or score reset at next wave.");
        var original = new SheepSaveData { ActiveMode = SheepMode.Endless, Endless = snapshot, EndlessBest = 144 };
        Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(original), out var decoded, out var error), error);
        EqualState(original, decoded);
        var resumed = new SheepGame(decoded.Endless);
        Assert(resumed.GetSnapshot().Wave == 2 && !resumed.CanUseTool(SheepTool.Shuffle), "Endless run reset on resume.");
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

    private static void LostAndRevivedRoundTrip()
    {
        SheepGame lost = null;
        for (uint seed = 1; seed <= 32 && lost == null; seed++)
        {
            var candidate = new SheepGame(SheepLevelGenerator.Generate(SheepMode.Endless, SheepDifficulty.Normal, seed).Snapshot);
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
        var original = new SheepSaveData { ActiveMode = SheepMode.Endless, Endless = lost.GetSnapshot(), EndlessBest = lost.GetSnapshot().Score };
        Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(original), out var restored, out var error), error);
        EqualState(original, restored);
        Assert(new SheepGame(restored.Game).Phase == SheepPhase.Lost, "Lost board became playable on restore.");
        Assert(lost.UseTool(SheepTool.Revive), "A first loss must allow revive.");
        original.Game = lost.GetSnapshot();
        Assert(original.Game.ReviveUsed && original.Game.Reserve.Length == 3, "Revive fixture state missing.");
        Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(original), out restored, out error), error);
        EqualState(original, restored);
        Assert(new SheepGame(restored.Game).Phase == SheepPhase.Playing && !new SheepGame(restored.Game).CanUseTool(SheepTool.Revive),
            "Revived run did not retain revival state.");
    }

    private static void RequiredFields()
    {
        var original = JObject.Parse(SheepSaveCodec.Encode(AllModes()));
        foreach (var section in new[] { "", "challenge", "endless", "tutorial", "preferences" })
        {
            var target = section.Length == 0 ? original : (JObject)original[section];
            foreach (var property in target.Properties())
            {
                var removed = (JObject)original.DeepClone();
                (section.Length == 0 ? removed : (JObject)removed[section]).Remove(property.Name);
                Reject(removed.ToString(), "missing " + section + "." + property.Name);
                if (section.Length == 0 && new[] { "challenge", "endless", "tutorial" }.Contains(property.Name)) continue;
                var nulled = (JObject)original.DeepClone();
                (section.Length == 0 ? nulled : (JObject)nulled[section])[property.Name] = JValue.CreateNull();
                Reject(nulled.ToString(), "null " + section + "." + property.Name);
            }
        }
    }

    private static void InvalidTokens()
    {
        foreach (var value in new JToken[] { 0, 1, 3, "2", 2.0, true, new JArray() }) InvalidRoot("schemaVersion", value);
        foreach (var value in new JToken[] { "jump", "SHEEP", "", 1, new JObject() }) InvalidRoot("gameId", value);
        foreach (var name in new[] { "challenge", "endless", "tutorial", "preferences" })
            foreach (var value in new JToken[] { new JArray(), "state", 1, false }) InvalidRoot(name, value);
        foreach (var name in new[] { "completedRuns", "endlessBest" })
            foreach (var value in InvalidLongTokens()) InvalidRoot(name, value);
        foreach (var value in new JToken[] { -1, 3, "0", 0.0, false, long.MaxValue }) InvalidRoot("activeMode", value);
        foreach (var name in new[] { "mode", "difficulty", "wave", "level", "layoutId", "lastPickedId", "lastSource", "lastReserveIndex",
                                     "removeLimit", "undoLimit", "shuffleLimit", "removeRemaining", "undoRemaining", "shuffleRemaining" })
            foreach (var value in new JToken[] { "2", 2.0, true, long.MaxValue, long.MinValue, new JObject() }) InvalidGame(name, value);
        foreach (var value in new JToken[] { "2", 2.0, true, -1, 4294967296L, new JObject() }) InvalidGame("randomState", value);
        foreach (var value in InvalidLongTokens()) InvalidGame("score", value);
        foreach (var value in new JToken[] { "false", 0, 1, new JObject() })
        {
            InvalidGame("reviveUsed", value); InvalidRoot("winRecorded", value); InvalidPreference("skipDifficultyPrompt", value);
        }
        foreach (var name in new[] { "defaultDifficulty", "removeLimit", "undoLimit", "shuffleLimit" })
            foreach (var value in new JToken[] { "2", 2.0, true, -1, 100, long.MaxValue, new JObject() }) InvalidPreference(name, value);
        foreach (var name in new[] { "kinds", "locations", "tray", "reserve" })
        {
            foreach (var value in new JToken[] { 0, "[]", new JObject() }) InvalidGame(name, value);
            foreach (var value in new JToken[] { JValue.CreateNull(), "0", 0.0, true, long.MaxValue, new JObject(), new JArray() })
                InvalidGame(name, new JArray(value));
            InvalidGame(name, new JArray(Enumerable.Repeat(0, 145)));
        }
        InvalidRoot("unexpectedField", 1); InvalidGame("unexpectedField", 1); InvalidPreference("unexpectedField", 1);
        var badLocation = JObject.Parse(SheepSaveCodec.Encode(Example()));
        badLocation["tutorial"]["locations"][0] = 256;
        Reject(badLocation.ToString(), "undefined location enum");
    }

    private static JToken[] InvalidLongTokens()
    {
        return new JToken[] { 5, -1, "-1", "9223372036854775808", "1.5", "+5", " 5", "5 ", true, new JObject() };
    }

    private static void InvalidDocuments()
    {
        var valid = SheepSaveCodec.Encode(Example());
        foreach (var invalid in new[] { (string)null, "", " ", "{", "null", "[]", "17", "{}", valid + "{}", valid + "17", valid + " trailing",
            valid.Replace("\"schemaVersion\":2", "\"schemaVersion\":2,\"schemaVersion\":2"),
            valid.Replace("\"level\":1", "\"level\":1,\"level\":1"),
            valid.Replace("\"removeLimit\":2", "\"removeLimit\":2,\"removeLimit\":2"),
            valid.Replace("\"randomState\":", "\"randomState\":1e999,\"bad\":"),
            "{\"tutorial\":[[[[[[[[[]]]]]]]]]}" }) Reject(invalid, "invalid document");
        Assert(SheepSaveCodec.TryDecode(valid + " \n\t", out var data, out var error), error);
        Assert(data != null, "Trailing whitespace is valid JSON.");
    }

    private static void OversizedPayloads()
    {
        Assert(SheepSaveCodec.MaximumLength == 128 * 1024, "Limit must be 128 KiB.");
        var valid = SheepSaveCodec.Encode(AllModes());
        Reject(valid + new string(' ', SheepSaveCodec.MaximumLength), "oversized text");
        Reject("{\"unknown\":\"" + new string('界', SheepSaveCodec.MaximumLength / 2) + "\"}", "oversized UTF-8 text");
    }

    private static void InvalidStates()
    {
        var mutations = new Action<SheepSnapshot>[]
        {
            game => game.Mode = (SheepMode)3, game => game.Difficulty = (SheepDifficulty)4,
            game => game.Level = 0, game => game.Level = 3, game => game.LayoutId = -1,
            game => game.Wave = 0, game => game.Wave = 2, game => game.Score = -1, game => game.Score = 3,
            game => game.RemoveLimit = 100, game => game.UndoLimit = -1, game => game.ShuffleLimit = 0,
            game => game.RemoveRemaining = 3, game => game.UndoRemaining = -1, game => game.ShuffleRemaining = 2,
            game => game.Kinds = null, game => game.Kinds = new int[0], game => game.Kinds[0] = 16,
            game => game.Locations = null, game => game.Locations = new SheepLocation[0], game => game.Locations[0] = (SheepLocation)4,
            game => game.Tray = null, game => game.Reserve = null, game => game.Tray = new[] { 0 }, game => game.Reserve = new[] { 0 },
            game => game.RandomState = 0, game => game.LastPickedId = 999, game => game.LastSource = 1, game => game.LastReserveIndex = 99
        };
        foreach (var mutate in mutations)
        {
            var invalid = Example(); mutate(invalid.Game);
            ExpectArgumentException(() => SheepSaveCodec.Encode(invalid));
        }
        ExpectArgumentException(() => SheepSaveCodec.Encode(null));
        ExpectArgumentException(() => SheepSaveCodec.Encode(new SheepSaveData { ActiveMode = (SheepMode)3 }));
        var wrongSlot = new SheepSaveData { Challenge = Example().Tutorial };
        ExpectArgumentException(() => SheepSaveCodec.Encode(wrongSlot));
        foreach (var name in new[] { "mode", "difficulty", "level" }) InvalidGame(name, 3);
        InvalidGame("layoutId", -1); InvalidGame("randomState", 0); InvalidGame("tray", new JArray(0));
        InvalidGame("reserve", new JArray(0)); InvalidGame("lastPickedId", 999); InvalidGame("lastSource", 1);
        InvalidGame("score", "3"); InvalidGame("wave", 2); InvalidGame("removeRemaining", 3);
        var inactiveBad = JObject.Parse(SheepSaveCodec.Encode(AllModes()));
        inactiveBad["endless"]["score"] = "1";
        Reject(inactiveBad.ToString(), "invalid inactive slot must invalidate whole save");
    }

    private static void WinAccounting()
    {
        ExpectArgumentException(() => SheepSaveCodec.Encode(new SheepSaveData { CompletedRuns = -1 }));
        foreach (var invalid in new[] {
            new SheepSaveData { WinRecorded = true, CompletedRuns = 1 },
            new SheepSaveData { Challenge = Example(2).Challenge, WinRecorded = true, CompletedRuns = 1 },
            new SheepSaveData { Tutorial = Won(1).Tutorial, WinRecorded = true, CompletedRuns = 1 },
            new SheepSaveData { Challenge = Won(2).Challenge, CompletedRuns = 1 },
            new SheepSaveData { Challenge = Won(2).Challenge, WinRecorded = true } })
            ExpectArgumentException(() => SheepSaveCodec.Encode(invalid));
        var original = Won(2);
        original.Endless = SheepLevelGenerator.Generate(SheepMode.Endless, SheepDifficulty.Normal, 17).Snapshot;
        original.ActiveMode = SheepMode.Endless;
        for (var index = 0; index < 3; index++)
        {
            Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(original), out var decoded, out var reason), reason);
            EqualState(original, decoded);
            Assert(decoded.CompletedRuns == 1 && decoded.WinRecorded, "A challenge win must not count again during mode switching.");
            original = decoded;
        }
        var won = JObject.Parse(SheepSaveCodec.Encode(original));
        won["winRecorded"] = false; Reject(won.ToString(), "unrecorded completed challenge");
        won["winRecorded"] = true; won["completedRuns"] = "0"; Reject(won.ToString(), "recorded win with no completion");
        InvalidRoot("winRecorded", true);
    }

    private static void EndlessAccounting()
    {
        ExpectArgumentException(() => SheepSaveCodec.Encode(new SheepSaveData { EndlessBest = -1 }));
        var generated = SheepLevelGenerator.Generate(SheepMode.Endless, SheepDifficulty.Normal, 17);
        var game = new SheepGame(generated.Snapshot);
        foreach (var id in generated.SolutionIds)
        {
            Assert(game.Pick(id).Changed, "Could not create scored endless run.");
            if (game.ClearedCount >= 3) break;
        }
        var data = new SheepSaveData { Endless = game.GetSnapshot() };
        ExpectArgumentException(() => SheepSaveCodec.Encode(data));
        data.EndlessBest = data.Endless.Score;
        Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(data), out var decoded, out var reason), reason);
        EqualState(data, decoded);
        var root = JObject.Parse(SheepSaveCodec.Encode(data)); root["endlessBest"] = "0";
        Reject(root.ToString(), "best below ranked score");
        data.Endless.RemoveLimit = 99; data.Endless.RemoveRemaining = 99; data.EndlessBest = 0;
        Assert(SheepSaveCodec.TryDecode(SheepSaveCodec.Encode(data), out decoded, out reason), reason);
        Assert(decoded.EndlessBest == 0 && decoded.Endless.Score > 0, "A custom run must not force a standard high score.");
        data.Preferences.RemoveLimit = 99;
        data.Endless = game.GetSnapshot();
        ExpectArgumentException(() => SheepSaveCodec.Encode(data));
    }

    private static void LegacyRejected()
    {
        const string legacy = "{\"schemaVersion\":1,\"gameId\":\"sheep\",\"game\":{\"level\":1,\"layoutId\":0,\"kinds\":[0,0,0,0,0,0,1,1,1,1,1,1,2,2,2,2,2,2],\"locations\":[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0],\"tray\":[],\"reserve\":[],\"randomState\":17,\"removeUsed\":false,\"undoUsed\":false,\"shuffleUsed\":false,\"reviveUsed\":false,\"lastPickedId\":-1,\"lastSource\":-1,\"lastReserveIndex\":-1},\"completedRuns\":\"0\",\"winRecorded\":false}";
        Reject(legacy, "legacy schema");
        WithDirectory(directory =>
        {
            var path = Path.Combine(directory, "sheep-v1.json"); File.WriteAllText(path, legacy);
            var store = new SheepSaveStore(directory);
            Assert(store.Load(out var warning) == null && warning == null, "Legacy save was migrated or treated as corrupt v2.");
            Assert(store.TrySave(new SheepSaveData(), out var error), error);
            Assert(File.ReadAllText(path) == legacy, "Legacy bytes changed.");
        });
    }

    private static void MissingSave()
    {
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            Assert(store.SavePath == Path.Combine(directory, "sheep-v2.json"), "Wrong independent filename.");
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
            foreach (var name in new[] { "xanaduyonder-v1.json", "jump-v1.json", "idiom-v1.json", "idiom-notebook-v1.json", "sheep-v1.json" })
                foreach (var suffix in new[] { "", ".bak", ".tmp" }) File.WriteAllBytes(Path.Combine(directory, name + suffix), sentinel);
            var store = new SheepSaveStore(directory);
            Assert(store.TrySave(Example(), out var error), error);
            Assert(store.TrySave(Example(2), out error), error);
            EqualState(Example(2), store.Load(out var warning));
            foreach (var name in new[] { "xanaduyonder-v1.json", "jump-v1.json", "idiom-v1.json", "idiom-notebook-v1.json", "sheep-v1.json" })
                foreach (var suffix in new[] { "", ".bak", ".tmp" }) EqualBytes(sentinel, File.ReadAllBytes(Path.Combine(directory, name + suffix)));
        });
    }

    private static void LoadedSnapshotsIndependent()
    {
        WithDirectory(directory =>
        {
            var store = new SheepSaveStore(directory);
            var original = AllModes();
            Assert(store.TrySave(original, out var error), error);
            var first = store.Load(out var warning);
            var second = store.Load(out warning);
            foreach (var mode in new[] { SheepMode.Challenge, SheepMode.Endless, SheepMode.Tutorial })
            {
                first.ActiveMode = mode; second.ActiveMode = mode;
                Assert(!ReferenceEquals(first.Game, second.Game) && !ReferenceEquals(first.Game.Kinds, second.Game.Kinds) &&
                       !ReferenceEquals(first.Game.Locations, second.Game.Locations) && !ReferenceEquals(first.Game.Tray, second.Game.Tray) &&
                       !ReferenceEquals(first.Game.Reserve, second.Game.Reserve), "Loaded mode objects alias each other.");
                first.Game.Kinds[0] = 999;
                first.Game.Locations[0] = SheepLocation.Removed;
            }
            Assert(!ReferenceEquals(first.Preferences, second.Preferences), "Loaded preferences alias each other.");
            first.Preferences.RemoveLimit = 99;
            first.CompletedRuns = 99;
            original.Challenge.Kinds[0] = 888;
            second.ActiveMode = SheepMode.Challenge;
            EqualState(AllModes(), second);
            EqualState(AllModes(), store.Load(out warning));
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
        var mode = level == 1 ? SheepMode.Tutorial : SheepMode.Challenge;
        var data = new SheepSaveData { ActiveMode = mode };
        data.Game = SheepLevelGenerator.Generate(mode, SheepDifficulty.Normal, seed).Snapshot;
        return data;
    }

    private static SheepSaveData AllModes()
    {
        return new SheepSaveData {
            Challenge = SheepLevelGenerator.Generate(SheepMode.Challenge, SheepDifficulty.Extreme, 17).Snapshot,
            Endless = SheepLevelGenerator.Generate(SheepMode.Endless, SheepDifficulty.Normal, 23).Snapshot,
            Tutorial = SheepLevelGenerator.Generate(SheepMode.Tutorial, SheepDifficulty.Normal, 29).Snapshot
        };
    }

    private static SheepSaveData Won(int level)
    {
        var mode = level == 1 ? SheepMode.Tutorial : SheepMode.Challenge;
        var generated = SheepLevelGenerator.Generate(mode, SheepDifficulty.Normal, 41);
        var game = new SheepGame(generated.Snapshot);
        foreach (var id in generated.SolutionIds) Assert(game.Pick(id).Changed, "Winning fixture rejected solution card " + id);
        Assert(game.Phase == SheepPhase.Won, "Solution must win the fixture.");
        var data = new SheepSaveData { ActiveMode = mode, CompletedRuns = level == 2 ? 1 : 0, WinRecorded = level == 2 };
        data.Game = game.GetSnapshot();
        return data;
    }

    private static void EqualState(SheepSaveData expected, SheepSaveData actual)
    {
        Assert(actual != null, "Loaded save is null.");
        Assert(expected.ActiveMode == actual.ActiveMode && expected.CompletedRuns == actual.CompletedRuns &&
               expected.EndlessBest == actual.EndlessBest && expected.WinRecorded == actual.WinRecorded, "Mode or records changed.");
        var p = expected.Preferences; var q = actual.Preferences;
        Assert(q != null && p.DefaultDifficulty == q.DefaultDifficulty && p.SkipDifficultyPrompt == q.SkipDifficultyPrompt &&
               p.RemoveLimit == q.RemoveLimit && p.UndoLimit == q.UndoLimit && p.ShuffleLimit == q.ShuffleLimit, "Preferences changed.");
        EqualSnapshot(expected.Challenge, actual.Challenge);
        EqualSnapshot(expected.Endless, actual.Endless);
        EqualSnapshot(expected.Tutorial, actual.Tutorial);
    }

    private static void EqualSnapshot(SheepSnapshot expected, SheepSnapshot actual)
    {
        if (expected == null) { Assert(actual == null, "An unstarted slot became a game."); return; }
        Assert(actual != null, "Saved slot disappeared.");
        Assert(expected.Mode == actual.Mode && expected.Difficulty == actual.Difficulty && expected.Level == actual.Level &&
               expected.LayoutId == actual.LayoutId && expected.Wave == actual.Wave && expected.Score == actual.Score &&
               expected.RandomState == actual.RandomState, "Run identity, progress or RNG changed.");
        Assert(expected.Kinds.SequenceEqual(actual.Kinds) && expected.Locations.SequenceEqual(actual.Locations) &&
               expected.Tray.SequenceEqual(actual.Tray) && expected.Reserve.SequenceEqual(actual.Reserve), "Card state or ordered slots changed.");
        Assert(expected.RemoveLimit == actual.RemoveLimit && expected.UndoLimit == actual.UndoLimit && expected.ShuffleLimit == actual.ShuffleLimit &&
               expected.RemoveRemaining == actual.RemoveRemaining && expected.UndoRemaining == actual.UndoRemaining &&
               expected.ShuffleRemaining == actual.ShuffleRemaining && expected.ReviveUsed == actual.ReviveUsed, "Frozen run quotas or tool state changed.");
        Assert(expected.LastPickedId == actual.LastPickedId && expected.LastSource == actual.LastSource &&
               expected.LastReserveIndex == actual.LastReserveIndex, "Undo state changed.");
    }

    private static void InvalidRoot(string name, JToken value)
    {
        var root = JObject.Parse(SheepSaveCodec.Encode(Example())); root[name] = value.DeepClone();
        Reject(root.ToString(Formatting.None), "root " + name + "=" + value);
    }

    private static void InvalidGame(string name, JToken value)
    {
        var root = JObject.Parse(SheepSaveCodec.Encode(Example())); root["tutorial"][name] = value.DeepClone();
        Reject(root.ToString(Formatting.None), "tutorial " + name + "=" + value);
    }

    private static void InvalidPreference(string name, JToken value)
    {
        var root = JObject.Parse(SheepSaveCodec.Encode(Example())); root["preferences"][name] = value.DeepClone();
        Reject(root.ToString(Formatting.None), "preferences " + name + "=" + value);
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
