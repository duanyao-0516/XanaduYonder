using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    internal static class DialogChoices
    {
        // Short, mutually exclusive choices; the owning page persists the selected value.
        public static Button[] Create(Transform parent, string name, float x, float y, float width,
            float height, string[] options, int selected, Action<int> changed, int fontSize = 25)
        {
            if (options == null || options.Length < 2 || selected < 0 || selected >= options.Length)
                throw new ArgumentException("A choice group needs multiple options and a valid selection.");

            var track = UiFactory.Panel(parent, name, x, y, width, height, DialogUi.Secondary);
            float inset = Mathf.Min(height * .08f, 6);
            float segment = (width - 2 * inset) / options.Length;
            var buttons = new Button[options.Length];
            var labels = new Text[options.Length];

            void Paint(int index, bool active)
            {
                buttons[index].image.color = active ? Color.white : DialogUi.Secondary;
                labels[index].color = active ? UiFactory.Accent : UiFactory.Ink;
                labels[index].fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            }

            for (int i = 0; i < options.Length; i++)
            {
                int option = i;
                buttons[i] = DialogUi.ActionButton(track.transform, options[i], inset + i * segment,
                    inset, segment, height - 2 * inset, () =>
                    {
                        if (selected == option) return;
                        Paint(selected, false);
                        selected = option;
                        Paint(option, true);
                        changed(option);
                    }, false, fontSize);
                labels[i] = buttons[i].GetComponentInChildren<Text>();
                Paint(i, i == selected);
            }
            return buttons;
        }
    }
}
