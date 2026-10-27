using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Sudoku;
using XanaduYonder.Runtime.Sudoku;

internal static class Program
{
    private static int passed;
    private static int failed;

    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--fresh-load")
        {
            var data = new SudokuSaveStore(args[1]).Load(out var warning);
            if (data == null) return 2;
            Console.WriteLine(SudokuSaveCodec.Encode(data));
            return 0;
        }
        Check("Absent input returns a reason without partial state", () => Reject(null, "absent input"));
        Check("Played board, notes, full undo history and UI state round trip", RoundTrip);
        Check("Literal schema decodes without serializer defaults", LiteralPayload);
        Check("Wrong values are valid and third mistake never revives", WrongAndLost);
        Check("Restored undo recovers peer notes without refunding cumulative counters", RestoredUndo);
        Check("Paid help with zero quota can be restored without another charge", PaidHint);
        Check("Every required property rejects omission and null", RequiredFields);
        Check("Strict token types reject coercion, null entries and overflow", StrictTokens);
        Check("Unknown fields and invalid array lengths are rejected", ArrayAndUnknownFields);
        Check("Malformed, repeated, deep and trailing JSON is rejected", InvalidDocuments);
        Check("UTF-8 bytes and text length are bounded", OversizedPayloads);
        Check("Impossible state is rejected before serialization", InvalidStates);
        Check("Forged undo history cannot change givens, counts or shape", HistoryTampering);
        Check("Hint quota, revision, target, answer and evidence stay consistent", HintValidation);
        Check("Completed records are unique and unassisted records are a subset", CompletionRecords);
        Check("Terminal completion is recorded in the same snapshot", TerminalAccounting);
        Check("Copies never alias nested arrays or saved hints", CopyOwnership);
        Check("Missing save is quiet and filename is independent", MissingSave);
        Check("Fresh process reads primary and recovered backup", FreshProcess);
        Check("Second atomic save creates a real usable backup", BackupRecovery);
        Check("Corrupt primary never overwrites the last valid backup", PreserveValidBackup);
        Check("Two invalid files return no partial state with warning", BothInvalid);
        Check("Invalid UTF-8 and oversized files recover from backup", InvalidFiles);
        Check("Temporary and replacement failures retain primary bytes", WriteFailures);
        Check("Invalid state leaves primary, backup and existing temporary bytes intact", InvalidWrite);
        Check("Success and failure leave all three existing games byte-identical", ExistingGamesUntouched);
        Check("Loaded instances own independent data", LoadedOwnership);
        Check("Orphan temporary file is never treated as committed", OrphanTemporary);
        Console.WriteLine("Sudoku persistence checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void RoundTrip()
    {
        var original = Played();
        var json = SudokuSaveCodec.Encode(original);
        var root = JObject.Parse(json);
        Assert((int)root["schemaVersion"] == 1 && (string)root["gameId"] == "sudoku", "Wrong schema identity.");
        Assert(SudokuSaveCodec.TryDecode(json, out var decoded, out var error), error);
        EqualState(original, decoded);
        Assert(!ReferenceEquals(original.Game.Values, decoded.Game.Values), "Decoded values alias input.");
    }

    private static void LiteralPayload()
    {
        var puzzle = Puzzle();
        var givens = string.Join(",", puzzle.Givens);
        var solution = string.Join(",", puzzle.Solution);
        var zeroes = string.Join(",", new int[81]);
        var json = "{\"schemaVersion\":1,\"gameId\":\"sudoku\",\"game\":{\"puzzle\":{\"id\":\"se-v1-d0-l001\",\"difficulty\":0,\"level\":1,\"givens\":[" + givens + "],\"solution\":[" + solution + "]},\"values\":[" + givens + "],\"notes\":[" + zeroes + "],\"mistakes\":0,\"hintsUsed\":0,\"revision\":0,\"history\":[]},\"elapsedSeconds\":0,\"pencil\":false,\"quick\":false,\"selectedCell\":-1,\"quickDigit\":0,\"hint\":null,\"completedIds\":[],\"unassistedIds\":[]}";
        Assert(SudokuSaveCodec.TryDecode(json, out var data, out var error), error);
        EqualState(Example(), data);
    }

    private static void WrongAndLost()
    {
        var game = new SudokuGame(Puzzle());
        Assert(game.EnterValue(2, 1).Changed, "Wrong fixture input failed.");
        var data = new SudokuSaveData { Game = game.GetSnapshot() };
        Assert(SudokuSaveCodec.TryDecode(SudokuSaveCodec.Encode(data), out var restored, out var error), error);
        Assert(restored.Game.Values[2] == 1 && restored.Game.Mistakes == 1, "Wrong value was rejected or normalized.");
        Assert(new SudokuGame(restored.Game).Phase == SudokuPhase.Playing, "First mistake should remain playable.");
        Assert(game.EnterValue(2, 2).Changed && game.EnterValue(2, 3).Changed, "Third mistake fixture failed.");
        data.Game = game.GetSnapshot();
        Assert(SudokuSaveCodec.TryDecode(SudokuSaveCodec.Encode(data), out restored, out error), error);
        var resumed = new SudokuGame(restored.Game);
        Assert(resumed.Phase == SudokuPhase.Lost && !resumed.Undo().Changed && !resumed.Erase(2).Changed, "Third error revived after restore.");
        EqualState(data, restored);
    }

    private static void RestoredUndo()
    {
        var game = new SudokuGame(Puzzle());
        Assert(game.ToggleNote(2, 4).Changed && game.ToggleNote(6, 4).Changed, "Note fixture failed.");
        Assert(game.EnterValue(2, 1).Changed && game.Erase(2).Changed, "Cumulative error fixture failed.");
        Assert(game.SpendHint(), "Hint fixture failed.");
        var before = game.GetSnapshot();
        Assert(game.EnterValue(2, 4).Changed, "Correct input failed.");
        var source = new SudokuSaveData { Game = game.GetSnapshot() };
        Assert(SudokuSaveCodec.TryDecode(SudokuSaveCodec.Encode(source), out var decoded, out var error), error);
        var resumed = new SudokuGame(decoded.Game);
        Assert(resumed.Undo().Changed, "Restored undo unavailable.");
        var result = resumed.GetSnapshot();
        Assert(result.Values.SequenceEqual(before.Values) && result.Notes.SequenceEqual(before.Notes), "Undo did not restore full transaction.");
        Assert(result.Mistakes == 1 && result.HintsUsed == 1, "Undo refunded counters.");
        Assert(resumed.Undo().Changed && resumed.ValueAt(2) == 1, "Undo did not restore historical wrong number.");
        Assert(resumed.Errors == 1, "Historical wrong number counted twice.");
    }

    private static void PaidHint()
    {
        var data = WithHint();
        var game = new SudokuGame(data.Game);
        Assert(game.SpendHint() && game.SpendHint(), "Could not consume remaining hints.");
        data.Game = game.GetSnapshot();
        data.Hint.Revealed = true;
        Assert(SudokuSaveCodec.TryDecode(SudokuSaveCodec.Encode(data), out var decoded, out var error), error);
        Assert(decoded.Game.HintsUsed == 3 && decoded.Hint.Revealed && decoded.Hint.Revision == decoded.Game.Revision, "Paid help lost at quota zero.");
        var resumed = new SudokuGame(decoded.Game);
        Assert(!resumed.SpendHint() && resumed.ApplyHint(decoded.Hint.Cell, decoded.Hint.Digit, decoded.Hint.Revision).Changed, "Already-paid hint cannot apply with zero quota.");
        Assert(resumed.GetSnapshot().HintsUsed == 3, "Apply charged another hint.");
    }

    private static void RequiredFields()
    {
        var root = JObject.Parse(SudokuSaveCodec.Encode(WithHint()));
        foreach (var path in new[] { "", "game", "game.puzzle", "hint" })
        {
            var section = path == "" ? root : (JObject)root.SelectToken(path);
            foreach (var property in section.Properties().ToArray())
            {
                var missing = (JObject)root.DeepClone();
                (path == "" ? missing : (JObject)missing.SelectToken(path)).Remove(property.Name);
                Reject(missing.ToString(), "missing " + path + "." + property.Name);
                if (path == "" && property.Name == "hint") continue;
                var nulled = (JObject)root.DeepClone();
                (path == "" ? nulled : (JObject)nulled.SelectToken(path))[property.Name] = JValue.CreateNull();
                Reject(nulled.ToString(), "null " + path + "." + property.Name);
            }
        }
        root = JObject.Parse(SudokuSaveCodec.Encode(Played()));
        foreach (var field in new[] { "values", "notes" })
        {
            var missing = (JObject)root.DeepClone();
            ((JObject)missing["game"]["history"][0]).Remove(field);
            Reject(missing.ToString(), "missing history " + field);
            var nulled = (JObject)root.DeepClone();
            nulled["game"]["history"][0][field] = JValue.CreateNull();
            Reject(nulled.ToString(), "null history " + field);
        }
    }

    private static void StrictTokens()
    {
        foreach (var path in new[] { "schemaVersion", "game.puzzle.difficulty", "game.puzzle.level", "game.mistakes", "game.hintsUsed", "game.revision", "selectedCell", "quickDigit", "hint.cell", "hint.digit", "hint.revision" })
            foreach (var value in new JToken[] { "1", 1.0, true, long.MaxValue, long.MinValue, new JObject() }) Invalid(path, value);
        foreach (var path in new[] { "pencil", "quick", "hint.isDirect", "hint.revealed" })
            foreach (var value in new JToken[] { "false", 0, 1, new JObject() }) Invalid(path, value);
        foreach (var path in new[] { "gameId", "game.puzzle.id", "hint.technique", "hint.explanation" })
            foreach (var value in new JToken[] { 1, true, new JObject() }) Invalid(path, value);
        foreach (var value in new JToken[] { "1.5", true, new JObject(), double.NaN, double.PositiveInfinity, -1.0 }) Invalid("elapsedSeconds", value);
        foreach (var path in new[] { "game.values[2]", "game.notes[2]", "game.puzzle.givens[2]", "game.puzzle.solution[2]", "hint.evidence[0]" })
            foreach (var value in new JToken[] { "1", 1.0, true, JValue.CreateNull(), long.MaxValue, new JArray() }) Invalid(path, value);
        Invalid("completedIds", new JArray(JValue.CreateNull()));
        Invalid("completedIds", new JArray(1));
        Invalid("unassistedIds", new JArray(JValue.CreateNull()));
        Invalid("schemaVersion", 2);
        Invalid("gameId", "sheep");
    }

    private static void ArrayAndUnknownFields()
    {
        foreach (var path in new[] { "game.values", "game.notes", "game.puzzle.givens", "game.puzzle.solution" })
            foreach (var value in new JToken[] { 1, "[]", new JObject(), new JArray(), new JArray(new int[80]), new JArray(new int[82]) }) Invalid(path, value);
        foreach (var path in new[] { "game.history", "hint.evidence", "completedIds", "unassistedIds" })
            foreach (var value in new JToken[] { 1, "[]", new JObject() }) Invalid(path, value);
        foreach (var path in new[] { "", "game", "game.puzzle", "hint" })
        {
            var root = JObject.Parse(SudokuSaveCodec.Encode(WithHint()));
            (path == "" ? root : (JObject)root.SelectToken(path))["unknown"] = 0;
            Reject(root.ToString(), "unknown field at " + path);
        }
        var history = JObject.Parse(SudokuSaveCodec.Encode(Played()));
        history["game"]["history"][0]["mistakes"] = 0;
        Reject(history.ToString(), "extra counter on undo frame");
    }

    private static void InvalidDocuments()
    {
        var valid = SudokuSaveCodec.Encode(WithHint());
        foreach (var bad in new[] { "", " ", "{", "null", "[]", "17", "{}", valid + "{}", valid + "17", valid + " trailing",
            valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1"),
            valid.Replace("\"difficulty\":0", "\"difficulty\":0,\"difficulty\":0"),
            valid.Replace("\"revision\":0", "\"revision\":0,\"revision\":0"),
            "{\"game\":[[[[[[[[[[[[[[[[[[[[[]]]]]]]]]]]]]]]]]]]]]}",
            valid.Replace("\"elapsedSeconds\":0.0", "\"elapsedSeconds\":1e999") })
            if (bad != valid) Reject(bad, "invalid document");
        Assert(SudokuSaveCodec.TryDecode(valid + " \n\t", out var data, out var error), error);
        Assert(data != null, "Whitespace should be valid.");
    }

    private static void OversizedPayloads()
    {
        Reject(new string(' ', SudokuSaveCodec.MaximumLength + 1), "too many characters");
        Reject("{\"x\":\"" + new string('界', SudokuSaveCodec.MaximumLength / 2) + "\"}", "too many UTF-8 bytes");
    }

    private static void InvalidStates()
    {
        var mutations = new Action<SudokuSaveData>[]
        {
            d => d.Game = null, d => d.Game.Puzzle = null, d => d.Game.Values = null, d => d.Game.Notes = null,
            d => d.Game.History = null, d => d.Game.Values[0] = 0, d => d.Game.Values[2] = 10,
            d => d.Game.Notes[0] = 1, d => d.Game.Notes[2] = 512, d => d.Game.Notes[2] = -1,
            d => d.Game.Mistakes = -1, d => d.Game.Mistakes = 4, d => d.Game.HintsUsed = -1, d => d.Game.HintsUsed = 4,
            d => d.Game.Revision = -1, d => d.Game.Puzzle.Id = "se-v1-d0-l002", d => d.Game.Puzzle.Difficulty = 4,
            d => d.Game.Puzzle.Level = 501, d => d.Game.Puzzle.Solution[0] = 1,
            d => d.ElapsedSeconds = double.NaN, d => d.ElapsedSeconds = double.PositiveInfinity, d => d.ElapsedSeconds = -1,
            d => d.SelectedCell = -2, d => d.SelectedCell = 81, d => d.QuickDigit = -1, d => d.QuickDigit = 10,
            d => d.CompletedIds = null, d => d.UnassistedIds = null
        };
        foreach (var mutate in mutations)
        {
            var data = Example();
            mutate(data);
            ExpectArgumentException(() => SudokuSaveCodec.Encode(data));
        }
        ExpectArgumentException(() => SudokuSaveCodec.Encode(null));
        foreach (var path in new[] { "game.mistakes", "game.hintsUsed" }) { Invalid(path, -1); Invalid(path, 4); }
        Invalid("game.puzzle.id", "se-v1-d0-l002");
        Invalid("selectedCell", 81);
        Invalid("quickDigit", 10);
    }

    private static void HistoryTampering()
    {
        var json = JObject.Parse(SudokuSaveCodec.Encode(Played()));
        var bad = (JObject)json.DeepClone(); bad["game"]["history"][0] = JValue.CreateNull(); Reject(bad.ToString(), "null frame");
        bad = (JObject)json.DeepClone(); bad["game"]["history"][0]["values"][0] = 0; Reject(bad.ToString(), "changed historical given");
        bad = (JObject)json.DeepClone(); bad["game"]["history"][0]["notes"] = new JArray(); Reject(bad.ToString(), "short frame");
        bad = (JObject)json.DeepClone(); bad["game"]["revision"] = 0; Reject(bad.ToString(), "history larger than revision");
        bad = (JObject)json.DeepClone(); bad["game"]["history"][0]["values"][2] = 1; Reject(bad.ToString(), "red frame with zero error count");
    }

    private static void HintValidation()
    {
        foreach (var path in new[] { "hint.cell", "hint.digit", "hint.revision" }) Invalid(path, -1);
        Invalid("hint.cell", 81); Invalid("hint.digit", 10); Invalid("hint.revision", 1);
        Invalid("hint.cell", 0); Invalid("hint.digit", 1); Invalid("game.hintsUsed", 0);
        Invalid("hint.technique", ""); Invalid("hint.explanation", "");
        Invalid("hint.evidence", new JArray(-1)); Invalid("hint.evidence", new JArray(81));
        Invalid("hint.evidence", new JArray(Enumerable.Range(0, 82).Select(i => i % 81)));
        Invalid("hint.technique", new string('a', 129)); Invalid("hint.explanation", new string('a', 8193));
        var data = WithHint();
        var game = new SudokuGame(data.Game);
        Assert(game.EnterValue(2, 4).Changed, "Hint target fixture failed.");
        data.Game = game.GetSnapshot(); data.Hint.Revision = data.Game.Revision;
        ExpectArgumentException(() => SudokuSaveCodec.Encode(data));
        data = WithHint(); game = new SudokuGame(data.Game);
        Assert(game.EnterValue(3, 1).Changed, "Error fixture failed.");
        data.Game = game.GetSnapshot(); data.Hint.Revision = data.Game.Revision;
        ExpectArgumentException(() => SudokuSaveCodec.Encode(data));
    }

    private static void CompletionRecords()
    {
        Invalid("completedIds", new JArray("se-v1-d0-l001", "se-v1-d0-l001"));
        foreach (var id in new[] { "", "bad", "se-v1-d4-l001", "se-v1-d0-l000", "se-v1-d0-l501", "se-v1-d0-l1" }) Invalid("completedIds", new JArray(id));
        Invalid("unassistedIds", new JArray("se-v1-d0-l001"));
        var good = Example(); good.CompletedIds = new[] { "se-v1-d0-l001", "se-v1-d3-l500" }; good.UnassistedIds = new[] { "se-v1-d3-l500" };
        Assert(SudokuSaveCodec.TryDecode(SudokuSaveCodec.Encode(good), out var result, out var error), error);
        EqualState(good, result);
        good.UnassistedIds = new[] { "se-v1-d3-l500", "se-v1-d3-l500" };
        ExpectArgumentException(() => SudokuSaveCodec.Encode(good));
    }

    private static void TerminalAccounting()
    {
        foreach (var assisted in new[] { false, true })
        {
            var game = new SudokuGame(Puzzle());
            if (assisted) Assert(game.SpendHint(), "Paid fixture failed.");
            var puzzle = Puzzle();
            for (var i = 0; i < 81; i++) if (puzzle.Givens[i] == 0) Assert(game.EnterValue(i, puzzle.Solution[i]).Changed, "Could not solve fixture.");
            Assert(game.Phase == SudokuPhase.Won, "Fixture is not won.");
            var data = new SudokuSaveData { Game = game.GetSnapshot() };
            ExpectArgumentException(() => SudokuSaveCodec.Encode(data));
            data.CompletedIds = new[] { puzzle.Id };
            if (!assisted)
            {
                ExpectArgumentException(() => SudokuSaveCodec.Encode(data));
                data.UnassistedIds = new[] { puzzle.Id };
            }
            Assert(SudokuSaveCodec.TryDecode(SudokuSaveCodec.Encode(data), out var result, out var error), error);
            Assert(new SudokuGame(result.Game).Phase == SudokuPhase.Won, "Won save revived.");
            EqualState(data, result);
        }
    }

    private static void CopyOwnership()
    {
        var source = WithHint(); source.CompletedIds = new[] { "se-v1-d1-l001" }; source.UnassistedIds = new[] { "se-v1-d1-l001" };
        var copy = source.Copy();
        EqualState(source, copy);
        Assert(!ReferenceEquals(source.Hint, copy.Hint) && !ReferenceEquals(source.Hint.Evidence, copy.Hint.Evidence), "Hint copy aliases source.");
        Assert(!ReferenceEquals(source.CompletedIds, copy.CompletedIds) && !ReferenceEquals(source.UnassistedIds, copy.UnassistedIds), "Completion copy aliases source.");
        copy.Hint.Evidence[0] = 80; copy.Game.Values[2] = 9; copy.Game.Puzzle.Solution[0] = 9;
        Assert(source.Hint.Evidence[0] == 0 && source.Game.Values[2] == 0 && source.Game.Puzzle.Solution[0] == 5, "Deep copy mutated source.");
        source = Played(); copy = source.Copy(); copy.Game.History[0].Values[2] = 9;
        Assert(source.Game.History[0].Values[2] == 0, "History copy aliases source.");
    }

    private static void MissingSave()
    {
        WithDirectory(directory =>
        {
            var store = new SudokuSaveStore(directory);
            Assert(store.SavePath == Path.Combine(directory, "sudoku-v1.json"), "Wrong filename.");
            Assert(store.Load(out var warning) == null && warning == null, "Missing save should be quiet.");
        });
        ExpectArgumentException(() => new SudokuSaveStore(null));
        ExpectArgumentException(() => new SudokuSaveStore(" "));
    }

    private static void FreshProcess()
    {
        WithDirectory(directory =>
        {
            var data = Played(); var store = new SudokuSaveStore(directory);
            Assert(store.TrySave(data, out var error), error);
            EqualState(data, ReadInFreshProcess(directory));
            Assert(store.TrySave(WithHint(), out error), error);
            File.WriteAllText(store.SavePath, "broken");
            EqualState(data, ReadInFreshProcess(directory));
        });
    }

    private static SudokuSaveData ReadInFreshProcess(string directory)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("--fresh-load"); info.ArgumentList.Add(directory);
        using (var process = Process.Start(info))
        {
            var output = process.StandardOutput.ReadToEnd(); var stderr = process.StandardError.ReadToEnd(); process.WaitForExit();
            Assert(process.ExitCode == 0, "Fresh process failed: " + stderr);
            Assert(SudokuSaveCodec.TryDecode(output.Trim(), out var data, out var error), error);
            return data;
        }
    }

    private static void BackupRecovery()
    {
        WithDirectory(directory =>
        {
            var store = new SudokuSaveStore(directory);
            Assert(store.TrySave(Played(), out var error), error);
            Assert(store.TrySave(WithHint(), out error), error);
            EqualState(WithHint(), new SudokuSaveStore(directory).Load(out var warning)); Assert(warning == null, warning);
            Assert(File.Exists(store.SavePath + ".bak") && !File.Exists(store.SavePath + ".tmp"), "Missing backup or uncommitted temporary remains.");
            File.WriteAllText(store.SavePath, "interrupted");
            EqualState(Played(), new SudokuSaveStore(directory).Load(out warning)); Assert(!string.IsNullOrWhiteSpace(warning), "Missing recovery warning.");
            File.Delete(store.SavePath);
            EqualState(Played(), new SudokuSaveStore(directory).Load(out warning)); Assert(!string.IsNullOrWhiteSpace(warning), "Missing primary recovery warning.");
        });
    }

    private static void PreserveValidBackup()
    {
        WithDirectory(directory =>
        {
            var store = new SudokuSaveStore(directory);
            Assert(store.TrySave(Played(), out var error) && store.TrySave(WithHint(), out error), error);
            var backup = File.ReadAllBytes(store.SavePath + ".bak"); File.WriteAllText(store.SavePath, "corrupt");
            Assert(store.TrySave(Example(), out error), error);
            EqualBytes(backup, File.ReadAllBytes(store.SavePath + ".bak"));
            File.WriteAllText(store.SavePath, "corrupt again");
            EqualState(Played(), store.Load(out var warning)); Assert(warning != null, "Missing fallback warning.");
        });
    }

    private static void BothInvalid()
    {
        WithDirectory(directory =>
        {
            var store = new SudokuSaveStore(directory); File.WriteAllText(store.SavePath, "bad"); File.WriteAllText(store.SavePath + ".bak", "bad");
            Assert(store.Load(out var warning) == null && !string.IsNullOrWhiteSpace(warning), "Invalid files must not leak partial data.");
        });
    }

    private static void InvalidFiles()
    {
        foreach (var bytes in new[] { new byte[] { 0xff, 0xfe, 0xff }, new byte[SudokuSaveCodec.MaximumLength + 1] })
            WithDirectory(directory =>
            {
                var store = new SudokuSaveStore(directory); File.WriteAllBytes(store.SavePath, bytes);
                File.WriteAllText(store.SavePath + ".bak", SudokuSaveCodec.Encode(Played()));
                EqualState(Played(), store.Load(out var warning)); Assert(warning != null, "Invalid file requires warning.");
            });
    }

    private static void WriteFailures()
    {
        WithDirectory(directory =>
        {
            var store = new SudokuSaveStore(directory); Directory.Delete(directory); File.WriteAllText(directory, "occupied");
            Assert(!store.TrySave(Example(), out var error) && !string.IsNullOrWhiteSpace(error), "File used as directory should fail.");
        });
        foreach (var suffix in new[] { ".tmp", ".bak" })
            WithDirectory(directory =>
            {
                var store = new SudokuSaveStore(directory); Assert(store.TrySave(Played(), out var error), error);
                var primary = File.ReadAllBytes(store.SavePath); Directory.CreateDirectory(store.SavePath + suffix);
                Assert(!store.TrySave(WithHint(), out error) && !string.IsNullOrWhiteSpace(error), "Blocked " + suffix + " should fail.");
                EqualBytes(primary, File.ReadAllBytes(store.SavePath)); EqualState(Played(), store.Load(out var warning));
            });
    }

    private static void InvalidWrite()
    {
        WithDirectory(directory =>
        {
            var store = new SudokuSaveStore(directory); Assert(store.TrySave(Played(), out var error) && store.TrySave(WithHint(), out error), error);
            File.WriteAllText(store.SavePath + ".tmp", "preserve me");
            var primary = File.ReadAllBytes(store.SavePath); var backup = File.ReadAllBytes(store.SavePath + ".bak"); var temporary = File.ReadAllBytes(store.SavePath + ".tmp");
            var invalid = WithHint(); invalid.Game.HintsUsed = 0;
            Assert(!store.TrySave(invalid, out error) && !string.IsNullOrWhiteSpace(error), "Invalid model written.");
            EqualBytes(primary, File.ReadAllBytes(store.SavePath)); EqualBytes(backup, File.ReadAllBytes(store.SavePath + ".bak")); EqualBytes(temporary, File.ReadAllBytes(store.SavePath + ".tmp"));
        });
    }

    private static void ExistingGamesUntouched()
    {
        WithDirectory(directory =>
        {
            var sentinel = new byte[] { 0, 255, 1, 2, 8, 13, 10, 31 };
            foreach (var name in new[] { "xanaduyonder-v1.json", "jump-v1.json", "sheep-v1.json" })
                foreach (var suffix in new[] { "", ".bak", ".tmp" }) File.WriteAllBytes(Path.Combine(directory, name + suffix), sentinel);
            var store = new SudokuSaveStore(directory); Assert(store.TrySave(Played(), out var error) && store.TrySave(WithHint(), out error), error);
            EqualState(WithHint(), store.Load(out var warning));
            Directory.CreateDirectory(store.SavePath + ".tmp"); Assert(!store.TrySave(Example(), out error), "Blocked write should fail.");
            foreach (var name in new[] { "xanaduyonder-v1.json", "jump-v1.json", "sheep-v1.json" })
                foreach (var suffix in new[] { "", ".bak", ".tmp" }) EqualBytes(sentinel, File.ReadAllBytes(Path.Combine(directory, name + suffix)));
        });
    }

    private static void LoadedOwnership()
    {
        WithDirectory(directory =>
        {
            var store = new SudokuSaveStore(directory); Assert(store.TrySave(WithHint(), out var error), error);
            var first = store.Load(out var warning); var second = store.Load(out warning);
            first.Game.Values[2] = 9; first.Hint.Evidence[0] = 80; first.Game.Puzzle.Solution[0] = 9;
            EqualState(WithHint(), second); EqualState(WithHint(), store.Load(out warning));
        });
    }

    private static void OrphanTemporary()
    {
        WithDirectory(directory =>
        {
            var store = new SudokuSaveStore(directory); File.WriteAllText(store.SavePath + ".tmp", SudokuSaveCodec.Encode(Played()));
            Assert(store.Load(out var warning) == null && warning == null, "Orphan temporary was committed.");
        });
    }

    private static SudokuPuzzle Puzzle()
    {
        return new SudokuPuzzle { Id = "se-v1-d0-l001", Difficulty = 0, Level = 1,
            Givens = Digits("530070000600195000098000060800060003400803001700020006060000280000419005000080079"),
            Solution = Digits("534678912672195348198342567859761423426853791713924856961537284287419635345286179") };
    }

    private static int[] Digits(string text) { return text.Select(c => c - '0').ToArray(); }
    private static SudokuSaveData Example() { return new SudokuSaveData { Game = new SudokuGame(Puzzle()).GetSnapshot() }; }
    private static SudokuSaveData Played()
    {
        var game = new SudokuGame(Puzzle());
        Assert(game.ToggleNote(2, 4).Changed && game.ToggleNote(6, 4).Changed && game.EnterValue(2, 4).Changed, "Could not construct played fixture.");
        return new SudokuSaveData { Game = game.GetSnapshot(), ElapsedSeconds = 123.456, Pencil = true, Quick = true, SelectedCell = 3, QuickDigit = 6 };
    }
    private static SudokuSaveData WithHint()
    {
        var data = Example(); var game = new SudokuGame(data.Game); Assert(game.SpendHint(), "Could not construct paid hint fixture."); data.Game = game.GetSnapshot();
        data.Hint = new SudokuSavedHint { Cell = 2, Digit = 4, Revision = data.Game.Revision, Technique = "direct", Explanation = "请选择直接揭晓推荐格。", Evidence = new[] { 0, 1 }, IsDirect = true };
        return data;
    }
    private static void EqualState(SudokuSaveData expected, SudokuSaveData actual)
    {
        Assert(actual != null && actual.Game != null, "Loaded state missing.");
        Assert(JToken.DeepEquals(JObject.Parse(SudokuSaveCodec.Encode(expected)), JObject.Parse(SudokuSaveCodec.Encode(actual))), "Roundtrip changed saved state.");
    }
    private static void Invalid(string path, JToken value)
    {
        var root = JObject.Parse(SudokuSaveCodec.Encode(WithHint())); root.SelectToken(path).Replace(value.DeepClone()); Reject(root.ToString(Formatting.None), path + "=" + value);
    }
    private static void Reject(string json, string label)
    {
        Assert(!SudokuSaveCodec.TryDecode(json, out var data, out var error), "Accepted " + label);
        Assert(data == null && !string.IsNullOrWhiteSpace(error), "Invalid data leaked partial state or omitted reason: " + label);
    }
    private static void EqualBytes(byte[] expected, byte[] actual) { Assert(expected.SequenceEqual(actual), "Existing file bytes changed."); }
    private static void WithDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "XanaduYonder-Sudoku-Persistence-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try { action(directory); }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); if (File.Exists(directory)) File.Delete(directory); }
    }
    private static void ExpectArgumentException(Action action)
    {
        try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Expected ArgumentException for invalid state.");
    }
    private static void Check(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS: " + name); }
        catch (Exception error) { failed++; Console.Error.WriteLine("FAIL: " + name + "\n" + error); }
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
