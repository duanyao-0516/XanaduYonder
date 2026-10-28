using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.HappyBall
{
    internal sealed partial class HappyBallPage
    {
        private RectTransform stageRect;
        private RawImage stageImage;
        private Text scoreText, bestText, pauseText, errorText, overlayTitle, overlayDetail, resumeText;
        private GameObject pauseOverlay, errorPanel;
        private Button pauseButton;

        private void Build(RectTransform root)
        {
            var navigation = UiFactory.Band(root, "HappyBall navigation", 0, 92, 0);
            UiFactory.IconButton(navigation, UiIconKind.Home, 56, 34, 58, 58, returnLobby);
            UiFactory.IconButton(navigation, UiIconKind.Restart, 126, 34, 58, 58, ConfirmRestart);
            var title = UiFactory.Label(navigation, "欢乐球球", 210, 34, 300, 58, 32, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.IconButton(navigation, UiIconKind.Book, 536, 34, 58, 58, ShowHelp);
            UiFactory.IconButton(navigation, UiIconKind.Settings, 606, 34, 58, 58, OpenSettings);

            var band = UiFactory.Band(root, "HappyBall single-line score", 108, 64, 0);
            var panel = UiFactory.Panel(band, "Score strip", 56, 0, 608, 64, UiFactory.Hex("E7EDE4"));
            UiFactory.Label(panel.transform, "本局", 22, 0, 60, 64, 23, UiFactory.Muted);
            scoreText = Number(panel.transform, "Current score", 88, 124);
            UiFactory.Panel(panel.transform, "Score divider", 218, 17, 1, 30, UiFactory.Hex("D1DCD0"), false);
            UiFactory.Label(panel.transform, "最高", 242, 0, 60, 64, 23, UiFactory.Muted);
            bestText = Number(panel.transform, "Best score", 308, 132);
            pauseButton = UiFactory.Button(panel.transform, "暂停", 464, 8, 128, 48, TogglePause);
            pauseButton.GetComponent<Image>().color = UiFactory.Paper;
            pauseText = pauseButton.GetComponentInChildren<Text>(); pauseText.fontSize = 24;

            stageRect = UiFactory.Rect(root, "HappyBall 3D play area", 0, 180, 720, 1140);
            UiFactory.Stretch(stageRect); stageRect.offsetMin = Vector2.zero; stageRect.offsetMax = new Vector2(0, -180);
            stageImage = stageRect.gameObject.AddComponent<RawImage>(); stageImage.color = Color.white; stageImage.raycastTarget = true;

            var cover = UiFactory.Panel(stageRect, "Session cover", 0, 0, 720, 1140, new Color(.965f, .957f, .925f, .88f), false);
            UiFactory.Stretch(cover.rectTransform); cover.raycastTarget = true; pauseOverlay = cover.gameObject;
            var content = UiFactory.Rect(cover.transform, "Session controls", 0, 0, 480, 300); UiFactory.Center(content);
            overlayTitle = UiFactory.Label(content, "", 0, 0, 480, 66, 40, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            overlayDetail = UiFactory.Label(content, "", 0, 70, 480, 44, 23, UiFactory.Muted, TextAnchor.MiddleCenter);
            var resume = UiFactory.Button(content, "开始游戏", 90, 132, 300, 72,
                () => { if (game.GameOver) Restart(); else Resume(); }, true);
            resumeText = resume.GetComponentInChildren<Text>();
            UiFactory.Button(content, "返回大厅", 90, 224, 300, 62, returnLobby);

            var warning = UiFactory.Panel(root, "Save warning", 20, 0, 680, 50, UiFactory.Hex("FFF0CF"));
            warning.rectTransform.anchorMin = warning.rectTransform.anchorMax = new Vector2(.5f, 0);
            warning.rectTransform.pivot = new Vector2(.5f, 0); warning.rectTransform.anchoredPosition = new Vector2(0, 12);
            errorText = UiFactory.Label(warning.transform, "", 12, 0, 656, 50, 21, UiFactory.Ink, TextAnchor.MiddleCenter);
            errorText.resizeTextForBestFit = true; errorText.resizeTextMinSize = 14; errorText.resizeTextMaxSize = 21;
            errorPanel = warning.gameObject; errorPanel.SetActive(false);
        }

        private static Text Number(Transform parent, string name, float x, float width)
        {
            var text = UiFactory.Label(parent, "0", x, 0, width, 64, 33, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            text.name = name; text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 10; text.resizeTextMaxSize = 33;
            return text;
        }
    }
}
