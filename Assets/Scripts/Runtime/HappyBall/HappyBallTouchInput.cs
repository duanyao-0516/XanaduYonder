using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.HappyBall
{
    /// <summary>Unity contact collection; the pure gesture owns frame arbitration.</summary>
    public sealed class HappyBallTouchInput : MonoBehaviour
    {
        public Func<bool> AcceptsInput;
        public Action<double> Rotate;
        public Action Canceled;
        private readonly HappyBallDragGesture gesture = new HappyBallDragGesture();
        private readonly List<HappyBallPointer> pointers = new List<HappyBallPointer>();
        private readonly List<RaycastResult> raycasts = new List<RaycastResult>();
        private readonly Vector3[] corners = new Vector3[4];
        private RectTransform playArea;
        private Graphic playGraphic;
        private bool focused = true;
        private bool paused;
        private bool suppressMouseUntilRelease;

        public void Configure(RectTransform area, Graphic graphic)
        {
            playArea = area;
            playGraphic = graphic;
            Cancel();
        }

        public void Cancel()
        {
            gesture.Cancel();
            suppressMouseUntilRelease = true;
            Canceled?.Invoke();
        }

        private void Update()
        {
            pointers.Clear();
            int count = Input.touchCount;
            if (count > 0)
            {
                // Includes release frames: a synthetic mouse UP must never duplicate touch UP.
                suppressMouseUntilRelease = true;
                for (int i = 0; i < count; i++)
                {
                    var touch = Input.GetTouch(i);
                    pointers.Add(Pointer(touch.fingerId, touch.position, Phase(touch.phase)));
                }
            }
            else if (suppressMouseUntilRelease)
            {
                if (!Input.GetMouseButton(0)) suppressMouseUntilRelease = false;
                else
                {
                    // Keep the reducer locked while a mouse button from the old round is held.
                    pointers.Add(Pointer(-1, Input.mousePosition, HappyBallPointerPhase.Stationary));
                }
            }
            else if (Input.GetMouseButtonDown(0) || Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
            {
                HappyBallPointerPhase phase = Input.GetMouseButtonDown(0) ? HappyBallPointerPhase.Began :
                    Input.GetMouseButtonUp(0) ? HappyBallPointerPhase.Ended : HappyBallPointerPhase.Moved;
                pointers.Add(Pointer(-1, Input.mousePosition, phase));
            }

            bool accepts = focused && !paused && playArea != null && playGraphic != null &&
                AcceptsInput != null && AcceptsInput();
            double delta = gesture.ProcessFrame(pointers, accepts, ViewportWidth());
            if (gesture.WasCanceled) Canceled?.Invoke();
            // The page queues this candidate only while its current session is live. Input
            // already advances its baseline, so a core collision never accumulates displacement.
            if (delta != 0 && Rotate != null && AcceptsInput != null && AcceptsInput()) Rotate(delta);
        }

        private HappyBallPointer Pointer(int id, Vector2 position, HappyBallPointerPhase phase)
        {
            bool inside = playArea != null && RectTransformUtility.RectangleContainsScreenPoint(playArea, position, EventCamera());
            return new HappyBallPointer {
                Id = id, X = position.x, Phase = phase, InPlayArea = inside,
                CanBegin = phase == HappyBallPointerPhase.Began && inside &&
                    HappyBallDragGesture.AllowsScreenStart(position.x, Screen.width) && TopHitIsPlayArea(id, position)
            };
        }

        private bool TopHitIsPlayArea(int id, Vector2 position)
        {
            var events = EventSystem.current;
            if (events == null || playGraphic == null) return false;
            raycasts.Clear();
            events.RaycastAll(new PointerEventData(events) { pointerId = id, position = position }, raycasts);
            return raycasts.Count > 0 && raycasts[0].gameObject == playGraphic.gameObject;
        }

        private Camera EventCamera()
        {
            var canvas = playGraphic == null ? null : playGraphic.canvas;
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private double ViewportWidth()
        {
            if (playArea == null) return 0;
            playArea.GetWorldCorners(corners);
            var camera = EventCamera();
            return Math.Abs(RectTransformUtility.WorldToScreenPoint(camera, corners[2]).x -
                RectTransformUtility.WorldToScreenPoint(camera, corners[0]).x);
        }

        private static HappyBallPointerPhase Phase(TouchPhase phase)
        {
            switch (phase)
            {
                case TouchPhase.Began: return HappyBallPointerPhase.Began;
                case TouchPhase.Moved: return HappyBallPointerPhase.Moved;
                case TouchPhase.Stationary: return HappyBallPointerPhase.Stationary;
                case TouchPhase.Ended: return HappyBallPointerPhase.Ended;
                default: return HappyBallPointerPhase.Canceled;
            }
        }
        private void OnDisable() { Cancel(); }
        private void OnApplicationFocus(bool value) { focused = value; if (!value) Cancel(); }
        private void OnApplicationPause(bool value) { paused = value; if (value) Cancel(); }
    }
}
