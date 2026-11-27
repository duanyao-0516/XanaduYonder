using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private void OpenSettings() { OpenSettings(null); }

        private void OpenSettings(Action after)
        {
            bool tetrisSettings = activePage == Page.Tetris && tetrisPage != null;
            bool poetrySettings = activePage == Page.Feihualing && feihualingPage != null && feihualingPage.ShowsAutoCompanionSetting;
            var panel = CreateSheet("设置", 440, after ?? CheckEndState);
            var sections = new DialogSheetSections(panel);
            AddFeedbackControls(panel, sections);
            if (tetrisSettings)
            {
                sections.Heading("操作");
                sections.Switch("方向与旋转对调", tetrisPage.SwapControls, tetrisPage.SetSwapControls, tetrisPage.CanChangeSwapControls);
            }
            if (poetrySettings)
            {
                sections.Heading("陪练");
                sections.Switch("离线陪练", feihualingPage.AutoCompanionEnabled, feihualingPage.SetAutoCompanion);
            }
            sections.Heading("声音与触感说明");
            sections.Body(FeedbackGuidance);
            sections.Action("完成", dismissModal, true);
            sections.Finish();
        }

        private void AddFeedbackControls(RectTransform panel, DialogSheetSections sections)
        {
            sections.Heading("声音与触感");
            DialogUi.FeedbackSwitches(panel, sections.Inset, sections.Reserve(88), sections.Width, 88, 16,
                data.SoundEnabled, data.HapticsEnabled, value =>
                {
                    SetFeedbackSetting(true, value);
                    goldMinerPage?.SettingsChanged();
                }, value => SetFeedbackSetting(false, value), 23);
        }

        private void ShowDialog(string title, string message, string primary, Action primaryAction,
            string secondary = null, Action secondaryAction = null)
        {
            ShowMessageDialog(title, message, primary, primaryAction, secondary, secondaryAction, false);
        }

        private void ShowMessageDialog(string title, string message, string primary, Action primaryAction,
            string secondary, Action secondaryAction, bool destructive, Action outsideAction = null)
        {
            Action cancel = () =>
            {
                CloseModal();
                if (secondaryAction != null) secondaryAction();
                else CheckEndState();
            };
            bool hasMessage = !string.IsNullOrWhiteSpace(message);
            var panel = CreateModal(title, outsideDismiss: outsideAction == null ? cancel : () => { CloseModal(); outsideAction(); });
            const float width = 532;
            bool stack = secondary != null && (primary.Length > 8 || secondary.Length > 8);
            float footerHeight = stack ? 214 : 116;
            float footer = 124;
            if (hasMessage)
            {
                const float bodyTop = 112, gap = 24;
                float maxHeight = panel.rect.height - bodyTop - gap - footerHeight;
                var heading = panel.GetComponentInChildren<Text>();
                // The message card has no trailing close icon. Its title and message share the entire text column.
                heading.rectTransform.anchoredPosition = new Vector2(32, heading.rectTransform.anchoredPosition.y);
                heading.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                var body = DialogUi.ScrollText(panel, message, 32, bodyTop, width, maxHeight, 24, fitHeight: true, heading: heading);
                footer = bodyTop + Mathf.Min(body.rectTransform.rect.height, maxHeight) + gap;
            }
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, footer + footerHeight);
            Action confirm = () => { CloseModal(); primaryAction(); };
            if (secondary == null)
                DialogUi.ActionButton(panel, primary, 32, footer, width, 88, confirm, true, destructive: destructive);
            else
            {
                if (stack)
                {
                    DialogUi.ActionButton(panel, secondary, 32, footer + 98, width, 88, cancel);
                    DialogUi.ActionButton(panel, primary, 32, footer, width, 88, confirm, true, destructive: destructive);
                }
                else
                {
                    DialogUi.ActionButton(panel, secondary, 32, footer, 258, 88, cancel);
                    DialogUi.ActionButton(panel, primary, 306, footer, 258, 88, confirm, true, destructive: destructive);
                }
            }
            dismissModal = cancel;
            modalBackReturnsToLobby = secondaryAction == (Action)ShowLobby;
        }

        private RectTransform CreateModal(string title, float requestedHeight = 488, Action outsideDismiss = null)
        {
            var frame = CreateDialogFrame(DialogUi.Scrim, outsideDismiss ?? (() => (dismissModal ?? CloseModal)()));
            var height = Mathf.Clamp(requestedHeight, 300, Mathf.Max(488, frame.rect.height - 64));
            const float width = 596;
            var panel = DialogCorners.Panel(frame, "Dialog", 0, 0, width, height, DialogUi.Surface, DialogCorners.Surface);
            panel.raycastTarget = true;
            UiFactory.Center(panel.rectTransform);
            float inset = outsideDismiss != null ? 98 : 32;
            UiFactory.Label(panel.transform, title, inset, 36, width - 2 * inset, 62, 33, UiFactory.Ink,
                TextAnchor.MiddleCenter, FontStyle.Bold).supportRichText = false;
            return panel.rectTransform;
        }

        private RectTransform CreateReadingModal(string title, float height, Action outsideDismiss = null)
        {
            var panel = CreateModal(title, height, outsideDismiss);
            DialogUi.AlignReadingTitle(panel);
            return panel;
        }

        private RectTransform CreateSheet(string title, float height, Action onDismissed = null)
        {
            var frame = CreateDialogFrame(DialogUi.Scrim, () => dismissModal?.Invoke());
            SetModalBack(SheetAction(onDismissed));
            var panel = UiFactory.Panel(frame, "Dialog", 0, 0, 720, height, DialogUi.Surface, false);
            DialogCorners.ApplyTop(panel, DialogCorners.Sheet);
            panel.raycastTarget = true;
            var content = UiFactory.Rect(panel.transform, "Sheet content", 0, 0, 720, height);
            DialogSheetLayout.Attach(panel.rectTransform, content, 720, height);
            UiFactory.Label(content, title, 40, 18, 640, 64, 28, UiFactory.Ink,
                TextAnchor.MiddleLeft, FontStyle.Bold).supportRichText = false;
            return content;
        }

        private RectTransform CreateDialogFrame(Color background, Action outsideDismiss = null)
        {
            if (inGame || activePage == Page.Lobby) Settle2048Animation();
            else StopAnimation();
            CloseModal();
            var overlay = DialogUi.ModalOverlay(canvasRoot, "Dialog overlay", background);
            modal = overlay.gameObject;
            DialogOutsideTap.Bind(overlay, () =>
            {
                if (modal == overlay.gameObject) (outsideDismiss ?? CloseModal)();
            });
            var modalFrame = UiFactory.Rect(modal.transform, "Dialog safe area", 0, 0, 720, 1320);
            modalFrame.gameObject.AddComponent<SafeDesignFrame>().Initialize(modalFrame, canvasRoot);
            return modalFrame;
        }

        private Action SheetAction(Action after)
        {
            var owner = modal;
            return () => DismissSheet(owner, after);
        }

        private void DismissSheet(GameObject owner, Action after = null)
        {
            if (owner == null || modal != owner) return;
            DialogSheetLayout.Dismiss(owner, () =>
            {
                if (modal != owner) return;
                CloseModal();
                after?.Invoke();
            });
        }

        private void CloseModal()
        {
            if (modal != null)
            {
                DialogFocusScope.ObserveVisible(modal.transform);
                modal.SetActive(false);
                Destroy(modal);
                modal = null;
            }
            dismissModal = null;
            modalBackReturnsToLobby = false;
        }
    }
}
