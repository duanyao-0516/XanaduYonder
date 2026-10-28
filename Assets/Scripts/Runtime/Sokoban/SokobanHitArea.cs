using UnityEngine;

namespace XanaduYonder.Runtime.Sokoban
{
    /// <summary>The raycaster and raw Android touch routing share exactly the same invisible target.</summary>
    internal sealed class SokobanHitArea : MonoBehaviour,ICanvasRaycastFilter
    {
        internal RectTransform Original;
        internal bool Round;
        public bool IsRaycastLocationValid(Vector2 screen,Camera camera)
        {
            var rect=(RectTransform)transform;
            if(!RectTransformUtility.RectangleContainsScreenPoint(rect,screen,camera)) return false;
            if(!Round||RectTransformUtility.RectangleContainsScreenPoint(Original,screen,camera)) return true;
            Vector2 point; RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,screen,camera,out point);
            var offset=point-rect.rect.center; float radius=Mathf.Min(rect.rect.width,rect.rect.height)/2;
            return offset.sqrMagnitude<=radius*radius;
        }
        internal static bool Contains(RectTransform rect,Vector2 screen,Camera camera)
        { var filter=rect.GetComponent<SokobanHitArea>(); return filter!=null?filter.IsRaycastLocationValid(screen,camera):RectTransformUtility.RectangleContainsScreenPoint(rect,screen,camera); }
    }
}
