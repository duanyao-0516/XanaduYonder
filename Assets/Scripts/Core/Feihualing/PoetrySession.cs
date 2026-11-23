using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Feihualing
{
    public enum PoetryPlayers { Solo, Duo }
    public enum PoetryGoal { Learning = 0, Competition = 1, Speed = 2 }
    public enum PoetryPhase { Active, Handoff, Paused, RoundOver, Finished }
    public enum PoetryOutcome { None, Completed, Timeout, Passed, Exhausted, Ended, Abandoned }

    public enum PoetryAnswerStatus
    {
        Accepted,
        Empty,
        MultipleLines,
        Unknown,
        WrongCharacter,
        WrongPosition,
        WrongPair,
        WrongTheme,
        Duplicate,
        Inactive,
        TimedOut,
        Unavailable
    }

    public readonly struct PoetryAnswer
    {
        internal PoetryAnswer(PoetryAnswerStatus status, int lineId = -1, PoetryEntry? entry = null)
        {
            Status = status;
            LineId = lineId;
            Entry = entry;
        }

        public PoetryAnswerStatus Status { get; }
        public int LineId { get; }
        public PoetryEntry? Entry { get; }
    }

    public struct PoetryEntry
    {
        public int LineId { get; set; }
        public int Round { get; set; }
        public int Player { get; set; }
        public bool Assisted { get; set; }
        public bool Companion { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public string Input { get; set; }
    }

    public struct PoetryRoundResult
    {
        public int Round { get; set; }
        public int Winner { get; set; }
        public PoetryOutcome Outcome { get; set; }
    }

    public sealed partial class PoetrySession
    {
        public const int RulesVersion = 2;
        public const int SoloMilliseconds = 60000;
        public const int DuoMilliseconds = 30000;
        public const int SoloTarget = 5;
        public const int MaximumRounds = 3;
        public const int CompetitionHintLimit = 3;

        private readonly PoetryCatalog catalog;
        private readonly int firstPlayer;
        private readonly int answerLineLimit;
        private readonly List<PoetryEntry> history = new List<PoetryEntry>();
        private readonly List<PoetryRoundResult> rounds = new List<PoetryRoundResult>();
        private readonly HashSet<int> usedGroups = new HashSet<int>();
        private readonly HashSet<int> exposedGroups = new HashSet<int>();
        private readonly List<int> competitionHintLines = new List<int>(CompetitionHintLimit);
        private IReadOnlyList<int> hints = Array.Empty<int>();
        private bool hintsShown;
        private long lastObserved;
        private long activeSince;
        private long elapsedBeforeActive;

        private PoetrySession(PoetryCatalog catalog, PoetryCard card, PoetryPlayers players, PoetryGoal goal, long now,
            int firstPlayer, int answerLineLimit)
        {
            this.catalog = catalog;
            this.firstPlayer = firstPlayer;
            this.answerLineLimit = answerLineLimit;
            Card = card;
            Players = players;
            Goal = goal;
            History = history.AsReadOnly();
            Rounds = rounds.AsReadOnly();
            Round = 1;
            Winner = -1;
            CurrentPlayer = firstPlayer;
            Position = Card.Rule == PoetryRule.Position ? 1 : 0;
            lastObserved = activeSince = Math.Max(now, 0);
            Phase = Players == PoetryPlayers.Duo ? PoetryPhase.Handoff : PoetryPhase.Active;
        }

        public PoetryCard Card { get; }
        public PoetryPlayers Players { get; }
        public PoetryGoal Goal { get; }
        public PoetryPhase Phase { get; private set; }
        public PoetryOutcome Outcome { get; private set; }
        public int CurrentPlayer { get; private set; }
        public int Position { get; private set; }
        public int Round { get; private set; }
        public int Winner { get; private set; }
        public bool ComputerTurn => IsComputerMatch && Phase == PoetryPhase.Active && CurrentPlayer == 1;
        public bool HasCurrentCompetitionHint => CanShowCompetitionHint && hintsShown;
        public int CompetitionHintsRemaining => Goal == PoetryGoal.Competition
            ? CompetitionHintLimit - competitionHintLines.Count : 0;
        public IReadOnlyList<PoetryEntry> History { get; }
        public IReadOnlyList<PoetryRoundResult> Rounds { get; }

        private int TimeLimit => Players == PoetryPlayers.Solo ? SoloMilliseconds : DuoMilliseconds;
        private bool IsTimed => Goal == PoetryGoal.Speed;
        private bool IsComputerMatch => Players == PoetryPlayers.Solo && Goal == PoetryGoal.Competition;
        private bool IsDuoMatch => Players == PoetryPlayers.Duo && Goal != PoetryGoal.Learning;
        private bool CanShowCompetitionHint => Goal == PoetryGoal.Competition && Phase == PoetryPhase.Active && !ComputerTurn;
        private IReadOnlyList<int> HintMaskTargets => Goal == PoetryGoal.Competition ? competitionHintLines : hints;

        public static PoetrySession Start(PoetryCatalog catalog, PoetryCard card, PoetryPlayers players, PoetryGoal goal,
            long now, int firstPlayer = 0)
        {
            if (catalog == null || card == null || catalog.FindCard(card.Id) != card ||
                !IsValidConfiguration(players, goal, firstPlayer))
                throw new ArgumentException("Invalid poetry session configuration.");
            return new PoetrySession(catalog, card, players, goal, now, firstPlayer, 0);
        }

        public int Wins(int player)
        {
            var wins = 0;
            if (player == 0 || player == 1)
                foreach (var result in rounds)
                    if (result.Winner == player)
                        wins++;
            return wins;
        }

        public int Count(int player, bool assisted)
        {
            var count = 0;
            foreach (var entry in history)
                if (!entry.Companion && entry.Player == player && entry.Assisted == assisted)
                    count++;
            return count;
        }

        public long Remaining(long now)
        {
            now = Observe(now);
            return IsTimed ? TimeLimit - Elapsed(now) : -1;
        }

        public bool Tick(long now)
        {
            now = Observe(now);
            if (Phase != PoetryPhase.Active || !IsTimed || Elapsed(now) < TimeLimit)
                return false;
            if (IsDuoMatch)
                FinishRound(PoetryOutcome.Timeout, 1 - CurrentPlayer, TimeLimit);
            else
                Finish(PoetryOutcome.Timeout, TimeLimit);
            return true;
        }

        public bool Ready(long now)
        {
            now = Observe(now);
            if (Phase != PoetryPhase.Handoff)
                return false;
            Activate(now);
            return true;
        }

        public bool Pause(long now)
        {
            now = Observe(now);
            if (Tick(now) || Phase != PoetryPhase.Active)
                return false;
            elapsedBeforeActive = Elapsed(now);
            Phase = PoetryPhase.Paused;
            return true;
        }

        public bool Resume(long now)
        {
            now = Observe(now);
            if (Phase != PoetryPhase.Paused)
                return false;
            Activate(now);
            return true;
        }

        public bool NextRound(long now)
        {
            Observe(now);
            if (Phase != PoetryPhase.RoundOver)
                return false;
            Round++;
            CurrentPlayer = (firstPlayer + Round - 1) % 2;
            Position = Card.Rule == PoetryRule.Position ? 1 : 0;
            usedGroups.Clear();
            elapsedBeforeActive = 0;
            Outcome = PoetryOutcome.None;
            Phase = PoetryPhase.Handoff;
            return true;
        }

        public bool Pass(long now)
        {
            now = Observe(now);
            if (Tick(now) || Phase != PoetryPhase.Active || ComputerTurn ||
                (Goal != PoetryGoal.Competition && (!IsTimed || Players != PoetryPlayers.Duo)))
                return false;
            FinishPass(Elapsed(now));
            return true;
        }

        public bool End(long now)
        {
            now = Observe(now);
            if (Phase == PoetryPhase.Finished || Goal != PoetryGoal.Learning)
                return false;
            Finish(PoetryOutcome.Ended, Elapsed(now));
            return true;
        }

        public bool Abandon(long now)
        {
            now = Observe(now);
            if (Tick(now) || Phase == PoetryPhase.Finished)
                return false;
            Finish(PoetryOutcome.Abandoned, Elapsed(now));
            return true;
        }

        public PoetryAnswer Submit(string input, long now)
        {
            now = Observe(now);
            if (Tick(now))
                return new PoetryAnswer(PoetryAnswerStatus.TimedOut);
            if (Phase != PoetryPhase.Active || ComputerTurn)
                return new PoetryAnswer(PoetryAnswerStatus.Inactive);

            var cleaned = PoetryCatalog.CleanPlayerInput(input);
            var answer = catalog.Judge(Card, Position, PoetryCatalog.Normalize(cleaned), answerLineLimit);
            if (answer.Status != PoetryAnswerStatus.Accepted)
                return answer;
            var group = catalog.Lines[answer.LineId].Group;
            if (usedGroups.Contains(group))
                return new PoetryAnswer(PoetryAnswerStatus.Duplicate, answer.LineId);
            return Accept(answer.LineId, cleaned, Elapsed(now), exposedGroups.Contains(group), false);
        }

        public PoetryAnswer Computer(long now)
        {
            now = Observe(now);
            if (!ComputerTurn)
                return new PoetryAnswer(PoetryAnswerStatus.Unavailable);
            var lineId = FindComputerLine();
            if (lineId < 0)
            {
                FinishPass(Elapsed(now));
                return new PoetryAnswer(PoetryAnswerStatus.Unavailable);
            }
            return GeneratedAnswer(lineId, now, false);
        }

        private int FindComputerLine()
        {
            var best = -1;
            var bestPriority = -1;
            foreach (var lineId in AvailableCandidates(Position))
            {
                var line = catalog.Lines[lineId];
                if (usedGroups.Contains(line.Group))
                    continue;
                var priority = (line.Common ? 2 : 0) + (IsRecentWork(line.WorkIndex) ? 0 : 1);
                if (priority <= bestPriority)
                    continue;
                best = lineId;
                bestPriority = priority;
                if (priority == 3)
                    break;
            }
            return best;
        }

        public PoetryAnswer Companion(long now)
        {
            now = Observe(now);
            if (Goal != PoetryGoal.Learning || Players != PoetryPlayers.Solo || Phase != PoetryPhase.Active)
                return new PoetryAnswer(PoetryAnswerStatus.Unavailable);

            var bestLine = -1;
            var bestScore = -1;
            var nextOnlyGroup = OnlyAvailableGroup(NextPosition());
            foreach (var lineId in AvailableCandidates(Position))
            {
                var line = catalog.Lines[lineId];
                if (!line.Common || usedGroups.Contains(line.Group))
                    continue;
                var score = CompanionPriority(line, nextOnlyGroup);
                if (score <= bestScore)
                    continue;
                bestLine = lineId;
                bestScore = score;
                if (score == 7)
                    break;
            }
            return bestLine < 0 ? new PoetryAnswer(PoetryAnswerStatus.Unavailable) : GeneratedAnswer(bestLine, now, true);
        }

        private PoetryAnswer GeneratedAnswer(int lineId, long now, bool companion)
        {
            var input = Suggestion(lineId);
            var answer = catalog.Judge(Card, Position, PoetryCatalog.Normalize(input), answerLineLimit);
            return Accept(answer.LineId, input.Trim(), Elapsed(now), false, companion);
        }

        public IReadOnlyList<int> Hints(bool refresh = false)
        {
            if (!CanExpose() && !CanShowCompetitionHint)
                return Array.Empty<int>();
            if (hintsShown && !refresh)
                return hints;
            if (Goal == PoetryGoal.Competition)
                return NextCompetitionHint();

            var candidates = new List<int>(3);
            foreach (var lineId in AvailableCandidates(Position))
            {
                var line = catalog.Lines[lineId];
                if (!line.Common || usedGroups.Contains(line.Group) || exposedGroups.Contains(line.Group))
                    continue;
                candidates.Add(lineId);
                if (!HasContextForAll(candidates))
                {
                    candidates.RemoveAt(candidates.Count - 1);
                    continue;
                }
                exposedGroups.Add(line.Group);
                if (candidates.Count == 3)
                    break;
            }
            if (hintsShown && candidates.Count == 0)
                return hints;
            hints = candidates.AsReadOnly();
            hintsShown = true;
            return hints;
        }

        private IReadOnlyList<int> NextCompetitionHint()
        {
            if (CompetitionHintsRemaining == 0)
                return hints;
            foreach (var lineId in AvailableCandidates(Position))
            {
                var line = catalog.Lines[lineId];
                if (!line.Common || usedGroups.Contains(line.Group) || HasChargedHint(line.Group) ||
                    catalog.ClueLines(lineId, competitionHintLines, answerLineLimit).Count == 0)
                    continue;
                competitionHintLines.Add(lineId);
                hints = Array.AsReadOnly(new[] { lineId });
                hintsShown = true;
                break;
            }
            return hints;
        }

        private bool HasChargedHint(int group)
        {
            foreach (var lineId in competitionHintLines)
                if (catalog.Lines[lineId].Group == group)
                    return true;
            return false;
        }

        public IReadOnlyList<int> ClueLines(int targetLineId)
        {
            var currentCompetitionTarget = HasCurrentCompetitionHint && hints[0] == targetLineId;
            return CanExpose() || currentCompetitionTarget
                ? catalog.ClueLines(targetLineId, HintMaskTargets, answerLineLimit) : Array.Empty<int>();
        }

        public string MaskClue(string text, int targetLineId)
        {
            return catalog.MaskClue(text, targetLineId, HintMaskTargets, answerLineLimit);
        }

        private bool HasContextForAll(IReadOnlyList<int> targets)
        {
            foreach (var target in targets)
                if (catalog.ClueLines(target, targets, answerLineLimit).Count == 0)
                    return false;
            return true;
        }

        public string Suggestion(int lineId)
        {
            return catalog.Suggestion(Card, Position, lineId, answerLineLimit);
        }

        public bool ExposeLine(int lineId)
        {
            return CanExpose() && catalog.HasLine(lineId, answerLineLimit) && exposedGroups.Add(catalog.Lines[lineId].Group);
        }

        public bool ExposeWork(int workIndex)
        {
            if (!CanExpose())
                return false;
            var lines = catalog.LinesInWork(workIndex);
            if (lines == null)
                return false;
            var changed = false;
            foreach (var lineId in lines)
                if (catalog.HasLine(lineId, answerLineLimit))
                    changed |= exposedGroups.Add(catalog.Lines[lineId].Group);
            return changed;
        }

        private bool CanExpose()
        {
            return Goal == PoetryGoal.Learning && Phase == PoetryPhase.Active;
        }

        private PoetryAnswer Accept(int lineId, string input, long elapsed, bool assisted, bool companion)
        {
            var entry = new PoetryEntry
            {
                LineId = lineId,
                Round = Round,
                Player = CurrentPlayer,
                Assisted = assisted,
                Companion = companion,
                ElapsedMilliseconds = elapsed,
                Input = input
            };
            history.Add(entry);
            usedGroups.Add(catalog.Lines[lineId].Group);
            Position = NextPosition();
            ResetHints();

            if (Goal == PoetryGoal.Competition)
                AdvanceRelay(elapsed);
            else if (IsTimed && Players == PoetryPlayers.Solo && usedGroups.Count == SoloTarget)
                Finish(PoetryOutcome.Completed, elapsed, 0);
            else if (!CanContinue(Position))
            {
                if (IsDuoMatch)
                    FinishRound(PoetryOutcome.Exhausted, -1, elapsed);
                else
                    Finish(PoetryOutcome.Exhausted, elapsed);
            }
            else if (Players == PoetryPlayers.Duo)
            {
                CurrentPlayer = 1 - CurrentPlayer;
                Handoff();
            }
            return new PoetryAnswer(PoetryAnswerStatus.Accepted, lineId, entry);
        }

        private void AdvanceRelay(long elapsed)
        {
            CurrentPlayer = 1 - CurrentPlayer;
            if (!ComputerTurn && !CanContinue(Position))
                FinishPass(elapsed);
            else if (Players == PoetryPlayers.Duo)
                Handoff();
        }

        private void Handoff()
        {
            elapsedBeforeActive = 0;
            Phase = PoetryPhase.Handoff;
        }

        private void FinishPass(long elapsed)
        {
            var winner = 1 - CurrentPlayer;
            if (IsDuoMatch)
                FinishRound(PoetryOutcome.Passed, winner, elapsed);
            else
                Finish(PoetryOutcome.Passed, elapsed, winner);
        }

        private int NextPosition()
        {
            return Card.Rule == PoetryRule.Position ? Position % Card.Width + 1 : 0;
        }

        private IEnumerable<int> AvailableCandidates(int position)
        {
            foreach (var lineId in Card.AtPosition(position))
                if (catalog.HasLine(lineId, answerLineLimit))
                    yield return lineId;
        }

        private bool CanContinue(int position)
        {
            foreach (var candidate in AvailableCandidates(position))
                if (!usedGroups.Contains(catalog.Lines[candidate].Group))
                    return true;
            return false;
        }

        private int? OnlyAvailableGroup(int position)
        {
            int? onlyGroup = null;
            foreach (var candidate in AvailableCandidates(position))
            {
                var group = catalog.Lines[candidate].Group;
                if (usedGroups.Contains(group))
                    continue;
                if (onlyGroup.HasValue && onlyGroup.Value != group)
                    return null;
                onlyGroup = group;
            }
            return onlyGroup;
        }

        private int CompanionPriority(PoetryLine line, int? nextOnlyGroup)
        {
            var priority = nextOnlyGroup != line.Group ? 4 : 0;
            if (!exposedGroups.Contains(line.Group))
                priority += 1;
            return priority + (IsRecentWork(line.WorkIndex) ? 0 : 2);
        }

        private bool IsRecentWork(int workIndex)
        {
            var recent = Math.Max(0, history.Count - 6);
            for (var index = history.Count - 1; index >= recent; index--)
                if (catalog.Lines[history[index].LineId].WorkIndex == workIndex)
                    return true;
            return false;
        }

        private long Observe(long now)
        {
            lastObserved = Math.Max(lastObserved, now);
            return lastObserved;
        }

        private long Elapsed(long now)
        {
            if (Phase != PoetryPhase.Active)
                return elapsedBeforeActive;
            var delta = now - activeSince;
            if (IsTimed)
                return delta >= TimeLimit - elapsedBeforeActive ? TimeLimit : elapsedBeforeActive + delta;
            return delta >= long.MaxValue - elapsedBeforeActive ? long.MaxValue : elapsedBeforeActive + delta;
        }

        private void Activate(long now)
        {
            activeSince = now;
            Phase = PoetryPhase.Active;
        }

        private void FinishRound(PoetryOutcome outcome, int winner, long elapsed)
        {
            rounds.Add(new PoetryRoundResult { Round = Round, Winner = winner, Outcome = outcome });
            var left = Wins(0);
            var right = Wins(1);
            if (left == 2 || right == 2 || Round == MaximumRounds)
            {
                Finish(outcome, elapsed, left == right ? -1 : left > right ? 0 : 1);
                return;
            }
            Outcome = outcome;
            elapsedBeforeActive = elapsed;
            Phase = PoetryPhase.RoundOver;
            ResetHints();
        }

        private void Finish(PoetryOutcome outcome, long elapsed, int winner = -1)
        {
            elapsedBeforeActive = elapsed;
            Outcome = outcome;
            Winner = winner;
            Phase = PoetryPhase.Finished;
            ResetHints();
        }

        private void ResetHints()
        {
            hints = Array.Empty<int>();
            hintsShown = false;
        }

        private static bool IsValidConfiguration(PoetryPlayers players, PoetryGoal goal, int firstPlayer)
        {
            return (players == PoetryPlayers.Solo || players == PoetryPlayers.Duo) &&
                (goal == PoetryGoal.Learning || goal == PoetryGoal.Competition || goal == PoetryGoal.Speed) &&
                (firstPlayer == 0 || (firstPlayer == 1 && players == PoetryPlayers.Duo));
        }
    }
}
