using System;
using XanaduYonder.Core.Tetris;
namespace XanaduYonder.Runtime.Tetris
{
    public enum TetrisSessionPhase { Ready, Running, Paused, Countdown, Ended }
    public sealed class TetrisSession
    {
        private readonly TetrisGame game;
        private double accumulator, countdown;
        public TetrisInput Input { get; } = new TetrisInput();
        public TetrisSnapshot Snapshot => game.GetSnapshot();
        public int GhostY => game.GhostY;
        public TetrisSessionPhase Phase { get; private set; }
        public bool HasStarted { get; private set; }
        public long Revision { get; private set; }
        public int CountdownNumber => Math.Max(1, (int)Math.Ceiling(countdown - 1e-9));
        public event Action<TetrisStepResult> Stepped;
        public TetrisSession(TetrisGame game, bool started)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            HasStarted = started;
            Phase = game.GameOver ? TetrisSessionPhase.Ended : started ? TetrisSessionPhase.Paused : TetrisSessionPhase.Ready;
        }
        public void StartOrResume()
        {
            if (Phase != TetrisSessionPhase.Ready && Phase != TetrisSessionPhase.Paused) return;
            Input.Cancel(); accumulator = 0;
            if (HasStarted) { countdown = 3; Phase = TetrisSessionPhase.Countdown; }
            else { HasStarted = true; Phase = TetrisSessionPhase.Running; Revision++; }
        }
        public void Pause()
        {
            Input.Cancel(); accumulator = 0;
            if (Phase == TetrisSessionPhase.Running || Phase == TetrisSessionPhase.Countdown) Phase = TetrisSessionPhase.Paused;
        }
        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (Phase != TetrisSessionPhase.Running && Phase != TetrisSessionPhase.Countdown) return;
            if (seconds > .25) { Pause(); return; }
            if (Phase == TetrisSessionPhase.Countdown)
            {
                countdown -= seconds;
                if (countdown <= 1e-9) { Phase = TetrisSessionPhase.Running; accumulator = 0; Input.Cancel(); }
                return;
            }
            DrainCommands();
            accumulator += seconds;
            while (Phase == TetrisSessionPhase.Running && accumulator + 1e-9 >= TetrisRules.StepSeconds)
            {
                accumulator -= TetrisRules.StepSeconds;
                Input.Tick(game.PieceSerial);
                DrainCommands();
                if (Phase == TetrisSessionPhase.Running) Apply(() => game.Tick(Input.SoftDropHeld));
            }
        }
        private void DrainCommands()
        {
            while (Phase == TetrisSessionPhase.Running && Input.TryTake(out var action))
            {
                if (action.PieceSerial != game.PieceSerial) continue;
                TetrisCommand command;
                switch (action.Action)
                {
                    case TetrisAction.Left: command = TetrisCommand.Left; break;
                    case TetrisAction.Right: command = TetrisCommand.Right; break;
                    case TetrisAction.Clockwise: command = TetrisCommand.Clockwise; break;
                    case TetrisAction.CounterClockwise: command = TetrisCommand.CounterClockwise; break;
                    case TetrisAction.Hold: command = TetrisCommand.Hold; break;
                    case TetrisAction.HardDrop: command = TetrisCommand.HardDrop; break;
                    default: continue;
                }
                Apply(() => game.Execute(command));
            }
        }
        private void Apply(Func<TetrisStepResult> step)
        {
            long serial = game.PieceSerial;
            var result = step();
            if (result.Changed) Revision++;
            if (serial != game.PieceSerial) Input.PieceChanged();
            if (result.GameOver || game.GameOver) { Phase = TetrisSessionPhase.Ended; Input.Cancel(); accumulator = 0; }
            if (result.Changed) Stepped?.Invoke(result);
        }
    }
}
