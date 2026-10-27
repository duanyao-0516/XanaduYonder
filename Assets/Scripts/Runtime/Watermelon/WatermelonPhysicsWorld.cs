using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using XanaduYonder.Core.Watermelon;
using Object = UnityEngine.Object;

namespace XanaduYonder.Runtime.Watermelon
{
    /// <summary>Owns an isolated, manually stepped 2D physics scene for one game page.</summary>
    public sealed class WatermelonPhysicsWorld : IDisposable
    {
        public const float FixedStep = 1f / 60f;
        private const float WallThickness = .5f;
        private const float SpawnSeparation = .015f;
        private readonly WatermelonGame game;
        private readonly Scene scene;
        private readonly PhysicsScene2D physics;
        private readonly PhysicsMaterial2D material;
        private readonly Dictionary<int, BallBody> bodies = new Dictionary<int, BallBody>();
        private readonly Dictionary<Collider2D, BallBody> colliders = new Dictionary<Collider2D, BallBody>();
        private readonly List<Collider2D> boundaries = new List<Collider2D>();
        private readonly List<WatermelonBallState> motions = new List<WatermelonBallState>();
        private readonly HashSet<ulong> pendingContacts = new HashSet<ulong>();
        private readonly List<ulong> orderedContacts = new List<ulong>();
        private readonly List<WatermelonMergeResult> mergedThisStep = new List<WatermelonMergeResult>();
        private Collider2D floor;
        private float remainder;
        private bool disposed, stepping, dispatching;

        public event Action<WatermelonMergeResult> Merged;

        public WatermelonPhysicsWorld(WatermelonGame game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            this.game = game;
            // Local physics belongs to this Scene; no global Physics2D simulation or gravity setting changes.
            // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.LocalPhysicsMode.html
            scene = SceneManager.CreateScene("WatermelonPhysics-" + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physics = scene.GetPhysicsScene2D();
            try
            {
                if (!physics.IsValid()) throw new InvalidOperationException("Watermelon local physics scene was not created.");
                material = new PhysicsMaterial2D("Watermelon contact") { friction = .2f, bounciness = .1f };
                floor = CreateBoundary("Floor", new Vector2(WatermelonRules.Width * .5f, -WallThickness * .5f),
                    new Vector2(WatermelonRules.Width + WallThickness * 2, WallThickness));
                float wallHeight = WatermelonRules.Height + 4;
                CreateBoundary("Left wall", new Vector2(-WallThickness * .5f, wallHeight * .5f), new Vector2(WallThickness, wallHeight));
                CreateBoundary("Right wall", new Vector2(WatermelonRules.Width + WallThickness * .5f, wallHeight * .5f), new Vector2(WallThickness, wallHeight));
                foreach (var ball in game.GetSnapshot().Balls) CreateBody(ball);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool IsMoving
        {
            get
            {
                if (disposed || game.GameOver) return false;
                foreach (var body in bodies.Values)
                    if (!body.Rigidbody.IsSleeping() || body.Rigidbody.linearVelocity.sqrMagnitude > .0001f
                        || Mathf.Abs(body.Rigidbody.angularVelocity) > .05f) return true;
                return false;
            }
        }

        public bool CanDrop(float x)
        {
            ThrowIfDisposed();
            if (float.IsNaN(x) || float.IsInfinity(x)) return false;
            if (game.GameOver || game.DropCooldown > 0 || game.BallCount >= WatermelonRules.MaxBalls
                || game.NextId == int.MaxValue) return false;
            float radius = WatermelonRules.GetRadius(game.CurrentLevel);
            var position = new Vector2(Mathf.Clamp(x, radius, WatermelonRules.Width - radius), WatermelonRules.SpawnY);
            // Every moving shape is a real circle with its center at Rigidbody.position. Direct circle
            // distance also sees newly created bodies before their first broadphase synchronization.
            foreach (var body in bodies.Values)
            {
                float separation = radius + body.Collider.radius + SpawnSeparation;
                if ((body.Rigidbody.position - position).sqrMagnitude < separation * separation) return false;
            }
            return true;
        }

        public bool TryDrop(float x)
        {
            ThrowIfDisposed();
            if (stepping) throw new InvalidOperationException("Cannot drop while a physics step is in progress.");
            if (!CanDrop(x)) return false;
            WatermelonBallState ball;
            if (!game.TryDrop(x, out ball)) return false;
            CreateBody(ball);
            return true;
        }

        public void Step(float dt)
        {
            ThrowIfDisposed();
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0 || dt > WatermelonRules.MaximumStep)
                throw new ArgumentOutOfRangeException(nameof(dt));
            if (stepping || dispatching) throw new InvalidOperationException("Cannot re-enter a physics step.");
            if (dt == 0 || game.GameOver) return;
            remainder += dt;
            // Fixed steps keep the same physics behavior across render refresh rates.
            // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PhysicsScene2D.Simulate.html
            while (remainder + .000001f >= FixedStep && !disposed)
            {
                remainder = Mathf.Max(0, remainder - FixedStep);
                SimulateOneStep();
                if (disposed || game.GameOver) { remainder = 0; break; }
            }
        }

        public void Capture()
        {
            ThrowIfDisposed();
            if (stepping) throw new InvalidOperationException("Cannot capture during a physics callback.");
            if (!game.GameOver) CaptureMotion();
        }

        internal void RecordContact(int ballId, Collider2D other)
        {
            if (disposed) return;
            BallBody body;
            if (!bodies.TryGetValue(ballId, out body)) return;
            if (other == floor)
            {
                body.Contact.MarkContacted();
                return;
            }
            BallBody partner;
            if (!colliders.TryGetValue(other, out partner) || partner.Id == ballId) return;
            body.Contact.MarkContacted();
            partner.Contact.MarkContacted();
            int first = Math.Min(ballId, partner.Id), second = Math.Max(ballId, partner.Id);
            pendingContacts.Add(((ulong)(uint)first << 32) | (uint)second);
        }

        private void SimulateOneStep()
        {
            stepping = true;
            mergedThisStep.Clear();
            try
            {
                pendingContacts.Clear();
                if (!physics.Simulate(FixedStep)) throw new InvalidOperationException("Watermelon physics simulation did not run.");
                CaptureMotion();
                orderedContacts.Clear();
                orderedContacts.AddRange(pendingContacts);
                orderedContacts.Sort();
                foreach (ulong pair in orderedContacts)
                {
                    int first = (int)(pair >> 32), second = (int)(pair & uint.MaxValue);
                    WatermelonMergeResult result;
                    if (!game.TryMerge(first, second, out result)) continue;
                    RemoveBody(result.FirstId);
                    RemoveBody(result.SecondId);
                    CreateBody(result.NewBall);
                    mergedThisStep.Add(result);
                }
                game.Advance(FixedStep);
            }
            finally { stepping = false; }
            // Listeners only observe a consistent core/body state, and may dispose the page safely.
            dispatching = true;
            try
            {
                foreach (var result in mergedThisStep)
                {
                    if (disposed) break;
                    var handler = Merged;
                    if (handler != null) handler(result);
                }
            }
            finally { dispatching = false; }
        }

        private void CaptureMotion()
        {
            motions.Clear();
            foreach (var body in bodies.Values)
            {
                var motion = body.Motion;
                var position = body.Rigidbody.position;
                var velocity = body.Rigidbody.linearVelocity;
                motion.X = position.x; motion.Y = position.y;
                motion.VelocityX = velocity.x; motion.VelocityY = velocity.y;
                motion.Angle = body.Rigidbody.rotation; motion.AngularVelocity = body.Rigidbody.angularVelocity;
                motion.Contacted = body.Contact.HasContacted;
                motions.Add(motion);
            }
            if (!game.UpdateMotion(motions))
                throw new InvalidOperationException("Watermelon physics produced an invalid or incomplete motion batch.");
        }

        private Collider2D CreateBoundary(string name, Vector2 position, Vector2 size)
        {
            var obj = NewObject(name);
            obj.transform.position = position;
            var rigidbody = obj.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = RigidbodyType2D.Static;
            rigidbody.position = position;
            var collider = obj.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.sharedMaterial = material;
            boundaries.Add(collider);
            return collider;
        }

        private void CreateBody(WatermelonBallState ball)
        {
            if (bodies.ContainsKey(ball.Id)) throw new InvalidOperationException("Duplicate watermelon physics body id.");
            var obj = NewObject("Ball " + ball.Id);
            var position = new Vector2(ball.X, ball.Y);
            // Re-enabling simulation rebuilds the body's shapes from its Transform. Seed both
            // representations so a restored or newly dropped ball cannot be recreated at (0, 0).
            obj.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 0, ball.Angle));
            var rigidbody = obj.AddComponent<Rigidbody2D>();
            rigidbody.simulated = false;
            rigidbody.bodyType = RigidbodyType2D.Dynamic;
            rigidbody.useAutoMass = true;
            rigidbody.gravityScale = 1;
            rigidbody.linearDamping = .08f;
            rigidbody.angularDamping = .12f;
            rigidbody.interpolation = RigidbodyInterpolation2D.None;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rigidbody.sleepMode = RigidbodySleepMode2D.StartAwake;
            var collider = obj.AddComponent<CircleCollider2D>();
            collider.radius = WatermelonRules.GetRadius(ball.Level);
            collider.density = 1;
            collider.sharedMaterial = material;
            var contact = obj.AddComponent<WatermelonContact>();
            contact.Initialize(this, ball.Id, ball.Contacted);
            var body = new BallBody { Id = ball.Id, Rigidbody = rigidbody, Collider = collider, Contact = contact, Motion = ball.Copy() };
            bodies.Add(ball.Id, body);
            colliders.Add(collider, body);
            rigidbody.simulated = true;
            rigidbody.position = position;
            rigidbody.rotation = ball.Angle;
            rigidbody.linearVelocity = new Vector2(ball.VelocityX, ball.VelocityY);
            rigidbody.angularVelocity = ball.AngularVelocity;
        }

        private GameObject NewObject(string name)
        {
            var obj = new GameObject(name);
            // Move the root before adding colliders or bodies so they only belong to our local scene.
            SceneManager.MoveGameObjectToScene(obj, scene);
            return obj;
        }

        private void RemoveBody(int id)
        {
            BallBody body;
            if (!bodies.TryGetValue(id, out body)) throw new InvalidOperationException("Merged watermelon physics body was missing.");
            bodies.Remove(id);
            colliders.Remove(body.Collider);
            DeactivateAndDestroy(body);
        }

        private static void DeactivateAndDestroy(BallBody body)
        {
            body.Contact.Detach();
            body.Collider.enabled = false;
            body.Collider.sharedMaterial = null;
            // Destroy is delayed until frame end; simulated=false removes ghost contacts immediately.
            // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rigidbody2D-simulated.html
            body.Rigidbody.simulated = false;
            Object.Destroy(body.Rigidbody.gameObject);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Merged = null;
            foreach (var body in bodies.Values) DeactivateAndDestroy(body);
            bodies.Clear(); colliders.Clear(); pendingContacts.Clear(); orderedContacts.Clear(); motions.Clear();
            foreach (var collider in boundaries)
            {
                if (collider == null) continue;
                collider.enabled = false;
                collider.sharedMaterial = null;
                if (collider.attachedRigidbody != null) collider.attachedRigidbody.simulated = false;
                Object.Destroy(collider.gameObject);
            }
            boundaries.Clear();
            if (material != null) Object.Destroy(material);
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        }

        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(WatermelonPhysicsWorld)); }

        private sealed class BallBody
        {
            public int Id;
            public Rigidbody2D Rigidbody;
            public CircleCollider2D Collider;
            public WatermelonContact Contact;
            public WatermelonBallState Motion;
        }
    }
}
