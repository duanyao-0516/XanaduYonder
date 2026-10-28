using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
namespace XanaduYonder.Runtime.Tetris
{
    // Raw input is collected as one frame so all fingers participate in arbitration.
    // UGUI raycasts decide where a press may begin; sliding onto a button never begins it.
    public sealed class TetrisTouchInput : MonoBehaviour
    {
        public Func<bool> AcceptsInput;
        public Func<TetrisSession> Session;
        private Dictionary<TetrisAction, RectTransform> targets;
        private readonly List<TetrisPointer> contacts = new List<TetrisPointer>();
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private bool waitMouseRelease;
        private static readonly KeyCode[] Keys = { KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.DownArrow, KeyCode.Z, KeyCode.X, KeyCode.Space, KeyCode.C };
        private static readonly TetrisAction[] Actions = { TetrisAction.Left, TetrisAction.Right, TetrisAction.SoftDrop, TetrisAction.CounterClockwise, TetrisAction.Clockwise, TetrisAction.HardDrop, TetrisAction.Hold };
        public void Configure(Dictionary<TetrisAction, RectTransform> actionTargets) { targets = actionTargets; Cancel(); }
        public void Cancel() { Session?.Invoke()?.Input.Cancel(); waitMouseRelease = true; }
        private void Update()
        {
            var session = Session?.Invoke();
            if (session == null || targets == null) return;
            contacts.Clear();
            if (Input.touchCount > 0)
            {
                waitMouseRelease = true;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    contacts.Add(Pointer(t.fingerId, t.position, Phase(t.phase)));
                }
            }
            else
            {
                if (waitMouseRelease)
                {
                    if (!Input.GetMouseButton(0)) waitMouseRelease = false;
                    else contacts.Add(new TetrisPointer(-1, TetrisAction.None, TetrisPointerPhase.Stationary, false));
                }
                else if (Input.GetMouseButtonDown(0) || Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
                    contacts.Add(Pointer(-1, Input.mousePosition, Input.GetMouseButtonDown(0) ? TetrisPointerPhase.Began : Input.GetMouseButtonUp(0) ? TetrisPointerPhase.Ended : TetrisPointerPhase.Stationary));
                for (int i = 0; i < Keys.Length; i++)
                    if (Input.GetKey(Keys[i]) || Input.GetKeyUp(Keys[i]))
                        contacts.Add(new TetrisPointer(-10 - i, Actions[i], Input.GetKeyDown(Keys[i]) ? TetrisPointerPhase.Began : Input.GetKeyUp(Keys[i]) ? TetrisPointerPhase.Ended : TetrisPointerPhase.Stationary, true));
            }
            session.Input.ProcessFrame(contacts, AcceptsInput != null && AcceptsInput(), session.Snapshot.PieceSerial);
        }
        private TetrisPointer Pointer(int id, Vector2 position, TetrisPointerPhase phase)
        {
            TetrisAction action = TetrisAction.None;
            var events = EventSystem.current;
            if (events != null)
            {
                hits.Clear(); events.RaycastAll(new PointerEventData(events) { pointerId = id, position = position }, hits);
                if (hits.Count > 0)
                {
                    var hit = hits[0].gameObject.transform;
                    foreach (var pair in targets)
                        if (hit == pair.Value || hit.IsChildOf(pair.Value)) { action = pair.Key; break; }
                }
            }
            return new TetrisPointer(id, action, phase, action != TetrisAction.None);
        }
        private static TetrisPointerPhase Phase(TouchPhase phase)
        {
            switch (phase)
            {
                case TouchPhase.Began: return TetrisPointerPhase.Began;
                case TouchPhase.Moved: return TetrisPointerPhase.Moved;
                case TouchPhase.Stationary: return TetrisPointerPhase.Stationary;
                case TouchPhase.Ended: return TetrisPointerPhase.Ended;
                default: return TetrisPointerPhase.Canceled;
            }
        }
        private void OnDisable() { Cancel(); }
        private void OnApplicationPause(bool paused) { if (paused) Cancel(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
    }
}
