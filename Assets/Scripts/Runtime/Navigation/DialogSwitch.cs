using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    internal sealed class DialogSwitch : MonoBehaviour
    {
        private static readonly Color Off = UiFactory.Hex("78877F");
        private Image track;
        private RectTransform thumb;
        private float inset, travel, position;
        public bool Value { get; private set; }

        internal void Initialize(float x, float y, float diameter, float width, bool initial)
        {
            track = DialogCorners.Panel(transform, "Switch track", x, y, width, diameter, Off, DialogCorners.Pill(diameter));
            travel = width - diameter;
            inset = diameter * .1f;
            thumb = UiFactory.CirclePanel(track.transform, "Thumb", inset, inset, diameter - 2 * inset, Color.white).rectTransform;
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
            track.color = Color.Lerp(Off, UiFactory.Accent, t);
        }
    }
}
