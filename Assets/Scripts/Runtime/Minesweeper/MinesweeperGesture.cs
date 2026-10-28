using System;
using System.Collections.Generic;

namespace XanaduYonder.Runtime.Minesweeper
{
    /// <summary>
    /// Owns one entire pointer round. Each frame is validated before any click or transform;
    /// callers must submit all contacts, including Ended/Canceled, before dropping ended contacts.
    /// </summary>
    public sealed class MinesweeperGesture
    {
        public const double MovementThreshold = 12;
        public const long HoldMilliseconds = 400;
        private enum GestureState { Idle, Single, Pair, PanOnly }
        private readonly MinesweeperViewport viewport;
        private readonly Dictionary<int, MinesweeperInputContact> tracked = new Dictionary<int, MinesweeperInputContact>();
        private GestureState state;
        private bool locked;
        private bool cancelCompletion;
        private bool clickEligible;
        private bool held;
        private bool secondaryButton;
        private bool initialOverview;
        private bool initialFlagMode;
        private string initialGameId;
        private int initialCell = -1;
        private MinesweeperInputPosition downPosition;
        private MinesweeperInputPosition lastSinglePosition;
        private MinesweeperInputPosition lastMidpoint;
        private double lastDistance;
        private long downMilliseconds;
        private long lastMilliseconds = -1;
        public bool IsLocked { get { return locked; } }

        public MinesweeperGesture(MinesweeperViewport viewport)
        { this.viewport = viewport ?? throw new ArgumentNullException("viewport"); }

        /// <summary>Invalidate current input; even after re-enable an all-up snapshot must be observed.</summary>
        public void Cancel()
        {
            cancelCompletion |= state != GestureState.Idle;
            locked = true;
            ClearRound();
        }

        public MinesweeperGestureResult ProcessFrame(MinesweeperInputFrame frame, Func<int, bool> canToggleFlag)
        {
            if (frame == null) throw new ArgumentNullException("frame");
            if (canToggleFlag == null) throw new ArgumentNullException("canToggleFlag");
            bool completed = cancelCompletion;
            cancelCompletion = false;
            if (locked)
            {
                if (!HasLiveContacts(frame.Contacts)) locked = false;
                return Result(frame, MinesweeperGestureAction.None, -1, -1, false, completed);
            }

            // Validate the complete snapshot before a hold or UP can commit. Multi-contact
            // joins and canceled/third/outside contacts always win over the hold threshold.
            if (!Validate(frame))
            {
                completed |= state != GestureState.Idle;
                ClearRound();
                locked = HasLiveContacts(frame.Contacts);
                lastMilliseconds = frame.TimeMilliseconds;
                return Result(frame, MinesweeperGestureAction.None, -1, -1, false, completed);
            }
            lastMilliseconds = frame.TimeMilliseconds;
            if (frame.Contacts.Count == 0)
                return Result(frame, MinesweeperGestureAction.None, -1, -1, false, completed);

            double priorScale = viewport.ZoomScale;
            double priorColumn = viewport.CenterColumn;
            double priorRow = viewport.CenterRow;
            bool priorOverview = viewport.IsOverview;
            MinesweeperGestureAction action = MinesweeperGestureAction.None;
            int actionCell = -1;
            int previewCell = -1;

            if (state == GestureState.Idle) StartRound(frame);
            if (frame.Contacts.Count == 2)
            {
                clickEligible = false;
                if (initialOverview)
                {
                    // Overview never transforms and a multi-contact round never locates.
                    locked = HasLiveContacts(frame.Contacts);
                    completed = true;
                    ClearRound();
                    return Result(frame, action, -1, -1, false, completed);
                }
                ProcessPair(frame.Contacts);
            }
            else
            {
                var contact = frame.Contacts[0];
                if (state == GestureState.Single)
                {
                    double movement = Distance(downPosition, contact.Position);
                    if (viewport.HitTest(contact.Position) != initialCell) clickEligible = false;
                    if (movement > MovementThreshold)
                    {
                        clickEligible = false;
                        state = GestureState.PanOnly;
                        if (!initialOverview) viewport.Pan(contact.Position.X - downPosition.X, contact.Position.Y - downPosition.Y);
                    }
                    held |= frame.TimeMilliseconds - downMilliseconds >= HoldMilliseconds;
                    if (clickEligible && held && !secondaryButton && !initialOverview)
                    {
                        // Consume cell eligibility before delivering the action. A held finger may
                        // still pan or join a pair, but cannot toggle again or reveal on release.
                        clickEligible = false;
                        if (frame.CellAllowed && canToggleFlag(initialCell))
                        {
                            action = MinesweeperGestureAction.ToggleFlag;
                            actionCell = initialCell;
                        }
                    }
                    if (contact.Phase == MinesweeperInputPhase.Ended && clickEligible)
                    {
                        if (initialOverview)
                        {
                            if (!held && !secondaryButton && viewport.LocateFromOverview(contact.Position))
                                action = MinesweeperGestureAction.OverviewLocated;
                        }
                        else if (frame.CellAllowed)
                        {
                            if (secondaryButton)
                            {
                                if (canToggleFlag(initialCell)) action = MinesweeperGestureAction.ToggleFlag;
                            }
                            else if (!held) action = MinesweeperGestureAction.ShortPress;
                        }
                        if (action != MinesweeperGestureAction.None) actionCell = initialCell;
                    }
                }
                else if (state == GestureState.PanOnly && !initialOverview)
                    viewport.Pan(contact.Position.X - lastSinglePosition.X, contact.Position.Y - lastSinglePosition.Y);
                lastSinglePosition = contact.Position;
            }

            tracked.Clear();
            for (int i = 0; i < frame.Contacts.Count; i++)
            {
                var contact = frame.Contacts[i];
                if (IsLive(contact)) tracked.Add(contact.Id, contact);
            }
            if (tracked.Count == 0)
            {
                completed = true;
                previewCell = -1;
                // Capture the request identity/mode before clearing the round.
                var result = Result(frame, action, actionCell, previewCell,
                    Changed(priorScale, priorColumn, priorRow, priorOverview), completed);
                ClearRound();
                return result;
            }
            if (state == GestureState.Pair && tracked.Count == 1)
            {
                state = GestureState.PanOnly;
                foreach (var contact in tracked.Values) lastSinglePosition = contact.Position;
            }
            return Result(frame, action, actionCell, previewCell,
                Changed(priorScale, priorColumn, priorRow, priorOverview), completed);
        }

        private bool Validate(MinesweeperInputFrame frame)
        {
            if (!frame.ViewAllowed || frame.TimeMilliseconds < lastMilliseconds || frame.Contacts.Count > 2) return false;
            if (state != GestureState.Idle && (frame.GameId != initialGameId || frame.FlagMode != initialFlagMode || viewport.IsOverview != initialOverview)) return false;
            var ids = new HashSet<int>();
            for (int i = 0; i < frame.Contacts.Count; i++)
            {
                var contact = frame.Contacts[i];
                if (!ids.Add(contact.Id) || !viewport.Contains(contact.Position) ||
                    contact.Phase == MinesweeperInputPhase.Canceled || !Enum.IsDefined(typeof(MinesweeperInputPhase), contact.Phase)) return false;
                bool existed = tracked.ContainsKey(contact.Id);
                if ((existed && contact.Phase == MinesweeperInputPhase.Began) ||
                    (!existed && contact.Phase != MinesweeperInputPhase.Began)) return false;
                if (existed && tracked[contact.Id].SecondaryButton != contact.SecondaryButton) return false;
            }
            foreach (int id in tracked.Keys) if (!ids.Contains(id)) return false;
            return true;
        }

        private void StartRound(MinesweeperInputFrame frame)
        {
            initialGameId = frame.GameId; initialFlagMode = frame.FlagMode; initialOverview = viewport.IsOverview;
            var first = frame.Contacts[0];
            initialCell = viewport.HitTest(first.Position);
            downPosition = first.Position; lastSinglePosition = first.Position; downMilliseconds = frame.TimeMilliseconds;
            secondaryButton = first.SecondaryButton;
            clickEligible = initialCell >= 0 && (frame.CellAllowed || initialOverview);
            held = false;
            state = GestureState.Single;
        }

        private void ProcessPair(IReadOnlyList<MinesweeperInputContact> contacts)
        {
            var first = contacts[0].Position;
            var second = contacts[1].Position;
            var midpoint = new MinesweeperInputPosition((first.X + second.X) / 2, (first.Y + second.Y) / 2);
            double distance = Distance(first, second);
            bool joined = state != GestureState.Pair || contacts[0].Phase == MinesweeperInputPhase.Began || contacts[1].Phase == MinesweeperInputPhase.Began;
            if (!joined)
            {
                double ratio = lastDistance >= MovementThreshold && distance >= MovementThreshold ? distance / lastDistance : 1;
                viewport.Transform(lastMidpoint, midpoint, ratio);
            }
            // Update even when scale or pan is clamped. Reversal must respond on the next frame.
            lastMidpoint = midpoint;
            lastDistance = distance;
            state = GestureState.Pair;
        }

        private bool Changed(double scale, double column, double row, bool overview)
        { return scale != viewport.ZoomScale || column != viewport.CenterColumn || row != viewport.CenterRow || overview != viewport.IsOverview; }

        private MinesweeperGestureResult Result(MinesweeperInputFrame frame, MinesweeperGestureAction action, int cell,
            int preview, bool viewChanged, bool completed)
        { return new MinesweeperGestureResult(action, cell, initialGameId ?? frame.GameId, initialGameId == null ? frame.FlagMode : initialFlagMode, preview, viewChanged, completed); }

        private void ClearRound()
        {
            state = GestureState.Idle; tracked.Clear(); clickEligible = false; held = false;
            initialCell = -1; initialGameId = null;
        }

        private static bool IsLive(MinesweeperInputContact contact)
        { return contact.Phase != MinesweeperInputPhase.Ended && contact.Phase != MinesweeperInputPhase.Canceled; }
        private static bool HasLiveContacts(IReadOnlyList<MinesweeperInputContact> contacts)
        { for (int i = 0; i < contacts.Count; i++) if (IsLive(contacts[i])) return true; return false; }
        private static double Distance(MinesweeperInputPosition a, MinesweeperInputPosition b)
        { double x = a.X - b.X, y = a.Y - b.Y; return Math.Sqrt(x * x + y * y); }
    }
}
