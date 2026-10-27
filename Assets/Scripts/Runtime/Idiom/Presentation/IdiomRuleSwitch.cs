using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Idiom
{
    // Keep both rule labels in their circular endpoints. The selected disc
    // moves, rather than replacing text or changing the control's hit target.
    internal sealed class IdiomRuleSwitch : MonoBehaviour
    {
        private RectTransform thumb;
        private Text character, sound;
        private Button button;
        private float position, destination;
        private bool initialized;

        public static IdiomRuleSwitch Create(Transform parent, float x, float y, Action changed)
        {
            var root = UiFactory.Rect(parent, "Idiom rule switch", x, y, 136, 52);
            var surface = root.gameObject.AddComponent<Image>();
            surface.color = Color.clear;
            var component = root.gameObject.AddComponent<IdiomRuleSwitch>();
            var background = UiFactory.Hex("E7EDE4");
            UiFactory.Panel(root, "Switch track", 26, 0, 84, 52, background, false);
            UiFactory.CirclePanel(root, "Character end", 0, 0, 52, background);
            UiFactory.CirclePanel(root, "Sound end", 84, 0, 52, background);
            component.thumb = UiFactory.CirclePanel(root, "Selected rule", 0, 0, 52, UiFactory.Accent).rectTransform;
            component.character = IdiomUi.Label(root, "字", 0, 0, 52, 52, 24, Color.white, TextAnchor.MiddleCenter, true);
            component.sound = IdiomUi.Label(root, "音", 84, 0, 52, 52, 24, UiFactory.Ink, TextAnchor.MiddleCenter, true);
            component.button = root.gameObject.AddComponent<Button>();
            component.button.targetGraphic = surface;
            component.button.transition = Selectable.Transition.None;
            component.button.navigation = new Navigation { mode = Navigation.Mode.None };
            component.button.onClick.AddListener(() => changed());
            return component;
        }

        public void SetState(bool sameSound, bool interactable, bool animate = true)
        {
            destination = sameSound ? 1 : 0;
            button.interactable = interactable;
            if (!initialized || !animate) position = destination;
            initialized = true;
            Paint();
        }

        private void Update()
        {
            if (Mathf.Approximately(position, destination)) return;
            position = Mathf.MoveTowards(position, destination, Time.unscaledDeltaTime / .16f);
            Paint();
        }

        private void Paint()
        {
            // Smooth the travel without changing either label's geometric center.
            var eased = position * position * (3 - 2 * position);
            thumb.anchoredPosition = new Vector2(84 * eased, 0);
            character.color = Color.Lerp(Color.white, UiFactory.Ink, eased);
            sound.color = Color.Lerp(UiFactory.Ink, Color.white, eased);
        }
    }
}
