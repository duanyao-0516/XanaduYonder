using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core;

namespace XanaduYonder.Runtime
{
    internal sealed class BoardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private const float CellSize = 135;
        private const float Pitch = 147;
        private readonly List<RectTransform> tiles = new List<RectTransform>();
        private Action<Direction> onSwipe;
        private Func<bool> canInteract;
        private RectTransform rect;
        private readonly SwipeGesture gesture = new SwipeGesture();

        public void Initialize(Action<Direction> swipe, Func<bool> acceptsInput)
        {
            onSwipe = swipe;
            canInteract = acceptsInput;
            rect = (RectTransform)transform;
            for (var i = 0; i < 16; i++)
            {
                var position = Position(i);
                UiFactory.Panel(transform, "Empty cell " + i, position.x, -position.y, CellSize, CellSize, UiFactory.Hex("D9E1D3"));
            }
        }

        private static Vector2 Position(int index)
        {
            return new Vector2(16 + index % 4 * Pitch, -(16 + index / 4 * Pitch));
        }

        public void Render(GameSnapshot snapshot)
        {
            ClearTiles();
            for (var i = 0; i < 16; i++) if (snapshot.cells[i] > 0) CreateTile(i, snapshot.cells[i]);
        }

        public IEnumerator Animate(MoveResult move, GameSnapshot snapshot)
        {
            gesture.Cancel();
            ClearTiles();
            foreach (var motion in move.Motions) CreateTile(motion.FromIndex, motion.Value);
            var elapsed = 0f;
            const float duration = .13f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / duration), 3);
                for (var i = 0; i < tiles.Count; i++)
                    tiles[i].anchoredPosition = Vector2.Lerp(Position(move.Motions[i].FromIndex), Position(move.Motions[i].ToIndex), t);
                yield return null;
            }
            Render(snapshot);
            var activeIndices = new List<int>();
            for (var i = 0; i < 16; i++) if (snapshot.cells[i] > 0) activeIndices.Add(i);
            elapsed = 0;
            while (elapsed < .1f)
            {
                elapsed += Time.unscaledDeltaTime;
                for (var i = 0; i < tiles.Count; i++)
                {
                    var index = activeIndices[i];
                    var merged = move.Motions.Exists(m => m.ToIndex == index && m.Merged);
                    var scale = merged ? 1 + .06f * Mathf.Sin(Mathf.Clamp01(elapsed / .1f) * Mathf.PI) : 1;
                    if (index == move.SpawnIndex) scale = Mathf.Lerp(.65f, 1, Mathf.Clamp01(elapsed / .1f));
                    tiles[i].localScale = Vector3.one * scale;
                }
                yield return null;
            }
            foreach (var tile in tiles) tile.localScale = Vector3.one;
        }

        private RectTransform CreateTile(int index, int value)
        {
            var position = Position(index);
            var panel = UiFactory.Panel(transform, "Tile " + value, position.x, -position.y, CellSize, CellSize, TileColor(value));
            var text = UiFactory.Label(panel.transform, value.ToString(), 3, 0, CellSize - 6, CellSize,
                value >= 16384 ? 29 : value >= 1024 ? 37 : value >= 128 ? 44 : 53,
                value >= 16 ? Color.white : UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = text.fontSize;
            var tile = (RectTransform)panel.transform;
            tiles.Add(tile);
            return tile;
        }

        private static Color TileColor(int value)
        {
            switch (value)
            {
                case 2: return UiFactory.Hex("F5F1D9");
                case 4: return UiFactory.Hex("E4E9B8");
                case 8: return UiFactory.Hex("A8C89C");
                case 16: return UiFactory.Hex("79AB81");
                case 32: return UiFactory.Hex("4E8E6D");
                case 64: return UiFactory.Hex("326F56");
                case 128: return UiFactory.Hex("D9A34B");
                case 256: return UiFactory.Hex("CD8640");
                case 512: return UiFactory.Hex("C36C39");
                case 1024: return UiFactory.Hex("AD5637");
                case 2048: return UiFactory.Hex("945039");
                default: return UiFactory.Hex("384B59");
            }
        }

        private void ClearTiles()
        {
            foreach (var tile in tiles)
            {
                tile.gameObject.SetActive(false);
                Destroy(tile.gameObject);
            }
            tiles.Clear();
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !canInteract()) return;
            Vector2 start;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out start))
                gesture.Begin(data.pointerId, start.x, start.y);
        }

        public void OnPointerUp(PointerEventData data)
        {
            Vector2 end;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out end))
            {
                gesture.Cancel();
                return;
            }
            Direction direction;
            if (gesture.End(data.pointerId, end.x, end.y, WasCancelled(data.pointerId), out direction)) onSwipe(direction);
        }

        private static bool WasCancelled(int id)
        {
            if (id < 0) return false; // Mouse pointers in the Editor.
            for (var i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.fingerId == id) return touch.phase == TouchPhase.Canceled;
            }
            return true; // A disappeared touch must not become a completed gesture.
        }

        public void CancelGesture() { gesture.Cancel(); }
        private void OnDisable() { gesture.Cancel(); }
    }
}
