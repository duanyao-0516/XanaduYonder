using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.HappyBall;
using XanaduYonder.Runtime.HappyBall;

internal static class Program
{
    private static int passed, failed;
    private static readonly string[] ObjectPaths = { "", "game", "game.layers[0]", "game.layers[0].sectors[0]", "game.layers[0].walls[0]" };
    private static int Main()
    {
        Check("Normal Armed Spent ready and result snapshots round trip", RoundTrip);
        Check("Ascending apex and descending smash saves replay identically", ReboundRoundTrip);
        Check("Legacy spent payload upgrades pending points once and preserves best score", LegacySpent);
        Check("Literal v1 Normal Armed and rebound saves migrate once across rebased origins", LegacyRulesMigration);
        Check("Invalid v1 geometry rejects before spacing migration can repair it", LegacyRulesBoundary);
        Check("Legacy short fins and new high fins retain the same strict disk schema", WallHeightRoundTrip);
        Check("Literal schema has no serializer-default dependence", Literal);
        Check("Deep copies and decoded sessions do not alias", Isolation);
        Check("Every field rejects missing null and extras", FieldContracts);
        Check("Strict field types never coerce strings floats or booleans", Types);
        Check("Unknown schemas rules ids and enums reject", Versions);
        Check("Score strings preserve Int64 and reject invalid decimal forms", Scores);
        Check("Motion geometry and collections are bounded", Bounds);
        Check("Result recording and ready state remain consistent", SessionState);
        Check("Standard JSON syntax rejects JavaScript extensions", StrictSyntax);
        Check("Duplicate fields reject at every nesting level", Duplicates);
        Check("Length depth UTF8 limits and trailing documents reject", Limits);
        Check("Invalid encoding fails before returning a payload", InvalidEncode);
        Check("Absent and orphan temp files start quietly", MissingFiles);
        Check("Flushed primary round trips in new store", Primary);
        Check("Valid primary becomes backup and corrupt primary recovers", Recovery);
        Check("Recovered corrupt primary cannot replace valid backup", PreserveBackup);
        Check("Invalid primary and backup never yield partial data", InvalidFiles);
        Check("Bad UTF8 and oversized primary recover backup", BadFiles);
        Check("Invalid writes retain primary backup and temporary bytes", InvalidWrite);
        Check("Failed temp and replacement writes preserve committed files", WriteFailure);
        Check("All other games and settings stay byte identical", OtherFiles);
        Console.WriteLine("HappyBall persistence checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }
    private static HappyBallSaveData Example()
    {
        var layers = new HappyBallLayer[8];
        for (int i = 0; i < layers.Length; i++) layers[i] = new HappyBallLayer {
            Index = i, Y = -i * HappyBallRules.LayerSpacing, State = i < 3 ? HappyBallLayerState.Passed : HappyBallLayerState.Active,
            Sectors = new[] { new HappyBallSector { StartAngle = .5, SweepAngle = 3.5 }, new HappyBallSector { StartAngle = 4, SweepAngle = .5, Dangerous = true } },
            Walls = new[] { new HappyBallWall { Angle = 2, WidthAngle = .16, Height = .3 } }
        };
        return new HappyBallSaveData { BestScore = long.MaxValue, HasStarted = true, Game = new HappyBallSnapshot {
            RandomState = uint.MaxValue, NextLayerIndex = 8, OriginLayer = 0, PassedLayers = 3, NaturalStreak = 2,
            TowerAngle = .125, BallY = -3 * HappyBallRules.LayerSpacing + .55, BallVelocity = -1.25, Score = 1234567890123456789L, Layers = layers
        } };
    }
    private static JObject Json() { return JObject.Parse(HappyBallSaveCodec.Encode(Example())); }
    private static JObject ObjectAt(JObject root, string path) { return path == "" ? root : (JObject)root.SelectToken(path); }
    private static void Mutate(string path, object value) { var root = Json(); root.SelectToken(path).Replace(value == null ? JValue.CreateNull() : JToken.FromObject(value)); Reject(root.ToString(Formatting.None)); }
    private static void RoundTrip()
    {
        for (int mode = 0; mode < 5; mode++) {
            var data = Example();
            if (mode == 1) { data.Game.Power = HappyBallPower.Armed; data.Game.NaturalStreak = 3; }
            if (mode == 2) { data.Game.Power = HappyBallPower.Spent; data.Game.ImpactLayerIndex = 3; data.Game.Layers[3].State = HappyBallLayerState.Broken; data.Game.NaturalStreak = 0; data.Game.BallY = data.Game.Layers[3].Y + .05; }
            if (mode == 3) { data.HasStarted = false; data.Game.Score = 0; data.Game.PassedLayers = 0; data.Game.NaturalStreak = 0; data.Game.BallY = .5; data.Game.BallVelocity = 0; foreach (var layer in data.Game.Layers) layer.State = HappyBallLayerState.Active; }
            if (mode == 4) { data.Game.GameOver = true; data.ResultRecorded = true; }
            Assert(HappyBallSaveCodec.TryDecode(HappyBallSaveCodec.Encode(data), out var result, out var reason), reason); Equal(data, result);
        }
    }
    private static void Literal()
    {
        const string text = "{\"schemaVersion\":1,\"gameId\":\"happy-ball\",\"bestScore\":\"9223372036854775807\",\"hasStarted\":false,\"resultRecorded\":false,\"game\":{\"rulesVersion\":1,\"randomState\":4294967295,\"nextLayerIndex\":1,\"originLayer\":0,\"passedLayers\":0,\"naturalStreak\":0,\"towerAngle\":0,\"ballY\":0.5,\"ballVelocity\":0,\"score\":\"0\",\"power\":\"Normal\",\"impactLayerIndex\":-1,\"gameOver\":false,\"layers\":[{\"index\":0,\"y\":0,\"state\":\"Active\",\"sectors\":[{\"startAngle\":0.5,\"sweepAngle\":4,\"dangerous\":false}],\"walls\":[]}]}}";
        Assert(HappyBallSaveCodec.TryDecode(text, out var result, out var reason), reason);
        Assert(result.BestScore == long.MaxValue && result.Game.RandomState == uint.MaxValue && !result.HasStarted, "literal changed");
    }
    private static void ReboundRoundTrip()
    {
        var data = Example();
        data.Game.Power = HappyBallPower.Armed; data.Game.NaturalStreak = 3; data.Game.Score = 6;
        data.Game.TowerAngle = 0;
        data.Game.Layers[3].Sectors = new[] { new HappyBallSector { SweepAngle = HappyBallGeometry.Tau, Dangerous = true } };
        data.Game.Layers[3].Walls = new HappyBallWall[0];
        data.Game.BallY = data.Game.Layers[3].Y + HappyBallRules.BallRadius + .01; data.Game.BallVelocity = -4;
        var game = new HappyBallGame(data.Game, false);
        foreach (double duration in new[] { .1, .23, .2 })
        {
            game.Step(duration, 0); data.Game = game.GetSnapshot();
            Assert(data.Game.Power == HappyBallPower.Spent && data.Game.Score == 10, "smash rebound/score state changed");
            Assert(HappyBallSaveCodec.TryDecode(HappyBallSaveCodec.Encode(data), out var restored, out var reason), reason);
            Equal(data, restored);
            var replay = new HappyBallGame(restored.Game, false);
            game.Step(HappyBallRules.FixedStep, 0); replay.Step(HappyBallRules.FixedStep, 0);
            data.Game = game.GetSnapshot(); restored.Game = replay.GetSnapshot(); Equal(data, restored);
            game = replay;
        }
    }
    private static void LegacySpent()
    {
        // Literal disk payload from the old rules contract: the plate is already
        // broken, its three-pass streak remains, and its impact points are pending.
        const string legacy = "{\"schemaVersion\":1,\"gameId\":\"happy-ball\",\"bestScore\":\"6\",\"hasStarted\":true,\"resultRecorded\":false,\"game\":{\"rulesVersion\":1,\"randomState\":77,\"nextLayerIndex\":5,\"originLayer\":0,\"passedLayers\":3,\"naturalStreak\":3,\"towerAngle\":0,\"ballY\":-4.85,\"ballVelocity\":-12,\"score\":\"6\",\"power\":\"Spent\",\"impactLayerIndex\":3,\"gameOver\":false,\"layers\":[{\"index\":3,\"y\":-4.95,\"state\":\"Broken\",\"sectors\":[{\"startAngle\":0,\"sweepAngle\":6.283185307179586,\"dangerous\":true}],\"walls\":[]},{\"index\":4,\"y\":-6.6,\"state\":\"Active\",\"sectors\":[{\"startAngle\":0,\"sweepAngle\":6.283185307179586,\"dangerous\":true}],\"walls\":[]}]}}";
        Assert(HappyBallSaveCodec.TryDecode(legacy, out var data, out var reason), reason);
        Assert(data.Game.Score == 10 && data.BestScore == 10 && data.Game.NaturalStreak == 0,
            "legacy pending impact points or matching best score were lost");
        Assert(Math.Abs(data.Game.BallY - (-4.85 - 3 * .75)) < 1e-12 && data.Game.BallVelocity == -12,
            "legacy continuation changed its relative in-flight position or velocity");
        for (int iteration = 0; iteration < 2; iteration++)
        {
            Assert(HappyBallSaveCodec.TryDecode(HappyBallSaveCodec.Encode(data), out data, out reason), reason);
            Assert(data.Game.Score == 10 && data.BestScore == 10, "legacy impact score applied twice");
        }
        var game = new HappyBallGame(data.Game, false);
        for (int step = 0; step < 30 && !game.GameOver; step++) game.Step(HappyBallRules.FixedStep, 0);
        Assert(game.GameOver && game.Score == 10 && game.GetSnapshot().PassedLayers == 4,
            "legacy physical passage rescored impact or retained smash");
        Reject(legacy.Replace("\"naturalStreak\":3", "\"naturalStreak\":2"));
        Reject(legacy.Replace("\"bestScore\":\"6\"", "\"bestScore\":\"1\""));
        Reject(legacy.Replace("\"impactLayerIndex\":3", "\"impactLayerIndex\":4"));
    }
    private static void WallHeightRoundTrip()
    {
        foreach (double height in new[] { .3, .5, .58, .70, .75, .90, 1.20, 1.50 })
        {
            var data = Example();
            foreach (var layer in data.Game.Layers) layer.Walls[0].Height = height;
            Assert(HappyBallSaveCodec.TryDecode(HappyBallSaveCodec.Encode(data), out var restored, out var reason), reason);
            Equal(data, restored);
            Assert(restored.SchemaVersion == 1 && restored.Game.RulesVersion == 2 &&
                restored.Game.Layers[3].Walls[0].Height == height, "fin height changed or old save schema was discarded");
        }
        var invalid = Example(); invalid.Game.Layers[3].Walls[0].Height = 1.5001;
        ExpectArgument(() => HappyBallSaveCodec.Encode(invalid));
        Mutate("game.layers[0].walls[0].height", 1.5001);
    }
    // This is a real v1 disk shape: its coordinates and wall limits are constants,
    // deliberately independent of the current rules and serializer defaults.
    private const string LegacyNormalLiteral = "{\"schemaVersion\":1,\"gameId\":\"happy-ball\",\"bestScore\":\"6\",\"hasStarted\":true,\"resultRecorded\":false,\"game\":{\"rulesVersion\":1,\"randomState\":77,\"nextLayerIndex\":5,\"originLayer\":0,\"passedLayers\":3,\"naturalStreak\":2,\"towerAngle\":0,\"ballY\":-4.4,\"ballVelocity\":-1.25,\"score\":\"6\",\"power\":\"Normal\",\"impactLayerIndex\":-1,\"gameOver\":false,\"layers\":[{\"index\":3,\"y\":-4.95,\"state\":\"Active\",\"sectors\":[{\"startAngle\":0,\"sweepAngle\":6.283185307179586,\"dangerous\":false}],\"walls\":[{\"angle\":2,\"widthAngle\":0.16,\"height\":0.3}]},{\"index\":4,\"y\":-6.6,\"state\":\"Active\",\"sectors\":[{\"startAngle\":0,\"sweepAngle\":6.283185307179586,\"dangerous\":true}],\"walls\":[]}]}}";

    private static void LegacyRulesMigration()
    {
        foreach (int origin in new[] { 0, 64 })
        for (int mode = 0; mode < 5; mode++)
        {
            // Normal, Armed, paid Spent ascending/descending, and the earlier
            // deferred-score Spent each exercise the real strict JSON decoder.
            var root = JObject.Parse(LegacyNormalLiteral); var legacy = (JObject)root["game"];
            bool spent = mode >= 2; int current = origin + 3;
            int streak = mode == 0 ? 2 : mode == 1 || mode == 4 ? 3 : 0;
            long score = origin == 0 ? (spent && mode != 4 ? 10 : 6) : 120;
            long expectedScore = score + (mode == 4 ? 4 : 0);
            string power = spent ? "Spent" : mode == 1 ? "Armed" : "Normal";
            double ballY = mode == 2 ? -4.0 : mode == 3 ? -3.8 : mode == 4 ? -4.85 : -4.4;
            double velocity = mode == 2 ? 7 : mode == 3 ? -6 : mode == 4 ? -12 : -1.25;
            root["bestScore"] = score.ToString(System.Globalization.CultureInfo.InvariantCulture);
            legacy["originLayer"] = origin; legacy["passedLayers"] = current; legacy["nextLayerIndex"] = current + 2;
            legacy["naturalStreak"] = streak; legacy["power"] = power; legacy["impactLayerIndex"] = spent ? current : -1;
            legacy["score"] = score.ToString(System.Globalization.CultureInfo.InvariantCulture);
            legacy["ballY"] = ballY; legacy["ballVelocity"] = velocity;
            var layers = (JArray)legacy["layers"];
            layers[0]["index"] = current; layers[1]["index"] = current + 1;
            layers[0]["state"] = spent ? "Broken" : "Active";
            string json = root.ToString(Formatting.None);
            Assert(HappyBallSaveCodec.TryDecode(json, out var data, out var reason), "mode=" + mode + " origin=" + origin + ": " + reason);
            Assert(data.SchemaVersion == 1 && data.Game.RulesVersion == 2, "old rules did not upgrade within schema 1");
            Assert(data.Game.OriginLayer == origin && data.Game.PassedLayers == current && data.Game.NextLayerIndex == current + 2,
                "migration changed logical progress or rebased origin");
            Assert(Math.Abs(data.Game.BallY - (ballY - 2.25)) < 1e-12 && data.Game.BallVelocity == velocity,
                "migration did not preserve ball motion relative to current layer");
            Assert(Math.Abs(data.Game.Layers[0].Y - -7.2) < 1e-12 && Math.Abs(data.Game.Layers[1].Y - -9.6) < 1e-12 &&
                Math.Abs((data.Game.BallY - data.Game.Layers[0].Y) - (ballY + 4.95)) < 1e-12,
                "v2 spacing or current-layer relative height changed");
            Assert(data.Game.Score == expectedScore && data.BestScore == expectedScore && data.Game.NaturalStreak == (spent ? 0 : streak),
                "migration lost or duplicated pending impact points");
            Assert(data.Game.RandomState == 77 && data.Game.TowerAngle == 0 && data.Game.Power.ToString() == power &&
                data.Game.ImpactLayerIndex == (spent ? current : -1) && data.Game.Layers[0].Walls[0].Height == .3,
                "migration changed saved motion, power, random cursor or old fin geometry");

            var raw = new HappyBallSnapshot { RulesVersion = 1, RandomState = 77, OriginLayer = origin,
                PassedLayers = current, NextLayerIndex = current + 2, NaturalStreak = streak, BallY = ballY,
                BallVelocity = velocity, Score = score, Power = spent ? HappyBallPower.Spent : mode == 1 ? HappyBallPower.Armed : HappyBallPower.Normal,
                ImpactLayerIndex = spent ? current : -1, Layers = new[] {
                    new HappyBallLayer { Index = current, Y = -4.95, State = spent ? HappyBallLayerState.Broken : HappyBallLayerState.Active,
                        Sectors = new[] { new HappyBallSector { SweepAngle = HappyBallGeometry.Tau } },
                        Walls = new[] { new HappyBallWall { Angle = 2, WidthAngle = .16, Height = .3 } } },
                    new HappyBallLayer { Index = current + 1, Y = -6.6, Sectors = new[] { new HappyBallSector { SweepAngle = HappyBallGeometry.Tau, Dangerous = true } }, Walls = new HappyBallWall[0] }
                } };
            var constructed = data.Copy(); constructed.Game = new HappyBallGame(raw, false).GetSnapshot();
            Equal(data, constructed);
            Assert(raw.RulesVersion == 1 && raw.BallY == ballY && raw.Layers[0].Y == -4.95 && raw.Score == score,
                "constructor normalization mutated the old caller snapshot");
            for (int iteration = 0; iteration < 2; iteration++)
            {
                Assert(HappyBallSaveCodec.TryDecode(HappyBallSaveCodec.Encode(data), out var reloaded, out reason), reason);
                Equal(data, reloaded); data = reloaded;
                Assert(data.Game.Score == expectedScore && data.BestScore == expectedScore && data.Game.RulesVersion == 2 &&
                    Math.Abs(data.Game.BallY - (ballY - 2.25)) < 1e-12, "migration reapplied points or coordinate shift on a v2 reload");
            }
        }
    }

    private static void LegacyRulesBoundary()
    {
        foreach (double height in new[] { .3, .5 })
        {
            var old = JObject.Parse(LegacyNormalLiteral); old.SelectToken("game.layers[0].walls[0].height").Replace(height);
            Assert(HappyBallSaveCodec.TryDecode(old.ToString(Formatting.None), out var migrated, out var reason), reason);
            Assert(migrated.Game.RulesVersion == 2 && migrated.Game.Layers[0].Walls[0].Height == height, "supported v1 fin changed during migration");
        }
        foreach (double height in new[] { .5001, .9, 1.5 })
        {
            var bad = JObject.Parse(LegacyNormalLiteral); bad.SelectToken("game.layers[0].walls[0].height").Replace(height);
            Reject(bad.ToString(Formatting.None));
        }
        foreach (double y in new[] { -4.85, -7.2 })
        {
            var bad = JObject.Parse(LegacyNormalLiteral); bad.SelectToken("game.layers[0].y").Replace(y);
            Reject(bad.ToString(Formatting.None));
        }
        // Migration would move the next slab farther away from this penetrated
        // ball. It must reject the invalid v1 state before that repair is possible.
        var embedded = JObject.Parse(LegacyNormalLiteral); embedded.SelectToken("game.ballY").Replace(-6.5);
        Reject(embedded.ToString(Formatting.None));
    }
    private static void Isolation()
    {
        var original = Example(); var copy = original.Copy(); copy.Game.Layers[0].Sectors[0].StartAngle = 1; copy.Game.Layers[0].Walls[0].Height = .4; copy.Game.Layers[1] = null;
        Assert(original.Game.Layers[0].Sectors[0].StartAngle == .5 && original.Game.Layers[0].Walls[0].Height == .3 && original.Game.Layers[1] != null, "copy aliases");
        string json = HappyBallSaveCodec.Encode(original); Assert(HappyBallSaveCodec.TryDecode(json, out var first, out var reason), reason); Assert(HappyBallSaveCodec.TryDecode(json, out var second, out reason), reason);
        first.Game.Layers[0].Sectors[0].StartAngle = 2; Assert(second.Game.Layers[0].Sectors[0].StartAngle == .5, "decode aliases");
    }
    private static void FieldContracts()
    {
        foreach (string path in ObjectPaths) {
            foreach (string field in ObjectAt(Json(), path).Properties().Select(p => p.Name).ToArray()) {
                var root = Json(); ObjectAt(root, path).Remove(field); Reject(root.ToString());
                root = Json(); ObjectAt(root, path)[field] = JValue.CreateNull(); Reject(root.ToString());
            }
            var extra = Json(); ObjectAt(extra, path)["extra"] = 1; Reject(extra.ToString());
        }
    }
    private static void Types()
    {
        foreach (string path in new[] { "schemaVersion", "game.rulesVersion", "game.randomState", "game.nextLayerIndex", "game.originLayer", "game.passedLayers", "game.naturalStreak", "game.impactLayerIndex", "game.layers[0].index" })
            foreach (object value in new object[] { "1", 1.0, true, new JArray() }) Mutate(path, value);
        foreach (string path in new[] { "hasStarted", "resultRecorded", "game.gameOver", "game.layers[0].sectors[0].dangerous" }) Mutate(path, 1);
        foreach (string path in new[] { "game.towerAngle", "game.ballY", "game.ballVelocity", "game.layers[0].y", "game.layers[0].sectors[0].startAngle", "game.layers[0].sectors[0].sweepAngle", "game.layers[0].walls[0].angle", "game.layers[0].walls[0].widthAngle", "game.layers[0].walls[0].height" }) Mutate(path, "0.5");
        Mutate("gameId", 1); Mutate("game.layers", new JObject()); Mutate("game.layers[0]", null); Mutate("game.layers[0].sectors[0]", null); Mutate("game.layers[0].walls[0]", null);
    }
    private static void Versions()
    {
        Mutate("schemaVersion", 2); Mutate("game.rulesVersion", 3); Mutate("gameId", "jump");
        foreach (string path in new[] { "game.power", "game.layers[0].state" }) foreach (object value in new object[] { "unknown", "0", 0, "normal" }) Mutate(path, value);
    }
    private static void Scores()
    {
        foreach (string path in new[] { "bestScore", "game.score" }) foreach (object value in new object[] { 1, 1.0, "", "-1", "+1", " 1", "1 ", "1e2", "1.0", "9223372036854775808", "٠١" }) Mutate(path, value);
        Mutate("bestScore", "1");
        string json = HappyBallSaveCodec.Encode(Example()); Assert(json.Contains("\"score\":\"1234567890123456789\"") && json.Contains("\"bestScore\":\"9223372036854775807\""), "64-bit score precision lost");
    }
    private static void Bounds()
    {
        Mutate("game.randomState", 0); Mutate("game.randomState", 4294967296L); Mutate("game.nextLayerIndex", 2147483648L);
        Mutate("game.ballY", 5000); Mutate("game.ballVelocity", 10001); Mutate("game.towerAngle", Math.PI * 2); Mutate("game.towerAngle", -.1);
        Mutate("game.layers[0].index", 1); Mutate("game.layers[0].y", .1); Mutate("game.layers[0].sectors[0].sweepAngle", 0); Mutate("game.layers[0].walls[0].height", 1.5001);
        foreach (string path in new[] { "game.layers", "game.layers[0].sectors", "game.layers[0].walls" }) {
            var root = Json(); var array = (JArray)root.SelectToken(path); while (array.Count <= 32) array.Add(array[0].DeepClone()); Reject(root.ToString());
        }
        foreach (string token in new[] { "NaN", "Infinity", "-Infinity", "1e999" }) Reject(HappyBallSaveCodec.Encode(Example()).Replace("\"towerAngle\":0.125", "\"towerAngle\":" + token));
    }
    private static void SessionState()
    {
        Mutate("resultRecorded", true); Mutate("hasStarted", false); Mutate("game.power", "Spent"); Mutate("game.power", "Armed"); Mutate("game.impactLayerIndex", 3);
        var data = Example(); data.Game.GameOver = true; data.HasStarted = false; Assert(!data.TryValidate(out var reason) && reason != null, "terminal unstarted accepted");
    }
    private static void StrictSyntax()
    {
        string json = HappyBallSaveCodec.Encode(Example());
        foreach (string value in new[] { json.Replace("\"schemaVersion\":1", "schemaVersion:1"), json.Replace("\"gameId\":\"happy-ball\"", "'gameId':'happy-ball'"), json.Replace("\"schemaVersion\":1", "\"schemaVersion\":01"), json.Replace("\"schemaVersion\":1", "\"schemaVersion\":+1"), json.Replace("\"schemaVersion\":1", "\"schemaVersion\":0x1"), json.Replace("\"towerAngle\":0.125", "\"towerAngle\":.125"), json.Replace("\"towerAngle\":0.125", "\"towerAngle\":1."), json.Insert(1, "/* comment */"), json.Insert(json.Length - 1, ","), json.Replace("\"gameId\":\"happy-ball\"", "\"gameId\":undefined") }) Reject(value);
        Reject(json.Replace("happy-ball", "happy\n-ball"));
    }
    private static void Duplicates()
    {
        foreach (string path in ObjectPaths) {
            var root = Json(); var obj = ObjectAt(root, path); string serialized = obj.ToString(Formatting.None); string field = obj.Properties().First().ToString(Formatting.None);
            string duplicate = serialized.Insert(1, field + ","); Reject(root.ToString(Formatting.None).Replace(serialized, duplicate));
        }
        Reject(HappyBallSaveCodec.Encode(Example()).Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schema\\u0056ersion\":1"));
    }
    private static void Limits()
    {
        foreach (string value in new[] { null, "", "{", "[]", "null", "true", HappyBallSaveCodec.Encode(Example()) + "{}", new string('[', 40) + "0" + new string(']', 40), new string(' ', HappyBallSaveCodec.MaximumLength + 1), "{\"gameId\":\"" + new string('球', HappyBallSaveCodec.MaximumLength / 2) + "\"}" }) Reject(value);
    }
    private static void InvalidEncode()
    {
        ExpectArgument(() => HappyBallSaveCodec.Encode(null)); ExpectArgument(() => HappyBallSaveCodec.Encode(new HappyBallSaveData()));
        var bad = Example(); bad.Game.Layers[0].Sectors[0].StartAngle = double.NaN; ExpectArgument(() => HappyBallSaveCodec.Encode(bad));
        bad = Example(); bad.SchemaVersion = 2; ExpectArgument(() => HappyBallSaveCodec.Encode(bad));
    }
    private static void MissingFiles() { InDirectory(folder => { var store = new HappyBallSaveStore(folder); Assert(Path.GetFileName(store.SavePath) == "happy-ball-v1.json", "wrong path"); Assert(store.Load(out var warning) == null && warning == null, "absent warning"); File.WriteAllText(store.SavePath + ".tmp", HappyBallSaveCodec.Encode(Example())); Assert(store.Load(out warning) == null && warning == null, "uncommitted temp restored"); }); }
    private static void Primary() { InDirectory(folder => { var store = new HappyBallSaveStore(folder); Save(store, Example()); Equal(Example(), new HappyBallSaveStore(folder).Load(out var warning)); Assert(warning == null && !File.Exists(store.SavePath + ".tmp"), "successful save incomplete"); }); }
    private static void Backup(HappyBallSaveStore store) { Save(store, Example()); var next = Example(); next.Game.TowerAngle = .5; Save(store, next); }
    private static void Recovery() { InDirectory(folder => { var store = new HappyBallSaveStore(folder); Backup(store); Assert(HappyBallSaveCodec.TryDecode(File.ReadAllText(store.SavePath + ".bak"), out var data, out var error), error); Equal(Example(), data); File.WriteAllText(store.SavePath, "broken"); Equal(Example(), store.Load(out var warning)); Assert(warning != null, "recovery warning missing"); }); }
    private static void PreserveBackup() { InDirectory(folder => { var store = new HappyBallSaveStore(folder); Backup(store); var backup = File.ReadAllBytes(store.SavePath + ".bak"); File.WriteAllText(store.SavePath, "broken"); var next = Example(); next.Game.TowerAngle = 1; Save(store, next); Equal(next, store.Load(out var warning)); Assert(backup.SequenceEqual(File.ReadAllBytes(store.SavePath + ".bak")), "backup overwritten by corrupt primary"); }); }
    private static void InvalidFiles() { InDirectory(folder => { var store = new HappyBallSaveStore(folder); File.WriteAllText(store.SavePath, "broken"); File.WriteAllText(store.SavePath + ".bak", "{}"); Assert(store.Load(out var warning) == null && warning != null, "invalid restored"); }); }
    private static void BadFiles() { InDirectory(folder => { var store = new HappyBallSaveStore(folder); Backup(store); File.WriteAllBytes(store.SavePath, new byte[] { 0xff, 0xfe, 0xff }); Equal(Example(), store.Load(out var warning)); Assert(warning != null, "bad UTF8 quiet"); File.WriteAllBytes(store.SavePath, new byte[HappyBallSaveCodec.MaximumLength + 1]); Equal(Example(), store.Load(out warning)); Assert(warning != null, "oversize quiet"); }); }
    private static void InvalidWrite() { InDirectory(folder => { var store = new HappyBallSaveStore(folder); Backup(store); var primary = File.ReadAllBytes(store.SavePath); var backup = File.ReadAllBytes(store.SavePath + ".bak"); File.WriteAllText(store.SavePath + ".tmp", "old temp"); var bad = Example(); bad.ResultRecorded = true; Assert(!store.TrySave(bad, out var error) && error != null, "invalid save accepted"); Assert(primary.SequenceEqual(File.ReadAllBytes(store.SavePath)) && backup.SequenceEqual(File.ReadAllBytes(store.SavePath + ".bak")) && File.ReadAllText(store.SavePath + ".tmp") == "old temp", "invalid save changed files"); }); }
    private static void WriteFailure() { InDirectory(folder => { var store = new HappyBallSaveStore(folder); Backup(store); var primary = File.ReadAllBytes(store.SavePath); var backup = File.ReadAllBytes(store.SavePath + ".bak"); Directory.CreateDirectory(store.SavePath + ".tmp"); Assert(!store.TrySave(Example(), out var error) && error != null, "temp failure hidden"); Assert(primary.SequenceEqual(File.ReadAllBytes(store.SavePath)) && backup.SequenceEqual(File.ReadAllBytes(store.SavePath + ".bak")), "temp failure changed files"); Directory.Delete(store.SavePath + ".tmp"); File.Delete(store.SavePath + ".bak"); Directory.CreateDirectory(store.SavePath + ".bak"); Assert(!store.TrySave(Example(), out error) && error != null, "replace failure hidden"); Assert(primary.SequenceEqual(File.ReadAllBytes(store.SavePath)), "replace failure changed primary"); }); }
    private static void OtherFiles() { InDirectory(folder => { string[] names = { "save-v1.json", "jump-v1.json", "sheep-v1.json", "idiom-v1.json", "sudoku-v1.json", "minesweeper-v1.json", "watermelon-v1.json" }; foreach (string name in names) File.WriteAllText(Path.Combine(folder, name), name); var store = new HappyBallSaveStore(folder); Backup(store); store.Load(out var warning); foreach (string name in names) Assert(File.ReadAllText(Path.Combine(folder, name)) == name, "touched " + name); }); }
    private static void Equal(HappyBallSaveData expected, HappyBallSaveData actual) { Assert(actual != null && HappyBallSaveCodec.Encode(expected) == HappyBallSaveCodec.Encode(actual), "state changed in persistence"); }
    private static void Save(HappyBallSaveStore store, HappyBallSaveData data) { Assert(store.TrySave(data, out var error), error); }
    private static void Reject(string json) { Assert(!HappyBallSaveCodec.TryDecode(json, out var data, out var reason) && data == null && !string.IsNullOrEmpty(reason), "malformed payload accepted or partial state returned"); }
    private static void ExpectArgument(Action body) { try { body(); } catch (ArgumentException) { return; } throw new Exception("invalid state did not throw ArgumentException"); }
    private static void InDirectory(Action<string> body) { string folder = Path.Combine(Path.GetTempPath(), "xy-happy-ball-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); try { body(folder); } finally { Directory.Delete(folder, true); } }
    private static void Check(string name, Action body) { try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
}
