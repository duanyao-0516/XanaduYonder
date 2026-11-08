using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    // Owns presentation timing only. Each page reserves its own review control space.
    internal sealed class ResultPresentation : MonoBehaviour
    {
        private readonly ResultPresentationState state = new ResultPresentationState();
        private RectTransform slot;
        private Graphic replacement;
        private bool replacementWasEnabled;
        private bool usesExistingAction;
        private Func<bool> canShow;
        private Action showResult;
        private Button reopen;
        private bool applicationPaused, applicationFocused = true;

        public static ResultPresentation For(MonoBehaviour owner, RectTransform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var value = owner.GetComponent<ResultPresentation>();
            return value != null ? value : owner.gameObject.AddComponent<ResultPresentation>();
        }

        // The page owns this slot's position, size and responsive layout. A decoration
        // may occupy it during ordinary play; only its Graphic is hidden during review.
        public void SetReopenSlot(RectTransform reservedSlot, Graphic ordinaryDecoration = null)
        {
            if (reservedSlot == null) throw new ArgumentNullException(nameof(reservedSlot));
            if (slot == reservedSlot && replacement == ordinaryDecoration && !usesExistingAction) return;
            ReleaseControl();
            slot = reservedSlot;
            replacement = ordinaryDecoration;
            replacementWasEnabled = replacement != null && replacement.enabled;
            usesExistingAction = false;
            RenderReviewControl(state.IsInspecting && Available);
        }

        // Pages with an established result action (for example Gomoku's confirmation
        // row) keep that action and call Reopen themselves. No duplicate control is added.
        public void UseExistingReopenAction()
        {
            ReleaseControl();
            slot = null;
            replacement = null;
            usesExistingAction = true;
        }

        public static void Clear(MonoBehaviour owner)
        {
            var value = owner.GetComponent<ResultPresentation>();
            if (value != null) value.Reset();
        }

        public static bool IsInspecting(MonoBehaviour owner)
        {
            var value = owner.GetComponent<ResultPresentation>();
            return value != null && value.state.IsInspecting;
        }

        private bool Available => !applicationPaused && applicationFocused && canShow != null && canShow();

        public bool Gate(object identity, Func<bool> available, Action show)
        {
            canShow = available;
            showResult = show;
            bool result = state.Request(identity);
            if (result) RenderReviewControl(false);
            return result;
        }

        public void Inspect(Action closeResult)
        {
            state.Inspect();
            closeResult?.Invoke();
            RenderReviewControl(Available);
        }

        public void Reset()
        {
            state.Reset();
            canShow = null;
            showResult = null;
            ReleaseControl();
        }

        private void ReleaseControl()
        {
            if (replacement != null) replacement.enabled = replacementWasEnabled;
            if (reopen == null) return;
            reopen.gameObject.SetActive(false);
            Destroy(reopen.gameObject);
            reopen = null;
        }

        private void RenderReviewControl(bool visible)
        {
            if (usesExistingAction) return;
            if (visible && slot == null)
                throw new InvalidOperationException("The page must reserve a result review slot before inspection.");
            if (visible && reopen == null)
            {
                reopen = UiFactory.Button(slot, "查看结算", 0, 0, slot.rect.width, slot.rect.height, Reopen, true);
                reopen.name = "Reopen result";
                UiFactory.Stretch(reopen.GetComponent<RectTransform>());
            }
            if (replacement != null) replacement.enabled = visible ? false : replacementWasEnabled;
            if (reopen == null) return;
            reopen.gameObject.SetActive(visible);
            if (visible)
                reopen.GetComponentInChildren<Text>().fontSize = Mathf.Max(12, Mathf.RoundToInt(slot.rect.height * .43f));
        }

        internal void Reopen()
        {
            if (!Available) return;
            state.Reopen();
            RenderReviewControl(false);
            showResult?.Invoke();
        }

        private void Update() { Advance(Time.unscaledDeltaTime); }

        // Editor checks drive the same foreground presentation path as Update.
        internal void Advance(float deltaSeconds)
        {
            bool available = Available;
            RenderReviewControl(state.IsInspecting && available);
            if (state.Advance(deltaSeconds, available)) showResult?.Invoke();
        }

        private void OnDisable() { Reset(); }
        private void OnEnable() { state.Interrupt(); }
        private void OnApplicationPause(bool value) { applicationPaused = value; state.Interrupt(); }
        private void OnApplicationFocus(bool value) { applicationFocused = value; state.Interrupt(); }
        private void OnDestroy() { Reset(); }
    }
}
