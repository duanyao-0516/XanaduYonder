using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private void ShowFeedbackHelp(Action after)
        {
            var panel = CreateModal("声音与触感说明", 900);
            float height = panel.rect.height;
            var viewport = UiFactory.Rect(panel, "Feedback help viewport", 32, 114, 532, height - 236);
            var background = viewport.gameObject.AddComponent<Image>();
            background.color = Color.clear;
            background.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiFactory.Rect(viewport, "Feedback help content", 0, 0, 532, 0);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;

            string message = "声音和触感开关在各游戏间共用，可以分别开启或关闭。\n\n" +
                "声音\n打开应用内的“声音”，并调高系统媒体音量。\n\n" +
                "触感\n触感是短促的震动反馈，不是持续震动，也不是每次点击都会触发。" +
                "例如：2048 合并、跳一跳落地或落空、扫雷插旗或拔旗。" +
                "Flappy Bird 在得分或碰撞时反馈，拍翼时没有触感。部分玩法没有触感事件。" +
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
            var label = UiFactory.Label(content, message, 0, 0, 532, 1, 24, UiFactory.Muted, TextAnchor.UpperLeft);
            label.name = "Feedback help text";
            label.supportRichText = false;
            float contentHeight = Mathf.Ceil(label.preferredHeight);
            label.rectTransform.sizeDelta = new Vector2(532, contentHeight);
            content.sizeDelta = new Vector2(532, contentHeight);

            Action back = () => { CloseModal(); OpenSettings(after); };
            UiFactory.Button(panel, "返回设置", 32, height - 100, 532, 74, back, true);
            dismissModal = back;
        }
    }
}
