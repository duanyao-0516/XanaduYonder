using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Idiom
{
    public sealed class IdiomGame
    {
        private readonly IdiomLexicon lexicon;
        private readonly List<string> history = new List<string>();
        private readonly List<IdiomHint> hints = new List<IdiomHint>();
        private readonly HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
        private uint randomState;
        public IdiomMode Mode { get; private set; }
        public IdiomActor FirstActor { get; private set; }
        public IdiomEndReason EndReason { get; private set; }
        public bool IsStarted { get { return FirstActor != IdiomActor.None; } }
        public bool IsEnded { get { return EndReason != IdiomEndReason.None; } }
        public int PlayerCount { get { return (history.Count + (FirstActor == IdiomActor.Player ? 1 : 0)) / 2; } }
        public int HintCount { get { return hints.Count; } }
        public IReadOnlyList<string> History { get; private set; }
        public IdiomEntry LastEntry { get { return At(history.Count - 1); } }
        public IdiomHint CurrentHint
        {
            get { return hints.Count > 0 && hints[hints.Count - 1].TurnIndex == history.Count ? hints[hints.Count - 1] : null; }
        }

        public IdiomGame(IdiomLexicon lexicon, IdiomMode mode, uint seed)
        {
            if (lexicon == null) throw new ArgumentNullException("lexicon");
            if (!ValidMode(mode)) throw new ArgumentOutOfRangeException("mode");
            this.lexicon = lexicon; Mode = mode; randomState = seed == 0 ? 0x9E3779B9u : seed;
            History = history.AsReadOnly();
        }

        public IdiomGame(IdiomLexicon lexicon, IdiomSnapshot snapshot)
        {
            if (lexicon == null) throw new ArgumentNullException("lexicon");
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            this.lexicon = lexicon;
            Mode = snapshot.Mode; FirstActor = snapshot.FirstActor; EndReason = snapshot.EndReason;
            randomState = snapshot.RandomState;
            history.AddRange(snapshot.History);
            foreach (var hint in snapshot.Hints)
                hints.Add(hint == null ? null : new IdiomHint(hint.TurnIndex, hint.EntryIds));
            History = history.AsReadOnly();
            ValidateRestoredState();
        }

        public IdiomActionResult Start(IdiomActor actor)
        {
            if (actor != IdiomActor.App && actor != IdiomActor.Player) throw new ArgumentOutOfRangeException("actor");
            if (IsStarted) return Result(IdiomError.AlreadyStarted);
            FirstActor = actor;
            if (actor == IdiomActor.App)
            {
                Append(ChooseApp());
                EndReason = NaturalEnd(IdiomActor.Player, LastEntry, used);
            }
            return Result(IdiomError.None);
        }

        public IdiomActionResult Submit(string text)
        {
            if (!IsStarted) return Result(IdiomError.NotStarted);
            if (IsEnded) return Result(IdiomError.AlreadyEnded);
            string word = text == null ? null : text.Trim(); string first, last;
            if (!IdiomCharacters.TryFour(word, out first, out last)) return Result(IdiomError.InvalidWord);
            IdiomEntry entry;
            if (!lexicon.TryGetWord(word, out entry)) return Result(IdiomError.UnknownWord);
            if (used.Contains(entry.Id)) return Result(IdiomError.RepeatedWord);
            if (!Matches(LastEntry, entry)) return Result(IdiomError.WrongStart);

            // A public action commits the player word and any APP reply together.
            Append(entry);
            EndReason = NaturalEnd(IdiomActor.App, LastEntry, used);
            if (!IsEnded)
            {
                Append(ChooseApp());
                EndReason = NaturalEnd(IdiomActor.Player, LastEntry, used);
            }
            return Result(IdiomError.None);
        }

        public IdiomHint RequestHint()
        {
            if (!IsStarted) return null;
            if (CurrentHint != null) return CurrentHint;
            if (IsEnded) return null;
            var choices = BestCandidates(LastEntry);
            if (choices.Count == 0) return null;
            var ids = new List<string>();
            for (int i = 0; i < choices.Count && i < 3; i++) ids.Add(choices[i].Id);
            var hint = new IdiomHint(history.Count, ids);
            hints.Add(hint);
            if (hints.Count == 3) EndReason = IdiomEndReason.HintLimit;
            return hint;
        }

        public void Finish()
        {
            if (IsStarted && !IsEnded) EndReason = IdiomEndReason.EndedByPlayer;
        }

        public IdiomSnapshot GetSnapshot()
        {
            return new IdiomSnapshot(Mode, FirstActor, EndReason, randomState, history, hints);
        }

        private IdiomEntry ChooseApp()
        {
            var choices = BestCandidates(LastEntry);
            if (choices.Count == 0) throw new InvalidOperationException("APP action requires an available move.");
            // Xorshift32 is explicitly persisted; neither hints nor failed commands draw.
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return choices[(int)(randomState % (uint)choices.Count)];
        }

        private List<IdiomEntry> BestCandidates(IdiomEntry last)
        {
            var best = new List<IdiomEntry>(); int bestScore = -1;
            var commonOpenings = new List<IdiomEntry>(); int bestOpeningScore = -1;
            foreach (var candidate in lexicon.Following(Mode, last))
            {
                if (used.Contains(candidate.Id)) continue;
                int continuations = 0;
                foreach (var next in lexicon.Following(Mode, candidate))
                {
                    if (next.Id != candidate.Id && !used.Contains(next.Id) && ++continuations == 3) break;
                }
                int tier = continuations >= 3 ? 2 : continuations > 0 ? 1 : 0;
                int score = tier * 2 + (candidate.Common ? 1 : 0);
                if (last == null && candidate.Common && continuations > 0)
                {
                    if (score > bestOpeningScore) { bestOpeningScore = score; commonOpenings.Clear(); }
                    if (score == bestOpeningScore) commonOpenings.Add(candidate);
                }
                if (score > bestScore) { bestScore = score; best.Clear(); }
                if (score == bestScore) best.Add(candidate);
            }
            return commonOpenings.Count > 0 ? commonOpenings : best;
        }

        private IdiomEndReason NaturalEnd(IdiomActor nextActor, IdiomEntry last, HashSet<string> occupied)
        {
            if (occupied.Count == lexicon.Count) return IdiomEndReason.DictionaryExhausted;
            foreach (var candidate in lexicon.Following(Mode, last))
                if (!occupied.Contains(candidate.Id)) return IdiomEndReason.None;
            return nextActor == IdiomActor.Player ? IdiomEndReason.PlayerNoMoves : IdiomEndReason.AppNoMoves;
        }

        private bool Matches(IdiomEntry previous, IdiomEntry next)
        {
            return previous == null || (Mode == IdiomMode.SameCharacter
                ? previous.LastCharacter == next.FirstCharacter : previous.LastKey == next.FirstKey);
        }
        private IdiomActor ActorAt(int index)
        {
            if ((index & 1) == 0) return FirstActor;
            return FirstActor == IdiomActor.App ? IdiomActor.Player : IdiomActor.App;
        }
        private IdiomEntry At(int index)
        {
            if (index < 0) return null;
            IdiomEntry entry; lexicon.TryGetById(history[index], out entry); return entry;
        }
        private void Append(IdiomEntry entry) { used.Add(entry.Id); history.Add(entry.Id); }
        private static IdiomActionResult Result(IdiomError error) { return new IdiomActionResult(error); }
        private static bool ValidMode(IdiomMode mode) { return mode == IdiomMode.SameCharacter || mode == IdiomMode.SameSound; }
        private static void Invalid(string message) { throw new ArgumentException("Invalid idiom snapshot: " + message, "snapshot"); }

        private void ValidateRestoredState()
        {
            if (!ValidMode(Mode) || FirstActor < IdiomActor.None || FirstActor > IdiomActor.Player ||
                EndReason < IdiomEndReason.None || EndReason > IdiomEndReason.DictionaryExhausted || randomState == 0)
                Invalid("unsupported mode, actor, end reason, or random state.");
            if (history.Count > lexicon.Count || hints.Count > 3) Invalid("history or hints exceeds bounds.");
            if (!IsStarted)
            {
                if (history.Count != 0 || hints.Count != 0 || IsEnded) Invalid("unstarted game contains progress.");
                return;
            }
            if (FirstActor == IdiomActor.App && history.Count == 0) Invalid("APP opening is missing.");
            IdiomEntry previous = null;
            foreach (string id in history)
            {
                IdiomEntry entry;
                if (!lexicon.TryGetById(id, out entry) || !used.Add(id)) Invalid("unknown or repeated history id.");
                if (!Matches(previous, entry)) Invalid("history does not follow the selected rule.");
                previous = entry;
            }
            ValidateHints();
            IdiomActor nextActor = ActorAt(history.Count);
            IdiomEndReason natural = NaturalEnd(nextActor, LastEntry, used);
            if (hints.Count == 3 && EndReason != IdiomEndReason.HintLimit) Invalid("third hint must end the game.");
            if (EndReason == IdiomEndReason.HintLimit)
            {
                if (hints.Count != 3 || hints[2].TurnIndex != history.Count || nextActor != IdiomActor.Player || natural != IdiomEndReason.None)
                    Invalid("hint terminal does not match its final player turn.");
            }
            else if (EndReason == IdiomEndReason.None || EndReason == IdiomEndReason.EndedByPlayer)
            {
                if (nextActor != IdiomActor.Player || natural != IdiomEndReason.None) Invalid("incomplete round or unrecorded natural ending.");
            }
            else if (EndReason != natural) Invalid("end reason disagrees with remaining legal moves.");
        }

        private void ValidateHints()
        {
            int previousTurn = -1, prefixLength = 0;
            var prefix = new HashSet<string>(StringComparer.Ordinal);
            foreach (var hint in hints)
            {
                if (hint == null || hint.TurnIndex <= previousTurn || hint.TurnIndex > history.Count ||
                    hint.TurnIndex < 0 || ActorAt(hint.TurnIndex) != IdiomActor.Player ||
                    hint.EntryIds.Count < 1 || hint.EntryIds.Count > 3)
                    Invalid("hint has invalid turn, order, or candidate count.");
                while (prefixLength < hint.TurnIndex) prefix.Add(history[prefixLength++]);
                IdiomEntry last = At(hint.TurnIndex - 1);
                var candidates = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in hint.EntryIds)
                {
                    IdiomEntry entry;
                    if (!lexicon.TryGetById(id, out entry) || prefix.Contains(id) || !candidates.Add(id) || !Matches(last, entry))
                        Invalid("hint candidate was not legal at its recorded turn.");
                }
                previousTurn = hint.TurnIndex;
            }
        }
    }
}
