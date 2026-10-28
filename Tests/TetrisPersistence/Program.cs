using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Tetris;
using XanaduYonder.Runtime.Tetris;

internal static class Program
{
    private static int passed;
    private static int failed;
    private static int Main(string[] args)
    {
        Run("preview round trip never creates a run", PreviewRoundTrip);
        Run("long scores preserve all 64 bits", LongScores);
        Run("strict JSON rejects extensions and trailing data", StrictJson);
        Run("schema shape rejects missing null wrong duplicate unknown fields", Shape);
        Run("future versions are recognized before current schema validation", Versions);
        Run("invalid data cannot encode or write", InvalidData);
        Run("missing store and atomic saves rotate last valid data", StoreRoundTrip);
        Run("backup recovery does not replace a good backup with corruption", Recovery);
        Run("all corrupt requires explicit archived reset", CorruptReset);
        Run("future primary and backup protect every save and reset", FutureProtection);
        Run("I/O errors protect saves and resets", IoProtection);
        Run("failed write leaves old bytes intact and retries cleanly", FailedWrite);
        Run("other games and common settings stay byte identical", Isolation);
        Run("read rejects oversized and invalid UTF8 without rewriting", FileLimits);
        if (args.Length == 0 || args[0] != "--codec-only")
        {
            Run("full dynamic snapshot round trips and has identical future", DynamicRoundTrip);
            Run("every snapshot field is required and token typed", SnapshotShape);
            Run("grounded contact restores lock reset budget and timer", ContactRoundTrip);
            Run("snapshot and save copies isolate mutable arrays", DefensiveCopy);
            Run("recorded results require a terminal game", TerminalResult);
        }
        Console.WriteLine("Tetris persistence: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }
    private static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static string Encode(TetrisSaveData data)
    {
        Check(TetrisSaveCodec.TryEncode(data, out var json, out var error), "encode: " + error); return json;
    }
    private static TetrisSaveData Decode(string json)
    {
        Check(TetrisSaveCodec.TryDecode(json, out var data, out var status, out var error), "decode: " + status + " " + error);
        Check(status == TetrisLoadStatus.Loaded, "successful codec status"); return data;
    }
    private static string Preview() { return Encode(new TetrisSaveData()); }
    private static void Rejected(string json, TetrisLoadStatus expected = TetrisLoadStatus.Corrupt)
    {
        Check(!TetrisSaveCodec.TryDecode(json, out var data, out var status, out var error), "unexpected valid input: " + json.Substring(0, Math.Min(json.Length, 140)));
        Check(data == null && status == expected && !string.IsNullOrWhiteSpace(error), "reject classification expected " + expected + ", got " + status);
    }
    private static void PreviewRoundTrip()
    {
        var decoded = Decode(Preview());
        Check(decoded.Game == null && decoded.BestScore == 0 && !decoded.ResultRecorded, "preview became an active game");
        Check(decoded.SchemaVersion == 1 && decoded.GameId == "tetris", "identity");
    }
    private static void LongScores()
    {
        var data = new TetrisSaveData { BestScore = long.MaxValue };
        Check(Decode(Encode(data)).BestScore == long.MaxValue, "lost precision");
        Rejected(Preview().Replace("\"bestScore\":\"0\"", "\"bestScore\":9223372036854775807"));
        foreach (var text in new[] { "-1", "+1", "1e3", "9223372036854775808", " 1", "1.0", "", "00" })
        { var root = JObject.Parse(Preview()); root["bestScore"] = text; Rejected(root.ToString()); }
    }
    private static void StrictJson()
    {
        var json = Preview();
        foreach (var bad in new[] { "", "null", "[]", json + "{}", json + " false", "/*comment*/" + json,
            json.Replace("\"schemaVersion\"", "schemaVersion"), json.Replace('"', '\''), json.Insert(json.Length - 1, ","),
            json.Replace(":1,", ":01,"), json.Replace(":1,", ":+1,"), json.Replace(":1,", ":0x1,"),
            json.Replace(":1,", ":NaN,"), json.Replace(":false", ":undefined"), json.Replace("\"tetris\"", "\"te\ntris\"") }) Rejected(bad);
        Check(Decode(" \r\n" + json + "\t ").Game == null, "valid JSON whitespace");
        Rejected(new string(' ', TetrisSaveCodec.MaximumLength + 1));
        var depth = JObject.Parse(json); depth["extra"] = JArray.Parse("[[[[[[[[[0]]]]]]]]]"); Rejected(depth.ToString());
    }
    private static void Shape()
    {
        var root = JObject.Parse(Preview());
        foreach (var property in root.Properties())
        {
            var missing = (JObject)root.DeepClone(); missing.Remove(property.Name); Rejected(missing.ToString());
            if (property.Name != "game") { var nil = (JObject)root.DeepClone(); nil[property.Name] = null; Rejected(nil.ToString()); }
        }
        var extra = (JObject)root.DeepClone(); extra["extra"] = false; Rejected(extra.ToString());
        var duplicate = Preview().Insert(1, "\"bestScore\":\"0\","); Rejected(duplicate);
        Rejected(Preview().Insert(1, "\"best\\u0053core\":\"0\","));
        foreach (var value in new JToken[] { 1, "false", new JArray(), new JObject() })
        { var wrong = (JObject)root.DeepClone(); wrong["resultRecorded"] = value; Rejected(wrong.ToString()); }
        foreach (var name in new[] { "schemaVersion", "gameId", "bestScore", "game" })
        { var wrong = (JObject)root.DeepClone(); wrong[name] = false; Rejected(wrong.ToString()); }
    }
    private static void Versions()
    {
        foreach (var json in new[] { "{\"schemaVersion\":2}", "{\"schemaVersion\":9223372036854775808}",
            "{\"gameId\":\"other-game\"}", "{\"game\":{\"rulesVersion\":2}}",
            "{\"schemaVersion\":2,\"game\":false,\"futureField\":42}" }) Rejected(json, TetrisLoadStatus.UnsupportedVersion);
        Rejected("{\"schemaVersion\":\"2\"}");
    }
    private static void InvalidData()
    {
        Check(!TetrisSaveCodec.TryEncode(null, out var json, out var error) && json == null && error != null, "missing data");
        foreach (var data in new[] { new TetrisSaveData { BestScore = -1 }, new TetrisSaveData { ResultRecorded = true }, new TetrisSaveData { SchemaVersion = 2 } })
            Check(!TetrisSaveCodec.TryEncode(data, out json, out error), "invalid data encoded");
        WithStore((store, directory) => { Check(!store.TrySave(new TetrisSaveData { BestScore = -1 }, out error), "invalid save accepted"); Check(!File.Exists(store.SavePath), "invalid data touched disk"); });
    }
    private static void StoreRoundTrip()
    {
        WithStore((store, directory) => {
            var empty = store.Load(); Check(empty.Status == TetrisLoadStatus.Missing && empty.CanWrite && empty.Data.Game == null, "missing status");
            Save(store, 17); var first = File.ReadAllText(store.SavePath); Save(store, 25);
            Check(store.Load().Status == TetrisLoadStatus.Loaded && store.Load().Data.BestScore == 25, "load latest");
            Check(File.ReadAllText(store.SavePath + ".bak") == first, "backup last good bytes");
            Check(!File.Exists(store.SavePath + ".tmp"), "temp not consumed");
            Check(!store.TryResetCorrupt(out var error), "reset permitted valid data");
        });
    }
    private static void Recovery()
    {
        WithStore((store, directory) => {
            Save(store, 17); Save(store, 25); var backup = File.ReadAllText(store.SavePath + ".bak");
            File.WriteAllText(store.SavePath, "broken"); var recovered = store.Load();
            Check(recovered.Status == TetrisLoadStatus.Recovered && recovered.CanWrite && recovered.Data.BestScore == 17, "recover corrupted main");
            Check(File.ReadAllText(store.SavePath) == "broken", "load mutated primary");
            Save(store, 35); Check(File.ReadAllText(store.SavePath + ".bak") == backup, "corrupt primary replaced valid backup");
            File.Delete(store.SavePath); recovered = store.Load();
            Check(recovered.Status == TetrisLoadStatus.Recovered && recovered.Data.BestScore == 17, "recover missing main");
            Save(store, 40); Check(File.ReadAllText(store.SavePath + ".bak") == backup, "missing main overwrote backup");
        });
    }
    private static void CorruptReset()
    {
        WithStore((store, directory) => {
            File.WriteAllText(store.SavePath, "bad-main"); File.WriteAllText(store.SavePath + ".bak", "bad-backup");
            var bad = store.Load(); Check(bad.Status == TetrisLoadStatus.Corrupt && !bad.CanWrite && bad.Data == null, "bad classification");
            Check(!store.TrySave(new TetrisSaveData(), out var error), "corrupt silently overwritten");
            Check(store.TryResetCorrupt(out error), "explicit reset: " + error);
            var files = Directory.GetFiles(directory, "*.corrupt-*"); Check(files.Length == 2, "both originals archived");
            bool main = false, backup = false; foreach (var file in files) { main |= File.ReadAllText(file) == "bad-main"; backup |= File.ReadAllText(file) == "bad-backup"; }
            Check(main && backup, "archive bytes changed"); Check(store.Load().Status == TetrisLoadStatus.Missing, "reset did not unlock"); Save(store, 2);
        });
    }
    private static void FutureProtection()
    {
        foreach (bool primary in new[] { true, false }) WithStore((store, directory) => {
            Save(store, 7); Save(store, 9); var futurePath = store.SavePath + (primary ? "" : ".bak");
            const string future = "{\"schemaVersion\":9,\"differentFormat\":true}"; File.WriteAllText(futurePath, future);
            var beforeMain = File.ReadAllBytes(store.SavePath); var beforeBackup = File.ReadAllBytes(store.SavePath + ".bak");
            var result = store.Load(); Check(result.Status == TetrisLoadStatus.UnsupportedVersion && !result.CanWrite && result.Data == null, "future classification");
            Check(!store.TrySave(new TetrisSaveData(), out var error), "future overwritten"); Check(!store.TryResetCorrupt(out error), "future reset");
            Check(Convert.ToBase64String(beforeMain) == Convert.ToBase64String(File.ReadAllBytes(store.SavePath)), "primary changed");
            Check(Convert.ToBase64String(beforeBackup) == Convert.ToBase64String(File.ReadAllBytes(store.SavePath + ".bak")), "backup changed");
        });
        WithStore((store, directory) => {
            Save(store, 3); Check(store.Load().CanWrite, "initial write gate");
            File.WriteAllText(store.SavePath + ".bak", "{\"game\":{\"rulesVersion\":2}}");
            Check(!store.TrySave(new TetrisSaveData(), out var error), "save skipped fresh backup preflight");
        });
    }
    private static void IoProtection()
    {
        foreach (bool primary in new[] { true, false }) WithStore((store, directory) => {
            Save(store, 3); string path = store.SavePath + (primary ? "" : ".bak"); if (File.Exists(path)) File.Delete(path); Directory.CreateDirectory(path);
            Check(store.Load().Status == TetrisLoadStatus.IoError && !store.Load().CanWrite, "I/O misclassified as missing/corrupt");
            Check(!store.TrySave(new TetrisSaveData(), out var error), "I/O overwritten"); Check(!store.TryResetCorrupt(out error), "I/O reset");
        });
    }
    private static void FailedWrite()
    {
        WithStore((store, directory) => {
            Save(store, 7); Save(store, 11); var main = File.ReadAllText(store.SavePath); var backup = File.ReadAllText(store.SavePath + ".bak");
            Directory.CreateDirectory(store.SavePath + ".tmp"); Check(!store.TrySave(new TetrisSaveData { BestScore = 19 }, out var error), "failure expected");
            Check(!string.IsNullOrWhiteSpace(error) && File.ReadAllText(store.SavePath) == main && File.ReadAllText(store.SavePath + ".bak") == backup, "failed save changed committed bytes");
            Directory.Delete(store.SavePath + ".tmp"); Save(store, 19); Check(store.Load().Data.BestScore == 19, "retry failed");
        });
    }
    private static void Isolation()
    {
        WithStore((store, directory) => {
            var names = new[] { "save.json", "settings.json", "dino-v1.json", "happy-ball-v1.json", "sokoban-v1.json" };
            foreach (var name in names) File.WriteAllBytes(Path.Combine(directory, name), new byte[] { 0, 255, 13, 10, 42 });
            Save(store, 1); Save(store, 2); File.WriteAllText(store.SavePath, "broken"); File.WriteAllText(store.SavePath + ".bak", "broken");
            Check(store.TryResetCorrupt(out var error), error); Save(store, 3);
            foreach (var name in names) Check(Convert.ToBase64String(File.ReadAllBytes(Path.Combine(directory, name))) == "AP8NCio=", "other game/settings changed: " + name);
        });
    }
    private static void FileLimits()
    {
        WithStore((store, directory) => {
            File.WriteAllBytes(store.SavePath, new byte[] { 0xff, 0xfe, 0xff }); Check(store.Load().Status == TetrisLoadStatus.Corrupt, "invalid UTF8 classification");
            File.WriteAllBytes(store.SavePath, new byte[TetrisSaveCodec.MaximumLength + 1]); Check(store.Load().Status == TetrisLoadStatus.Corrupt, "oversize classification");
            Check(!store.TrySave(new TetrisSaveData(), out var error), "oversize silently overwritten");
        });
    }
    private static TetrisSaveData Active()
    {
        var game = new TetrisGame(0x12345678); game.Execute(TetrisCommand.Hold); game.Execute(TetrisCommand.Left); game.Execute(TetrisCommand.Clockwise);
        for (int i = 0; i < 7; i++) game.Tick(true);
        return new TetrisSaveData { Game = game.GetSnapshot(), BestScore = long.MaxValue };
    }
    private static void DynamicRoundTrip()
    {
        var data = Active(); data.Game.Score = long.MaxValue - 10; data.Game.ElapsedTicks = 9007199254740993L; data.Game.PieceSerial = 9007199254740993L;
        var json = Encode(data); var decoded = Decode(json); Check(json == Encode(decoded), "fields not exact after round trip");
        var root = JObject.Parse(json); foreach (var name in new[] { "score", "elapsedTicks", "pieceSerial" }) Check(root["game"][name].Type == JTokenType.String, "long not stored as text");
        var a = new TetrisGame(data.Game); var b = new TetrisGame(decoded.Game);
        for (int i = 0; i < 240; i++)
        {
            if (i % 17 == 0) { a.Execute(TetrisCommand.Left); b.Execute(TetrisCommand.Left); }
            if (i % 31 == 0) { a.Execute(TetrisCommand.Clockwise); b.Execute(TetrisCommand.Clockwise); }
            if (i % 53 == 0) { a.Execute(TetrisCommand.HardDrop); b.Execute(TetrisCommand.HardDrop); }
            a.Tick(i % 3 == 0); b.Tick(i % 3 == 0);
            Check(Encode(new TetrisSaveData { Game = a.GetSnapshot(), BestScore = long.MaxValue }) == Encode(new TetrisSaveData { Game = b.GetSnapshot(), BestScore = long.MaxValue }), "future diverged at " + i);
        }
    }
    private static void ContactRoundTrip()
    {
        var game = new TetrisGame(Active().Game); game.Execute(TetrisCommand.HardDrop); game.Execute(TetrisCommand.Hold);
        for (int i = 0; i < 100 && !game.GetSnapshot().LockStarted; i++) game.Tick(true);
        Check(game.GetSnapshot().LockStarted, "fixture never contacted stack");
        if (!game.Execute(TetrisCommand.Left).Changed) game.Execute(TetrisCommand.Right);
        for (int i = 0; i < 5; i++) game.Tick(false);
        var source = game.GetSnapshot();
        Check(source.LockStarted && source.LockResets > 0 && source.LockTicks > 0 && source.LockTicks < 30 && source.GravityTicks > 0, "fixture lacks dynamic lock and gravity state");
        bool occupied = false; foreach (var cell in source.Cells) occupied |= cell != TetrisPiece.None;
        Check(occupied && source.HoldPiece != TetrisPiece.None && !source.CanHold, "fixture lacks stack and used hold");
        var decoded = Decode(Encode(new TetrisSaveData { Game = source, BestScore = long.MaxValue }));
        var restored = new TetrisGame(decoded.Game);
        for (int i = 0; i < 45; i++)
        {
            game.Tick(false); restored.Tick(false);
            Check(Encode(new TetrisSaveData { Game = game.GetSnapshot(), BestScore = long.MaxValue }) == Encode(new TetrisSaveData { Game = restored.GetSnapshot(), BestScore = long.MaxValue }), "lock timing diverged at " + i);
        }
    }
    private static void SnapshotShape()
    {
        var root = JObject.Parse(Encode(Active())); var game = (JObject)root["game"];
        foreach (var property in game.Properties())
        {
            var missing = (JObject)root.DeepClone(); ((JObject)missing["game"]).Remove(property.Name); Rejected(missing.ToString());
            var nil = (JObject)root.DeepClone(); nil["game"][property.Name] = null; Rejected(nil.ToString());
            var wrong = (JObject)root.DeepClone(); wrong["game"][property.Name] = new JObject(); Rejected(wrong.ToString());
        }
        foreach (var name in new[] { "cells", "nextQueue", "bag" })
        { var bad = (JObject)root.DeepClone(); bad["game"][name][0] = null; Rejected(bad.ToString()); }
        foreach (var token in new JToken[] { 1, "1", "i", "Unknown" })
        { var bad = (JObject)root.DeepClone(); bad["game"]["activePiece"] = token; Rejected(bad.ToString()); }
        var shortCells = (JObject)root.DeepClone(); ((JArray)shortCells["game"]["cells"]).RemoveAt(0); Rejected(shortCells.ToString());
        var unknown = (JObject)root.DeepClone(); unknown["game"]["extra"] = 0; Rejected(unknown.ToString());
        var numericScore = (JObject)root.DeepClone(); numericScore["game"]["score"] = 1; Rejected(numericScore.ToString());
        var future = (JObject)root.DeepClone(); future["game"]["rulesVersion"] = 2; future["game"]["cells"] = false; Rejected(future.ToString(), TetrisLoadStatus.UnsupportedVersion);
        var overflow = (JObject)root.DeepClone(); overflow["game"]["rngState"] = 4294967296L; Rejected(overflow.ToString());
    }
    private static void DefensiveCopy()
    {
        var data = Active(); var copy = data.Copy(); var before = Encode(data); copy.Game.Cells[219] = TetrisPiece.I; copy.Game.NextQueue[0] = TetrisPiece.O;
        Check(Encode(data) == before, "save copy shares arrays");
    }
    private static void TerminalResult()
    {
        var live = Active(); live.ResultRecorded = true; Check(!live.TryValidate(out var error), "live result recorded");
        var game = new TetrisGame(3); for (int i = 0; i < 100 && !game.GetSnapshot().GameOver; i++) game.Execute(TetrisCommand.HardDrop);
        var terminal = new TetrisSaveData { Game = game.GetSnapshot(), BestScore = long.MaxValue, ResultRecorded = true };
        Check(terminal.Game.GameOver && Decode(Encode(terminal)).ResultRecorded, "terminal result did not survive");
    }
    private static void Save(TetrisSaveStore store, long best) { Check(store.TrySave(new TetrisSaveData { BestScore = best }, out var error), "save: " + error); }
    private static void WithStore(Action<TetrisSaveStore, string> test)
    {
        string directory = Path.Combine(Path.GetTempPath(), "xanadu-tetris-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try { test(new TetrisSaveStore(directory), directory); } finally { Directory.Delete(directory, true); }
    }
}
