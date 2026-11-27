using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime;

namespace XanaduYonder.EditorTools
{
    public static partial class MiniGameUiChecks
    {
        private static void CheckLobbyConfirmations(Fixture fixture, AppController host, RectTransform content)
        {
            var opener = (RectTransform)content.Find("Lobby header/Lobby settings");
            Invoke(host, "HandleBack");
            var overlay = Get<GameObject>(host, "modal");
            var panel = (RectTransform)overlay.transform.Find("Dialog safe area/Dialog");
            var actions = panel.GetComponentsInChildren<Button>();
            Require(actions.Length == 2 && actions[0].name == "取消" && actions[1].name == "退出",
                "The lobby exit question offers Cancel first and an explicit Exit action");
            var captions = panel.GetComponentsInChildren<Text>();
            Require(captions.Length == 3 && captions[0].text == "退出应用？" && panel.Find("Reading area") == null,
                "The exit card contains only the question and its two decisions");
            Require(panel.rect.height < 260 && actions[0].image.rectTransform.rect.height >= 88,
                "A title-only question stays compact while retaining full button touch targets");
            fixture.Capture("dialog-lobby-exit");
            fixture.Click((RectTransform)actions[0].transform);
            Require(Get<GameObject>(host, "modal") == null, "Cancel dismisses the real exit question");

            Invoke(host, "HandleBack");
            fixture.Click(opener);
            Require(Get<GameObject>(host, "modal") == null, "Tapping the exit scrim cancels without opening lobby settings");
            Invoke(host, "HandleBack");
            Invoke(host, "HandleBack");
            Require(Get<GameObject>(host, "modal") == null, "System Back cancels the exit question");

            int accepted = 0;
            Action confirm = () => accepted++;
            Invoke(host, "ShowDialog", "确认操作？", null, "确认", confirm, "取消", null);
            fixture.Click((RectTransform)FindCaption(Get<GameObject>(host, "modal").transform, "确认").transform);
            Require(accepted == 1 && Get<GameObject>(host, "modal") == null, "An explicit confirmation invokes its action once");

            Invoke(host, "ShowDialog", "提示", "一条简短说明。", "知道了", confirm, null, null);
            panel = (RectTransform)Get<GameObject>(host, "modal").transform.Find("Dialog safe area/Dialog");
            var shortText = panel.Find("Reading area").GetComponentInChildren<Text>();
            Require(shortText.alignment == TextAnchor.MiddleCenter && panel.rect.height < 320 &&
                Mathf.Abs(shortText.rectTransform.rect.height - shortText.preferredHeight) <= 1,
                "One-line copy is centered and the panel follows its measured text height");
            Invoke(host, "CloseModal");

            var paragraph = new string('文', 320);
            Invoke(host, "ShowDialog", "说明", paragraph, "确认这项需要说明的操作", confirm, "取消", null);
            panel = (RectTransform)Get<GameObject>(host, "modal").transform.Find("Dialog safe area/Dialog");
            var scroll = panel.GetComponentInChildren<ScrollRect>();
            var accept = (RectTransform)FindCaption(panel, "确认这项需要说明的操作").transform;
            var cancel = (RectTransform)FindCaption(panel, "取消").transform;
            Require(scroll != null && scroll.content.rect.height > scroll.viewport.rect.height &&
                accept.anchoredPosition.y - cancel.anchoredPosition.y >= accept.rect.height &&
                -cancel.anchoredPosition.y + cancel.rect.height <= panel.rect.height,
                "Long copy scrolls within the card and both stacked actions remain visible");
            Invoke(host, "CloseModal");
        }
    }
}
