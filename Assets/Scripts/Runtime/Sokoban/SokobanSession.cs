using System;
using XanaduYonder.Core;
using XanaduYonder.Core.Sokoban;

namespace XanaduYonder.Runtime.Sokoban
{
    public sealed class SokobanSession
    {
        public string AttemptId { get; private set; }
        public SokobanGame Game { get; private set; }
        public bool Visible { get; private set; }
        public bool Foreground { get; private set; }
        public bool Blocked { get; private set; }
        public bool CanInteract { get { return Visible && Foreground && !Blocked && !IsTerminal; } }
        public bool IsTerminal { get { return Game.Won; } }
        public bool ResultRecorded { get; private set; }
        public bool IsNewBest { get; private set; }
        public double Zoom { get; private set; }
        public double CenterX { get; private set; }
        public double CenterY { get; private set; }
        public SokobanSession(SokobanLevel level) { Game = new SokobanGame(level); Visible = Foreground = true; ResetAttempt(); }
        public SokobanSession(SokobanSessionData data, SokobanCatalog catalog)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var copy = data.Copy(); if (!copy.TryReplay(catalog, out var game, out var reason)) throw new ArgumentException(reason, nameof(data));
            Game = game; AttemptId = copy.AttemptId; ResultRecorded = copy.ResultRecorded; Zoom = copy.Zoom; CenterX = copy.CenterX; CenterY = copy.CenterY; Visible = Foreground = true;
        }
        public SokobanMoveResult Move(Direction direction) { return Execute(AttemptId, direction); }
        public SokobanMoveResult Execute(string attemptId, Direction direction)
        { return !CanInteract || attemptId != AttemptId ? new SokobanMoveResult(SokobanMoveReason.Blocked) : Game.Move(direction); }
        public bool Undo() { return CanInteract && Game.Undo(); }
        public void Restart() { Game.Restart(); ResetAttempt(); }
        private void ResetAttempt() { AttemptId = Guid.NewGuid().ToString("D"); ResultRecorded = IsNewBest = false; Zoom = 1; CenterX = CenterY = .5; }
        public void Enter() { Visible = true; }
        public void Leave() { Visible = false; Blocked = false; }
        public void SetForeground(bool foreground) { Foreground = foreground; }
        public void SetBlocked(bool blocked) { Blocked = blocked; }
        public void SetView(double zoom, double centerX, double centerY)
        {
            if (!SokobanSessionData.Finite(zoom) || !SokobanSessionData.Finite(centerX) || !SokobanSessionData.Finite(centerY)) throw new ArgumentException("Viewport must be finite.");
            Zoom = Math.Max(1, Math.Min(2, zoom)); CenterX = Math.Max(0, Math.Min(1, centerX)); CenterY = Math.Max(0, Math.Min(1, centerY)); if (Zoom == 1) CenterX = CenterY = .5;
        }
        public SokobanSessionData Export()
        {
            var snapshot = Game.GetSnapshot(); return new SokobanSessionData { AttemptId = AttemptId, LevelId = Game.Level.LevelId, Actions = snapshot.Actions, Player = snapshot.Player, Boxes = snapshot.Boxes, Moves = snapshot.Moves, Pushes = snapshot.Pushes, Won = snapshot.Won, ResultRecorded = ResultRecorded, Zoom = Zoom, CenterX = CenterX, CenterY = CenterY };
        }
        internal void MarkResultRecorded(bool newBest) { ResultRecorded = true; IsNewBest = newBest; }
    }
}
