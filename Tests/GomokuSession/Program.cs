using System;
using XanaduYonder.Core.Gomoku;
using XanaduYonder.Runtime.Gomoku;
internal static class Program
{
    private static int passed;
    private static void Main()
    {
        Check("preview moves without committing until confirmation", () => {
            var s = Local(); Assert(s.TrySelect(0)); Eq(s.Game.MoveCount, 0); Assert(s.TrySelect(1)); Eq(s.SelectedIndex.Value, 1);
            Assert(s.ConfirmSelection()); Eq(s.Game.MoveCount, 1); Assert(!s.SelectedIndex.HasValue); Assert(!s.ConfirmSelection());
            Assert(!s.TrySelect(1)); Assert(!s.TrySelect(-1)); Assert(!s.TrySelect(225)); Assert(s.TrySelect(2)); s.ClearSelection(); Assert(!s.SelectedIndex.HasValue);
        });
        Check("human cannot preview or commit AI turn", () => {
            var s = Human(GomokuStone.White); Assert(s.NeedsAi && !s.CanSelect); Assert(!s.TrySelect(0)); Assert(!s.ConfirmSelection());
            var r = s.BeginAi(); Assert(r != null && s.AiPending); Assert(s.BeginAi() == null); Assert(s.AcceptAi(r, 112)); Assert(s.CanSelect && !s.NeedsAi); Assert(!s.AcceptAi(r, 113));
        });
        Check("pause cancels preview and an AI result even after resume", () => {
            var s = Human(); Play(s, 0); var r = s.BeginAi(); s.Pause(); Assert(s.Paused && !s.AiPending && !s.CanSelect && !s.NeedsAi); Assert(!s.AcceptAi(r, 1));
            s.Resume(); Assert(s.NeedsAi); var next = s.BeginAi(); Assert(next.Generation > r.Generation); Assert(!s.AcceptAi(r, 1)); Assert(s.AcceptAi(next, 1));
            Assert(s.TrySelect(2)); s.Pause(); Assert(!s.SelectedIndex.HasValue); Assert(!s.ConfirmSelection());
        });
        Check("background and blocker retain pause until explicit resume", () => {
            var s = Human(GomokuStone.White); var r = s.BeginAi(); s.SetForeground(false); s.Resume(); Assert(s.Paused && !s.AcceptAi(r, 1));
            s.SetForeground(true); Assert(s.Paused); s.Resume(); var next = s.BeginAi(); s.SetBlocked(true); s.Resume(); Assert(s.Paused && !s.AcceptAi(next, 1));
            s.SetBlocked(false); Assert(s.Paused); s.Resume(); Assert(s.NeedsAi);
        });
        Check("AI request binds exact owner revision and is accepted once", () => {
            var first = Human(GomokuStone.White); var request = first.BeginAi(); var other = new GomokuSession(first.Export()); other.Resume(); other.BeginAi();
            Assert(!other.AcceptAi(request, 112)); Assert(!first.AcceptAi(request, -1)); Assert(!first.AcceptAi(request, 225)); Assert(first.AcceptAi(request, 112)); Assert(!first.AcceptAi(request, 113));
        });
        Check("cancelled requests cannot displace newer requests", () => {
            var s = Human(); Play(s, 0); var old = s.BeginAi(); s.CancelAi(); Assert(!s.AiPending); var next = s.BeginAi();
            Assert(!s.AcceptAi(old, 1)); Assert(!s.AcceptAi(next, 0)); Assert(s.AcceptAi(next, 1)); Eq(s.Game.MoveCount, 2);
        });
        Check("local undo removes one ply and clears preview", () => {
            var s = Local(); Assert(!s.CanUndo && !s.Undo()); Play(s, 0); Play(s, 1); s.TrySelect(2); Assert(s.CanUndo && s.Undo());
            Eq(s.Game.MoveCount, 1); Assert(!s.SelectedIndex.HasValue); Eq((int)s.Game.ToPlay, (int)GomokuStone.White);
        });
        Check("human undo while AI thinks invalidates request", () => {
            var s = Human(); Play(s, 0); var request = s.BeginAi(); Assert(s.Undo()); Eq(s.Game.MoveCount, 0); Assert(!s.AcceptAi(request, 1)); Assert(s.CanSelect);
        });
        Check("human undo after AI response restores before human move", () => {
            var s = Human(); Play(s, 0); Assert(s.AcceptAi(s.BeginAi(), 1)); Assert(s.Undo()); Eq(s.Game.MoveCount, 0);
            var w = Human(GomokuStone.White); Assert(w.AcceptAi(w.BeginAi(), 112)); Assert(!w.CanUndo && !w.Undo());
            Play(w, 113); Assert(w.Undo()); Eq(w.Game.MoveCount, 1); Play(w, 114); Assert(w.AcceptAi(w.BeginAi(), 115)); Assert(w.Undo()); Eq(w.Game.MoveCount, 1); Eq(w.Game.LastMove, 112);
        });
        Check("restore is paused and owns data game and request snapshots", () => {
            var s = Human(); Play(s, 0); var data = s.Export(); var restored = new GomokuSession(data); data.Moves[0] = 9;
            Assert(restored.Paused && !restored.SelectedIndex.HasValue && !restored.AiPending); Eq(restored.Game.LastMove, 0);
            var game = restored.Game; game.TryPlay(1); Eq(restored.Game.MoveCount, 1); restored.Resume(); var r = restored.BeginAi(); r.Game.TryPlay(2); Eq(r.Game.MoveCount, 1); Eq(restored.Game.MoveCount, 1);
            var copy = restored.Export(); copy.Moves[0] = 10; Eq(restored.Export().Moves[0], 0); Assert(restored.AcceptAi(r, 1));
        });
        Check("terminal sessions cannot select undo or request AI", () => {
            var s = Local(); foreach (int cell in new[] {0,15,1,16,2,17,3,18,4}) Play(s, cell);
            Assert(s.Game.State == GomokuState.BlackWon); Assert(!s.CanSelect && !s.CanUndo && !s.NeedsAi); Assert(!s.TrySelect(5) && !s.Undo() && s.BeginAi() == null);
            var restored = new GomokuSession(s.Export()); restored.Resume(); Assert(!restored.CanSelect && !restored.CanUndo);
        });
        Check("invalid session options and histories are rejected", () => {
            Throws(() => GomokuSession.Create((GomokuMode)9, GomokuDifficulty.Normal, GomokuStone.Black, 1));
            Throws(() => GomokuSession.Create(GomokuMode.Local, (GomokuDifficulty)9, GomokuStone.Black, 1));
            Throws(() => GomokuSession.Create(GomokuMode.Local, GomokuDifficulty.Normal, GomokuStone.Empty, 1));
            Throws(() => GomokuSession.Create(GomokuMode.Local, GomokuDifficulty.Normal, GomokuStone.Black, 3));
            var d = Local().Export(); d.SessionId = Guid.Empty.ToString("D"); Throws(() => new GomokuSession(d));
            d = Local().Export(); d.Moves = new[] {0,0}; Throws(() => new GomokuSession(d));
        });
        Console.WriteLine("Gomoku session: " + passed + " passed");
    }
    private static GomokuSession Local() { return GomokuSession.Create(GomokuMode.Local, GomokuDifficulty.Normal, GomokuStone.Black, 2); }
    private static GomokuSession Human(GomokuStone stone = GomokuStone.Black) { return GomokuSession.Create(GomokuMode.HumanVsAi, GomokuDifficulty.Normal, stone, 1); }
    private static void Play(GomokuSession s, int index) { Assert(s.TrySelect(index)); Assert(s.ConfirmSelection()); }
    private static void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected ArgumentException"); }
    private static void Eq(int actual, int expected) { Assert(actual == expected, actual + " != " + expected); }
    private static void Assert(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
    private static void Check(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
}
