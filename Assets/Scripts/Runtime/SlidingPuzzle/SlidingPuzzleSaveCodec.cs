using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.SlidingPuzzle;

namespace XanaduYonder.Runtime.SlidingPuzzle
{
    public static class SlidingPuzzleSaveCodec
    {
        public const int MaximumLength = 2 * 1024 * 1024;
        public const int MaximumDepth = 12;
        public static string Encode(SlidingPuzzleSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!data.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(data));
            var free = new JArray(); foreach (var session in data.FreeSessions) free.Add(Session(session));
            string json = new JObject { ["schemaVersion"] = 1, ["saveKind"] = "sliding-puzzle", ["contentVersion"] = data.ContentVersion, ["selectedMode"] = data.SelectedMode, ["selectedSize"] = data.SelectedSize, ["selectedChapter"] = data.SelectedChapter, ["freeSessions"] = free, ["campaignSession"] = Session(data.CampaignSession), ["completedLevels"] = new JArray(data.CompletedLevels), ["freeRecords"] = Records(data.FreeRecords), ["levelRecords"] = Records(data.LevelRecords) }.ToString(Formatting.None);
            if (Encoding.UTF8.GetByteCount(json) > MaximumLength) throw new ArgumentException("Save exceeds 2 MiB.", nameof(data)); return json;
        }
        private static JToken Session(SlidingPuzzleSessionData data)
        {
            if (data == null) return JValue.CreateNull(); var s = data.Snapshot;
            return new JObject { ["levelIndex"] = data.LevelIndex, ["elapsedSeconds"] = data.ElapsedSeconds, ["resultRecorded"] = data.ResultRecorded, ["snapshot"] = new JObject { ["size"] = s.Size, ["initialCells"] = new JArray(s.InitialCells), ["cells"] = new JArray(s.Cells), ["history"] = new JArray(s.History), ["moves"] = s.Moves, ["undoCount"] = s.UndoCount } };
        }
        private static JArray Records(SlidingPuzzleRecord[] records)
        { var array = new JArray(); foreach (var r in records) array.Add(r == null ? (JToken)JValue.CreateNull() : new JObject { ["bestMoves"] = Score(r.BestMoves), ["bestTime"] = Score(r.BestTime) }); return array; }
        private static JObject Score(SlidingPuzzleScore score) { return new JObject { ["moves"] = score.Moves, ["seconds"] = score.Seconds }; }
        public static bool TryDecode(string json, out SlidingPuzzleSaveData data, out string reason)
        {
            data = null; reason = null;
            try
            {
                var root = Parse(json); Fields(root, "schemaVersion", "saveKind", "contentVersion", "selectedMode", "selectedSize", "selectedChapter", "freeSessions", "campaignSession", "completedLevels", "freeRecords", "levelRecords");
                if (Integer(root["schemaVersion"]) != 1 || Text(root["saveKind"]) != "sliding-puzzle") throw new FormatException("Unknown save schema or game.");
                var free = Array(root["freeSessions"]); if (free.Count != 3) throw new FormatException("Expected three free slots.");
                var decoded = new SlidingPuzzleSaveData { ContentVersion = Text(root["contentVersion"]), SelectedMode = Integer(root["selectedMode"]), SelectedSize = Integer(root["selectedSize"]), SelectedChapter = Integer(root["selectedChapter"]), CampaignSession = ReadSession(root["campaignSession"]), CompletedLevels = Integers(root["completedLevels"], SlidingPuzzleCatalog.Count), FreeRecords = ReadRecords(root["freeRecords"], 3), LevelRecords = ReadRecords(root["levelRecords"], SlidingPuzzleCatalog.Count) };
                for (int i = 0; i < free.Count; i++) decoded.FreeSessions[i] = ReadSession(free[i]);
                if (!decoded.TryValidate(out reason)) return false;
                data = decoded; return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException || exception is ArgumentException || exception is OverflowException)
            { reason = exception.Message; data = null; return false; }
        }
        private static SlidingPuzzleSessionData ReadSession(JToken token)
        {
            if (token.Type == JTokenType.Null) return null;
            var item = Object(token); Fields(item, "levelIndex", "elapsedSeconds", "resultRecorded", "snapshot");
            var snapshot = Object(item["snapshot"]); Fields(snapshot, "size", "initialCells", "cells", "history", "moves", "undoCount");
            return new SlidingPuzzleSessionData { LevelIndex = Integer(item["levelIndex"]), ElapsedSeconds = Number(item["elapsedSeconds"]), ResultRecorded = Boolean(item["resultRecorded"]), Snapshot = new SlidingPuzzleSnapshot { Size = Integer(snapshot["size"]), InitialCells = Integers(snapshot["initialCells"], 25), Cells = Integers(snapshot["cells"], 25), History = Integers(snapshot["history"], SlidingPuzzleGame.MaxActions), Moves = Integer(snapshot["moves"]), UndoCount = Integer(snapshot["undoCount"]) } };
        }
        private static SlidingPuzzleRecord[] ReadRecords(JToken token, int count)
        {
            var array = Array(token); if (array.Count != count) throw new FormatException("Wrong record array length.");
            var records = new SlidingPuzzleRecord[count];
            for (int i = 0; i < count; i++) { if (array[i].Type == JTokenType.Null) continue; var item = Object(array[i]); Fields(item, "bestMoves", "bestTime"); records[i] = new SlidingPuzzleRecord { BestMoves = ReadScore(item["bestMoves"]), BestTime = ReadScore(item["bestTime"]) }; }
            return records;
        }
        private static SlidingPuzzleScore ReadScore(JToken token)
        { var item = Object(token); Fields(item, "moves", "seconds"); return new SlidingPuzzleScore { Moves = Integer(item["moves"]), Seconds = Number(item["seconds"]) }; }
        private static int[] Integers(JToken token, int maximum)
        { var array = Array(token); if (array.Count > maximum) throw new FormatException("Array exceeds its length limit."); var values = new int[array.Count]; for (int i = 0; i < array.Count; i++) values[i] = Integer(array[i]); return values; }
        private static JObject Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength) throw new FormatException("Missing or oversized JSON.");
            new StrictJsonSyntax(json).Validate();
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { MaxDepth = MaximumDepth, DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double })
            { var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); if (reader.Read()) throw new FormatException("Trailing JSON content."); return root; }
        }
        private static JObject Object(JToken token) { var result = token as JObject; if (result == null) throw new FormatException("Expected an object."); return result; }
        private static JArray Array(JToken token) { var result = token as JArray; if (result == null) throw new FormatException("Expected an array."); return result; }
        private static void Fields(JObject obj, params string[] fields)
        { var expected = new HashSet<string>(fields, StringComparer.Ordinal); foreach (var property in obj.Properties()) if (!expected.Remove(property.Name)) throw new FormatException("Unknown field: " + property.Name); if (expected.Count != 0) throw new FormatException("Missing required field."); }
        private static string Text(JToken token) { if (token == null || token.Type != JTokenType.String) throw new FormatException("Expected a string."); return (string)token; }
        private static int Integer(JToken token) { if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected an integer."); return checked((int)token); }
        private static bool Boolean(JToken token) { if (token == null || token.Type != JTokenType.Boolean) throw new FormatException("Expected a boolean."); return (bool)token; }
        private static double Number(JToken token) { if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) throw new FormatException("Expected a number."); double value = (double)token; if (!SlidingPuzzleSessionData.Finite(value)) throw new FormatException("Expected a finite number."); return value; }
        private sealed class StrictJsonSyntax
        {
            private readonly string text;
            private int position;
            internal StrictJsonSyntax(string text) { this.text = text; }
            internal void Validate() { Value(0); Space(); if (position != text.Length) Invalid(); }
            private void Value(int depth)
            {
                Space(); if (position >= text.Length) Invalid(); char c = text[position];
                if (c == '{' || c == '[')
                {
                    if (++depth > MaximumDepth) Invalid(); bool obj = c == '{'; position++; Space(); char close = obj ? '}' : ']';
                    if (Take(close)) return;
                    while (true)
                    {
                        if (obj) { String(); Space(); if (!Take(':')) Invalid(); }
                        Value(depth); Space(); if (Take(close)) return; if (!Take(',')) Invalid(); Space();
                    }
                }
                if (c == '"') { String(); return; }
                if (c == 't') { Literal("true"); return; }
                if (c == 'f') { Literal("false"); return; }
                if (c == 'n') { Literal("null"); return; }
                Number();
            }
            private void String()
            {
                if (!Take('"')) Invalid();
                while (position < text.Length)
                {
                    char c = text[position++]; if (c == '"') return; if (c < 32) Invalid();
                    if (c != '\\') continue;
                    if (position >= text.Length) Invalid(); c = text[position++];
                    if (c == 'u')
                    {
                        for (int i = 0; i < 4; i++)
                        { if (position >= text.Length) Invalid(); c = text[position++]; if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) Invalid(); }
                    }
                    else if (c != '"' && c != '\\' && c != '/' && c != 'b' && c != 'f' && c != 'n' && c != 'r' && c != 't') Invalid();
                }
                Invalid();
            }
            private void Number()
            {
                Take('-');
                if (!Take('0'))
                { if (position >= text.Length || text[position] < '1' || text[position] > '9') Invalid(); Digits(); }
                if (Take('.')) { if (!Digit()) Invalid(); Digits(); }
                if (Take('e') || Take('E')) { if (!Take('+')) Take('-'); if (!Digit()) Invalid(); Digits(); }
            }
            private bool Digit() { return position < text.Length && text[position] >= '0' && text[position] <= '9'; }
            private void Digits() { while (Digit()) position++; }
            private void Literal(string value)
            { foreach (char c in value) if (!Take(c)) Invalid(); }
            private bool Take(char c) { if (position >= text.Length || text[position] != c) return false; position++; return true; }
            private void Space() { while (position < text.Length && (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' || text[position] == '\n')) position++; }
            private static void Invalid() { throw new FormatException("Save file is not strict JSON or exceeds the depth limit."); }
        }
    }
}
