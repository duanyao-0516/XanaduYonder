using System;
using System.Collections.Generic;
using System.Text;

namespace XanaduYonder.Core.Feihualing
{
    public enum PoetryRule
    {
        Character,
        Position,
        Pair,
        Theme
    }

    public sealed class PoetryWork
    {
        public PoetryWork(string id, string author, string title, string body)
        {
            Id = id;
            Author = author;
            Title = title;
            Body = body;
        }

        public string Id { get; }
        public string Author { get; }
        public string Title { get; }
        public string Body { get; }
    }

    public sealed class PoetryLine
    {
        public PoetryLine(int workIndex, int start, int length, int group, bool common, string[] aliases)
        {
            WorkIndex = workIndex;
            Start = start;
            Length = length;
            Group = group;
            Common = common;
            Aliases = aliases;
        }

        public int WorkIndex { get; }
        public int Start { get; }
        public int Length { get; }
        public int Group { get; }
        public bool Common { get; }
        public string[] Aliases { get; }
    }

    public sealed class PoetryCard
    {
        public PoetryCard(string id, PoetryRule rule, string title, string key, string secondKey, int width,
            int[][] candidates, IReadOnlyDictionary<int, string> reasons)
        {
            Id = id;
            Rule = rule;
            Title = title;
            Key = key;
            SecondKey = secondKey;
            Width = width;
            Candidates = candidates;
            Reasons = reasons ?? new Dictionary<int, string>();
        }

        public string Id { get; }
        public PoetryRule Rule { get; }
        public string Title { get; }
        public string Key { get; }
        public string SecondKey { get; }
        public int Width { get; }
        public int[][] Candidates { get; }
        public IReadOnlyDictionary<int, string> Reasons { get; }

        internal int[] AtPosition(int position)
        {
            return Candidates[CandidateIndex(position)];
        }

        internal int CandidateIndex(int position)
        {
            return Rule == PoetryRule.Position ? position - 1 : 0;
        }
    }

    public sealed partial class PoetryCatalog
    {
        internal const int MaximumInputLength = 512;
        private readonly Dictionary<string, int> firstMatches = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<int>> otherMatches = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        private readonly Dictionary<string, PoetryCard> cardsById = new Dictionary<string, PoetryCard>(StringComparer.Ordinal);
        private readonly Dictionary<int, int> groupHeads = new Dictionary<int, int>();
        private readonly Dictionary<PoetryCard, int[][]> activeCandidateGroups = new Dictionary<PoetryCard, int[][]>();
        private readonly HashSet<int> retired = new HashSet<int>();
        private readonly string[] lineKeys;
        private readonly int[][] workLines;
        private readonly int[] followingLines;
        private readonly int[] precedingLines;
        private readonly int[] nextInGroup;
        private int maximumKeyLength;

        public PoetryCatalog(string version, PoetryWork[] works, PoetryLine[] lines, PoetryCard[] cards, int[] retiredLines = null)
        {
            if (string.IsNullOrWhiteSpace(version) || works == null || works.Length == 0 ||
                lines == null || lines.Length == 0 || cards == null || cards.Length == 0)
                throw new ArgumentException("Incomplete poetry catalog.");
            Version = version;
            Works = works;
            Lines = lines;
            Cards = cards;
            lineKeys = new string[lines.Length];
            followingLines = new int[lines.Length];
            precedingLines = new int[lines.Length];
            nextInGroup = new int[lines.Length];
            workLines = new int[works.Length][];
            if (retiredLines != null)
                foreach (var lineId in retiredLines)
                    if (!HasStoredLine(lineId) || !retired.Add(lineId))
                        throw new ArgumentException("Invalid retired poetry line.");

            ValidateWorks();
            IndexLines();
            foreach (var card in cards)
                ValidateCard(card);
        }

        public string Version { get; }
        public PoetryWork[] Works { get; }
        public PoetryLine[] Lines { get; }
        public PoetryCard[] Cards { get; }

        public PoetryCard FindCard(string cardId)
        {
            return cardId != null && cardsById.TryGetValue(cardId, out var card) ? card : null;
        }

        public string Text(int lineId)
        {
            var line = Lines[lineId];
            return Works[line.WorkIndex].Body.Substring(line.Start, line.Length);
        }

        private bool HasStoredLine(int lineId)
        {
            return lineId >= 0 && lineId < Lines.Length;
        }

        internal bool HasLine(int lineId, int limit = 0)
        {
            // A saved prefix represents the original corpus before editorial retirements.
            return HasStoredLine(lineId) && (limit > 0 ? lineId < limit : !retired.Contains(lineId));
        }

        internal bool HasGroup(int group, int limit)
        {
            if (!groupHeads.TryGetValue(group, out var lineId))
                return false;
            while (lineId >= 0 && !HasLine(lineId, limit))
                lineId = nextInGroup[lineId];
            return lineId >= 0;
        }

        internal int[] LinesInWork(int workIndex)
        {
            return workIndex >= 0 && workIndex < workLines.Length ? workLines[workIndex] : null;
        }

        internal PoetryAnswer Judge(PoetryCard card, int position, string key, int lineLimit)
        {
            if (key == null)
                return new PoetryAnswer(PoetryAnswerStatus.Unknown);
            if (key.Length == 0)
                return new PoetryAnswer(PoetryAnswerStatus.Empty);
            if (!TryFindMatch(key, lineLimit, out var lineId))
                return new PoetryAnswer(IsMultipleLines(key, lineLimit) ? PoetryAnswerStatus.MultipleLines : PoetryAnswerStatus.Unknown);

            var status = RuleStatus(card, position, key, lineId);
            if (status == PoetryAnswerStatus.WrongTheme && otherMatches.TryGetValue(key, out var otherLines))
                foreach (var otherLine in otherLines)
                    if (HasLine(otherLine, lineLimit) && card.Reasons.ContainsKey(otherLine))
                        return new PoetryAnswer(PoetryAnswerStatus.Accepted, otherLine);
            if (status == PoetryAnswerStatus.Accepted && card.Rule != PoetryRule.Theme &&
                !HasCandidateGroup(card, position, Lines[lineId].Group, lineLimit))
                status = PoetryAnswerStatus.Unavailable;
            return new PoetryAnswer(status, lineId);
        }

        private bool TryFindMatch(string key, int lineLimit, out int lineId)
        {
            if (firstMatches.TryGetValue(key, out lineId))
            {
                if (HasLine(lineId, lineLimit))
                    return true;
                if (otherMatches.TryGetValue(key, out var alternatives))
                    foreach (var candidate in alternatives)
                        if (HasLine(candidate, lineLimit))
                        {
                            lineId = candidate;
                            return true;
                        }
            }
            return false;
        }

        private bool HasCandidateGroup(PoetryCard card, int position, int group, int lineLimit)
        {
            if (lineLimit == 0)
                return Array.BinarySearch(activeCandidateGroups[card][card.CandidateIndex(position)], group) >= 0;
            foreach (var candidate in card.AtPosition(position))
                if (HasLine(candidate, lineLimit) && Lines[candidate].Group == group)
                    return true;
            return false;
        }

        internal string Suggestion(PoetryCard card, int position, int lineId, int lineLimit)
        {
            return HasLine(lineId, lineLimit) && TryFindLegalVariant(card, position, lineId, out var alias)
                ? alias ?? Text(lineId) : null;
        }

        internal static string Normalize(string input)
        {
            if (input == null)
                return string.Empty;
            if (input.Length > MaximumInputLength || !HasValidUtf16(input))
                return null;
            var key = CanonicalizeMarks(input.Trim().Normalize(NormalizationForm.FormC));
            if (key.Length == 0)
                return key;
            return IsTerminalMark(key[key.Length - 1])
                ? key.Substring(0, key.Length - 1).TrimEnd()
                : key;
        }

        private static string CanonicalizeMarks(string text)
        {
            StringBuilder result = null;
            for (var index = 0; index < text.Length; index++)
            {
                var mark = CanonicalMark(text[index]);
                if (mark == text[index])
                    continue;
                if (result == null)
                    result = new StringBuilder(text);
                result[index] = mark;
            }
            return result == null ? text : result.ToString();
        }

        private void ValidateWorks()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var work in Works)
                if (work == null || string.IsNullOrWhiteSpace(work.Id) || !ids.Add(work.Id) ||
                    work.Author == null || string.IsNullOrWhiteSpace(work.Title) || string.IsNullOrWhiteSpace(work.Body))
                    throw new ArgumentException("Invalid poetry work identity or text.");
        }

        private void IndexLines()
        {
            var byWork = new List<int>[Works.Length];
            for (var id = 0; id < Lines.Length; id++)
            {
                var line = Lines[id];
                if (line == null || line.WorkIndex < 0 || line.WorkIndex >= Works.Length ||
                    line.Start < 0 || line.Length <= 0 || line.Group < 0 || line.Aliases == null ||
                    line.Start > Works[line.WorkIndex].Body.Length - line.Length)
                    throw new ArgumentException("Invalid poetry line reference.");

                nextInGroup[id] = groupHeads.TryGetValue(line.Group, out var groupHead) ? groupHead : -1;
                groupHeads[line.Group] = id;
                lineKeys[id] = Normalize(Text(id));
                AddMatch(lineKeys[id], id);
                foreach (var alias in line.Aliases)
                    AddMatch(Normalize(alias), id);

                followingLines[id] = precedingLines[id] = -1;
                var work = line.WorkIndex;
                if (byWork[work] == null)
                    byWork[work] = new List<int>();
                byWork[work].Add(id);
            }

            for (var work = 0; work < byWork.Length; work++)
            {
                var ids = byWork[work];
                if (ids == null)
                {
                    workLines[work] = Array.Empty<int>();
                    continue;
                }
                ids.Sort((left, right) => Lines[left].Start.CompareTo(Lines[right].Start));
                workLines[work] = ids.ToArray();
                var body = Works[work].Body;
                for (var index = 1; index < ids.Count; index++)
                {
                    var previous = Lines[ids[index - 1]];
                    var next = Lines[ids[index]];
                    var end = previous.Start + previous.Length;
                    if (end > next.Start)
                        throw new ArgumentException("Overlapping poetry line references.");
                    if (SkipSeparators(body, end, next.Start) == next.Start)
                    {
                        followingLines[ids[index - 1]] = ids[index];
                        precedingLines[ids[index]] = ids[index - 1];
                    }
                }
            }
        }

        private void AddMatch(string key, int lineId)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Invalid poetry line or alias.");
            maximumKeyLength = Math.Max(maximumKeyLength, key.Length);
            if (!firstMatches.TryGetValue(key, out var firstLine))
            {
                firstMatches.Add(key, lineId);
                return;
            }
            if (Lines[firstLine].Group != Lines[lineId].Group)
                throw new ArgumentException("A poetry text key belongs to different answer groups: " + key);
            if (firstLine == lineId)
                return;
            if (!otherMatches.TryGetValue(key, out var matches))
            {
                matches = new List<int>();
                otherMatches.Add(key, matches);
            }
            if (matches.Count == 0 || matches[matches.Count - 1] != lineId)
                matches.Add(lineId);
        }

        private void ValidateCard(PoetryCard card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.Id) || string.IsNullOrWhiteSpace(card.Title) ||
                cardsById.ContainsKey(card.Id) || card.Key == null || card.SecondKey == null || card.Candidates == null)
                throw new ArgumentException("Invalid poetry card identity.");
            var positionRule = card.Rule == PoetryRule.Position;
            var pairRule = card.Rule == PoetryRule.Pair;
            var themeRule = card.Rule == PoetryRule.Theme;
            if (card.Rule < PoetryRule.Character || card.Rule > PoetryRule.Theme ||
                (positionRule ? card.Width != 5 && card.Width != 7 : card.Width != 0) ||
                (pairRule ? !IsSingleHan(card.SecondKey) || card.SecondKey == card.Key : card.SecondKey.Length != 0) ||
                (themeRule ? card.Key.Length != 0 : !IsSingleHan(card.Key) || card.Reasons.Count != 0) ||
                card.Candidates.Length != (positionRule ? card.Width : 1))
                throw new ArgumentException("Invalid poetry card rule: " + card.Id);

            var compiledGroups = new int[card.Candidates.Length][];
            for (var index = 0; index < card.Candidates.Length; index++)
            {
                var candidates = card.Candidates[index];
                var seen = new HashSet<int>();
                var groupsAtPosition = new HashSet<int>();
                if (candidates == null || candidates.Length == 0 || (themeRule && card.Reasons.Count != candidates.Length))
                    throw new ArgumentException("Incomplete poetry card candidates: " + card.Id);
                foreach (var candidate in candidates)
                {
                    if (!HasStoredLine(candidate) || !seen.Add(candidate) ||
                        !TryFindLegalVariant(card, index + 1, candidate, out _) ||
                        (themeRule && (!card.Reasons.TryGetValue(candidate, out var reason) || string.IsNullOrWhiteSpace(reason))))
                        throw new ArgumentException("Invalid poetry card candidate: " + card.Id + " / " + candidate);
                    if (!retired.Contains(candidate))
                        groupsAtPosition.Add(Lines[candidate].Group);
                }
                var allowedGroups = new int[groupsAtPosition.Count];
                groupsAtPosition.CopyTo(allowedGroups);
                Array.Sort(allowedGroups);
                compiledGroups[index] = allowedGroups;
            }
            activeCandidateGroups.Add(card, compiledGroups);
            cardsById.Add(card.Id, card);
        }

        private bool TryFindLegalVariant(PoetryCard card, int position, int lineId, out string alias)
        {
            alias = null;
            if (RuleStatus(card, position, lineKeys[lineId], lineId) == PoetryAnswerStatus.Accepted)
                return true;
            foreach (var alternate in Lines[lineId].Aliases)
            {
                if (RuleStatus(card, position, Normalize(alternate), lineId) != PoetryAnswerStatus.Accepted)
                    continue;
                alias = alternate;
                return true;
            }
            return false;
        }

        private static PoetryAnswerStatus RuleStatus(PoetryCard card, int position, string key, int lineId)
        {
            switch (card.Rule)
            {
                case PoetryRule.Character:
                    return key.IndexOf(card.Key, StringComparison.Ordinal) >= 0
                        ? PoetryAnswerStatus.Accepted : PoetryAnswerStatus.WrongCharacter;
                case PoetryRule.Position:
                    return MatchesPosition(key, card.Key, card.Width, position)
                        ? PoetryAnswerStatus.Accepted : PoetryAnswerStatus.WrongPosition;
                case PoetryRule.Pair:
                    return key.IndexOf(card.Key, StringComparison.Ordinal) >= 0 && key.IndexOf(card.SecondKey, StringComparison.Ordinal) >= 0
                        ? PoetryAnswerStatus.Accepted : PoetryAnswerStatus.WrongPair;
                default:
                    return card.Reasons.ContainsKey(lineId) ? PoetryAnswerStatus.Accepted : PoetryAnswerStatus.WrongTheme;
            }
        }

        private bool IsMultipleLines(string input, int lineLimit)
        {
            bool?[] separated = null;
            for (var end = 1; end <= maximumKeyLength && end < input.Length; end++)
            {
                var prefix = input.Substring(0, end);
                if (!TryFindMatch(prefix, lineLimit, out var first))
                    continue;
                var nextStart = SkipSeparators(input, end, input.Length);
                if (nextStart == input.Length)
                    continue;
                if (nextStart > end)
                {
                    if (separated == null)
                        separated = new bool?[input.Length];
                    if (MatchesSeparated(input, nextStart, separated, lineLimit))
                        return true;
                }
                if (MatchesFollowing(input, nextStart, followingLines[first], lineLimit))
                    return true;
                if (otherMatches.TryGetValue(prefix, out var matches))
                    foreach (var match in matches)
                        if (match != first && HasLine(match, lineLimit) &&
                            MatchesFollowing(input, nextStart, followingLines[match], lineLimit))
                            return true;
            }
            return false;
        }

        private bool MatchesSeparated(string input, int start, bool?[] cache, int lineLimit)
        {
            if (cache[start].HasValue)
                return cache[start].Value;
            var limit = Math.Min(start + maximumKeyLength, input.Length);
            for (var end = start + 1; end <= limit; end++)
            {
                if (!TryFindMatch(input.Substring(start, end - start), lineLimit, out _))
                    continue;
                var nextStart = SkipSeparators(input, end, input.Length);
                if (end == input.Length ||
                    (nextStart > end && nextStart < input.Length && MatchesSeparated(input, nextStart, cache, lineLimit)))
                {
                    cache[start] = true;
                    return true;
                }
            }
            cache[start] = false;
            return false;
        }

        private bool MatchesFollowing(string input, int start, int lineId, int lineLimit)
        {
            if (!HasLine(lineId, lineLimit))
                return false;
            if (MatchesFollowingKey(input, start, lineId, lineKeys[lineId], lineLimit))
                return true;
            foreach (var alias in Lines[lineId].Aliases)
                if (MatchesFollowingKey(input, start, lineId, Normalize(alias), lineLimit))
                    return true;
            return false;
        }

        private bool MatchesFollowingKey(string input, int start, int lineId, string key, int lineLimit)
        {
            if (input.Length - start < key.Length || string.CompareOrdinal(input, start, key, 0, key.Length) != 0)
                return false;
            var end = start + key.Length;
            return end == input.Length ||
                MatchesFollowing(input, SkipSeparators(input, end, input.Length), followingLines[lineId], lineLimit);
        }

        private static bool MatchesPosition(string text, string key, int width, int position)
        {
            var count = 0;
            var matched = false;
            for (var index = 0; index < text.Length;)
            {
                var code = char.ConvertToUtf32(text, index);
                var length = code >= 0x10000 ? 2 : 1;
                if (IsHan(code))
                {
                    count++;
                    if (count == position)
                        matched = length == key.Length && string.CompareOrdinal(text, index, key, 0, length) == 0;
                }
                else if (!char.IsWhiteSpace(text, index) && !char.IsPunctuation(text, index))
                {
                    return false;
                }
                index += length;
            }
            return matched && count == width;
        }

        private static bool IsSingleHan(string key)
        {
            if (key.Length == 0 || key.Length > 2 || !HasValidUtf16(key))
                return false;
            var code = char.ConvertToUtf32(key, 0);
            return key.Length == (code >= 0x10000 ? 2 : 1) && IsHan(code);
        }

        private static bool IsHan(int code)
        {
            // Keep the playable ranges in sync with HAN_RANGES in the desktop importer.
            return (code >= 0x3400 && code <= 0x4DBF) || (code >= 0x4E00 && code <= 0x9FFF) ||
                (code >= 0xF900 && code <= 0xFAFF) || (code >= 0x20000 && code <= 0x2A6DF) ||
                (code >= 0x2A700 && code <= 0x2B73F) || (code >= 0x2B740 && code <= 0x2B81F) ||
                (code >= 0x2B820 && code <= 0x2CEAF) || (code >= 0x2CEB0 && code <= 0x2EBEF) ||
                (code >= 0x2EBF0 && code <= 0x2EE5F) || (code >= 0x2F800 && code <= 0x2FA1F) ||
                (code >= 0x30000 && code <= 0x3134F) || (code >= 0x31350 && code <= 0x323AF);
        }

        private static bool HasValidUtf16(string text)
        {
            for (var index = 0; index < text.Length; index++)
                if (char.IsSurrogate(text[index]))
                {
                    if (!char.IsHighSurrogate(text[index]) || index + 1 >= text.Length || !char.IsLowSurrogate(text[index + 1]))
                        return false;
                    index++;
                }
            return true;
        }

        private static int SkipSeparators(string text, int index, int limit)
        {
            while (index < limit)
            {
                var mark = CanonicalMark(text[index]);
                if (!char.IsWhiteSpace(mark) && !IsTerminalMark(mark) && mark != '、')
                    break;
                index++;
            }
            return index;
        }

        private static bool IsTerminalMark(char mark)
        {
            return mark == '.' || mark == ',' || mark == '!' || mark == '?' || mark == ';' || mark == ':';
        }

        private static char CanonicalMark(char mark)
        {
            switch (mark)
            {
                case '。':
                case '．': return '.';
                case '，': return ',';
                case '！': return '!';
                case '？': return '?';
                case '；': return ';';
                case '：': return ':';
                case '､': return '、';
                default: return mark;
            }
        }
    }
}
