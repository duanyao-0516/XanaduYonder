using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    // Presentation only: each game still owns its countdown, input and pause.
    // The parent is the playable board, so navigation and controls stay visible.
    internal sealed class BoardCountdownView
    {
        private readonly Text number;
        public RectTransform Root { get; }

        public BoardCountdownView(RectTransform board, string name)
        {
            var tint = UiFactory.Ink;
            tint.a = .44f;
            var surface = UiFactory.Panel(board, name, 0, 0, 1, 1, tint, false);
            Root = surface.rectTransform;
            UiFactory.Stretch(Root);
            surface.raycastTarget = true;
            number = UiFactory.Label(Root, "", 0, 0, 1, 1, 112, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            number.gameObject.name = "Countdown number";
            UiFactory.Stretch(number.rectTransform);
            number.horizontalOverflow = HorizontalWrapMode.Overflow;
            Hide();
        }

        public void SetNumber(int value)
        {
            if (value <= 0) { Hide(); return; }
            number.text = value.ToString();
            Root.gameObject.SetActive(true);
        }

        public void Hide() { Root.gameObject.SetActive(false); }
    }
}
