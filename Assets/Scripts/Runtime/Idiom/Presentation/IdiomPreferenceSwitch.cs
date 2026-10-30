using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Idiom
{
    internal sealed class IdiomPreferenceSwitch : MonoBehaviour
    {
        private RectTransform thumb;
        private Image track, left, right;
        private float position, destination;
        private bool value;

        public static void Create(Transform parent, string caption, float y, bool initial, bool enabled, Action<bool> changed)
        {
            IdiomUi.Label(parent, caption, 32, y, 410, 58, 24);
            var root = UiFactory.Rect(parent, caption + " switch", 468, y + 5, 96, 48);
            var surface = root.gameObject.AddComponent<Image>();
            surface.color = Color.clear;
            var component = root.gameObject.AddComponent<IdiomPreferenceSwitch>();
            component.track = UiFactory.Panel(root, "Track", 24, 0, 48, 48, UiFactory.Accent, false);
            component.left = UiFactory.CirclePanel(root, "Left end", 0, 0, 48, UiFactory.Accent);
            component.right = UiFactory.CirclePanel(root, "Right end", 48, 0, 48, UiFactory.Accent);
            component.thumb = UiFactory.CirclePanel(root, "Thumb", 4, 4, 40, Color.white).rectTransform;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = surface; button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.interactable = enabled;
            component.value = initial; component.position = component.destination = initial ? 1 : 0;
            component.Paint();
            button.onClick.AddListener(() =>
            {
                component.value = !component.value;
                component.destination = component.value ? 1 : 0;
                changed(component.value);
            });
        }

        private void Update()
        {
            position = Mathf.MoveTowards(position, destination, Time.unscaledDeltaTime / .16f);
            Paint();
        }

        private void Paint()
        {
            var eased = position * position * (3 - 2 * position);
            thumb.anchoredPosition = new Vector2(4 + 48 * eased, -4);
            track.color = left.color = right.color = Color.Lerp(UiFactory.Hex("CBD5C8"), UiFactory.Accent, eased);
        }
    }
}
