using UnityEngine;

namespace XanaduYonder.Runtime.Watermelon
{
    // Physics callbacks only record contacts. The owning world performs merges after Simulate.
    internal sealed class WatermelonContact : MonoBehaviour
    {
        private WatermelonPhysicsWorld owner;
        private int ballId;
        internal bool HasContacted { get; private set; }

        internal void Initialize(WatermelonPhysicsWorld world, int id, bool contacted)
        {
            owner = world;
            ballId = id;
            HasContacted = contacted;
        }

        internal void MarkContacted() { HasContacted = true; }
        internal void Detach() { owner = null; }

        private void OnCollisionEnter2D(Collision2D collision) { Record(collision); }
        private void OnCollisionStay2D(Collision2D collision) { Record(collision); }

        private void Record(Collision2D collision)
        {
            if (owner == null || collision == null) return;
            var other = collision.collider;
            if (other != null && other.gameObject == gameObject) other = collision.otherCollider;
            if (other != null) owner.RecordContact(ballId, other);
        }
    }
}
