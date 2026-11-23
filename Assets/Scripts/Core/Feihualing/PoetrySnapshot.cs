using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Feihualing
{
    public sealed class PoetrySnapshot
    {
        public string CorpusVersion { get; set; }
        public int RulesVersion { get; set; }
        public string CardId { get; set; }
        public PoetryPlayers Players { get; set; }
        public PoetryGoal Goal { get; set; }
        public PoetryPhase Phase { get; set; }
        public PoetryOutcome Outcome { get; set; }
        public int FirstPlayer { get; set; }
        public int CurrentPlayer { get; set; }
        public int Position { get; set; }
        public int Round { get; set; }
        public int Winner { get; set; }
        public long RemainingMilliseconds { get; set; }
        public long RoundElapsedMilliseconds { get; set; }
        public PoetryEntry[] History { get; set; }
        public PoetryRoundResult[] Rounds { get; set; }
        public int[] ExposedGroups { get; set; }
        public int[] HintLines { get; set; }
        public int[] CompetitionHintLines { get; set; } = Array.Empty<int>();
        public bool HintsShown { get; set; }
        public int AnswerLineLimit { get; set; }
    }

    public sealed partial class PoetrySession
    {
        public PoetrySnapshot Capture(long now)
        {
            Tick(now);
            var elapsed = Elapsed(lastObserved);
            var exposure = new int[exposedGroups.Count];
            exposedGroups.CopyTo(exposure);
            Array.Sort(exposure);
            var hintLines = new int[hints.Count];
            for (var index = 0; index < hintLines.Length; index++)
                hintLines[index] = hints[index];
            return new PoetrySnapshot
            {
                CorpusVersion = catalog.Version,
                RulesVersion = RulesVersion,
                CardId = Card.Id,
                Players = Players,
                Goal = Goal,
                Phase = Phase,
                Outcome = Outcome,
                FirstPlayer = firstPlayer,
                CurrentPlayer = CurrentPlayer,
                Position = Position,
                Round = Round,
                Winner = Winner,
                RemainingMilliseconds = IsTimed ? TimeLimit - elapsed : -1,
                RoundElapsedMilliseconds = elapsed,
                History = history.ToArray(),
                Rounds = rounds.ToArray(),
                ExposedGroups = exposure,
                HintLines = hintLines,
                CompetitionHintLines = competitionHintLines.ToArray(),
                HintsShown = hintsShown,
                AnswerLineLimit = answerLineLimit
            };
        }

        public static bool TryRestore(PoetryCatalog catalog, PoetrySnapshot snapshot, long now, out PoetrySession session)
        {
            session = null;
            if (catalog == null || snapshot == null || snapshot.CorpusVersion != catalog.Version ||
                snapshot.RulesVersion != RulesVersion || !IsValidConfiguration(snapshot.Players, snapshot.Goal, snapshot.FirstPlayer))
                return false;
            var card = catalog.FindCard(snapshot.CardId);
            if (card == null || snapshot.History == null || snapshot.Rounds == null ||
                snapshot.ExposedGroups == null || snapshot.HintLines == null || snapshot.CompetitionHintLines == null ||
                snapshot.Round < 1 || snapshot.Round > MaximumRounds || snapshot.Rounds.Length > snapshot.Round ||
                snapshot.AnswerLineLimit < 0 || snapshot.AnswerLineLimit > catalog.Lines.Length)
                return false;

            var restored = new PoetrySession(catalog, card, snapshot.Players, snapshot.Goal, 0,
                snapshot.FirstPlayer, snapshot.AnswerLineLimit);
            if (!restored.RestoreExposure(snapshot) || !restored.Replay(snapshot) || !restored.RestoreHints(snapshot))
                return false;

            restored.lastObserved = restored.activeSince = Math.Max(0, now);
            restored.elapsedBeforeActive = snapshot.RoundElapsedMilliseconds;
            if (restored.IsTimed && restored.Phase == PoetryPhase.Active)
                restored.Phase = PoetryPhase.Paused;
            session = restored;
            return true;
        }

        private bool RestoreExposure(PoetrySnapshot snapshot)
        {
            if (Goal != PoetryGoal.Learning && snapshot.ExposedGroups.Length != 0)
                return false;
            foreach (var group in snapshot.ExposedGroups)
                if (!catalog.HasGroup(group, answerLineLimit) || !exposedGroups.Add(group))
                    return false;
            return true;
        }

        private bool Replay(PoetrySnapshot snapshot)
        {
            if (!IsDuoMatch && (snapshot.Round != 1 || snapshot.Rounds.Length != 0))
                return false;

            var elapsed = snapshot.RoundElapsedMilliseconds;
            if (elapsed < 0)
                return false;
            if (IsTimed &&
                (elapsed > TimeLimit || snapshot.RemainingMilliseconds != TimeLimit - elapsed ||
                 ((snapshot.Phase == PoetryPhase.Active || snapshot.Phase == PoetryPhase.Paused) && elapsed == TimeLimit)))
                return false;
            if (!IsTimed && snapshot.RemainingMilliseconds != -1)
                return false;

            // Rebuild positions, duplicate groups and scores from accepted answers and round endings.
            var historyIndex = 0;
            var soloElapsed = 0L;
            for (var round = 1; round <= snapshot.Round; round++)
            {
                if (round > 1 && !NextRound(0))
                    return false;
                while (historyIndex < snapshot.History.Length && snapshot.History[historyIndex].Round == round)
                {
                    var entry = snapshot.History[historyIndex++];
                    if (Players == PoetryPlayers.Solo && (entry.ElapsedMilliseconds < soloElapsed || entry.ElapsedMilliseconds > elapsed))
                        return false;
                    soloElapsed = entry.ElapsedMilliseconds;
                    if (!ReplayEntry(entry))
                        return false;
                }
                if (round <= snapshot.Rounds.Length &&
                    !ReplayRound(snapshot.Rounds[round - 1], round == snapshot.Round ? elapsed : (long?)null))
                    return false;
            }

            return historyIndex == snapshot.History.Length &&
                rounds.Count == snapshot.Rounds.Length &&
                ApplySnapshotPhase(snapshot, elapsed) &&
                Round == snapshot.Round && Phase == snapshot.Phase && Outcome == snapshot.Outcome &&
                CurrentPlayer == snapshot.CurrentPlayer && Position == snapshot.Position && Winner == snapshot.Winner;
        }

        private bool ReplayEntry(PoetryEntry entry)
        {
            if (Phase == PoetryPhase.Handoff)
                Ready(0);
            if (Phase != PoetryPhase.Active || entry.Player != CurrentPlayer ||
                entry.ElapsedMilliseconds < 0 || (IsTimed && entry.ElapsedMilliseconds >= TimeLimit) ||
                !catalog.HasLine(entry.LineId, answerLineLimit) || entry.Input == null ||
                (entry.Assisted && (Goal != PoetryGoal.Learning || entry.Companion ||
                 !exposedGroups.Contains(catalog.Lines[entry.LineId].Group))) ||
                (entry.Companion && (Goal != PoetryGoal.Learning || Players != PoetryPlayers.Solo)))
                return false;

            var key = PoetryCatalog.Normalize(entry.Input);
            if (ComputerTurn && !IsComputerAnswer(key))
                return false;
            var answer = catalog.Judge(Card, Position, key, answerLineLimit);
            if (answer.Status != PoetryAnswerStatus.Accepted || answer.LineId != entry.LineId ||
                usedGroups.Contains(catalog.Lines[entry.LineId].Group))
                return false;
            Accept(entry.LineId, entry.Input, entry.ElapsedMilliseconds, entry.Assisted, entry.Companion);
            return true;
        }

        private bool IsComputerAnswer(string key)
        {
            var candidate = FindComputerLine();
            return candidate >= 0 && key == PoetryCatalog.Normalize(Suggestion(candidate));
        }

        private bool ReplayRound(PoetryRoundResult result, long? currentElapsed)
        {
            if (result.Round != Round)
                return false;
            if (rounds.Count < Round)
            {
                if (result.Outcome != PoetryOutcome.Timeout && result.Outcome != PoetryOutcome.Passed)
                    return false;
                if (Phase == PoetryPhase.Handoff)
                    Ready(0);
                if (Phase != PoetryPhase.Active || result.Winner != 1 - CurrentPlayer)
                    return false;
                var elapsed = currentElapsed ?? (result.Outcome == PoetryOutcome.Timeout ? TimeLimit : 0);
                if (result.Outcome == PoetryOutcome.Timeout ? !IsTimed || elapsed != TimeLimit : IsTimed && elapsed >= TimeLimit)
                    return false;
                FinishRound(result.Outcome, result.Winner, elapsed);
            }
            var actual = rounds[Round - 1];
            return actual.Round == result.Round && actual.Winner == result.Winner && actual.Outcome == result.Outcome &&
                (!currentElapsed.HasValue || elapsedBeforeActive == currentElapsed.Value);
        }

        private bool ApplySnapshotPhase(PoetrySnapshot snapshot, long elapsed)
        {
            if (Phase == PoetryPhase.Finished || Phase == PoetryPhase.RoundOver)
            {
                if (elapsedBeforeActive != elapsed)
                    return false;
                if (Phase == PoetryPhase.RoundOver && snapshot.Phase == PoetryPhase.Finished && snapshot.Outcome == PoetryOutcome.Abandoned)
                    Finish(PoetryOutcome.Abandoned, elapsed);
                return true;
            }

            switch (snapshot.Phase)
            {
                case PoetryPhase.Handoff:
                    return Phase == PoetryPhase.Handoff && elapsed == 0;
                case PoetryPhase.Active:
                case PoetryPhase.Paused:
                    if (Phase == PoetryPhase.Handoff)
                        Ready(0);
                    Phase = snapshot.Phase;
                    return true;
                case PoetryPhase.Finished:
                    if (snapshot.Outcome == PoetryOutcome.Timeout && IsTimed &&
                        Players == PoetryPlayers.Solo && elapsed == TimeLimit)
                    {
                        Finish(PoetryOutcome.Timeout, elapsed);
                        return true;
                    }
                    if (snapshot.Outcome == PoetryOutcome.Passed && IsComputerMatch &&
                        (!ComputerTurn || !CanContinue(Position)))
                    {
                        FinishPass(elapsed);
                        return true;
                    }
                    if ((snapshot.Outcome == PoetryOutcome.Ended && Goal == PoetryGoal.Learning) ||
                        (snapshot.Outcome == PoetryOutcome.Abandoned && (!IsTimed || elapsed < TimeLimit)))
                    {
                        Finish(snapshot.Outcome, elapsed);
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private bool RestoreHints(PoetrySnapshot snapshot)
        {
            if (!RestoreCompetitionHints(snapshot.CompetitionHintLines))
                return false;
            if (!snapshot.HintsShown)
                return snapshot.HintLines.Length == 0;
            if (Phase != PoetryPhase.Active && Phase != PoetryPhase.Paused)
                return false;
            if (Goal == PoetryGoal.Competition)
                return RestoreCurrentCompetitionHint(snapshot.HintLines);
            if (Goal != PoetryGoal.Learning || snapshot.HintLines.Length > 3)
                return false;
            var hintsOnTurn = new HashSet<int>();
            foreach (var lineId in snapshot.HintLines)
            {
                if (!catalog.HasLine(lineId, answerLineLimit) || !catalog.Lines[lineId].Common ||
                    Array.IndexOf(Card.AtPosition(Position), lineId) < 0)
                    return false;
                var group = catalog.Lines[lineId].Group;
                if (usedGroups.Contains(group) || !exposedGroups.Contains(group) || !hintsOnTurn.Add(group))
                    return false;
            }
            hintsShown = true;
            hints = Array.AsReadOnly((int[])snapshot.HintLines.Clone());
            return HasContextForAll(hints);
        }

        private bool RestoreCompetitionHints(int[] charged)
        {
            if (Goal != PoetryGoal.Competition)
                return charged.Length == 0;
            if (charged.Length > CompetitionHintLimit)
                return false;
            foreach (var lineId in charged)
            {
                if (!catalog.HasLine(lineId, answerLineLimit) || !catalog.Lines[lineId].Common ||
                    HasChargedHint(catalog.Lines[lineId].Group) ||
                    !Array.Exists(Card.Candidates, candidates => Array.IndexOf(candidates, lineId) >= 0) ||
                    catalog.ClueLines(lineId, competitionHintLines, answerLineLimit).Count == 0)
                    return false;
                competitionHintLines.Add(lineId);
            }
            return true;
        }

        private bool RestoreCurrentCompetitionHint(int[] cached)
        {
            if (cached.Length != 1 || competitionHintLines.Count == 0 ||
                cached[0] != competitionHintLines[competitionHintLines.Count - 1] ||
                (IsComputerMatch && CurrentPlayer == 1) ||
                Array.IndexOf(Card.AtPosition(Position), cached[0]) < 0 || usedGroups.Contains(catalog.Lines[cached[0]].Group))
                return false;
            hintsShown = true;
            hints = Array.AsReadOnly((int[])cached.Clone());
            return true;
        }
    }
}
