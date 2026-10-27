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
        private uint hintRandomState;
        private readonly IdiomLearningProfile profile;
        private readonly Func<long> utcNow;
        private readonly HashSet<string> assisted = new HashSet<string>(StringComparer.Ordinal);
        private IdiomSelectionContext endSelectionContext;
        public IdiomMode Mode { get; private set; }
        public IdiomMatchKind MatchKind { get; private set; }
        public IdiomActor FirstActor { get; private set; }
        public IdiomActor NextActor { get { return ActorAt(history.Count); } }
        public IdiomEndReason EndReason { get; private set; }
        public bool IsStarted { get { return FirstActor != IdiomActor.None; } }
        public bool IsEnded { get { return EndReason != IdiomEndReason.None; } }
        public int PlayerCount { get { return (history.Count + (FirstActor == IdiomActor.Player ? 1 : 0)) / 2; } }
        public int RightCount { get { return PlayerCount; } }
        public int LeftCount { get { return history.Count - RightCount; } }
        public int HintCount { get { return hints.Count; } }
        public IReadOnlyList<string> History { get; private set; }
        public IdiomEntry LastEntry { get { return At(history.Count - 1); } }
        public IdiomHint CurrentHint
        {
            get { return hints.Count > 0 && hints[hints.Count - 1].TurnIndex == history.Count ? hints[hints.Count - 1] : null; }
        }

        public IdiomGame(IdiomLexicon lexicon, IdiomMode mode, uint seed)
            : this(lexicon, mode, seed, IdiomMatchKind.Computer) { }

        public IdiomGame(IdiomLexicon lexicon, IdiomMode mode, uint seed, IdiomMatchKind matchKind)
            : this(lexicon, mode, seed, matchKind, new IdiomLearningProfile(lexicon), () => DateTimeOffset.UtcNow.ToUnixTimeSeconds()) { }

        public IdiomGame(IdiomLexicon lexicon, IdiomMode mode, uint seed, IdiomMatchKind matchKind,
            IdiomLearningProfile profile, Func<long> utcNow)
        {
            if (lexicon == null) throw new ArgumentNullException("lexicon");
            if (!ValidMode(mode)) throw new ArgumentOutOfRangeException("mode");
            if (!ValidMatchKind(matchKind)) throw new ArgumentOutOfRangeException("matchKind");
            this.lexicon = lexicon; Mode = mode; MatchKind = matchKind; randomState = seed == 0 ? 0x9E3779B9u : seed;
            this.profile = profile ?? throw new ArgumentNullException("profile");
            this.utcNow = utcNow ?? throw new ArgumentNullException("utcNow");
            hintRandomState = IdiomSnapshot.HintSeed(randomState);
            History = history.AsReadOnly();
        }

        public IdiomGame(IdiomLexicon lexicon, IdiomSnapshot snapshot)
            : this(lexicon, snapshot, new IdiomLearningProfile(lexicon), () => DateTimeOffset.UtcNow.ToUnixTimeSeconds()) { }

        public IdiomGame(IdiomLexicon lexicon, IdiomSnapshot snapshot, IdiomLearningProfile profile, Func<long> utcNow)
        {
            if (lexicon == null) throw new ArgumentNullException("lexicon");
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            this.lexicon = lexicon;
            this.profile = profile ?? throw new ArgumentNullException("profile");
            this.utcNow = utcNow ?? throw new ArgumentNullException("utcNow");
            Mode = snapshot.Mode; MatchKind = snapshot.MatchKind; FirstActor = snapshot.FirstActor; EndReason = snapshot.EndReason;
            randomState = snapshot.RandomState;
            hintRandomState = snapshot.HintRandomState;
            foreach (string id in snapshot.AssistedEntryIds)
            {
                IdiomEntry entry;
                if (!lexicon.TryGetById(id, out entry) || !assisted.Add(id)) Invalid("invalid assisted word.");
            }
            endSelectionContext = snapshot.EndSelectionContext;
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
            long now = Now();
            FirstActor = actor;
            if (MatchKind == IdiomMatchKind.Computer && actor == IdiomActor.App)
            {
                var opening = ChooseApp(now);
                if (opening == null) EndReason = IdiomEndReason.RecommendationExhausted;
                else { Append(opening); profile.RecordApp(opening.Id, now); EndReason = NaturalEnd(IdiomActor.Player, LastEntry, used); }
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
            long now = Now();
            if (MatchKind == IdiomMatchKind.Computer)
                profile.RecordUse(entry.Id, assisted.Contains(entry.Id) || Contains(CurrentHint, entry.Id));
            Append(entry);
            assisted.Clear();
            if (MatchKind == IdiomMatchKind.Local)
            {
                EndReason = NaturalEnd(NextActor, LastEntry, used);
                return Result(IdiomError.None);
            }
            // A computer-match action commits the player word and any APP reply together.
            EndReason = NaturalEnd(IdiomActor.App, LastEntry, used);
            if (!IsEnded)
            {
                var reply = ChooseApp(now);
                if (reply == null) EndReason = IdiomEndReason.RecommendationExhausted;
                else { Append(reply); profile.RecordApp(reply.Id, now); EndReason = NaturalEnd(IdiomActor.Player, LastEntry, used); }
            }
            return Result(IdiomError.None);
        }

        public IdiomHint RequestHint()
        {
            if (!IsStarted) return null;
            if (CurrentHint != null) return CurrentHint;
            if (IsEnded) return null;
            long now = Now();
            var context = profile.SelectionContext(now, false);
            var choices = Candidates(context, LastEntry);
            if (choices.Count == 0) return null;
            var ids = new List<string>();
            var ordinary = new List<Candidate>(); var review = new List<Candidate>();
            foreach (var choice in choices) (profile.IsNotebook(choice.Entry.Id) ? review : ordinary).Add(choice);
            AddHints(ordinary, ids, 2, now); AddHints(review, ids, 1, now);
            ordinary.AddRange(review); AddHints(ordinary, ids, 3 - ids.Count, now);
            var hint = new IdiomHint(history.Count, ids);
            hints.Add(hint);
            if (HintCountFor(NextActor) == 3) EndReason = IdiomEndReason.HintLimit;
            return hint;
        }
        private void AddHints(List<Candidate> choices, List<string> ids, int count, long now)
        {
            while (choices.Count > 0 && count-- > 0)
            {
                var choice = Pick(choices, ref hintRandomState, now);
                ids.Add(choice.Entry.Id); assisted.Add(choice.Entry.Id); choices.Remove(choice);
            }
        }

        public void RecordDefinitionViewed(string entryId)
        {
            if (!IsStarted || IsEnded) return;
            IdiomEntry entry;
            if (!lexicon.TryGetById(entryId, out entry)) throw new ArgumentException("Unknown definition entry.", "entryId");
            assisted.Add(entryId);
        }

        public int HintCountFor(IdiomActor actor)
        {
            if (actor < IdiomActor.None || actor > IdiomActor.Player) throw new ArgumentOutOfRangeException("actor");
            int count = 0;
            foreach (var hint in hints) if (ActorAt(hint.TurnIndex) == actor) count++;
            return count;
        }

        public void Finish()
        {
            if (IsStarted && !IsEnded) EndReason = IdiomEndReason.EndedByPlayer;
        }

        public IdiomSnapshot GetSnapshot()
        {
            var assistedIds = new List<string>(assisted); assistedIds.Sort(StringComparer.Ordinal);
            return new IdiomSnapshot(Mode, FirstActor, EndReason, randomState, history, hints, MatchKind,
                hintRandomState, assistedIds, endSelectionContext);
        }

        private IdiomEntry ChooseApp(long now)
        {
            var context = profile.SelectionContext(now, true);
            var choices = Candidates(context, LastEntry);
            if (choices.Count == 0) { endSelectionContext = context; return null; }
            var ordinary = new List<Candidate>(); var review = new List<Candidate>();
            foreach (var choice in choices) (profile.IsNotebook(choice.Entry.Id) ? review : ordinary).Add(choice);
            var pool = ordinary.Count == 0 ? review : review.Count == 0 ? ordinary : Next(ref randomState) % 100 < 20 ? review : ordinary;
            return Pick(pool, ref randomState, now).Entry;
        }
        private sealed class Candidate
        {
            internal IdiomEntry Entry;
            internal int Continuations;
        }
        private List<Candidate> Candidates(IdiomSelectionContext context, IdiomEntry last)
        {
            var ordinary = new HashSet<string>(context.OrdinaryIds, StringComparer.Ordinal);
            var review = new HashSet<string>(context.ReviewIds, StringComparer.Ordinal);
            var result = new List<Candidate>();
            foreach (var candidate in lexicon.Following(Mode, last))
            {
                if (used.Contains(candidate.Id) || (!ordinary.Contains(candidate.Id) && !review.Contains(candidate.Id))) continue;
                int continuations = 0;
                foreach (var next in lexicon.Following(Mode, candidate))
                {
                    if (next.Id != candidate.Id && !used.Contains(next.Id) && ordinary.Contains(next.Id) && ++continuations == 3) break;
                }
                if (continuations > 0) result.Add(new Candidate { Entry = candidate, Continuations = continuations });
            }
            return result;
        }
        private Candidate Pick(List<Candidate> choices, ref uint state, long now)
        {
            ulong total = 0;
            foreach (var choice in choices) total += (uint)profile.Weight(choice.Entry, choice.Continuations, now);
            ulong draw = Next(ref state) % total;
            foreach (var choice in choices)
            {
                uint weight = (uint)profile.Weight(choice.Entry, choice.Continuations, now);
                if (draw < weight) return choice;
                draw -= weight;
            }
            throw new InvalidOperationException("Invalid selection weight.");
        }
        private static uint Next(ref uint state) { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
        private long Now() { long value = utcNow(); IdiomLearningProfile.RequireTime(value); return value; }
        private static bool Contains(IdiomHint hint, string id)
        { if (hint != null) foreach (string value in hint.EntryIds) if (value == id) return true; return false; }

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
        public IdiomActor ActorAt(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException("index");
            if (!IsStarted) return IdiomActor.None;
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
        private static bool ValidMatchKind(IdiomMatchKind kind) { return kind == IdiomMatchKind.Computer || kind == IdiomMatchKind.Local; }
        private static void Invalid(string message) { throw new ArgumentException("Invalid idiom snapshot: " + message, "snapshot"); }

        private void ValidateRestoredState()
        {
            if (!ValidMode(Mode) || !ValidMatchKind(MatchKind) || FirstActor < IdiomActor.None || FirstActor > IdiomActor.Player ||
                EndReason < IdiomEndReason.None || EndReason > IdiomEndReason.RecommendationExhausted || randomState == 0 || hintRandomState == 0)
                Invalid("unsupported mode, actor, end reason, or random state.");
            if (history.Count > lexicon.Count || hints.Count > (MatchKind == IdiomMatchKind.Local ? 5 : 3)) Invalid("history or hints exceeds bounds.");
            if (!IsStarted)
            {
                if (history.Count != 0 || hints.Count != 0 || assisted.Count != 0 || endSelectionContext != null || IsEnded) Invalid("unstarted game contains progress.");
                return;
            }
            if (MatchKind == IdiomMatchKind.Computer && FirstActor == IdiomActor.App && history.Count == 0 && EndReason != IdiomEndReason.RecommendationExhausted) Invalid("APP opening is missing.");
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
            bool hintLimit = HintCountFor(IdiomActor.App) == 3 || HintCountFor(IdiomActor.Player) == 3;
            if (hintLimit && EndReason != IdiomEndReason.HintLimit) Invalid("third hint must end the game.");
            if (EndReason != IdiomEndReason.RecommendationExhausted && endSelectionContext != null) Invalid("unexpected policy ending context.");
            if (EndReason == IdiomEndReason.HintLimit)
            {
                if (!hintLimit || hints[hints.Count - 1].TurnIndex != history.Count || HintCountFor(nextActor) != 3 || natural != IdiomEndReason.None)
                    Invalid("hint terminal does not match its final human turn.");
            }
            else if (EndReason == IdiomEndReason.None || EndReason == IdiomEndReason.EndedByPlayer)
            {
                if ((MatchKind == IdiomMatchKind.Computer && nextActor != IdiomActor.Player) || natural != IdiomEndReason.None)
                    Invalid("incomplete round or unrecorded natural ending.");
            }
            else if (EndReason == IdiomEndReason.RecommendationExhausted)
            {
                if (MatchKind != IdiomMatchKind.Computer || nextActor != IdiomActor.App || natural != IdiomEndReason.None || endSelectionContext == null)
                    Invalid("policy ending is not an available computer turn.");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var group in new[] { endSelectionContext.OrdinaryIds, endSelectionContext.ReviewIds })
                    foreach (string id in group)
                    { IdiomEntry entry; if (!lexicon.TryGetById(id, out entry) || !ids.Add(id)) Invalid("invalid policy eligibility."); }
                if (Candidates(endSelectionContext, LastEntry).Count != 0) Invalid("policy ending still has eligible recommendations.");
            }
            else if (EndReason != natural) Invalid("end reason disagrees with remaining legal moves.");
        }

        private void ValidateHints()
        {
            int previousTurn = -1, prefixLength = 0;
            int leftHints = 0, rightHints = 0;
            var prefix = new HashSet<string>(StringComparer.Ordinal);
            foreach (var hint in hints)
            {
                if (hint == null || hint.TurnIndex <= previousTurn || hint.TurnIndex > history.Count ||
                    hint.TurnIndex < 0 || (MatchKind == IdiomMatchKind.Computer && ActorAt(hint.TurnIndex) != IdiomActor.Player) ||
                    hint.EntryIds.Count < 1 || hint.EntryIds.Count > 3)
                    Invalid("hint has invalid turn, order, or candidate count.");
                int actorHints = ActorAt(hint.TurnIndex) == IdiomActor.App ? ++leftHints : ++rightHints;
                if (actorHints > 3 || (actorHints == 3 && hint.TurnIndex != history.Count))
                    Invalid("progress continued after a human's third hint.");
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
