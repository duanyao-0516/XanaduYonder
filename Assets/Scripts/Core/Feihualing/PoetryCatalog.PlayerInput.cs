using System;
using System.Globalization;
using System.Text;

namespace XanaduYonder.Core.Feihualing
{
    public sealed partial class PoetryCatalog
    {
        internal static string CleanPlayerInput(string input)
        {
            if (string.IsNullOrEmpty(input) || input.Length > MaximumInputLength || !HasValidUtf16(input))
                return input;

            StringBuilder cleaned = null;
            var afterDecoration = false;
            for (var index = 0; index < input.Length;)
            {
                var scalar = char.ConvertToUtf32(input, index);
                var width = scalar >= 0x10000 ? 2 : 1;
                if (IsPlayerDecoration(input, index, width, scalar) ||
                    (afterDecoration && IsEmojiPresentation(scalar)))
                {
                    if (cleaned == null)
                        cleaned = new StringBuilder(input.Length).Append(input, 0, index);
                    afterDecoration = true;
                }
                else
                {
                    cleaned?.Append(input, index, width);
                    afterDecoration = false;
                }
                index += width;
            }

            var text = cleaned?.ToString() ?? input;
            var start = 0;
            var end = text.Length;
            while (start < end && IsPlayerEnvelope(text[start]))
                start++;
            while (end > start && IsPlayerEnvelope(text[end - 1]))
                end--;
            return start == 0 && end == text.Length ? text : text.Substring(start, end - start);
        }

        private static bool IsPlayerEnvelope(char value)
        {
            var mark = CanonicalMark(value);
            return char.IsWhiteSpace(value) || IsTerminalMark(mark) || mark == '、' ||
                value == '/' || value == '／' || value == '|' || value == '｜' ||
                char.GetUnicodeCategory(value) == UnicodeCategory.DashPunctuation ||
                value == '…' || value == '‼' || value == '⁇' || value == '⁈' || value == '⁉';
        }

        private static bool IsPlayerDecoration(string text, int index, int width, int scalar)
        {
            // Keep vertical separators, even though Unicode classifies them as mathematical symbols.
            if (scalar == '|' || scalar == '｜')
                return false;
            if (scalar == '"' || scalar == '\'' || scalar == '*' || scalar == '＊' || scalar == '※' ||
                scalar == '＂' || scalar == '＇')
                return true;
            switch (CharUnicodeInfo.GetUnicodeCategory(text, index))
            {
                case UnicodeCategory.OpenPunctuation:
                case UnicodeCategory.ClosePunctuation:
                case UnicodeCategory.InitialQuotePunctuation:
                case UnicodeCategory.FinalQuotePunctuation:
                    return true;
                case UnicodeCategory.MathSymbol:
                case UnicodeCategory.CurrencySymbol:
                case UnicodeCategory.ModifierSymbol:
                case UnicodeCategory.OtherSymbol:
                    return !HasCompatibilityText(text.Substring(index, width));
                default:
                    return false;
            }
        }

        private static bool HasCompatibilityText(string symbol)
        {
            // Enclosed letters and similar glyphs still carry input, even when their category is Symbol.
            var compatibility = symbol.Normalize(NormalizationForm.FormKC);
            for (var index = 0; index < compatibility.Length;)
            {
                if (char.IsLetter(compatibility, index) || char.IsNumber(compatibility, index))
                    return true;
                index += char.IsHighSurrogate(compatibility[index]) ? 2 : 1;
            }
            return false;
        }

        private static bool IsEmojiPresentation(int scalar)
        {
            return scalar == 0x200D || scalar == 0xFE0E || scalar == 0xFE0F ||
                (scalar >= 0xE0020 && scalar <= 0xE007F);
        }
    }
}
