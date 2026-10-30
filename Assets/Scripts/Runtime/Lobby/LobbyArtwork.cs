using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    // Reuses existing illustrations without creating game sessions or taking ownership of Resources.
    internal static class LobbyArtwork
    {
        internal static void Build(RectTransform slot, Action<Transform> draw, Vector2 opticalCenterOffset = default)
        {
            var source = UiFactory.Rect(slot, "Large logo artwork", 0, 0, 608, 280);
            UiFactory.Center(source);
            draw(source);
            var corners = new Vector3[4];
            var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            bool found = false;
            foreach (var graphic in source.GetComponentsInChildren<Graphic>())
            {
                graphic.raycastTarget = false;
                graphic.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector2 point = source.InverseTransformPoint(corner);
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                }
                found = true;
            }
            if (!found) return;
            var size = maximum - minimum;
            if (size.x <= 0 || size.y <= 0) return;
            float scale = Mathf.Min(slot.rect.width / size.x, slot.rect.height / size.y);
            source.localScale = Vector3.one * scale;
            source.anchoredPosition = -((minimum + maximum) * .5f + opticalCenterOffset) * scale;
        }
    }
}
