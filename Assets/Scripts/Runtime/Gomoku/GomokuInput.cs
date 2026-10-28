using System;
using System.Collections.Generic;

namespace XanaduYonder.Runtime.Gomoku
{
    internal enum GomokuInputPhase { Began, Moved, Stationary, Ended, Canceled }

    internal struct GomokuContact
    {
        public readonly int Id;
        public readonly double X, Y;
        public readonly GomokuInputPhase Phase;
        public readonly bool BeganOnBoard;
        public GomokuContact(int id, double x, double y, GomokuInputPhase phase, bool beganOnBoard)
        { Id = id; X = x; Y = y; Phase = phase; BeganOnBoard = beganOnBoard; }
    }

    internal sealed class GomokuInput
    {
        private const double DragDistanceSquared = 12 * 12;
        private bool tracking, blocked;
        private int pointerId, cell = -1;
        private double startX, startY;

        // The whole frame includes contacts outside the board and those ending now.
        // An UP plus another DOWN therefore cannot depend on UGUI callback order.
        public int ProcessFrame(bool interactable, IReadOnlyList<GomokuContact> contacts)
        {
            if (contacts == null) throw new ArgumentNullException("contacts");
            bool allReleased = true, canceled = false;
            for (int i = 0; i < contacts.Count; i++)
            {
                var phase = contacts[i].Phase;
                if (phase != GomokuInputPhase.Ended && phase != GomokuInputPhase.Canceled) allReleased = false;
                if (phase == GomokuInputPhase.Canceled) canceled = true;
            }
            if (!interactable || contacts.Count > 1 || canceled) Cancel();
            if (blocked)
            {
                if (allReleased) blocked = false;
                return -1;
            }
            if (contacts.Count == 0) { tracking = false; cell = -1; return -1; }

            var contact = contacts[0];
            int hit = GomokuBoardGeometry.HitTest(contact.X, contact.Y);
            if (!tracking)
            {
                if (contact.Phase != GomokuInputPhase.Began || !contact.BeganOnBoard || hit < 0)
                    return Reject(allReleased);
                tracking = true; pointerId = contact.Id; cell = hit;
                startX = contact.X; startY = contact.Y;
                return -1;
            }
            double dx = contact.X - startX, dy = contact.Y - startY;
            if (contact.Id != pointerId || contact.Phase == GomokuInputPhase.Began || hit != cell ||
                dx * dx + dy * dy > DragDistanceSquared) return Reject(allReleased);
            if (contact.Phase != GomokuInputPhase.Ended) return -1;
            int selected = cell;
            tracking = false; cell = -1;
            return selected;
        }

        public void Cancel() { tracking = false; cell = -1; blocked = true; }

        private int Reject(bool allReleased)
        { Cancel(); if (allReleased) blocked = false; return -1; }
    }
}
