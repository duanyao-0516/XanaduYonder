using System;
using System.Diagnostics;
using XanaduYonder.Core;
using XanaduYonder.Core.Minesweeper;

namespace XanaduYonder.Runtime.Minesweeper
{
    /// <summary>Owns active foreground time and all gates independent of Unity and wall-clock dates.</summary>
    public sealed class MinesweeperSession
    {
        private readonly Func<long> clock;
        private long elapsed, anchor;
        private bool resultRecorded;
        public string SessionId { get; private set; }
        public MinesweeperGame Game { get; private set; }
        public MinesweeperInputMode InputMode { get; private set; }
        public bool Paused { get; private set; }
        public bool Visible { get; private set; }
        public bool Foreground { get; private set; }
        public bool Blocked { get; private set; }
        public bool Overview { get; private set; }
        public bool Fullscreen { get; private set; }
        public double ZoomScale { get; private set; }
        public double CenterColumn { get; private set; }
        public double CenterRow { get; private set; }
        public bool IsNewBest { get; private set; }
        public bool IsTerminal { get { return Game.State == MinesweeperState.Won || Game.State == MinesweeperState.Lost; } }
        public bool CanView { get { return Visible && Foreground && !Paused && !Blocked; } }
        public bool CanInteract { get { return CanView && !Overview && !IsTerminal; } }
        private bool Running { get { return Game.State == MinesweeperState.Active && CanView; } }
        public long ElapsedMilliseconds { get { return Running ? SaturatingAdd(elapsed, Delta(clock())) : elapsed; } }
        public MinesweeperSession(int difficulty, IRandomSource random, Func<long> monotonicMilliseconds = null)
        {
            clock = monotonicMilliseconds ?? MonotonicMilliseconds; anchor = clock();
            SessionId = Guid.NewGuid().ToString("D"); Game = new MinesweeperGame(difficulty, random);
            Visible = Foreground = true; ZoomScale = 1; CenterColumn = Game.Width / 2.0; CenterRow = Game.Height / 2.0;
        }
        public MinesweeperSession(MinesweeperSessionData data, IRandomSource random, Func<long> monotonicMilliseconds = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!data.TryValidate(out var reason, true)) throw new ArgumentException(reason, nameof(data));
            clock = monotonicMilliseconds ?? MonotonicMilliseconds; anchor = clock();
            SessionId = data.SessionId; Game = new MinesweeperGame(data.Board, random); elapsed = data.ElapsedMilliseconds;
            InputMode = data.InputMode; Fullscreen = data.Fullscreen; ZoomScale = data.ZoomScale; CenterColumn = data.CenterColumn; CenterRow = data.CenterRow;
            resultRecorded = data.ResultRecorded; Visible = Foreground = true; Paused = Game.State == MinesweeperState.Active;
        }
        public MinesweeperMoveResult Execute(string sessionId, int index, bool toggleFlag = false)
        {
            if (!CanInteract || !string.Equals(SessionId, sessionId, StringComparison.Ordinal))
                return new MinesweeperMoveResult(Game.State, MinesweeperMoveReason.Blocked, null);
            Settle();
            var result = toggleFlag || InputMode == MinesweeperInputMode.Flag ? Game.ToggleFlag(index) : Game.Reveal(index);
            anchor = clock();
            return result;
        }
        public void Pause()
        { if (IsTerminal) return; Settle(); Paused = true; Overview = false; ResetAnchor(); }
        public void Resume()
        { if (!Paused || IsTerminal || !Visible || !Foreground || Blocked) return; Paused = false; ResetAnchor(); }
        public void Enter()
        { if (Visible) return; Visible = true; if (Game.State == MinesweeperState.Active) Paused = true; ResetAnchor(); }
        public void Leave()
        { Settle(); Visible = false; Overview = false; Blocked = false; if (Game.State == MinesweeperState.Active) Paused = true; ResetAnchor(); }
        public void SetForeground(bool foreground)
        {
            if (Foreground == foreground) return;
            Settle(); Foreground = foreground;
            if (!foreground) { Overview = false; Blocked = false; if (Game.State == MinesweeperState.Active) Paused = true; }
            ResetAnchor();
        }
        public void SetBlocked(bool blocked)
        {
            if (Blocked == blocked) return;
            Settle(); Blocked = blocked;
            if (blocked) { Overview = false; if (Game.State == MinesweeperState.Active) Paused = true; }
            ResetAnchor();
        }
        public void SetOverview(bool overview)
        { if (overview && !CanView) return; Settle(); Overview = overview; }
        public void SetInputMode(MinesweeperInputMode mode)
        {
            if (mode != MinesweeperInputMode.Reveal && mode != MinesweeperInputMode.Flag) throw new ArgumentOutOfRangeException(nameof(mode));
            Settle(); InputMode = mode;
        }
        public void SetView(bool fullscreen, double zoomScale, double centerColumn, double centerRow)
        {
            if (!MinesweeperSessionData.Finite(zoomScale) || zoomScale < .5 || zoomScale > 2 ||
                !MinesweeperSessionData.Finite(centerColumn) || centerColumn < 0 || centerColumn > Game.Width ||
                !MinesweeperSessionData.Finite(centerRow) || centerRow < 0 || centerRow > Game.Height)
                throw new ArgumentOutOfRangeException(nameof(zoomScale), "Invalid normal viewport.");
            Settle(); Fullscreen = fullscreen; ZoomScale = zoomScale; CenterColumn = centerColumn; CenterRow = centerRow;
        }
        public MinesweeperSessionData Export()
        {
            Settle();
            return new MinesweeperSessionData { SessionId = SessionId, Board = Game.GetSnapshot(), ElapsedMilliseconds = elapsed,
                InputMode = InputMode, Fullscreen = Fullscreen, ResultRecorded = resultRecorded, ZoomScale = ZoomScale,
                CenterColumn = CenterColumn, CenterRow = CenterRow };
        }
        internal void MarkResultRecorded(bool newBest) { resultRecorded = true; IsNewBest = newBest; }
        internal static long SaturatingAdd(long value, long addition) { return addition > long.MaxValue - value ? long.MaxValue : value + addition; }
        private static long MonotonicMilliseconds()
        {
            long ticks = Stopwatch.GetTimestamp();
            return (ticks / Stopwatch.Frequency) * 1000 + (ticks % Stopwatch.Frequency) * 1000 / Stopwatch.Frequency;
        }
        private long Delta(long now)
        {
            if (now <= anchor) return 0;
            // An injected clock may span the signed range; never overflow its elapsed duration.
            return anchor < 0 && now > long.MaxValue + anchor ? long.MaxValue : now - anchor;
        }
        private void Settle()
        {
            long now = clock();
            if (Running) elapsed = SaturatingAdd(elapsed, Delta(now));
            if (now > anchor) anchor = now;
        }
        private void ResetAnchor() { anchor = clock(); }
    }
}
