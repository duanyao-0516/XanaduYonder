using System;
using XanaduYonder.Core.SlidingPuzzle;

namespace XanaduYonder.Runtime.SlidingPuzzle
{
    /// <summary>Owns attempt time and lifecycle gates. The first legal move starts the clock.</summary>
    public sealed class SlidingPuzzleSession
    {
        public SlidingPuzzleGame Game { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public bool ResultRecorded { get; private set; }
        public bool IsNewBest { get; private set; }
        public bool Active { get; private set; }
        public bool Foreground { get; private set; }
        public bool Blocked { get; private set; }
        public bool IsTerminal { get { return Game.Won; } }
        public bool CanInteract { get { return Active && Foreground && !Blocked && !IsTerminal && !Game.AtActionLimit; } }

        public SlidingPuzzleSession(SlidingPuzzleGame game)
        { Game = game ?? throw new ArgumentNullException(nameof(game)); Active = Foreground = true; }
        internal SlidingPuzzleSession(SlidingPuzzleSessionData saved)
            : this(SlidingPuzzleGame.Restore(saved.Snapshot))
        { ElapsedSeconds = saved.ElapsedSeconds; ResultRecorded = saved.ResultRecorded; }
        public bool Move(int tile) { return CanInteract && Game.TryMove(tile); }
        public bool Undo() { return CanInteract && Game.Undo(); }
        public void Tick(double seconds)
        {
            if (!SlidingPuzzleSessionData.Finite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (CanInteract && Game.Moves > 0) ElapsedSeconds = Math.Min(SlidingPuzzleSessionData.MaximumSeconds, ElapsedSeconds + seconds);
        }
        public void Enter() { Active = true; }
        public void Leave() { Active = false; Blocked = false; }
        public void SetForeground(bool foreground) { Foreground = foreground; }
        public void SetBlocked(bool blocked) { Blocked = blocked; }
        public void Restart()
        {
            Game = new SlidingPuzzleGame(Game.Size, Game.GetInitial());
            ElapsedSeconds = 0; ResultRecorded = IsNewBest = false;
        }
        internal void MarkResultRecorded(bool newBest) { ResultRecorded = true; IsNewBest = newBest; }
        internal SlidingPuzzleSessionData Export(int levelIndex)
        { return new SlidingPuzzleSessionData { LevelIndex = levelIndex, Snapshot = Game.GetSnapshot(), ElapsedSeconds = ElapsedSeconds, ResultRecorded = ResultRecorded }; }
    }
}
