using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Watermelon;
using XanaduYonder.Runtime.Watermelon;

internal static class Program
{
    private static int passed, failed;
    private static int Main()
    {
        Check("Complete motion, queue, themes and preferences round trip", RoundTrip);
        Check("Literal schema reads without serializer defaults", LiteralFixture);
        Check("Copy isolates the entire mutable snapshot", CopyIsolation);
        Check("All required fields reject absence and null", RequiredFields);
        Check("All object levels reject unexpected fields", UnexpectedFields);
        Check("No integer, boolean or floating point coercion", InvalidTypes);
        Check("Schema, theme, score and core invariants are enforced", InvalidStates);
        Check("NaN, infinity and float overflow are rejected", InvalidFloatingPoint);
        Check("Malformed, duplicate, trailing and deep JSON is rejected", MalformedDocuments);
        Check("UTF8 and character size limits are enforced", SizeLimits);
        Check("Every invalid encode rejects before serialization", InvalidEncode);
        Check("New game and terminal game round trip", BoundaryStates);
        Check("Absent file starts quietly with independent filename", MissingSave);
        Check("Primary persists through a new store instance", PrimaryRoundTrip);
        Check("Corrupt primary recovers the previous valid backup", BackupRecovery);
        Check("Corrupt primary never replaces a valid backup", PreserveBackup);
        Check("Two invalid files return no partial state", InvalidFiles);
        Check("Invalid UTF8 primary recovers backup", InvalidUtf8);
        Check("Oversized primary recovers backup", OversizedFile);
        Check("Invalid writes preserve primary and backup bytes", InvalidWrite);
        Check("Temporary write failure preserves primary", TemporaryFailure);
        Check("Other games remain byte-identical", ExistingFiles);
        Check("Orphan temporary file is never restored", OrphanTemporary);
        Check("Decoded objects have independent ownership", DecodeIsolation);
        Console.WriteLine("Watermelon persistence checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static WatermelonSaveData Example()
    {
        return new WatermelonSaveData {
            Theme = 2, BestScore = 1234, GoalAcknowledged = true,
            Game = new WatermelonSnapshot {
                Score = 123, CurrentLevel = 2, NextLevel = 4, RandomState = uint.MaxValue,
                NextId = 17, DropCooldown = .21f, DangerTime = .25f,
                Balls = new[] {
                    new WatermelonBallState { Id = 4, Level = 10, X = 2.5f, Y = 1.25f, VelocityX = -.123f,
                        VelocityY = 2.12f, Angle = -123.45f, AngularVelocity = 89.1f, Age = 42.5f, Contacted = true },
                    new WatermelonBallState { Id = 16, Level = 0, X = 3.12f, Y = 6.71f, VelocityY = -1.25f,
                        Angle = 4.1f, Age = .125f }
                }
            }
        };
    }
    private static JObject Json() { return JObject.Parse(WatermelonSaveCodec.Encode(Example())); }
    private static void RoundTrip()
    {
        for (int theme = 0; theme < 3; theme++) {
            var data = Example(); data.Theme = theme;
            var json = WatermelonSaveCodec.Encode(data);
            Assert(WatermelonSaveCodec.TryDecode(json, out var result, out var reason), reason);
            Equal(data, result);
        }
    }
    private static void LiteralFixture()
    {
        const string json = "{\"schemaVersion\":1,\"gameId\":\"watermelon\",\"theme\":0,\"bestScore\":7,\"goalAcknowledged\":false,\"game\":{\"balls\":[],\"score\":0,\"currentLevel\":1,\"nextLevel\":4,\"randomState\":4294967295,\"nextId\":1,\"dropCooldown\":0,\"dangerTime\":0,\"gameOver\":false}}";
        Assert(WatermelonSaveCodec.TryDecode(json, out var result, out var reason), reason);
        Assert(result.Game.Balls.Length == 0 && result.Game.RandomState == uint.MaxValue && result.BestScore == 7 && result.Game.NextLevel == 4, "Fixture values changed.");
    }
    private static void CopyIsolation()
    {
        var original = Example(); var copy = original.Copy();
        copy.Game.Balls[0].X = 5; copy.Game.Balls[1] = null; copy.Theme = 0;
        Assert(original.Game.Balls[0].X == 2.5f && original.Game.Balls[1] != null && original.Theme == 2, "Copy aliases input.");
    }
    private static void RequiredFields()
    {
        foreach (var path in new[] { "", "game", "game.balls[0]" }) {
            var template = Json(); var fields = ((JObject)(path == "" ? template : template.SelectToken(path))).Properties().Select(p => p.Name).ToArray();
            foreach (var field in fields) {
                var absent = Json(); var obj = (JObject)(path == "" ? absent : absent.SelectToken(path)); obj.Remove(field); Reject(absent.ToString());
                var nullValue = Json(); obj = (JObject)(path == "" ? nullValue : nullValue.SelectToken(path)); obj[field] = null; Reject(nullValue.ToString());
            }
        }
    }
    private static void UnexpectedFields()
    {
        foreach (var path in new[] { "", "game", "game.balls[0]" }) {
            var root = Json(); ((JObject)(path == "" ? root : root.SelectToken(path)))["surprise"] = 1; Reject(root.ToString());
        }
    }
    private static void InvalidTypes()
    {
        foreach (var path in new[] { "schemaVersion", "theme", "bestScore", "game.score", "game.currentLevel", "game.nextLevel", "game.randomState", "game.nextId", "game.balls[0].id", "game.balls[0].level" })
            foreach (var token in new JToken[] { new JValue("1"), new JValue(1.0), new JValue(true), new JArray() }) { var root = Json(); root.SelectToken(path).Replace(token.DeepClone()); Reject(root.ToString()); }
        foreach (var path in new[] { "goalAcknowledged", "game.gameOver", "game.balls[0].contacted" }) {
            var root = Json(); root.SelectToken(path).Replace(1); Reject(root.ToString());
        }
        foreach (var path in new[] { "game.dropCooldown", "game.dangerTime", "game.balls[0].x", "game.balls[0].y", "game.balls[0].velocityX", "game.balls[0].velocityY", "game.balls[0].angle", "game.balls[0].angularVelocity", "game.balls[0].age" }) {
            var root = Json(); root.SelectToken(path).Replace("0.25"); Reject(root.ToString());
        }
        var bad = Json(); bad["gameId"] = 6; Reject(bad.ToString());
        bad = Json(); bad["game"]["balls"] = new JObject(); Reject(bad.ToString());
        bad = Json(); bad["game"]["balls"][0] = null; Reject(bad.ToString());
    }
    private static void InvalidStates()
    {
        foreach (var pair in new[] { "schemaVersion:2", "theme:-1", "theme:3", "bestScore:122", "game.score:-1", "game.nextId:16", "game.currentLevel:5", "game.nextLevel:5", "game.randomState:0", "game.balls[0].level:11", "game.balls[0].id:16", "game.balls[0].age:-1" }) {
            var parts = pair.Split(':'); var root = Json(); root.SelectToken(parts[0]).Replace(int.Parse(parts[1])); Reject(root.ToString());
        }
        var bad = Json(); bad["gameId"] = "sheep"; Reject(bad.ToString());
        bad = Json(); bad["game"]["gameOver"] = true; Reject(bad.ToString());
        bad = Json(); bad["game"]["randomState"] = 4294967296L; Reject(bad.ToString());
        bad = Json(); bad["bestScore"] = 2147483648L; Reject(bad.ToString());
        bad = Json(); var balls = (JArray)bad["game"]["balls"]; while (balls.Count <= WatermelonRules.MaxBalls) balls.Add(balls[0].DeepClone()); Reject(bad.ToString());
    }
    private static void InvalidFloatingPoint()
    {
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue }) {
            var root = Json(); root["game"]["balls"][0]["angle"] = new JValue(value); Reject(root.ToString(Formatting.None));
        }
        Reject(WatermelonSaveCodec.Encode(Example()).Replace("\"dropCooldown\":0.21", "\"dropCooldown\":NaN"));
    }
    private static void MalformedDocuments()
    {
        foreach (var value in new[] { null, "", "{", "[]", "null", "true", "{\"a\":1,\"a\":2}", WatermelonSaveCodec.Encode(Example()) + "{}", "{\"a\": [[[[[[[[[[0]]]]]]]]]]}" }) Reject(value);
        Reject(WatermelonSaveCodec.Encode(Example()).Replace("\"theme\":2", "\"theme\":2,\"theme\":0"));
        Reject(WatermelonSaveCodec.Encode(Example()).Replace("\"score\":123", "\"score\":123,\"score\":0"));
        Reject(WatermelonSaveCodec.Encode(Example()).Replace("\"id\":4", "\"id\":4,\"id\":5"));
    }
    private static void SizeLimits()
    {
        Reject(new string(' ', WatermelonSaveCodec.MaximumLength + 1));
        Reject("{\"gameId\":\"" + new string('旸', WatermelonSaveCodec.MaximumLength / 2) + "\"}");
    }
    private static void InvalidEncode()
    {
        ExpectArgument(() => WatermelonSaveCodec.Encode(null));
        ExpectArgument(() => WatermelonSaveCodec.Encode(new WatermelonSaveData()));
        var bad = Example(); bad.Theme = 3; ExpectArgument(() => WatermelonSaveCodec.Encode(bad));
        bad = Example(); bad.Game.Balls[0].X = float.NaN; ExpectArgument(() => WatermelonSaveCodec.Encode(bad));
        bad = Example(); bad.BestScore = -1; ExpectArgument(() => WatermelonSaveCodec.Encode(bad));
    }
    private static void BoundaryStates()
    {
        var data = Example(); data.Game.Balls = new WatermelonBallState[0]; data.Game.Score = 0; data.Game.DangerTime = 0; data.GoalAcknowledged = false;
        Assert(WatermelonSaveCodec.TryDecode(WatermelonSaveCodec.Encode(data), out var decoded, out var reason), reason); Equal(data, decoded);
        data = Example(); data.Game.GameOver = true; data.Game.DangerTime = WatermelonRules.DangerDuration; data.Game.Score = int.MaxValue; data.BestScore = int.MaxValue;
        Assert(WatermelonSaveCodec.TryDecode(WatermelonSaveCodec.Encode(data), out decoded, out reason), reason); Equal(data, decoded);
    }
    private static void MissingSave() { InDirectory(folder => { var store = new WatermelonSaveStore(folder); Assert(Path.GetFileName(store.SavePath) == "watermelon-v1.json", "Wrong filename."); Assert(store.Load(out var warning) == null && warning == null, "Absent save should be quiet."); }); }
    private static void PrimaryRoundTrip() { InDirectory(folder => { var store = new WatermelonSaveStore(folder); Save(store, Example()); var loaded = new WatermelonSaveStore(folder).Load(out var warning); Assert(warning == null, warning); Equal(Example(), loaded); Assert(!File.Exists(store.SavePath + ".tmp"), "Temporary retained."); }); }
    private static void PrepareBackup(WatermelonSaveStore store) { Save(store, Example()); var next = Example(); next.Theme = 1; Save(store, next); }
    private static void BackupRecovery() { InDirectory(folder => { var store = new WatermelonSaveStore(folder); PrepareBackup(store); File.WriteAllText(store.SavePath, "broken"); var result = store.Load(out var warning); Assert(warning != null, "Recovery warning absent."); Equal(Example(), result); }); }
    private static void PreserveBackup() { InDirectory(folder => { var store = new WatermelonSaveStore(folder); PrepareBackup(store); var backup = File.ReadAllBytes(store.SavePath + ".bak"); File.WriteAllText(store.SavePath, "broken"); var next = Example(); next.Theme = 0; Save(store, next); Assert(backup.SequenceEqual(File.ReadAllBytes(store.SavePath + ".bak")), "Valid backup overwritten."); Equal(next, store.Load(out var warning)); }); }
    private static void InvalidFiles() { InDirectory(folder => { var store = new WatermelonSaveStore(folder); File.WriteAllText(store.SavePath, "broken"); File.WriteAllText(store.SavePath + ".bak", "{}"); Assert(store.Load(out var warning) == null && warning != null, "Invalid data restored."); }); }
    private static void InvalidUtf8() { InDirectory(folder => { var store = new WatermelonSaveStore(folder); PrepareBackup(store); File.WriteAllBytes(store.SavePath, new byte[] { 0xff, 0xfe, 0xff }); Equal(Example(), store.Load(out var warning)); Assert(warning != null, "UTF8 recovery not reported."); }); }
    private static void OversizedFile() { InDirectory(folder => { var store = new WatermelonSaveStore(folder); PrepareBackup(store); File.WriteAllBytes(store.SavePath, new byte[WatermelonSaveCodec.MaximumLength + 1]); Equal(Example(), store.Load(out var warning)); Assert(warning != null, "Size recovery not reported."); }); }
    private static void InvalidWrite() { InDirectory(folder => { var store = new WatermelonSaveStore(folder); PrepareBackup(store); var primary = File.ReadAllBytes(store.SavePath); var backup = File.ReadAllBytes(store.SavePath + ".bak"); var bad = Example(); bad.Game.Balls[0].Level = 11; Assert(!store.TrySave(bad, out var error) && error != null, "Invalid save accepted."); Assert(primary.SequenceEqual(File.ReadAllBytes(store.SavePath)) && backup.SequenceEqual(File.ReadAllBytes(store.SavePath + ".bak")), "Validation touched files."); }); }
    private static void TemporaryFailure() { InDirectory(folder => { var store = new WatermelonSaveStore(folder); Save(store, Example()); var primary = File.ReadAllBytes(store.SavePath); Directory.CreateDirectory(store.SavePath + ".tmp"); Assert(!store.TrySave(Example(), out var error) && error != null, "Write failure not returned."); Assert(primary.SequenceEqual(File.ReadAllBytes(store.SavePath)), "Failed write changed primary."); }); }
    private static void ExistingFiles() { InDirectory(folder => { var names = new[] { "save-v1.json", "jump-v1.json", "sheep-v1.json", "idiom-v1.json", "sudoku-v1.json" }; foreach (var name in names) File.WriteAllText(Path.Combine(folder, name), name); var store = new WatermelonSaveStore(folder); Save(store, Example()); store.Load(out var warning); foreach (var name in names) Assert(File.ReadAllText(Path.Combine(folder, name)) == name, "Touched " + name); }); }
    private static void OrphanTemporary() { InDirectory(folder => { var store = new WatermelonSaveStore(folder); File.WriteAllText(store.SavePath + ".tmp", WatermelonSaveCodec.Encode(Example())); Assert(store.Load(out var warning) == null && warning == null, "Uncommitted file restored."); }); }
    private static void DecodeIsolation()
    {
        var json = WatermelonSaveCodec.Encode(Example());
        Assert(WatermelonSaveCodec.TryDecode(json, out var first, out var reason), reason); Assert(WatermelonSaveCodec.TryDecode(json, out var second, out reason), reason);
        first.Game.Balls[0].Level = 0; Assert(second.Game.Balls[0].Level == 10, "Decoded ball references shared.");
    }
    private static void Equal(WatermelonSaveData expected, WatermelonSaveData actual) { Assert(actual != null && WatermelonSaveCodec.Encode(expected) == WatermelonSaveCodec.Encode(actual), "State changed during persistence."); }
    private static void Save(WatermelonSaveStore store, WatermelonSaveData data) { Assert(store.TrySave(data, out var error), error); }
    private static void Reject(string json) { Assert(!WatermelonSaveCodec.TryDecode(json, out var data, out var reason) && data == null && !string.IsNullOrEmpty(reason), "Accepted malformed payload or returned partial state."); }
    private static void ExpectArgument(Action body) { try { body(); } catch (ArgumentException) { return; } throw new Exception("Invalid state did not throw ArgumentException."); }
    private static void InDirectory(Action<string> body) { var folder = Path.Combine(Path.GetTempPath(), "xy-watermelon-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); try { body(folder); } finally { Directory.Delete(folder, true); } }
    private static void Check(string name, Action body) { try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); } }
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
}
