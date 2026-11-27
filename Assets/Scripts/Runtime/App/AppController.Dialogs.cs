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
            Action finish = () => { CloseModal(); if (after != null) after(); else CheckEndState(); };
            bool tetrisSettings = activePage == Page.Tetris && tetrisPage != null;
            bool poetrySettings = activePage == Page.Feihualing && feihualingPage != null && feihualingPage.ShowsAutoCompanionSetting;
            var panel = CreateSheet("设置", tetrisSettings || poetrySettings ? 530 : 428);
            float width = panel.rect.width - 64;
            DialogUi.FeedbackSwitches(panel, 32, 98, width, 98, 16, data.SoundEnabled, data.HapticsEnabled, value =>
            {
                SetFeedbackSetting(true, value);
                goldMinerPage?.SettingsChanged();
            }, value => SetFeedbackSetting(false, value));
            DialogUi.Disclosure(panel, "声音与触感说明", 32, 212, width, 84, () => ShowFeedbackHelp(after));
            if (tetrisSettings)
                DialogUi.SwitchRow(panel, "方向与旋转对调", 32, 312, width, 84, tetrisPage.SwapControls,
                    tetrisPage.SetSwapControls, tetrisPage.CanChangeSwapControls);
            if (poetrySettings)
                DialogUi.SwitchRow(panel, "离线陪练", 32, 312, width, 84, feihualingPage.AutoCompanionEnabled,
                    feihualingPage.SetAutoCompanion);
            DialogUi.ActionButton(panel, "完成", 32, panel.rect.height - 116, width, 88, finish, true);
            SetModalBack(finish);
        }

        private void ShowDialog(string title, string message, string primary, Action primaryAction,
            string secondary = null, Action secondaryAction = null)
        {
            ShowMessageDialog(title, message, primary, primaryAction, secondary, secondaryAction, false);
        }

        private void ShowMessageDialog(string title, string message, string primary, Action primaryAction,
            string secondary, Action secondaryAction, bool destructive)
        {
            var panel = CreateModal(title);
            const float width = 532;
            bool stack = secondary != null && (primary.Length > 8 || secondary.Length > 8);
            float footer = panel.rect.height - (stack ? 216 : 116);
            DialogUi.ScrollText(panel, message, 32, 112, width, footer - 136, 24);
            Action confirm = () => { CloseModal(); primaryAction(); };
            if (secondary == null)
                DialogUi.ActionButton(panel, primary, 32, footer, width, 88, confirm, true, destructive: destructive);
            else
            {
                Action cancel = () => { CloseModal(); secondaryAction(); };
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
            dismissModal = () =>
            {
                CloseModal();
                if (secondaryAction != null) secondaryAction();
                else CheckEndState();
            };
            modalBackReturnsToLobby = secondaryAction == (Action)ShowLobby;
        }

        private RectTransform CreateModal(string title, float requestedHeight = 488, Action outsideDismiss = null)
        {
            var frame = CreateDialogFrame(DialogUi.Scrim, outsideDismiss);
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

        private RectTransform CreateSheet(string title, float height, Action dismiss = null)
        {
            var frame = CreateDialogFrame(DialogUi.Scrim, dismiss);
            var panel = UiFactory.Panel(frame, "Dialog", 0, 0, 720, height, DialogUi.Surface, false);
            DialogCorners.ApplyTop(panel, DialogCorners.Sheet);
            panel.raycastTarget = true;
            var content = UiFactory.Rect(panel.transform, "Sheet content", 0, 0, 720, height);
            DialogSheetLayout.Attach(panel.rectTransform, content, 720, height);
            UiFactory.Label(content, title, 32, 18, 656, 64, 33, UiFactory.Ink,
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
            if (outsideDismiss != null)
            {
                // Keep the dismiss button behind, rather than above, the panel. A click
                // on panel whitespace must not bubble to a parent dismiss handler.
                var backdrop = UiFactory.Panel(overlay.transform, "Dismiss outside dialog", 0, 0, 720, 1320, Color.clear, false);
                UiFactory.Stretch(backdrop.rectTransform);
                backdrop.raycastTarget = true;
                var dismiss = backdrop.gameObject.AddComponent<Button>();
                dismiss.targetGraphic = backdrop;
                dismiss.transition = Selectable.Transition.None;
                dismiss.navigation = new Navigation { mode = Navigation.Mode.None };
                dismiss.onClick.AddListener(() => outsideDismiss());
            }
            var modalFrame = UiFactory.Rect(modal.transform, "Dialog safe area", 0, 0, 720, 1320);
            modalFrame.gameObject.AddComponent<SafeDesignFrame>().Initialize(modalFrame, canvasRoot);
            return modalFrame;
        }

        private void CloseModal()
        {
            if (modal != null)
            {
                modal.SetActive(false);
                Destroy(modal);
                modal = null;
            }
            dismissModal = null;
            modalBackReturnsToLobby = false;
        }
    }
}
