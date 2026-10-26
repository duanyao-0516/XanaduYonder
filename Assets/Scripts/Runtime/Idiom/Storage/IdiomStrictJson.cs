using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XanaduYonder.Runtime.Idiom
{
    internal sealed class IdiomVersionException : FormatException { }
    internal sealed class IdiomLimitException : FormatException
    {
        internal IdiomLimitException() : base("记录超出保存容量上限，暂未保存。") { }
    }

    internal static class IdiomStrictJson
    {
        internal static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly Regex Number = new Regex(@"\A-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?\z", RegexOptions.CultureInvariant);

        internal static JObject Read(string json, int maximumBytes)
        {
            if (json == null || json.Length > maximumBytes || Utf8.GetByteCount(json) > maximumBytes) throw new FormatException();
            CheckLexicalJson(json);
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { MaxDepth = 12, DateParseHandling = DateParseHandling.None })
            {
                var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new FormatException();
                return root;
            }
        }

        // JsonTextReader also accepts JavaScript syntax. Limit it to JSON before loading.
        private static void CheckLexicalJson(string json)
        {
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == ' ' || c == '\r' || c == '\n' || c == '\t') continue;
                if (c == '"')
                {
                    bool closed = false;
                    while (++i < json.Length)
                    {
                        c = json[i];
                        if (c == '"') { closed = true; break; }
                        if (c < 0x20) throw new FormatException();
                        if (c != '\\') continue;
                        if (++i >= json.Length) throw new FormatException();
                        c = json[i];
                        if (c == 'u')
                        {
                            int scalar = EscapeScalar(json, ref i);
                            if (scalar >= 0xD800 && scalar <= 0xDBFF)
                            {
                                if (i + 2 >= json.Length || json[++i] != '\\' || json[++i] != 'u') throw new FormatException();
                                int low = EscapeScalar(json, ref i);
                                if (low < 0xDC00 || low > 0xDFFF) throw new FormatException();
                            }
                            else if (scalar >= 0xDC00 && scalar <= 0xDFFF) throw new FormatException();
                        }
                        else if ("\"\\/bfnrt".IndexOf(c) < 0) throw new FormatException();
                    }
                    if (!closed) throw new FormatException();
                    continue;
                }
                if ("{}[]:,".IndexOf(c) >= 0)
                {
                    if (c == ',')
                    {
                        int next = i + 1;
                        while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                        if (next == json.Length || json[next] == '}' || json[next] == ']') throw new FormatException();
                    }
                    continue;
                }
                int start = i;
                while (i + 1 < json.Length && !char.IsWhiteSpace(json[i + 1]) && "{}[]:,\"".IndexOf(json[i + 1]) < 0) i++;
                string token = json.Substring(start, i - start + 1);
                if (token != "true" && token != "false" && token != "null" && !Number.IsMatch(token)) throw new FormatException();
            }
        }
        private static int EscapeScalar(string json, ref int position)
        {
            int value = 0;
            for (int n = 0; n < 4; n++)
            {
                if (++position >= json.Length || !Uri.IsHexDigit(json[position])) throw new FormatException();
                char c = json[position];
                value = value * 16 + (c <= '9' ? c - '0' : char.ToLowerInvariant(c) - 'a' + 10);
            }
            return value;
        }

        internal static JObject Object(JToken token, params string[] fields)
        {
            var value = token as JObject;
            if (value == null || value.Count != fields.Length) throw new FormatException();
            var allowed = new HashSet<string>(fields, StringComparer.Ordinal);
            foreach (var property in value.Properties()) if (!allowed.Contains(property.Name)) throw new FormatException();
            return value;
        }
        internal static JArray Array(JToken token, int maximum)
        {
            var array = token as JArray;
            if (array == null || array.Count > maximum) throw new FormatException();
            return array;
        }
        internal static string Text(JToken token, int maximum, bool allowEmpty = false)
        {
            if (token == null || token.Type != JTokenType.String) throw new FormatException();
            string value = (string)token;
            ValidateText(value, maximum, allowEmpty);
            return value;
        }
        internal static void ValidateText(string value, int maximum, bool allowEmpty = false)
        {
            if (value != null && value.Length > maximum) throw new IdiomLimitException();
            if (value == null || (!allowEmpty && string.IsNullOrWhiteSpace(value)) || value.IndexOf('\0') >= 0) throw new FormatException();
            Utf8.GetByteCount(value);
        }
        internal static long Integer(JToken token, long min, long max)
        {
            long value;
            if (token == null || token.Type != JTokenType.Integer || !long.TryParse(token.ToString(Formatting.None), NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out value) || value < min || value > max) throw new FormatException();
            return value;
        }
        internal static bool Boolean(JToken token)
        {
            if (token == null || token.Type != JTokenType.Boolean) throw new FormatException();
            return (bool)token;
        }
        internal static void Identity(JObject root, string gameId)
        {
            long version = Integer(root["schemaVersion"], 0, int.MaxValue);
            string identity = Text(root["gameId"], 64);
            if (version != 1 || identity != gameId) throw new IdiomVersionException();
        }
        internal static string Write(JObject root, int maximumBytes)
        {
            string value = root.ToString(Formatting.None);
            if (Utf8.GetByteCount(value) > maximumBytes) throw new IdiomLimitException();
            return value;
        }
    }
}
