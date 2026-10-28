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
        private sealed class Contact { public TetrisAction Action; public long Order; public bool SoftBlocked; }
        private readonly Dictionary<int, Contact> owners = new Dictionary<int, Contact>();
        private readonly List<TetrisPendingAction> pending = new List<TetrisPendingAction>();
        private readonly HashSet<int> live = new HashSet<int>();
        private readonly List<int> removed = new List<int>();
        private long order;
        private bool suppressed;
        private int? directionOwner;
        private double heldSeconds, nextRepeat = .18;
        public bool SoftDropHeld
        {
            get { foreach (var pair in owners) if (pair.Value.Action == TetrisAction.SoftDrop && !pair.Value.SoftBlocked) return true; return false; }
        }
        public void ProcessFrame(IList<TetrisPointer> contacts, bool accepts, long serial)
        {
            if (contacts == null) throw new ArgumentNullException(nameof(contacts));
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
                if (p.Phase == TetrisPointerPhase.Ended || !p.Inside || (owners.TryGetValue(p.Id, out var existing) && existing.Action != p.Action)) { Remove(p.Id); continue; }
                if (p.Phase != TetrisPointerPhase.Began || p.Action == TetrisAction.None || owners.ContainsKey(p.Id)) continue;
                owners.Add(p.Id, new Contact { Action = p.Action, Order = frameOrder });
                if (p.Action != TetrisAction.SoftDrop && !(newLeft && newRight && (p.Action == TetrisAction.Left || p.Action == TetrisAction.Right)))
                    Queue(p.Id, p.Action, serial);
            }
            RefreshDirection();
        }
        public void Cancel()
        {
            owners.Clear(); pending.Clear(); directionOwner = null; heldSeconds = 0; nextRepeat = .18; suppressed = true;
        }
        public void PieceChanged()
        {
            pending.Clear();
            foreach (var c in owners.Values) if (c.Action == TetrisAction.SoftDrop) c.SoftBlocked = true;
            heldSeconds = 0; nextRepeat = .18;
        }
        public void Tick(long serial)
        {
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
        private void Remove(int id)
        {
            owners.Remove(id);
            pending.RemoveAll(p => p.Owner == id);
        }
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
