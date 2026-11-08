using System;
using System.Collections.Generic;
namespace XanaduYonder.Runtime.Tetris
{
    public enum TetrisAction { None, Left, Right, SoftDrop, Clockwise, CounterClockwise, HardDrop, Hold }
    public enum TetrisPointerPhase { Began, Moved, Stationary, Ended, Canceled }
    public struct TetrisPointer
    {
        public int Id; public TetrisAction Action; public TetrisPointerPhase Phase; public bool Inside;
        public TetrisPointer(int id, TetrisAction action, TetrisPointerPhase phase, bool inside)
        { Id = id; Action = action; Phase = phase; Inside = inside; }
    }
    public struct TetrisPendingAction
    {
        public int Owner; public TetrisAction Action; public long PieceSerial;
    }

    // Complete contact frames are reduced before any command executes. A third finger can
    // therefore cancel the entire frame, including a hard drop received earlier in it.
    public sealed class TetrisInput
    {
        private sealed class Contact
        {
            public TetrisAction Action; public long Order, PieceSerial;
            public bool SoftBlocked, TapEligible = true;
            public double StartedAt;
        }
        private const double MaximumTapSeconds = .18, MaximumTapGapSeconds = .30, MaximumTripleSeconds = .65;
        private readonly Dictionary<int, Contact> owners = new Dictionary<int, Contact>();
        private readonly List<TetrisPendingAction> pending = new List<TetrisPendingAction>();
        private readonly HashSet<int> live = new HashSet<int>();
        private readonly List<int> removed = new List<int>();
        private long order;
        private bool suppressed;
        private int? directionOwner;
        private double heldSeconds, nextRepeat = .18;
        private double clockSeconds, firstTapStartedAt, lastTapEndedAt;
        private int downTaps;
        private long tapPieceSerial;
        public bool SoftDropHeld
        {
            get { foreach (var pair in owners) if (pair.Value.Action == TetrisAction.SoftDrop && !pair.Value.SoftBlocked) return true; return false; }
        }
        public void ProcessFrame(IList<TetrisPointer> contacts, bool accepts, long serial)
        { ProcessFrame(contacts, accepts, serial, clockSeconds); }
        public void ProcessFrame(IList<TetrisPointer> contacts, bool accepts, long serial, double time)
        {
            if (contacts == null) throw new ArgumentNullException(nameof(contacts));
            if (double.IsNaN(time) || double.IsInfinity(time) || time < 0) throw new ArgumentOutOfRangeException(nameof(time));
            if (downTaps > 0 && (tapPieceSerial != serial || time - firstTapStartedAt > MaximumTripleSeconds)) ClearDownTaps();
            live.Clear();
            int active = 0;
            bool invalid = false;
            foreach (var p in contacts)
            {
                if (!live.Add(p.Id) || p.Phase == TetrisPointerPhase.Canceled) invalid = true;
                if (p.Phase != TetrisPointerPhase.Ended && p.Phase != TetrisPointerPhase.Canceled) active++;
            }
            if (!accepts || invalid || active > 2) { Cancel(); if (active == 0) suppressed = false; return; }
            if (suppressed) { if (active == 0) suppressed = false; return; }
            // A second contact invalidates the tap gesture before any release can finish it.
            // Ordinary two-finger movement, rotation and held soft drop still run below.
            bool multipleContacts = live.Count > 1;
            if (multipleContacts)
            {
                ClearDownTaps();
                foreach (var c in owners.Values) if (c.Action == TetrisAction.SoftDrop) c.TapEligible = false;
            }
            removed.Clear();
            foreach (var pair in owners) if (!live.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var id in removed) Remove(id);
            bool newLeft = false, newRight = false;
            foreach (var p in contacts)
            {
                if (p.Phase != TetrisPointerPhase.Began || !p.Inside || owners.ContainsKey(p.Id)) continue;
                newLeft |= p.Action == TetrisAction.Left; newRight |= p.Action == TetrisAction.Right;
            }
            long frameOrder = ++order;
            foreach (var p in contacts)
            {
                owners.TryGetValue(p.Id, out var existing);
                if (p.Phase == TetrisPointerPhase.Ended)
                {
                    bool cleanDown = existing != null && existing.Action == TetrisAction.SoftDrop && p.Action == existing.Action && p.Inside;
                    Remove(p.Id, !cleanDown);
                    if (cleanDown) CompleteDownTap(p.Id, existing, serial, time);
                    continue;
                }
                if (!p.Inside || (existing != null && existing.Action != p.Action)) { Remove(p.Id); continue; }
                if (existing != null && existing.Action == TetrisAction.SoftDrop && time - existing.StartedAt > MaximumTapSeconds)
                { existing.TapEligible = false; ClearDownTaps(); }
                if (p.Phase != TetrisPointerPhase.Began || p.Action == TetrisAction.None || owners.ContainsKey(p.Id)) continue;
                owners.Add(p.Id, new Contact { Action = p.Action, Order = frameOrder, StartedAt = time, PieceSerial = serial, TapEligible = !multipleContacts });
                if (p.Action != TetrisAction.SoftDrop && !(newLeft && newRight && (p.Action == TetrisAction.Left || p.Action == TetrisAction.Right)))
                    Queue(p.Id, p.Action, serial);
            }
            RefreshDirection();
        }
        public void Cancel()
        {
            owners.Clear(); pending.Clear(); directionOwner = null; heldSeconds = 0; nextRepeat = .18; suppressed = true;
            ClearDownTaps();
        }
        public void PieceChanged()
        {
            pending.Clear();
            ClearDownTaps();
            foreach (var c in owners.Values) if (c.Action == TetrisAction.SoftDrop) c.SoftBlocked = true;
            heldSeconds = 0; nextRepeat = .18;
        }
        public void Tick(long serial)
        {
            clockSeconds += 1d / 60;
            if (!directionOwner.HasValue) return;
            heldSeconds += 1d / 60;
            if (heldSeconds + 1e-9 < nextRepeat) return;
            nextRepeat += .06;
            Queue(directionOwner.Value, owners[directionOwner.Value].Action, serial);
        }
        public bool TryTake(out TetrisPendingAction action)
        {
            action = default(TetrisPendingAction);
            if (pending.Count == 0) return false;
            action = pending[0]; pending.RemoveAt(0); return true;
        }
        private void Queue(int id, TetrisAction action, long serial)
        { pending.Add(new TetrisPendingAction { Owner = id, Action = action, PieceSerial = serial }); }
        private void Remove(int id, bool cancelTap = true)
        {
            if (cancelTap && owners.TryGetValue(id, out var contact) && contact.Action == TetrisAction.SoftDrop) ClearDownTaps();
            owners.Remove(id);
            pending.RemoveAll(p => p.Owner == id);
        }
        private void CompleteDownTap(int id, Contact contact, long serial, double time)
        {
            double duration = time - contact.StartedAt;
            if (!contact.TapEligible || contact.SoftBlocked || contact.PieceSerial != serial || duration < 0 || duration > MaximumTapSeconds)
            { ClearDownTaps(); return; }
            if (downTaps == 0 || tapPieceSerial != serial || contact.StartedAt - lastTapEndedAt > MaximumTapGapSeconds || time - firstTapStartedAt > MaximumTripleSeconds)
            { downTaps = 0; firstTapStartedAt = contact.StartedAt; tapPieceSerial = serial; }
            downTaps++; lastTapEndedAt = time;
            if (downTaps != 3) return;
            ClearDownTaps();
            Queue(id, TetrisAction.HardDrop, serial);
        }
        private void ClearDownTaps() { downTaps = 0; tapPieceSerial = 0; firstTapStartedAt = lastTapEndedAt = 0; }
        private void RefreshDirection()
        {
            int? next = null; long newest = -1;
            TetrisAction newestAction = TetrisAction.None;
            bool tied = false;
            foreach (var p in owners)
            {
                if (p.Value.Action != TetrisAction.Left && p.Value.Action != TetrisAction.Right) continue;
                if (p.Value.Order > newest)
                { newest = p.Value.Order; newestAction = p.Value.Action; next = p.Key; tied = false; }
                else if (p.Value.Order == newest && p.Value.Action != newestAction) tied = true;
            }
            if (tied) next = null;
            if (next == directionOwner) return;
            directionOwner = next; heldSeconds = 0; nextRepeat = .18;
        }
    }
}
