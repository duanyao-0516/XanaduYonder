using System;
using UnityEngine;

namespace XanaduYonder.Runtime.GoldMiner
{
    /// <summary>One page owns this scope and calls Tick once per frame, then Dispose on every exit.</summary>
    internal sealed class GoldMinerOrientationScope : IDisposable
    {
        private readonly ScreenOrientation priorOrientation;
        private readonly bool priorPortrait;
        private readonly bool priorPortraitUpsideDown;
        private readonly bool priorLandscapeLeft;
        private readonly bool priorLandscapeRight;
        private readonly bool priorFullScreen;
        private bool disposed;
        private bool landscape;
        private int stableFrames;
        private int previousWidth;
        private int previousHeight;
        private Rect previousSafeArea;
        private int lastObservedFrame = -1;
        private double startedAt;
        private Action<long> ready;
        private Action<long> failed;

        public long Generation { get; private set; }
        public bool IsPending { get; private set; }

        public GoldMinerOrientationScope()
        {
            priorOrientation = Screen.orientation;
            priorPortrait = Screen.autorotateToPortrait;
            priorPortraitUpsideDown = Screen.autorotateToPortraitUpsideDown;
            priorLandscapeLeft = Screen.autorotateToLandscapeLeft;
            priorLandscapeRight = Screen.autorotateToLandscapeRight;
            priorFullScreen = Screen.fullScreen;
        }

        /// <summary>Replaces any earlier request; ready/failure return only this request's generation.</summary>
        public long Request(bool landscape, Action<long> ready, Action<long> failed)
        {
            if (disposed) throw new ObjectDisposedException("GoldMinerOrientationScope");
            if (ready == null) throw new ArgumentNullException("ready");
            if (failed == null) throw new ArgumentNullException("failed");
            Generation = checked(Generation + 1);
            this.landscape = landscape;
            this.ready = ready;
            this.failed = failed;
            stableFrames = 0;
            lastObservedFrame = -1;
            startedAt = Time.realtimeSinceStartupAsDouble;
            IsPending = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.fullScreen = true;
            Screen.orientation = landscape ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
            return Generation;
        }

        public void Tick()
        {
            if (disposed || !IsPending || lastObservedFrame == Time.frameCount) return;
            lastObservedFrame = Time.frameCount;
            int width = Screen.width;
            int height = Screen.height;
            Rect safeArea = Screen.safeArea;
            bool targetShape = width > 0 && height > 0 && (landscape ? width > height : height > width);
            bool validSafeArea = Finite(safeArea.x) && Finite(safeArea.y) && Finite(safeArea.width) && Finite(safeArea.height)
                && safeArea.width > 0 && safeArea.height > 0 && safeArea.xMin >= 0 && safeArea.yMin >= 0
                && safeArea.xMax <= width && safeArea.yMax <= height;
            if (!targetShape || !validSafeArea) stableFrames = 0;
            else if (stableFrames > 0 && width == previousWidth && height == previousHeight && safeArea == previousSafeArea)
                stableFrames++;
            else stableFrames = 1;
            previousWidth = width;
            previousHeight = height;
            previousSafeArea = safeArea;
            if (stableFrames >= 3) Complete(true);
            else if (Time.realtimeSinceStartupAsDouble - startedAt >= 5.0) Complete(false);
        }

        private void Complete(bool succeeded)
        {
            long completedGeneration = Generation;
            Action<long> callback = succeeded ? ready : failed;
            // Clear before invoking: the page may synchronously request a different mode or dispose.
            IsPending = false;
            ready = null;
            failed = null;
            callback(completedGeneration);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            IsPending = false;
            ready = null;
            failed = null;
            Screen.autorotateToPortrait = priorPortrait;
            Screen.autorotateToPortraitUpsideDown = priorPortraitUpsideDown;
            Screen.autorotateToLandscapeLeft = priorLandscapeLeft;
            Screen.autorotateToLandscapeRight = priorLandscapeRight;
            Screen.orientation = priorOrientation;
            Screen.fullScreen = priorFullScreen;
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
