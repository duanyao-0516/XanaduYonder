using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    internal sealed class DialogSwitch : MonoBehaviour
    {
        private static readonly Color Off = UiFactory.Hex("78877F");
        private Image left, middle, right;
        private RectTransform thumb;
        private float inset, travel, position;
        public bool Value { get; private set; }

        internal void Initialize(float x, float y, float diameter, float width, bool initial)
        {
            var track = UiFactory.Rect(transform, "Switch track", x, y, width, diameter);
            travel = width - diameter;
            inset = diameter * .1f;
            middle = UiFactory.Panel(track, "Track", diameter / 2, 0, travel, diameter, Off, false);
            left = UiFactory.CirclePanel(track, "Left end", 0, 0, diameter, Off);
            right = UiFactory.CirclePanel(track, "Right end", travel, 0, diameter, Off);
            thumb = UiFactory.CirclePanel(track, "Thumb", inset, inset, diameter - 2 * inset, Color.white).rectTransform;
            Value = initial;
            Paint(initial ? 1 : 0);
        }

        public void SetValueWithoutNotify(bool value)
        {
            Value = value;
            StopAllCoroutines();
            float target = value ? 1 : 0;
            if (Application.isPlaying && isActiveAndEnabled && !Mathf.Approximately(position, target))
                StartCoroutine(Slide(target));
            else Paint(target);
        }

        private IEnumerator Slide(float target)
        {
            while (!Mathf.Approximately(position, target))
            {
                Paint(Mathf.MoveTowards(position, target, Time.unscaledDeltaTime / .14f));
                yield return null;
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (thumb != null) Paint(Value ? 1 : 0);
        }

        private void Paint(float progress)
        {
            position = progress;
            float t = Mathf.SmoothStep(0, 1, progress);
            thumb.anchoredPosition = new Vector2(inset + travel * t, -inset);
            left.color = middle.color = right.color = Color.Lerp(Off, UiFactory.Accent, t);
        }
    }
}
