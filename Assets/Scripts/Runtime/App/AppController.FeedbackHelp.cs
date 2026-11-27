using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private void ShowFeedbackHelp(Action after)
        {
            var frame = CreateDialogFrame(DialogUi.Surface);
            var page = UiFactory.Rect(frame, "Feedback help page", 0, 0, 720, 0);
            page.anchorMin = new Vector2(.5f, 0);
            page.anchorMax = new Vector2(.5f, 1);
            page.pivot = new Vector2(.5f, .5f);
            page.anchoredPosition = Vector2.zero;
            Action back = () => { CloseModal(); OpenSettings(after); };
            SetModalBack(back);
            DialogUi.ActionButton(page, "返回设置", 32, 24, 168, 84, back);
            UiFactory.Label(page, "声音与触感说明", 32, 132, 656, 64, 38, UiFactory.Ink,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            string message = "所有游戏共用声音与触感设置，可分别开关。\n\n" +
                "声音\n打开应用内的“声音”，并调高系统媒体音量。\n\n" +
                "触感\n触感是短促的震动反馈，不是持续震动，也不是每次点击都会触发。" +
                "例如 2048 合并、跳一跳落地或落空、扫雷插旗或拔旗时会有触感。" +
                "\nFlappy Bird 在得分或碰撞时反馈。部分玩法没有触感事件。" +
                "设备也需要支持触觉反馈。\n\n";
#if UNITY_IOS
            message += "iPhone 系统设置\n应用内开启触感后，还需开启以下系统选项：\n\n" +
                "Settings → Sounds & Haptics → System Haptics\n" +
                "设置 → 声音与触感 → 系统触感\n\n" +
                "Settings → Accessibility → Touch → Vibration\n" +
                "设置 → 辅助功能 → 触控 → 振动\n\n" +
                "System Haptics 控制本应用使用的标准交互触感；关闭后，即使游戏触感开启，也可能没有震感。" +
                "它不等于禁止所有 App、来电或通知振动。Vibration 则是范围更广的振动总开关。" +
                "不支持触感的设备上，开启这些选项也不会产生震动。";
#elif UNITY_ANDROID
            message += "Android 系统设置\n应用内开启触感后，还需允许系统的触摸反馈或触感反馈。" +
                "选项名称因机型而异，部分设备还可调整触感强度。应用内开关不会覆盖系统设置。";
#else
            message += "系统与设备\n应用内触感开关仍受设备能力和系统触感设置影响。";
#endif
            var viewport = UiFactory.Rect(page, "Reading area", 0, 0, 0, 0);
            UiFactory.Stretch(viewport);
            viewport.offsetMin = new Vector2(32, 48);
            viewport.offsetMax = new Vector2(-32, -220);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = UiFactory.Label(viewport, message, 0, 0, 0, 0, 27, UiFactory.Ink, TextAnchor.UpperLeft);
            text.supportRichText = false;
            text.rectTransform.anchorMax = Vector2.one;
            text.lineSpacing = 1.15f;
            var fit = text.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = text.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = text.fontSize;
        }
    }
}
