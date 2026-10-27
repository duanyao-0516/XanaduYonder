using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XanaduYonder.Core.Watermelon;
using XanaduYonder.Runtime.Watermelon;

namespace XanaduYonder.EditorTools
{
    // Run without -quit: the test exits after entering real Play Mode (edit-mode Simulate
    // deliberately does not dispatch collision callbacks). No test hook ships in the APK.
    [InitializeOnLoad]
    public static class WatermelonPhysicsChecks
    {
        private const string Pending = "Xanadu.WatermelonPhysicsChecks.Pending";
        private static int failures;
        static WatermelonPhysicsChecks() { EditorApplication.playModeStateChanged += StateChanged; }

        public static void Run()
        {
            Debug.Log("WATERMELON PHYSICS entering Play Mode");
            SessionState.SetBool(Pending, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            // Do not share delayCall with startup search indexing: a Unity Search exception
            // in that multicast delegate can prevent subsequent delegates from being invoked.
            Execute();
        }

        private static void Execute()
        {
            int passed = 0;
            try
            {
                RunCase("native falling, floor and circle contact", Falling, ref passed);
                RunCase("same-level contact merges exactly once", Merge, ref passed);
                RunCase("contact chain produces level 11; highest level persists", Goal, ref passed);
                RunCase("blocking spawn does not consume queue", BlockedSpawn, ref passed);
                RunCase("separate worlds and captured motion survive resume", Resume, ref passed);
                RunCase("resting overflow finishes after grace", Overflow, ref passed);
                RunCase("seeded drop stress preserves valid snapshots", Stress, ref passed);
                Require(failures == 0, failures + " physics check(s) failed; " + passed + " passed");
                Directory.CreateDirectory("artifacts/validation");
                File.WriteAllText("artifacts/validation/watermelon-physics.txt", "PASS " + passed + " real Unity Play Mode Physics2D checks\n");
                Debug.Log("WATERMELON PHYSICS PASS " + passed);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void RunCase(string name, Action action, ref int count)
        {
            try { action(); count++; Debug.Log("PASS Watermelon: " + name); }
            catch (Exception exception) { failures++; Debug.LogError("FAIL Watermelon: " + name + "\n" + exception); }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Simulate(WatermelonPhysicsWorld world, int frames) { for (int i = 0; i < frames; i++) world.Step(WatermelonPhysicsWorld.FixedStep); }
        private static WatermelonBallState Ball(int id, int level, float x, float y) { return new WatermelonBallState { Id = id, Level = level, X = x, Y = y }; }
        private static WatermelonGame Fixture(params WatermelonBallState[] balls)
        {
            var snapshot = new WatermelonGame(1234).GetSnapshot();
            snapshot.Balls = balls; snapshot.NextId = balls.Length == 0 ? 1 : balls.Max(b => b.Id) + 1;
            return new WatermelonGame(snapshot);
        }

        private static void Falling()
        {
            var game = new WatermelonGame(1234);
            using (var world = new WatermelonPhysicsWorld(game))
            {
                Require(world.TryDrop(-10), "Clamped drop rejected");
                world.Capture();
                Require(Mathf.Abs(game.GetSnapshot().Balls[0].Y - WatermelonRules.SpawnY) < .001f, "Native body did not start at spawn height");
                Simulate(world, 240);
                var ball = game.GetSnapshot().Balls.Single();
                float radius = WatermelonRules.GetRadius(ball.Level);
                Require(Mathf.Abs(ball.Y - radius) < .05f && ball.X >= radius - .05f, "Ball escaped floor/wall");
                Require(ball.Contacted, "Real floor callback not recorded");
                Require(!game.GetSnapshot().GameOver, "Normal drop incorrectly ended game");
            }
        }

        private static void Merge()
        {
            var game = Fixture(Ball(1, 0, 3, .19f), Ball(2, 0, 3, 1.2f));
            int merges = 0;
            using (var world = new WatermelonPhysicsWorld(game))
            {
                world.Merged += _ => merges++;
                Simulate(world, 240);
                var snapshot = game.GetSnapshot();
                Require(merges == 1 && snapshot.Score == 1 && snapshot.Balls.Length == 1 && snapshot.Balls[0].Level == 1, "Collision merge missing or duplicate");
            }
        }

        private static void Goal()
        {
            var game = Fixture(Ball(1, 8, 1.2f, .87f), Ball(2, 8, 1.2f, 2.7f), Ball(3, 9, 3, 1));
            using (var world = new WatermelonPhysicsWorld(game))
            {
                Simulate(world, 420);
                Require(game.HasWatermelon && game.GetSnapshot().Score == 19, "Native chain failed to produce goal; score=" + game.Score + " balls=" + game.BallCount);
                Require(game.GetSnapshot().Balls.Length == 1, "Consumed bodies remain after chain");
            }
            var highest = Fixture(Ball(1, 10, 1.2f, 1.16f), Ball(2, 10, 3.52f, 1.16f));
            using (var world = new WatermelonPhysicsWorld(highest))
            {
                Simulate(world, 180);
                Require(highest.GetSnapshot().Balls.Length == 2 && highest.GetSnapshot().Score == 0, "Highest level merged again");
            }
        }

        private static void BlockedSpawn()
        {
            var game = Fixture(Ball(1, 4, 3, WatermelonRules.SpawnY));
            using (var world = new WatermelonPhysicsWorld(game))
            {
                var before = game.GetSnapshot();
                world.Capture();
                var native = game.GetSnapshot().Balls[0];
                Require(Mathf.Abs(native.X - 3) < .001f && Mathf.Abs(native.Y - WatermelonRules.SpawnY) < .001f, "Constructing native body reset snapshot position");
                Require(!world.TryDrop(3), "Overlapping spawn accepted");
                var after = game.GetSnapshot();
                Require(after.NextId == before.NextId && after.RandomState == before.RandomState && after.Balls.Length == 1, "Blocked spawn changed state");
            }
        }

        private static void Resume()
        {
            var game = new WatermelonGame(1234);
            using (var first = new WatermelonPhysicsWorld(game))
            {
                Require(first.TryDrop(2), "Drop rejected"); Simulate(first, 15); first.Capture();
                var captured = game.GetSnapshot();
                var restored = new WatermelonGame(captured);
                using (var second = new WatermelonPhysicsWorld(restored))
                {
                    Simulate(second, 3);
                    Require(game.GetSnapshot().Balls[0].Y == captured.Balls[0].Y, "Other local scene advanced paused world");
                    Simulate(first, 3);
                    Require(Mathf.Abs(game.GetSnapshot().Balls[0].Y - restored.GetSnapshot().Balls[0].Y) < .001f, "Restored motion diverged without contacts");
                }
            }
        }

        private static void Overflow()
        {
            var game = Fixture(Ball(1, 10, 3, 1.16f), Ball(2, 9, 3, 3.32f), Ball(3, 8, 3, 5.19f), Ball(4, 7, 3, 6.81f));
            using (var world = new WatermelonPhysicsWorld(game))
            {
                Simulate(world, 45);
                Require(!game.GetSnapshot().GameOver, "Overflow had no grace");
                Simulate(world, 480);
                Require(game.GetSnapshot().GameOver, "Resting overflow never ended game");
            }
        }

        private static void Stress()
        {
            for (uint seed = 1; seed <= 3; seed++)
            {
                var game = new WatermelonGame(seed);
                var random = new System.Random((int)seed);
                using (var world = new WatermelonPhysicsWorld(game))
                {
                    for (int i = 0; i < 6000 && !game.GetSnapshot().GameOver; i++)
                    {
                        if (i % 30 == 0) world.TryDrop((float)random.NextDouble() * 6);
                        world.Step(WatermelonPhysicsWorld.FixedStep);
                        string reason;
                        Require(game.GetSnapshot().TryValidate(out reason), "Stress invalid: " + reason);
                    }
                }
            }
        }
    }
}
