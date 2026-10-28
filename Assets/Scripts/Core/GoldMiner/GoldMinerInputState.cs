using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.GoldMiner
{
    public enum GoldMinerPointerPhase { Down, Up, Cancel }

    public struct GoldMinerPointerEvent
    {
        public GoldMinerPointerPhase Phase { get; private set; }
        public int PointerId { get; private set; }
        public int PlayerId { get; private set; }
        public bool UiConsumed { get; private set; }
        public bool CanAct { get; private set; }
        public long Generation { get; private set; }

        public GoldMinerPointerEvent(GoldMinerPointerPhase phase, int pointerId, int playerId,
            bool uiConsumed, bool canAct, long generation)
        {
            Phase = phase; PointerId = pointerId; PlayerId = playerId;
            UiConsumed = uiConsumed; CanAct = canAct; Generation = generation;
        }
    }

    public struct GoldMinerInputIntent
    {
        public bool ShouldLaunch { get; private set; }
        public int PlayerId { get; private set; }

        internal GoldMinerInputIntent(int playerId) { ShouldLaunch = true; PlayerId = playerId; }
    }

    public sealed class GoldMinerInputState
    {
        private readonly int playerCount;
        private readonly Dictionary<int, int> pointers = new Dictionary<int, int>();
        private readonly HashSet<int> quarantined = new HashSet<int>();
        private readonly int?[] owners;

        public long Generation { get; private set; }

        public GoldMinerInputState(bool cooperative, long generation)
        {
            if (generation < 0) throw new ArgumentOutOfRangeException("generation");
            playerCount = cooperative ? 2 : 1;
            owners = new int?[playerCount];
            Generation = generation;
        }

        public GoldMinerInputIntent Process(GoldMinerPointerEvent input)
        {
            if (input.Generation != Generation) return default(GoldMinerInputIntent);
            if (input.Phase == GoldMinerPointerPhase.Cancel)
            {
                // Native cancellation invalidates this input surface's gesture ownership.
                // Other held fingers remain consumed until they too finish.
                foreach (int pointerId in pointers.Keys) quarantined.Add(pointerId);
                pointers.Clear();
                Array.Clear(owners, 0, owners.Length);
                quarantined.Remove(input.PointerId);
                return default(GoldMinerInputIntent);
            }
            if (input.Phase == GoldMinerPointerPhase.Up)
            {
                Release(input.PointerId);
                return default(GoldMinerInputIntent);
            }
            if (input.Phase != GoldMinerPointerPhase.Down || quarantined.Contains(input.PointerId)
                || pointers.ContainsKey(input.PointerId))
                return default(GoldMinerInputIntent);

            // Every Down is consumed, including an ignored extra finger, UI, or a closed gate.
            // Such fingers never acquire a player later when another owner releases.
            pointers.Add(input.PointerId, -1);
            if (input.UiConsumed || input.PlayerId < 0 || input.PlayerId >= playerCount)
                return default(GoldMinerInputIntent);
            if (owners[input.PlayerId].HasValue) return default(GoldMinerInputIntent);
            owners[input.PlayerId] = input.PointerId;
            pointers[input.PointerId] = input.PlayerId;
            return input.CanAct ? new GoldMinerInputIntent(input.PlayerId) : default(GoldMinerInputIntent);
        }

        private void Release(int pointerId)
        {
            quarantined.Remove(pointerId);
            int playerId;
            if (!pointers.TryGetValue(pointerId, out playerId)) return;
            pointers.Remove(pointerId);
            if (playerId >= 0 && owners[playerId] == pointerId) owners[playerId] = null;
        }

        /// <summary>Replace ownership with a physical-device snapshot; no held pointer may restart.</summary>
        public void QuarantineActivePointers(IEnumerable<int> activePointerIds)
        {
            if (activePointerIds == null) throw new ArgumentNullException("activePointerIds");
            var active = new HashSet<int>(activePointerIds);
            pointers.Clear();
            Array.Clear(owners, 0, owners.Length);
            quarantined.Clear();
            quarantined.UnionWith(active);
        }

        public void ResetGeneration(long generation, IEnumerable<int> activePointerIds)
        {
            if (generation <= Generation) throw new ArgumentOutOfRangeException("generation");
            QuarantineActivePointers(activePointerIds);
            Generation = generation;
        }

        /// <summary>Consumes UI Down before its own command; UI never owns either mining player.</summary>
        public bool TryConsumeUiDown(int pointerId, long generation, bool canAct)
        {
            if (generation != Generation || quarantined.Contains(pointerId) || pointers.ContainsKey(pointerId))
                return false;
            pointers.Add(pointerId, -1);
            return canAct;
        }
    }
}
