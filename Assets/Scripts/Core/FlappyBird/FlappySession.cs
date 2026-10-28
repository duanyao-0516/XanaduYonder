using System;
namespace XanaduYonder.Core.FlappyBird
{
    public enum FlappyPhase { Ready, Running, Dying, Result, Paused, Countdown }
    public sealed class FlappySession
    {
        private double accumulated, countdown, sinceDeath, sinceResult;
        private bool pendingPress;
        public FlappyGame Game { get; private set; }
        public FlappyPhase Phase { get; private set; }
        public int CountdownNumber { get { return Phase == FlappyPhase.Countdown ? Math.Max(1, (int)Math.Ceiling(countdown - FlappyRules.Epsilon)) : 0; } }
        public bool CanRestart { get { return Phase == FlappyPhase.Result && sinceResult + FlappyRules.Epsilon >= FlappyRules.RestartGuardSeconds; } }
        public FlappySession(uint seed) { Game = new FlappyGame(seed); Phase = FlappyPhase.Ready; }
        public void Press()
        {
            if (Phase == FlappyPhase.Ready) Phase = FlappyPhase.Running;
            if (Phase == FlappyPhase.Running) pendingPress = true;
        }
        public void ClearInput() { pendingPress = false; }
        public void Suspend()
        {
            ClearInput(); accumulated = 0; countdown = 0;
            if (Phase == FlappyPhase.Running || Phase == FlappyPhase.Countdown) Phase = FlappyPhase.Paused;
            else if (Phase == FlappyPhase.Dying) EnterResult();
        }
        public void Resume()
        {
            ClearInput(); accumulated = 0;
            if (Phase == FlappyPhase.Paused) { countdown = 3; Phase = FlappyPhase.Countdown; }
        }
        public void Restart(uint seed)
        {
            if (Phase != FlappyPhase.Ready && Phase != FlappyPhase.Paused && !CanRestart) return;
            Game = new FlappyGame(seed); Phase = FlappyPhase.Ready;
            accumulated = countdown = sinceDeath = sinceResult = 0; ClearInput();
        }
        private void EnterResult() { Phase = FlappyPhase.Result; sinceResult = 0; }
        public FlappyStepResult Advance(double elapsed)
        {
            var result = new FlappyStepResult();
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0 || elapsed > .1 + FlappyRules.Epsilon)
            { Suspend(); return result; }
            if (Phase == FlappyPhase.Result) { sinceResult += elapsed; return result; }
            if (Phase == FlappyPhase.Dying) {
                sinceDeath += elapsed;
                if (sinceDeath + FlappyRules.Epsilon >= FlappyRules.DyingSeconds) EnterResult();
                return result;
            }
            if (Phase == FlappyPhase.Countdown) {
                countdown -= elapsed;
                if (countdown > FlappyRules.Epsilon) return result;
                elapsed = Math.Max(0, -countdown); countdown = 0; Phase = FlappyPhase.Running;
            }
            if (Phase != FlappyPhase.Running) return result;
            accumulated += elapsed;
            while (accumulated + FlappyRules.Epsilon >= FlappyRules.FixedStep) {
                accumulated = Math.Max(0, accumulated - FlappyRules.FixedStep);
                double beforeStep = Game.ElapsedTime;
                var step = Game.Step(pendingPress); ClearInput();
                result.Flapped |= step.Flapped; result.Points += step.Points; result.Died |= step.Died;
                if (step.Died) { Phase = FlappyPhase.Dying; sinceDeath = accumulated + FlappyRules.FixedStep - (Game.ElapsedTime - beforeStep); accumulated = 0; break; }
            }
            return result;
        }
    }
}
