using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Tetris;

namespace XanaduYonder.Runtime.Tetris
{
    public static class TetrisSaveCodec
    {
        public const int MaximumLength = 128 * 1024;
        private const int MaximumDepth = 8;
        private static readonly string[] RootFields = { "schemaVersion", "gameId", "game", "bestScore", "resultRecorded" };
        private static readonly string[] GameFields = {
            "rulesVersion", "cells", "activePiece", "rotation", "x", "y", "holdPiece", "canHold", "nextQueue", "bag", "rngState",
            "score", "lines", "gravityTicks", "softDropTicks", "lockTicks", "lockResets", "lockStarted", "gameOver", "elapsedTicks", "pieceSerial"
        };

        public static bool TryEncode(TetrisSaveData data, out string json, out string error)
        {
            json = null; error = null;
            if (data == null) { error = "Missing Tetris save data."; return false; }
            if (!data.TryValidate(out error)) return false;
            var copy = data.Copy();
            var root = new JObject {
                ["schemaVersion"] = copy.SchemaVersion, ["gameId"] = copy.GameId,
                ["game"] = copy.Game == null ? JValue.CreateNull() : (JToken)EncodeGame(copy.Game),
                ["bestScore"] = Decimal(copy.BestScore), ["resultRecorded"] = copy.ResultRecorded
            };
            string encoded = root.ToString(Formatting.None);
            if (Encoding.UTF8.GetByteCount(encoded) > MaximumLength) { error = "Tetris save exceeds the size limit."; return false; }
            json = encoded; return true;
        }

        public static bool TryDecode(string json, out TetrisSaveData data, out TetrisLoadStatus status, out string error)
        {
            data = null; status = TetrisLoadStatus.Corrupt; error = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength)
            { error = "Missing or oversized Tetris save."; return false; }
            try
            {
                // Json.NET also accepts JavaScript syntax. Validate the strict JSON grammar first.
                new StrictJsonSyntax(json).Validate();
                JObject root;
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { MaxDepth = MaximumDepth, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException("Trailing data after Tetris save.");
                }
                // A newer writer may have a different shape. Protect its identity before enforcing v1 fields.
                if (UnsupportedVersion(root["schemaVersion"]) ||
                    (root["gameId"] != null && root["gameId"].Type == JTokenType.String && (string)root["gameId"] != "tetris") ||
                    (root["game"] is JObject && UnsupportedVersion(root["game"]["rulesVersion"])))
                { status = TetrisLoadStatus.UnsupportedVersion; error = "Tetris save was written for an unsupported game or version."; return false; }
                RequireFields(root, RootFields);
                var candidate = new TetrisSaveData {
                    SchemaVersion = Integer(root["schemaVersion"]), GameId = Text(root["gameId"]),
                    Game = root["game"].Type == JTokenType.Null ? null : DecodeGame(Object(root["game"])),
                    BestScore = DecimalInteger(root["bestScore"]), ResultRecorded = Boolean(root["resultRecorded"])
                };
                if (!candidate.TryValidate(out error)) return false;
                data = candidate; status = TetrisLoadStatus.Loaded; return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException || exception is OverflowException)
            { error = exception.Message; return false; }
        }

        private static JObject EncodeGame(TetrisSnapshot game)
        {
            return new JObject {
                ["rulesVersion"] = game.RulesVersion, ["cells"] = Pieces(game.Cells), ["activePiece"] = game.ActivePiece.ToString(),
                ["rotation"] = game.Rotation, ["x"] = game.X, ["y"] = game.Y, ["holdPiece"] = game.HoldPiece.ToString(),
                ["canHold"] = game.CanHold, ["nextQueue"] = Pieces(game.NextQueue), ["bag"] = Pieces(game.Bag), ["rngState"] = game.RngState,
                ["score"] = Decimal(game.Score), ["lines"] = game.Lines, ["gravityTicks"] = game.GravityTicks, ["softDropTicks"] = game.SoftDropTicks,
                ["lockTicks"] = game.LockTicks, ["lockResets"] = game.LockResets, ["lockStarted"] = game.LockStarted, ["gameOver"] = game.GameOver,
                ["elapsedTicks"] = Decimal(game.ElapsedTicks), ["pieceSerial"] = Decimal(game.PieceSerial)
            };
        }

        private static TetrisSnapshot DecodeGame(JObject game)
        {
            RequireFields(game, GameFields);
            return new TetrisSnapshot {
                RulesVersion = Integer(game["rulesVersion"]), Cells = Pieces(game["cells"], 220, 220), ActivePiece = Piece(game["activePiece"]),
                Rotation = Integer(game["rotation"]), X = Integer(game["x"]), Y = Integer(game["y"]), HoldPiece = Piece(game["holdPiece"]),
                CanHold = Boolean(game["canHold"]), NextQueue = Pieces(game["nextQueue"], 3, 3), Bag = Pieces(game["bag"], 0, 7), RngState = UnsignedInteger(game["rngState"]),
                Score = DecimalInteger(game["score"]), Lines = Integer(game["lines"]), GravityTicks = Integer(game["gravityTicks"]), SoftDropTicks = Integer(game["softDropTicks"]),
                LockTicks = Integer(game["lockTicks"]), LockResets = Integer(game["lockResets"]), LockStarted = Boolean(game["lockStarted"]), GameOver = Boolean(game["gameOver"]),
                ElapsedTicks = DecimalInteger(game["elapsedTicks"]), PieceSerial = DecimalInteger(game["pieceSerial"])
            };
        }
        private static bool UnsupportedVersion(JToken token)
        {
            // Comparing the integer token text also protects future versions larger than Int64.
            return token != null && token.Type == JTokenType.Integer && token.ToString(Formatting.None) != "1";
        }
        private static JArray Pieces(TetrisPiece[] pieces)
        { var array = new JArray(); foreach (var piece in pieces) array.Add(piece.ToString()); return array; }
        private static TetrisPiece[] Pieces(JToken token, int minimum, int maximum)
        {
            var array = token as JArray;
            if (array == null || array.Count < minimum || array.Count > maximum) throw new FormatException("Invalid Tetris piece array length.");
            var result = new TetrisPiece[array.Count]; for (int i = 0; i < result.Length; i++) result[i] = Piece(array[i]); return result;
        }
        private static TetrisPiece Piece(JToken token)
        {
            switch (Text(token)) {
                case "None": return TetrisPiece.None; case "I": return TetrisPiece.I; case "O": return TetrisPiece.O;
                case "T": return TetrisPiece.T; case "J": return TetrisPiece.J; case "L": return TetrisPiece.L;
                case "S": return TetrisPiece.S; case "Z": return TetrisPiece.Z;
                default: throw new FormatException("Unknown Tetris piece.");
            }
        }
        private static void RequireFields(JObject value, string[] names)
        {
            if (value.Count != names.Length) throw new FormatException("Unexpected or missing Tetris save fields.");
            foreach (string name in names) if (value.Property(name) == null) throw new FormatException("Missing Tetris save field: " + name);
        }
        private static JObject Object(JToken token)
        { var value = token as JObject; if (value == null) throw new FormatException("Expected a JSON object."); return value; }
        private static string Text(JToken token)
        { if (token == null || token.Type != JTokenType.String) throw new FormatException("Expected a JSON string."); return (string)token; }
        private static bool Boolean(JToken token)
        { if (token == null || token.Type != JTokenType.Boolean) throw new FormatException("Expected a JSON boolean."); return (bool)token; }
        private static int Integer(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer || !int.TryParse(token.ToString(Formatting.None), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                throw new FormatException("Expected a 32-bit JSON integer.");
            return value;
        }
        private static uint UnsignedInteger(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer || !uint.TryParse(token.ToString(Formatting.None), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                throw new FormatException("Expected an unsigned 32-bit JSON integer.");
            return value;
        }
        private static string Decimal(long value) { return value.ToString(CultureInfo.InvariantCulture); }
        private static long DecimalInteger(JToken token)
        {
            string text = Text(token);
            if (text.Length == 0 || (text.Length > 1 && text[0] == '0')) throw new FormatException("Expected canonical nonnegative decimal digits.");
            foreach (char c in text) if (c < '0' || c > '9') throw new FormatException("Expected nonnegative decimal digits.");
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) throw new FormatException("Decimal value exceeds Int64.");
            return value;
        }

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
                if (!Take('0')) { if (position >= text.Length || text[position] < '1' || text[position] > '9') Invalid(); Digits(); }
                if (Take('.')) { if (!Digit()) Invalid(); Digits(); }
                if (Take('e') || Take('E')) { if (!Take('+')) Take('-'); if (!Digit()) Invalid(); Digits(); }
            }
            private bool Digit() { return position < text.Length && text[position] >= '0' && text[position] <= '9'; }
            private void Digits() { while (Digit()) position++; }
            private void Literal(string value) { foreach (char c in value) if (!Take(c)) Invalid(); }
            private bool Take(char c) { if (position >= text.Length || text[position] != c) return false; position++; return true; }
            private void Space() { while (position < text.Length && (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' || text[position] == '\n')) position++; }
            private static void Invalid() { throw new FormatException("Save is not strict JSON or exceeds its depth limit."); }
        }
    }
}
