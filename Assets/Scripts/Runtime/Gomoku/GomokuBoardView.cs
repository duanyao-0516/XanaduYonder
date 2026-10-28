using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Gomoku;

namespace XanaduYonder.Runtime.Gomoku
{
    internal sealed class GomokuBoardView
    {
        private readonly GomokuPointer pointer;
        private readonly GomokuBoardGraphic graphic;
        private readonly Text[,] columns = new Text[15, 2], rows = new Text[15, 2];
        private GomokuGame game;
        private int moveCount = -1, lastMove = -1;
        private GomokuState state;
        private bool interactable;

        public event Action<int> Selected;
        public bool Interactable
        {
            get { return interactable; }
            set { interactable = value; pointer.Interactable = value && game != null && game.State == GomokuState.Playing; }
        }

        public GomokuBoardView(RectTransform surface)
        {
            if (surface == null) throw new ArgumentNullException("surface");
            var paper = UiFactory.Panel(surface, "Gomoku paper", 0, 0, 648, 648, UiFactory.Hex("EAE7D9"));
            UiFactory.Stretch(paper.rectTransform);
            var drawing = UiFactory.Rect(surface, "Gomoku intersections and stones", 0, 0, 648, 648);
            UiFactory.Stretch(drawing);
            graphic = drawing.gameObject.AddComponent<GomokuBoardGraphic>();
            graphic.raycastTarget = false;
            for (int i = 0; i < 15; i++)
            {
                float at = (float)(GomokuBoardGeometry.Margin + i * GomokuBoardGeometry.Pitch);
                for (int side = 0; side < 2; side++)
                {
                    columns[i, side] = Axis(surface, GomokuBoardGeometry.ColumnLabel(i), at, side == 0 ? 22 : 626);
                    columns[i, side].name = "Column " + GomokuBoardGeometry.ColumnLabel(i) + (side == 0 ? " top" : " bottom");
                    rows[i, side] = Axis(surface, GomokuBoardGeometry.RowLabel(i), side == 0 ? 22 : 626, at);
                    rows[i, side].name = "Row " + GomokuBoardGeometry.RowLabel(i) + (side == 0 ? " left" : " right");
                }
            }
            pointer = surface.gameObject.AddComponent<GomokuPointer>();
            pointer.Initialize(surface, cell => Selected?.Invoke(cell));
        }

        public void Render(GomokuGame value, int? selected)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (!ReferenceEquals(game, value) || moveCount != value.MoveCount || lastMove != value.LastMove || state != value.State)
                CancelInput();
            game = value; moveCount = value.MoveCount; lastMove = value.LastMove; state = value.State;
            int preview = selected.GetValueOrDefault(-1);
            if (preview < 0 || preview >= GomokuGame.CellCount || value.State != GomokuState.Playing ||
                value.GetCell(preview) != GomokuStone.Empty) preview = -1;
            graphic.Render(value, preview);
            for (int i = 0; i < 15; i++)
            for (int side = 0; side < 2; side++)
            {
                Highlight(columns[i, side], preview >= 0 && preview % 15 == i);
                Highlight(rows[i, side], preview >= 0 && preview / 15 == i);
            }
            pointer.Interactable = interactable && value.State == GomokuState.Playing;
        }

        public void CancelInput() { pointer.CancelInput(); }

        private static Text Axis(RectTransform parent, string text, float x, float y)
        {
            var label = UiFactory.Label(parent, text, 0, 0, 40, 32, 22, UiFactory.Hex("657365"), TextAnchor.MiddleCenter);
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(x / 648, 1 - y / 648);
            rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero;
            return label;
        }

        private static void Highlight(Text label, bool selected)
        {
            label.color = selected ? UiFactory.Ink : UiFactory.Hex("657365");
            label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
        }
    }
}
