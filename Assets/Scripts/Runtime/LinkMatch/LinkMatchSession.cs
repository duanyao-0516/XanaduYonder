using System;
using XanaduYonder.Core.LinkMatch;

namespace XanaduYonder.Runtime.LinkMatch
{
    /// <summary>Effective play clock and input gates. The page supplies foreground frame deltas, never wall-clock catch-up.</summary>
    public sealed class LinkMatchSession
    {
        public const double MaximumElapsedSeconds = 315576000;
        private uint randomState;
        private long activityOrder;
        private bool automaticShuffle;
        public string AttemptId { get; private set; }
        public LinkMatchGame Game { get; private set; }
        public LinkMatchMode Mode { get; private set; }
        public LinkMatchOutcome Outcome { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public double RemainingSeconds { get { return Mode == LinkMatchMode.Challenge ? Math.Max(0, Game.Level.ChallengeSeconds - ElapsedSeconds) : 0; } }
        public int HintsUsed { get; private set; }
        public int ShufflesUsed { get; private set; }
        public int HintsRemaining { get { return Mode == LinkMatchMode.Challenge ? 3 - HintsUsed : int.MaxValue; } }
        public int ShufflesRemaining { get { return Mode == LinkMatchMode.Challenge ? 2 - ShufflesUsed : int.MaxValue; } }
        public bool Visible { get; private set; }
        public bool Foreground { get; private set; }
        public bool Blocked { get; private set; }
        public bool ResultRecorded { get; private set; }
        public bool IsNewBest { get; private set; }
        public bool IsTerminal { get { return Outcome != LinkMatchOutcome.Playing; } }
        public bool CanInteract { get { return Visible && Foreground && !Blocked && !automaticShuffle && !IsTerminal; } }
        public LinkMatchSession(LinkMatchLevel level, LinkMatchMode mode, uint seed)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (!LinkMatchSessionData.ValidMode(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            randomState = seed == 0 ? 0x9e3779b9u : seed;
            Game = new LinkMatchGame(level, LinkMatchGenerator.Generate(level, NextSeed()).Tiles);
            Mode = mode; AttemptId = Guid.NewGuid().ToString("D"); Foreground = true;
        }
        public LinkMatchSession(LinkMatchSessionData saved)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            var copy = saved.Copy(); if (!copy.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(saved));
            Game = new LinkMatchGame(LinkMatchLevel.Get(copy.LevelId), copy.Tiles); AttemptId = copy.AttemptId; Mode = copy.Mode; Outcome = copy.Outcome;
            ElapsedSeconds = copy.ElapsedSeconds; HintsUsed = copy.HintsUsed; ShufflesUsed = copy.ShufflesUsed; randomState = copy.RandomState;
            ResultRecorded = copy.ResultRecorded; activityOrder = copy.ActivityOrder; Foreground = true;
        }
        public void Enter() { Visible = true; }
        public void Leave() { Visible = false; }
        public void SetForeground(bool foreground) { Foreground = foreground; if (!foreground) Blocked = true; }
        public void SetBlocked(bool blocked) { Blocked = blocked; }
        public void Tick(double seconds)
        {
            if (!LinkMatchSessionData.Finite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!CanInteract) return;
            double maximum = Mode == LinkMatchMode.Challenge ? Game.Level.ChallengeSeconds : MaximumElapsedSeconds;
            ElapsedSeconds = Math.Min(maximum, ElapsedSeconds + Math.Min(seconds, maximum));
            if (Mode == LinkMatchMode.Challenge && RemainingSeconds == 0) Outcome = LinkMatchOutcome.TimedOut;
        }
        public bool TryMatch(int first, int second, out LinkMatchPath path)
        {
            path = null; if (!CanInteract || !Game.TryMatch(first, second, out path)) return false;
            if (Game.Won) Outcome = LinkMatchOutcome.Won;
            return true;
        }
        public LinkMatchPair TryHint()
        {
            if (!CanInteract || HintsRemaining == 0) return null;
            var pair = Game.FindMatch(); if (pair != null) HintsUsed = Increment(HintsUsed); return pair;
        }
        public bool TryShuffle()
        {
            if (!CanInteract || ShufflesRemaining == 0) return false;
            Game.Shuffle(NextSeed()); ShufflesUsed = Increment(ShufflesUsed); return true;
        }
        /// <summary>May run while the board is concealed/preparing. Caller excludes synchronous rescue work from its next time delta.</summary>
        public bool EnsureMove()
        {
            if (IsTerminal || Game.Won || Game.FindMatch() != null) return false;
            automaticShuffle = true;
            try { Game.Shuffle(NextSeed()); return true; }
            finally { automaticShuffle = false; }
        }
        public LinkMatchSessionData Export()
        {
            return new LinkMatchSessionData { AttemptId = AttemptId, Mode = Mode, LevelId = Game.Level.Id, Columns = Game.Level.Columns, Rows = Game.Level.Rows, Tiles = Game.GetTiles(), ElapsedSeconds = ElapsedSeconds, RemainingSeconds = RemainingSeconds, HintsUsed = HintsUsed, ShufflesUsed = ShufflesUsed, RandomState = randomState, Outcome = Outcome, ResultRecorded = ResultRecorded, ActivityOrder = activityOrder };
        }
        internal void MarkResultRecorded(bool isNewBest) { ResultRecorded = true; IsNewBest = isNewBest; }
        internal void SetActivityOrder(long order) { activityOrder = order; }
        internal static int Increment(int value) { return value == int.MaxValue ? value : value + 1; }
        private uint NextSeed() { randomState ^= randomState << 13; randomState ^= randomState >> 17; randomState ^= randomState << 5; return randomState; }
    }
}
