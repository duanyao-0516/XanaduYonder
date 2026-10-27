using System;
using System.Collections.Generic;
using XanaduYonder.Core.Watermelon;

internal static class Program
{
    private static int passed, failed;

    private static void Main()
    {
        Check("new games have a reproducible valid two-ball queue", () => {
            var a = new WatermelonGame(1234).GetSnapshot();
            var b = new WatermelonGame(1234).GetSnapshot();
            Valid(a);
            Assert(a.CurrentLevel >= 0 && a.CurrentLevel <= 4 && a.NextLevel >= 0 && a.NextLevel <= 4);
            Assert(a.Balls.Length == 0 && a.Score == 0 && a.NextId == 1);
            Equal(a, b);
        });
        Check("seed zero normalizes to a reproducible nonzero generator", () => {
            var a = new WatermelonGame(0).GetSnapshot();
            Valid(a); Assert(a.RandomState != 0); Equal(a, new WatermelonGame(0).GetSnapshot());
        });
        Check("drops consume the queue and respect left and right radii", () => {
            var game = new WatermelonGame(56); var before = game.GetSnapshot();
            WatermelonBallState left, right;
            Assert(game.TryDrop(-100, out left));
            Assert(left.Level == before.CurrentLevel && left.Id == 1);
            Near(left.X, WatermelonRules.GetRadius(left.Level)); Near(left.Y, WatermelonRules.SpawnY);
            var after = game.GetSnapshot();
            Assert(after.CurrentLevel == before.NextLevel && after.NextId == 2 && after.RandomState != before.RandomState);
            Advance(game, .4f); Assert(game.TryDrop(100, out right));
            Near(right.X, WatermelonRules.Width - WatermelonRules.GetRadius(right.Level));
            Assert(!right.Contacted && right.Age == 0);
        });
        Check("cooldown rejects rapid drops without consuming state or randomness", () => {
            var game = new WatermelonGame(1); WatermelonBallState ball;
            Assert(game.TryDrop(3, out ball)); var before = game.GetSnapshot();
            Assert(!game.TryDrop(4, out ball) && ball == null); Equal(before, game.GetSnapshot());
            Advance(game, .37f); Assert(!game.TryDrop(4, out ball));
            Advance(game, .02f); Assert(game.TryDrop(4, out ball));
        });
        Check("nonfinite drop coordinates reject atomically", () => {
            var game = new WatermelonGame(1); var before = game.GetSnapshot(); WatermelonBallState ball;
            Assert(!game.TryDrop(float.NaN, out ball)); Assert(!game.TryDrop(float.PositiveInfinity, out ball));
            Equal(before, game.GetSnapshot());
        });
        Check("all five starting sizes appear without generating larger sizes", () => {
            var game = new WatermelonGame(434); var seen = new bool[5];
            for (int i = 0; i < 100; i++) {
                WatermelonBallState ball; Assert(game.TryDrop(3, out ball));
                Assert(ball.Level >= 0 && ball.Level <= 4); seen[ball.Level] = true; Advance(game, .4f);
            }
            foreach (bool item in seen) Assert(item);
        });
        Check("same-size contact creates one larger ball and scores its old size", () => {
            var game = From(Ball(1, 3, 2, 2), Ball(2, 3, 4, 3));
            WatermelonMergeResult result; Assert(game.TryMerge(1, 2, out result));
            Assert(result.FirstId == 1 && result.SecondId == 2 && result.NewBall.Level == 4 && result.NewBall.Id == 3);
            Assert(result.AddedScore == 4 && !result.ReachedWatermelon);
            Near(result.NewBall.X, 3); Near(result.NewBall.Y, 2.5f);
            Assert(result.NewBall.Contacted && result.NewBall.Age == 0);
            var after = game.GetSnapshot(); Assert(after.Balls.Length == 1 && after.Score == 4 && after.NextId == 4); Valid(after);
        });
        Check("duplicate contact callbacks and consumed partners cannot score twice", () => {
            var game = From(Ball(1, 0), Ball(2, 0), Ball(3, 0));
            WatermelonMergeResult result; Assert(game.TryMerge(1, 2, out result)); var after = game.GetSnapshot();
            Assert(!game.TryMerge(2, 1, out result) && result == null);
            Assert(!game.TryMerge(1, 3, out result)); Equal(after, game.GetSnapshot());
        });
        Check("invalid or unlike collision partners leave state unchanged", () => {
            var game = From(Ball(1, 0), Ball(2, 1)); var before = game.GetSnapshot(); WatermelonMergeResult result;
            Assert(!game.TryMerge(1, 1, out result)); Assert(!game.TryMerge(1, 2, out result));
            Assert(!game.TryMerge(1, 999, out result)); Equal(before, game.GetSnapshot());
        });
        Check("larger merge radius is clamped within all container sides", () => {
            var game = From(Ball(1, 9, -.1f, -.1f), Ball(2, 9, -.1f, -.1f));
            WatermelonMergeResult result; Assert(game.TryMerge(1, 2, out result));
            Near(result.NewBall.X, WatermelonRules.GetRadius(10)); Near(result.NewBall.Y, WatermelonRules.GetRadius(10));
            game = From(Ball(1, 9, 6.1f, 9), Ball(2, 9, 6.1f, 9));
            Assert(game.TryMerge(1, 2, out result));
            Near(result.NewBall.X, WatermelonRules.Width - WatermelonRules.GetRadius(10));
            Near(result.NewBall.Y, WatermelonRules.Height - WatermelonRules.GetRadius(10));
        });
        Check("merge keeps average translational and angular velocity", () => {
            var a = Ball(1, 2); var b = Ball(2, 2);
            a.VelocityX = 2; a.VelocityY = -6; a.AngularVelocity = 30;
            b.VelocityX = -4; b.VelocityY = 2; b.AngularVelocity = -10;
            var game = From(a, b); WatermelonMergeResult result; Assert(game.TryMerge(1, 2, out result));
            Near(result.NewBall.VelocityX, -1); Near(result.NewBall.VelocityY, -2); Near(result.NewBall.AngularVelocity, 10);
        });
        Check("watermelon achievement remains playable but largest balls cannot merge", () => {
            var game = From(Ball(1, 9), Ball(2, 9), Ball(3, 10)); WatermelonMergeResult result;
            Assert(game.TryMerge(1, 2, out result) && result.ReachedWatermelon && game.HasWatermelon);
            Assert(!game.GetSnapshot().GameOver); Assert(!game.TryMerge(3, result.NewBall.Id, out result));
            WatermelonBallState dropped; Assert(game.TryDrop(3, out dropped));
        });
        Check("scores saturate without integer overflow", () => {
            var snapshot = State(Ball(1, 9), Ball(2, 9)); snapshot.Score = int.MaxValue - 3;
            var game = new WatermelonGame(snapshot); WatermelonMergeResult result;
            Assert(game.TryMerge(1, 2, out result)); Assert(game.GetSnapshot().Score == int.MaxValue && result.AddedScore == 3);
        });
        Check("restoring a snapshot reproduces the future queue and ids", () => {
            var a = new WatermelonGame(888); WatermelonBallState ball; Assert(a.TryDrop(2, out ball)); Advance(a, .4f);
            var b = new WatermelonGame(a.GetSnapshot());
            for (int i = 0; i < 10; i++) {
                Assert(a.TryDrop(i % 6, out ball)); Assert(b.TryDrop(i % 6, out ball));
                Advance(a, .4f); Advance(b, .4f); Equal(a.GetSnapshot(), b.GetSnapshot());
            }
        });
        Check("motion updates preserve queue, score, age and irreversible contact", () => {
            var original = Ball(1, 2); original.Age = 2; original.Contacted = true;
            var game = From(original); var moved = Ball(1, 2, 2, 3); moved.Age = 500;
            moved.VelocityX = 1; moved.VelocityY = 2; moved.Angle = 40; moved.AngularVelocity = 20;
            var before = game.GetSnapshot(); Assert(game.UpdateMotion(new[] { moved })); var after = game.GetSnapshot();
            Assert(after.Score == before.Score && after.RandomState == before.RandomState && after.CurrentLevel == before.CurrentLevel);
            var body = after.Balls[0]; Near(body.X, 2); Near(body.Y, 3); Near(body.Angle, 40); Near(body.Age, 2);
            Near(body.VelocityX, 1); Near(body.VelocityY, 2); Near(body.AngularVelocity, 20); Assert(body.Contacted);
        });
        Check("motion requires one complete valid batch without partial writes", () => {
            var game = From(Ball(1, 0), Ball(2, 0)); var before = game.GetSnapshot();
            Assert(!game.UpdateMotion(null)); Assert(!game.UpdateMotion(new[] { Ball(1, 0) }));
            Assert(!game.UpdateMotion(new[] { Ball(1, 0, 4), Ball(1, 0) }));
            Assert(!game.UpdateMotion(new[] { Ball(1, 0, 4), Ball(99, 0) }));
            Assert(!game.UpdateMotion(new[] { Ball(1, 0, 4), Ball(2, 1) }));
            Assert(!game.UpdateMotion(new[] { Ball(1, 0, 4), null }));
            var invalid = Ball(2, 0); invalid.VelocityY = float.NaN;
            Assert(!game.UpdateMotion(new[] { Ball(1, 0, 4), invalid })); Equal(before, game.GetSnapshot());
        });
        Check("motion batch may be reordered and marks new contacts", () => {
            var game = From(Ball(1, 0), Ball(2, 1)); var a = Ball(1, 0, 1); a.Contacted = true;
            Assert(game.UpdateMotion(new[] { Ball(2, 1, 4), a }));
            var after = game.GetSnapshot(); Near(after.Balls[0].X, 1); Near(after.Balls[1].X, 4); Assert(after.Balls[0].Contacted);
        });
        Check("first falling ball does not start overflow timer", () => {
            var game = new WatermelonGame(12); WatermelonBallState ball; Assert(game.TryDrop(3, out ball));
            Advance(game, 4); var after = game.GetSnapshot(); Assert(after.DangerTime == 0 && !after.GameOver);
        });
        Check("newly contacted balls receive age grace before overflow", () => {
            var ball = Ball(1, 0, 3, 6.4f); ball.Contacted = true; var game = From(ball);
            Advance(game, 1.1f); Assert(game.GetSnapshot().DangerTime == 0);
            Advance(game, .2f); Assert(game.GetSnapshot().DangerTime > 0 && !game.GetSnapshot().GameOver);
        });
        Check("overflow uses ball top and loses only after two seconds", () => {
            var ball = Ball(1, 2, 3, 6.05f); ball.Contacted = true; ball.Age = 2;
            var game = From(ball); Advance(game, 1.9f); Assert(!game.GetSnapshot().GameOver);
            Advance(game, .1f); var after = game.GetSnapshot(); Assert(after.GameOver); Near(after.DangerTime, 2); Valid(after);
        });
        Check("moving above line does not accumulate danger", () => {
            var ball = Ball(1, 1, 3, 6.4f); ball.Contacted = true; ball.Age = 2; ball.VelocityX = .4f; ball.VelocityY = .4f;
            var game = From(ball); Advance(game, 3); Assert(!game.GetSnapshot().GameOver && game.GetSnapshot().DangerTime == 0);
        });
        Check("returning below danger line resets the grace timer", () => {
            var ball = Ball(1, 1, 3, 6.4f); ball.Contacted = true; ball.Age = 2;
            var game = From(ball); Advance(game, 1.5f); ball.Y = 2; Assert(game.UpdateMotion(new[] { ball }));
            game.Advance(.02f); Near(game.GetSnapshot().DangerTime, 0);
            ball.Y = 6.4f; Assert(game.UpdateMotion(new[] { ball })); Advance(game, 1); Assert(!game.GetSnapshot().GameOver);
        });
        Check("a fresh impulse also resets accumulated danger", () => {
            var ball = Ball(1, 1, 3, 6.4f); ball.Contacted = true; ball.Age = 2;
            var game = From(ball); Advance(game, 1.5f); ball.VelocityY = -1;
            Assert(game.UpdateMotion(new[] { ball })); game.Advance(.02f); Near(game.GetSnapshot().DangerTime, 0);
        });
        Check("merge before danger step can rescue a nearly lost game", () => {
            var a = Ball(1, 0, 3, 6.4f); var b = Ball(2, 0, 3, 6.4f);
            a.Contacted = b.Contacted = true; a.Age = b.Age = 2;
            var snapshot = State(a, b); snapshot.DangerTime = 1.99f;
            var game = new WatermelonGame(snapshot); WatermelonMergeResult result;
            Assert(game.TryMerge(1, 2, out result)); game.Advance(.02f);
            Assert(!game.GetSnapshot().GameOver && game.GetSnapshot().DangerTime == 0);
        });
        Check("ended games reject all mutations and random consumption", () => {
            var snapshot = State(Ball(1, 0), Ball(2, 0)); snapshot.GameOver = true; snapshot.DangerTime = 2;
            var game = new WatermelonGame(snapshot); var before = game.GetSnapshot();
            WatermelonBallState ball; WatermelonMergeResult result;
            Assert(!game.TryDrop(3, out ball)); Assert(!game.TryMerge(1, 2, out result));
            Assert(!game.UpdateMotion(new[] { Ball(1, 0), Ball(2, 0) })); game.Advance(.1f); Equal(before, game.GetSnapshot());
        });
        Check("invalid elapsed time is rejected without state change", () => {
            var game = new WatermelonGame(1); var before = game.GetSnapshot();
            Throws<ArgumentOutOfRangeException>(() => game.Advance(float.NaN));
            Throws<ArgumentOutOfRangeException>(() => game.Advance(float.PositiveInfinity));
            Throws<ArgumentOutOfRangeException>(() => game.Advance(-1)); Throws<ArgumentOutOfRangeException>(() => game.Advance(1));
            game.Advance(0); Equal(before, game.GetSnapshot());
        });
        Check("age saturates for long-lived fruit without corrupting snapshots", () => {
            var ball = Ball(1, 0); ball.Age = 86400; var game = From(ball); game.Advance(.2f);
            Near(game.GetSnapshot().Balls[0].Age, 86400); Valid(game.GetSnapshot());
        });
        Check("ball cap and exhausted ids reject drops without advancing queue", () => {
            var balls = new WatermelonBallState[WatermelonRules.MaxBalls];
            for (int i = 0; i < balls.Length; i++) balls[i] = Ball(i + 1, 0);
            var game = From(balls); var before = game.GetSnapshot(); WatermelonBallState ball;
            Assert(!game.TryDrop(3, out ball)); Equal(before, game.GetSnapshot());
            var snapshot = State(); snapshot.NextId = int.MaxValue; game = new WatermelonGame(snapshot); before = game.GetSnapshot();
            Assert(!game.TryDrop(3, out ball)); Equal(before, game.GetSnapshot());
        });
        Check("id exhaustion never consumes merge partners", () => {
            var snapshot = State(Ball(1, 0), Ball(2, 0)); snapshot.NextId = int.MaxValue;
            var game = new WatermelonGame(snapshot); var before = game.GetSnapshot(); WatermelonMergeResult result;
            Assert(!game.TryMerge(1, 2, out result)); Equal(before, game.GetSnapshot());
        });
        Check("snapshots and returned bodies cannot mutate live game state", () => {
            var source = State(Ball(1, 0)); var game = new WatermelonGame(source); source.Balls[0].X = 99;
            var snapshot = game.GetSnapshot(); snapshot.Balls[0].Y = 99; snapshot.Score = 99;
            Assert(game.GetSnapshot().Balls[0].X != 99 && game.GetSnapshot().Balls[0].Y != 99 && game.GetSnapshot().Score == 0);
            WatermelonBallState drop; Assert(game.TryDrop(2, out drop)); drop.X = 99; Assert(game.GetSnapshot().Balls[1].X == 2);
            var copied = snapshot.Copy(); copied.Balls[0].Level = 10; Assert(snapshot.Balls[0].Level == 0);
        });
        Check("merge result cannot mutate the newly inserted ball", () => {
            var game = From(Ball(1, 0), Ball(2, 0)); WatermelonMergeResult result;
            Assert(game.TryMerge(1, 2, out result)); result.NewBall.X = 99; Assert(game.GetSnapshot().Balls[0].X != 99);
        });
        Check("invalid snapshot arrays ids levels and random state are rejected", () => {
            Invalid(s => s.Balls = null); Invalid(s => s.Balls = new WatermelonBallState[] { null });
            Invalid(s => s.Balls[0].Id = 0); Invalid(s => s.Balls[0].Id = s.NextId);
            Invalid(s => s.Balls = new[] { s.Balls[0], s.Balls[0].Copy() });
            Invalid(s => s.Balls[0].Level = -1); Invalid(s => s.Balls[0].Level = 11);
            Invalid(s => s.CurrentLevel = -1); Invalid(s => s.NextLevel = 5); Invalid(s => s.RandomState = 0);
            Invalid(s => s.Score = -1); Invalid(s => s.NextId = 0);
            Invalid(s => s.Balls = new WatermelonBallState[129]);
        });
        Check("invalid snapshot floating point fields and timers are rejected", () => {
            Invalid(s => s.Balls[0].X = float.NaN); Invalid(s => s.Balls[0].Y = float.PositiveInfinity);
            Invalid(s => s.Balls[0].VelocityX = float.NaN); Invalid(s => s.Balls[0].VelocityY = float.NegativeInfinity);
            Invalid(s => s.Balls[0].Angle = float.NaN); Invalid(s => s.Balls[0].AngularVelocity = float.NaN);
            Invalid(s => s.Balls[0].Age = float.NaN); Invalid(s => s.Balls[0].Age = -1);
            Invalid(s => s.Balls[0].X = 100); Invalid(s => s.Balls[0].Y = -100); Invalid(s => s.Balls[0].VelocityY = 101);
            Invalid(s => s.DropCooldown = float.NaN); Invalid(s => s.DropCooldown = -.1f); Invalid(s => s.DropCooldown = 1);
            Invalid(s => s.DangerTime = float.PositiveInfinity); Invalid(s => s.DangerTime = -1);
            Invalid(s => s.DangerTime = 2); Invalid(s => s.GameOver = true);
        });
        Check("valid minor solver penetration and transient overshoot can be restored", () => {
            var a = Ball(1, 0, -.02f, -.02f); var b = Ball(2, 0, 6.02f, 7.8f);
            a.Angle = 100000; b.AngularVelocity = -2000;
            Valid(State(a, b)); Valid(new WatermelonGame(State(a, b)).GetSnapshot());
        });
        Check("restoration rejects invalid snapshots before they become live state", () => {
            Throws<ArgumentNullException>(() => new WatermelonGame(null));
            var snapshot = State(Ball(1, 0)); snapshot.Balls[0].X = float.NaN;
            Throws<ArgumentException>(() => new WatermelonGame(snapshot));
        });
        Check("radius lookup rejects invalid levels instead of silently remapping", () => {
            Throws<ArgumentOutOfRangeException>(() => WatermelonRules.GetRadius(-1));
            Throws<ArgumentOutOfRangeException>(() => WatermelonRules.GetRadius(11));
            for (int i = 1; i < WatermelonRules.LevelCount; i++) Assert(WatermelonRules.GetRadius(i) > WatermelonRules.GetRadius(i - 1));
        });
        Console.WriteLine("Watermelon: " + passed + " passed, " + failed + " failed.");
        if (failed > 0) Environment.Exit(1);
    }

    private static WatermelonBallState Ball(int id, int level, float x = 3, float y = 2)
    { return new WatermelonBallState { Id = id, Level = level, X = x, Y = y }; }

    private static WatermelonSnapshot State(params WatermelonBallState[] balls)
    {
        var result = new WatermelonSnapshot { Balls = balls, CurrentLevel = 0, NextLevel = 1, NextId = 1, RandomState = 123 };
        foreach (var ball in balls) result.NextId = Math.Max(result.NextId, ball.Id + 1);
        return result;
    }

    private static WatermelonGame From(params WatermelonBallState[] balls) { return new WatermelonGame(State(balls)); }
    private static void Advance(WatermelonGame game, float time)
    {
        while (time > .000001f) { float step = Math.Min(.02f, time); game.Advance(step); time -= step; }
    }
    private static void Valid(WatermelonSnapshot snapshot)
    { string error; Assert(snapshot.TryValidate(out error), "Snapshot rejected: " + error); }
    private static void Invalid(Action<WatermelonSnapshot> corrupt)
    {
        var snapshot = State(Ball(1, 0)); corrupt(snapshot); string error;
        Assert(!snapshot.TryValidate(out error) && !string.IsNullOrEmpty(error), "Invalid snapshot accepted.");
    }
    private static void Equal(WatermelonSnapshot a, WatermelonSnapshot b)
    {
        Assert(a.Score == b.Score && a.CurrentLevel == b.CurrentLevel && a.NextLevel == b.NextLevel && a.RandomState == b.RandomState);
        Assert(a.NextId == b.NextId && a.GameOver == b.GameOver && a.Balls.Length == b.Balls.Length);
        Near(a.DropCooldown, b.DropCooldown); Near(a.DangerTime, b.DangerTime);
        for (int i = 0; i < a.Balls.Length; i++) {
            var x = a.Balls[i]; var y = b.Balls[i];
            Assert(x.Id == y.Id && x.Level == y.Level && x.Contacted == y.Contacted);
            Near(x.X, y.X); Near(x.Y, y.Y); Near(x.VelocityX, y.VelocityX); Near(x.VelocityY, y.VelocityY);
            Near(x.Angle, y.Angle); Near(x.AngularVelocity, y.AngularVelocity); Near(x.Age, y.Age);
        }
    }
    private static void Near(float a, float b) { Assert(Math.Abs(a - b) < .0001f, a + " != " + b); }
    private static void Assert(bool condition, string message = "Assertion failed.") { if (!condition) throw new Exception(message); }
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    private static void Check(string name, Action action)
    { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
}
