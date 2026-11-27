using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XanaduYonder.EditorTools
{
    public static partial class MiniGameUiChecks
    {
        private static void CheckMenuFocus(Button sound, Button haptics)
        {
            var events = EventSystem.current;
            var soundColors = sound.colors;
            var hapticsColors = haptics.colors;
            try
            {
                var instant = soundColors;
                instant.fadeDuration = 0;
                sound.colors = instant;
                haptics.colors = instant;
                events.SetSelectedGameObject(sound.gameObject);
                Require(sound.image.canvasRenderer.GetColor() == instant.normalColor,
                    "Opening a menu by touch leaves its initial navigation target in its resting color");

                var right = new AxisEventData(events) { moveDir = MoveDirection.Right };
                ExecuteEvents.Execute(sound.gameObject, right, ExecuteEvents.moveHandler);
                Require(events.currentSelectedGameObject == haptics.gameObject &&
                    haptics.image.canvasRenderer.GetColor() == instant.selectedColor,
                    "A directional key or gamepad move paints the destination focus");

                var finger = new PointerEventData(events) { pointerId = 0, button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(haptics.gameObject, finger, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(haptics.gameObject, finger, ExecuteEvents.pointerDownHandler);
                Require(haptics.image.canvasRenderer.GetColor() == instant.pressedColor,
                    "A finger press still provides immediate feedback");
                ExecuteEvents.Execute(haptics.gameObject, finger, ExecuteEvents.pointerUpHandler);
                Require(events.currentSelectedGameObject == haptics.gameObject &&
                    haptics.image.canvasRenderer.GetColor() == instant.normalColor,
                    "Lifting the finger restores the resting color and preserves the Submit target");
                ExecuteEvents.Execute(haptics.gameObject, finger, ExecuteEvents.pointerExitHandler);
            }
            finally
            {
                sound.colors = soundColors;
                haptics.colors = hapticsColors;
            }
        }
    }
}
