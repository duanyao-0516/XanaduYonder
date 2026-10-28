using System;
using System.Collections.Generic;

namespace XanaduYonder.Runtime.HappyBall
{
    public enum HappyBallPointerPhase { Began, Moved, Stationary, Ended, Canceled }
    public struct HappyBallPointer
    {
        public int Id;
        public double X;
        public HappyBallPointerPhase Phase;
        public bool InPlayArea;
        public bool CanBegin;
    }
    public sealed class HappyBallDragGesture
    {
        private bool dragging;
        private bool locked;
        private int owner;
        private double lastX;
        public bool IsDragging { get { return dragging; } }
        public bool IsLocked { get { return locked; } }
        public bool WasCanceled { get; private set; }
        /// <summary>Do not acquire a system-edge swipe; an existing drag keeps its ownership.</summary>
        public static bool AllowsScreenStart(double x, double screenWidth)
        {
            if (!Finite(x) || !Finite(screenWidth) || screenWidth <= 0) return false;
            double edge = screenWidth * 16 / 720;
            return x >= edge && x <= screenWidth - edge;
        }

        /// <summary>Old contacts must all release before a fresh down may acquire ownership.</summary>
        public void Cancel() { dragging = false; locked = true; WasCanceled = true; }

        /// <summary>
        /// Submit the entire frame, including Ended/Canceled contacts. Cancellation wins over
        /// final UP displacement, independently of enumeration order. Every emitted delta is
        /// consumed here even if the simulation later rejects rotation at a wall.
        /// </summary>
        public double ProcessFrame(IList<HappyBallPointer> pointers, bool acceptsInput, double viewportWidth)
        {
            if (pointers == null) throw new ArgumentNullException(nameof(pointers));
            WasCanceled = false;
            bool live = HasLivePointers(pointers);
            if (locked) { if (!live) locked = false; return 0; }
            if (!acceptsInput || !Finite(viewportWidth) || viewportWidth <= 0 || pointers.Count > 1)
                return Reject(live);
            if (pointers.Count == 0) { WasCanceled = dragging; dragging = false; return 0; }
            var pointer = pointers[0];
            if (!Finite(pointer.X) || !pointer.InPlayArea || pointer.Phase == HappyBallPointerPhase.Canceled ||
                !Enum.IsDefined(typeof(HappyBallPointerPhase), pointer.Phase)) return Reject(live);
            if (!dragging)
            {
                if (pointer.Phase != HappyBallPointerPhase.Began || !pointer.CanBegin) return Reject(live);
                owner = pointer.Id; lastX = pointer.X; dragging = true; return 0;
            }
            if (pointer.Id != owner || pointer.Phase == HappyBallPointerPhase.Began) return Reject(live);
            double delta = (pointer.X - lastX) / viewportWidth * Math.PI * 1.5;
            if (!Finite(delta)) return Reject(live);
            lastX = pointer.X;
            if (pointer.Phase == HappyBallPointerPhase.Ended) dragging = false;
            return delta;
        }

        private double Reject(bool live) { dragging = false; locked = live; WasCanceled = true; return 0; }
        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        private static bool HasLivePointers(IList<HappyBallPointer> pointers)
        {
            for (int i = 0; i < pointers.Count; i++)
                if (pointers[i].Phase != HappyBallPointerPhase.Ended && pointers[i].Phase != HappyBallPointerPhase.Canceled) return true;
            return false;
        }
    }
}
