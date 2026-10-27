using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Sudoku;

namespace XanaduYonder.Runtime.Sudoku
{
    public static class SudokuSaveCodec
    {
        // History is never silently truncated. Oversized writes fail before touching committed files.
        public const int MaximumLength = 16 * 1024 * 1024;
        private static readonly string[] RootFields = { "schemaVersion", "gameId", "game", "elapsedSeconds", "pencil", "quick", "selectedCell", "quickDigit", "hint", "completedIds", "unassistedIds" };
        private static readonly string[] GameFields = { "puzzle", "values", "notes", "mistakes", "hintsUsed", "revision", "history" };
        private static readonly string[] PuzzleFields = { "id", "difficulty", "level", "givens", "solution" };
        private static readonly string[] FrameFields = { "values", "notes" };
        private static readonly string[] HintFields = { "cell", "digit", "revision", "technique", "explanation", "evidence", "isDirect", "revealed" };

        public static string Encode(SudokuSaveData data)
        {
            if (data == null) throw new ArgumentException("Missing sudoku save state.", nameof(data));
            if (!data.TryValidate(out var error)) throw new ArgumentException(error, nameof(data));
            var saved = data.Copy();
            var snapshot = saved.Game;
            var puzzle = snapshot.Puzzle;
            var history = new JArray();
            foreach (var frame in snapshot.History)
                history.Add(new JObject { ["values"] = new JArray(frame.Values), ["notes"] = new JArray(frame.Notes) });
            var hint = saved.Hint;
            var root = new JObject
            {
                ["schemaVersion"] = 1,
                ["gameId"] = "sudoku",
                ["game"] = new JObject
                {
                    ["puzzle"] = new JObject
                    {
                        ["id"] = puzzle.Id, ["difficulty"] = puzzle.Difficulty, ["level"] = puzzle.Level,
                        ["givens"] = new JArray(puzzle.Givens), ["solution"] = new JArray(puzzle.Solution)
                    },
                    ["values"] = new JArray(snapshot.Values), ["notes"] = new JArray(snapshot.Notes),
                    ["mistakes"] = snapshot.Mistakes, ["hintsUsed"] = snapshot.HintsUsed,
                    ["revision"] = snapshot.Revision, ["history"] = history
                },
                ["elapsedSeconds"] = saved.ElapsedSeconds, ["pencil"] = saved.Pencil, ["quick"] = saved.Quick,
                ["selectedCell"] = saved.SelectedCell, ["quickDigit"] = saved.QuickDigit,
                ["hint"] = hint == null ? (JToken)JValue.CreateNull() : new JObject
                {
                    ["cell"] = hint.Cell, ["digit"] = hint.Digit, ["revision"] = hint.Revision,
                    ["technique"] = hint.Technique, ["explanation"] = hint.Explanation,
                    ["evidence"] = new JArray(hint.Evidence), ["isDirect"] = hint.IsDirect, ["revealed"] = hint.Revealed
                },
                ["completedIds"] = new JArray(saved.CompletedIds), ["unassistedIds"] = new JArray(saved.UnassistedIds)
            };
            var json = root.ToString(Formatting.None);
            if (json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength)
                throw new ArgumentException("Sudoku save exceeds the size limit; history was not discarded.", nameof(data));
            return json;
        }

        public static bool TryDecode(string json, out SudokuSaveData data, out string error)
        {
            data = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength)
            {
                error = "Missing or oversized sudoku save file.";
                return false;
            }
            try
            {
                JObject root;
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { MaxDepth = 8, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException("Trailing data after sudoku save object.");
                }
                RequireFields(root, RootFields);
                if (Integer(root["schemaVersion"]) != 1 || Text(root["gameId"]) != "sudoku")
                    throw new FormatException("Unsupported sudoku save schema.");
                var game = Object(root["game"], GameFields);
                var puzzle = Object(game["puzzle"], PuzzleFields);
                var frames = game["history"] as JArray;
                if (frames == null) throw new FormatException("Expected sudoku undo history array.");
                var history = new SudokuUndoFrame[frames.Count];
                for (var index = 0; index < frames.Count; index++)
                {
                    var frame = Object(frames[index], FrameFields);
                    history[index] = new SudokuUndoFrame { Values = IntegerArray(frame["values"], 81, true), Notes = IntegerArray(frame["notes"], 81, true) };
                }
                SudokuSavedHint hint = null;
                if (root["hint"].Type != JTokenType.Null)
                {
                    var savedHint = Object(root["hint"], HintFields);
                    hint = new SudokuSavedHint
                    {
                        Cell = Integer(savedHint["cell"]), Digit = Integer(savedHint["digit"]), Revision = Integer(savedHint["revision"]),
                        Technique = Text(savedHint["technique"]), Explanation = Text(savedHint["explanation"]),
                        Evidence = IntegerArray(savedHint["evidence"], 81, false), IsDirect = Boolean(savedHint["isDirect"]), Revealed = Boolean(savedHint["revealed"])
                    };
                }
                var decoded = new SudokuSaveData
                {
                    Game = new SudokuSnapshot
                    {
                        Puzzle = new SudokuPuzzle
                        {
                            Id = Text(puzzle["id"]), Difficulty = Integer(puzzle["difficulty"]), Level = Integer(puzzle["level"]),
                            Givens = IntegerArray(puzzle["givens"], 81, true), Solution = IntegerArray(puzzle["solution"], 81, true)
                        },
                        Values = IntegerArray(game["values"], 81, true), Notes = IntegerArray(game["notes"], 81, true),
                        Mistakes = Integer(game["mistakes"]), HintsUsed = Integer(game["hintsUsed"]), Revision = Integer(game["revision"]), History = history
                    },
                    ElapsedSeconds = Number(root["elapsedSeconds"]), Pencil = Boolean(root["pencil"]), Quick = Boolean(root["quick"]),
                    SelectedCell = Integer(root["selectedCell"]), QuickDigit = Integer(root["quickDigit"]), Hint = hint,
                    CompletedIds = TextArray(root["completedIds"], 2000), UnassistedIds = TextArray(root["unassistedIds"], 2000)
                };
                if (!decoded.TryValidate(out error)) return false;
                data = decoded;
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException || exception is OverflowException || exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        private static JObject Object(JToken token, string[] fields)
        {
            var value = token as JObject;
            if (value == null) throw new FormatException("Expected sudoku save object.");
            RequireFields(value, fields);
            return value;
        }

        private static void RequireFields(JObject value, string[] fields)
        {
            if (value.Count != fields.Length) throw new FormatException("Unexpected sudoku save fields.");
            foreach (var field in fields)
                if (value.Property(field) == null) throw new FormatException("Missing sudoku save field: " + field);
        }

        private static int Integer(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected an integer field.");
            return checked((int)token);
        }

        private static double Number(JToken token)
        {
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
                throw new FormatException("Expected a numeric field.");
            return (double)token;
        }

        private static bool Boolean(JToken token)
        {
            if (token == null || token.Type != JTokenType.Boolean) throw new FormatException("Expected a boolean field.");
            return (bool)token;
        }

        private static string Text(JToken token)
        {
            if (token == null || token.Type != JTokenType.String) throw new FormatException("Expected a string field.");
            return (string)token;
        }

        private static int[] IntegerArray(JToken token, int count, bool exact)
        {
            var array = token as JArray;
            if (array == null || array.Count > count || (exact && array.Count != count))
                throw new FormatException("Invalid sudoku integer array length.");
            var result = new int[array.Count];
            for (var index = 0; index < result.Length; index++) result[index] = Integer(array[index]);
            return result;
        }

        private static string[] TextArray(JToken token, int maximumCount)
        {
            var array = token as JArray;
            if (array == null || array.Count > maximumCount) throw new FormatException("Invalid sudoku completion array.");
            var result = new string[array.Count];
            for (var index = 0; index < result.Length; index++) result[index] = Text(array[index]);
            return result;
        }
    }
}
