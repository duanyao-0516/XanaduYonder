using System;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif
using XanaduYonder.Core;
using XanaduYonder.Runtime;

namespace XanaduYonder.EditorTools
{
    // These checks use the game's actual codec and disk store in both .NET and Unity.
    public static class PersistenceChecks
    {
        private const string ValidJson = "{\"schemaVersion\":1,\"saveKind\":\"xanaduyonder\",\"game\":{\"schemaVersion\":1,\"gameId\":\"2048\",\"cells\":[2,4,0,0,0,0,0,0,0,0,0,0,0,0,0,0],\"score\":\"4\",\"winState\":1},\"bestScore\":\"16\",\"soundState\":1,\"hapticsState\":2}";

#if UNITY_EDITOR
        [MenuItem("XanaduYonder/Validate persistence")]
#endif
        public static void Run()
        {
            var passed = 0;
            var failed = 0;
            Check("JSON round trip preserves all fields", RoundTrip, ref passed, ref failed);
            Check("Decode accepts explicit valid schema", DecodeKnownPayload, ref passed, ref failed);
            Check("Codec rejects malformed and incompatible payloads", RejectInvalidPayloads, ref passed, ref failed);
            Check("Encode rejects invalid state", RejectInvalidState, ref passed, ref failed);
            Check("Missing save starts without warning", MissingSave, ref passed, ref failed);
            Check("Two writes preserve a recoverable backup", BackupRecovery, ref passed, ref failed);
            Check("Corrupt files safely return no saved game", CorruptFiles, ref passed, ref failed);
            Check("Write failures are returned to the caller", WriteFailure, ref passed, ref failed);
            Check("Unplayable empty and single-tile saves are rejected", RejectImpossibleBoards, ref passed, ref failed);
            Log($"Persistence checks: {passed} passed, {failed} failed.");
            if (failed != 0)
            {
#if UNITY_EDITOR
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                    return;
                }
#endif
                throw new InvalidOperationException($"{failed} persistence checks failed.");
            }
        }

        private static void Check(string name, Action action, ref int passed, ref int failed)
        {
            try
            {
                action();
                passed++;
                Log("PASS: " + name);
            }
            catch (Exception exception)
            {
                failed++;
                Log("FAIL: " + name + "\n" + exception, true);
            }
        }

        private static void Log(string message, bool error = false)
        {
#if UNITY_EDITOR
            if (error) Debug.LogError(message);
            else Debug.Log(message);
#else
            if (error) Console.Error.WriteLine(message);
            else Console.WriteLine(message);
#endif
        }

        private static void RoundTrip()
        {
            var original = Example();
            original.Game.cells[0] = 2048;
            original.Game.score = 9007199254740993L;
            original.Game.winAcknowledged = true;
            original.BestScore = long.MaxValue;
            original.SoundEnabled = false;
            original.HapticsEnabled = true;
            var json = JsonSaveCodec.Encode(original);
            Assert(JsonSaveCodec.TryDecode(json, out var decoded, out var reason), reason);
            EqualState(original, decoded);
        }

        private static void DecodeKnownPayload()
        {
            Assert(JsonSaveCodec.TryDecode(ValidJson, out var decoded, out var reason), reason);
            EqualState(Example(), decoded);
        }

        private static void RejectInvalidPayloads()
        {
            var invalid = new[]
            {
                (string)null, "", " ", "{", "null", "[]", "17", "{}",
                ValidJson.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"),
                ValidJson.Replace("\"schemaVersion\":1,", ""),
                ValidJson.Replace("\"schemaVersion\":1", "\"schemaVersion\":\"1\""),
                ValidJson.Replace("\"schemaVersion\":1,", "\"schemaVersion\":1,\"schemaVersion\":1,"),
                ValidJson.Replace("\"game\":{\"schemaVersion\":1", "\"game\":{\"schemaVersion\":2"),
                ValidJson.Replace("\"game\":{\"schemaVersion\":1,", "\"game\":{"),
                ValidJson.Replace("\"saveKind\":\"xanaduyonder\",", ""),
                ValidJson.Replace("\"saveKind\":\"xanaduyonder\"", "\"saveKind\":null"),
                ValidJson.Replace("\"saveKind\":\"xanaduyonder\"", "\"saveKind\":\"unknown\""),
                ValidJson.Replace("\"gameId\":\"2048\",", ""),
                ValidJson.Replace("\"gameId\":\"2048\"", "\"gameId\":null"),
                ValidJson.Replace("\"gameId\":\"2048\"", "\"gameId\":\"other\""),
                ValidJson.Replace("[2,4,0,0,0,0,0,0,0,0,0,0,0,0,0,0]", "null"),
                ValidJson.Replace("[2,4,0,0,0,0,0,0,0,0,0,0,0,0,0,0]", "[]"),
                ValidJson.Replace("[2,4,0,0,0,0,0,0,0,0,0,0,0,0,0,0]", "[2,4]"),
                ValidJson.Replace("[2,4,", "[3,4,"),
                ValidJson.Replace("[2,4,", "[-2,4,"),
                ValidJson.Replace("[2,4,", "[null,4,"),
                ValidJson.Replace("[2,4,", "[\"2\",4,"),
                ValidJson.Replace("[2,4,", "[2.5,4,"),
                ValidJson.Replace("\"score\":\"4\",", ""),
                ValidJson.Replace("\"score\":\"4\"", "\"score\":null"),
                ValidJson.Replace("\"score\":\"4\"", "\"score\":4"),
                ValidJson.Replace("\"score\":\"4\"", "\"score\":\"-1\""),
                ValidJson.Replace("\"score\":\"4\"", "\"score\":\"9223372036854775808\""),
                ValidJson.Replace("\"score\":\"4\"", "\"score\":\"1.5\""),
                ValidJson.Replace("\"bestScore\":\"16\",", ""),
                ValidJson.Replace("\"bestScore\":\"16\"", "\"bestScore\":null"),
                ValidJson.Replace("\"bestScore\":\"16\"", "\"bestScore\":16"),
                ValidJson.Replace("\"bestScore\":\"16\"", "\"bestScore\":\"3\""),
                ValidJson.Replace("\"bestScore\":\"16\"", "\"bestScore\":\"9223372036854775808\""),
                ValidJson.Replace(",\"winState\":1", ""),
                ValidJson.Replace("\"winState\":1", "\"winState\":0"),
                ValidJson.Replace("\"winState\":1", "\"winState\":2"),
                ValidJson.Replace("\"winState\":1", "\"winState\":3"),
                ValidJson.Replace("\"winState\":1", "\"winState\":\"1\""),
                ValidJson.Replace("\"soundState\":1,", ""),
                ValidJson.Replace("\"soundState\":1", "\"soundState\":0"),
                ValidJson.Replace("\"soundState\":1", "\"soundState\":null"),
                ValidJson.Replace("\"soundState\":1", "\"soundState\":true"),
                ValidJson.Replace("\"soundState\":1", "\"soundState\":\"1\""),
                ValidJson.Replace(",\"hapticsState\":2", ""),
                ValidJson.Replace("\"hapticsState\":2", "\"hapticsState\":0"),
                ValidJson.Replace("\"hapticsState\":2", "\"hapticsState\":3"),
                "{\"schemaVersion\":1,\"saveKind\":\"xanaduyonder\",\"game\":null,\"bestScore\":\"16\",\"soundState\":1,\"hapticsState\":2}"
            };
            for (var index = 0; index < invalid.Length; index++)
            {
                Assert(!JsonSaveCodec.TryDecode(invalid[index], out var data, out var reason),
                    "Invalid payload accepted at case " + index + ": " + invalid[index]);
                Assert(data == null, "Rejected payload exposed partial state at case " + index);
                Assert(!string.IsNullOrWhiteSpace(reason), "Missing rejection reason at case " + index);
            }
        }

        private static void RejectInvalidState()
        {
            ExpectArgumentException(() => JsonSaveCodec.Encode(null));
            var invalid = Example();
            invalid.Game = null;
            ExpectArgumentException(() => JsonSaveCodec.Encode(invalid));
            invalid = Example();
            invalid.Game.cells[0] = 3;
            ExpectArgumentException(() => JsonSaveCodec.Encode(invalid));
            invalid = Example();
            invalid.BestScore = invalid.Game.score - 1;
            ExpectArgumentException(() => JsonSaveCodec.Encode(invalid));
        }

        private static void RejectImpossibleBoards()
        {
            foreach (var replacement in new[] { "[0,0,", "[2,0," })
                Assert(!JsonSaveCodec.TryDecode(ValidJson.Replace("[2,4,", replacement), out var data, out var reason),
                    "A persisted game always has at least two occupied cells after spawning.");
            var empty = Example();
            empty.Game.cells = new int[16];
            ExpectArgumentException(() => JsonSaveCodec.Encode(empty));
        }

        private static void MissingSave()
        {
            WithDirectory(directory =>
            {
                var data = new LocalSaveStore(directory).Load(out var warning);
                Assert(data == null, "Missing save should return null.");
                Assert(string.IsNullOrEmpty(warning), "First launch should not warn about missing save.");
            });
        }

        private static void BackupRecovery()
        {
            WithDirectory(directory =>
            {
                var store = new LocalSaveStore(directory);
                var first = Example();
                Assert(store.TrySave(first, out var firstError), firstError);
                Assert(File.Exists(store.SavePath), "Primary save was not written.");
                EqualState(first, store.Load(out var firstWarning));
                Assert(string.IsNullOrEmpty(firstWarning), firstWarning);
                var second = Example();
                second.Game.cells[0] = 8;
                second.Game.score = 12;
                second.BestScore = 32;
                second.SoundEnabled = false;
                Assert(store.TrySave(second, out var secondError), secondError);
                EqualState(second, new LocalSaveStore(directory).Load(out var secondWarning));
                Assert(string.IsNullOrEmpty(secondWarning), secondWarning);
                Assert(File.Exists(store.SavePath + ".bak"), "Second save did not preserve backup.");
                File.WriteAllText(store.SavePath, "{interrupted write");
                EqualState(first, new LocalSaveStore(directory).Load(out var warning));
                Assert(!string.IsNullOrWhiteSpace(warning), "Backup recovery must be reported.");
            });
        }

        private static void CorruptFiles()
        {
            WithDirectory(directory =>
            {
                var store = new LocalSaveStore(directory);
                File.WriteAllText(store.SavePath, "broken");
                File.WriteAllText(store.SavePath + ".bak", "also broken");
                Assert(store.Load(out var warning) == null, "Two corrupt files returned a saved game.");
                Assert(!string.IsNullOrWhiteSpace(warning), "Corrupt files need a user-visible warning.");
            });
        }

        private static void WriteFailure()
        {
            WithDirectory(directory =>
            {
                var store = new LocalSaveStore(directory);
                Directory.Delete(directory, true);
                File.WriteAllText(directory, "not a directory");
                Assert(!store.TrySave(Example(), out var error), "Write into a file path should fail.");
                Assert(!string.IsNullOrWhiteSpace(error), "Write failure must describe the error.");
            });
        }

        private static SaveData Example()
        {
            return new SaveData
            {
                Game = new GameSnapshot { cells = new[] { 2, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, score = 4 },
                BestScore = 16,
                SoundEnabled = true,
                HapticsEnabled = false
            };
        }

        private static void EqualState(SaveData expected, SaveData actual)
        {
            Assert(actual != null && actual.Game != null, "Decoded save is null.");
            Assert(expected.Game.schemaVersion == actual.Game.schemaVersion, "Game schema changed.");
            Assert(expected.Game.gameId == actual.Game.gameId, "Game identifier changed.");
            Assert(actual.Game.cells != null && actual.Game.cells.Length == 16, "Board dimensions changed.");
            for (var index = 0; index < 16; index++)
                Assert(expected.Game.cells[index] == actual.Game.cells[index], "Cell changed at " + index);
            Assert(expected.Game.score == actual.Game.score, "Score changed.");
            Assert(expected.Game.winAcknowledged == actual.Game.winAcknowledged, "Win acknowledgement changed.");
            Assert(expected.BestScore == actual.BestScore, "Best score changed.");
            Assert(expected.SoundEnabled == actual.SoundEnabled, "Sound setting changed.");
            Assert(expected.HapticsEnabled == actual.HapticsEnabled, "Haptics setting changed.");
        }

        private static void WithDirectory(Action<string> action)
        {
            var directory = Path.Combine(Path.GetTempPath(), "XanaduYonder-Persistence-" + Guid.NewGuid().ToString("N"));
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

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
