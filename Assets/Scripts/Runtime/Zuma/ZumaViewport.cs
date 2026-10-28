using UnityEngine;

namespace XanaduYonder.Runtime.Zuma
{
    internal sealed class ZumaViewport : MonoBehaviour
    {
        private RectTransform frame;
        private Rect previousSafe;
        private Vector2 previousSize;
        public ZumaViewportRect Bounds { get; private set; }
        public event System.Action Changed;
        public void Initialize(RectTransform designFrame) { frame=designFrame; Refresh(); }
        private void Update()
        {
            if (previousSafe != Screen.safeArea || previousSize != new Vector2(Screen.width,Screen.height)) Refresh();
        }
        public void Refresh()
        {
            if (frame == null) return;
            var safe=Screen.safeArea;
            previousSafe=safe; previousSize=new Vector2(Screen.width,Screen.height);
            Bounds=ZumaViewportMath.Fit(safe.x,safe.y,safe.width,safe.height);
            frame.anchorMin=frame.anchorMax=new Vector2(0,0);
            frame.pivot=new Vector2(0,1);
            frame.sizeDelta=new Vector2(1280,720);
            frame.anchoredPosition=new Vector2(Bounds.Left,Bounds.Bottom+Bounds.Height);
            frame.localScale=Vector3.one*(Bounds.Width/1280f);
            Changed?.Invoke();
        }
    }
}
