using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Sudoku;
using XanaduYonder.Runtime.Sudoku;

internal static class Program
{
    private static int passed, failed;

    private static int Main()
    {
        Check("Fresh profile and game creation never invent an active slot", FreshProfile);
        Check("Completion totals are counted independently for each difficulty", CompletionCounts);
        Check("Completion totals refresh after capture without counting replays twice", CompletionCountsAfterCapture);
        Check("All four slots, pages and global completion metadata round trip", FourSlots);
        Check("Replacing one difficulty preserves all other slots", ReplaceOne);
        Check("Terminal capture clears only its slot and merges global completion", TerminalCapture);
        Check("Capture, retrieval, creation and profile copy own nested data", DefensiveCopies);
        Check("Resumed slots receive latest global completion metadata", GlobalMetadata);
        Check("Invalid profile and slot ownership are rejected", InvalidProfiles);
        Check("Invalid capture leaves profile unchanged", InvalidCapture);
        Check("Every root field rejects omission and null", RequiredFields);
        Check("Token types, null entries and numeric ranges are strict", StrictFields);
        Check("Nested sessions retain strict v1 validation", NestedValidation);
        Check("Malformed, duplicate and trailing JSON return no partial state", InvalidDocuments);
        Check("Missing store is quiet and orphan temporary is ignored", MissingStore);
        Check("Active v1 migration preserves paid help, controls and full history", MigrateActive);
        Check("Won and lost v1 games migrate metadata without active slots", MigrateTerminal);
        Check("Untouched v1 default does not become a phantom active game", MigrateDefault);
        Check("Explicit zero-time nondefault v1 selections remain resumable", MigrateChosenUntouched);
        Check("Time, control, hint and undone-input activity remain resumable", MigrateActivity);
        Check("Migration recovers v1 backup without touching old files", MigrateBackup);
        Check("Any committed v2 blocks stale v1 fallback", V2Precedence);
        Check("Second atomic write creates a usable recovery backup", BackupRecovery);
        Check("Corrupt primary cannot overwrite the last valid backup", PreserveBackup);
        Check("Invalid UTF8 and oversized files recover from backup", InvalidFiles);
        Check("Invalid models preserve primary, backup and temporary bytes", InvalidWrite);
        Check("Storage failures preserve the committed profile", WriteFailures);
        Check("Other games and legacy files remain byte-identical", OtherFilesUntouched);
        Console.WriteLine("Sudoku profile checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void FreshProfile()
    {
        var profile = new SudokuProfile();
        Assert(profile.TryValidate(out var error), error);
        Assert(profile.SelectedDifficulty == 0 && profile.CompletedCount == 0 && profile.Pages.SequenceEqual(new int[4]), "Wrong initial profile.");
        for (var difficulty = 0; difficulty < 4; difficulty++) Assert(profile.GetSlot(difficulty) == null, "Phantom active slot.");
        var fresh = profile.CreateGame(Puzzle(2, 17));
        Assert(fresh.TryValidate(out error), error);
        Assert(fresh.Game.Revision == 0 && fresh.SelectedCell == -1 && profile.GetSlot(2) == null, "Creating a candidate modified the profile.");
        ExpectArgument(() => profile.GetSlot(-1)); ExpectArgument(() => profile.GetSlot(4));
    }

    private static void FourSlots()
    {
        var original = FilledProfile();
        var json = SudokuProfileCodec.Encode(original);
        var root = JObject.Parse(json);
        Assert((int)root["schemaVersion"] == 2 && (string)root["gameId"] == "sudoku", "Wrong v2 schema.");
        Assert(SudokuProfileCodec.TryDecode(json, out var decoded, out var error), error);
        EqualProfile(original, decoded);
        for (var difficulty = 0; difficulty < 4; difficulty++) EqualSave(original.GetSlot(difficulty), decoded.GetSlot(difficulty));
    }

    private static void CompletionCounts()
    {
        var profile = new SudokuProfile();
        for (int difficulty = 0; difficulty < 4; difficulty++)
            Assert(profile.CompletedCountForDifficulty(difficulty) == 0, "Fresh difficulty invented completions.");
        profile.CompletedIds = new[] { "se-v1-d0-l001", "se-v1-d0-l500", "se-v1-d1-l001", "se-v1-d2-l001", "se-v1-d2-l009", "se-v1-d2-l010", "se-v1-d3-l500" };
        Assert(profile.TryValidate(out var error), error);
        var expected = new[] { 2, 1, 3, 1 };
        for (int difficulty = 0; difficulty < 4; difficulty++)
            Assert(profile.CompletedCountForDifficulty(difficulty) == expected[difficulty], "Completion belongs to the wrong difficulty.");
        Assert(expected.Sum() == profile.CompletedCount, "Per-difficulty counts differ from global total.");
        foreach (int invalid in new[] { -1, 4, int.MinValue, int.MaxValue })
        {
            try { profile.CompletedCountForDifficulty(invalid); }
            catch (ArgumentOutOfRangeException exception)
            {
                Assert(exception.ParamName == "difficulty", "Invalid difficulty omitted parameter name.");
                continue;
            }
            throw new InvalidOperationException("Invalid difficulty was accepted.");
        }
        Assert(SudokuProfileCodec.TryDecode(SudokuProfileCodec.Encode(profile), out var restored, out error), error);
        for (int difficulty = 0; difficulty < 4; difficulty++)
            Assert(restored.CompletedCountForDifficulty(difficulty) == expected[difficulty], "Restored counts changed.");
    }

    private static void CompletionCountsAfterCapture()
    {
        var profile = new SudokuProfile();
        Assert(profile.CompletedCountForDifficulty(1) == 0, "Fixture must start empty.");
        profile.Capture(Terminal(1, true));
        Assert(profile.CompletedCountForDifficulty(1) == 1 && profile.CompletedCount == 1, "New completion not reflected.");
        profile.Capture(Terminal(1, true));
        Assert(profile.CompletedCountForDifficulty(1) == 1 && profile.CompletedCount == 1, "Replay counted twice.");
        profile.Capture(Terminal(3, true));
        profile.Capture(Terminal(2, false));
        Assert(profile.CompletedCountForDifficulty(0) == 0 && profile.CompletedCountForDifficulty(1) == 1 &&
            profile.CompletedCountForDifficulty(2) == 0 && profile.CompletedCountForDifficulty(3) == 1 && profile.CompletedCount == 2,
            "Later capture changed an unrelated count or recorded a loss.");
    }

    private static void ReplaceOne()
    {
        var profile = FilledProfile(); var before = profile.Copy();
        profile.Capture(Played(2, 88));
        Assert(profile.GetSlot(2).Game.Puzzle.Level == 88, "Requested replacement missing.");
        foreach (var difficulty in new[] { 0, 1, 3 }) EqualSave(before.GetSlot(difficulty), profile.GetSlot(difficulty));
        Assert(profile.CompletedIds.SequenceEqual(before.CompletedIds), "Replacement discarded completions.");
    }

    private static void TerminalCapture()
    {
        var profile = FilledProfile();
        profile.Capture(Terminal(1, true));
        Assert(profile.GetSlot(1) == null && profile.CompletedIds.Contains("se-v1-d1-l001") && profile.UnassistedIds.Contains("se-v1-d1-l001"), "Win was not accounted globally.");
        profile.Capture(Terminal(2, false));
        Assert(profile.GetSlot(2) == null && profile.GetSlot(0) != null && profile.GetSlot(3) != null, "Terminal affected another difficulty.");
        Assert(!profile.CompletedIds.Contains("se-v1-d2-l001"), "Loss counted as completion.");
        Assert(profile.CompletedCount == 2 && profile.TryValidate(out var error), "Wrong completed count or invalid terminal capture.");
    }

    private static void DefensiveCopies()
    {
        var profile = FilledProfile(); var source = WithHint(1); profile.Capture(source);
        var before = profile.Copy();
        source.Game.Values[2] = 9; source.Game.History[0].Values[0] = 0; source.Hint.Evidence[0] = 80; source.CompletedIds[0] = "changed";
        EqualProfile(before, profile);
        var retrieved = profile.GetSlot(1); retrieved.Hint.Evidence[0] = 80; retrieved.Game.Puzzle.Solution[0] = 0; retrieved.UnassistedIds[0] = "changed";
        EqualProfile(before, profile);
        var clone = profile.Copy(); clone.Pages[0] = 9; clone.Slots[1].Game.History[0].Notes[2] = 511; clone.CompletedIds[0] = "changed";
        EqualProfile(before, profile);
        var created = profile.CreateGame(Puzzle(0)); created.CompletedIds[0] = "changed"; created.UnassistedIds[0] = "changed";
        EqualProfile(before, profile);
    }

    private static void GlobalMetadata()
    {
        var profile = FilledProfile();
        profile.Capture(Terminal(1, true));
        var stale = profile.Slots[0]; Assert(!stale.CompletedIds.Contains("se-v1-d1-l001"), "Fixture is not stale.");
        var resumed = profile.GetSlot(0);
        Assert(resumed.CompletedIds.Contains("se-v1-d1-l001") && resumed.UnassistedIds.Contains("se-v1-d1-l001"), "Resume missed global metadata.");
        profile.Capture(stale); Assert(profile.CompletedIds.Contains("se-v1-d1-l001"), "Stale capture overwrote global metadata.");
        Assert(profile.TryValidate(out var error), error);
    }

    private static void InvalidProfiles()
    {
        Action<SudokuProfile>[] invalid = {
            p => p.SelectedDifficulty = -1, p => p.SelectedDifficulty = 4,
            p => p.Pages = null, p => p.Pages = new int[3], p => p.Pages[0] = -1, p => p.Pages[0] = 10,
            p => p.Slots = null, p => p.Slots = new SudokuSaveData[5], p => p.Slots[0] = Played(1),
            p => p.Slots[0] = Terminal(0, false), p => p.Slots[0] = Terminal(0, true),
            p => p.CompletedIds = null, p => p.UnassistedIds = null,
            p => p.CompletedIds = new[] { "se-v1-d0-l000" }, p => p.CompletedIds = new[] { "se-v1-d0-l501" },
            p => p.CompletedIds = new[] { "se-v1-d4-l001" }, p => p.CompletedIds = new string[] { null },
            p => p.CompletedIds = new[] { "se-v1-d3-l500", "se-v1-d3-l500" },
            p => p.UnassistedIds = new[] { "se-v1-d0-l001" },
            p => p.CompletedIds = new string[0], p => p.UnassistedIds = new string[0]
        };
        foreach (var change in invalid)
        {
            var profile = FilledProfile(); change(profile);
            Assert(!profile.TryValidate(out var error) && !string.IsNullOrWhiteSpace(error), "Invalid profile accepted.");
            ExpectArgument(() => SudokuProfileCodec.Encode(profile));
        }
    }

    private static void InvalidCapture()
    {
        var profile = FilledProfile(); var before = profile.Copy();
        ExpectArgument(() => profile.Capture(null));
        var bad = Played(1); bad.Game.Values = null;
        ExpectArgument(() => profile.Capture(bad)); EqualProfile(before, profile);
        bad = Played(1); bad.CompletedIds = new[] { "bad" };
        ExpectArgument(() => profile.Capture(bad)); EqualProfile(before, profile);
    }

    private static void RequiredFields()
    {
        var root = JObject.Parse(SudokuProfileCodec.Encode(FilledProfile()));
        foreach (var property in root.Properties())
        {
            var missing = (JObject)root.DeepClone(); missing.Remove(property.Name); Reject(missing.ToString(), "missing " + property.Name);
            var nulled = (JObject)root.DeepClone(); nulled[property.Name] = JValue.CreateNull(); Reject(nulled.ToString(), "null " + property.Name);
        }
    }

    private static void StrictFields()
    {
        foreach (var path in new[] { "schemaVersion", "selectedDifficulty", "pages[0]" })
            foreach (var value in new JToken[] { "1", 1.0, true, long.MaxValue, JValue.CreateNull(), new JObject() }) Invalid(path, value);
        foreach (var value in new JToken[] { 1, true, new JObject() }) Invalid("gameId", value);
        Invalid("schemaVersion", 1); Invalid("gameId", "sheep"); Invalid("selectedDifficulty", 4); Invalid("pages[0]", 10);
        foreach (var path in new[] { "pages", "slots", "completedIds", "unassistedIds" })
            foreach (var value in new JToken[] { "[]", 1, new JObject() }) Invalid(path, value);
        Invalid("pages", new JArray(0, 0, 0)); Invalid("slots", new JArray()); Invalid("slots[0]", 1);
        Invalid("completedIds[0]", JValue.CreateNull()); Invalid("completedIds[0]", 1);
        Invalid("unassistedIds[0]", "se-v1-d0-l001");
        var root = JObject.Parse(SudokuProfileCodec.Encode(FilledProfile())); root["unknown"] = 0; Reject(root.ToString(), "unknown root field");
    }

    private static void NestedValidation()
    {
        Invalid("slots[0].game.puzzle.difficulty", 1); Invalid("slots[0].game.values[2]", "4");
        Invalid("slots[0].game.history[0]", JValue.CreateNull()); Invalid("slots[0].completedIds", new JArray("se-v1-d0-l002"));
        Invalid("slots[0].gameId", "sheep");
        var root = JObject.Parse(SudokuProfileCodec.Encode(FilledProfile())); ((JObject)root["slots"][0]).Remove("pencil"); Reject(root.ToString(), "missing session field");
        root = JObject.Parse(SudokuProfileCodec.Encode(FilledProfile())); root["slots"][0]["extra"] = 1; Reject(root.ToString(), "unknown session field");
    }

    private static void InvalidDocuments()
    {
        var valid = SudokuProfileCodec.Encode(FilledProfile());
        foreach (var invalid in new[] { (string)null, "", "[]", "null", "{}", "{", valid + "{}", valid.Replace("\"schemaVersion\":2", "\"schemaVersion\":2,\"schemaVersion\":2"), valid.Replace("\"pencil\":false", "\"pencil\":false,\"pencil\":false") }) Reject(invalid, "invalid document");
    }

    private static void MissingStore()
    {
        WithDirectory(directory => {
            var store = new SudokuProfileStore(directory);
            Assert(Path.GetFileName(store.SavePath) == "sudoku-v2.json", "Wrong profile filename.");
            Assert(store.Load(out var warning) == null && warning == null, "Missing store warned or invented progress.");
            File.WriteAllText(store.SavePath + ".tmp", SudokuProfileCodec.Encode(FilledProfile()));
            Assert(store.Load(out warning) == null && warning == null, "Temporary file treated as committed.");
        });
    }

    private static void MigrateActive()
    {
        WithDirectory(directory => {
            var legacy = new SudokuSaveStore(directory); var old = WithHint(2, 117);
            Assert(legacy.TrySave(old, out var error), error); var bytes = File.ReadAllBytes(legacy.SavePath);
            var store = new SudokuProfileStore(directory); var migrated = store.Load(out var warning);
            Assert(migrated != null && warning == null && migrated.SelectedDifficulty == 2 && migrated.Pages[2] == 2, "Migration selection/page incorrect.");
            EqualSave(old, migrated.GetSlot(2)); Assert(migrated.CompletedCount == 1, "Migration dropped completion metadata.");
            Assert(File.Exists(store.SavePath), "Migration was not committed to v2.");
            EqualProfile(migrated, store.Load(out warning)); EqualBytes(bytes, File.ReadAllBytes(legacy.SavePath));
        });
    }

    private static void MigrateTerminal()
    {
        foreach (var won in new[] { false, true }) WithDirectory(directory => {
            var old = Terminal(3, won); old.CompletedIds = old.CompletedIds.Concat(new[] { "se-v1-d2-l500" }).ToArray();
            var legacy = new SudokuSaveStore(directory); Assert(legacy.TrySave(old, out var error), error);
            var profile = new SudokuProfileStore(directory).Load(out var warning);
            Assert(profile != null && profile.Slots.All(s => s == null) && profile.CompletedIds.SequenceEqual(old.CompletedIds) && profile.UnassistedIds.SequenceEqual(old.UnassistedIds), "Terminal migration lost metadata or revived game.");
        });
    }

    private static void MigrateDefault()
    {
        WithDirectory(directory => {
            var old = Fresh(0); old.CompletedIds = new[] { "se-v1-d3-l500" };
            Assert(new SudokuSaveStore(directory).TrySave(old, out var error), error);
            var profile = new SudokuProfileStore(directory).Load(out var warning);
            Assert(profile != null && profile.Slots.All(s => s == null) && profile.CompletedCount == 1, "Untouched default became active or metadata was lost.");
        });
    }

    private static void MigrateActivity()
    {
        Action<SudokuSaveData>[] activities = {
            d => d.ElapsedSeconds = 0.01, d => d.Pencil = true, d => d.Quick = true, d => d.SelectedCell = 2, d => d.QuickDigit = 4,
            d => { var game = new SudokuGame(d.Game); game.SpendHint(); d.Game = game.GetSnapshot(); },
            d => { var game = new SudokuGame(d.Game); game.EnterValue(2, 4); game.Undo(); d.Game = game.GetSnapshot(); }
        };
        foreach (var activity in activities) WithDirectory(directory => {
            var old = Fresh(1); activity(old); Assert(new SudokuSaveStore(directory).TrySave(old, out var error), error);
            var profile = new SudokuProfileStore(directory).Load(out var warning); EqualSave(old, profile.GetSlot(1));
        });
    }

    private static void MigrateChosenUntouched()
    {
        foreach (var old in new[] { Fresh(0, 2), Fresh(2, 123), Fresh(3, 1) }) WithDirectory(directory => {
            Assert(new SudokuSaveStore(directory).TrySave(old, out var error), error);
            var profile = new SudokuProfileStore(directory).Load(out var warning);
            EqualSave(old, profile.GetSlot(old.Game.Puzzle.Difficulty));
        });
    }

    private static void MigrateBackup()
    {
        WithDirectory(directory => {
            var legacy = new SudokuSaveStore(directory); File.WriteAllText(legacy.SavePath, "broken");
            File.WriteAllText(legacy.SavePath + ".bak", SudokuSaveCodec.Encode(WithHint(3)));
            var primary = File.ReadAllBytes(legacy.SavePath); var backup = File.ReadAllBytes(legacy.SavePath + ".bak");
            var profile = new SudokuProfileStore(directory).Load(out var warning);
            EqualSave(WithHint(3), profile.GetSlot(3)); Assert(warning != null, "Legacy recovery warning omitted.");
            EqualBytes(primary, File.ReadAllBytes(legacy.SavePath)); EqualBytes(backup, File.ReadAllBytes(legacy.SavePath + ".bak"));
        });
    }

    private static void V2Precedence()
    {
        foreach (var suffix in new[] { "", ".bak" }) WithDirectory(directory => {
            Assert(new SudokuSaveStore(directory).TrySave(Played(0), out var error), error);
            var store = new SudokuProfileStore(directory); File.WriteAllText(store.SavePath + suffix, "broken");
            Assert(store.Load(out var warning) == null && warning != null, "Existing corrupt v2 fell back to stale v1.");
        });
        WithDirectory(directory => {
            Assert(new SudokuSaveStore(directory).TrySave(Played(0), out var error), error);
            var store = new SudokuProfileStore(directory); Assert(store.TrySave(new SudokuProfile(), out error), error);
            Assert(store.Load(out var warning).Slots.All(s => s == null), "Valid empty v2 resurrected v1.");
        });
    }

    private static void BackupRecovery()
    {
        WithDirectory(directory => {
            var store = new SudokuProfileStore(directory); var first = FilledProfile();
            Assert(store.TrySave(first, out var error), error); var second = first.Copy(); second.Capture(Played(2, 77));
            Assert(store.TrySave(second, out error), error); EqualProfile(second, store.Load(out var warning));
            Assert(File.Exists(store.SavePath + ".bak"), "Backup missing."); File.WriteAllText(store.SavePath, "broken");
            EqualProfile(first, store.Load(out warning)); Assert(warning != null, "Recovery omitted warning.");
        });
    }

    private static void PreserveBackup()
    {
        WithDirectory(directory => {
            var store = new SudokuProfileStore(directory); var first = FilledProfile();
            Assert(store.TrySave(first, out var error) && store.TrySave(new SudokuProfile(), out error), error);
            var backup = File.ReadAllBytes(store.SavePath + ".bak"); File.WriteAllText(store.SavePath, "broken");
            Assert(store.TrySave(new SudokuProfile(), out error), error); EqualBytes(backup, File.ReadAllBytes(store.SavePath + ".bak"));
        });
    }

    private static void InvalidFiles()
    {
        foreach (var oversized in new[] { false, true }) WithDirectory(directory => {
            var store = new SudokuProfileStore(directory);
            if (oversized) using (var stream = File.Create(store.SavePath)) stream.SetLength(SudokuProfileCodec.MaximumLength + 1L);
            else File.WriteAllBytes(store.SavePath, new byte[] { 0xff, 0xfe, 0xff });
            File.WriteAllText(store.SavePath + ".bak", SudokuProfileCodec.Encode(FilledProfile()));
            EqualProfile(FilledProfile(), store.Load(out var warning)); Assert(warning != null, "Invalid file warning missing.");
        });
    }

    private static void InvalidWrite()
    {
        WithDirectory(directory => {
            var store = new SudokuProfileStore(directory); Assert(store.TrySave(FilledProfile(), out var error) && store.TrySave(FilledProfile(), out error), error);
            File.WriteAllText(store.SavePath + ".tmp", "preserve me");
            var bytes = new[] { "", ".bak", ".tmp" }.Select(s => File.ReadAllBytes(store.SavePath + s)).ToArray();
            var invalid = FilledProfile(); invalid.Pages[0] = 10;
            Assert(!store.TrySave(invalid, out error) && error != null, "Invalid profile written.");
            for (var index = 0; index < bytes.Length; index++) EqualBytes(bytes[index], File.ReadAllBytes(store.SavePath + new[] { "", ".bak", ".tmp" }[index]));
        });
    }

    private static void WriteFailures()
    {
        foreach (var suffix in new[] { ".tmp", ".bak" }) WithDirectory(directory => {
            var store = new SudokuProfileStore(directory); Assert(store.TrySave(FilledProfile(), out var error), error);
            var bytes = File.ReadAllBytes(store.SavePath); Directory.CreateDirectory(store.SavePath + suffix);
            Assert(!store.TrySave(new SudokuProfile(), out error) && error != null, "Blocked write succeeded."); EqualBytes(bytes, File.ReadAllBytes(store.SavePath));
        });
        WithDirectory(directory => {
            var legacy = new SudokuSaveStore(directory); Assert(legacy.TrySave(Played(2), out var error), error);
            var store = new SudokuProfileStore(directory); Directory.CreateDirectory(store.SavePath + ".tmp");
            var profile = store.Load(out var warning); Assert(profile != null && warning != null, "Migration write failure lost readable progress or omitted warning.");
            EqualSave(Played(2), profile.GetSlot(2));
        });
    }

    private static void OtherFilesUntouched()
    {
        WithDirectory(directory => {
            var names = new[] { "xanaduyonder-v1.json", "jump-v1.json", "sheep-v1.json", "sudoku-v1.json" }; var sentinel = new byte[] { 0, 255, 8, 13, 10 };
            foreach (var name in names) foreach (var suffix in new[] { "", ".bak", ".tmp" }) File.WriteAllBytes(Path.Combine(directory, name + suffix), sentinel);
            var store = new SudokuProfileStore(directory); Assert(store.TrySave(FilledProfile(), out var error) && store.TrySave(new SudokuProfile(), out error), error);
            store.Load(out var warning);
            foreach (var name in names) foreach (var suffix in new[] { "", ".bak", ".tmp" }) EqualBytes(sentinel, File.ReadAllBytes(Path.Combine(directory, name + suffix)));
        });
    }

    private static SudokuProfile FilledProfile()
    {
        var profile = new SudokuProfile { SelectedDifficulty = 2, Pages = new[] { 0, 3, 7, 9 }, CompletedIds = new[] { "se-v1-d3-l500" }, UnassistedIds = new[] { "se-v1-d3-l500" } };
        for (var difficulty = 0; difficulty < 4; difficulty++) profile.Capture(Played(difficulty, difficulty + 1));
        return profile;
    }
    private static SudokuPuzzle Puzzle(int difficulty, int level = 1)
    {
        return new SudokuPuzzle { Id = "se-v1-d" + difficulty + "-l" + level.ToString("000"), Difficulty = difficulty, Level = level,
            Givens = Digits("530070000600195000098000060800060003400803001700020006060000280000419005000080079"),
            Solution = Digits("534678912672195348198342567859761423426853791713924856961537284287419635345286179") };
    }
    private static SudokuSaveData Fresh(int difficulty, int level = 1) { return new SudokuSaveData { Game = new SudokuGame(Puzzle(difficulty, level)).GetSnapshot() }; }
    private static SudokuSaveData Played(int difficulty, int level = 1)
    {
        var game = new SudokuGame(Puzzle(difficulty, level)); Assert(game.ToggleNote(2, 4).Changed && game.ToggleNote(6, 4).Changed && game.EnterValue(2, 4).Changed, "Played fixture failed.");
        return new SudokuSaveData { Game = game.GetSnapshot(), ElapsedSeconds = 123.456, Pencil = false, Quick = true, SelectedCell = 3, QuickDigit = 6,
            CompletedIds = new[] { "se-v1-d3-l500" }, UnassistedIds = new[] { "se-v1-d3-l500" } };
    }
    private static SudokuSaveData WithHint(int difficulty, int level = 1)
    {
        var data = Played(difficulty, level); var game = new SudokuGame(data.Game); Assert(game.SpendHint(), "Hint fixture failed."); data.Game = game.GetSnapshot();
        data.Hint = new SudokuSavedHint { Cell = 3, Digit = 6, Revision = data.Game.Revision, Technique = "direct", Explanation = "请选择直接揭晓推荐格。", Evidence = new[] { 0, 1 }, IsDirect = true, Revealed = true }; return data;
    }
    private static SudokuSaveData Terminal(int difficulty, bool won)
    {
        var game = new SudokuGame(Puzzle(difficulty));
        if (won) for (var cell = 0; cell < 81; cell++) { if (!game.IsGiven(cell)) Assert(game.EnterValue(cell, game.Puzzle.Solution[cell]).Changed, "Win fixture failed."); }
        else { Assert(game.EnterValue(2, 1).Changed && game.EnterValue(2, 2).Changed && game.EnterValue(2, 3).Changed, "Loss fixture failed."); }
        return new SudokuSaveData { Game = game.GetSnapshot(), CompletedIds = won ? new[] { game.Puzzle.Id } : new string[0], UnassistedIds = won ? new[] { game.Puzzle.Id } : new string[0] };
    }
    private static int[] Digits(string text) { return text.Select(c => c - '0').ToArray(); }
    private static void Invalid(string path, JToken value) { var root = JObject.Parse(SudokuProfileCodec.Encode(FilledProfile())); root.SelectToken(path).Replace(value.DeepClone()); Reject(root.ToString(Formatting.None), path); }
    private static void Reject(string json, string label) { Assert(!SudokuProfileCodec.TryDecode(json, out var profile, out var error), "Accepted " + label); Assert(profile == null && !string.IsNullOrWhiteSpace(error), "Partial state or no reason: " + label); }
    private static void EqualSave(SudokuSaveData expected, SudokuSaveData actual) { Assert(actual != null && JToken.DeepEquals(JObject.Parse(SudokuSaveCodec.Encode(expected)), JObject.Parse(SudokuSaveCodec.Encode(actual))), "Session state changed."); }
    private static void EqualProfile(SudokuProfile expected, SudokuProfile actual) { Assert(actual != null && JToken.DeepEquals(JObject.Parse(SudokuProfileCodec.Encode(expected)), JObject.Parse(SudokuProfileCodec.Encode(actual))), "Profile state changed."); }
    private static void EqualBytes(byte[] expected, byte[] actual) { Assert(expected.SequenceEqual(actual), "Committed file changed."); }
    private static void ExpectArgument(Action action) { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Expected ArgumentException."); }
    private static void WithDirectory(Action<string> action) { var directory = Path.Combine(Path.GetTempPath(), "XanaduYonder-Sudoku-Profile-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); try { action(directory); } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); } }
    private static void Check(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS: " + name); } catch (Exception error) { failed++; Console.Error.WriteLine("FAIL: " + name + "\n" + error); } }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
