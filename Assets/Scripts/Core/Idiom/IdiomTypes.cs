using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Idiom
{
    public enum IdiomMode { SameCharacter = 0, SameSound = 1 }
    public enum IdiomMatchKind { Computer = 0, Local = 1 }
    // Values 1 and 2 remain compatible with the original computer-match saves.
    public enum IdiomActor { None = 0, App = 1, Player = 2, Left = App, Right = Player }
    public enum IdiomEndReason { None = 0, HintLimit = 1, PlayerNoMoves = 2, AppNoMoves = 3, EndedByPlayer = 4, DictionaryExhausted = 5, RecommendationExhausted = 6 }
    public enum IdiomError { None = 0, NotStarted = 1, AlreadyStarted = 2, AlreadyEnded = 3, InvalidWord = 4, UnknownWord = 5, RepeatedWord = 6, WrongStart = 7 }

    public sealed class IdiomActionResult
    {
        public bool Success { get { return Error == IdiomError.None; } }
        public IdiomError Error { get; private set; }
        internal IdiomActionResult(IdiomError error) { Error = error; }
    }

    public sealed class IdiomHint
    {
        public int TurnIndex { get; private set; }
        public IReadOnlyList<string> EntryIds { get; private set; }

        public IdiomHint(int turnIndex, IEnumerable<string> entryIds)
        {
            if (entryIds == null) throw new ArgumentNullException("entryIds");
            TurnIndex = turnIndex;
            EntryIds = new List<string>(entryIds).AsReadOnly();
        }
    }

    public sealed class IdiomSnapshot
    {
        public IdiomMode Mode { get; private set; }
        public IdiomMatchKind MatchKind { get; private set; }
        public IdiomActor FirstActor { get; private set; }
        public IdiomEndReason EndReason { get; private set; }
        public uint RandomState { get; private set; }
        public uint HintRandomState { get; private set; }
        public IReadOnlyList<string> AssistedEntryIds { get; private set; }
        public IdiomSelectionContext EndSelectionContext { get; private set; }
        public IReadOnlyList<string> History { get; private set; }
        public IReadOnlyList<IdiomHint> Hints { get; private set; }

        public IdiomSnapshot(IdiomMode mode, IdiomActor firstActor, IdiomEndReason endReason,
                             uint randomState, IEnumerable<string> history, IEnumerable<IdiomHint> hints)
            : this(mode, firstActor, endReason, randomState, history, hints, IdiomMatchKind.Computer) { }

        public IdiomSnapshot(IdiomMode mode, IdiomActor firstActor, IdiomEndReason endReason,
                             uint randomState, IEnumerable<string> history, IEnumerable<IdiomHint> hints, IdiomMatchKind matchKind)
            : this(mode, firstActor, endReason, randomState, history, hints, matchKind,
                   HintSeed(randomState), new string[0], null) { }

        public IdiomSnapshot(IdiomMode mode, IdiomActor firstActor, IdiomEndReason endReason,
                             uint randomState, IEnumerable<string> history, IEnumerable<IdiomHint> hints, IdiomMatchKind matchKind,
                             uint hintRandomState, IEnumerable<string> assistedEntryIds, IdiomSelectionContext endSelectionContext)
        {
            if (history == null) throw new ArgumentNullException("history");
            if (hints == null) throw new ArgumentNullException("hints");
            if (assistedEntryIds == null) throw new ArgumentNullException("assistedEntryIds");
            Mode = mode; MatchKind = matchKind; FirstActor = firstActor; EndReason = endReason; RandomState = randomState;
            History = new List<string>(history).AsReadOnly();
            HintRandomState = hintRandomState;
            AssistedEntryIds = new List<string>(assistedEntryIds).AsReadOnly();
            EndSelectionContext = endSelectionContext == null ? null : new IdiomSelectionContext(endSelectionContext.OrdinaryIds, endSelectionContext.ReviewIds);
            var copies = new List<IdiomHint>();
            foreach (var hint in hints)
                copies.Add(hint == null ? null : new IdiomHint(hint.TurnIndex, hint.EntryIds));
            Hints = copies.AsReadOnly();
        }
        internal static uint HintSeed(uint seed) { uint value = seed ^ 0xA511E9B3u; return value == 0 ? 1u : value; }
    }

    internal static class IdiomCharacters
    {
        internal static bool TryFour(string word, out string first, out string last)
        {
            first = null; last = null;
            if (word == null) return false;
            int count = 0;
            for (int offset = 0; offset < word.Length;)
            {
                int start = offset; int scalar = word[offset++];
                if (char.IsHighSurrogate((char)scalar))
                {
                    if (offset >= word.Length || !char.IsLowSurrogate(word[offset])) return false;
                    scalar = char.ConvertToUtf32(word[start], word[offset++]);
                }
                else if (char.IsLowSurrogate((char)scalar)) return false;
                if (!IsHan(scalar) || ++count > 4) return false;
                string character = word.Substring(start, offset - start);
                if (count == 1) first = character;
                last = character;
            }
            return count == 4;
        }

        private static bool IsHan(int scalar)
        {
            return In(scalar, 0x3400, 0x4DBF) || In(scalar, 0x4E00, 0x9FFF) ||
                   In(scalar, 0xF900, 0xFAFF) || In(scalar, 0x20000, 0x2A6DF) ||
                   In(scalar, 0x2A700, 0x2B73F) || In(scalar, 0x2B740, 0x2B81F) ||
                   In(scalar, 0x2B820, 0x2CEAF) || In(scalar, 0x2CEB0, 0x2EBEF) ||
                   In(scalar, 0x2EBF0, 0x2EE5F) || In(scalar, 0x2F800, 0x2FA1F) ||
                   In(scalar, 0x30000, 0x323AF);
        }
        private static bool In(int value, int min, int max) { return value >= min && value <= max; }
    }
}
