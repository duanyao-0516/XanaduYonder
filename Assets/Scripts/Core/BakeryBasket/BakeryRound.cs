using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.BakeryBasket
{
    public enum BakeryPhase { Playing, Full, Cleared }

    public sealed class BakeryRound
    {
        public const int KindCount = 15;
        public const int PieceCount = 180;
        public const int SlotCount = 7;
        public const int ReserveSlotCount = 6;
        public const int MaxMoveOutCount = 3;
        private static readonly int[] Counts = { 12, 12, 15, 15, 9, 12, 12, 15, 15, 9, 12, 9, 12, 12, 9 };
        private static readonly int[] NoMatch = Array.Empty<int>();
        private readonly int[] kinds = new int[PieceCount];
        private readonly bool[] taken = new bool[PieceCount];
        private readonly List<int> tray = new List<int>(SlotCount);
        private readonly List<int> reserve = new List<int>(ReserveSlotCount);
        public IReadOnlyList<int> Tray { get; }
        public IReadOnlyList<int> Reserve { get; }
        public int Remaining { get; private set; } = PieceCount;
        public BakeryPhase Phase { get; private set; }
        public int MoveOutCount => Math.Min(MaxMoveOutCount, Math.Min(tray.Count, ReserveSlotCount - reserve.Count));
        public bool CanMoveOut => MoveOutCount > 0;

        public BakeryRound(int seed)
        {
            Tray = tray.AsReadOnly();
            Reserve = reserve.AsReadOnly();
            int next = 0;
            for (int kind = 0; kind < Counts.Length; kind++)
            {
                Array.Fill(kinds, kind, next, Counts[kind]);
                next += Counts[kind];
            }
            var random = new Random(seed);
            for (int i = PieceCount - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int kind = kinds[i];
                kinds[i] = kinds[j];
                kinds[j] = kind;
            }
        }

        public int KindOf(int id) => kinds[id];

        // A null return means the requested piece could not be taken.
        public int[] Take(int id)
        {
            if (Phase != BakeryPhase.Playing || id < 0 || id >= PieceCount || taken[id]) return null;
            taken[id] = true;
            Remaining--;
            return AddToTray(id);
        }

        public bool MoveOut()
        {
            int count = MoveOutCount;
            if (count == 0) return false;
            for (int i = 0; i < count; i++) reserve.Add(tray[i]);
            tray.RemoveRange(0, count);
            UpdatePhase();
            return true;
        }

        // A reserve piece is already taken from the basket, so returning it never changes Remaining.
        public int[] ReturnFromReserve(int slot)
        {
            if (Phase != BakeryPhase.Playing || slot < 0 || slot >= reserve.Count) return null;
            int id = reserve[slot];
            reserve.RemoveAt(slot);
            return AddToTray(id);
        }

        private int[] AddToTray(int id)
        {
            tray.Add(id);

            int kind = KindOf(id);
            int count = 0;
            foreach (int slot in tray)
                if (KindOf(slot) == kind) count++;
            int[] match = NoMatch;
            if (count == 3)
            {
                match = new int[3];
                int matchIndex = 0;
                foreach (int slot in tray)
                    if (KindOf(slot) == kind) match[matchIndex++] = slot;
                tray.RemoveAll(slot => KindOf(slot) == kind);
            }

            UpdatePhase();
            return match;
        }

        private void UpdatePhase()
        {
            if (Remaining == 0 && tray.Count == 0 && reserve.Count == 0) Phase = BakeryPhase.Cleared;
            else Phase = tray.Count == SlotCount ? BakeryPhase.Full : BakeryPhase.Playing;
        }
    }
}
