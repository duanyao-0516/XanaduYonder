using System;
using System.Collections.Generic;
using XanaduYonder.Core.Tetris;

internal static class Program
{
    static int passed, failed;
    static int Main()
    {
        Run("spawn, shapes and defensive copies", ShapesAndCopies);
        Run("fixed gravity and scored soft drop", GravityAndSoftDrop);
        Run("seven bag reproducible sequence", SevenBag);
        Run("single double triple tetris scoring", LineScores);
        Run("crossing level uses prior level", CrossLevel);
        Run("hard drop agrees with ghost", GhostAndHardDrop);
        Run("SRS normal wall and floor kicks", NormalKicks);
        Run("SRS I wall and floor kicks", IKicks);
        Run("O rotation never resets lock", ORotation);
        Run("hold once per falling piece", HoldLimit);
        Run("hold spawn remains legal above visible support", HoldSpawnSupport);
        Run("touchdown keeps full 30 tick adjustment", FirstTouch);
        Run("15 successful lock resets then budget stops", ResetLimit);
        Run("failed move cannot extend lock", FailedMove);
        Run("airborne expiry never locks until ground", AirborneExpiry);
        Run("expired airborne lock cannot be revived by input", ExpiredAirborneInput);
        Run("blocked SRS rotation preserves lock timer", BlockedRotation);
        Run("lower ground never replenishes reset budget", LowerGround);
        Run("clear lines before checking hidden residue", TopoutRescue);
        Run("hidden residue ends after locking", HiddenTopout);
        Run("blocked spawn terminal snapshot validation", SpawnBlocked);
        Run("terminal commands and unknown commands freeze", TerminalAndUnknown);
        Run("score counters saturate safely", Saturation);
        Run("restore preserves future rules and random sequence", RestoreFuture);
        Run("invalid and impossible snapshots are rejected", InvalidSnapshots);
        Run("random command snapshots always restore", RandomSnapshots);
        Run("read-only identity getters allocate nothing", IdentityGetters);
        Console.WriteLine("Tetris tests: " + passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }

    static void ShapesAndCopies()
    {
        var game = new TetrisGame(123u); var s = game.GetSnapshot(); Valid(s);
        Assert(s.Cells.Length == 220 && s.X == 3 && s.Y == 0 && s.Rotation == 0, "spawn geometry");
        Assert(s.NextQueue.Length == 3 && s.PieceSerial == 1 && s.CanHold, "spawn metadata");
        for (int p = 1; p <= 7; p++) for (int r = 0; r < 4; r++) {
            var cells = TetrisPieces.GetCells((TetrisPiece)p, r);
            var unique = new HashSet<int>();
            Assert(cells.Length == 4, "four cells");
            foreach (var c in cells) Assert(c.X >= 0 && c.X < 4 && c.Y >= 0 && c.Y < 4 && unique.Add(c.Y * 4 + c.X), "shape bounds and uniqueness");
            cells[0] = new TetrisCell(99, 99);
            Assert(TetrisPieces.GetCells((TetrisPiece)p, r)[0].X != 99, "shape cannot mutate static data");
        }
        s.Cells[0] = TetrisPiece.Z; s.NextQueue[0] = TetrisPiece.None; s.Bag[0] = TetrisPiece.None;
        Valid(game.GetSnapshot());
        var copy = game.GetSnapshot().Copy(); var restored = new TetrisGame(copy); copy.Cells[100] = TetrisPiece.I;
        Assert(restored.GetSnapshot().Cells[100] == TetrisPiece.None, "constructor copies");
        Throws<ArgumentNullException>(delegate { new TetrisGame((TetrisSnapshot)null); });
        Throws<ArgumentOutOfRangeException>(delegate { TetrisPieces.GetCells((TetrisPiece)88, 0); });
        Throws<ArgumentOutOfRangeException>(delegate { TetrisPieces.GetCells(TetrisPiece.I, 4); });
        Assert(TetrisPieces.GetCells(TetrisPiece.None, 0).Length == 0, "empty shape");
    }

    static void GravityAndSoftDrop()
    {
        var g = Game(TetrisPiece.T, 3, 0, 0); for (int i = 0; i < 59; i++) g.Tick(false);
        Assert(g.GetSnapshot().Y == 0, "not early"); g.Tick(false); Assert(g.GetSnapshot().Y == 1, "60 ticks one row");
        Assert(g.GetSnapshot().ElapsedTicks == 60 && g.GetSnapshot().Score == 0, "natural gravity no score");
        g = Game(TetrisPiece.T, 3, 0, 0); g.Tick(true); g.Tick(true); Assert(g.GetSnapshot().Y == 0, "soft not early");
        var r = g.Tick(true); Assert(g.GetSnapshot().Y == 1 && r.ScoreDelta == 1, "soft scored actual cell");
        g = Game(TetrisPiece.O, 3, 20, 0); for (int i = 0; i < 3; i++) g.Tick(true);
        Assert(g.GetSnapshot().Score == 0, "blocked soft awards nothing");
        Assert(TetrisRules.GravityIntervalTicks(1) == 60 && TetrisRules.GravityIntervalTicks(2) == 51 && TetrisRules.GravityIntervalTicks(11) == 12, "quantized curve");
        Assert(TetrisRules.GravityIntervalTicks(int.MaxValue) == 12, "gravity floor");
    }

    static void SevenBag()
    {
        var a = new TetrisGame(7u); var b = new TetrisGame(7u);
        for (int bag = 0; bag < 8; bag++) {
            var seen = new HashSet<TetrisPiece>();
            for (int i = 0; i < 7; i++) {
                var x = a.GetSnapshot(); var y = b.GetSnapshot();
                Assert(x.ActivePiece == y.ActivePiece && seen.Add(x.ActivePiece), "each seeded bag exactly seven");
                a.Execute(TetrisCommand.HardDrop); b.Execute(TetrisCommand.HardDrop);
                var sa = a.GetSnapshot(); var sb = b.GetSnapshot(); Array.Clear(sa.Cells, 0, 220); Array.Clear(sb.Cells, 0, 220);
                a = new TetrisGame(sa); b = new TetrisGame(sb);
            }
        }
        Valid(new TetrisGame(0u).GetSnapshot());
    }

    static TetrisSnapshot ClearFixture(int count, int lines)
    {
        var s = Fixture(TetrisPiece.I, 2, 18, 1); s.Lines = lines;
        for (int y = 22 - count; y < 22; y++) for (int x = 0; x < 10; x++) if (x != 4) s.Cells[y * 10 + x] = TetrisPiece.J;
        Ground(s); return s;
    }
    static void LineScores()
    {
        int[] scores = { 100, 300, 500, 800 };
        for (int n = 1; n <= 4; n++) {
            var g = new TetrisGame(ClearFixture(n, 0)); var r = g.Execute(TetrisCommand.HardDrop); var s = g.GetSnapshot();
            Assert(r.Locked && r.ClearedLines == n && r.ClearedRows.Length == n, "actual cleared rows");
            Assert(r.ScoreDelta == scores[n - 1] && s.Score == scores[n - 1] && s.Lines == n, "normal line score " + n);
            Assert(!s.GameOver, "line clear continues"); Valid(s);
        }
    }
    static void CrossLevel()
    {
        var g = new TetrisGame(ClearFixture(2, 9)); var r = g.Execute(TetrisCommand.HardDrop);
        Assert(r.ScoreDelta == 300 && g.GetSnapshot().Level == 2 && g.GetSnapshot().Lines == 11, "crossing uses old multiplier");
        g = new TetrisGame(ClearFixture(4, 19)); Assert(g.Execute(TetrisCommand.HardDrop).ScoreDelta == 1600 && g.GetSnapshot().Level == 3, "second multiplier");
    }
    static void GhostAndHardDrop()
    {
        var s = Fixture(TetrisPiece.T, 3, 3, 0); s.Cells[20 * 10 + 4] = TetrisPiece.O;
        var g = new TetrisGame(s); int y = g.GhostY; var cells = TetrisPieces.GetCells(s.ActivePiece, s.Rotation);
        var r = g.Execute(TetrisCommand.HardDrop); var after = g.GetSnapshot();
        Assert(r.Locked && r.ScoreDelta == 2 * (y - s.Y), "distance score");
        foreach (var c in cells) Assert(after.Cells[(y + c.Y) * 10 + s.X + c.X] == TetrisPiece.T, "ghost locks exact cells");
    }
    static void NormalKicks()
    {
        var g = Game(TetrisPiece.T, -1, 5, 1); Assert(g.Execute(TetrisCommand.CounterClockwise).Changed, "left wall kicks");
        Assert(g.GetSnapshot().X == 0 && g.GetSnapshot().Rotation == 0, "R to 0 right kick");
        g = Game(TetrisPiece.T, 8, 5, 3); g.Execute(TetrisCommand.Clockwise);
        Assert(g.GetSnapshot().X == 7 && g.GetSnapshot().Rotation == 0, "L to 0 left kick");
        g = Game(TetrisPiece.T, 3, 20, 0); g.Execute(TetrisCommand.Clockwise);
        Assert(g.GetSnapshot().X == 2 && g.GetSnapshot().Y == 19 && g.GetSnapshot().Rotation == 1, "floor kick order");
        g = Game(TetrisPiece.T, 3, 20, 0); g.Execute(TetrisCommand.CounterClockwise);
        Assert(g.GetSnapshot().X == 4 && g.GetSnapshot().Y == 19 && g.GetSnapshot().Rotation == 3, "counterclockwise floor kick");
    }
    static void IKicks()
    {
        var g = Game(TetrisPiece.I, -2, 5, 1); g.Execute(TetrisCommand.CounterClockwise);
        Assert(g.GetSnapshot().X == 0 && g.GetSnapshot().Rotation == 0, "I two cell wall kick");
        g = Game(TetrisPiece.I, 3, 20, 0); g.Execute(TetrisCommand.Clockwise);
        Assert(g.GetSnapshot().X == 4 && g.GetSnapshot().Y == 18 && g.GetSnapshot().Rotation == 1, "I floor kick fifth attempt");
        g = Game(TetrisPiece.I, 3, 20, 0); g.Execute(TetrisCommand.CounterClockwise);
        Assert(g.GetSnapshot().X == 2 && g.GetSnapshot().Y == 18 && g.GetSnapshot().Rotation == 3, "I counterclockwise floor kick");
        g = Game(TetrisPiece.I, 8, 5, 3); g.Execute(TetrisCommand.Clockwise);
        Assert(g.GetSnapshot().X == 6 && g.GetSnapshot().Rotation == 0, "I right wall two cell kick");
    }
    static void ORotation()
    {
        var s = Fixture(TetrisPiece.O, 3, 20, 0); Ground(s); s.LockTicks = 4;
        var g = new TetrisGame(s); Assert(!g.Execute(TetrisCommand.Clockwise).Changed, "O rotation no operation");
        var a = g.GetSnapshot(); Assert(a.X == 3 && a.Y == 20 && a.Rotation == 0 && a.LockTicks == 4 && a.LockResets == 0, "O cannot stall");
    }
    static void HoldLimit()
    {
        var g = new TetrisGame(1u); var old = g.GetSnapshot(); var result = g.Execute(TetrisCommand.Hold); var s = g.GetSnapshot();
        Assert(result.Held && s.HoldPiece == old.ActivePiece && s.ActivePiece == old.NextQueue[0] && !s.CanHold && s.PieceSerial == old.PieceSerial + 1, "empty hold draws queue");
        Assert(!g.Execute(TetrisCommand.Hold).Changed, "second hold rejected"); g.Execute(TetrisCommand.HardDrop);
        s = g.GetSnapshot(); var queue = (TetrisPiece[])s.NextQueue.Clone(); var held = s.HoldPiece; var active = s.ActivePiece;
        g.Execute(TetrisCommand.Clockwise); g.Execute(TetrisCommand.Left); result = g.Execute(TetrisCommand.Hold); s = g.GetSnapshot();
        Assert(result.Held && s.ActivePiece == held && s.HoldPiece == active && s.Rotation == 0 && s.X == 3 && s.Y == 0, "hold resets spawn");
        Equal(queue, s.NextQueue, "swap does not draw queue");
    }
    static void HoldSpawnSupport()
    {
        // The fixed spawn lives wholly in hidden rows. Immediate hidden-row lockout means
        // a live game cannot have locked spawn blockers. Visible support must still allow Hold.
        var s = Fixture(TetrisPiece.T, 3, 0, 0); s.Cells[2 * 10 + 4] = TetrisPiece.J; Ground(s);
        var g = new TetrisGame(s); Assert(g.Execute(TetrisCommand.Hold).Held && !g.GetSnapshot().GameOver, "support itself does not block spawn");
    }
    static void FirstTouch()
    {
        var s = Fixture(TetrisPiece.O, 3, 19, 0); s.GravityTicks = 59;
        var g = new TetrisGame(s); g.Tick(false); s = g.GetSnapshot();
        Assert(s.Y == 20 && s.LockStarted && s.LockTicks == 30, "new contact full lock period");
        for (int i = 0; i < 29; i++) Assert(!g.Tick(false).Locked, "not early lock");
        Assert(g.Tick(false).Locked, "30th subsequent tick locks");
    }
    static void ResetLimit()
    {
        var s = Fixture(TetrisPiece.O, 3, 20, 0); Ground(s); s.LockTicks = 1; var g = new TetrisGame(s);
        for (int i = 0; i < 15; i++) { g.Execute(i % 2 == 0 ? TetrisCommand.Left : TetrisCommand.Right); Assert(g.GetSnapshot().LockTicks == 30 && g.GetSnapshot().LockResets == i + 1, "reset " + i); g.Tick(false); }
        int remaining = g.GetSnapshot().LockTicks; g.Execute(TetrisCommand.Right);
        Assert(g.GetSnapshot().LockTicks == remaining && g.GetSnapshot().LockResets == 15, "16th cannot reset");
        for (int i = 1; i < remaining; i++) Assert(!g.Tick(false).Locked, "budget exact remaining");
        Assert(g.Tick(false).Locked, "finite lock");
    }
    static void FailedMove()
    {
        var s = Fixture(TetrisPiece.O, -1, 20, 0); Ground(s); s.LockTicks = 2; var g = new TetrisGame(s);
        Assert(!g.Execute(TetrisCommand.Left).Changed && g.GetSnapshot().LockTicks == 2 && g.GetSnapshot().LockResets == 0, "failed horizontal no reset");
        g.Tick(false); Assert(g.Tick(false).Locked, "failed movement cannot save");
    }
    static TetrisSnapshot Ledge()
    {
        var s = Fixture(TetrisPiece.O, 3, 18, 0); s.Cells[20 * 10 + 4] = TetrisPiece.J; Ground(s); s.LockResets = 15; s.LockTicks = 1; return s;
    }
    static void AirborneExpiry()
    {
        var g = new TetrisGame(Ledge()); g.Execute(TetrisCommand.Right); var r = g.Tick(false);
        Assert(!r.Locked && g.GetSnapshot().LockTicks == 0 && g.GetSnapshot().Y == 18, "air expiry retained"); Valid(g.GetSnapshot());
        int serial = (int)g.GetSnapshot().PieceSerial;
        for (int i = 0; i < 200 && g.GetSnapshot().PieceSerial == serial; i++) g.Tick(false);
        Assert(g.GetSnapshot().PieceSerial == serial + 1, "locks on future contact");
        Assert(g.GetSnapshot().Cells[21 * 10 + 5] == TetrisPiece.O, "not suspended above floor");
    }
    static void ExpiredAirborneInput()
    {
        var s = Ledge(); s.X = 4; s.LockResets = 3; s.LockTicks = 0;
        var g = new TetrisGame(s); var r = g.Execute(TetrisCommand.Left);
        Assert(r.Locked && g.GetSnapshot().PieceSerial == 2, "expired input landing locks immediately despite unused resets");
        g = new TetrisGame(s); g.Execute(TetrisCommand.Right);
        Assert(g.GetSnapshot().LockTicks == 0 && g.GetSnapshot().LockResets == 3, "expired air movement cannot revive countdown");
    }
    static void BlockedRotation()
    {
        var s = Fixture(TetrisPiece.T, 3, 5, 0);
        for (int y = 3; y <= 10; y++) for (int x = 0; x < 9; x++) s.Cells[y * 10 + x] = TetrisPiece.J;
        foreach (var c in TetrisPieces.GetCells(TetrisPiece.T, 0)) s.Cells[(5 + c.Y) * 10 + 3 + c.X] = TetrisPiece.None;
        Ground(s); s.LockTicks = 5;
        var g = new TetrisGame(s); Assert(!g.Execute(TetrisCommand.Clockwise).Changed, "all five kick candidates blocked");
        Same(s, g.GetSnapshot()); Assert(!g.Execute(TetrisCommand.CounterClockwise).Changed, "all counterclockwise candidates blocked"); Same(s, g.GetSnapshot());
    }
    static void LowerGround()
    {
        var s = Ledge(); s.LockTicks = 20; var g = new TetrisGame(s); g.Execute(TetrisCommand.Right);
        for (int i = 0; i < 3; i++) g.Tick(true);
        Assert(g.GetSnapshot().Y == 19 && g.GetSnapshot().LockResets == 15 && g.GetSnapshot().LockTicks == 17, "lower line retains budget and elapsed lock");
    }
    static void TopoutRescue()
    {
        var s = Fixture(TetrisPiece.I, 2, 0, 1);
        for (int y = 2; y <= 3; y++) for (int x = 0; x < 10; x++) if (x != 4) s.Cells[y * 10 + x] = TetrisPiece.J;
        s.Cells[4 * 10 + 4] = TetrisPiece.J; Ground(s);
        var g = new TetrisGame(s); var r = g.Execute(TetrisCommand.HardDrop); var a = g.GetSnapshot();
        Assert(r.ClearedLines == 2 && !a.GameOver && a.Cells[2 * 10 + 4] == TetrisPiece.I && a.Cells[3 * 10 + 4] == TetrisPiece.I, "line clear moves hidden cells to visible"); Valid(a);
    }
    static TetrisGame TopoutGame()
    {
        var s = Fixture(TetrisPiece.O, 3, 0, 0); s.Cells[2 * 10 + 4] = TetrisPiece.J; Ground(s);
        var g = new TetrisGame(s); g.Execute(TetrisCommand.HardDrop); return g;
    }
    static void HiddenTopout()
    {
        var g = TopoutGame(); var s = g.GetSnapshot(); Assert(s.GameOver && s.ActivePiece == TetrisPiece.None, "hidden lockout"); Valid(s);
    }
    static void SpawnBlocked()
    {
        var s = TopoutGame().GetSnapshot(); s.ActivePiece = TetrisPiece.T; s.X = 3; s.Y = 0; s.Rotation = 0;
        Valid(s); var g = new TetrisGame(s); Assert(g.GetSnapshot().GameOver, "blocked spawn terminal snapshot accepted");
    }
    static void TerminalAndUnknown()
    {
        var g = new TetrisGame(2u); var s = g.GetSnapshot(); Assert(!g.Execute((TetrisCommand)99).Changed, "unknown rejected"); Same(s, g.GetSnapshot());
        g = TopoutGame(); s = g.GetSnapshot();
        for (int i = 0; i < 10; i++) { Assert(!g.Tick(true).Changed && !g.Execute(TetrisCommand.HardDrop).Changed, "terminal frozen"); }
        Same(s, g.GetSnapshot());
    }
    static void Saturation()
    {
        var s = Fixture(TetrisPiece.I, 3, 0, 0); s.Score = long.MaxValue - 1; var g = new TetrisGame(s);
        Assert(g.Execute(TetrisCommand.HardDrop).ScoreDelta == 1 && g.GetSnapshot().Score == long.MaxValue, "score saturation");
        s = ClearFixture(4, int.MaxValue - 1); s.ElapsedTicks = long.MaxValue; s.PieceSerial = long.MaxValue; var h = new TetrisGame(s); h.Tick(false); h.Execute(TetrisCommand.HardDrop);
        Assert(h.GetSnapshot().Lines == int.MaxValue && h.GetSnapshot().ElapsedTicks == long.MaxValue && h.GetSnapshot().PieceSerial == long.MaxValue, "counter saturation"); Valid(h.GetSnapshot());
    }
    static void RestoreFuture()
    {
        var a = new TetrisGame(7123u); for (int i = 0; i < 47; i++) a.Tick(i % 4 < 2); a.Execute(TetrisCommand.Hold); a.Execute(TetrisCommand.Clockwise);
        var b = new TetrisGame(a.GetSnapshot());
        for (int i = 0; i < 400; i++) {
            if (i % 17 == 0) { a.Execute(TetrisCommand.HardDrop); b.Execute(TetrisCommand.HardDrop); }
            else if (i % 5 == 0) { var cmd = (TetrisCommand)(i % 6); a.Execute(cmd); b.Execute(cmd); }
            else { a.Tick(i % 3 == 0); b.Tick(i % 3 == 0); }
            Same(a.GetSnapshot(), b.GetSnapshot());
        }
    }
    static void InvalidSnapshots()
    {
        Reject(delegate(TetrisSnapshot s) { s.RulesVersion = 2; });
        Reject(delegate(TetrisSnapshot s) { s.Cells = null; });
        Reject(delegate(TetrisSnapshot s) { s.Cells = new TetrisPiece[219]; });
        Reject(delegate(TetrisSnapshot s) { s.Cells[30] = (TetrisPiece)8; });
        Reject(delegate(TetrisSnapshot s) { s.ActivePiece = TetrisPiece.None; });
        Reject(delegate(TetrisSnapshot s) { s.X = int.MaxValue; });
        Reject(delegate(TetrisSnapshot s) { s.Rotation = 4; });
        Reject(delegate(TetrisSnapshot s) { s.NextQueue = new TetrisPiece[2]; });
        Reject(delegate(TetrisSnapshot s) { s.NextQueue[0] = TetrisPiece.None; });
        Reject(delegate(TetrisSnapshot s) { s.Bag = new[] { TetrisPiece.I, TetrisPiece.I }; });
        Reject(delegate(TetrisSnapshot s) { s.RngState = 0; });
        Reject(delegate(TetrisSnapshot s) { s.Score = -1; });
        Reject(delegate(TetrisSnapshot s) { s.Lines = -1; });
        Reject(delegate(TetrisSnapshot s) { s.GravityTicks = 60; });
        Reject(delegate(TetrisSnapshot s) { s.SoftDropTicks = 3; });
        Reject(delegate(TetrisSnapshot s) { s.SoftDropTicks = 1; s.GravityTicks = 1; });
        Reject(delegate(TetrisSnapshot s) { s.LockTicks = 30; });
        Reject(delegate(TetrisSnapshot s) { s.LockStarted = true; s.LockResets = 16; });
        Reject(delegate(TetrisSnapshot s) { s.CanHold = false; s.HoldPiece = TetrisPiece.None; });
        Reject(delegate(TetrisSnapshot s) { s.ElapsedTicks = -1; });
        Reject(delegate(TetrisSnapshot s) { s.PieceSerial = 0; });
        Reject(delegate(TetrisSnapshot s) { s.Cells[0] = TetrisPiece.J; });
        Reject(delegate(TetrisSnapshot s) { for (int x = 0; x < 10; x++) s.Cells[210 + x] = TetrisPiece.I; });
        Reject(delegate(TetrisSnapshot s) { s.GameOver = true; });
        var terminal = TopoutGame().GetSnapshot(); terminal.CanHold = false; terminal.HoldPiece = TetrisPiece.O; Invalid(terminal);
        var grounded = Fixture(TetrisPiece.O, 3, 20, 0); Invalid(grounded); Ground(grounded); grounded.LockTicks = 0; Invalid(grounded);
    }
    static void RandomSnapshots()
    {
        var rng = new Random(43);
        for (int run = 0; run < 40; run++) {
            var g = new TetrisGame((uint)(run + 1));
            for (int i = 0; i < 1500; i++) {
                if (rng.Next(4) == 0) g.Execute((TetrisCommand)rng.Next(6)); else g.Tick(rng.Next(2) == 0);
                var s = g.GetSnapshot(); Valid(s); var restored = new TetrisGame(s); Same(s, restored.GetSnapshot());
                if (s.GameOver) break;
            }
        }
    }

    static void IdentityGetters()
    {
        var serial = typeof(TetrisGame).GetProperty("PieceSerial");
        var over = typeof(TetrisGame).GetProperty("GameOver");
        Assert(serial != null && serial.CanRead && !serial.CanWrite && over != null && over.CanRead && !over.CanWrite, "public read-only scalar getters");
        var game = new TetrisGame(12u);
        Assert((long)serial.GetValue(game) == 1 && !(bool)over.GetValue(game), "fresh scalar state");
        game.Execute(TetrisCommand.Hold); Assert((long)serial.GetValue(game) == 2, "hold identity");
        Assert((bool)over.GetValue(TopoutGame()), "terminal scalar state");
        game = new TetrisGame(12u); game.Tick(false);
        long sum = 0, before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) { sum += game.PieceSerial; Assert(!game.GameOver, "read stays live"); }
        for (int i = 0; i < 100; i++) game.Tick(false);
        Assert(sum == 10000 && GC.GetAllocatedBytesForCurrentThread() == before, "identity reads and nonlocking fixed ticks allocate nothing");
    }

    static TetrisSnapshot Fixture(TetrisPiece p, int x, int y, int rotation)
    {
        var s = new TetrisGame(99u).GetSnapshot(); s.ActivePiece = p; s.X = x; s.Y = y; s.Rotation = rotation;
        s.LockStarted = false; s.LockTicks = 0; s.LockResets = 0; return s;
    }
    static TetrisGame Game(TetrisPiece p, int x, int y, int rotation) { var s = Fixture(p, x, y, rotation); if (AtGround(s)) Ground(s); return new TetrisGame(s); }
    static bool AtGround(TetrisSnapshot s) { foreach (var c in TetrisPieces.GetCells(s.ActivePiece, s.Rotation)) { int x = s.X + c.X, y = s.Y + c.Y + 1; if (y >= 22 || s.Cells[y * 10 + x] != TetrisPiece.None) return true; } return false; }
    static void Ground(TetrisSnapshot s) { s.LockStarted = true; s.LockTicks = 30; }
    static void Reject(Action<TetrisSnapshot> mutation) { var s = new TetrisGame(13u).GetSnapshot(); mutation(s); Invalid(s); }
    static void Invalid(TetrisSnapshot s) { string error; Assert(!s.TryValidate(out error) && !string.IsNullOrEmpty(error), "bad snapshot rejected"); Throws<ArgumentException>(delegate { new TetrisGame(s); }); }
    static void Valid(TetrisSnapshot s) { string error; Assert(s.TryValidate(out error), "snapshot: " + error); }
    static void Same(TetrisSnapshot a, TetrisSnapshot b)
    {
        Assert(a.RulesVersion == b.RulesVersion && a.ActivePiece == b.ActivePiece && a.HoldPiece == b.HoldPiece && a.Rotation == b.Rotation && a.X == b.X && a.Y == b.Y && a.CanHold == b.CanHold && a.RngState == b.RngState && a.Score == b.Score && a.Lines == b.Lines && a.GravityTicks == b.GravityTicks && a.SoftDropTicks == b.SoftDropTicks && a.LockTicks == b.LockTicks && a.LockResets == b.LockResets && a.LockStarted == b.LockStarted && a.GameOver == b.GameOver && a.ElapsedTicks == b.ElapsedTicks && a.PieceSerial == b.PieceSerial, "all scalar state identical");
        Equal(a.Cells, b.Cells, "cells"); Equal(a.NextQueue, b.NextQueue, "queue"); Equal(a.Bag, b.Bag, "bag");
    }
    static void Equal(TetrisPiece[] a, TetrisPiece[] b, string message) { Assert(a.Length == b.Length, message); for (int i = 0; i < a.Length; i++) Assert(a[i] == b[i], message); }
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static void Throws<T>(Action body) where T : Exception { try { body(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    static void Run(string name, Action body) { try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); } }
}
