using System;
using System.Collections.Generic;
using System.Text;

namespace XanaduYonder.Runtime.Idiom
{
    // Shared by the live detail and the notebook snapshot. Only display labels
    // and separators are added; source content is not trimmed or rewritten.
    public static class IdiomDefinitionText
    {
        private const int StoryPreviewLength = 480;

        public static string Format(IdiomDefinition definition, bool expanded)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var text = new StringBuilder();
            if (definition.Sources != null && definition.Sources.Length > 0)
            {
                foreach (var source in definition.Sources)
                {
                    if (text.Length > 0) text.Append("\n\n\n");
                    text.Append("词库：").Append(source.SourceLabel).Append("\n原拼音\n");
                    text.Append(Available(source.Pinyin)).Append("\n\n");
                    AppendContents(text, source.Meaning, source.Stories, source.Origins, source.Examples, expanded);
                }
            }
            else AppendContents(text, definition.Meaning, definition.Stories, definition.Origins, definition.Examples, expanded);
            return text.ToString();
        }

        public static bool HasLongStories(IdiomDefinition definition)
        {
            if (definition.Sources != null && definition.Sources.Length > 0)
            {
                foreach (var source in definition.Sources)
                    if (JoinedStories(source.Stories).Length > StoryPreviewLength) return true;
                return false;
            }
            return JoinedStories(definition.Stories).Length > StoryPreviewLength;
        }

        public static string PinyinCaption(IdiomDefinition definition)
        {
            if (definition.Sources != null && definition.Sources.Length > 1) return "拼音见各词库资料";
            var pinyin = definition.Sources != null && definition.Sources.Length == 1 ? definition.Sources[0].Pinyin : definition.Pinyin;
            return CompactPinyin(pinyin);
        }

        public static string CompactPinyin(string pinyin)
        {
            if (string.IsNullOrEmpty(pinyin)) return "暂无拼音资料";
            return pinyin.Length > 48 || pinyin.IndexOfAny(new[] { '\r', '\n' }) >= 0 ? "拼音见各词库资料" : pinyin;
        }

        public static string[] DisplaySegments(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var result = new List<string>();
            for (var start = 0; start < text.Length;)
            {
                var length = Math.Min(2048, text.Length - start);
                if (start + length < text.Length && char.IsHighSurrogate(text[start + length - 1])) length--;
                result.Add(text.Substring(start, length));
                start += length;
            }
            return result.ToArray();
        }

        private static void AppendContents(StringBuilder text, string meaning, string[] stories,
            IdiomCitation[] origins, IdiomCitation[] examples, bool expanded)
        {
            text.Append("释义\n").Append(Available(meaning)).Append("\n\n典故\n");
            var story = JoinedStories(stories);
            if (!expanded && story.Length > StoryPreviewLength)
            {
                var cut = StoryPreviewLength;
                if (char.IsHighSurrogate(story[cut - 1])) cut--;
                text.Append(story.Substring(0, cut)).Append("…");
            }
            else text.Append(Available(story));
            AppendCitations(text, "出处", origins);
            AppendCitations(text, "例句", examples);
        }

        private static string JoinedStories(string[] stories) { return stories == null ? "" : string.Join("\n\n", stories); }
        private static string Available(string value) { return string.IsNullOrEmpty(value) ? "暂无" : value; }

        private static void AppendCitations(StringBuilder text, string title, IdiomCitation[] values)
        {
            text.Append("\n\n").Append(title).Append('\n');
            if (values == null || values.Length == 0) { text.Append("暂无"); return; }
            for (var i = 0; i < values.Length; i++)
            {
                if (i > 0) text.Append("\n\n");
                var value = values[i];
                text.Append(Available(value.Text));
                if (!string.IsNullOrEmpty(value.Reference)) text.Append("\n—— ").Append(value.Reference);
            }
        }
    }
}
