using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace XanaduYonder.Runtime.FlappyBird
{
    // UGUI is the only pointer source on both touch devices and desktop. A held
    // pointer cannot repeat, and another contact cannot take over on release.
    internal sealed class FlappyPointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ICancelHandler
    {
        private readonly HashSet<int> contacts = new HashSet<int>();
        private int? owner;
        internal Func<bool> AcceptsInput;
        internal Action Flap, Canceled;
        internal bool HasOwner { get { return owner.HasValue; } }

        internal bool Press(int pointerId)
        {
            if (!contacts.Add(pointerId)) return false;
            if (contacts.Count != 1 || owner.HasValue || AcceptsInput == null || !AcceptsInput()) return false;
            owner = pointerId;
            Flap?.Invoke();
            return true;
        }

        internal void Release(int pointerId)
        {
            contacts.Remove(pointerId);
            if (owner == pointerId) owner = null;
        }
        internal void Block(int pointerId)
        {
            contacts.Add(pointerId);
            Cancel();
        }

        // Retain live contacts across a modal/layout interruption. Their eventual
        // pointer-up is consumed; neither it nor a second finger restarts play.
        internal void Cancel() { owner = null; Canceled?.Invoke(); }
        internal void ResetContacts() { contacts.Clear(); Cancel(); }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            Press(data.pointerId);
        }
        public void OnPointerUp(PointerEventData data) { Release(data.pointerId); }
        public void OnPointerExit(PointerEventData data) { if (owner == data.pointerId) Cancel(); }
        public void OnCancel(BaseEventData data) { ResetContacts(); }
        private void OnDisable() { ResetContacts(); }
    }
}
