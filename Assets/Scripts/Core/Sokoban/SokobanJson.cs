using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XanaduYonder.Core.Sokoban
{
    /// <summary>Shared boundary parser for this game's bundled catalog and disk format.</summary>
    public static class SokobanJson
    {
        internal const int MaximumLength = 2 * 1024 * 1024;
        internal const int MaximumDepth = 12;
        public static JObject Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumLength || Encoding.UTF8.GetByteCount(json) > MaximumLength) throw new FormatException("Missing or oversized JSON.");
            new StrictJsonSyntax(json).Validate();
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { MaxDepth = MaximumDepth, DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double })
            {
                var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new FormatException("Trailing JSON content.");
                return root;
            }
        }
        public static JObject Object(JToken token) { var result = token as JObject; if (result == null) throw new FormatException("Expected an object."); return result; }
        public static JArray Array(JToken token) { var result = token as JArray; if (result == null) throw new FormatException("Expected an array."); return result; }
        public static void Fields(JObject obj, params string[] fields)
        {
            var expected = new HashSet<string>(fields, StringComparer.Ordinal);
            foreach (var property in obj.Properties()) if (!expected.Remove(property.Name)) throw new FormatException("Unknown field: " + property.Name);
            if (expected.Count != 0) throw new FormatException("Missing required field.");
        }
        public static string Text(JToken token) { if (token == null || token.Type != JTokenType.String) throw new FormatException("Expected a string."); return (string)token; }
        public static string Nonempty(JToken token) { string text = Text(token); if (string.IsNullOrWhiteSpace(text)) throw new FormatException("Expected a nonempty string."); return text; }
        public static int Integer(JToken token) { if (token == null || token.Type != JTokenType.Integer) throw new FormatException("Expected an integer."); return checked((int)token); }
        public static bool Boolean(JToken token) { if (token == null || token.Type != JTokenType.Boolean) throw new FormatException("Expected a boolean."); return (bool)token; }
        public static double Number(JToken token) { if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) throw new FormatException("Expected a number."); double value = (double)token; if (double.IsNaN(value) || double.IsInfinity(value)) throw new FormatException("Expected a finite number."); return value; }
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
