using UnityEngine;
using UnityEngine.EventSystems;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.Watermelon;

namespace XanaduYonder.EditorTools
{
    public static partial class MiniGameUiChecks
    {
        private static void CheckOutsideTap(Fixture fixture)
        {
            var root = fixture.Page("Outside tap fixture");
            try
            {
                int boardPresses = 0;
                var boardInput = root.gameObject.AddComponent<WatermelonPointer>();
                boardInput.CanAim = () => { boardPresses++; return false; };
                int dismissals = 0;
                var shade = DialogUi.ModalOverlay(root, "Check shade");
                DialogOutsideTap.Bind(shade, () => dismissals++);
                var outside = UiFactory.Rect(root, "Outside position", 8, 8, 24, 24);
                var card = UiFactory.Rect(shade.transform, "Art-only card", 120, 300, 390, 380);
                DialogOutsideTap.Protect(card);
                fixture.Click(card);
                Require(dismissals == 0, "Blank space inside an illustrated card consumes the tap");
                fixture.Click(outside);
                Require(dismissals == 1 && boardPresses == 0,
                    "A tap on the extended backdrop dismisses exactly once without reaching the game board's press handler");

                var background = shade.GetComponentInChildren<DialogBackdrop>().gameObject;
                var receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(background);
                var pointer = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    pointerPressRaycast = new RaycastResult { gameObject = card.gameObject },
                    pointerCurrentRaycast = new RaycastResult { gameObject = background }
                };
                ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
                pointer.pointerPressRaycast = pointer.pointerCurrentRaycast;
                pointer.dragging = true;
                ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
                Require(ExecuteEvents.GetEventHandler<IDragHandler>(background) == receiver && dismissals == 1,
                    "Crossing the card edge and dragging across the shade both preserve the dialog");
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }
    }
}
