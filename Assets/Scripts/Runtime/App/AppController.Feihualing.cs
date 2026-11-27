using System;
using UnityEngine;
using XanaduYonder.Runtime.Feihualing;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private LobbyEntry BuildFeihualingEntry()
        {
            return LobbyGame("feihualing", "Feihualing", "飞花令", "33", "文字语言", "诗词接令",
                "循一字，接一行诗。", "单机 / 同机双人 · 学习 / 竞技 / 竞速", "学习 / 竞技 / 竞速",
                EnterFeihualing, FeihualingPage.DrawLobbySeal);
        }

        private void AddPoetryCompanionSetting(RectTransform panel, Action after)
        {
            UiFactory.Button(panel, "离线陪练  ·  " + (feihualingPage.AutoCompanionEnabled ? "开启" : "关闭"), 32, 350, 532, 70, () =>
            {
                feihualingPage.SetAutoCompanion(!feihualingPage.AutoCompanionEnabled);
                CloseModal();
                OpenSettings(after);
            });
        }

        private void EnterFeihualing()
        {
            // This entry is available only from the lobby, so other pages have left
            // and released their input/IME ownership before the new page enters.
            if (activePage != Page.Lobby || modal != null) return;
            if (feihualingPage == null)
            {
                var pageRoot = UiFactory.Rect(frame, "Feihualing page", 0, 0, 720, 1320);
                UiFactory.Stretch(pageRoot);
                feihualingPage = pageRoot.gameObject.AddComponent<FeihualingPage>();
                feihualingPage.Initialize(pageRoot, ShowLobby, OpenSettings,
                    () => activePage == Page.Feihualing && modal == null && !paused && (focused || IsTextKeyboardInForeground()),
                    SetStatus, OpenTextGameModal, CloseModal);
                feihualingPage.AnswerAccepted += () => feedback.Play(false, data.SoundEnabled, data.HapticsEnabled);
            }
            frame.GetComponent<SafeDesignFrame>().WidthOnly = true;
            SetActivePage(Page.Feihualing);
            if (status != null) status.gameObject.SetActive(false);
            feihualingPage.Enter();
        }

        private bool IsTextKeyboardInForeground()
        {
            if (activePage == Page.Idiom) return idiomPage != null && idiomPage.KeyboardVisible && idiomPage.KeyboardForeground;
            if (activePage == Page.Feihualing) return feihualingPage != null && feihualingPage.KeyboardVisible && feihualingPage.KeyboardForeground;
            return false;
        }
    }
}
